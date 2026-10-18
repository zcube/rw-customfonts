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
                Require(font.dynamic && _importedFontBundles.ContainsKey(font), "pack registered as imported dynamic font");
                font.RequestCharactersInTexture("AWgj", pixels);
                foreach (char c in "AWgj")
                    Require(font.GetCharacterInfo(c, out var glyph, pixels) && glyph.advance > 0
                        && glyph.glyphWidth > 0 && glyph.glyphHeight > 0, "Unity pack glyph rasterizes: " + size + "/" + c);
                Require(font.material != null && font.material.mainTexture != null, "pack UI atlas exists");
            }
        }
    }
}
