using Verse;
using RimWorld;
using HarmonyLib;
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
        public Pawn[] Owners = new Pawn[5];

        public bool Allowed(Pawn pawn)
        {
            if (OwnerCount == 0)
            {
                return true;
            }

            for (int i = 0; i < OwnerCount; i++)
            {
                if (Owners[i] == pawn)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public static class RoomAccessCache
    {
        private const int CACHE_INTERVAL = 600;
        public static readonly Dictionary<Room, RoomAccessInfo> cache = new Dictionary<Room, RoomAccessInfo>();

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
            if (room == null)
            {
                return;
            }

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
            for (int i = 0; i < info.OwnerCount; i++)
            {
                info.Owners[i] = null;
            }

            info.OwnerCount = 0;

            foreach (Building_Bed bed in room.ContainedBeds)
            {
                if (bed.OwnersForReading.Capacity == 0)
                {
                    continue;
                }

                foreach (Pawn pawn in bed.OwnersForReading)
                {
                    if (pawn.IsColonist && info.OwnerCount < 5)
                    {
                        info.Owners[info.OwnerCount] = pawn;
                        info.OwnerCount++;
                    }
                }
            }
        }

        public static bool HasOccupant(Room room)
        {
            foreach (Building_Bed bed in room.ContainedBeds)
            {
                if (bed.AnyOccupants)
                {
                    return true;
                }
            }
            return false;
        }
    }

    public class RoomEmergencyCache
    {
        private const int CACHE_INTERVAL = 60;

        public static readonly Dictionary<Room, EmergencyInfo> cache = new Dictionary<Room, EmergencyInfo>();

        public class EmergencyInfo
        {
            public int LastUpdateTick = 0;
            public bool result = false;
        }

        public static bool HasEmergency(Room room)
        {
            int tick = Find.TickManager.TicksGame;
            if (!cache.TryGetValue(room, out EmergencyInfo info))
            {
                info = new EmergencyInfo();
                cache[room] = info;
            }

            if (tick - info.LastUpdateTick >= CACHE_INTERVAL)
            {
                info.LastUpdateTick = tick;
                info.result = Recompute(room);
            }

            return info.result;
        }

        private static bool Recompute(Room room)
        {
            foreach (Building_Bed bed in room.ContainedBeds)
            {
                if (bed.AnyOccupants)
                {
                    foreach (Pawn pawn in bed.CurOccupants)
                    {
                        if (pawn.Downed || HealthAIUtility.ShouldBeTendedNowByPlayer(pawn) ||
                            HealthAIUtility.ShouldEverReceiveMedicalCareFromPlayer(pawn))
                        {
                            return true;
                        }
                    }
                }
            }

            Map map = room.Map;
            foreach (IntVec3 cell in room.Cells)
            {
                if (cell.GetFirstThing<Fire>(map) != null)
                {
                    return true;
                }

                Pawn pawn = cell.GetFirstPawn(map);
                if (pawn != null && pawn.Downed)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
