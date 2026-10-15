using System;
using System.Linq;
using Verse;

namespace CustomFonts
{
    public partial class CustomFonts
    {
        private void VerifyEngineSizeFallbacks()
        {
            foreach (var size in PreviewFonts)
            {
                var style = Text.fontStyles[(int)size];
                style.font.RequestCharactersInTexture("Wgj 한글", style.fontSize);
                foreach (char c in "Wgj한글")
                    Require(style.font.GetCharacterInfo(c, out var glyph, style.fontSize)
                        && glyph.glyphWidth > 0 && glyph.glyphHeight > 0,
                        "dynamic primary/fallback glyph rasterizes at the requested size: " + size + "/" + style.fontSize);
            }
        }
        private string DescribeVisualFontMetrics(string sample) => string.Join(" | ", PreviewFonts.Select(size =>
        {
            var style = Text.fontStyles[(int)size]; var font = CurrentFonts[size];
            font.RequestCharactersInTexture(sample, style.fontSize);
            int descender = 0;
            foreach (char c in sample)
                if (font.GetCharacterInfo(c, out var glyph, style.fontSize)) descender = Math.Min(descender, glyph.minY);
            return $"{size}: descender {descender}, offset {EffectiveVerticalOffset(size):F1}";
        }));
    }
}
