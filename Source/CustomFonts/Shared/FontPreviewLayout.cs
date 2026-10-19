using System;

namespace CustomFonts
{
    internal struct FontPreviewRectangle
    {
        internal int X, Y, Width, Height;
        internal FontPreviewRectangle(int x, int y, int width, int height)
        { X = x; Y = y; Width = width; Height = height; }
    }

    // Both rasterizers place actual bitmap bounds, not advance widths or an
    // assumed baseline. Anchor ink at the top left; the fixed row may clip its bottom.
    internal sealed class FontPreviewLayout
    {
        internal const int Padding = 2;
        private int _left, _right, _top, _bottom;
        private bool _hasInk;
        internal bool TryAdd(FontPreviewRectangle rect)
        {
            if (rect.Width <= 0 || rect.Height <= 0) return true;
            if (Math.Abs((long)rect.X) > 1000000 || Math.Abs((long)rect.Y) > 1000000
                || rect.Width > 1000000 || rect.Height > 1000000) return false;
            int left = _hasInk ? Math.Min(_left, rect.X) : rect.X;
            int right = _hasInk ? Math.Max(_right, rect.X + rect.Width) : rect.X + rect.Width;
            if (right - left > FontPreviewImage.MaxWidth - Padding * 2) return false;
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
            offsetY = bottomUp ? height - Padding - _bottom : Padding - _top;
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
}
