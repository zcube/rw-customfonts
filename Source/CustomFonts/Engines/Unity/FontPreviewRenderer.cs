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
        private FontPreviewImage RenderFontPreview(string path, string label, int previewHeight = 0)
        {
            if (!_fontEngineInitialized) { FontEngine.InitializeFontEngine(); _fontEngineInitialized = true; }
            try
            {
                if (FontEngine.LoadFontFace(path, FontPreviewPixelSize) != FontEngineError.Success) return null;
                var glyphs = new List<Glyph>();
                var layout = new FontPreviewLayout();
                float pen = 0;
                int width = FontPreviewImage.MaxWidth, height = previewHeight > 0 ? previewHeight : FontPreviewHeight;
                for (int i = 0; i < label.Length && i < 256; i++)
                {
                    uint scalar = char.IsHighSurrogate(label[i]) && i + 1 < label.Length && char.IsLowSurrogate(label[i + 1])
                        ? (uint)char.ConvertToUtf32(label[i], label[++i]) : label[i];
                    if (!FontEngine.TryGetGlyphWithUnicodeValue(scalar, GlyphLoadFlags.LOAD_RENDER, out var glyph)) continue;
                    var metrics = glyph.metrics;
                    int w = glyph.glyphRect.width, h = glyph.glyphRect.height;
                    int x = (int)Math.Floor(pen + metrics.horizontalBearingX);
                    int y = (int)Math.Round(metrics.horizontalBearingY) - h;
                    if (w > 0 && h > 0)
                    {
                        if (!layout.TryAdd(new FontPreviewRectangle(x, y, w, h))) break;
                        glyph.glyphRect = new GlyphRect(x, y, w, h); glyphs.Add(glyph);
                    }
                    pen += metrics.horizontalAdvance;
                }
                // TextCore needs complete glyph rectangles inside its scratch buffer.
                // Render there, then copy only the fixed-height top-left viewport.
                int rasterHeight = Math.Max(height, layout.RequiredHeight);
                if (rasterHeight > 4096) return null;
                if (!layout.TryPlace(rasterHeight, out int offsetX, out int offsetY, out int used, bottomUp: true)) return null;
                foreach (var glyph in glyphs)
                {
                    var rect = glyph.glyphRect;
                    glyph.glyphRect = new GlyphRect(rect.x + offsetX, rect.y + offsetY, rect.width, rect.height);
                }
                var mask = new byte[width * rasterHeight];
                if (RenderPreviewGlyphs(glyphs, 0, GlyphRenderMode.SMOOTH_HINTED, mask, width, rasterHeight) != FontEngineError.Success) return null;
                var alpha = new byte[used * height];
                for (int y = 0; y < height; y++) Array.Copy(mask, (rasterHeight - 1 - y) * width, alpha, y * used, used);
                var image = new FontPreviewImage { Width = used, Height = height, Alpha = alpha };
                return image;
            }
            finally { FontEngine.UnloadFontFace(); }
        }
        private static string PreviewEngine => "Unity-smooth-v2";
    }
}
