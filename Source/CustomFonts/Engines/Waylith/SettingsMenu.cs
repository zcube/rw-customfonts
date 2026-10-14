using System;
using UnityEngine;
using Verse;

namespace CustomFonts
{
    public partial class CustomFonts
    {
        partial void LogEngineResourceStats(string reason)
        {
            Log.Message($"[Custom Fonts] Waylith registrations ({reason}): {_registeredFileFonts.Count} file fonts; registrations remain engine-owned until exit");
        }
        private string _newFontSource = "";
        private string _sourceInputError;
        private Vector2 _sourceScroll;
        private float _sourceContentHeight = 500f;

        public void DrawEngineSettings(Rect rect)
        {
            var outer = new Rect(rect.x, rect.y, rect.width, rect.height);
            var view = new Rect(0, 0, Math.Max(100, rect.width - 20), Math.Max(rect.height, _sourceContentHeight));
            Widgets.BeginScrollView(outer, ref _sourceScroll, view);
            var list = new Listing_Standard();
            list.Begin(view);
            list.Label("CustomFonts.FontSources".Translate());
            list.Label("CustomFonts.FontSourcesHelp".Translate());
            list.CheckboxLabeled("CustomFonts.IncludeSystemFonts".Translate(), ref _settings.IncludeSystemFonts);
            list.CheckboxLabeled("CustomFonts.RecursiveFolders".Translate(), ref _settings.RecursiveFontFolders);
            _newFontSource = list.TextEntryLabeled("CustomFonts.FontPath".Translate(), _newFontSource);
            if (list.ButtonText("CustomFonts.AddFontPath".Translate()))
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(_newFontSource)) throw new ArgumentException("CustomFonts.EmptyFontPath".Translate());
                    string path = FontFiles.Normalize(_newFontSource, _content.RootDir);
                    // Validate existence/type before saving. Detailed file errors remain visible after scan.
                    FontFiles.Enumerate(path, _settings.RecursiveFontFolders);
                    if (!_settings.FontSourcePaths.Contains(path)) _settings.FontSourcePaths.Add(path);
                    _newFontSource = "";
                    _sourceInputError = null;
                    RefreshFontSources();
                }
                catch (Exception ex) { _sourceInputError = ex.Message; }
            }
            if (_sourceInputError != null) list.Label(_sourceInputError);
            int remove = -1;
            for (int i = 0; i < _settings.FontSourcePaths.Count; i++)
            {
                Rect row = list.GetRect(Math.Max(36, Text.CalcHeight(_settings.FontSourcePaths[i], view.width - 85) + 8));
                Widgets.Label(new Rect(row.x, row.y, row.width - 85, row.height), _settings.FontSourcePaths[i]);
                if (Widgets.ButtonText(new Rect(row.xMax - 80, row.y, 80, 28), "CustomFonts.RemoveFontPath".Translate())) remove = i;
            }
            if (remove >= 0) { _settings.FontSourcePaths.RemoveAt(remove); RefreshFontSources(); }
            if (list.ButtonText("CustomFonts.RefreshFontSources".Translate())) RefreshFontSources();
            list.Label("CustomFonts.FontSourceCount".Translate(OSFontPaths.Count, _fontSourceErrors.Count));
            if (_fontSourceErrors.Count > 0) list.Label(_fontSourceErrors[0]);
            list.GapLine();
            DrawWaylithDiagnostics(list);
            _sourceContentHeight = list.CurHeight + 16;
            list.End();
            Widgets.EndScrollView();
        }

        private void DrawWaylithDiagnostics(Listing_Standard list)
        {
            if (!Prefs.DevMode) return;
            list.GapLine();
            list.Label("CustomFonts.WaylithFontRegistrations".Translate(_registeredFileFonts.Count));
            list.CheckboxLabeled("CustomFonts.DebugLogging".Translate(), ref _settings.DebugLogging,
                "CustomFonts.WaylithDebugLoggingDesc".Translate());
            GUI.enabled = _settings.DebugLogging;
            if (list.ButtonTextLabeled("CustomFonts.DebugLogStats".Translate(), "CustomFonts.DebugLogNow".Translate()))
                LogResourceStats("manual Waylith");
            if (list.ButtonTextLabeled("CustomFonts.WaylithDebugStressTest".Translate(), "CustomFonts.DebugStressTestRun".Translate()))
                BeginFontStressTest();
            if (list.ButtonTextLabeled("CustomFonts.DebugWorldFontTest".Translate(), "CustomFonts.DebugWorldFontTestRun".Translate()))
                BeginWorldFontTest();
            GUI.enabled = true;
        }
    }
}
