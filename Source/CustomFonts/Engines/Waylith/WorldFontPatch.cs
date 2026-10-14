using HarmonyLib;
using RimWorld.Planet;
using TMPro;
using Verse;

namespace CustomFonts
{
    // Waylith's prefab cloning does not preserve the TMP font binding. Apply it to the
    // actual label as well as the prefab; Unity keeps its original prefab-only patch.
    [HarmonyPatch(typeof(WorldFeatureTextMesh_TextMeshPro), "Init")]
    internal static class WaylithWorldFontPatch
    {
        [HarmonyPostfix]
        private static void Postfix(TextMeshPro ___textMesh)
        {
            var mod = LoadedModManager.GetMod<CustomFonts>();
            if (mod == null || ___textMesh == null) return;
            ___textMesh.font = mod.GetWorldFontAsset();
            // Default has no sourceFontFile in Waylith. Its renderer index still needs
            // the CJK path, otherwise returning to Default renders only Latin-1.
            WaylithWorldGlyphPatch.Register(___textMesh.font.fontIndex);
            ___textMesh.UpdateFontAsset();
        }
    }
}
