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
        private float _verificationFirstWidth;
        private int _verificationBundledBefore;
        private Font _verificationBundledFont;
        private readonly bool _verificationEnabled = GenCommandLine.TryGetCommandLineArg("customfonts-verify", out _);

        // Opt-in integration check. Use an isolated -savedatafolder and -quicktest. Exercises
        // the live renderer, text measurement, actual world labels and registration reuse.
        partial void OnEngineVerification()
        {
            if (!_verificationEnabled || _verificationStep < 0 || Current.ProgramState != ProgramState.Playing
                || Time.frameCount < _verificationFrame || Event.current.type != EventType.Repaint) return;
            try
            {
                switch (_verificationStep)
                {
                    case 0:
                        if (GenCommandLine.TryGetCommandLineArg("customfonts-language", out string expectedLanguage))
                            Require(LanguageDatabase.activeLanguage?.folderName == expectedLanguage,
                                "active game language: " + LanguageDatabase.activeLanguage?.folderName);
                        _settings.DebugLogging = true;
                        SetupOSFontPaths();
                        Require(OSFontPaths.Count > 0, "system font discovery");
                        Require(BundledFonts.Count > 0, "bundled font discovery");
                        Require(BundledFonts.Values.Any(font => font == null), "unselected bundled fonts have no loaded source data");
                        GenCommandLine.TryGetCommandLineArg("customfonts-verify", out string path);
                        _settings.FontSourcePaths.Clear();
                        _settings.FontSourcePaths.Add(path);
                        _settings.RecursiveFontFolders = true;
                        RefreshFontSources();
                        _verificationFonts = OSFontPaths.Keys.Where(k => k.StartsWith("(File) ")).Take(2).ToArray();
                        Require(_verificationFonts.Length == 2, "two custom file fonts discovered");
                        _settings.UniformUIfonts = true;
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
                        int before = _registeredFileFonts.Count;
                        int nativeBefore = EngineFontCount();
                        for (int i = 0; i < 200; i++)
                        {
                            foreach (var size in PreviewFonts) _settings.SetScaleFactor(size, 0.8f + (i % 10) / 10f);
                            SaveFontAll(_verificationFonts[i % 2]);
                            GetWorldFontAsset();
                        }
                        Require(_registeredFileFonts.Count == before, "200 font/size switches reuse registrations");
                        Require(EngineFontCount() == nativeBefore, "200 font/size switches keep the engine registration count unchanged");
                        foreach (var size in PreviewFonts) _settings.SetScaleFactor(size, 1f);
                        SaveFontAll(_verificationFonts[0]);
                        SaveWorldFont(_verificationFonts[0]);
                        break;
                    case 3:
                        VerifyLiveLabels(_verificationFonts[0]);
                        Require(CurrentFonts[GameFont.Small].GameFontIndex == _verificationFirstIndex, "return to original file reuses renderer index");
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
                        Require(ReferenceEquals(_verificationBundledFont, BundledFonts[_settings.CurrentWorldFontName]),
                            "reselecting bundle reuses loaded font");
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

        private static void Require(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException(description);
            Log.Message("[Custom Fonts][verify] OK: " + description);
        }

        private void VerifyWorldRendering()
        {
            var labels = (IEnumerable<WorldFeatureTextMesh>)AccessTools.Field(typeof(WorldFeatures), "texts").GetValue(Find.WorldFeatures);
            int checkedLabels = 0, staleUvs = 0, blankGlyphs = 0, wrongTextures = 0;
            foreach (var label in labels.OfType<WorldFeatureTextMesh_TextMeshPro>())
            {
                var mesh = (TextMeshPro)AccessTools.Field(typeof(WorldFeatureTextMesh_TextMeshPro), "textMesh").GetValue(label);
                if (!label.Active || mesh.color.a <= 0.01f || mesh.mesh == null || mesh.mesh.vertexCount == 0) continue;
                checkedLabels++;
                var texture = TextMeshProBackend.Current.GetAtlasTexture(mesh.font.fontIndex) as Texture2D;
                if (texture == null || !ReferenceEquals(mesh.fontMaterial.mainTexture, texture)) wrongTextures++;
                var info = mesh.textInfo;
                var uvs = mesh.mesh.uv;
                for (int i = 0; i < info.characterCount; i++)
                {
                    var character = info.characterInfo[i];
                    if (!character.isVisible || !NativeFontAccess.TryGlyph(mesh.font.fontIndex,
                        UnicodeScalars.At(mesh.text, i), TextMeshProBackend.Current.BakePixelSize, out var glyph)) continue;
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
                    break;
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
            Require(GuiBackend.Current.MeasureText(font.GameFontIndex, 20, FontStyle.Normal, "Test 한글", 1000).x > 0,
                "native renderer measures Latin and Korean");
            Require(TextMeshProBackend.Current.TryGetGlyph(font.GameFontIndex, 'A', out var glyph) && glyph.found && glyph.visible,
                "native font rasterizes glyphs");
            if (selection.Contains("Malgun Gothic") || selection.Contains("HangeulNuri"))
                Require(TextMeshProBackend.Current.TryGetGlyph(font.GameFontIndex, '한', out var korean) && korean.found && korean.visible,
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
                if (!checkedKoreanMesh && (selection.Contains("Malgun Gothic") || selection.Contains("HangeulNuri")))
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
