using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;

namespace CustomFonts
{
    public partial class CustomFonts
    {
        private static readonly Func<List<Glyph>, int, GlyphRenderMode, byte[], int, int, FontEngineError> RenderPreviewGlyphs
            = (Func<List<Glyph>, int, GlyphRenderMode, byte[], int, int, FontEngineError>)Delegate.CreateDelegate(
                typeof(Func<List<Glyph>, int, GlyphRenderMode, byte[], int, int, FontEngineError>),
                AccessTools.Method(typeof(FontEngine), "RenderGlyphsToTexture", new[] {
                    typeof(List<Glyph>), typeof(int), typeof(GlyphRenderMode), typeof(byte[]), typeof(int), typeof(int) }));
        private FontPreviewImage RenderFontPreview(string path, string label, int previewHeight = 0, int wrapWidth = 0)
        {
            if (!_fontEngineInitialized) { FontEngine.InitializeFontEngine(); _fontEngineInitialized = true; }
            try
            {
                if (FontEngine.LoadFontFace(path, FontPreviewPixelSize) != FontEngineError.Success) return null;
                var glyphs = new List<Glyph>();
                var characters = new List<FontPreviewGlyph>();
                if (wrapWidth > 0 && label.Length > 256) return null;
                int width = wrapWidth > 0 ? Math.Min(wrapWidth, FontPreviewImage.MaxWidth) : FontPreviewImage.MaxWidth;
                int lineHeight = previewHeight > 0 ? previewHeight : FontPreviewHeight;
                for (int i = 0; i < label.Length && i < 256; i++)
                {
                    uint scalar = char.IsHighSurrogate(label[i]) && i + 1 < label.Length && char.IsLowSurrogate(label[i + 1])
                        ? (uint)char.ConvertToUtf32(label[i], label[++i]) : label[i];
                    if (!FontEngine.TryGetGlyphWithUnicodeValue(scalar, GlyphLoadFlags.LOAD_RENDER, out var glyph)) continue;
                    var metrics = glyph.metrics;
                    int w = glyph.glyphRect.width, h = glyph.glyphRect.height;
                    int x = (int)Math.Floor(metrics.horizontalBearingX);
                    int y = (int)Math.Round(metrics.horizontalBearingY) - h;
                    characters.Add(new FontPreviewGlyph { Index = glyphs.Count, Scalar = (int)scalar,
                        Advance = metrics.horizontalAdvance, Bounds = new FontPreviewRectangle(x, y, w, h) });
                    glyphs.Add(glyph);
                }
                var lines = FontPreviewLines.Create(characters, width, wrapWidth > 0);
                if (lines == null || lines.Length == 0 || lines.Length > FontPreviewImage.MaxHeight / lineHeight) return null;
                int used = 0, height = lines.Length * lineHeight;
                foreach (var line in lines)
                {
                    if (!line.Layout.TryPlace(lineHeight, out _, out _, out int lineWidth)) return null;
                    used = Math.Max(used, lineWidth);
                }
                var alpha = new byte[used * height];
                for (int index = 0; index < lines.Length; index++)
                {
                    var line = lines[index];
                    // Keep complete glyphs in TextCore's scratch buffer, then crop each
                    // line separately so unusually tall ink cannot overlap the next line.
                    int rasterHeight = Math.Max(lineHeight, line.Layout.RequiredHeight);
                    if (rasterHeight > 4096 || !line.Layout.TryPlace(rasterHeight, out int offsetX,
                        out int offsetY, out int lineWidth, bottomUp: true)) return null;
                    var placed = new List<Glyph>();
                    foreach (var character in line.Glyphs)
                    {
                        var rect = character.Bounds;
                        if (rect.Width <= 0 || rect.Height <= 0) continue;
                        var glyph = glyphs[character.Index];
                        glyph.glyphRect = new GlyphRect(rect.X + offsetX, rect.Y + offsetY, rect.Width, rect.Height);
                        placed.Add(glyph);
                    }
                    var mask = new byte[width * rasterHeight];
                    if (RenderPreviewGlyphs(placed, 0, GlyphRenderMode.SMOOTH_HINTED, mask, width, rasterHeight) != FontEngineError.Success) return null;
                    for (int y = 0; y < lineHeight; y++)
                        Array.Copy(mask, (rasterHeight - 1 - y) * width, alpha, (index * lineHeight + y) * used, lineWidth);
                }
                var image = new FontPreviewImage { Width = used, Height = height, Alpha = alpha };
                return image;
            }
            finally { FontEngine.UnloadFontFace(); }
        }
        private static string PreviewEngine => "Unity-smooth-v3-wrap";
    }
}
