using System;
using System.Linq;
using HarmonyLib;
using RimWorld.Planet;
using TMPro;
using UnityStubs.Gui;
using Verse;

namespace CustomFonts
{
    public partial class CustomFonts
    {
        private static readonly string[] CjkSamples =
        {
            "简体中文汉字龙门山脉", "繁體中文漢字龍門山脈",
            "日本語ひらがなカタカナ", "한국어한글ㄱㄴㅏㅓ", "「」『』、。！？（）Ａ１"
        };

        private void VerifyCjkLabels(WorldFeatureTextMesh_TextMeshPro label)
        {
            if (!GenCommandLine.TryGetCommandLineArg("customfonts-language", out string language)) return;
            var mesh = (TextMeshPro)AccessTools.Field(typeof(WorldFeatureTextMesh_TextMeshPro), "textMesh").GetValue(label);
            foreach (string sample in CjkSamples)
            {
                foreach (var size in PreviewFonts)
                {
                    foreach (char c in sample)
                        if (!NativeFontAccess.TryGlyph(CurrentFonts[size].GameFontIndex, c, Text.fontStyles[(int)size].fontSize, out var glyph) || !glyph.visible)
                            throw new InvalidOperationException($"CJK UI missing U+{(int)c:X4} ({size})");
                }
                VerifyMeshText(mesh, sample);
                Require(true, "CJK UI + world text: " + sample);
            }
            if (language != "English")
                Require("Options".Translate().ToString() != "Options", "game UI translation is active: " + "Options".Translate());
        }

        private static void VerifyMeshText(TextMeshPro mesh, string sample)
        {
            string original = mesh.text;
            try
            {
                mesh.text = sample;
                mesh.ForceMeshUpdate();
                int expected = sample.EnumerateRunes().Count();
                int visible = mesh.textInfo.characterInfo.Take(mesh.textInfo.characterCount).Count(c => c.isVisible);
                if (visible != expected || mesh.mesh.vertexCount != expected * 4)
                    throw new InvalidOperationException($"CJK world geometry: {visible}/{expected} glyphs for {sample}");
                // Exercise the UV refresh path too, including supplementary ideographs.
                AccessTools.Method(typeof(TextMeshPro), "RefreshUvs").Invoke(mesh,
                    new object[] { TextMeshProBackend.Current, mesh.textInfo.meshInfo[0] });
            }
            finally { mesh.text = original; mesh.ForceMeshUpdate(); }
        }

        private void VerifySupplementaryWorldText()
        {
            if (!GenCommandLine.TryGetCommandLineArg("customfonts-supplementary", out string path)) return;
            var font = LoadFontFile(path);
            var asset = TMP_FontAsset.CreateFontAsset(font);
            WaylithWorldGlyphPatch.Register(font.GameFontIndex);
            var labels = (System.Collections.Generic.IEnumerable<WorldFeatureTextMesh>)AccessTools.Field(typeof(WorldFeatures), "texts").GetValue(Find.WorldFeatures);
            var label = labels.OfType<WorldFeatureTextMesh_TextMeshPro>().First();
            var mesh = (TextMeshPro)AccessTools.Field(typeof(WorldFeatureTextMesh_TextMeshPro), "textMesh").GetValue(label);
            var previous = mesh.font;
            try
            {
                mesh.font = asset;
                const string sample = "𠀀𠮷";
                foreach (var rune in sample.EnumerateRunes())
                    Require(NativeFontAccess.TryGlyph(font.GameFontIndex, rune.Value, 24, out var glyph) && glyph.visible,
                        "supplementary UI glyph U+" + rune.Value.ToString("X"));
                VerifyMeshText(mesh, sample);
                Require(true, "supplementary CJK world geometry and UV refresh: " + sample);
            }
            finally { mesh.font = previous; mesh.ForceMeshUpdate(); QueueDestroy(asset); }
        }

        private void VerifyCollectionFont()
        {
            if (!GenCommandLine.TryGetCommandLineArg("customfonts-collection", out string path)) return;
            var font = LoadFontFile(path);
            var asset = TMP_FontAsset.CreateFontAsset(font);
            WaylithWorldGlyphPatch.Register(font.GameFontIndex);
            var labels = (System.Collections.Generic.IEnumerable<WorldFeatureTextMesh>)AccessTools.Field(typeof(WorldFeatures), "texts").GetValue(Find.WorldFeatures);
            var mesh = (TextMeshPro)AccessTools.Field(typeof(WorldFeatureTextMesh_TextMeshPro), "textMesh")
                .GetValue(labels.OfType<WorldFeatureTextMesh_TextMeshPro>().First());
            var previous = mesh.font;
            try
            {
                mesh.font = asset;
                foreach (string sample in CjkSamples)
                {
                    foreach (char c in sample)
                        Require(NativeFontAccess.TryGlyph(font.GameFontIndex, c, 24, out var glyph) && glyph.visible,
                            "collection UI glyph U+" + ((int)c).ToString("X"));
                    VerifyMeshText(mesh, sample);
                }
                Require(true, "TTC first face renders mixed CJK in UI and world text: " + font.name);
            }
            finally { mesh.font = previous; mesh.ForceMeshUpdate(); QueueDestroy(asset); }
        }
    }
}
