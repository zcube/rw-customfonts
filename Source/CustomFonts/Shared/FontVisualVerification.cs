using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;
using Verse;

namespace CustomFonts
{
    public partial class CustomFonts
    {
        private readonly bool _visualVerify = FontVerificationOptions.TryGet("customfonts-visual-verify", out _);
        private FontVisualVerificationWindow _visualWindow;
        internal void LogVisualFontDefaults(string stage)
        {
            if (!_visualVerify) return;
            Log.Message("[Custom Fonts][visual-verify] " + stage + ": " + string.Join(" | ", PreviewFonts.Select(size =>
                $"{size}: original={DefaultFontStyle[size].fontSize}/{DefaultFonts[size].fontSize}, current={Text.fontStyles[(int)size].fontSize}, offset={Text.fontStyles[(int)size].contentOffset.y}, scale={_settings.GetScaleFactor(size)}")));
        }
        public void TickFontVisualVerification()
        {
            if (!_visualVerify || _visualWindow != null || Current.ProgramState != ProgramState.Playing) return;
            FontVerificationOptions.TryGet("customfonts-visual-verify", out string path);
            _settings.FontSourcePaths.Clear(); _settings.FontSourcePaths.Add(Path.GetFullPath(path));
            _settings.IncludeSystemFonts = true; _settings.UniformUIfonts = true;
            foreach (FontTarget target in Enum.GetValues(typeof(FontTarget))) _settings.GetFallbackFonts(target).Clear();
            if (FontVerificationOptions.TryGet("customfonts-visual-fallback", out _))
                foreach (FontTarget target in Enum.GetValues(typeof(FontTarget))) _settings.SetFallbackFonts(target, new[] { "HangeulNuri" });
            foreach (var size in PreviewFonts) { _settings.SetScaleFactor(size, 1f); _settings.SetVerticalOffset(size, 0); }
            RefreshFontSources(); SetupBundledFonts();
            _visualWindow = new FontVisualVerificationWindow(this);
            Find.WindowStack.Add(_visualWindow);
        }

        private sealed class FontVisualVerificationWindow : Window
        {
            private readonly CustomFonts _mod;
            private readonly string[] _families;
            private int _selection;
            private bool _automatic, _world, _fallback;
            private float _nextSwitch;
            private double _applyMilliseconds;
            private long _lastFrame;
            private double _maxFrameMilliseconds;
            private int _lastFrameNumber = -1;
            private const string Sample = "한글 폰트 Agjypq 아래쪽 테스트";
            public override Vector2 InitialSize => new Vector2(1100, 650);
            internal FontVisualVerificationWindow(CustomFonts mod)
            {
                _mod = mod; doCloseX = true; draggable = true; forcePause = true;
                _families = new[] { "Arial", "Malgun Gothic", "HangeulNuri", "MaruBuri", mod.BundledFonts.Keys.First(), FontSettings.DefaultFontName };
                _fallback = mod._settings.GetFallbackFonts(FontTarget.Medium).Count != 0;
                Select(0);
            }
            private void Select(int selection)
            {
                _selection = selection; _maxFrameMilliseconds = 0;
                var timer = Stopwatch.StartNew();
                _mod.SaveFontAll(_families[selection]);
                if (_world) _mod.SaveWorldFont(_families[selection]);
                _applyMilliseconds = timer.Elapsed.TotalMilliseconds;
                _nextSwitch = Time.realtimeSinceStartup + 3f;
                Log.Message($"[Custom Fonts][visual-verify] {_families[selection]}: apply={_applyMilliseconds:F1}ms, world={_world}");
            }
            public override void DoWindowContents(Rect inRect)
            {
                if (Event.current.type == EventType.Repaint && _lastFrameNumber != Time.frameCount)
                {
                    long now = Stopwatch.GetTimestamp();
                    if (_lastFrame != 0) _maxFrameMilliseconds = Math.Max(_maxFrameMilliseconds, (now - _lastFrame) * 1000.0 / Stopwatch.Frequency);
                    _lastFrame = now; _lastFrameNumber = Time.frameCount;
                    if (_automatic && Time.realtimeSinceStartup >= _nextSwitch) Select((_selection + 1) % _families.Length);
                }
                var backup = Text.Font; var anchor = Text.Anchor;
                try
                {
                    Text.Font = GameFont.Small; Text.Anchor = TextAnchor.UpperLeft;
                    Widgets.Label(new Rect(0, 0, 1050, 30), $"Font visual review: {_families[_selection]} | apply {_applyMilliseconds:F1}ms | max frame {_maxFrameMilliseconds:F1}ms");
                    for (int i = 0; i < _families.Length; i++)
                        if (Widgets.ButtonText(new Rect(i * 170, 35, 165, 32), _families[i])) Select(i);
                    Widgets.CheckboxLabeled(new Rect(0, 80, 240, 28), "Auto switch (3 seconds)", ref _automatic);
                    Widgets.CheckboxLabeled(new Rect(260, 80, 240, 28), "Switch world font too", ref _world);
                    if (Widgets.ButtonText(new Rect(520, 80, 100, 28), "1x")) { _mod.ApplyScaleFactor(GameFont.Medium, 1f); Select(_selection); }
                    if (Widgets.ButtonText(new Rect(630, 80, 100, 28), "1.5x")) { _mod.ApplyScaleFactor(GameFont.Medium, 1.5f); Select(_selection); }
                    foreach (int offset in new[] { -4, 0, 4 })
                        if (Widgets.ButtonText(new Rect(740 + (offset + 4) / 4 * 100, 80, 95, 28), "Offset " + offset))
                        { _mod.ApplyVerticalOffset(GameFont.Medium, offset); _mod.RecalcCustomLineHeights(); }
                    bool previousFallback = _fallback;
                    Widgets.CheckboxLabeled(new Rect(0, 112, 350, 24), "HangeulNuri fallback", ref _fallback);
                    if (previousFallback != _fallback)
                    {
                        foreach (FontTarget target in Enum.GetValues(typeof(FontTarget)))
                            _mod._settings.SetFallbackFonts(target, _fallback ? new[] { "HangeulNuri" } : new string[0]);
                        _mod.ApplyFallbackChanges(FontTarget.Medium); Select(_selection);
                    }
                    float y = 155;
                    foreach (var size in PreviewFonts)
                    {
                        Text.Font = size;
                        float height = Text.CalcHeight(Sample, 650);
                        var rect = new Rect(5, y, 650, height);
                        Widgets.DrawBox(rect); Widgets.Label(rect, Sample);
                        Widgets.Label(new Rect(675, y, 350, 55), $"{size}: {Text.fontStyles[(int)size].fontSize}px / box {height:F1}px");
                        Widgets.ButtonText(new Rect(5, y + height + 5, 400, 30), Sample);
                        Widgets.TextField(new Rect(420, y + height + 5, 235, 30), Sample);
                        y += 125;
                    }
                    Text.Font = GameFont.Small;
                    Widgets.Label(new Rect(5, 530, 1050, 50), _mod.DescribeVisualFontMetrics(Sample));
                }
                finally { Text.Font = backup; Text.Anchor = anchor; }
            }
        }
    }
}
