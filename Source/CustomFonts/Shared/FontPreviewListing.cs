using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;
using Verse;

namespace CustomFonts
{
    public partial class CustomFonts
    {
        private sealed class PreviewJob { internal string Name, Path, Key; internal bool Done; internal int RequestedFrame; }
        private sealed class PreviewTexture { internal Texture2D Texture; internal int Used, Frame; }
        private const int PreviewTextureLimit = 64;
        private Dictionary<string, PreviewJob> _previewJobs = new Dictionary<string, PreviewJob>();
        private Dictionary<string, PreviewTexture> _previewTextures = new Dictionary<string, PreviewTexture>();
        private Queue<PreviewJob> _previewPriority = new Queue<PreviewJob>();
        private HashSet<PreviewJob> _previewPrioritized = new HashSet<PreviewJob>();
        private FontPreviewCache _previewDisk;
        private string _previewLocale;
        private int _previewCompleted, _previewFrame = -1, _previewRasterFrame = -10, _previewClock;
        private int _previewUploads;
        private GUIStyle _previewLabelStyle;
        private float _choiceVisibleTop, _choiceVisibleBottom = float.MaxValue;

        private GUIStyle PreviewLabelStyle => _previewLabelStyle ?? (_previewLabelStyle = new GUIStyle
        {
            font = DefaultFonts[GameFont.Small], fontSize = FontPreviewPixelSize, richText = false,
            alignment = TextAnchor.UpperLeft, wordWrap = false,
            normal = { textColor = Color.white }
        });

        private GUIStyle _previewProgressStyle, _previewButtonStyle;
        private GUIStyle PreviewProgressStyle => _previewProgressStyle ?? (_previewProgressStyle = new GUIStyle(PreviewLabelStyle) { fontSize = 16 });
        private GUIStyle PreviewButtonStyle => _previewButtonStyle ?? (_previewButtonStyle = new GUIStyle(PreviewLabelStyle) { alignment = TextAnchor.MiddleCenter, fontSize = 20 });
        private bool FixedPreviewButton(Rect rect, string text)
        {
            bool clicked = Widgets.ButtonText(rect, "");
            GUI.Label(rect, text, PreviewButtonStyle);
            return clicked;
        }

        private int FontPreviewPixelSize => FontPreviewImage.ClampSize(_settings.FontNamePreviewSize);
        private int FontPreviewHeight => FontPreviewImage.HeightFor(FontPreviewPixelSize);
        private static string FontPreviewDirectory => Path.Combine(GenFilePaths.ConfigFolderPath, "CustomFontsCache", "Previews");
        private static Rect ReserveFontPreviewFooter(ref Rect rect)
        {
            // Reserve the same area while idle, loading, disabled and completed.
            // A progress update must never move the list or its scroll position.
            var footer = new Rect(rect.x, rect.yMax - 24f, rect.width, 24f);
            rect.height = Math.Max(0f, rect.height - 28f);
            return footer;
        }
        private void SetFontNamePreviewSize(int pixels)
        {
            pixels = FontPreviewImage.ClampSize(pixels);
            if (pixels == FontPreviewPixelSize) return;
            ReleaseFontPreviewMemory();
            new FontPreviewCache(FontPreviewDirectory).Clear();
            _settings.FontNamePreviewSize = pixels;
            WriteSettings();
        }

        private void ResetFontPreviews()
        {
            foreach (var entry in _previewTextures.Values) QueueDestroy(entry.Texture);
            _previewTextures = new Dictionary<string, PreviewTexture>();
            _previewJobs = new Dictionary<string, PreviewJob>();
            _previewPriority = new Queue<PreviewJob>();
            _previewPrioritized = new HashSet<PreviewJob>();
            _previewCompleted = 0;
            _previewLocale = null;
        }
        internal void ReleaseFontPreviewMemory()
        {
            ResetFontPreviews();
            _previewDisk = null;
            _previewLabelStyle = null; _previewButtonStyle = null; _previewProgressStyle = null;
        }
        private void SetFontNamePreviewsEnabled(bool enabled)
        {
            _settings.ShowFontNamePreviews = enabled;
            if (!enabled) ReleaseFontPreviewMemory();
            WriteSettings();
        }
        private void EnsureFontPreviews()
        {
            string locale = LanguageDatabase.activeLanguage?.folderName ?? "English";
            if (_previewLocale == locale) return;
            ResetFontPreviews(); _previewLocale = locale;
            if (_previewDisk == null) _previewDisk = new FontPreviewCache(FontPreviewDirectory);
            // Catalog discovery stays metadata-only. Preview jobs are created only
            // when an actual visible row asks for its image.
        }

        private void ProcessFontPreviews()
        {
            if (!_settings.ShowFontNamePreviews || Event.current.type != EventType.Repaint || _previewFrame == Time.frameCount
                || LongEventHandler.AnyEventNowOrWaiting) return;
            _previewFrame = Time.frameCount; _previewUploads = 0;
            var timer = Stopwatch.StartNew();
            int examined = 0;
            while (examined++ < 8 && timer.ElapsedMilliseconds < 4 && _previewPriority.Count > 0)
            {
                var job = _previewPriority.Dequeue();
                _previewPrioritized.Remove(job);
                if (job.Done || job.RequestedFrame != Time.frameCount) continue;
                try
                {
                    string label = DisplayFontName(job.Name);
                    var file = new FileInfo(job.Path);
                    job.Key = FontPreviewCache.Key(job.Path, file.Length, file.LastWriteTimeUtc.Ticks, _previewLocale, label, PreviewEngine, FontPreviewPixelSize);
                    var image = _previewDisk.Read(job.Key);
                    if (image == null)
                    {
                        // At most one transient native face is alive at a time. Destruction
                        // happens in StartOfOnGUI after two frames, before the next bake.
                        if (Time.frameCount - _previewRasterFrame < 3)
                        { if (_previewPrioritized.Add(job)) _previewPriority.Enqueue(job); break; }
                        _previewRasterFrame = Time.frameCount;
                        // An unusually large source still has a readable fixed-size label.
                        // Preview creation never needs to materialize a >32 MiB font.
                        if (file.Length <= 32 * 1024 * 1024) image = RenderFontPreview(job.Path, label);
                        if (image != null) _previewDisk.Write(job.Key, image);
                        else job.Key = null;
                        Complete(job);
                        break;
                    }
                    Complete(job);
                }
                catch (Exception ex)
                {
                    job.Key = null;
                    Complete(job);
                    DebugLog("Font preview skipped: " + job.Name + ": " + ex.Message);
                }
            }
        }
        private void Complete(PreviewJob job) { job.Done = true; _previewCompleted++; }
        private void DrawFontPreviewProgress(Rect bar)
        {
            if (!_settings.ShowFontNamePreviews) return;
            var visible = _previewJobs.Values.Where(j => j.RequestedFrame >= Time.frameCount - 1).ToArray();
            int completed = visible.Count(j => j.Done);
            if (completed >= visible.Length) return;
            Widgets.DrawBoxSolid(bar, new Color(0.14f, 0.14f, 0.14f));
            Widgets.DrawBoxSolid(new Rect(bar.x, bar.y, bar.width * completed / Math.Max(1, visible.Length), bar.height),
                new Color(0.2f, 0.4f, 0.5f));
            GUI.Label(bar.ContractedBy(3), "CustomFonts.PreviewProgress".Translate(completed, visible.Length), PreviewProgressStyle);
        }
        private Texture2D FontPreviewTexture(string name)
        {
            if (!_settings.ShowFontNamePreviews) return null;
            if (!_previewJobs.TryGetValue(name, out var job))
            {
                string path;
                if (_bundledFontSources.TryGetValue(name, out var bundle)) path = bundle.Path;
                else if (!OSFontPaths.TryGetValue(name, out path)) return null;
                job = new PreviewJob { Name = name, Path = path };
                _previewJobs[name] = job;
            }
            job.RequestedFrame = Time.frameCount;
            if (!job.Done)
            {
                if (_previewPrioritized.Add(job)) _previewPriority.Enqueue(job);
                return null;
            }
            if (job.Key == null) return null;
            if (_previewTextures.TryGetValue(job.Key, out var entry))
            { entry.Used = ++_previewClock; entry.Frame = Time.frameCount; return entry.Texture; }
            // Only visible rows upload textures; bounding uploads also bounds deferred
            // destruction, even while dragging the scrollbar through thousands of fonts.
            if (Event.current.type != EventType.Repaint || _previewUploads >= 4) return null;
            // Keep the current/previous viewport pinned. If more than 64 rows
            // fit on screen, excess rows keep plain labels instead of thrashing.
            string victim = null;
            if (_previewTextures.Count >= PreviewTextureLimit)
            {
                victim = _previewTextures.Where(p => p.Value.Frame < Time.frameCount - 1)
                    .OrderBy(p => p.Value.Used).Select(p => p.Key).FirstOrDefault();
                if (victim == null) return null;
            }
            _previewUploads++;
            var image = _previewDisk.Read(job.Key);
            if (image == null) { job.Key = null; return null; }
            if (victim != null)
            {
                var retired = _previewTextures[victim];
                _previewTextures.Remove(victim); QueueDestroy(retired.Texture);
            }
            var colors = new Color32[image.Alpha.Length];
            for (int y = 0; y < image.Height; y++)
                for (int x = 0; x < image.Width; x++)
                    colors[(image.Height - 1 - y) * image.Width + x]
                        = new Color32(255, 255, 255, image.Alpha[y * image.Width + x]);
            var texture = new Texture2D(image.Width, image.Height, TextureFormat.RGBA32, false);
            texture.name = "CustomFonts preview";
            texture.SetPixels32(colors); texture.Apply(false, true);
            _previewTextures[job.Key] = new PreviewTexture { Texture = texture, Used = ++_previewClock, Frame = Time.frameCount };
            return texture;
        }
        private void DrawFontPreviewLabel(Rect rect, string name)
        {
            var texture = FontPreviewTexture(name);
            if (texture == null) { GUI.Label(rect, DisplayFontName(name), PreviewLabelStyle); return; }
            // Clip long names; never scale the raster to the available row width.
            GUI.BeginGroup(rect);
            try { GUI.DrawTexture(new Rect(0, 0, texture.width, texture.height), texture); }
            finally { GUI.EndGroup(); }
        }
    }
}
