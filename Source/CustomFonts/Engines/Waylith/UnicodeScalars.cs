namespace CustomFonts
{
    internal static class UnicodeScalars
    {
        // Keep UTF-16 indices for RimWorld's curved-text transforms, but draw each scalar
        // once, at its leading code unit. Invalid surrogate units become U+FFFD.
        internal static int At(string text, int index)
        {
            if (text == null || index < 0 || index >= text.Length) return -1;
            char c = text[index];
            if (char.IsHighSurrogate(c))
                return index + 1 < text.Length && char.IsLowSurrogate(text[index + 1])
                    ? char.ConvertToUtf32(c, text[index + 1]) : 0xfffd;
            if (char.IsLowSurrogate(c))
                return index > 0 && char.IsHighSurrogate(text[index - 1]) ? -1 : 0xfffd;
            return c;
        }
    }
}
