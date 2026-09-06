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


    public partial class CustomFonts : Mod
    {
        public readonly FontSettings _settings;
        private List<string> _fontNames = new List<string>();
        private bool _hasInstalledFontNames;
        private bool _hasBundledFonts;
        private bool _hasOSFontAssets;
        private bool _hasPlatformInfo;
        private bool _hasUnityPlayerSo;
        private bool _hasRunHostFonts;
        public bool ForceLegacyText;
        public readonly Dictionary<string, Font> BundledFonts = new Dictionary<string, Font>();
        public readonly Dictionary<GameFont, Font> DefaultFonts = new Dictionary<GameFont, Font>();
        public readonly Dictionary<GameFont, Font> CurrentFonts = new Dictionary<GameFont, Font>();
        public readonly Dictionary<GameFont, GUIStyle> DefaultFontStyle = new Dictionary<GameFont, GUIStyle>();
        public readonly Dictionary<string, string> OSFontPaths = new Dictionary<string, string>();
        public TMP_FontAsset DefaultTMPFontAsset;

        // Dynamically created fonts are owned by this mod and must be released again once
        // nothing references them, otherwise every font switch / slider tick leaks a Unity
        // font atlas texture until new fonts can no longer be uploaded to the GPU.
        private const string DefaultFontKey = "default";
        private readonly Dictionary<string, Font> _osFontCache = new Dictionary<string, Font>();
        private readonly Dictionary<GameFont, string> _appliedFontKeys = new Dictionary<GameFont, string>();
        private readonly List<PendingDestroy> _pendingDestroy = new List<PendingDestroy>();
        private TMP_FontAsset _worldFontAsset;
        private Font _worldFontSource;
        private string _worldFontAssetKey;
        private static bool _fontEngineInitialized;
        private bool _textMetricsDirty;
        private bool _worldScalePending;
        private float _lastSpecimenHeight;
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
            var uniformBefore = _settings.UniformUIfonts;
            listingStandard.CheckboxLabeled("CustomFonts.UniformUIfonts".Translate(), ref _settings.UniformUIfonts, "CustomFonts.UniformUIfonts".Translate());
            if (_settings.UniformUIfonts && !uniformBefore)
            {
                // Turning it on used to leave whatever Tiny and Small were set to earlier in
                // place, so the UI kept drawing the old sizes while only Medium was editable.
                _settings.UpdateUniformUIfonts();
                UpdateFont();
                DebugLog($"uniform UI fonts enabled, Tiny/Small synced from Medium ({DescribeSettings()})");
            }
            if (_settings.UniformUIfonts && toolbarInt < 2)
            {
                // The uniform tab strip starts at Medium, but toolbarInt still pointed at the
                // hidden Tiny tab, so the sliders silently edited Tiny instead.
                toolbarInt = 2;
            }

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

            var specimenText = "CustomFonts.Specimen".Translate();

            // The preview used to reserve a hard coded 100px. Listing_Standard.Label sizes each
            // line with Text.CalcHeight, so a long translated sentence wraps and a large scaling
            // factor grows every line - both push the preview past 100px and it then draws over
            // whatever follows. Measure the exact height instead.
            var previewHeight = MeasureSpecimenHeight(specimenText, listingStandard.ColumnWidth);

            Rect fontViewRect = listingStandard.GetRect(previewHeight);
            {
                var fontViewListingStandard = new Listing_Standard();
                fontViewListingStandard.Begin(fontViewRect);

                var backup = Text.Font;
                foreach (var previewFont in PreviewFonts)
                {
                    Text.Font = previewFont;
                    fontViewListingStandard.Label(SpecimenLine(previewFont, specimenText));
                }
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

        private static readonly GameFont[] PreviewFonts = { GameFont.Tiny, GameFont.Small, GameFont.Medium };

        // Each preview line is tagged with its GameFont initial and the pixel size actually
        // bound to that style, so a line rendering at the wrong size is obvious on screen
        // instead of having to be guessed from the shapes.
        private static string SpecimenLine(GameFont fontIndex, string specimenText)
        {
            return $"[{fontIndex.ToString().Substring(0, 1)} {Text.fontStyles[(int)fontIndex].fontSize}px] {specimenText}";
        }

        // Mirrors what Listing_Standard.Label does per line: CalcHeight for the wrapped text
        // plus one verticalSpacing gap. Measuring with the same call the layout uses keeps the
        // reserved box exactly as tall as what gets drawn into it.
        private float MeasureSpecimenHeight(string specimenText, float width)
        {
            var lineGap = new Listing_Standard().verticalSpacing;
            var backup = Text.Font;
            var height = 0f;
            var parts = new List<string>();
            foreach (var previewFont in PreviewFonts)
            {
                Text.Font = previewFont;
                var lineHeight = Text.CalcHeight(SpecimenLine(previewFont, specimenText), width);
                height += lineHeight + lineGap;
                parts.Add($"{previewFont}={Text.fontStyles[(int)previewFont].fontSize}px/h{lineHeight:F0}");
            }
            Text.Font = backup;

            if (Math.Abs(height - _lastSpecimenHeight) > 0.5f)
            {
                _lastSpecimenHeight = height;
                DebugLog($"specimen preview at width {width:F0}: {string.Join(", ", parts.ToArray())}, " +
                         $"total {height:F0}px (the old hard coded box was 100px)");
            }
            return height;
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
            SetupOSFontPaths();
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
                    ApplyVerticalOffset(fontIndex, newOffset);
                    RecalcCustomLineHeights();
                }

                var scaleValue = _settings.GetScaleFactor(fontIndex);
                listingStandard.AddLabeledSlider(
                    "CustomFonts.FontScalingFactor".Translate(_settings.GetScaleFactor(fontIndex).ToString("F1")),
                     ref scaleValue, 0.5f, 2.0f);
                var fontSizeScale = (float)Math.Round(scaleValue, 1);
                if (Math.Abs(_settings.GetScaleFactor(fontIndex) - fontSizeScale) > 0.01)
                {
                    ApplyScaleFactor(fontIndex, fontSizeScale);
                    UpdateFont();
                }

                var widthScaleValue = _settings.GetFontWidthScaleFactor(fontIndex);
                listingStandard.AddLabeledSlider(
                    "CustomFonts.FontWidthScalingFactor".Translate(_settings.GetFontWidthScaleFactor(fontIndex).ToString("F1")),
                  ref widthScaleValue, 0.5f, 2.0f);
                var widthScale = (float)Math.Round(widthScaleValue, 1);
                if (Math.Abs(_settings.GetFontWidthScaleFactor(fontIndex) - widthScale) > 0.01)
                {
                    ApplyFontWidthScaleFactor(fontIndex, widthScale);
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
                    _worldScalePending = true;
                }

                // Rebuilding every world label costs far more than moving a slider, so the new
                // scale is applied once the slider is let go rather than on every drag tick.
                // hotControl drops back to 0 as soon as no control is being dragged any more.
                if (_worldScalePending && GUIUtility.hotControl == 0)
                {
                    _worldScalePending = false;
                    DebugLog($"world scale slider released at {_settings.WorldScaleFactor:F1}, rebuilding labels");
                    RefreshWorldFeatureTexts();
                }
            }

            listingStandard.End();
        }

        // With uniform fonts on, only the Medium tab is reachable, so a slider must write the
        // value to every size. Writing just the selected one let Tiny and Small keep stale
        // values that the UI still rendered with while offering no way to change them.
        private void ApplyVerticalOffset(GameFont fontIndex, int value)
        {
            if (!_settings.UniformUIfonts)
            {
                _settings.SetVerticalOffset(fontIndex, value);
                return;
            }
            foreach (GameFont each in Enum.GetValues(typeof(GameFont)))
            {
                _settings.SetVerticalOffset(each, value);
            }
        }

        private void ApplyScaleFactor(GameFont fontIndex, float value)
        {
            if (!_settings.UniformUIfonts)
            {
                _settings.SetScaleFactor(fontIndex, value);
                return;
            }
            foreach (GameFont each in Enum.GetValues(typeof(GameFont)))
            {
                _settings.SetScaleFactor(each, value);
            }
        }

        private void ApplyFontWidthScaleFactor(GameFont fontIndex, float value)
        {
            if (!_settings.UniformUIfonts)
            {
                _settings.SetFontWidthScaleFactor(fontIndex, value);
                return;
            }
            foreach (GameFont each in Enum.GetValues(typeof(GameFont)))
            {
                _settings.SetFontWidthScaleFactor(each, value);
            }
        }

        public void DrawLinuxSettings(Rect inRect)
        {
            SetupPlatformInfo();
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

            DrawDebugSettings(listingStandard);

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

        public void SetupPlatformInfo()
        {
            if (_hasPlatformInfo) return;
            _hasPlatformInfo = true;
            try
            {
                string UnityPlayerSoPath = Path.GetFullPath("UnityPlayer.so");
                _hasUnityPlayerSo = File.Exists(UnityPlayerSoPath);
                _hasRunHostFonts = Directory.Exists("/run/host/fonts");
            }
            catch (Exception ex)
            {
                Log.Message($"[Custom Fonts] error: {ex.Message}");
            }
        }

        // Reads the family / style name straight out of the font file through the low level
        // font engine. Creating a TMP_FontAsset just to read those two strings would allocate
        // a full glyph atlas per font, which is never released.
        private static bool TryGetFaceName(string filePath, out string faceName)
        {
            faceName = null;
            if (!_fontEngineInitialized)
            {
                FontEngine.InitializeFontEngine();
                _fontEngineInitialized = true;
            }

            if (FontEngine.LoadFontFace(filePath) != FontEngineError.Success) return false;
            try
            {
                var faceInfo = FontEngine.GetFaceInfo();
                if (string.IsNullOrEmpty(faceInfo.familyName)) return false;
                faceName = $"{faceInfo.familyName} ({faceInfo.styleName})";
                return true;
            }
            finally
            {
                FontEngine.UnloadFontFace();
            }
        }

        private static bool TryGetFaceName(Font font, out string faceName)
        {
            faceName = null;
            if (!_fontEngineInitialized)
            {
                FontEngine.InitializeFontEngine();
                _fontEngineInitialized = true;
            }

            if (FontEngine.LoadFontFace(font) != FontEngineError.Success) return false;
            try
            {
                var faceInfo = FontEngine.GetFaceInfo();
                if (string.IsNullOrEmpty(faceInfo.familyName)) return false;
                faceName = $"{faceInfo.familyName} ({faceInfo.styleName})";
                return true;
            }
            finally
            {
                FontEngine.UnloadFontFace();
            }
        }

        public void SetupOSFontPaths()
        {
            try
            {
                if (_hasOSFontAssets) return;
                _hasOSFontAssets = true;
                var startedAt = Time.realtimeSinceStartup;
                foreach (var path in Font.GetPathsToOSFonts())
                {
                    string fontName;
                    if (!TryGetFaceName(path, out fontName))
                    {
#if DEBUG
                        Log.Warning($"[Custom Fonts] Unable to read face info for {path}, skipping.");
#endif
                        continue;
                    }
                    if (!OSFontPaths.ContainsKey(fontName))
                    {
                        OSFontPaths.Add(fontName, path);
                    }
                }
                DebugLog($"scanned {OSFontPaths.Count} OS font faces in {(Time.realtimeSinceStartup - startedAt) * 1000f:F0} ms");
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
                string faceName;
                if (!TryGetFaceName(font, out faceName))
                {
                    Log.Warning($"[Custom Fonts] Unable to read face info for bundled font {font.name}, skipping.");
                    continue;
                }
                var fontName = $"(Bundled) {faceName}";
                if (!BundledFonts.ContainsKey(fontName))
                {
                    BundledFonts.Add(fontName, font);
                }
            }
            DebugLog($"loaded {BundledFonts.Count} bundled font(s)");
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
            RefreshWorldFeatureTexts();
        }

        // World feature labels pick up their font in Init, which only runs when the labels are
        // built. Throwing the existing ones away makes RimWorld rebuild them with the font that
        // is selected now, instead of the change only showing up after a save reload.
        public void RefreshWorldFeatureTexts()
        {
            try
            {
                if (Current.ProgramState != ProgramState.Playing || Find.World == null) return;

                var features = Find.WorldFeatures;
                if (features == null) return;

                var texts = AccessTools.Field(typeof(WorldFeatures), "texts").GetValue(features) as IList<WorldFeatureTextMesh>;
                var destroyed = 0;
                if (texts != null)
                {
                    foreach (var text in texts)
                    {
                        if (text == null) continue;
                        text.Destroy();
                        destroyed++;
                    }
                    texts.Clear();
                }
                features.textsCreated = false;

                // Rebuild straight away when the world is on screen; otherwise the labels are
                // built the next time it is shown.
                AccessTools.Method(typeof(WorldFeatures), "CreateOrDestroyTexts").Invoke(features, null);

                DebugLog($"world feature texts invalidated ({destroyed} destroyed), " +
                         $"rebuilt with '{_settings.CurrentWorldFontName}'");
            }
            catch (Exception ex)
            {
                Log.Warning($"[Custom Fonts] unable to refresh world feature texts: {ex.Message}");
            }
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
            var fontName = _settings.GetCurrentUIFontName(fontIndex);
            var fontSize = (int)Math.Round(DefaultFonts[fontIndex].fontSize * _settings.GetScaleFactor(fontIndex));
            var isBundled = BundledFonts.ContainsKey(fontName);

            Font font = null;
            var fontKey = DefaultFontKey;

            try
            {
                if (isBundled)
                {
                    font = BundledFonts[fontName];
                    fontKey = BundledFontKey(fontName);
                }
                else if (fontName != FontSettings.DefaultFontName)
                {
                    fontKey = OSFontKey(fontName, fontSize);
                    font = ResolveOSFont(fontKey, fontName, fontSize);
                }
                else
                {
                    font = DefaultFonts[fontIndex];
                }
            }
            catch (Exception ex)
            {
                Log.Message($"[Custom Fonts] error: {ex.Message}");
                font = null;
            }

            if (font == null)
            {
                Log.Message($"[Custom Fonts] Font {fontName} not found, using default font");
                _settings.SetCurrentUIFontName(fontIndex, FontSettings.DefaultFontName);
                font = DefaultFonts[fontIndex];
                fontKey = DefaultFontKey;
            }

            string previousKey;
            if (!_appliedFontKeys.TryGetValue(fontIndex, out previousKey) || previousKey != fontKey)
            {
                Log.Message($"[Custom Fonts] Updating font for {fontIndex} with name {fontName}");
            }
            _appliedFontKeys[fontIndex] = fontKey;

            CurrentFonts[fontIndex] = font;
            Text.fontStyles[(int)fontIndex].font = font;
            Text.fontStyles[(int)fontIndex].fontSize = fontSize;
            Text.textFieldStyles[(int)fontIndex].font = font;
            Text.textFieldStyles[(int)fontIndex].fontSize = fontSize;
            Text.textAreaStyles[(int)fontIndex].font = font;
            Text.textAreaStyles[(int)fontIndex].fontSize = fontSize;
            Text.textAreaReadOnlyStyles[(int)fontIndex].font = font;
            Text.textAreaReadOnlyStyles[(int)fontIndex].fontSize = fontSize;
            ApplyFontToTextCaches(fontIndex, font);
            RecalcCustomLineHeights(fontIndex);
            PruneOSFontCache();
        }

        // RimWorld derives its layout metrics from the fonts once, at startup, and caches them
        // in private statics. Swapping the GUIStyle font alone leaves Text.fonts pointing at the
        // original font and every line height / line spacing describing it, so a bigger font
        // still gets laid out in the original font's rows.
        private void ApplyFontToTextCaches(GameFont fontIndex, Font font)
        {
            try
            {
                var fonts = AccessTools.StaticFieldRefAccess<Font[]>(typeof(Text), "fonts");
                if (fonts != null && (int)fontIndex < fonts.Length) fonts[(int)fontIndex] = font;
            }
            catch (Exception ex)
            {
                Log.Warning($"[Custom Fonts] unable to update Text.fonts: {ex.Message}");
            }
            _textMetricsDirty = true;
        }

        // CalcHeight needs a live GUI context, and fonts also change from places that are not
        // inside OnGUI, so the metrics are refreshed from the per frame hook instead.
        public void RecalcTextMetricsIfNeeded()
        {
            if (!_textMetricsDirty) return;
            _textMetricsDirty = false;

            try
            {
                var lineHeights = AccessTools.StaticFieldRefAccess<float[]>(typeof(Text), "lineHeights");
                var spaceBetweenLines = AccessTools.StaticFieldRefAccess<float[]>(typeof(Text), "spaceBetweenLines");
                if (lineHeights == null || spaceBetweenLines == null) return;

                var backup = Text.Font;
                foreach (GameFont value in Enum.GetValues(typeof(GameFont)))
                {
                    var index = (int)value;
                    if (index >= lineHeights.Length || index >= spaceBetweenLines.Length) continue;

                    Text.Font = value;
                    var lineHeight = Text.CalcHeight("W", 999f);
                    lineHeights[index] = lineHeight;
                    spaceBetweenLines[index] = Text.CalcHeight("W\nW", 999f) - lineHeight * 2f;
                }
                Text.Font = backup;

                DebugLog($"refreshed Text line metrics: {DescribeLineMetrics()}");
            }
            catch (Exception ex)
            {
                Log.Warning($"[Custom Fonts] unable to refresh Text line metrics: {ex.Message}");
            }
        }

        private static string BundledFontKey(string fontName) => "bundled:" + fontName;

        private static string OSFontKey(string fontName, int fontSize) => "os:" + fontSize + ":" + fontName;

        private Font ResolveOSFont(string fontKey, string fontName, int fontSize)
        {
            Font cached;
            if (_osFontCache.TryGetValue(fontKey, out cached) && cached != null) return cached;

            var font = Font.CreateDynamicFontFromOSFont(fontName, fontSize);
            if (font != null)
            {
                _osFontCache[fontKey] = font;
                DebugLog($"created dynamic OS font '{fontName}' @{fontSize} (cache={_osFontCache.Count})");
            }
            else
            {
                _osFontCache.Remove(fontKey);
            }
            return font;
        }

        // Only the dynamic fonts that are actually bound to a GameFont are kept alive; every
        // other one was created by an earlier font/size change and can go away.
        private void PruneOSFontCache()
        {
            if (_osFontCache.Count == 0) return;

            List<string> stale = null;
            foreach (var entry in _osFontCache)
            {
                if (_appliedFontKeys.ContainsValue(entry.Key)) continue;
                if (stale == null) stale = new List<string>();
                stale.Add(entry.Key);
            }
            if (stale == null) return;

            foreach (var key in stale)
            {
                DebugLog($"releasing unused dynamic OS font [{key}]");
                QueueDestroy(_osFontCache[key]);
                _osFontCache.Remove(key);
            }
        }

        public TMP_FontAsset GetWorldFontAsset()
        {
            var fontName = _settings.CurrentWorldFontName;
            if (fontName == FontSettings.DefaultFontName)
            {
                ReleaseWorldFontAsset();
                return DefaultTMPFontAsset;
            }
            if (_worldFontAssetKey == fontName && _worldFontAsset != null)
            {
                DebugLog($"reusing world font asset '{fontName}'");
                return _worldFontAsset;
            }

            TMP_FontAsset fontAsset = null;
            Font source = null;
            try
            {
                if (BundledFonts.ContainsKey(fontName))
                {
                    fontAsset = TMP_FontAsset.CreateFontAsset(BundledFonts[fontName]);
                }
                else
                {
                    SetupOSFontPaths();
                    string path;
                    if (OSFontPaths.TryGetValue(fontName, out path))
                    {
                        source = new Font(path);
                        fontAsset = TMP_FontAsset.CreateFontAsset(source);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Message($"[Custom Fonts] error: {ex.Message}");
                fontAsset = null;
            }

            ReleaseWorldFontAsset();

            if (fontAsset == null)
            {
                QueueDestroy(source);
                Log.Message($"[Custom Fonts] World font {fontName} not found, using default font");
                _settings.CurrentWorldFontName = FontSettings.DefaultFontName;
                return DefaultTMPFontAsset;
            }

            _worldFontAsset = fontAsset;
            _worldFontSource = source;
            _worldFontAssetKey = fontName;
            DebugLog($"created world font asset '{fontName}' ({fontAsset.atlasWidth}x{fontAsset.atlasHeight})");
            return fontAsset;
        }

        // A TMP_FontAsset owns an atlas texture and a material; dropping the reference alone
        // leaves both behind, so every world map re-init would leak another atlas.
        public void ReleaseWorldFontAsset()
        {
            if (_worldFontAsset != null)
            {
                DebugLog($"releasing world font asset '{_worldFontAssetKey}'");
                var atlasTextures = _worldFontAsset.atlasTextures;
                if (atlasTextures != null)
                {
                    foreach (var atlasTexture in atlasTextures)
                    {
                        QueueDestroy(atlasTexture);
                    }
                }
                QueueDestroy(_worldFontAsset.material);
                QueueDestroy(_worldFontAsset);
            }
            QueueDestroy(_worldFontSource);

            _worldFontAsset = null;
            _worldFontSource = null;
            _worldFontAssetKey = null;
        }

        private struct PendingDestroy
        {
            public int Frame;
            public UnityEngine.Object Target;
        }

        private void QueueDestroy(UnityEngine.Object target)
        {
            if (target == null) return;
            if (IsProtected(target))
            {
                Log.Warning($"[Custom Fonts] Refusing to destroy game owned resource {target.name} ({target.GetType().Name})");
                return;
            }
            _pendingDestroy.Add(new PendingDestroy { Frame = Time.frameCount, Target = target });
        }

        // The game's own fonts and everything that comes out of the asset bundle belong to
        // RimWorld / Unity, never to this mod. Destroying one of those would break the UI for
        // the rest of the session, so they are rejected here no matter who asks.
        private bool IsProtected(UnityEngine.Object target)
        {
            foreach (var font in DefaultFonts.Values)
            {
                if (target == font) return true;
            }
            foreach (var font in BundledFonts.Values)
            {
                if (target == font) return true;
            }
            foreach (var style in DefaultFontStyle.Values)
            {
                if (style != null && target == style.font) return true;
            }

            if (DefaultTMPFontAsset != null)
            {
                if (target == DefaultTMPFontAsset) return true;
                if (target == DefaultTMPFontAsset.material) return true;
                if (target == DefaultTMPFontAsset.sourceFontFile) return true;
                var atlasTextures = DefaultTMPFontAsset.atlasTextures;
                if (atlasTextures != null)
                {
                    foreach (var atlasTexture in atlasTextures)
                    {
                        if (target == atlasTexture) return true;
                    }
                }
            }

            return false;
        }

        // Destruction is deferred by a frame so nothing that is still being drawn with the
        // replaced font/asset in the current OnGUI pass can hit a destroyed object.
        public void ProcessPendingDestroy()
        {
            if (_pendingDestroy.Count == 0) return;

            var frame = Time.frameCount;
            var destroyed = 0;
            for (var i = _pendingDestroy.Count - 1; i >= 0; i--)
            {
                var entry = _pendingDestroy[i];
                if (frame - entry.Frame < 2) continue;
                _pendingDestroy.RemoveAt(i);
                if (entry.Target == null) continue;
                DebugLog($"destroy {entry.Target.GetType().Name} '{entry.Target.name}'");
                UnityEngine.Object.Destroy(entry.Target);
                destroyed++;
            }
            if (destroyed > 0) DebugLog($"destroyed {destroyed} object(s), {_pendingDestroy.Count} still queued");
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
        private static CustomFonts _mod;

        // StartOfOnGUI runs for every GUI event, so the mod lookup is resolved only once.
        private static CustomFonts Mod => _mod ?? (_mod = LoadedModManager.GetMod<CustomFonts>());

        [HarmonyPatch(typeof(Text), nameof(Text.StartOfOnGUI))]
        class StartOfOnGUIPatcher
        {
            [HarmonyPostfix]
            public static void Postfix()
            {
                var mod = Mod;
                if (mod == null) return;

                if (!_patcherInitialized)
                {
                    _patcherInitialized = true;
                    mod.SetupOSInstalledFontNames();
                    mod.SetupBundledFonts();
                    mod.DefaultTMPFontAsset = WorldFeatureTextMesh_TextMeshPro.WorldTextPrefab.GetComponent<TextMeshPro>().font;
                    mod.UpdateFont();
                    AccessTools.StaticFieldRefAccess<bool>(typeof(WorldFeatures), "ForceLegacyText") = mod.ForceLegacyText;
                    mod.DebugLog("font patcher initialised");
                    if (mod.DebugLogging) mod.LogResourceStats("startup");

                    // Unattended leak check: RimWorldWin64.exe -customfonts-stress
                    if (Environment.GetCommandLineArgs().Any(arg => arg == "-customfonts-stress"))
                    {
                        mod.BeginFontStressTest();
                    }
                    if (Environment.GetCommandLineArgs().Any(arg => arg == "-customfonts-worldtest"))
                    {
                        mod.BeginWorldFontTest();
                    }
                }

                mod.RecalcTextMetricsIfNeeded();
                mod.TickFontStressTest();
                mod.TickWorldFontTest();
                mod.ProcessPendingDestroy();
                mod.TickDebugStats();
            }
        }

        [HarmonyPatch(typeof(GenScene), nameof(GenScene.GoToMainMenu))]
        class GoToMainMenuPatcher
        {
            [HarmonyPostfix]
            public static void Postfix()
            {
                var mod = Mod;
                mod.UpdateFont();
            }
        }

        [HarmonyPatch(typeof(WorldFeatures), "HasCharacter")]
        private class WorldMapHasCharacterPatcher
        {
            [HarmonyPrefix]
            public static bool Prefix(ref bool __result)
            {
                var mod = Mod;
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
                var mod = Mod;
                var fontAsset = mod.GetWorldFontAsset();

                var prefab = WorldFeatureTextMesh_TextMeshPro.WorldTextPrefab.GetComponent<TextMeshPro>();
                if (prefab.font != fontAsset)
                {
                    prefab.font = fontAsset;
                    prefab.UpdateFontAsset();
                }
                AccessTools.StaticFieldRefAccess<float>(typeof(WorldFeatureTextMesh_TextMeshPro), "TextScale") =
                    1.75f * mod._settings.WorldScaleFactor;
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
