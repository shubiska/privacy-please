using Verse;
using RimWorld;
using System.Collections.Generic;
using System.Linq;

namespace PrivacyPlease
{
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
        public static bool dirty = false;
        private const int CACHE_INTERVAL = 600;
        public static readonly Dictionary<Room, RoomAccessInfo> cache = new Dictionary<Room, RoomAccessInfo>();

        public static RoomAccessInfo Get(Room room)
        {
            dirty = true;
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
            info.OwnerCount = 0;
            for (int i = 0; i < info.OwnerCount; i++)
            {
                info.Owners[i] = null;
            }

            foreach (Building_Bed bed in room.ContainedBeds)
            {
                foreach (Pawn pawn in bed.OwnersForReading)
                {
                    if (!pawn.IsSlave && !pawn.IsPrisoner && info.OwnerCount < 5)
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

        public static bool HasRelativeOwner(RoomAccessInfo info, Pawn pawn)
        {
            foreach (Pawn owner in info.Owners)
            {
                if (owner.relations.FamilyByBlood.Contains(pawn))
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
                foreach (Pawn pawn in bed.CurOccupants)
                {
                    if (pawn.Downed || HealthAIUtility.ShouldBeTendedNowByPlayer(pawn))
                    {
                        return true;
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
                if (pawn != null && pawn.Downed && HealthAIUtility.ShouldBeTendedNowByPlayer(pawn))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
