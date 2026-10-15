using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using TMPro;
using UnityEngine;
using Verse;

namespace CustomFonts
{
    public partial class CustomFonts
    {
        private int _verificationStep;
        private int _verificationFrame;
        private string[] _verificationFonts;
        private float _verificationFirstWidth;
        private readonly bool _verificationEnabled = FontVerificationOptions.TryGet("customfonts-verify", out _);
        private int ImportedFileFontCount => _importedFontBundles.Keys.Count(font => !_bundledFontVariants.Values.Contains(font));
        private string _fallbackFirstGlyphMetrics;
        private void VerifyScopedFallbackGlyphs(bool defaults)
        {
            foreach (var size in PreviewFonts)
            {
                var first = SourcesFor((FontTarget)size)[0];
                Font expected = CreateImportedFont(defaults ? new byte[0] : FontFiles.Read(OSFontPaths[_settings.GetCurrentUIFontName(size)]),
                    "CustomFontsScopedReference", defaults ? DefaultFonts[size].fontNames : null, new[] { first });
                try
                {
                    int pixels = Text.fontStyles[(int)size].fontSize;
                    CurrentFonts[size].RequestCharactersInTexture("한", pixels); expected.RequestCharactersInTexture("한", pixels);
                    Require(CurrentFonts[size].GetCharacterInfo('한', out var a, pixels) && expected.GetCharacterInfo('한', out var b, pixels)
                        && a.glyphWidth > 0 && a.advance == b.advance && a.glyphWidth == b.glyphWidth && a.glyphHeight == b.glyphHeight,
                        "Unity " + size + " renders its own first fallback: " + first.Name);
                    Log.Message($"[Custom Fonts][scoped-fallback] glyph {size}: first={first.Name}, advance={a.advance}, default={defaults}");
                }
                finally { QueueDestroy(expected); }
            }
        }
        private void VerifyDefinedFallbackGlyphs()
        {
            string primary = _settings.GetCurrentUIFontName(GameFont.Medium);
            Font expected = CreateImportedFont(FontFiles.Read(OSFontPaths[primary]), "CustomFonts-fallback-reference", null, new[] { FallbackSources[0] });
            try
            {
                foreach (var size in PreviewFonts)
                {
                    int pixels = Text.fontStyles[(int)size].fontSize;
                    Font actual = CurrentFonts[size];
                    actual.RequestCharactersInTexture("한", pixels); expected.RequestCharactersInTexture("한", pixels);
                    bool foundActual = actual.GetCharacterInfo('한', out var a, pixels), foundExpected = expected.GetCharacterInfo('한', out var b, pixels);
                    string metrics = $"{a.advance}/{a.glyphWidth}/{a.glyphHeight}/{a.minX}/{a.minY}";
                    Log.Message($"[Custom Fonts][fallback-verify] glyph {size}/{pixels}: actual={foundActual}/{metrics}, reference={foundExpected}/{b.advance}/{b.glyphWidth}/{b.glyphHeight}; names={string.Join(",", actual.fontNames)}");
                    Require(foundActual && foundExpected && a.glyphWidth > 0 && a.glyphWidth == b.glyphWidth
                        && a.glyphHeight == b.glyphHeight && a.advance == b.advance, "Unity UI uses first defined private fallback at " + size);
                    if (size == GameFont.Medium && _fallbackTestStep == 1) _fallbackFirstGlyphMetrics = metrics;
                    if (size == GameFont.Medium && _fallbackTestStep == 2) Require(metrics != _fallbackFirstGlyphMetrics,
                        "reversing fallback order changes the rendered Hangul glyph");
                }
            }
            finally { QueueDestroy(expected); }
        }
        private void VerifyDefinedFallbackWorld(TextMeshPro label)
        {
            foreach (char expected in label.text.Where(c => c >= '\uac00' && c <= '\ud7a3'))
                Require(label.textInfo.characterInfo.Take(label.textInfo.characterCount).Any(c => c.character == expected
                    && c.textElement != null && c.textElement.unicode == expected), "world Hangul retains its original character instead of a square");
            Require(label.textInfo.characterInfo.Take(label.textInfo.characterCount)
                .Where(c => c.character >= '\uac00' && c.character <= '\ud7a3').All(c => c.fontAsset == _fallbackAssets[0]),
                "Unity TMP Hangul uses first defined fallback asset");
        }
        private void VerifyDefaultFallbackRendering()
        {
            foreach (var size in PreviewFonts)
            {
                int pixels = Text.fontStyles[(int)size].fontSize;
                CurrentFonts[size].RequestCharactersInTexture("W한", pixels); DefaultFonts[size].RequestCharactersInTexture("W", pixels);
                Require(CurrentFonts[size].GetCharacterInfo('W', out var a, pixels) && DefaultFonts[size].GetCharacterInfo('W', out var b, pixels)
                    && a.advance == b.advance, "Default UI retains primary Latin metrics at " + size);
                Require(CurrentFonts[size].GetCharacterInfo('한', out var c, pixels) && c.glyphWidth > 0, "Default UI fallback renders Hangul at " + size);
            }
        }
        private void VerifyDefaultWorldResourceLifetime()
            => Require(DefaultTMPFontAsset != null && DefaultTMPFontAsset.material != null
                && DefaultTMPFontAsset.atlasTextures.All(t => t != null), "game-owned default atlas survives delayed resource cleanup");

        // Same opt-in command as Waylith. Run only in an isolated test profile.
        partial void OnEngineVerification()
        {
            if (!_verificationEnabled || _verificationStep < 0 || Current.ProgramState != ProgramState.Playing
                || Time.frameCount < _verificationFrame || Event.current.type != EventType.Repaint) return;
            try
            {
                if (_verificationStep == 0)
                {
                    FontVerificationOptions.TryGet("customfonts-verify", out string path);
                    SetupBundledFonts();
                    Require(BundledFonts.Values.Any(font => font == null), "unselected bundles retain no loaded fonts");
                    VerifySharedFontCatalog();
                    _settings.FontSourcePaths.Clear();
                    _settings.FontSourcePaths.Add(path);
                    _settings.IncludeSystemFonts = false;
                    _settings.RecursiveFontFolders = true;
                    RefreshFontSources();
                    _verificationFonts = OSFontPaths.Keys.Take(2).ToArray();
                    Require(_verificationFonts.Length == 2 && _fontNames.Count == 2,
                        "private files discovered with system fonts excluded");
                    _settings.UniformUIfonts = true;
                    SaveFontAll(_verificationFonts[0]);
                    SaveWorldFont(_verificationFonts[0]);
                    CameraJumper.TryShowWorld();
                    _verificationFirstWidth = Text.CalcSize("Wide WWW iii 한글 폰트").x;
                }
                else if (_verificationStep <= 21)
                {
                    VerifyUnityFileRendering();
                    Require(ImportedFileFontCount <= 1, "UI/world share one content font and obsolete imports are released");
                    if (_verificationStep == 2)
                        Require(Math.Abs(Text.CalcSize("Wide WWW iii 한글 폰트").x - _verificationFirstWidth) > 0.1f,
                            "distinct file selection changes glyph metrics");
                    string selected = _verificationFonts[_verificationStep % 2];
                    SaveFontAll(selected);
                    SaveWorldFont(selected);
                    if (_verificationStep == 21) RefreshFontSources();
                }
                else if (_verificationStep == 22)
                {
                    VerifyUnityFileRendering();
                    Require(ImportedFileFontCount == 1, "refresh reuses one UI/world content font without retaining old bundles");
                    SaveFontAll(FontSettings.DefaultFontName);
                    SaveWorldFont(FontSettings.DefaultFontName);
                }
                else if (_verificationStep == 23)
                {
                    Require(ImportedFileFontCount == 0, "Default restoration releases all imported file bundles");
                    Require(PreviewFonts.All(f => ReferenceEquals(CurrentFonts[f], DefaultFonts[f])), "default UI fonts restored");
                    SaveFontAll(_verificationFonts[0]);
                    SaveWorldFont(_verificationFonts[0]);
                }
                else
                {
                    VerifyUnityFileRendering();
                    string bundled = BundledFonts.Keys.First();
                    int loaded = BundledFonts.Values.Count(f => f != null);
                    Font first = ResolveBundledFont(bundled);
                    Require(first != null && ReferenceEquals(first, ResolveBundledFont(bundled))
                        && BundledFonts.Values.Count(f => f != null) <= loaded + 1, "bundle selection is lazy and reuses the loaded font");
                    Require(BundledFonts.Values.Any(f => f == null), "other bundles remain unloaded");
                    RefreshWorldFeatureTexts();
                    WriteSettings();
                    Log.Message("[Custom Fonts][verify] PASS: live Unity file fonts, Hangul UI/world, 20 switches, refresh and bundle cleanup");
                    _verificationStep = -1;
                    return;
                }
                _verificationStep++;
                _verificationFrame = Time.frameCount + 120;
            }
            catch (Exception ex)
            {
                _verificationStep = -1;
                Log.Error("[Custom Fonts][verify] FAIL: " + ex);
            }
        }

        private void VerifyUnityFileRendering()
        {
            Font selected = CurrentFonts[GameFont.Medium];
            Require(selected != null && selected.dynamic && _importedFontBundles.ContainsKey(selected), "UI uses imported dynamic font");
            foreach (var size in PreviewFonts)
            {
                var style = Text.fontStyles[(int)size];
                Require(ReferenceEquals(style.font, selected), "three UI sizes share one file font");
                selected.RequestCharactersInTexture("Test 한글폰트", style.fontSize);
                foreach (char c in "한글폰트")
                    Require(selected.GetCharacterInfo(c, out var glyph, style.fontSize) && glyph.advance > 0
                        && glyph.glyphWidth > 0 && glyph.glyphHeight > 0, "Hangul UI glyph rasterizes at " + size);
            }
            Require(selected.material?.mainTexture != null, "UI font has a live atlas texture");
            var labels = (IEnumerable<WorldFeatureTextMesh>)AccessTools.Field(typeof(WorldFeatures), "texts").GetValue(Find.WorldFeatures);
            int rendered = 0;
            foreach (var label in labels.OfType<WorldFeatureTextMesh_TextMeshPro>())
            {
                var mesh = (TextMeshPro)AccessTools.Field(typeof(WorldFeatureTextMesh_TextMeshPro), "textMesh").GetValue(label);
                Require(mesh != null && mesh.font == _worldFontAsset, "world label uses selected file font");
                if (!label.Active || mesh.color.a <= 0.01f) continue;
                string original = mesh.text;
                try
                {
                    mesh.text = "한글폰트";
                    mesh.ForceMeshUpdate(true);
                    int vertices = mesh.textInfo.meshInfo.Sum(info => info.vertexCount);
                    Require(mesh.textInfo.characterCount == 4 && vertices >= 16,
                        "four Hangul characters render across world atlas submeshes (vertices=" + vertices + ")");
                }
                finally { mesh.text = original; mesh.ForceMeshUpdate(true); }
                rendered++;
            }
            Require(rendered > 0, "world has active visible labels");
        }
    }
}
