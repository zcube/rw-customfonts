using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Verse;

namespace CustomFonts
{
    public partial class CustomFonts
    {
        private int _previewVerifyStage, _previewVerifyWait, _previewStressNext;
        private PreviewReviewWindow _previewReviewWindow;
        private bool _previewVerifySizeChanged;
        private int _previewVerifyOriginalSize;
        public void TickFontPreviewVerification()
        {
            if (_previewVerifyStage < 0 || !FontVerificationOptions.TryGet("customfonts-preview-verify", out string folder)) return;
            Application.runInBackground = true; // Isolated verification runs must keep rendering while unfocused.
            if (Event.current.type != EventType.Repaint || LongEventHandler.AnyEventNowOrWaiting) return;
            try
            {
                if (_previewVerifyStage == 0)
                {
                    Require(!_settings.ShowFontNamePreviews, "font-name previews default to disabled");
                    _previewVerifyOriginalSize = FontPreviewPixelSize;
                    foreach (var size in PreviewFonts) _settings.SetScaleFactor(size, 1f);
                    _settings.ShowFontNamePreviews = true;
                    _settings.IncludeSystemFonts = false;
                    _settings.FontSourcePaths.Clear(); _settings.FontSourcePaths.Add(folder);
                    RefreshFontSources(); EnsureFontPreviews();
                    Require(OSFontPaths.Count >= 3, "preview verification has real font sources");
                    VerifyFontStyleChoices(folder);
                    VerifyAutomaticFontRefresh(folder);
                    VerifyFontFavoriteControls();
                    _previewReviewWindow = new PreviewReviewWindow(this);
                    Find.WindowStack.Add(_previewReviewWindow);
                    _previewVerifyStage = 1;
                }
                if (_previewVerifyStage == 1 && _previewJobs.Count >= 3 && _previewCompleted == _previewJobs.Count)
                {
                    if (!_previewVerifySizeChanged)
                    {
                        string marker = Path.Combine(FontPreviewDirectory, "size-invalidation.preview");
                        File.WriteAllBytes(marker, new byte[] { 1 });
                        SetFontNamePreviewSize(FontPreviewPixelSize);
                        Require(File.Exists(marker), "applying unchanged preview size preserves cached images");
                        int changedSize = FontPreviewPixelSize == 32 ? 24 : 32;
                        SetFontNamePreviewSize(changedSize);
                        Require(!Directory.EnumerateFiles(FontPreviewDirectory).Any() && _previewJobs.Count == 0
                            && _previewTextures.Count == 0 && _previewDisk == null, "size change deletes all disk and memory previews before regeneration");
                        Require(PreviewLabelStyle.fontSize == changedSize && FontPreviewHeight == changedSize * 2,
                            "requested preview size controls labels and raster height");
                        _previewVerifySizeChanged = true; EnsureFontPreviews();
                        return;
                    }
                    foreach (var job in _previewJobs.Values)
                    {
                        Require(job.Key != null, "raster generated: " + job.Name);
                        var image = _previewDisk.Read(job.Key);
                        Require(image != null && image.Alpha.Any(a => a > 0) && FontPreviewLayout.HasClearBorder(image), "persistent preview has visible pixels: " + job.Name);
                        File.WriteAllBytes(Path.Combine(folder, "preview-" + job.Key + ".pgm"),
                            System.Text.Encoding.ASCII.GetBytes("P5\n" + image.Width + " " + image.Height + "\n255\n").Concat(image.Alpha).ToArray());
                    }
                    string italic = Path.Combine(folder, "ABeeZee-Italic.ttf");
                    if (File.Exists(italic))
                    {
                        var probe = RenderFontPreview(italic, "jÁgypQ ÇÅÉ");
                        Require(probe != null && FontPreviewLayout.HasClearBorder(probe),
                            "italic overhangs, accents and descenders retain padding when they fit");
                        int inkTop = 0, inkBottom = probe.Height - 1;
                        while (inkTop < probe.Height && !probe.Alpha.Skip(inkTop * probe.Width).Take(probe.Width).Any(a => a > 0)) inkTop++;
                        while (inkBottom >= 0 && !probe.Alpha.Skip(inkBottom * probe.Width).Take(probe.Width).Any(a => a > 0)) inkBottom--;
                        Require(inkTop <= inkBottom && Math.Abs(inkTop - (probe.Height - 1 - inkBottom)) <= 2,
                            "fitting preview ink is vertically centered");
                        var clipped = RenderFontPreview(italic, "ÁjgypQ", 24);
                        Require(clipped != null && clipped.Height == 24 && clipped.Alpha.Any(a => a > 0)
                            && clipped.Alpha.Skip((clipped.Height - 1) * clipped.Width).Any(a => a > 0),
                            "oversized glyphs produce an actual top-left cropped image instead of a plain label");
                        int savedSize = _settings.FontNamePreviewSize;
                        try
                        {
                            foreach (int pixels in new[] { 12, 40 })
                            {
                                _settings.FontNamePreviewSize = pixels;
                                const string longName = "ABeeZee Regular - A long external font mod name with wrapped preview lines";
                                var narrow = RenderFontPreview(italic, longName, wrapWidth: 120);
                                var wide = RenderFontPreview(italic, longName, wrapWidth: 240);
                                Require(narrow != null && wide != null && narrow.Width <= 120 && wide.Width <= 240
                                    && narrow.Height > FontPreviewHeight && wide.Height <= narrow.Height,
                                    "actual raster wraps at the column width using preview size " + pixels);
                                for (int line = 0; line < narrow.Height / FontPreviewHeight; line++)
                                    Require(narrow.Alpha.Skip(line * FontPreviewHeight * narrow.Width)
                                        .Take(FontPreviewHeight * narrow.Width).Any(a => a > 0), "wrapped raster line retains visible glyphs");
                                File.WriteAllBytes(Path.Combine(folder, "wrapped-" + pixels + ".pgm"),
                                    System.Text.Encoding.ASCII.GetBytes("P5\n" + narrow.Width + " " + narrow.Height + "\n255\n").Concat(narrow.Alpha).ToArray());
                            }
                        }
                        finally { _settings.FontNamePreviewSize = savedSize; }
                    }
                    var first = _previewJobs.Values.First();
                    var original = FontPreviewTexture(first.Name);
                    Require(original != null, "cached image uploaded for listing");
                    float pickerWidth = _previewWrapWidth + 100f;
                    float height = FontChoiceHeight(first.Name, pickerWidth);
                    _settings.SetScaleFactor(GameFont.Medium, 3f); RecalcCustomLineHeights();
                    Require(FontChoiceHeight(first.Name, pickerWidth) == height && ReferenceEquals(original, FontPreviewTexture(first.Name))
                        && PreviewLabelStyle.fontSize == FontPreviewPixelSize, "listing raster and row size are independent of configured UI size");
                    // Simulate a new session: discard memory textures and re-scan disk cache.
                    ResetFontPreviews(); _previewDisk = null; EnsureFontPreviews();
                    _previewVerifyWait = Time.frameCount + 30;
                    _previewVerifyStage = 2;
                }
                else if (_previewVerifyStage == 2 && Time.frameCount >= _previewVerifyWait && _previewCompleted == _previewJobs.Count)
                {
                    Require(_previewTextures.Count <= PreviewTextureLimit, "preview textures stay within memory budget");
                    Require(!_pendingDestroy.Any(p => p.Target is Font), "temporary raster fonts have been released");
                    VerifyRetiredEngineFontResources();
                    // Exercise the real texture LRU using 80 small, disk-backed masks.
                    var sample = _previewDisk.Read(_previewJobs.Values.First().Key);
                    for (int i = 0; i < 80; i++)
                    {
                        string name = "preview-stress-" + i;
                        string key = FontPreviewCache.Key(name, 1, 1, "test", name, PreviewEngine);
                        _previewDisk.Write(key, sample);
                        _previewJobs[name] = new PreviewJob { Name = name, Key = key, Done = true };
                    }
                    _previewVerifyStage = 3;
                }
                else if (_previewVerifyStage == 3)
                {
                    while (_previewStressNext < 80 && _previewUploads < 4)
                    {
                        Require(FontPreviewTexture("preview-stress-" + _previewStressNext) != null, "stress texture uploads");
                        _previewStressNext++;
                    }
                    Require(_previewTextures.Count <= PreviewTextureLimit, "real texture LRU remains bounded during scrolling");
                    if (_previewStressNext < 80) return;
                    foreach (var entry in _previewTextures.Values) entry.Frame = Time.frameCount;
                    int pending = _pendingDestroy.Count;
                    _previewUploads = 0; // Exercise admission, not the per-frame upload limit.
                    Require(FontPreviewTexture("preview-stress-0") == null && _previewTextures.Count == PreviewTextureLimit
                        && _pendingDestroy.Count == pending, "a full visible working set never evicts itself to load excess rows");
                    // Exercise the actual 1.6/1.7 settings-close Harmony hook.
                    new RimWorld.Dialog_ModSettings(this).PreClose();
                    Require(_previewTextures.Count == 0 && _previewJobs.Count == 0 && _previewDisk == null,
                        "closing mod settings releases texture, queue and disk-cache references");
                    _previewReviewWindow.Close();
                    _previewVerifyStage = 4; _previewVerifyWait = Time.frameCount + 10;
                }
                else if (_previewVerifyStage == 4 && Time.frameCount >= _previewVerifyWait)
                {
                    Require(_previewTextures.Count == 0 && !_pendingDestroy.Any(p => p.Target is Texture2D),
                        "evicted and cleared preview textures are destroyed");
                    _settings.SetScaleFactor(GameFont.Medium, 1f); RecalcCustomLineHeights();
                    SetFontNamePreviewSize(_previewVerifyOriginalSize);
                    SetFontNamePreviewsEnabled(false);
                    Require(FontPreviewTexture(OSFontPaths.Keys.First()) == null && _previewJobs.Count == 0 && _previewDisk == null,
                        "disabled previews never queue work or open a disk cache");
                    string message = "[Custom Fonts][preview-verify] PASS: automatic source refresh on settings reopen; persisted styled UI/world selections and optional style filter; persisted favorites and picker controls; vertical centering and top-aligned oversized-glyph clipping; adjustable preview size and full cache invalidation; real raster pixels, persistent cache reuse, fixed listing size, transient font cleanup, viewport pinning, settings-close cleanup and disabled-preview bypass";
                    Log.Message(message);
                    if (FontVerificationOptions.TryGet("customfonts-preview-result", out string result)) File.WriteAllText(result, message);
                    _previewVerifyStage = -1;
                }
            }
            catch (Exception ex)
            {
                _previewVerifyStage = -1;
                string message = "[Custom Fonts][preview-verify] FAIL: " + ex;
                Log.Error(message);
                if (FontVerificationOptions.TryGet("customfonts-preview-result", out string result)) File.WriteAllText(result, message);
            }
        }
        private void VerifyAutomaticFontRefresh(string folder)
        {
            string path = Path.Combine(folder, "ABeeZee-Italic.ttf"), parked = path + ".autorefresh-test";
            if (!File.Exists(path)) return;
            Require(!File.Exists(parked), "automatic refresh fixture has no previous parked font");
            DateTime stamp = File.GetLastWriteTimeUtc(path);
            var previous = PreviewFonts.ToDictionary(size => size, size => _settings.GetCurrentUIFontName(size));
            string previousWorld = _settings.CurrentWorldFontName;
            var favorites = _settings.FavoriteFontNames.ToArray();
            const string italic = "ABeeZee (Italic)";
            try
            {
                SaveFontAll(italic); SaveWorldFont(italic);
                if (!IsFavoriteFont(italic)) ToggleFavoriteFont(italic);
                Require(!RefreshChangedFontsOnSettingsOpen(), "unchanged settings opening reuses the font catalogs");
                File.Move(path, parked);
                Require(!RefreshChangedFontsOnSettingsOpen() && OSFontPaths.ContainsKey(italic),
                    "an open settings window does not repeatedly scan sources");
                new RimWorld.Dialog_ModSettings(this).PreClose();
                Require(RefreshChangedFontsOnSettingsOpen() && !OSFontPaths.ContainsKey(italic),
                    "reopening settings detects removed fonts");
                Require(PreviewFonts.All(size => _settings.GetCurrentUIFontName(size) == italic)
                    && _settings.CurrentWorldFontName == italic && IsFavoriteFont(italic),
                    "missing fonts retain saved styled selections and favorites");
                File.Move(parked, path);
                new RimWorld.Dialog_ModSettings(this).PreClose();
                Require(RefreshChangedFontsOnSettingsOpen() && OSFontPaths.ContainsKey(italic)
                    && CurrentFonts[GameFont.Medium].name.Contains("Italic"),
                    "reopening settings detects added fonts and restores the actual styled face");
                File.SetLastWriteTimeUtc(path, stamp.AddSeconds(2));
                new RimWorld.Dialog_ModSettings(this).PreClose();
                Require(RefreshChangedFontsOnSettingsOpen(), "reopening settings detects modified font files");
                new RimWorld.Dialog_ModSettings(this).PreClose();
                Require(!RefreshChangedFontsOnSettingsOpen(), "unchanged reopen does not refresh again");
            }
            finally
            {
                if (File.Exists(parked)) File.Move(parked, path);
                File.SetLastWriteTimeUtc(path, stamp);
                RefreshFontSources();
                foreach (var choice in previous) SaveFont(choice.Key, choice.Value);
                SaveWorldFont(previousWorld);
                _settings.FavoriteFontNames.Clear(); _settings.FavoriteFontNames.AddRange(favorites);
                new RimWorld.Dialog_ModSettings(this).PreClose();
            }
        }

        private void VerifyFontStyleChoices(string folder)
        {
            if (!File.Exists(Path.Combine(folder, "ABeeZee-Italic.ttf"))) return;
            string regular = FontSourceCatalog.FindKey(OSFontPaths, "ABeeZee");
            string italic = FontSourceCatalog.FindKey(OSFontPaths, "ABeeZee (Italic)");
            Require(regular == "ABeeZee (Regular)" && italic == "ABeeZee (Italic)"
                && OSFontPaths[regular] != OSFontPaths[italic], "real styles have independent catalog sources");
            var previous = PreviewFonts.ToDictionary(size => size, size => _settings.GetCurrentUIFontName(size));
            string previousWorld = _settings.CurrentWorldFontName;
            SaveFontAll(regular);
            var regularFont = CurrentFonts[GameFont.Medium];
            SaveFontAll(italic); SaveWorldFont(italic);
            Require(PreviewFonts.All(size => _settings.GetCurrentUIFontName(size) == italic)
                && _settings.CurrentWorldFontName == italic && !ReferenceEquals(regularFont, CurrentFonts[GameFont.Medium])
                && CurrentFonts[GameFont.Medium].name.Contains("Italic")
                && GetWorldFontAsset().sourceFontFile.name.Contains("Italic"), "UI and world load the actual italic font");
            _fontStyleFilter = "Italic";
            Require(OSFontPaths.Keys.Where(name => FontSourceCatalog.MatchesStyle(name, _fontStyleFilter)).SequenceEqual(new[] { italic }),
                "style filter selects the actual italic face");
            _fontStyleFilter = null;
            Require(OSFontPaths.Keys.All(name => FontSourceCatalog.MatchesStyle(name, _fontStyleFilter))
                && _settings.CurrentWorldFontName == italic, "clearing the filter restores all styles without changing selection");
            WriteSettings();
            Require(Directory.EnumerateFiles(GenFilePaths.ConfigFolderPath, "Mod_*_CustomFonts.xml")
                .Any(path => File.ReadAllText(path).Contains(italic)), "styled selections persist in settings XML");
            foreach (var choice in previous) SaveFont(choice.Key, choice.Value);
            SaveWorldFont(previousWorld);
        }

        private void VerifyFontFavoriteControls()
        {
            string file = OSFontPaths.Keys.First(), bundle = BundledFonts.Keys.First();
            string before = string.Join("|", PreviewFonts.Select(f => _settings.GetCurrentUIFontName(f))) + "|" + _settings.CurrentWorldFontName;
            string fallbacks = FallbackSignature;
            _settings.FavoriteFontNames.Clear();
            ToggleFavoriteFont(file); ToggleFavoriteFont(bundle);
            var choices = FontFavoriteLists.Create(_settings.FavoriteFontNames, BundledFonts.Keys, OSFontPaths.Keys);
            Require(choices.Favorites.SequenceEqual(new[] { file, bundle }) && !choices.Installed.Contains(file)
                && !choices.Bundled.Contains(bundle), "system and bundled favorites precede bundles without duplicate rows");
            ToggleFavoriteFont(file.ToLowerInvariant());
            Require(!IsFavoriteFont(file) && IsFavoriteFont(bundle), "favorite control removes exactly its target");
            ToggleFavoriteFont(file);
            Require(before == string.Join("|", PreviewFonts.Select(f => _settings.GetCurrentUIFontName(f))) + "|" + _settings.CurrentWorldFontName
                && fallbacks == FallbackSignature && _previewJobs.Count == 0,
                "favorite controls persist choices without selecting fonts, changing fallbacks or loading previews");
        }
        private sealed class PreviewReviewWindow : Window
        {
            private readonly CustomFonts _mod;
            public override Vector2 InitialSize => new Vector2(900, 550);
            internal PreviewReviewWindow(CustomFonts mod) { _mod = mod; doCloseX = true; draggable = true; }
            public override void DoWindowContents(Rect rect)
            {
                float top = rect.y, originalHeight = rect.height;
                Rect footer = ReserveFontPreviewFooter(ref rect);
                Require(rect.y == top && rect.height == originalHeight - 28 && footer.y == top + originalHeight - 24,
                    "progress footer reserves constant bottom geometry regardless of completion");
                _mod.EnsureFontPreviews();
                GUI.Label(new Rect(rect.x, rect.y, rect.width, 40), "Cached font names: " + _mod.FontPreviewPixelSize + "px", _mod.PreviewLabelStyle);
                var picker = new Rect(rect.x, rect.y + 44, rect.width, Math.Max(1, rect.height - 44));
                if (Time.frameCount % 2 == 0) _mod.DrawFontSettings(GameFont.Small, picker);
                else _mod.DrawWorldFontSettings(picker);
                _mod.DrawFontPreviewProgress(footer);
                _mod.ProcessFontPreviews();
            }
        }
    }
}
