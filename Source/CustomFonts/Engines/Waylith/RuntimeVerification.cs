using System;
using System.Linq;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using TMPro;
using UnityEngine;
using UnityStubs.Gui;
using Verse;

namespace CustomFonts
{
    public partial class CustomFonts
    {
        private int _verificationStep;
        private int _verificationFrame;
        private string[] _verificationFonts;
        private int _verificationFirstIndex;
        private int _verificationSwitch;
        private float _verificationFirstWidth;
        private int _verificationBundledBefore;
        private Font _verificationBundledFont;
        private readonly bool _verificationEnabled = FontVerificationOptions.TryGet("customfonts-verify", out _);
        private float _fallbackFirstAdvance;
        private void VerifyScopedFallbackGlyphs(bool defaults)
        {
            foreach (var size in PreviewFonts)
            {
                var first = SourcesFor((FontTarget)size)[0];
                int pixels = Text.fontStyles[(int)size].fontSize;
                Font reference = LoadFontFile(first.Path, null, pixels);
                bool actualFound = NativeFontAccess.TryGlyph(CurrentFonts[size].GameFontIndex, '한', pixels, out var a);
                bool referenceFound = NativeFontAccess.TryGlyph(reference.GameFontIndex, '한', pixels, out var b);
                if (!actualFound || !referenceFound || !a.visible || Math.Abs(a.advanceX - b.advanceX) >= 0.01f)
                    Log.Message($"[Custom Fonts][scoped-fallback] mismatch {size}: actual={actualFound}/{a.advanceX}/index={CurrentFonts[size].GameFontIndex}, reference={referenceFound}/{b.advanceX}/index={reference.GameFontIndex}, first={first.Name}");
                Require(actualFound && referenceFound && a.visible && Math.Abs(a.advanceX - b.advanceX) < 0.01f,
                    "Waylith " + size + " renders its own first fallback: " + first.Name);
                Log.Message($"[Custom Fonts][scoped-fallback] glyph {size}: first={first.Name}, advance={a.advanceX}, index={CurrentFonts[size].GameFontIndex}, default={defaults}");
            }
        }
        private void VerifyDefinedFallbackGlyphs()
        {
            Font expected = LoadFontFile(FallbackSources[0].Path);
            foreach (var size in PreviewFonts)
            {
                int pixels = Text.fontStyles[(int)size].fontSize;
                bool foundActual = NativeFontAccess.TryGlyph(CurrentFonts[size].GameFontIndex, '한', pixels, out var a);
                bool foundExpected = NativeFontAccess.TryGlyph(expected.GameFontIndex, '한', pixels, out var b);
                Log.Message($"[Custom Fonts][fallback-verify] glyph {size}/{pixels}/{FallbackSources[0].Name}: actual={foundActual}/{a.advanceX}/{a.x0}/{a.x1}/{a.y0}/{a.y1}, reference={foundExpected}/{b.advanceX}/{b.x0}/{b.x1}/{b.y0}/{b.y1}");
                Require(foundActual && foundExpected
                    && a.visible && Math.Abs(a.advanceX - b.advanceX) < 0.01f && Math.Abs(a.x0 - b.x0) <= 1f && Math.Abs(a.x1 - b.x1) <= 1f,
                    "Waylith UI uses first defined private fallback at " + size);
                if (size == GameFont.Medium && _fallbackTestStep == 1) _fallbackFirstAdvance = a.advanceX;
                if (size == GameFont.Medium && _fallbackTestStep == 2) Require(Math.Abs(a.advanceX - _fallbackFirstAdvance) > 0.1f,
                    "reversing fallback order changes the rendered Hangul glyph");
            }
        }
        private void VerifyDefinedFallbackWorld(TextMeshPro label)
        {
            Require(NativeFontAccess.TryGlyph(label.font.fontIndex, '한', 48, out var glyph) && glyph.visible,
                "Waylith world uses visible merged fallback glyph");
        }
        private void VerifyDefaultFallbackRendering()
        {
            foreach (var size in PreviewFonts)
                Require(NativeFontAccess.TryGlyph(CurrentFonts[size].GameFontIndex, '한', Text.fontStyles[(int)size].fontSize, out var glyph) && glyph.visible,
                    "Default UI fallback renders Hangul at " + size);
        }
        private void VerifyDefaultWorldResourceLifetime()
            // The facade's atlasTextures entries are placeholders; the actual borrowed
            // native atlas is returned by the TMP backend and checked on the live labels.
            => Require(DefaultTMPFontAsset != null
                && TextMeshProBackend.Current.GetAtlasTexture(DefaultTMPFontAsset.fontIndex) != null,
                "native default world atlas survives delayed resource cleanup");

        // Opt-in integration check. Use an isolated -savedatafolder and -quicktest. Exercises
        // the live renderer, text measurement, actual world labels and registration reuse.
        partial void OnEngineVerification()
        {
            if (_textFieldVerify) VerifyTextFieldSizeIfRequested();
            if (!_verificationEnabled || _verificationStep < 0 || Current.ProgramState != ProgramState.Playing
                || Time.frameCount < _verificationFrame || Event.current.type != EventType.Repaint) return;
            try
            {
                switch (_verificationStep)
                {
                    case 0:
                        SetupBundledFonts();
                        if (GenCommandLine.TryGetCommandLineArg("customfonts-language", out string expectedLanguage))
                            Require(LanguageDatabase.activeLanguage?.folderName == expectedLanguage,
                                "active game language: " + LanguageDatabase.activeLanguage?.folderName);
                        _settings.DebugLogging = true;
                        SetupOSFontPaths();
                        Require(OSFontPaths.Count > 0, "system font discovery");
                        Require(BundledFonts.Count > 0, "bundled font discovery");
                        Require(BundledFonts.Values.Any(font => font == null), "unselected bundled fonts have no loaded source data");
                        VerifySharedFontCatalog();
                        FontVerificationOptions.TryGet("customfonts-verify", out string path);
                        _settings.FontSourcePaths.Clear();
                        _settings.FontSourcePaths.Add(path);
                        _settings.RecursiveFontFolders = true;
                        RefreshFontSources();
                        _verificationFonts = OSFontPaths.Where(p => FontFiles.Enumerate(path, true).Contains(p.Value, FontSourceCatalog.PathComparer))
                            .Select(p => p.Key).Take(2).ToArray();
                        Require(_verificationFonts.Length == 2, "two custom file fonts discovered");
                        _settings.UniformUIfonts = true;
                        foreach (var size in PreviewFonts) _settings.SetScaleFactor(size, 1f);
                        SaveFontAll(_verificationFonts[0]);
                        SaveWorldFont(_verificationFonts[0]);
                        CameraJumper.TryShowWorld();
                        _verificationFirstIndex = CurrentFonts[GameFont.Small].GameFontIndex;
                        _verificationFirstWidth = Text.CalcSize("Wide WWW iii 한글 폰트").x;
                        Require(_verificationFirstIndex >= 3, "custom font registered with renderer");
                        break;
                    case 1:
                        VerifyLiveLabels(_verificationFonts[0]);
                        SaveFontAll(_verificationFonts[1]);
                        SaveWorldFont(_verificationFonts[1]);
                        Require(CurrentFonts[GameFont.Small].GameFontIndex != _verificationFirstIndex, "distinct files use distinct renderer fonts");
                        Require(Math.Abs(Text.CalcSize("Wide WWW iii 한글 폰트").x - _verificationFirstWidth) > 0.1f,
                            "font selection changes measured glyph widths");
                        break;
                    case 2:
                        VerifyLiveLabels(_verificationFonts[1]);
                        if (_verificationSwitch < 20)
                        {
                            foreach (var size in PreviewFonts) _settings.SetScaleFactor(size, 0.8f + (_verificationSwitch % 10) / 10f);
                            SaveFontAll(_verificationFonts[_verificationSwitch++ % 2]);
                            GetWorldFontAsset();
                            // Allow native retirement before changing sizes again.
                            _verificationFrame = Time.frameCount + 12; return;
                        }
                        ProcessPendingDestroy();
                        Require(_registeredFileFonts.Count <= 4 && WaylithFontLifetime.LiveCount <= 4,
                            "20 size/family switches retain only the three UI sizes and world registration");
                        foreach (var size in PreviewFonts) _settings.SetScaleFactor(size, 1f);
                        SaveFontAll(_verificationFonts[0]);
                        SaveWorldFont(_verificationFonts[0]);
                        break;
                    case 3:
                        VerifyLiveLabels(_verificationFonts[0]);
                        Require(CurrentFonts[GameFont.Small].GameFontIndex >= 3
                            && Math.Abs(Text.CalcSize("Wide WWW iii 한글 폰트").x - _verificationFirstWidth) < 0.1f,
                            "return to a retired file restores its original glyph metrics in a reusable slot");
                        string bundled = BundledFonts.Keys.First();
                        _verificationBundledBefore = BundledFonts.Values.Count(font => font != null);
                        SaveFontAll(bundled);
                        SaveWorldFont(bundled);
                        _verificationBundledFont = BundledFonts[bundled];
                        Require(_verificationBundledFont != null && BundledFonts.Values.Count(font => font != null) <= _verificationBundledBefore + 1,
                            "selection loads only one bundled font for UI and world");
                        break;
                    case 4:
                        VerifyLiveLabels(_settings.CurrentWorldFontName);
                        SaveFontAll(_settings.CurrentWorldFontName);
                        Require(ReferenceEquals(CurrentFonts[GameFont.Small], ResolveBundledFont(_settings.CurrentWorldFontName,
                            SourcesFor(FontTarget.Small), Text.fontStyles[(int)GameFont.Small].fontSize)),
                            "reselecting bundle reuses the matching UI size variant");
                        Require(BundledFonts.Values.Any(font => font == null), "other bundle sources remain unloaded after rendering");
                        // Ensure rescanning doesn't duplicate registrations or invalidate selections.
                        SaveFontAll(_verificationFonts[0]);
                        SaveWorldFont(_verificationFonts[0]);
                        int count = _registeredFileFonts.Count;
                        RefreshFontSources();
                        Require(_registeredFileFonts.Count == count, "refresh reuses content registration");
                        SaveFontAll(FontSettings.DefaultFontName);
                        SaveWorldFont(FontSettings.DefaultFontName);
                        break;
                    case 5:
                        VerifyLiveLabels(FontSettings.DefaultFontName);
                        Require(PreviewFonts.All(f => ReferenceEquals(CurrentFonts[f], DefaultFonts[f])), "default UI fonts restored");
                        Require(!ForceLegacyText, "default world font keeps Waylith TMP renderer");
                        VerifySupplementaryWorldText();
                        VerifyCollectionFont();
                        if (GenCommandLine.TryGetCommandLineArg("customfonts-language", out _))
                        {
                            SaveFontAll(_verificationFonts[0]);
                            SaveWorldFont(_verificationFonts[0]);
                        }
                        WriteSettings();
                        break;
                    case 6:
                        VerifyWorldRendering();
                        break;
                    case 7:
                        VerifyWorldRendering();
                        Log.Message("[Custom Fonts][verify] PASS: live Waylith UI, custom files, bundles, world labels, scale switches and refresh");
                        _verificationStep = -1;
                        return;
                }
                _verificationStep++;
                _verificationFrame = Time.frameCount + (_verificationStep == 7 ? 600 : 120);
            }
            catch (Exception ex)
            {
                _verificationStep = -1;
                Log.Error("[Custom Fonts][verify] FAIL: " + ex);
            }
        }

        partial void VerifyWorldFallbackBackend() => VerifyWorldRendering();

        private void VerifyWorldRendering()
        {
            var labels = (IEnumerable<WorldFeatureTextMesh>)AccessTools.Field(typeof(WorldFeatures), "texts").GetValue(Find.WorldFeatures);
            int checkedLabels = 0, staleUvs = 0, blankGlyphs = 0, wrongTextures = 0;
            foreach (var label in labels.OfType<WorldFeatureTextMesh_TextMeshPro>())
            {
                var mesh = (TextMeshPro)AccessTools.Field(typeof(WorldFeatureTextMesh_TextMeshPro), "textMesh").GetValue(label);
                if (!label.Active || mesh.color.a <= 0.01f || mesh.mesh == null || mesh.mesh.vertexCount == 0) continue;
                Require(mesh.gameObject.layer == LayerMask.NameToLayer("WorldText"),
                    "world label uses WorldText render layer (actual=" + mesh.gameObject.layer + ")");
                Require(mesh.mesh.colors32.Any(c => c.a > 0), "visible world label has nontransparent vertex colors");
                checkedLabels++;
                var texture = TextMeshProBackend.Current.GetAtlasTexture(mesh.font.fontIndex) as Texture2D;
                if (texture == null || !ReferenceEquals(mesh.fontMaterial.mainTexture, texture)) wrongTextures++;
                var info = mesh.textInfo;
                var uvs = mesh.mesh.uv;
                for (int i = 0; i < info.characterCount; i++)
                {
                    var character = info.characterInfo[i];
                    if (!character.isVisible) continue;
                    Require(NativeFontAccess.TryGlyph(mesh.font.fontIndex,
                        UnicodeScalars.At(mesh.text, i), TextMeshProBackend.Current.BakePixelSize, out var glyph)
                        && glyph.visible, "visible world character resolves to its actual glyph (no replacement square)");
                    int v = character.vertexIndex;
                    if (Math.Abs(uvs[v].x - glyph.u0) > 0.00001f || Math.Abs(uvs[v].y - glyph.v1) > 0.00001f) staleUvs++;
                    if (texture != null)
                    {
                        bool ink = false;
                        int x0 = (int)(glyph.u0 * texture.width), x1 = (int)(glyph.u1 * texture.width);
                        int y0 = (int)((1f - glyph.v1) * texture.height), y1 = (int)((1f - glyph.v0) * texture.height);
                        for (int y = y0; y < y1 && !ink; y++)
                            for (int x = x0; x < x1 && !ink; x++) ink = texture.GetPixel(x, y).a > 0.01f;
                        if (!ink) blankGlyphs++;
                    }
                }
            }
            Log.Message($"[Custom Fonts][verify] rendering: {checkedLabels} visible labels, {staleUvs} stale UVs, {blankGlyphs} blank atlas glyphs, {wrongTextures} wrong textures; atlas version {TextMeshProBackend.Current.AtlasVersion}");
            Require(checkedLabels > 0 && staleUvs == 0 && blankGlyphs == 0 && wrongTextures == 0,
                "world labels keep their texture and glyph UVs after the final font switch");
        }

        private static int EngineFontCount()
        {
            var gui = GuiBackend.Current;
            var host = AccessTools.Field(gui.GetType(), "host").GetValue(gui);
            return ((System.Collections.ICollection)AccessTools.Property(host.GetType(), "Fonts").GetValue(host)).Count;
        }

        private void VerifyLiveLabels(string selection)
        {
            VerifyWorldRendering();
            Font font = CurrentFonts[GameFont.Small];
            Require(PreviewFonts.All(f => ReferenceEquals(Text.fontStyles[(int)f].font, CurrentFonts[f])), "all UI styles bound");
            Require(WaylithTextMeasurement.Measure(GuiBackend.Current, font.GameFontIndex, 20, FontStyle.Normal, "Test 한글", 1000).x > 0,
                "native renderer measures Latin and Korean");
            // UI size variants are not world TMP fonts. Probe the UI rasterizer at
            // its actual size; the world asset's TMP mesh/atlas is checked below.
            int uiPixels = Text.fontStyles[(int)GameFont.Small].fontSize;
            Require(NativeFontAccess.TryGlyph(font.GameFontIndex, 'A', uiPixels, out var glyph) && glyph.visible,
                "native font rasterizes glyphs");
            if (selection.Contains("Malgun Gothic") || selection.Contains("Sarasa UI"))
                Require(NativeFontAccess.TryGlyph(font.GameFontIndex, '한', uiPixels, out var korean) && korean.visible,
                    "Korean font rasterizes Hangul glyphs");
            var labels = (IEnumerable<WorldFeatureTextMesh>)AccessTools.Field(typeof(WorldFeatures), "texts").GetValue(Find.WorldFeatures);
            int withGeometry = 0;
            bool checkedKoreanMesh = false;
            foreach (var label in labels.OfType<WorldFeatureTextMesh_TextMeshPro>())
            {
                var mesh = (TextMeshPro)AccessTools.Field(typeof(WorldFeatureTextMesh_TextMeshPro), "textMesh").GetValue(label);
                var expected = selection == FontSettings.DefaultFontName ? DefaultTMPFontAsset : _worldFontAsset;
                if (mesh == null || mesh.font != expected) throw new InvalidOperationException("world label uses wrong font");
                if (mesh.mesh != null && mesh.mesh.vertexCount > 0) withGeometry++;
                if (!checkedKoreanMesh && (selection.Contains("Malgun Gothic") || selection.Contains("Sarasa UI")))
                {
                    string original = mesh.text;
                    mesh.text = "한글폰트";
                    mesh.ForceMeshUpdate();
                    Require(mesh.mesh != null && mesh.mesh.vertexCount >= 16, "four Hangul characters produce world-label geometry");
                    mesh.text = original;
                    mesh.ForceMeshUpdate();
                    checkedKoreanMesh = true;
                }
            }
            Require(withGeometry > 0, "world labels have rendered geometry: " + selection);
            VerifyCjkLabels(labels.OfType<WorldFeatureTextMesh_TextMeshPro>().First());
            LogWorldTextFonts("verification");
        }
    }
}
