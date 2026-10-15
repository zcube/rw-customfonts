using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace CustomFonts
{
    public partial class CustomFonts
    {
        private readonly bool _memoryVerify = FontVerificationOptions.TryGet("customfonts-memory-verify", out _);
        private int _memoryStep, _memoryFrame, _memoryPhase;
        private string[] _memoryKeys;
        private string _memoryBundle;
        private Dictionary<string, long> _memoryBaseline;
        private bool _memoryCleanup;
        public void TickFontMemoryVerification()
        {
            if (!_memoryVerify || _memoryStep < 0 || Current.ProgramState != ProgramState.Playing
                || Event.current.type != EventType.Repaint || Time.frameCount < _memoryFrame) return;
            try
            {
                if (_memoryCleanup)
                {
                    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); ProcessPendingDestroy();
                    VerifyRetiredFontResources();
                    foreach (var size in PreviewFonts)
                    {
                        var current = Text.fontStyles[(int)size]; var original = DefaultFontStyle[size];
                        Require(current.contentOffset == original.contentOffset,
                            "unscaled Default restores the game's original content offset: " + size);
                        var sample = new GUIContent("Wgj 한글");
                        Require(current.CalcSize(sample) == original.CalcSize(sample),
                            "unscaled Default retains the game's original rendered dimensions: " + size);
                    }
                    Log.Message("[Custom Fonts][memory-verify] Default cleanup: "
                        + string.Join(", ", CaptureFontMemory().Select(p => p.Key + "=" + p.Value)));
                    WriteSettings();
                    Log.Message("[Custom Fonts][memory-verify] PASS: 96 UI/world switches across file/bundle/Default and four fallback policies; refresh, native styles, stable settled resources; Default releases all custom registrations/imports");
                    _memoryStep = -1; return;
                }
                if (_memoryKeys == null)
                {
                    FontVerificationOptions.TryGet("customfonts-memory-verify", out string path);
                    _settings.DebugLogging = false; _settings.UniformUIfonts = false;
                    foreach (var size in PreviewFonts) _settings.SetVerticalOffset(size, 0);
                    _settings.FontSourcePaths.Clear(); _settings.FontSourcePaths.Add(path);
                    _settings.IncludeSystemFonts = true;
                    foreach (FontTarget t in Enum.GetValues(typeof(FontTarget))) _settings.GetFallbackFonts(t).Clear();
                    RefreshFontSources(); SetupBundledFonts();
                    _memoryKeys = OSFontPaths.Where(p => FontFiles.Enumerate(path, true).Contains(p.Value, FontSourceCatalog.PathComparer))
                        .Select(p => p.Key).OrderBy(k => k, StringComparer.Ordinal).Take(2).ToArray();
                    Require(_memoryKeys.Length == 2, "memory test has two private fallback families");
                    _memoryBundle = BundledFonts.Keys.First();
                    ShowFallbackTestWorld();
                }
                if (_memoryStep > 0 && _memoryStep % 24 == 0)
                {
                    // A settled identical configuration after each full cycle makes
                    // resource growth distinguishable from legitimate lazy loading.
                    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                    ProcessPendingDestroy();
                    var snapshot = CaptureFontMemory();
                    Log.Message("[Custom Fonts][memory-verify] cycle " + (++_memoryPhase) + ": "
                        + string.Join(", ", snapshot.Select(p => p.Key + "=" + p.Value))
                        + "; managed heap=" + GC.GetTotalMemory(false));
                    if (_memoryBaseline == null) _memoryBaseline = snapshot;
                    else foreach (var pair in snapshot)
                        Require(pair.Value <= _memoryBaseline[pair.Key], "settled " + pair.Key + " does not grow: " + pair.Value + " <= " + _memoryBaseline[pair.Key]);
                    VerifyWorldFallbackRendering();
                    if (_memoryPhase == 4)
                    {
                        foreach (FontTarget t in Enum.GetValues(typeof(FontTarget))) _settings.GetFallbackFonts(t).Clear();
                        foreach (var size in PreviewFonts) _settings.SetScaleFactor(size, 1f);
                        SaveFontAll(FontSettings.DefaultFontName); SaveWorldFont(FontSettings.DefaultFontName);
                        ApplyFallbackChanges(FontTarget.Medium);
                        _memoryCleanup = true; _memoryFrame = Time.frameCount + 180; return;
                    }
                    // Move past the sample point without applying an extra configuration.
                }
                if (_memoryStep % 6 == 0 && _memoryStep != 0) RefreshFontSources();
                string primary = _memoryStep % 3 == 0 ? "Arial" : _memoryStep % 3 == 1 ? FontSettings.DefaultFontName : _memoryBundle;
                foreach (var size in PreviewFonts)
                {
                    int policy = (_memoryStep + (int)size) % 4;
                    _settings.SetFallbackFonts((FontTarget)size, MemoryFallbacks(policy));
                    _settings.SetCurrentUIFontName(size, primary);
                    _settings.SetScaleFactor(size, 1f + (_memoryStep % 3) * 0.1f);
                }
                _settings.SetFallbackFonts(FontTarget.World, MemoryFallbacks(_memoryStep % 4));
                _settings.CurrentWorldFontName = _memoryStep % 2 == 0 ? "Arial" : FontSettings.DefaultFontName;
                ApplyFallbackChanges(FontTarget.Medium);
                // World primary changes also need to replace the actual label objects.
                GetWorldFontAsset(); RefreshWorldFeatureTexts();
                foreach (var size in PreviewFonts)
                {
                    var style = new GUIStyle(Text.fontStyles[(int)size]) { fontStyle = FontStyle.BoldAndItalic };
                    GUI.Label(new Rect(20, 100 + (int)size * 50, 400, 40), "Memory 한글 fallback", style);
                }
                _memoryStep++;
                _memoryFrame = Time.frameCount + (_memoryStep % 24 == 0 ? 180 : 30);
            }
            catch (Exception ex) { _memoryStep = -1; Log.Error("[Custom Fonts][memory-verify] FAIL: " + ex); }
        }
        private IEnumerable<string> MemoryFallbacks(int policy) => policy == 0 ? _memoryKeys.Take(1)
            : policy == 1 ? _memoryKeys.Skip(1) : policy == 2 ? _memoryKeys : _memoryKeys.Reverse();
    }
}
