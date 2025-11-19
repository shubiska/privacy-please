using Verse;
using HarmonyLib;
using UnityEngine;

namespace PrivacyPlease
{
    public class PrivacyPleaseMod : Mod
    {
        public static PrivacyPleaseSettings settings;

        public PrivacyPleaseMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<PrivacyPleaseSettings>();
            new Harmony("Xubisca.PrivacyPlease").PatchAll();
        }

        public override string SettingsCategory() => "Privacy Please!";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            listing.CheckboxLabeled("Allow path even if someone is sleeping inside", ref settings.allowPathSleeping);
            if (settings.allowPathSleeping)
            {
                listing.CheckboxLabeled("Allow path while sleeping only for family", ref settings.allowPathSleepingFamily);
            }

            if (RoomAccessCache.dirty)
            {
                RoomAccessCache.dirty = true;
                RoomAccessCache.cache.Clear();
            }

            listing.End();
            base.DoSettingsWindowContents(inRect);
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
            RoomAccessCache.cache.Clear();
        }
    }

    public class PrivacyPleaseSettings : ModSettings
    {
        public bool allowPathSleeping = false;
        public bool allowPathSleepingFamily = false;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref allowPathSleeping, "allowPathSleeping", false);
            Scribe_Values.Look(ref allowPathSleepingFamily, "allowPathSleepingFamily", false);
            base.ExposeData();
        }
    }
}
