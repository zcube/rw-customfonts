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
                        var clipped = RenderFontPreview(italic, "ÁjgypQ", 24);
                        Require(clipped != null && clipped.Height == 24 && clipped.Alpha.Any(a => a > 0)
                            && clipped.Alpha.Skip((clipped.Height - 1) * clipped.Width).Any(a => a > 0),
                            "oversized glyphs produce an actual top-left cropped image instead of a plain label");
                    }
                    var first = _previewJobs.Values.First();
                    var original = FontPreviewTexture(first.Name);
                    Require(original != null, "cached image uploaded for listing");
                    float height = FontChoiceHeight(first.Name, 200);
                    _settings.SetScaleFactor(GameFont.Medium, 3f); RecalcCustomLineHeights();
                    Require(FontChoiceHeight(first.Name, 200) == height && ReferenceEquals(original, FontPreviewTexture(first.Name))
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
                    string message = "[Custom Fonts][preview-verify] PASS: persisted favorites and UI/world picker controls; top-left alignment and oversized-glyph clipping; adjustable preview size and full cache invalidation; real raster pixels, persistent cache reuse, fixed listing size, transient font cleanup, viewport pinning, settings-close cleanup and disabled-preview bypass";
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
