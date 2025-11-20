using Verse;
using HarmonyLib;
using System.Reflection;

namespace PrivacyPlease
{
    public class PrivacyPleaseMod : Mod
    {
        public static bool hospitality;

        public PrivacyPleaseMod(ModContentPack content) : base(content)
        {
            hospitality = ModLister.GetModWithIdentifier("Orion.Hospitality", false) != null;
            var harmony = new Harmony("Xubisca.PrivacyPlease");

            harmony.PatchAll(typeof(Patch_Vanilla).Assembly);

            if (hospitality)
            {
                PatchHospitality(harmony);
            }
        }

        public void PatchHospitality(Harmony harmony)
        {
            var type = AccessTools.TypeByName("Hospitality.CompGuest");
            var method = AccessTools.Method(type, "ClaimBed");

            var postfix = typeof(JobDriver_ClaimBed_ClaimBedPatch_Hospitality).GetMethod("Postfix", BindingFlags.Static | BindingFlags.Public);

            harmony.Patch(method, postfix: new HarmonyMethod(postfix));
        }
    }
}
