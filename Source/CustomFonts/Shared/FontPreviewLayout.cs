using System;
using System.Collections.Generic;

namespace CustomFonts
{
    internal struct FontPreviewRectangle
    {
        internal int X, Y, Width, Height;
        internal FontPreviewRectangle(int x, int y, int width, int height)
        { X = x; Y = y; Width = width; Height = height; }
    }

    // Both rasterizers place actual bitmap bounds, not advance widths or an
    // assumed baseline. Center ink vertically when it fits; oversized lines clip below.
    internal sealed class FontPreviewLayout
    {
        internal const int Padding = 2;
        private readonly int _maxWidth;
        private int _left, _right, _top, _bottom;
        private bool _hasInk;
        internal FontPreviewLayout(int maxWidth = FontPreviewImage.MaxWidth) { _maxWidth = maxWidth; }
        internal bool TryAdd(FontPreviewRectangle rect)
        {
            if (rect.Width <= 0 || rect.Height <= 0) return true;
            if (Math.Abs((long)rect.X) > 1000000 || Math.Abs((long)rect.Y) > 1000000
                || rect.Width > 1000000 || rect.Height > 1000000) return false;
            int left = _hasInk ? Math.Min(_left, rect.X) : rect.X;
            int right = _hasInk ? Math.Max(_right, rect.X + rect.Width) : rect.X + rect.Width;
            if (right - left > _maxWidth - Padding * 2) return false;
            _left = left; _right = right;
            _top = _hasInk ? Math.Min(_top, rect.Y) : rect.Y;
            _bottom = _hasInk ? Math.Max(_bottom, rect.Y + rect.Height) : rect.Y + rect.Height;
            _hasInk = true;
            return true;
        }
        internal int RequiredHeight => _hasInk ? _bottom - _top + Padding * 2 : 0;
        internal bool TryPlace(int height, out int offsetX, out int offsetY, out int width, bool bottomUp = false)
        {
            offsetX = offsetY = width = 0;
            if (!_hasInk || height < Padding * 2) return false;
            offsetX = Padding - _left;
            int insetY = Math.Max(Padding, (height - (_bottom - _top)) / 2);
            offsetY = bottomUp ? height - insetY - _bottom : insetY - _top;
            width = _right - _left + Padding * 2;
            return true;
        }
        internal static bool HasClearBorder(FontPreviewImage image)
        {
            for (int x = 0; x < image.Width; x++)
                if (image.Alpha[x] != 0 || image.Alpha[(image.Height - 1) * image.Width + x] != 0) return false;
            for (int y = 0; y < image.Height; y++)
                if (image.Alpha[y * image.Width] != 0 || image.Alpha[y * image.Width + image.Width - 1] != 0) return false;
            return true;
        }
    }

    internal struct FontPreviewGlyph
    {
        internal int Index, Scalar;
        internal float Advance;
        internal FontPreviewRectangle Bounds;
    }

    internal sealed class FontPreviewLine
    {
        internal FontPreviewLayout Layout;
        internal FontPreviewGlyph[] Glyphs;
    }

    // Break on spaces where possible, otherwise on whole Unicode glyphs. Both
    // renderers use actual advances and ink bounds at the requested preview size.
    internal static class FontPreviewLines
    {
        internal static FontPreviewLine[] Create(IReadOnlyList<FontPreviewGlyph> glyphs, int width, bool wrap = true)
        {
            var lines = new List<FontPreviewLine>();
            int start = 0;
            while (start < glyphs.Count)
            {
                var probe = new FontPreviewLayout(width);
                float pen = 0;
                int end = start, next = start, space = -1;
                for (int i = start; i < glyphs.Count; i++)
                {
                    var glyph = glyphs[i];
                    var rect = glyph.Bounds; rect.X += (int)Math.Floor(pen);
                    if (pen + glyph.Advance > width - FontPreviewLayout.Padding * 2 || !probe.TryAdd(rect))
                    {
                        if (i == start) return null;
                        end = wrap && space >= start ? space : i;
                        next = !wrap ? glyphs.Count : space >= start ? space + 1 : i;
                        break;
                    }
                    if (glyph.Scalar == ' ' || glyph.Scalar == '\t') space = i;
                    pen += glyph.Advance;
                    end = next = i + 1;
                }
                var layout = new FontPreviewLayout(width);
                var placed = new List<FontPreviewGlyph>();
                pen = 0;
                for (int i = start; i < end; i++)
                {
                    var glyph = glyphs[i]; glyph.Bounds.X += (int)Math.Floor(pen);
                    if (!layout.TryAdd(glyph.Bounds)) return null;
                    placed.Add(glyph); pen += glyph.Advance;
                }
                if (placed.Count > 0) lines.Add(new FontPreviewLine { Layout = layout, Glyphs = placed.ToArray() });
                start = next;
                while (start < glyphs.Count && (glyphs[start].Scalar == ' ' || glyphs[start].Scalar == '\t')) start++;
                if (!wrap) break;
            }
            return lines.ToArray();
        }
    }
}
