using System;
using System.IO;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityStubs.Gui;
using Verse;

namespace CustomFonts
{
    public partial class CustomFonts
    {
        private readonly bool _textFieldVerify = FontVerificationOptions.TryGet("customfonts-textfield-verify", out _);
        private int _textFieldVerifyStep, _textFieldVerifyFrame, _textFieldVerifySamples;
        private string[] _textFieldVerifyFamilies;

        // Exercise the engine's unmodified input renderer. Size 0 must resolve to
        // the selected UI variant, including scaled Default and bundled fonts.
        private void VerifyTextFieldSizeIfRequested()
        {
            if (!_textFieldVerify || _textFieldVerifyStep < 0 || Current.ProgramState != ProgramState.Playing
                || Event.current.type != EventType.Repaint || Time.frameCount < _textFieldVerifyFrame) return;
            try
            {
                if (_textFieldVerifyFamilies == null)
                {
                    FontVerificationOptions.TryGet("customfonts-textfield-verify", out string path);
                    path = Path.GetFullPath(path);
                    Require(Directory.Exists(path), "input verification has a private font folder");
                    _settings.UniformUIfonts = false; _settings.IncludeSystemFonts = true;
                    _settings.FontSourcePaths.Clear(); _settings.FontSourcePaths.Add(path);
                    RefreshFontSources(); SetupBundledFonts();
                    string family = OSFontPaths.First(p => FontFiles.Enumerate(path, true).Contains(p.Value, FontSourceCatalog.PathComparer)).Key;
                    _textFieldVerifyFamilies = new[] { family, BundledFonts.Keys.First(), FontSettings.DefaultFontName };
                    foreach (FontTarget target in Enum.GetValues(typeof(FontTarget))) _settings.SetFallbackFonts(target, new[] { family });
                    ApplyFallbackChanges(FontTarget.Medium);
                    ShowFallbackTestWorld();
                    ConfigureInputVerification(); return;
                }
                var backend = GuiBackend.Current;
                object layout = AccessTools.Field(backend.GetType(), "textLayout").GetValue(backend);
                var width = AccessTools.Property(layout.GetType(), "Width");
                var height = AccessTools.Property(layout.GetType(), "Height");
                foreach (var size in PreviewFonts)
                {
                    var style = Text.textFieldStyles[(int)size];
                    int pixels = style.fontSize;
                    backend.FontMetrics(style.FontIndex, out float importSize, out _, out _);
                    Require(importSize == pixels, "registered input size equals the UI size: " + size + "/" + pixels);
                    foreach (bool multiline in new[] { false, true })
                    {
                        string sample = multiline ? "abcdef 한글\nSecond 한글" : "abcdef 한글";
                        Vector2 expected = WaylithTextMeasurement.Measure(backend, style.FontIndex, pixels, style.fontStyle, sample, -1f);
                        Require(expected == WaylithTextMeasurement.Measure(backend, style.FontIndex, 0f, style.fontStyle, sample, -1f),
                            "native size-0 metrics match explicitly sized label: " + pixels);
                        var rect = new Rect(20, 100 + ((int)size * 2 + (multiline ? 1 : 0)) * 70, 600, 70);
                        if (multiline) GUI.TextArea(rect, sample, Text.textAreaStyles[(int)size]);
                        else GUI.TextField(rect, sample, style);
                        float actualWidth = (float)width.GetValue(layout), actualHeight = (float)height.GetValue(layout);
                        Require(Math.Abs(actualWidth - expected.x) < 0.01f && Math.Abs(actualHeight - expected.y) < 0.01f,
                            $"unpatched input layout matches label at {pixels}px, multiline={multiline}: {actualWidth}/{actualHeight}, expected {expected}");
                        _textFieldVerifySamples++;
                    }
                    Require(NativeFontAccess.TryGlyph(style.FontIndex, '한', pixels, out var glyph) && glyph.visible,
                        "input variant contains visible Hangul at " + pixels);
                }
                if (_textFieldVerifyStep < 4)
                    Require(PreviewFonts.All(s => ReferenceEquals(WaylithFontLifetime.SourceBytes(CurrentFonts[s].GameFontIndex),
                        WaylithFontLifetime.SourceBytes(CurrentFonts[GameFont.Medium].GameFontIndex))),
                        "size variants share one original source buffer");
                VerifyWorldFallbackRendering();
                var unchanged = CurrentFonts.Values.ToArray();
                UpdateFont();
                Require(CurrentFonts.Values.SequenceEqual(unchanged), "unchanged sizes reuse existing font facades");
                _textFieldVerifyStep++;
                if (_textFieldVerifyStep == 6)
                {
                    Require(WaylithFontAtlasVerification.PendingFrames == 0, "native atlas uploads finish before every render snapshot");
                    WriteSettings();
                    Log.Message("[Custom Fonts][textfield-verify] PASS: " + _textFieldVerifySamples
                        + " unpatched file/bundle/Default input layouts at 12/14/21/24/28/42px; shared source data, size reuse, Hangul and live world glyphs");
                    _textFieldVerifyStep = -1; return;
                }
                ConfigureInputVerification();
            }
            catch (Exception ex) { _textFieldVerifyStep = -1; Log.Error("[Custom Fonts][textfield-verify] FAIL: " + ex); }
        }
        private void ConfigureInputVerification()
        {
            foreach (var size in PreviewFonts) _settings.SetScaleFactor(size, _textFieldVerifyStep % 2 == 0 ? 1f : 2f);
            SaveFontAll(_textFieldVerifyFamilies[_textFieldVerifyStep / 2]);
            SaveWorldFont(_textFieldVerifyFamilies[_textFieldVerifyStep / 2]);
            _textFieldVerifyFrame = Time.frameCount + 120;
        }
    }
}
