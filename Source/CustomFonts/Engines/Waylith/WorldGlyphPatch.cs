using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityStubs.Gui;
using TMPro;
using UnityEngine;

namespace CustomFonts
{
    // 1.7.4977's TMP cache contains only Latin-1 and uses the startup font locale.
    // Resolve on demand from the same locale-aware fonts as the game's UI renderer.
    [HarmonyPatch]
    internal static class WaylithWorldGlyphPatch
    {
        private static readonly HashSet<int> Fonts = new HashSet<int>();
        private static readonly Dictionary<(int, int), TmpGlyphMetrics> Requested = new Dictionary<(int, int), TmpGlyphMetrics>();
        private static object _backend;
        private static int _generation;
        private static FieldInfo _atlasPixels;

        internal static void Register(int fontIndex) => Fonts.Add(fontIndex);
        internal static void Forget(int fontIndex)
        {
            Fonts.Remove(fontIndex);
            foreach (var key in new List<(int, int)>(Requested.Keys))
                if (key.Item1 == fontIndex) Requested.Remove(key);
            WaylithWorldBakeLifetimePatch.Forget(fontIndex);
        }
        internal static bool Handles(int fontIndex) => Fonts.Contains(fontIndex);

        private static MethodBase TargetMethod() => AccessTools.Method(
            AccessTools.TypeByName("RimWorldWaylith.Imgui.ImGuiTextMeshProBackend"), "TryGetGlyph");

        [HarmonyPrefix]
        private static bool Prefix(ITextMeshProBackend __instance, int __0, char __1,
            ref TmpGlyphMetrics __2, ref bool __result)
        {
            if (!Handles(__0)) return true;
            __result = TryGlyph(__instance, __0, __1, out __2);
            return false;
        }

        internal static bool TryGlyph(ITextMeshProBackend backend, int fontIndex, int scalar, out TmpGlyphMetrics glyph)
        {
            bool found = NativeFontAccess.TryGlyph(fontIndex, scalar, backend.BakePixelSize, out glyph);
            if (!ReferenceEquals(_backend, backend) || _generation != NativeFontAccess.Generation)
            {
                _backend = backend;
                _generation = NativeFontAccess.Generation;
                Requested.Clear();
                _atlasPixels = AccessTools.Field(backend.GetType(), "atlasRgba");
            }
            // Adding a glyph can change pixels without resizing the native atlas. Force the
            // next engine capture to copy it into the world renderer's texture and bump its
            // version, allowing existing TMP meshes to refresh their UVs after repacking.
            if (found)
            {
                var key = (fontIndex, scalar);
                if (!Requested.TryGetValue(key, out var previous) || previous.u0 != glyph.u0
                    || previous.v0 != glyph.v0 || previous.u1 != glyph.u1 || previous.v1 != glyph.v1)
                    _atlasPixels?.SetValue(backend, null);
                Requested[key] = glyph;
            }
            return found;
        }
    }

    // The engine keeps its startup fonts' 48px bakes alive, but world glyphs use the
    // locale-specific font copies. Keep those bakes active while their meshes draw;
    // otherwise ImGui evicts their pixels after the layout has stopped changing.
    [HarmonyPatch(typeof(TextMeshPro), "LateUpdate")]
    internal static class WaylithWorldBakeLifetimePatch
    {
        private static readonly Dictionary<int, int> Touched = new Dictionary<int, int>();
        internal static void Forget(int index) => Touched.Remove(index);

        [HarmonyPrefix]
        private static void Prefix(TextMeshPro __instance)
        {
            int index = __instance.font?.fontIndex ?? -1;
            if (!WaylithWorldGlyphPatch.Handles(index) || !__instance.gameObject.activeInHierarchy) return;
            if (Touched.TryGetValue(index, out int frame) && frame == Time.frameCount) return;
            Touched[index] = Time.frameCount;
            NativeFontAccess.TryMetrics(index, TextMeshProBackend.Current.BakePixelSize, out _);
        }
    }

    [HarmonyPatch]
    internal static class WaylithWorldMetricsPatch
    {
        private static MethodBase TargetMethod() => AccessTools.Method(
            AccessTools.TypeByName("RimWorldWaylith.Imgui.ImGuiTextMeshProBackend"), "TryGetFontMetrics");

        [HarmonyPrefix]
        private static bool Prefix(ITextMeshProBackend __instance, int __0, ref TmpFontMetrics __1, ref bool __result)
        {
            if (!WaylithWorldGlyphPatch.Handles(__0)) return true;
            // The shim's font list updates at frame end. Use live metrics so a newly
            // selected font can render immediately, with the same face used for glyphs.
            __result = NativeFontAccess.TryMetrics(__0, __instance.BakePixelSize, out __1);
            return false;
        }
    }
}
