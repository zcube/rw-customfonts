using UnityEngine;
using Verse;

namespace CustomFonts
{
    public partial class CustomFonts
    {
        partial void VerifyFontPackUiGlyphs()
        {
            foreach (var size in PreviewFonts)
            {
                var font = CurrentFonts[size];
                int pixels = Text.fontStyles[(int)size].fontSize;
                Require(font.GameFontIndex >= 0, "pack registered in native renderer");
                foreach (char c in "AWgj")
                    Require(NativeFontAccess.TryGlyph(font.GameFontIndex, c, pixels, out var glyph)
                        && glyph.visible && glyph.advanceX > 0, "Waylith pack glyph rasterizes: " + size + "/" + c);
            }
        }
    }
}
