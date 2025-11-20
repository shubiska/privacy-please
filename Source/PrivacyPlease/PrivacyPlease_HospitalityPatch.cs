using Verse;
using HarmonyLib;

namespace PrivacyPlease
{
    public static class JobDriver_ClaimBed_ClaimBedPatch_Hospitality
    {
        public static void Postfix(object __instance, object newBed)
        {
            var compGuestType = __instance.GetType();
            var bedField = AccessTools.Field(compGuestType, "bed");
            var bed = bedField.GetValue(__instance);

            if (bed != newBed)
            {
                return;
            }

            if (Prefs.DevMode)
            {
                var pawn = AccessTools.Property(compGuestType, "Pawn").GetValue(__instance);
                Log.Message($"[PrivacyPlease!] Guest claimed bed: {pawn}.");
            }
        }
    }
}
