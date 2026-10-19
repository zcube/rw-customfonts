using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace CustomFonts
{
    public partial class FontSettings
    {
        public List<string> FontSourcePaths = new List<string>();
        public bool IncludeSystemFonts = true;
        public bool RecursiveFontFolders = true;
        public bool ShowFontNamePreviews;
        public int FontNamePreviewSize = FontPreviewImage.DefaultPixelSize;
        public List<string> FavoriteFontNames = new List<string>();
        public List<string> FallbackFontNames = new List<string>();
        public List<string> FallbackFontsTiny;
        public List<string> FallbackFontsSmall;
        public List<string> FallbackFontsMedium;
        public List<string> FallbackFontsWorld;

        internal List<string> GetFallbackFonts(FontTarget target)
        {
            switch (target)
            {
                case FontTarget.Tiny: return FallbackFontsTiny ?? (FallbackFontsTiny = new List<string>());
                case FontTarget.Small: return FallbackFontsSmall ?? (FallbackFontsSmall = new List<string>());
                case FontTarget.World: return FallbackFontsWorld ?? (FallbackFontsWorld = new List<string>());
                default: return FallbackFontsMedium ?? (FallbackFontsMedium = new List<string>());
            }
        }
        internal void SetFallbackFonts(FontTarget target, IEnumerable<string> names)
        {
            var list = FontFallbackLists.Normalize(names);
            switch (target)
            {
                case FontTarget.Tiny: FallbackFontsTiny = list; break;
                case FontTarget.Small: FallbackFontsSmall = list; break;
                case FontTarget.World: FallbackFontsWorld = list; break;
                default: FallbackFontsMedium = list; break;
            }
        }

        partial void ExposeEngineData()
        {
            // Keep the existing keys so profiles retain their 1.7 sources when switching
            // between Unity and Waylith. Both engines now read/write these same settings.
            Scribe_Collections.Look(ref FontSourcePaths, "WaylithFontSourcePaths", LookMode.Value);
            Scribe_Values.Look(ref IncludeSystemFonts, "WaylithIncludeSystemFonts", true);
            Scribe_Values.Look(ref RecursiveFontFolders, "WaylithRecursiveFontFolders", true);
            Scribe_Values.Look(ref ShowFontNamePreviews, "ShowFontNamePreviews", false);
            Scribe_Values.Look(ref FontNamePreviewSize, "FontNamePreviewSize", FontPreviewImage.DefaultPixelSize);
            FontNamePreviewSize = FontPreviewImage.ClampSize(FontNamePreviewSize);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
                Scribe_Collections.Look(ref FallbackFontNames, "FallbackFontNames", LookMode.Value);
            Scribe_Collections.Look(ref FavoriteFontNames, "FavoriteFontNames", LookMode.Value);
            FavoriteFontNames = FontFavoriteLists.Normalize(FavoriteFontNames);
            Scribe_Collections.Look(ref FallbackFontsTiny, "FallbackFontsTiny", LookMode.Value);
            Scribe_Collections.Look(ref FallbackFontsSmall, "FallbackFontsSmall", LookMode.Value);
            Scribe_Collections.Look(ref FallbackFontsMedium, "FallbackFontsMedium", LookMode.Value);
            Scribe_Collections.Look(ref FallbackFontsWorld, "FallbackFontsWorld", LookMode.Value);
            if (FontSourcePaths == null) FontSourcePaths = new List<string>();
            if (FallbackFontNames == null) FallbackFontNames = new List<string>();
            FallbackFontsTiny = FontFallbackLists.Migrate(FallbackFontsTiny, FallbackFontNames);
            FallbackFontsSmall = FontFallbackLists.Migrate(FallbackFontsSmall, FallbackFontNames);
            FallbackFontsMedium = FontFallbackLists.Migrate(FallbackFontsMedium, FallbackFontNames);
            FallbackFontsWorld = FontFallbackLists.Migrate(FallbackFontsWorld, FallbackFontNames);
        }
    }

}
