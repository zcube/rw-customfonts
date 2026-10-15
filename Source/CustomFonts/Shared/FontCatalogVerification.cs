using System;
using System.Linq;
using UnityEngine;
using Verse;

namespace CustomFonts
{
    public partial class CustomFonts
    {
        private static void Require(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException(description);
        }
        // Invoked only by the explicit, isolated-profile integration checks.
        private void VerifySharedFontCatalog()
        {
            _settings.IncludeSystemFonts = true;
            foreach (var size in PreviewFonts) _settings.SetScaleFactor(size, 1f);
            RefreshFontSources();
            Require(_fontNames.SequenceEqual(OSFontPaths.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase)),
                "UI and world use the same shared file catalog");
            if (CurrentFontPlatform() != FontPlatform.Windows) return;
            string regular = FontSourceCatalog.FindKey(OSFontPaths, "Malgun Gothic");
            Require(regular != null && regular == "Malgun Gothic", "Windows catalog retains portable family identity");
            SaveFontAll("Malgun Gothic");
            Require(PreviewFonts.All(f => _settings.GetCurrentUIFontName(f) == regular), "UI selections persist canonical family");
            var font = CurrentFonts[GameFont.Medium];
            foreach (var size in PreviewFonts)
                Require(ReferenceEquals(CurrentFonts[size], font)
                    == (FontSizeKey(Text.fontStyles[(int)size].fontSize) == FontSizeKey(Text.fontStyles[(int)GameFont.Medium].fontSize)),
                    "system fonts reuse matching engine size variants: " + size);
            SaveWorldFont(regular);
            Require(ReferenceEquals(GetWorldFontAsset().sourceFontFile, font)
                == (FontSizeKey(0) == FontSizeKey(Text.fontStyles[(int)GameFont.Medium].fontSize)),
                "system UI/world selections reuse matching engine size variants");
            const string missing = "CustomFonts verification missing family";
            SaveFontAll(missing); SaveWorldFont(missing);
            Require(PreviewFonts.All(f => _settings.GetCurrentUIFontName(f) == missing)
                && _settings.CurrentWorldFontName == missing, "missing family selections survive temporary unavailability");
            Require(PreviewFonts.All(f => ReferenceEquals(CurrentFonts[f], DefaultFonts[f]))
                && ReferenceEquals(GetWorldFontAsset(), DefaultTMPFontAsset), "missing family renders with default");
            SaveFontAll(regular); SaveWorldFont(regular);
        }
    }
}
