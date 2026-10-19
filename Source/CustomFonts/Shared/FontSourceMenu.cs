using System;
using UnityEngine;
using Verse;

namespace CustomFonts
{
    public partial class CustomFonts
    {
        private string _newFontSource = "";
        private string _sourceInputError;
        private float _previewSizeDraft;
        private Vector2 _sourceScroll;
        private float _sourceContentHeight = 500f;

        public void DrawFontSourceSettings(Rect rect)
        {
            var outer = new Rect(rect.x, rect.y, rect.width, rect.height);
            var view = new Rect(0, 0, Math.Max(100, rect.width - 20), Math.Max(rect.height, _sourceContentHeight));
            _sourceScroll.x = 0;
            _sourceScroll.y = Mathf.Clamp(_sourceScroll.y, 0, Math.Max(0, view.height - outer.height));
            Widgets.BeginScrollView(outer, ref _sourceScroll, view);
            var list = new Listing_Standard();
            // Listing starts a new column when it reaches its bounds. Measure all rows
            // in one tall column; the scroll view supplies the visible clipping bounds.
            list.Begin(new Rect(0, 0, view.width, 1000000f));
            bool previews = _settings.ShowFontNamePreviews;
            list.CheckboxLabeled("CustomFonts.ShowFontNamePreviews".Translate(), ref previews,
                "CustomFonts.ShowFontNamePreviewsHelp".Translate());
            if (previews != _settings.ShowFontNamePreviews) SetFontNamePreviewsEnabled(previews);
            if (_previewSizeDraft <= 0) _previewSizeDraft = FontPreviewPixelSize;
            list.Label("CustomFonts.PreviewSize".Translate((int)_previewSizeDraft));
            _previewSizeDraft = Widgets.HorizontalSlider(list.GetRect(24f), _previewSizeDraft,
                FontPreviewImage.MinPixelSize, FontPreviewImage.MaxPixelSize, true, roundTo: 1f);
            list.Label("CustomFonts.PreviewSizeHelp".Translate());
            if (list.ButtonText("CustomFonts.Apply".Translate()))
            {
                try { SetFontNamePreviewSize((int)Math.Round(_previewSizeDraft)); }
                catch (Exception ex) { _sourceInputError = ex.Message; }
            }
            list.GapLine();
            list.Label("CustomFonts.FontSources".Translate());
            list.Label("CustomFonts.FontSourcesHelp".Translate());
            list.CheckboxLabeled("CustomFonts.IncludeSystemFonts".Translate(), ref _settings.IncludeSystemFonts);
            list.CheckboxLabeled("CustomFonts.RecursiveFolders".Translate(), ref _settings.RecursiveFontFolders);
            list.GapLine();
            list.Label("CustomFonts.SystemFontDirectories".Translate(CurrentFontPlatform().ToString()));
            list.Label("CustomFonts.SystemFontDirectoriesHelp".Translate());
            foreach (string path in SystemFontSearchRoots) list.Label(path);
            list.GapLine();
            _newFontSource = list.TextEntryLabeled("CustomFonts.FontPath".Translate(), _newFontSource);
            if (list.ButtonText("CustomFonts.AddFontPath".Translate()))
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(_newFontSource)) throw new ArgumentException("CustomFonts.EmptyFontPath".Translate());
                    string expression = _newFontSource.Trim().Trim('"');
                    string path = FontFiles.Normalize(expression, _content.RootDir);
                    // Validate existence/type before saving. Detailed file errors remain visible after scan.
                    FontFiles.Enumerate(path, _settings.RecursiveFontFolders);
                    if (!_settings.FontSourcePaths.Exists(existing => FontFiles.SameLocation(existing, path, _content.RootDir)))
                        // Keep ~, environment variables and relative paths portable.
                        // Expand only for validation and discovery on this machine.
                        _settings.FontSourcePaths.Add(expression);
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
            list.GapLine();
            list.Label("CustomFonts.FontPackSearch".Translate(_fontPackScans.Count));
            list.Label("CustomFonts.FontPackSearchHelp".Translate());
            if (_fontPackScans.Count == 0) list.Label("CustomFonts.FontPackSearchEmpty".Translate());
            foreach (var pack in _fontPackScans)
            {
                string label = "CustomFonts.FontPackSearchEntry".Translate(pack.Name, pack.FontCount);
                Rect row = list.GetRect(Math.Max(Text.LineHeight, Text.CalcHeight(label, view.width)));
                Widgets.Label(row, label);
                TooltipHandler.TipRegion(row, pack.PackageId);
            }
            list.GapLine();
            if (list.ButtonText("CustomFonts.RefreshFontSources".Translate())) RefreshFontSources();
            list.Label("CustomFonts.FontSourceCount".Translate(OSFontPaths.Count, _fontSourceErrors.Count));
            if (_fontSourceErrors.Count > 0) list.Label(_fontSourceErrors[0]);
            list.GapLine();
            DrawDebugSettings(list);
            _sourceContentHeight = list.CurHeight + 16;
            list.End();
            Widgets.EndScrollView();
        }

    }
}
