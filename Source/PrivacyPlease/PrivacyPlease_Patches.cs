using Verse;
using RimWorld;
using HarmonyLib;

namespace PrivacyPlease
{
    [HarmonyPatch(typeof(Autosaver), nameof(Autosaver.DoAutosave))]
    public static class Autosaver_DoAutosavePatch
    {
        static void Prefix()
        {
            RoomAccessCache.cache.Clear();
            RoomEmergencyCache.cache.Clear();
        }
    }

    [HarmonyPatch(typeof(CompAssignableToPawn), nameof(CompAssignableToPawn.TryAssignPawn))]
    public static class CompAssignableToPawn_TryAssignPawnPatch
    {
        static void Postfix(CompAssignableToPawn __instance)
        {
            if (__instance.parent is Building_Bed bed)
            {
                Room room = bed.GetRoom();
                RoomAccessCache.ForceRecompute(room);
            }
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.Destroy))]
    public static class Thing_DestroyPatch
    {
        static void Prefix(Thing __instance)
        {
            if (__instance is Building_Bed bed)
            {
                Room room = bed.GetRoom();
                RoomAccessCache.ForceRecompute(room);
            }
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.DeSpawn))]
    public static class Thing_DespawnPatch
    {
        static void Prefix(Thing __instance)
        {
            if (__instance is Building_Bed bed)
            {
                Room room = bed.GetRoom();
                RoomAccessCache.ForceRecompute(room);
            }
        }
    }

    [HarmonyPatch(typeof(Region), nameof(Region.Allows))]
    public static class Region_AllowsPatch
    {
        static void Postfix(Region __instance, TraverseParms tp, bool isDestination, ref bool __result)
        {
            // If the target room is not a bedroom, default behavior
            Room room = __instance.Room;
            if (!__result || room == null || room.Role != RoomRoleDefOf.Bedroom)
            {
                return;
            }

            // If invalid or non-human, default behavior
            Pawn pawn = tp.pawn;
            if (pawn == null || !pawn.RaceProps.Humanlike || pawn.Faction == null)
            {
                return;
            }

            // If the pawn is hostile, default behavior
            if (pawn.Faction != Faction.OfPlayer && pawn.Faction.RelationWith(Faction.OfPlayer).kind == FactionRelationKind.Hostile)
            {
                return;
            }

            // If already inside the bedroom, default behavior
            Room pawnRoom = pawn.GetRoom();
            if (pawnRoom != null && pawnRoom == room)
            {
                return;
            }

            RoomAccessInfo info = RoomAccessCache.Get(__instance.Room);

            // If there are no owners, default behavior
            if (info.OwnerCount == 0)
            {
                return;
            }

            // Messy
            if (!isDestination)
            {
                if (!RoomAccessCache.HasOccupant(__instance.Room))
                {
                    return;
                }

                if (PrivacyPleaseMod.settings.allowPathSleeping)
                {
                    if (PrivacyPleaseMod.settings.allowPathSleepingFamily)
                    {
                        if (RoomAccessCache.HasRelativeOwner(info, pawn))
                        {
                            return;
                        }
                    }
                    else
                    {
                        return;
                    }
                }
            }

            // Prohibit pawns of going inside claimed bedrooms not owned by them, unless drafted or has pawn needing treatment
            if (!info.Allowed(pawn) && !pawn.Drafted && !RoomEmergencyCache.HasEmergency(__instance.Room))
            {
                __result = false;
                return;
            }

            __result = true;
        }
    }
}
