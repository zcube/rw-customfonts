using System;
using Verse;

namespace CustomFonts
{
    public partial class FontSettings : ModSettings
    {
        // Setting values
        public const string DefaultFontName = "Default";
        public string CurrentWorldFontName = "Default";
        public float WorldScaleFactor = 1.0f;

        public string CurrentUIFontNameTiny = "Default";
        public string CurrentUIFontNameSmall = "Default";
        public string CurrentUIFontNameMedium = "Default";

        public float ScaleFactorTiny = 1.0f;
        public float ScaleFactorSmall = 1.0f;
        public float ScaleFactorMedium = 1.0f;

        public int VerticalOffsetTiny = 0;
        public int VerticalOffsetSmall = 0;
        public int VerticalOffsetMedium = 0;

        public float FontWidthScaleFactorTiny = 1.0f;
        public float FontWidthScaleFactorSmall = 1.0f;
        public float FontWidthScaleFactorMedium = 1.0f;

        public bool UniformUIfonts = false;
        public bool DebugLogging = false;

        public string GetCurrentUIFontName(GameFont fontIndex)
        {
            switch (fontIndex)
            {
                case GameFont.Tiny:
                    return CurrentUIFontNameTiny;
                case GameFont.Small:
                    return CurrentUIFontNameSmall;
                case GameFont.Medium:
                    return CurrentUIFontNameMedium;
                default:
                    return CurrentUIFontNameMedium;
            }
        }

        public void SetCurrentUIFontName(GameFont fontIndex, string fontName)
        {
            switch (fontIndex)
            {
                case GameFont.Tiny:
                    CurrentUIFontNameTiny = fontName;
                    break;
                case GameFont.Small:
                    CurrentUIFontNameSmall = fontName;
                    break;
                case GameFont.Medium:
                    CurrentUIFontNameMedium = fontName;
                    break;
            }
        }

        public float GetScaleFactor(GameFont fontIndex)
        {
            switch (fontIndex)
            {
                case GameFont.Tiny:
                    return ScaleFactorTiny;
                case GameFont.Small:
                    return ScaleFactorSmall;
                case GameFont.Medium:
                    return ScaleFactorMedium;
                default:
                    return ScaleFactorMedium;
            }
        }

        public void SetScaleFactor(GameFont fontIndex, float value)
        {
            switch (fontIndex)
            {
                case GameFont.Tiny:
                    ScaleFactorTiny = value;
                    break;
                case GameFont.Small:
                    ScaleFactorSmall = value;
                    break;
                case GameFont.Medium:
                    ScaleFactorMedium = value;
                    break;
            }
        }

        public int GetVerticalOffset(GameFont fontIndex)
        {
            switch (fontIndex)
            {
                case GameFont.Tiny:
                    return VerticalOffsetTiny;
                case GameFont.Small:
                    return VerticalOffsetSmall;
                case GameFont.Medium:
                    return VerticalOffsetMedium;
                default:
                    return VerticalOffsetMedium;
            }
        }

        public void SetVerticalOffset(GameFont fontIndex, int value)
        {
            switch (fontIndex)
            {
                case GameFont.Tiny:
                    VerticalOffsetTiny = value;
                    break;
                case GameFont.Small:
                    VerticalOffsetSmall = value;
                    break;
                case GameFont.Medium:
                    VerticalOffsetMedium = value;
                    break;
            }
        }

        public float GetFontWidthScaleFactor(GameFont fontIndex)
        {
            switch (fontIndex)
            {
                case GameFont.Tiny:
                    return FontWidthScaleFactorTiny;
                case GameFont.Small:
                    return FontWidthScaleFactorSmall;
                case GameFont.Medium:
                    return FontWidthScaleFactorMedium;
                default:
                    return FontWidthScaleFactorMedium;
            }
        }

        public void SetFontWidthScaleFactor(GameFont fontIndex, float value)
        {
            switch (fontIndex)
            {
                case GameFont.Tiny:
                    FontWidthScaleFactorTiny = value;
                    break;
                case GameFont.Small:
                    FontWidthScaleFactorSmall = value;
                    break;
                case GameFont.Medium:
                    FontWidthScaleFactorMedium = value;
                    break;
            }
        }


        public override void ExposeData() // Writing settings to the mod file
        {
            UpdateUniformUIfonts();
            Scribe_Values.Look(ref UniformUIfonts, "UniformUIfonts", true);
            Scribe_Values.Look(ref DebugLogging, "DebugLogging", false);

            Scribe_Values.Look(ref CurrentWorldFontName, "CurrentWorldFontName", DefaultFontName);
            Scribe_Values.Look(ref WorldScaleFactor, "WorldScaleFactor", 1.0f);

            Scribe_Values.Look(ref CurrentUIFontNameMedium, "CurrentUIFontName", DefaultFontName);
            Scribe_Values.Look(ref CurrentUIFontNameTiny, "CurrentUITinyFontName", DefaultFontName);
            Scribe_Values.Look(ref CurrentUIFontNameSmall, "CurrentUISmallFontName", DefaultFontName);

            Scribe_Values.Look(ref ScaleFactorMedium, "ScaleFactor", 1.0f);
            Scribe_Values.Look(ref ScaleFactorTiny, "ScaleFactorTiny", 1.0f);
            Scribe_Values.Look(ref ScaleFactorSmall, "ScaleFactorSmall", 1.0f);

            Scribe_Values.Look(ref VerticalOffsetMedium, "VerticalOffset", 0);
            Scribe_Values.Look(ref VerticalOffsetTiny, "VerticalOffsetTiny", 0);
            Scribe_Values.Look(ref VerticalOffsetSmall, "VerticalOffsetSmall", 0);

            Scribe_Values.Look(ref FontWidthScaleFactorMedium, "FontWidthScaleFactor", 1.0f);
            Scribe_Values.Look(ref FontWidthScaleFactorTiny, "FontWidthScaleFactorTiny", 1.0f);
            Scribe_Values.Look(ref FontWidthScaleFactorSmall, "FontWidthScaleFactorSmall", 1.0f);

            ExposeEngineData();
            base.ExposeData();
        }

        partial void ExposeEngineData();

        public void UpdateUniformUIfonts()
        {
            if (UniformUIfonts)
            {
                SetCurrentUIFontName(GameFont.Tiny, CurrentUIFontNameMedium);
                SetCurrentUIFontName(GameFont.Small, CurrentUIFontNameMedium);
                SetScaleFactor(GameFont.Tiny, ScaleFactorMedium);
                SetScaleFactor(GameFont.Small, ScaleFactorMedium);
                SetVerticalOffset(GameFont.Tiny, VerticalOffsetMedium);
                SetVerticalOffset(GameFont.Small, VerticalOffsetMedium);
                SetFontWidthScaleFactor(GameFont.Tiny, FontWidthScaleFactorMedium);
                SetFontWidthScaleFactor(GameFont.Small, FontWidthScaleFactorMedium);
                SetFallbackFonts(FontTarget.Tiny, GetFallbackFonts(FontTarget.Medium));
                SetFallbackFonts(FontTarget.Small, GetFallbackFonts(FontTarget.Medium));
            }
        }

        // Uniform mode makes Medium the only editable size, so Tiny and Small are only ever
        // copies of it. Any path that writes one size without mirroring it leaves the UI
        // drawing stale sizes with no visible way to fix them, so the settings window checks
        // this every frame and repairs the copies instead of waiting for the checkbox to be
        // toggled off and on again.
        // Identifies the Medium values the copies are made from. The settings window uses it
        // to notice a repair that did not stick (a font that cannot be bound falls back to
        // Default) and stop retrying it every frame instead of rebuilding fonts forever.
        public string UniformSignature()
        {
            return $"{CurrentUIFontNameMedium}|{ScaleFactorMedium:F2}|{VerticalOffsetMedium}|{FontWidthScaleFactorMedium:F2}|{FontFallbackLists.NamesKey(GetFallbackFonts(FontTarget.Medium))}";
        }

        public bool UniformOutOfSync()
        {
            if (!UniformUIfonts) return false;
            foreach (GameFont value in Enum.GetValues(typeof(GameFont)))
            {
                if (GetCurrentUIFontName(value) != CurrentUIFontNameMedium) return true;
                if (Math.Abs(GetScaleFactor(value) - ScaleFactorMedium) > 0.001f) return true;
                if (GetVerticalOffset(value) != VerticalOffsetMedium) return true;
                if (Math.Abs(GetFontWidthScaleFactor(value) - FontWidthScaleFactorMedium) > 0.001f) return true;
                if (!System.Linq.Enumerable.SequenceEqual(GetFallbackFonts((FontTarget)value), GetFallbackFonts(FontTarget.Medium))) return true;
            }
            return false;
        }
    }
}
