using System;
using System.Collections.Generic;
using Brutal.ImGuiApi;
using UnityEngine;
using UnityStubs.Gui;

namespace CustomFonts
{
    public partial class CustomFonts
    {
        private unsafe FontPreviewImage RenderFontPreview(string path, string label, int previewHeight = 0)
        {
            Font font = null;
            try
            {
                byte[] bytes = FontCollections.FirstFace(FontFiles.Read(path));
                font = RegisterScopedFont(bytes, "Font preview", FallbackFontSource.Hash(bytes) + ":preview",
                    FontPreviewPixelSize, 0, new FallbackFontSource[0]);
                var scalars = new List<int>();
                for (int i = 0; i < label.Length && i < 256; i++)
                {
                    int scalar = UnicodeScalars.At(label, i);
                    if (scalar < 0) continue;
                    scalars.Add(scalar);
                    NativeFontAccess.TryGlyph(font.GameFontIndex, scalar, FontPreviewPixelSize, out _);
                }
                // Bake all glyphs first, then reread UVs: baking can resize the atlas.
                var texture = ImGui.GetIO().Fonts.TexData;
                if (texture.IsNull() || texture.Width <= 0 || texture.Height <= 0) return null;
                int stride = texture.BytesPerPixel;
                if (stride != 1 && stride != 4) return null;
                int height = previewHeight > 0 ? previewHeight : FontPreviewHeight;
                var layout = new FontPreviewLayout();
                var glyphs = new List<TmpGlyphMetrics>();
                var rectangles = new List<FontPreviewRectangle>();
                float pen = 0;
                foreach (int scalar in scalars)
                {
                    if (!NativeFontAccess.TryGlyph(font.GameFontIndex, scalar, FontPreviewPixelSize, out var glyph)) continue;
                    int w = (int)Math.Round((glyph.u1 - glyph.u0) * texture.Width);
                    int h = (int)Math.Round((glyph.v1 - glyph.v0) * texture.Height);
                    if (glyph.visible && w > 0 && h > 0)
                    {
                        var rect = new FontPreviewRectangle((int)Math.Round(pen + glyph.x0), (int)Math.Round(glyph.y0), w, h);
                        if (!layout.TryAdd(rect)) break;
                        rectangles.Add(rect); glyphs.Add(glyph);
                    }
                    pen += glyph.advanceX;
                }
                if (!layout.TryPlace(height, out int offsetX, out int offsetY, out int width)) return null;
                byte* pixels = (byte*)(void*)texture.GetPixelsAt(0, 0);
                if (pixels == null) return null;
                var mask = new byte[width * height];
                bool visible = false;
                for (int i = 0; i < glyphs.Count; i++)
                {
                    var glyph = glyphs[i]; var rect = rectangles[i];
                    int x0 = (int)Math.Round(glyph.u0 * texture.Width), y0 = (int)Math.Round(glyph.v0 * texture.Height);
                    if (x0 < 0 || y0 < 0 || x0 + rect.Width > texture.Width || y0 + rect.Height > texture.Height) return null;
                    // Clip the destination, without dropping or scaling the glyph.
                    int startY = Math.Max(0, -(rect.Y + offsetY));
                    int endY = Math.Min(rect.Height, height - rect.Y - offsetY);
                    for (int y = startY; y < endY; y++)
                        for (int x = 0; x < rect.Width; x++)
                        {
                            int dx = rect.X + offsetX + x, dy = rect.Y + offsetY + y;
                            byte alpha = pixels[((y0 + y) * texture.Width + x0 + x) * stride + stride - 1];
                            int at = dy * width + dx;
                            mask[at] = (byte)(alpha + mask[at] * (255 - alpha) / 255);
                            if (alpha > 0) visible = true;
                        }
                }
                if (!visible) return null;
                var image = new FontPreviewImage { Width = width, Height = height, Alpha = mask };
                return image;
            }
            finally { if (font != null) QueueDestroy(font); }
        }
        private static string PreviewEngine => "Waylith";
    }
}
