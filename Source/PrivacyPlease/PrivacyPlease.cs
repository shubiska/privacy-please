using Verse;
using RimWorld;
using HarmonyLib;
using Verse.AI;
using System.Collections.Generic;

namespace PrivacyPlease
{
    public class PrivacyPleaseMod : Mod
    {
        public PrivacyPleaseMod(ModContentPack content) : base(content)
        {
            new Harmony("Xubisca.PrivacyPlease").PatchAll();
        }
    }

    public class RoomAccessInfo
    {
        public int LastUpdateTick = 0;
        public int OwnerCount = 0;
        public Pawn[] Owners = new Pawn[2];

        public bool Allowed(Pawn pawn)
        {
            return OwnerCount == 0 || Owners[0] == pawn || Owners[1] == pawn;
        }
    }

    public static class RoomAccessCache
    {
        private const int CACHE_INTERVAL = 2500; // 1 in-game hour
        private static readonly Dictionary<Room, RoomAccessInfo> cache = new Dictionary<Room, RoomAccessInfo>();

        public static RoomAccessInfo Get(Room room)
        {
            int tick = Find.TickManager.TicksGame;
            if (!cache.TryGetValue(room, out RoomAccessInfo info))
            {
                info = new RoomAccessInfo();
                cache[room] = info;
            }

            if (tick - info.LastUpdateTick >= CACHE_INTERVAL)
            {
                info.LastUpdateTick = tick;
                Recompute(room, info);
            }

            return info;
        }

        public static void ForceRecompute(Room room)
        {
            int tick = Find.TickManager.TicksGame;
            if (!cache.TryGetValue(room, out RoomAccessInfo info))
            {
                info = new RoomAccessInfo();
                cache[room] = info;
            }

            info.LastUpdateTick = tick;
            Recompute(room, info);
        }

        private static void Recompute(Room room, RoomAccessInfo info)
        {
            info.Owners[0] = null;
            info.Owners[1] = null;
            info.OwnerCount = 0;

            foreach (Building_Bed bed in room.ContainedBeds)
            {
                if (bed.OwnersForReading.Capacity == 0)
                {
                    continue;
                }

                foreach (Pawn pawn in bed.OwnersForReading)
                {
                    if (pawn.IsColonist && info.OwnerCount < 2)
                    {
                        info.Owners[info.OwnerCount] = pawn;
                        info.OwnerCount++;
                    }
                }
            }
        }

        public static bool IsEmergency(Pawn pawn)
        {
            if (pawn.Drafted)
            {
                return true;
            }

            Job job = pawn.CurJob;
            if (job == null)
            {
                return false;
            }

            if (job.playerForced || job.def == JobDefOf.Rescue || job.def == JobDefOf.BeatFire || job.def == JobDefOf.ExtinguishFiresNearby || job.def == JobDefOf.TendPatient)
            {
                return true;
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(CompAssignableToPawn), nameof(CompAssignableToPawn.TryAssignPawn))]
    public static class Patch_TryAssignPawn
    {
        static void Postfix(CompAssignableToPawn __instance)
        {
            if (__instance.parent is Building_Bed bed)
            {
                Room room = bed.GetRoom();
                if (room == null)
                {
                    return;
                }

                RoomAccessCache.ForceRecompute(room);
            }
        }
    }

    [HarmonyPatch(typeof(Region), "Allows")]
    public static class Region_AllowsPatch
    {
        static bool Prefix(Region __instance, TraverseParms tp, bool isDestination, ref bool __result)
        {
            // If the target room is not a bedroom, default behavior
            Room room = __instance.Room;
            if (room == null || room.Role != RoomRoleDefOf.Bedroom)
            {
                return true;
            }

            Pawn pawn = tp.pawn;

            // If invalid or non-human, default behavior
            if (pawn == null || !pawn.RaceProps.Humanlike || pawn.Faction == null)
            {
                return true;
            }

            // If the pawn is not a member...
            if (pawn.Faction != Faction.OfPlayer)
            {
                // If enemy, default behavior
                if (pawn.Faction.RelationWith(Faction.OfPlayer).kind == FactionRelationKind.Hostile)
                {
                    return true;
                }
                // If friendly, prohibit
                else
                {
                    __result = false;
                    return false;
                }
            }

            // Prohibit pawns of going inside claimed bedrooms not owned by them, unless there is an emergency
            RoomAccessInfo info = RoomAccessCache.Get(__instance.Room);
            if (!info.Allowed(pawn) && !RoomAccessCache.IsEmergency(pawn))
            {
                __result = false;
                return false;
            }

            return true;
        }
    }
}
