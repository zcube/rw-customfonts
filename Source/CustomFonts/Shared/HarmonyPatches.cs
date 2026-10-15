using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Verse;
using UnityEngine;
using HarmonyLib;
using RimWorld.Planet;
using RimWorld;
using SettingsHelper;
using TMPro;

namespace CustomFonts
{
    internal static class HarmoneyPatchers
    {
        private static bool _patcherInitialized;
        private static CustomFonts _mod;

        // StartOfOnGUI runs for every GUI event, so the mod lookup is resolved only once.
        private static CustomFonts Mod => _mod ?? (_mod = LoadedModManager.GetMod<CustomFonts>());

        [HarmonyPatch(typeof(Text), nameof(Text.StartOfOnGUI))]
        class StartOfOnGUIPatcher
        {
            [HarmonyPostfix]
            public static void Postfix()
            {
                var mod = Mod;
                if (mod == null) return;

                if (!_patcherInitialized)
                {
                    _patcherInitialized = true;
                    var startupTimer = System.Diagnostics.Stopwatch.StartNew();
                    mod.CaptureDefaultUIFonts();
                    mod.DefaultTMPFontAsset = WorldFeatureTextMesh_TextMeshPro.WorldTextPrefab.GetComponent<TextMeshPro>().font;
                    mod.LogVisualFontDefaults("before startup apply");
                    mod.UpdateFont();
                    mod.LogVisualFontDefaults("after startup apply");
                    mod.LogFontTiming("startup font apply", startupTimer);
                    AccessTools.StaticFieldRefAccess<bool>(typeof(WorldFeatures), "ForceLegacyText") = mod.ForceLegacyText;
                    mod.DebugLog("font patcher initialised");
                    if (mod.DebugLogging) mod.LogResourceStats("startup");

                    // Unattended leak check: RimWorldWin64.exe -customfonts-stress
                    if (Environment.GetCommandLineArgs().Any(arg => arg == "-customfonts-stress"))
                    {
                        mod.BeginFontStressTest();
                    }
                    if (Environment.GetCommandLineArgs().Any(arg => arg == "-customfonts-worldtest"))
                    {
                        mod.BeginWorldFontTest();
                    }
                }

                mod.EnsureUniformFonts();
                mod.RecalcTextMetricsIfNeeded();
                mod.TickFontSizeVerification();
                mod.TickFontStressTest();
                mod.TickWorldFontTest();
                mod.ProcessPendingDestroy();
                mod.TickDebugStats();
                mod.TickEngineVerification();
                mod.TickFallbackVerification();
                mod.TickScopedFallbackVerification();
                mod.TickFontMemoryVerification();
                mod.TickFontVisualVerification();
            }
        }

        [HarmonyPatch(typeof(GenScene), nameof(GenScene.GoToMainMenu))]
        class GoToMainMenuPatcher
        {
            [HarmonyPostfix]
            public static void Postfix()
            {
                var mod = Mod;
                if (mod != null && mod.DefaultFonts.Count != 0) mod.UpdateFont();
            }
        }

        [HarmonyPatch(typeof(WorldFeatures), "HasCharacter")]
        private class WorldMapHasCharacterPatcher
        {
            [HarmonyPrefix]
            public static bool Prefix(ref bool __result)
            {
                var mod = Mod;
                if (mod.ForceLegacyText)
                    return true;
                __result = true;
                return false;
            }
        }

        [HarmonyPatch(typeof(WorldFeatureTextMesh_TextMeshPro), "Init")]
        class WorldMapInitPatcher
        {
            [HarmonyPrefix]
            public static void Prefix()
            {
                var mod = Mod;
                var fontAsset = mod.GetWorldFontAsset();

                var prefab = WorldFeatureTextMesh_TextMeshPro.WorldTextPrefab.GetComponent<TextMeshPro>();
                if (prefab.font != fontAsset)
                {
                    prefab.font = fontAsset;
                    prefab.UpdateFontAsset();
                }
                AccessTools.StaticFieldRefAccess<float>(typeof(WorldFeatureTextMesh_TextMeshPro), "TextScale") =
                    1.75f * mod._settings.WorldScaleFactor;
            }

        }

        [HarmonyPatch(typeof(Dialog_FileList), nameof(Dialog_FileList.DrawDateAndVersion))]
        class DrawDateAndVersionPatcher
        {
            [HarmonyPrefix]
            public static void Prefix(Dialog_FileList __instance, SaveFileInfo sfi, ref Rect rect)
            {
                string str = "0000-00-00 00:00";

                var backup = Text.Font;
                Text.Font = GameFont.Tiny;
                var size = Text.CalcSize(str);

                rect.x -= size.x - rect.width + 2.0f;
                rect.width = size.x + 2.0f;
                Text.Font = backup;
            }
        }
    }
}
