using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Verse;

namespace CustomFonts
{
    public partial class CustomFonts
    {
        private readonly bool _sizeVerify = FontVerificationOptions.TryGet("customfonts-size-verify", out _);
        private readonly bool _sizeRestartVerify = FontVerificationOptions.TryGet("customfonts-size-restart-verify", out _);
        private int _sizeStep, _sizeFrame;
        private string[] _sizeFamilies;
        private FontSizeReviewWindow _sizeWindow;
        private static readonly float[][] SizeReviewScales = {
            new[] { 1f, 1f, 1f }, new[] { 1f, 1f, 2f }, new[] { 1f, 1f, 3f },
            new[] { 1f, 2f, 3f }, new[] { 2f, 2f, 3f }, new[] { 2f, 2f, 1f }, new[] { 1f, 1f, 1f }
        };

        // Separate from the uniform-scale stress check: change only one size at a
        // time, compare unchanged rendered glyphs, and restart with unequal sizes.
        public void TickFontSizeVerification()
        {
            if ((!_sizeVerify && !_sizeRestartVerify) || _sizeStep < 0 || Event.current.type != EventType.Repaint
                || Time.frameCount < _sizeFrame) return;
            try
            {
                VerifySizeStyles("startup/settled");
                if (_sizeWindow == null)
                {
                    _sizeWindow = new FontSizeReviewWindow(this); Find.WindowStack.Add(_sizeWindow);
                    Log.Message("[Custom Fonts][size-verify] first GUI: " + DescribeSizeReview());
                    if (_sizeRestartVerify)
                    {
                        Require(!_settings.UniformUIfonts && _settings.GetScaleFactor(GameFont.Medium) == 3f
                            && _settings.GetScaleFactor(GameFont.Small) == 1f && _settings.GetScaleFactor(GameFont.Tiny) == 1f,
                            "unequal saved scales survive mod loading");
                        VerifySizeFallbacks();
                        _sizeStep = 1; _sizeFrame = Time.frameCount + 180; return;
                    }
                    FontVerificationOptions.TryGet("customfonts-size-verify", out string path);
                    _settings.UniformUIfonts = false; _settings.IncludeSystemFonts = true;
                    _settings.FontSourcePaths.Clear(); _settings.FontSourcePaths.Add(Path.GetFullPath(path));
                    foreach (var size in PreviewFonts) { _settings.SetScaleFactor(size, 1); _settings.SetVerticalOffset(size, 0); }
                    RefreshFontSources(); SetupBundledFonts();
                    _settings.SetFallbackFonts(FontTarget.Tiny, new[] { "HangeulNuri" });
                    _settings.SetFallbackFonts(FontTarget.Small, new[] { "MaruBuri" });
                    _settings.SetFallbackFonts(FontTarget.Medium, new[] { "HangeulNuri", "MaruBuri" });
                    ApplyFallbackChanges(FontTarget.Medium);
                    _sizeFamilies = new[] { "Arial", BundledFonts.Keys.First(), FontSettings.DefaultFontName };
                    ConfigureSizeReview(); return;
                }
                VerifySizeFallbacks();
                if (_sizeRestartVerify)
                {
                    Log.Message("[Custom Fonts][size-verify] PASS: unequal primary/fallback sizes applied before first UI draw and retained after mod loading: " + DescribeSizeReview());
                    _sizeStep = -1; return;
                }
                _sizeStep++;
                if (_sizeStep == _sizeFamilies.Length * SizeReviewScales.Length)
                {
                    // Leave an intentionally asymmetric profile for the restart check.
                    _settings.SetScaleFactor(GameFont.Medium, 3f); SaveFontAll("Arial"); WriteSettings();
                    VerifySizeStyles("final immediate"); VerifySizeFallbacks();
                    Log.Message("[Custom Fonts][size-verify] PASS: 21 independent file/bundle/Default scale combinations, unchanged-size glyphs and per-size fallbacks; restart profile saved: " + DescribeSizeReview());
                    _sizeStep = -1; return;
                }
                ConfigureSizeReview();
            }
            catch (Exception ex) { _sizeStep = -1; Log.Error("[Custom Fonts][size-verify] FAIL: " + ex); }
        }

        private void ConfigureSizeReview()
        {
            string family = _sizeFamilies[_sizeStep / SizeReviewScales.Length];
            var scales = SizeReviewScales[_sizeStep % SizeReviewScales.Length];
            var fonts = PreviewFonts.Select(s => CurrentFonts[s]).ToArray();
            var samples = PreviewFonts.Select(s => Text.fontStyles[(int)s].CalcSize(new GUIContent("Wgj 한글"))).ToArray();
            var oldScales = PreviewFonts.Select(s => _settings.GetScaleFactor(s)).ToArray();
            bool sameFamily = PreviewFonts.All(s => _settings.GetCurrentUIFontName(s) == family);
            var world = _worldFontAsset;
            for (int i = 0; i < PreviewFonts.Length; i++) _settings.SetScaleFactor(PreviewFonts[i], scales[i]);
            SaveFontAll(family);
            // This runs in the same GUI event as a player's slider change, before
            // the next StartOfOnGUI has a chance to repair stale line metrics.
            VerifySizeStyles("immediate change");
            for (int i = 0; i < PreviewFonts.Length; i++)
                if (sameFamily && oldScales[i] == scales[i])
                {
                    var size = PreviewFonts[i];
                    Require(ReferenceEquals(fonts[i], CurrentFonts[size])
                        && samples[i] == Text.fontStyles[(int)size].CalcSize(new GUIContent("Wgj 한글")),
                        "changing another size preserves the font and rendered Latin/Hangul dimensions: " + size);
                }
            Require(ReferenceEquals(world, _worldFontAsset), "UI scale changes preserve the world font asset");
            Log.Message("[Custom Fonts][size-verify] combination " + _sizeStep + ": " + DescribeSizeReview());
            _sizeFrame = Time.frameCount + 90;
        }

        private void VerifySizeStyles(string stage)
        {
            foreach (var size in PreviewFonts)
            {
                int pixels = (int)Math.Round(DefaultUIFontSize(size) * _settings.GetScaleFactor(size));
                foreach (var style in UIFontStyles(size))
                    Require(style.font == CurrentFonts[size] && style.fontSize == pixels,
                        stage + " label/input/area use their own size: " + size + "/" + pixels);
                float measured = Text.fontStyles[(int)size].CalcHeight(new GUIContent("W"), 999f);
                Require(Math.Abs(Text.LineHeightOf(size) - measured) < 0.01f,
                    stage + " cached line height matches the active font: " + size + "/" + Text.LineHeightOf(size) + "/" + measured);
            }
        }

        private void VerifySizeFallbacks()
        {
            VerifyEngineSizeFallbacks();
            if (PreviewFonts.All(s => _settings.GetCurrentUIFontName(s) == "Arial")) VerifyScopedFallbackGlyphs(false);
            else if (PreviewFonts.All(s => _settings.GetCurrentUIFontName(s) == FontSettings.DefaultFontName)) VerifyScopedFallbackGlyphs(true);
        }
        private string DescribeSizeReview() => string.Join(" | ", PreviewFonts.Select(s =>
            $"{s}: {_settings.GetCurrentUIFontName(s)}, {Text.fontStyles[(int)s].fontSize}px, line {Text.LineHeightOf(s):F1}, fallback={string.Join(",", _settings.GetFallbackFonts((FontTarget)s))}"));

        private sealed class FontSizeReviewWindow : Window
        {
            private readonly CustomFonts _mod;
            public override Vector2 InitialSize => new Vector2(1100, 650);
            internal FontSizeReviewWindow(CustomFonts mod) { _mod = mod; doCloseX = true; draggable = true; forcePause = true; }
            public override void DoWindowContents(Rect inRect)
            {
                var previous = Text.Font;
                try
                {
                    Text.Font = GameFont.Small;
                    Widgets.Label(new Rect(0, 0, 1030, 65), "Independent font sizes: " + _mod.DescribeSizeReview());
                    float y = 85;
                    foreach (var size in PreviewFonts)
                    {
                        Text.Font = size;
                        const string sample = "Wgj 한글 폰트";
                        float height = Text.CalcHeight(sample, 1000);
                        var rect = new Rect(5, y, 1000, height);
                        Widgets.DrawBox(rect); Widgets.Label(rect, sample);
                        Widgets.TextField(new Rect(5, y + height + 5, 1000, height + 12), sample);
                        y += height * 2 + 30;
                    }
                }
                finally { Text.Font = previous; }
            }
        }
    }
}
