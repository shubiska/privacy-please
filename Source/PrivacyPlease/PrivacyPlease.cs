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
        public bool IsPrivate = false;
        public int LastUpdateTick = -99999;
        public Pawn[] Owners = new Pawn[2];

        public bool Allowed(Pawn pawn)
        {
            return !IsPrivate || pawn == Owners[0] || pawn == Owners[1];
        }
    }

    public static class RoomAccessCache
    {
        private const int CACHE_INTERVAL = 30;
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

        private static void Recompute(Room room, RoomAccessInfo info)
        {
            info.Owners[0] = null;
            info.Owners[1] = null;
            int ownerCount = 0;
            info.IsPrivate = false;

            foreach (Building_Bed bed in room.ContainedBeds)
            {
                if (bed.Medical || bed.OwnersForReading.Capacity == 0)
                {
                    continue;
                }

                foreach (Pawn pawn in bed.OwnersForReading)
                {
                    if (pawn.IsColonist && ownerCount < 2)
                    {
                        info.IsPrivate = true;
                        info.Owners[ownerCount] = pawn;
                        ownerCount++;
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

    [HarmonyPatch(typeof(Region), "Allows")]
    public static class Region_AllowsPatch
    {
        static bool Prefix(Region __instance, TraverseParms tp, bool isDestination, ref bool __result)
        {
            // If the target room is not a bedroom, default behavior
            if (__instance.Room?.Role != RoomRoleDefOf.Bedroom)
            {
                return true;
            }

            // If the pawn is not a human, default behavior
            if (tp.pawn?.RaceProps?.Humanlike != true)
            {
                return true;
            }

            // If the pawn is hostile, default behavior
            if (tp.pawn.HostileTo(Faction.OfPlayer))
            {
                return true;
            }

            // If player forced or emergency job, default behavior
            if (RoomAccessCache.IsEmergency(tp.pawn))
            {
                return true;
            }

            // Prohibit pawns of going inside claimed bedrooms not owned by them
            RoomAccessInfo info = RoomAccessCache.Get(__instance.Room);
            if (!info.Allowed(tp.pawn))
            {
                __result = false;
                return false;
            }

            return true;
        }
    }
}
