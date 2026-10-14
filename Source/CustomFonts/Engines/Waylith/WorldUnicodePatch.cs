using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityStubs.Gui;

namespace CustomFonts
{
    // Preserve UTF-16 indices for RimWorld's curved-text transforms, while giving the
    // rasterizer complete scalar values instead of splitting supplementary ideographs.
    [HarmonyPatch(typeof(TextMeshPro), "GenerateTextMesh")]
    internal static class WaylithWorldUnicodePatch
    {
        [ThreadStatic] private static int _scalar;

        private static char ReadCodeUnit(string text, int index)
        {
            _scalar = UnicodeScalars.At(text, index);
            return text[index];
        }

        private static bool ReadGlyph(ITextMeshProBackend backend, int fontIndex, char codeUnit, out TmpGlyphMetrics glyph)
        {
            if (!WaylithWorldGlyphPatch.Handles(fontIndex)) return backend.TryGetGlyph(fontIndex, codeUnit, out glyph);
            return WaylithWorldGlyphPatch.TryGlyph(backend, fontIndex, _scalar, out glyph);
        }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            var readChar = AccessTools.PropertyGetter(typeof(string), "Chars");
            var readGlyph = AccessTools.Method(typeof(ITextMeshProBackend), "TryGetGlyph");
            int chars = 0, glyphs = 0;
            foreach (var instruction in code)
            {
                if (instruction.Calls(readChar))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = AccessTools.Method(typeof(WaylithWorldUnicodePatch), nameof(ReadCodeUnit));
                    chars++;
                }
                else if (instruction.Calls(readGlyph))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = AccessTools.Method(typeof(WaylithWorldUnicodePatch), nameof(ReadGlyph));
                    glyphs++;
                }
            }
            if (chars != 2 || glyphs != 2) throw new InvalidOperationException("Waylith TMP Unicode patch: unexpected layout method.");
            return code;
        }
    }

    [HarmonyPatch(typeof(TextMeshPro), "RefreshUvs")]
    internal static class WaylithWorldUnicodeUvsPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(TextMeshPro __instance, ITextMeshProBackend backend, TMP_MeshInfo mi,
            int ____layoutFontIndex, string ____layoutText, ref int ____layoutAtlasVersion)
        {
            if (!WaylithWorldGlyphPatch.Handles(____layoutFontIndex)) return true;
            ____layoutAtlasVersion = backend.AtlasVersion;
            var characters = __instance.textInfo?.characterInfo;
            if (__instance.mesh == null || mi.uvs0 == null || characters == null || mi.uvs0.Length != mi.vertexCount) return false;
            for (int i = 0; i < __instance.textInfo.characterCount; i++)
            {
                var character = characters[i];
                if (!character.isVisible || !WaylithWorldGlyphPatch.TryGlyph(backend, ____layoutFontIndex,
                    UnicodeScalars.At(____layoutText, i), out var glyph)) continue;
                int v = character.vertexIndex;
                mi.uvs0[v] = new Vector2(glyph.u0, glyph.v1);
                mi.uvs0[v + 1] = new Vector2(glyph.u0, glyph.v0);
                mi.uvs0[v + 2] = new Vector2(glyph.u1, glyph.v0);
                mi.uvs0[v + 3] = new Vector2(glyph.u1, glyph.v1);
            }
            __instance.mesh.uv = mi.uvs0;
            __instance.mesh.UploadMeshData(false);
            return false;
        }
    }
}
