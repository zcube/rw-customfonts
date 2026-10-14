using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace CustomFonts
{
    public partial class FontSettings
    {
        public List<string> FontSourcePaths = new List<string>();
        public bool IncludeSystemFonts = true;
        public bool RecursiveFontFolders = true;

        partial void ExposeEngineData()
        {
            Scribe_Collections.Look(ref FontSourcePaths, "WaylithFontSourcePaths", LookMode.Value);
            Scribe_Values.Look(ref IncludeSystemFonts, "WaylithIncludeSystemFonts", true);
            Scribe_Values.Look(ref RecursiveFontFolders, "WaylithRecursiveFontFolders", true);
            if (FontSourcePaths == null) FontSourcePaths = new List<string>();
        }
    }

}
