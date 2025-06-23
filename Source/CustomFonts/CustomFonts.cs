using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Verse;
using UnityEngine;
using HarmonyLib;
using RimWorld.Planet;
using RimWorld;
using SettingsHelper;
using TMPro;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;

namespace CustomFonts
{
    public class FontSettings : ModSettings
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

            base.ExposeData();
        }

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
            }
        }
    }

    [StaticConstructorOnStartup]
    static class StartupFontPatcher
    {
        static StartupFontPatcher()
        {
        }
    }


    public class CustomFonts : Mod
    {
        public readonly FontSettings _settings;
        private List<string> _fontNames = new List<string>();
        private bool _hasInstalledFontNames;
        private bool _hasBundledFonts;
        private bool _hasOSFontAssets;
        private bool _hasUnityPlayerSo;
        private bool _hasRunHostFonts;
        public bool ForceLegacyText;
        public readonly Dictionary<string, Font> BundledFonts = new Dictionary<string, Font>();
        public readonly Dictionary<GameFont, Font> DefaultFonts = new Dictionary<GameFont, Font>();
        public readonly Dictionary<GameFont, Font> CurrentFonts = new Dictionary<GameFont, Font>();
        public readonly Dictionary<GameFont, GUIStyle> DefaultFontStyle = new Dictionary<GameFont, GUIStyle>();
        public readonly Dictionary<string, string> OSFontPaths = new Dictionary<string, string>();
        public TMP_FontAsset DefaultTMPFontAsset;
        private Vector2 scrollPosition = Vector2.zero;
        public Harmony MyHarmony { get; private set; }
        private ModContentPack _content;
        private string searchText = "";
        private List<TabRecord> tabs = new List<TabRecord>();
        private List<TabRecord> uniformTabs = new List<TabRecord>();
        public int toolbarInt = 0;

        public CustomFonts(ModContentPack content) :
            base(content) // A mandatory constructor which resolves the reference to the mod settings.
        {
            _settings = GetSettings<FontSettings>();

            _settings.UpdateUniformUIfonts();

            foreach (GameFont value in Enum.GetValues(typeof(GameFont)))
            {
                if (float.IsNaN(_settings.GetScaleFactor(value)))
                {
                    _settings.SetScaleFactor(value, 1.0f);
                }

                if (float.IsNaN(_settings.GetFontWidthScaleFactor(value)))
                {
                    _settings.SetFontWidthScaleFactor(value, 1.0f);
                }

                if (float.IsNaN(_settings.GetVerticalOffset(value)))
                {
                    _settings.SetVerticalOffset(value, 0);
                }

                DefaultFonts[value] = Text.fontStyles[(int)value].font;
                DefaultFontStyle[value] = Text.fontStyles[(int)value];
            }

            ForceLegacyText = _settings.CurrentWorldFontName == FontSettings.DefaultFontName;

            // var gameFontEnumLength = Enum.GetValues(typeof(GameFont)).Length;

            _content = content;

            MyHarmony = new Harmony("zcubekr.customfonts");
            MyHarmony.PatchAll();

            tabs.Add(new TabRecord("CustomFonts.FontTiny", delegate
            {
                toolbarInt = 0;
            }, () => toolbarInt == 0));
            tabs.Add(new TabRecord("CustomFonts.FontSmall", delegate
            {
                toolbarInt = 1;
            }, () => toolbarInt == 1));
            tabs.Add(new TabRecord("CustomFonts.FontMedium", delegate
            {
                toolbarInt = 2;
            }, () => toolbarInt == 2));
            tabs.Add(new TabRecord("CustomFonts.FontWorld", delegate
            {
                toolbarInt = 3;
            }, () => toolbarInt == 3));
            tabs.Add(new TabRecord("CustomFonts.Etc", delegate
            {
                toolbarInt = 4;
            }, () => toolbarInt == 4));
            uniformTabs.Add(tabs[2]);
            uniformTabs.Add(tabs[3]);
            uniformTabs.Add(tabs[4]);
        }

        public override void DoSettingsWindowContents(Rect inRect) // The GUI part to edit the mod settings.
        {
            SetupOSInstalledFontNames();
            SetupOSFontPaths();
            SetupBundledFonts();

            tabs[0].label = "CustomFonts.FontTiny".Translate(_settings.GetCurrentUIFontName(GameFont.Tiny));
            tabs[1].label = "CustomFonts.FontSmall".Translate(_settings.GetCurrentUIFontName(GameFont.Small));
            tabs[2].label = "CustomFonts.FontMedium".Translate(_settings.GetCurrentUIFontName(GameFont.Medium));
            tabs[3].label = "CustomFonts.FontWorld".Translate(_settings.CurrentWorldFontName);
            tabs[4].label = "CustomFonts.Etc".Translate(_settings.CurrentWorldFontName);

            var listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);
            if (listingStandard.ButtonTextLabeled("CustomFonts.ResetToDefault".Translate(), "CustomFonts.Apply".Translate()))
            {
                foreach (GameFont value in Enum.GetValues(typeof(GameFont)))
                {
                    _settings.SetScaleFactor(value, 1.0f);
                    _settings.SetVerticalOffset(value, 0);
                    _settings.SetFontWidthScaleFactor(value, 1.0f);
                    SaveFont(value, FontSettings.DefaultFontName, true);
                }
                _settings.UniformUIfonts = true;
                _settings.WorldScaleFactor = 1.0f;
                SaveWorldFont(FontSettings.DefaultFontName);
            }
            listingStandard.CheckboxLabeled("CustomFonts.UniformUIfonts".Translate(), ref _settings.UniformUIfonts, "CustomFonts.UniformUIfonts".Translate());

            Rect toolbarRect = listingStandard.GetRect(TabDrawer.TabHeight);
            toolbarRect.y += TabDrawer.TabHeight;
            toolbarRect.height = 0f;

            if (_settings.UniformUIfonts)
            {
                var selectedTab = TabDrawer.DrawTabs(toolbarRect, uniformTabs);
            }
            else
            {
                var selectedTab = TabDrawer.DrawTabs(toolbarRect, tabs);
            }

            switch (toolbarInt)
            {
                case 0:
                    listingStandard.Label("CustomFonts.CurrentFont".Translate(_settings.GetCurrentUIFontName(GameFont.Tiny)));
                    break;
                case 1:
                    listingStandard.Label("CustomFonts.CurrentFont".Translate(_settings.GetCurrentUIFontName(GameFont.Small)));
                    break;
                case 2:
                    listingStandard.Label("CustomFonts.CurrentFont".Translate(_settings.GetCurrentUIFontName(GameFont.Medium)));
                    break;
                case 3:
                    listingStandard.Label("CustomFonts.CurrentFont".Translate(_settings.CurrentWorldFontName));
                    break;
                case 4:
                    listingStandard.Label("CustomFonts.Etc".Translate(_settings.CurrentWorldFontName));
                    break;
            }

            string[] specimen =
            {
                "CustomFonts.Specimen".Translate(),
            };

            Rect fontViewRect = listingStandard.GetRect(100);
            {
                var fontViewListingStandard = new Listing_Standard();
                fontViewListingStandard.Begin(fontViewRect);

                var backup = Text.Font;
                Text.Font = GameFont.Tiny;
                fontViewListingStandard.Label(String.Join("\n", specimen));
                Text.Font = GameFont.Small;
                fontViewListingStandard.Label(String.Join("\n", specimen));
                Text.Font = GameFont.Medium;
                fontViewListingStandard.Label(String.Join("\n", specimen));
                // TODO: world font specimen
                Text.Font = backup;
                fontViewListingStandard.End();
            }

            listingStandard.GapLine();

            Rect remainRect = listingStandard.GetRect(0);
            var scrollHeight = inRect.height - remainRect.y - 10f;
            var listRect = new Rect();
            var listScrollRect = new Rect();
            var propertyRect = new Rect();
            var fullRect = new Rect();
            var leftListingStandard = new Listing_Standard();
            switch (toolbarInt)
            {
                case 0:
                    listingStandard.LineRectSpilter(out listRect, out propertyRect, height: scrollHeight);
                    listRect = Padding(listRect, 10);
                    propertyRect = Padding(propertyRect, 10);
                    leftListingStandard.Begin(listRect);
                    searchText = leftListingStandard.TextEntryLabeled("CustomFonts.Search".Translate(), searchText).Trim();
                    remainRect = leftListingStandard.GetRect(0);
                    listScrollRect = new Rect(listRect.x, remainRect.y, listRect.width, listRect.height - remainRect.y);
                    DrawFontSettings(GameFont.Tiny, listScrollRect);
                    leftListingStandard.End();
                    DrawPropertySettings(toolbarInt, propertyRect);
                    break;
                case 1:
                    listingStandard.LineRectSpilter(out listRect, out propertyRect, height: scrollHeight);
                    listRect = Padding(listRect, 10);
                    propertyRect = Padding(propertyRect, 10);
                    leftListingStandard.Begin(listRect);
                    searchText = leftListingStandard.TextEntryLabeled("CustomFonts.Search".Translate(), searchText).Trim();
                    remainRect = leftListingStandard.GetRect(0);
                    listScrollRect = new Rect(listRect.x, remainRect.y, listRect.width, listRect.height - remainRect.y);
                    DrawFontSettings(GameFont.Small, listScrollRect);
                    leftListingStandard.End();
                    DrawPropertySettings(toolbarInt, propertyRect);
                    break;
                case 2:
                    listingStandard.LineRectSpilter(out listRect, out propertyRect, height: scrollHeight);
                    listRect = Padding(listRect, 10);
                    propertyRect = Padding(propertyRect, 10);
                    leftListingStandard.Begin(listRect);
                    searchText = leftListingStandard.TextEntryLabeled("CustomFonts.Search".Translate(), searchText).Trim();
                    remainRect = leftListingStandard.GetRect(0);
                    listScrollRect = new Rect(listRect.x, remainRect.y, listRect.width, listRect.height - remainRect.y);
                    DrawFontSettings(GameFont.Medium, listScrollRect);
                    leftListingStandard.End();
                    DrawPropertySettings(toolbarInt, propertyRect);
                    break;
                case 3:
                    listingStandard.LineRectSpilter(out listRect, out propertyRect, height: scrollHeight);
                    listRect = Padding(listRect, 10);
                    propertyRect = Padding(propertyRect, 10);
                    leftListingStandard.Begin(listRect);
                    searchText = leftListingStandard.TextEntryLabeled("CustomFonts.Search".Translate(), searchText).Trim();
                    remainRect = leftListingStandard.GetRect(0);
                    listScrollRect = new Rect(listRect.x, remainRect.y, listRect.width, listRect.height - remainRect.y);
                    DrawWorldFontSettings(listScrollRect);
                    leftListingStandard.End();
                    DrawPropertySettings(toolbarInt, propertyRect);
                    break;
                case 4:
                    fullRect = listingStandard.GetRect(scrollHeight);
                    DrawLinuxSettings(fullRect);
                    break;
            }


            listingStandard.End();

            base.DoSettingsWindowContents(inRect);
        }

        public Rect Padding(Rect rect, float padding)
        {
            return new Rect(rect.x + padding, rect.y + padding, rect.width - padding * 2, rect.height - padding * 2);
        }

        public void DrawFontSettings(GameFont fontIndex, Rect inRect)
        {
            var bundledFontsCount = 0;
            var fontNameCount = 0;
            foreach (var name in BundledFonts.Keys)
            {
                if (name.ToLower().Contains(searchText.ToLower()))
                {
                    bundledFontsCount++;
                }
            }
            foreach (var name in _fontNames)
            {
                if (name.ToLower().Contains(searchText.ToLower()))
                {
                    fontNameCount++;
                }
            }
            var fontListScrollOuter = new Rect(inRect.x, inRect.y,
                inRect.width - 10f, inRect.height);
            var fontListScrollInner = new Rect(fontListScrollOuter.x, fontListScrollOuter.y,
                fontListScrollOuter.width - 24f,
                23.6f * (3 + bundledFontsCount + fontNameCount));
            Widgets.BeginScrollView(fontListScrollOuter, ref scrollPosition, fontListScrollInner);
            var listingStandard = fontListScrollInner.BeginListingStandard();

            if (listingStandard.RadioButton(FontSettings.DefaultFontName, _settings.GetCurrentUIFontName(fontIndex) == FontSettings.DefaultFontName))
            {
                if (_settings.UniformUIfonts)
                    SaveFontAll(fontIndex, FontSettings.DefaultFontName);
                else
                    SaveFont(fontIndex, FontSettings.DefaultFontName);
            }
            listingStandard.GapLine();
            foreach (var name in BundledFonts.Keys.OrderBy(x => x))
            {
                if (name.ToLower().Contains(searchText.ToLower()))
                {
                    if (listingStandard.RadioButton(name, _settings.GetCurrentUIFontName(fontIndex) == name))
                    {
                        if (_settings.UniformUIfonts)
                            SaveFontAll(fontIndex, name);
                        else
                            SaveFont(fontIndex, name);
                    }
                }
            }
            listingStandard.GapLine();
            foreach (var name in _fontNames)
            {
                if (name.ToLower().Contains(searchText.ToLower()))
                {
                    if (listingStandard.RadioButton(name, _settings.GetCurrentUIFontName(fontIndex) == name))
                    {
                        if (_settings.UniformUIfonts)
                            SaveFontAll(fontIndex, name);
                        else
                            SaveFont(fontIndex, name);
                    }
                }
            }

            listingStandard.End();
            Widgets.EndScrollView();
        }

        public void DrawWorldFontSettings(Rect inRect)
        {
            var bundledFontsCount = 0;
            var osFontPathsCount = 0;
            foreach (var name in BundledFonts.Keys)
            {
                if (name.ToLower().Contains(searchText.ToLower()))
                {
                    bundledFontsCount++;
                }
            }
            foreach (var name in OSFontPaths.Keys)
            {
                if (name.ToLower().Contains(searchText.ToLower()))
                {
                    osFontPathsCount++;
                }
            }
            var fontListScrollOuter = new Rect(inRect.x, inRect.y,
                inRect.width - 10f, inRect.height);
            var fontListScrollInner = new Rect(fontListScrollOuter.x, fontListScrollOuter.y,
                fontListScrollOuter.width - 24f,
                23.6f * (3 + BundledFonts.Count + OSFontPaths.Count));
            Widgets.BeginScrollView(fontListScrollOuter, ref scrollPosition, fontListScrollInner);
            var listingStandard = fontListScrollInner.BeginListingStandard();

            if (listingStandard.RadioButton(FontSettings.DefaultFontName, _settings.CurrentWorldFontName == FontSettings.DefaultFontName))
                SaveWorldFont(FontSettings.DefaultFontName);
            listingStandard.GapLine();
            foreach (var name in BundledFonts.Keys.OrderBy(x => x))
            {
                if (name.ToLower().Contains(searchText.ToLower()))
                {
                    if (listingStandard.RadioButton(name, _settings.CurrentWorldFontName == name))
                        SaveWorldFont(name);
                }
            }
            listingStandard.GapLine();
            foreach (var name in OSFontPaths.Keys.OrderBy(x => x))
            {
                if (name.ToLower().Contains(searchText.ToLower()))
                {
                    if (listingStandard.RadioButton(name, _settings.CurrentWorldFontName == name))
                        SaveWorldFont(name);
                }
            }

            listingStandard.End();
            Widgets.EndScrollView();
        }

        public void DrawPropertySettings(int toolbarInt, Rect inRect)
        {
            var listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);

            if (toolbarInt < 3)
            {
                GameFont fontIndex = GameFont.Tiny;
                switch (toolbarInt)
                {
                    case 0:
                        fontIndex = GameFont.Tiny;
                        break;
                    case 1:
                        fontIndex = GameFont.Small;
                        break;
                    case 2:
                        fontIndex = GameFont.Medium;
                        break;
                }
                var offsetValue = (float)_settings.GetVerticalOffset(fontIndex);
                listingStandard.AddLabeledSlider(
                    "CustomFonts.VerticalOffset".Translate(_settings.GetVerticalOffset(fontIndex) > 0 ? "+" : "", _settings.GetVerticalOffset(fontIndex)),
                    ref offsetValue, -20.0f, 20.0f);
                var newOffset = (int)Math.Round(offsetValue);
                if (newOffset != _settings.GetVerticalOffset(fontIndex))
                {
                    _settings.SetVerticalOffset(fontIndex, newOffset);
                    RecalcCustomLineHeights();
                }

                var scaleValue = _settings.GetScaleFactor(fontIndex);
                listingStandard.AddLabeledSlider(
                    "CustomFonts.FontScalingFactor".Translate(_settings.GetScaleFactor(fontIndex).ToString("F1")),
                     ref scaleValue, 0.5f, 2.0f);
                var fontSizeScale = (float)Math.Round(scaleValue, 1);
                if (Math.Abs(_settings.GetScaleFactor(fontIndex) - fontSizeScale) > 0.01)
                {
                    _settings.SetScaleFactor(fontIndex, fontSizeScale);
                    UpdateFont();
                }

                var widthScaleValue = _settings.GetFontWidthScaleFactor(fontIndex);
                listingStandard.AddLabeledSlider(
                    "CustomFonts.FontWidthScalingFactor".Translate(_settings.GetFontWidthScaleFactor(fontIndex).ToString("F1")),
                  ref widthScaleValue, 0.5f, 2.0f);
                var widthScale = (float)Math.Round(widthScaleValue, 1);
                if (Math.Abs(_settings.GetFontWidthScaleFactor(fontIndex) - widthScale) > 0.01)
                {
                    _settings.SetFontWidthScaleFactor(fontIndex, widthScale);
                    UpdateFont();
                }
            }
            else if (toolbarInt == 3)
            {
                var scaleValue = _settings.WorldScaleFactor;
                listingStandard.AddLabeledSlider(
                    "CustomFonts.FontScalingFactor".Translate(_settings.WorldScaleFactor.ToString("F1")),
                     ref scaleValue, 0.5f, 2.0f);
                var fontSizeScale = (float)Math.Round(scaleValue, 1);
                if (Math.Abs(_settings.WorldScaleFactor - fontSizeScale) > 0.01)
                {
                    _settings.WorldScaleFactor = fontSizeScale;
                }
            }

            listingStandard.End();
        }

        public void DrawLinuxSettings(Rect inRect)
        {
            var listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);

            listingStandard.Label("CustomFonts.SteamDeckPatch".Translate());
            GUI.enabled = false;
            var __hasUnityPlayerSo = _hasUnityPlayerSo;
            listingStandard.CheckboxLabeled("CustomFonts.HasUnityPlayerSo".Translate(), ref __hasUnityPlayerSo);
            var __hasRunHostFonts = _hasRunHostFonts;
            listingStandard.CheckboxLabeled("CustomFonts.HasRunHostFonts".Translate(), ref __hasRunHostFonts);
            GUI.enabled = _hasUnityPlayerSo && _hasRunHostFonts;
            if (listingStandard.ButtonTextLabeled("CustomFonts.ToRunHostFonts".Translate(), "CustomFonts.Patch".Translate()))
            {
                string UnityPlayerSoPath = Path.GetFullPath("UnityPlayer.so");
                ReplaceStringInBinaryFile(UnityPlayerSoPath, "/usr/share/fonts", "/run/host/fonts/");
            }
            GUI.enabled = _hasUnityPlayerSo;
            if (listingStandard.ButtonTextLabeled("CustomFonts.ToUsrShareFonts".Translate(), "CustomFonts.Patch".Translate()))
            {
                string UnityPlayerSoPath = Path.GetFullPath("UnityPlayer.so");
                ReplaceStringInBinaryFile(UnityPlayerSoPath, "/run/host/fonts/", "/usr/share/fonts");
            }
            GUI.enabled = true;
            listingStandard.End();
        }

        public override string SettingsCategory() => "Custom Fonts";// "CustomFonts.Category".Translate();

        public void SetupOSInstalledFontNames()
        {
            if (_hasInstalledFontNames) return;
            _hasInstalledFontNames = true;
            _fontNames = Font.GetOSInstalledFontNames().ToList();
            _fontNames.Sort();
        }

        public void SetupOSFontPaths()
        {
            try
            {
                if (_hasOSFontAssets) return;
                _hasOSFontAssets = true;
                foreach (var path in Font.GetPathsToOSFonts())
                {
                    var asset = TMP_FontAsset.CreateFontAsset(new Font(path));
                    if (asset == null)
                    {
                        Log.Warning($"[Custom Fonts] Unable to create font asset for {path}, skipping.");
                        continue;
                    }
                    var fontName = $"{asset.faceInfo.familyName} ({asset.faceInfo.styleName})";
                    if (!OSFontPaths.ContainsKey(fontName))
                    {
                        OSFontPaths.Add(fontName, path);
                    }
                }
                string UnityPlayerSoPath = Path.GetFullPath("UnityPlayer.so");
                _hasUnityPlayerSo = File.Exists(UnityPlayerSoPath);
                _hasRunHostFonts = Directory.Exists("/run/host/fonts");
            }
            catch (Exception ex)
            {
                Log.Message($"[Custom Fonts] error: {ex.Message}");
            }
        }

        public void SetupBundledFonts()
        {
            if (_hasBundledFonts) return;
            _hasBundledFonts = true;
            var fontAssetPath = Path.Combine(_content.RootDir, "rimfonts");
            var cab = AssetBundle.LoadFromFile(fontAssetPath);
            if (cab == null)
            {
                Log.Message("[Custom Fonts] Unable to load bundled fonts.");
                return;
            }
#if DEBUG
            Log.Message(
                $"[Custom Fonts] Loading font asset at {fontAssetPath}:\n{string.Join("\n", cab.GetAllAssetNames())}");
#endif

            foreach (var font in cab.LoadAllAssets<Font>())
            {
                var asset = TMP_FontAsset.CreateFontAsset(font);
                var fontName = $"(Bundled) {asset.faceInfo.familyName} ({asset.faceInfo.styleName})";
                if (!BundledFonts.ContainsKey(fontName))
                {
                    BundledFonts.Add(fontName, font);
                }
            }
        }

        private void SaveFont(GameFont fontIndex, string fontName, bool forceUpdate = false)
        {
            if (fontName == _settings.GetCurrentUIFontName(fontIndex) && !forceUpdate) return;
            _settings.SetCurrentUIFontName(fontIndex, fontName);
            UpdateFont(fontIndex);
        }

        private void SaveFontAll(GameFont fontIndex, string fontName, bool forceUpdate = false)
        {
            foreach (GameFont value in Enum.GetValues(typeof(GameFont)))
            {
                SaveFont(value, fontName, forceUpdate);
            }
        }

        private void SaveWorldFont(string fontName)
        {
            if (fontName == _settings.CurrentWorldFontName) return;
            _settings.CurrentWorldFontName = fontName;
            ForceLegacyText = fontName == FontSettings.DefaultFontName;
            AccessTools.StaticFieldRefAccess<bool>(typeof(WorldFeatures), "ForceLegacyText") = ForceLegacyText;
        }

        public void UpdateFont()
        {
            SetupBundledFonts();
            foreach (GameFont value in Enum.GetValues(typeof(GameFont)))
            {
                UpdateFont(value);
            }
        }

        private void UpdateFont(GameFont fontIndex)
        {
            Log.Message($"[Custom Fonts] Updating font for {fontIndex} with name {_settings.GetCurrentUIFontName(fontIndex)}");
            // if (_settings.CurrentFontName == _settings.PreviousFontName && !forceUpdate) return;

            var isBundled = BundledFonts.ContainsKey(_settings.GetCurrentUIFontName(fontIndex));
            Font font = null;

            var fontSize = (int)Math.Round(DefaultFonts[fontIndex].fontSize * _settings.GetScaleFactor(fontIndex));

            try
            {
                if (isBundled)
                {
                    font = BundledFonts[_settings.GetCurrentUIFontName(fontIndex)];
                    Log.Message($"[Custom Fonts] Using bundled font {_settings.GetCurrentUIFontName(fontIndex)}");
                }
                else
                {
                    font = _settings.GetCurrentUIFontName(fontIndex) != FontSettings.DefaultFontName
                        ? Font.CreateDynamicFontFromOSFont(_settings.GetCurrentUIFontName(fontIndex), fontSize)
                        : DefaultFonts[fontIndex];
                    Log.Message($"[Custom Fonts] Using OS font {_settings.GetCurrentUIFontName(fontIndex)}");
                }
            }
            catch (Exception ex)
            {
                Log.Message($"[Custom Fonts] error: {ex.Message}");
            }

            if (font == null)
            {
                Log.Message($"[Custom Fonts] Font {_settings.GetCurrentUIFontName(fontIndex)} not found, using default font");
                _settings.SetCurrentUIFontName(fontIndex, FontSettings.DefaultFontName);
                font = DefaultFonts[fontIndex];
            }

#if DEBUG
            Log.Message($"[Custom Fonts] Updating font to {string.Join(", ", font.fontNames)}");
#endif

            CurrentFonts[fontIndex] = font;
            Text.fontStyles[(int)fontIndex].font = font;
            Text.fontStyles[(int)fontIndex].fontSize = fontSize;
            Text.textFieldStyles[(int)fontIndex].font = font;
            Text.textFieldStyles[(int)fontIndex].fontSize = fontSize;
            Text.textAreaStyles[(int)fontIndex].font = font;
            Text.textAreaStyles[(int)fontIndex].fontSize = fontSize;
            Text.textAreaReadOnlyStyles[(int)fontIndex].font = font;
            Text.textAreaReadOnlyStyles[(int)fontIndex].fontSize = fontSize;
            RecalcCustomLineHeights(fontIndex);
        }

        public void RecalcCustomLineHeights()
        {
            foreach (GameFont value in Enum.GetValues(typeof(GameFont)))
            {
                RecalcCustomLineHeights(value);
            }
        }

        public void RecalcCustomLineHeights(GameFont fontIndex)
        {
            // var isDefault = _settings.CurrentUIFontName(fontIndex) == FontSettings.DefaultFontName;
            var offsetVector = new Vector2(0f, _settings.GetVerticalOffset(fontIndex));
            // Text.fontStyles[(int)fontIndex].clipping = isDefault ? TextClipping.Clip : TextClipping.Overflow;
            Text.fontStyles[(int)fontIndex].contentOffset = offsetVector;
            // Text.textFieldStyles[(int)fontIndex].clipping = isDefault ? TextClipping.Clip : TextClipping.Overflow;
            Text.textFieldStyles[(int)fontIndex].contentOffset = offsetVector;
            // Text.textAreaStyles[(int)fontIndex].clipping = TextClipping.Clip;
            Text.textAreaStyles[(int)fontIndex].contentOffset = offsetVector;
            // Text.textAreaReadOnlyStyles[(int)fontIndex].clipping = TextClipping.Clip;
            Text.textAreaReadOnlyStyles[(int)fontIndex].contentOffset = offsetVector;
        }

        public int IndexOf(byte[] array, byte[] pattern)
        {
            if (pattern.Length > array.Length)
                return -1;

            for (int i = 0; i <= array.Length - pattern.Length; i++)
            {
                bool found = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (array[i + j] != pattern[j])
                    {
                        found = false;
                        break;
                    }
                }
                if (found)
                    return i;
            }
            return -1;
        }

        public void ReplaceStringInBinaryFile(string filePath, string searchString, string replaceString)
        {
            try
            {
                byte[] searchStringBytes = Encoding.UTF8.GetBytes(searchString);
                byte[] replaceStringBytes = Encoding.UTF8.GetBytes(replaceString);

                using (FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite))
                {
                    byte[] data = new byte[fileStream.Length];
                    fileStream.Read(data, 0, (int)fileStream.Length);
                    int index = IndexOf(data, searchStringBytes);
                    if (index != -1)
                    {
                        Array.Copy(replaceStringBytes, 0, data, index, replaceStringBytes.Length);
                        fileStream.Seek(0, SeekOrigin.Begin);
                        fileStream.Write(data, 0, data.Length);
                        Log.Message($"[Custom Fonts] Updating UnityPlayer.so fonts path from {searchString} to {replaceString}");
                    }
                    else
                    {
                        Log.Message($"[Custom Fonts] {searchString} not found");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Message($"[Custom Fonts] error: {ex.Message}");
            }
        }
    }

    internal static class HarmoneyPatchers
    {
        private static bool _patcherInitialized;

        [HarmonyPatch(typeof(Text), nameof(Text.StartOfOnGUI))]
        class StartOfOnGUIPatcher
        {
            [HarmonyPostfix]
            public static void Postfix()
            {
                if (_patcherInitialized) return;

                _patcherInitialized = true;
                var mod = LoadedModManager.GetMod<CustomFonts>();
                mod.SetupOSInstalledFontNames();
                mod.SetupOSFontPaths();
                mod.SetupBundledFonts();
                mod.DefaultTMPFontAsset = WorldFeatureTextMesh_TextMeshPro.WorldTextPrefab.GetComponent<TextMeshPro>().font;
                mod.UpdateFont();
                AccessTools.StaticFieldRefAccess<bool>(typeof(WorldFeatures), "ForceLegacyText") = mod.ForceLegacyText;
#if DEBUG
                Log.Message("[Custom Fonts] Font patcher initialised");
#endif
            }
        }

        [HarmonyPatch(typeof(GenScene), nameof(GenScene.GoToMainMenu))]
        class GoToMainMenuPatcher
        {
            [HarmonyPostfix]
            public static void Postfix()
            {
                var mod = LoadedModManager.GetMod<CustomFonts>();
                mod.UpdateFont();
            }
        }

        [HarmonyPatch(typeof(WorldFeatures), "HasCharacter")]
        private class WorldMapHasCharacterPatcher
        {
            [HarmonyPrefix]
            public static bool Prefix(ref bool __result)
            {
                var mod = LoadedModManager.GetMod<CustomFonts>();
                if (mod.ForceLegacyText)
                    return true;
                __result = true;
                return false;
            }
        }

        [HarmonyPatch(typeof(WorldFeatureTextMesh_TextMeshPro), "Init")]
        class WorldMapInitPatcher
        {
            [HarmonyPrefix]
            public static void Prefix()
            {
                TMP_FontAsset fontAsset;
                var mod = LoadedModManager.GetMod<CustomFonts>();
                var fontSettings = mod._settings;
                try
                {
                    if (mod.BundledFonts.ContainsKey(fontSettings.CurrentWorldFontName))
                    {
                        fontAsset = TMP_FontAsset.CreateFontAsset(
                            mod.BundledFonts[fontSettings.CurrentWorldFontName]);
                    }
                    else if (mod.OSFontPaths.ContainsKey(fontSettings.CurrentWorldFontName))
                    {
                        fontAsset = TMP_FontAsset.CreateFontAsset(
                            new Font(mod.OSFontPaths[fontSettings.CurrentWorldFontName]));
                    }
                    else if (fontSettings.CurrentWorldFontName == FontSettings.DefaultFontName)
                    {
                        fontAsset = mod.DefaultTMPFontAsset;
                    }
                    else
                    {
                        fontAsset = mod.DefaultTMPFontAsset;
                    }
                }
                catch (Exception ex)
                {
                    Log.Message($"[Custom Fonts] error: {ex.Message}");
                    fontAsset = null;
                }

                if (fontAsset == null)
                {
                    fontSettings.CurrentWorldFontName = FontSettings.DefaultFontName;
                    fontAsset = mod.DefaultTMPFontAsset;
                }

                var prefab = WorldFeatureTextMesh_TextMeshPro.WorldTextPrefab.GetComponent<TextMeshPro>();
                prefab.font = fontAsset;
                prefab.UpdateFontAsset();
                AccessTools.StaticFieldRefAccess<float>(typeof(WorldFeatureTextMesh_TextMeshPro), "TextScale") =
                    1.75f * fontSettings.WorldScaleFactor;
            }

        }

        [HarmonyPatch(typeof(Dialog_FileList), nameof(Dialog_FileList.DrawDateAndVersion))]
        class DrawDateAndVersionPatcher
        {
            [HarmonyPrefix]
            public static void Prefix(Dialog_FileList __instance, SaveFileInfo sfi, ref Rect rect)
            {
                string str = "0000-00-00 00:00";

                var backup = Text.Font;
                Text.Font = GameFont.Tiny;
                var size = Text.CalcSize(str);

                rect.x -= size.x - rect.width + 2.0f;
                rect.width = size.x + 2.0f;
                Text.Font = backup;
            }
        }
    }
}
