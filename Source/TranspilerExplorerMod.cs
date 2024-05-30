using HarmonyLib;
using UnityEngine;
using Verse;

namespace TranspilerExplorer
{
    public class TranspilerExplorerMod : Mod
    {
        public static Harmony harmony = new Harmony("transpiler_explorer");
        public static Settings settings;

        private readonly TranspilerExplorerServer server;

        public TranspilerExplorerMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<Settings>();
            EarlyPatches();
            server = new TranspilerExplorerServer(content);
        }

        private void EarlyPatches()
        {
            harmony.Patch(
                AccessTools.Method("HarmonyLib.PatchFunctions:UpdateWrapper"),
                finalizer: new HarmonyMethod(AccessTools.Method(typeof(HarmonyPatch_UpdateWrapper_Patch), "Finalizer"))
            );
            harmony.Patch(
                AccessTools.Method("HarmonyLib.PatchFunctions:ReversePatch"),
                finalizer: new HarmonyMethod(AccessTools.Method(
                    typeof(HarmonyPatch_Register_ReversePatches_Patch),
                    nameof(HarmonyPatch_Register_ReversePatches_Patch.Finalizer)))
            );
        }

        private string portBuffer;

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.ColumnWidth = 220f;

            listing.TextFieldNumericLabeled("Port (requires restart): ", ref settings.port, ref portBuffer, 0, ushort.MaxValue);

            listing.End();
        }

        public override string SettingsCategory()
        {
            return "Transpiler Explorer";
        }
    }

    public class Settings : ModSettings
    {
        const int DEFAULT_PORT = 8339;
        public int port = DEFAULT_PORT;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref port, "port", DEFAULT_PORT);
        }
    }
}
