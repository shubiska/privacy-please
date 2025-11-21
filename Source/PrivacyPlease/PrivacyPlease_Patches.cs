using Verse;
using RimWorld;
using HarmonyLib;
using PrivacyPlease;

public static class Patch_Vanilla
{
    [HarmonyPatch(typeof(Autosaver), nameof(Autosaver.DoAutosave))]
    public static class Autosaver_DoAutosavePatch
    {
        static void Prefix()
        {
            RoomAccessCache.cache.Clear();
            RoomEmergencyCache.cache.Clear();
            if (Prefs.DevMode)
            {
                Log.Message($"[PrivacyPlease!] Autosave cleared cache.");
            }
        }
    }

    [HarmonyPatch(typeof(Pawn_Ownership), nameof(Pawn_Ownership.ClaimBedIfNonMedical))]
    public static class Pawn_Ownership_ClaimBedIfNonMedicalPatch
    {
        public static void Postfix(Pawn_Ownership __instance, Building_Bed newBed)
        {
            if (newBed == null)
            {
                return;
            }

            Room room = newBed.GetRoom();
            RoomAccessCache.ForceRecompute(room);

            if (Prefs.DevMode)
            {
                Pawn pawn = (Pawn)AccessTools.Field(typeof(Pawn_Ownership), "pawn").GetValue(__instance);
                Log.Message($"[PrivacyPlease!] Pawn claimed bed: {pawn}.");
            }
        }
    }

    [HarmonyPatch(typeof(Pawn_Ownership), nameof(Pawn_Ownership.UnclaimBed))]
    public static class UnclaimBed_Patch
    {
        public static void Postfix(Pawn_Ownership __instance)
        {
            Building_Bed bed = __instance.OwnedBed;
            if (bed == null)
            {
                return;
            }

            Room room = bed.GetRoom();
            RoomAccessCache.ForceRecompute(room);

            if (Prefs.DevMode)
            {
                Pawn pawn = (Pawn)AccessTools.Field(typeof(Pawn_Ownership), "pawn").GetValue(__instance);
                Log.Message($"[PrivacyPlease!] Pawn unclaimed bed: {pawn}.");
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
                if (Prefs.DevMode)
                {
                    Log.Message($"[PrivacyPlease!] Bed destroyed, ownership updated.");
                }
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
                if (Prefs.DevMode)
                {
                    Log.Message($"[PrivacyPlease!] Bed despawned, ownership updated.");
                }
            }
        }
    }

    // Block going directly inside a bedroom
    [HarmonyPatch(typeof(Region), nameof(Region.Allows))]
    public static class Region_AllowsPatch
    {
        static bool Prefix(Region __instance, TraverseParms tp, bool isDestination, ref bool __result)
        {
            // Allow passing by
            if (!isDestination)
            {
                return true;
            }

            // If the target room is not a room, default behavior
            Room room = __instance.Room;
            if (room == null)
            {
                return true;
            }

            // If the target is not a bedrooma and not a guest bedroom(hospitality), default behavior
            if (room.Role != RoomRoleDefOf.Bedroom && !(PrivacyPleaseMod.hospitality && room.Role.defName == "GuestRoom"))
            {
                return true;
            }

            // If invalid or non-human, default behavior
            Pawn pawn = tp.pawn;
            if (pawn == null || !pawn.RaceProps.Humanlike || pawn.Faction == null)
            {
                return true;
            }

            // If the pawn is hostile, default behavior
            if (pawn.Faction != Faction.OfPlayer && pawn.Faction.RelationWith(Faction.OfPlayer).kind == FactionRelationKind.Hostile)
            {
                return true;
            }

            RoomAccessInfo info = RoomAccessCache.Get(room);

            // If there are no owners, default behavior
            if (info.OwnerCount == 0)
            {
                return true;
            }

            // Prohibit pawns of going inside claimed bedrooms not owned by them, unless drafted or has pawn needing treatment
            if (!info.Allowed(pawn) && !pawn.Drafted && !RoomEmergencyCache.HasEmergency(room))
            {
                __result = false;
                return false;
            }

            return true;
        }
    }
}
