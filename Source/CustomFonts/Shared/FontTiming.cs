using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Verse;

namespace CustomFonts
{
    public partial class CustomFonts
    {
        private FontMetadataCache _metadataCache;
        private FontMetadataCache MetadataCache => _metadataCache ?? (_metadataCache = new FontMetadataCache(
            Path.Combine(GenFilePaths.ConfigFolderPath, "CustomFontsCache")));

        internal void LogFontTiming(string operation, Stopwatch timer, string detail = "")
        {
            if (FontVerificationOptions.TryGet("customfonts-profile", out string value) && value == "1")
                Log.Message("[Custom Fonts][timing] " + operation + ": " + timer.Elapsed.TotalMilliseconds.ToString("F1", CultureInfo.InvariantCulture) + " ms " + detail);
        }
    }
}
