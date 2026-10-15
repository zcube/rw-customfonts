using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Verse;
using UnityEngine;
using HarmonyLib;
using RimWorld.Planet;
using RimWorld;
using SettingsHelper;
using TMPro;

namespace CustomFonts
{

    // Source management and selection are shared. The selected engine supplies font
    // metadata/creation, platform diagnostics and native resource ownership.
    public partial class CustomFonts : Mod
    {
        public readonly FontSettings _settings;
        public void TickEngineVerification() => OnEngineVerification();
        partial void OnEngineVerification();
        private List<string> _fontNames = new List<string>();
        private bool _hasInstalledFontNames;
        private bool _hasBundledFonts;
        private bool _hasOSFontAssets;

        public bool ForceLegacyText;
        public readonly Dictionary<string, Font> BundledFonts = new Dictionary<string, Font>();
        public readonly Dictionary<GameFont, Font> DefaultFonts = new Dictionary<GameFont, Font>();
        public readonly Dictionary<GameFont, Font> CurrentFonts = new Dictionary<GameFont, Font>();
        public readonly Dictionary<GameFont, GUIStyle> DefaultFontStyle = new Dictionary<GameFont, GUIStyle>();
        private readonly Dictionary<GameFont, Vector2[]> _defaultContentOffsets = new Dictionary<GameFont, Vector2[]>();
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

        private bool _textMetricsDirty;
        private bool _worldScalePending;
        private float _lastSpecimenHeight;
        private string _lastUniformSignature;
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
            }

            ForceLegacyText = UseLegacyWorldText(_settings.CurrentWorldFontName);

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

            tabs[0].label = "CustomFonts.FontTiny".Translate(DisplayFontName(_settings.GetCurrentUIFontName(GameFont.Tiny)));
            tabs[1].label = "CustomFonts.FontSmall".Translate(DisplayFontName(_settings.GetCurrentUIFontName(GameFont.Small)));
            tabs[2].label = "CustomFonts.FontMedium".Translate(DisplayFontName(_settings.GetCurrentUIFontName(GameFont.Medium)));
            tabs[3].label = "CustomFonts.FontWorld".Translate(DisplayFontName(_settings.CurrentWorldFontName));
            tabs[4].label = "CustomFonts.Etc".Translate(DisplayFontName(_settings.CurrentWorldFontName));

            var listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);
            if (listingStandard.ButtonTextLabeled("CustomFonts.ResetToDefault".Translate(), "CustomFonts.Apply".Translate()))
                ResetToDefault();
            var uniformBefore = _settings.UniformUIfonts;
            listingStandard.CheckboxLabeled("CustomFonts.UniformUIfonts".Translate(), ref _settings.UniformUIfonts, "CustomFonts.UniformUIfonts".Translate());
            if (_settings.UniformUIfonts != uniformBefore)
            {
                // Tiny/Small only scale the vertical offset while uniform is on, so the applied
                // offsets have to be rebuilt whenever the checkbox flips either way.
                RecalcCustomLineHeights();
            }
            EnsureUniformFonts();
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
                    listingStandard.Label("CustomFonts.CurrentFont".Translate(DisplayFontName(_settings.GetCurrentUIFontName(GameFont.Tiny))));
                    break;
                case 1:
                    listingStandard.Label("CustomFonts.CurrentFont".Translate(DisplayFontName(_settings.GetCurrentUIFontName(GameFont.Small))));
                    break;
                case 2:
                    listingStandard.Label("CustomFonts.CurrentFont".Translate(DisplayFontName(_settings.GetCurrentUIFontName(GameFont.Medium))));
                    break;
                case 3:
                    listingStandard.Label("CustomFonts.CurrentFont".Translate(DisplayFontName(_settings.CurrentWorldFontName)));
                    break;
                case 4:
                    listingStandard.Label("CustomFonts.Etc".Translate(DisplayFontName(_settings.CurrentWorldFontName)));
                    break;
            }

            if (toolbarInt < 4)
            {
                var specimenText = "CustomFonts.Specimen".Translate();
                // Measure translated/scaled previews instead of reserving a fixed height.
                var previewHeight = MeasureSpecimenHeight(specimenText, listingStandard.ColumnWidth);
                Rect fontViewRect = listingStandard.GetRect(previewHeight);
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
                    DrawFontSourceSettings(fullRect);
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
                (Text.LineHeight + 4f) * (3 + bundledFontsCount + fontNameCount));
            Widgets.BeginScrollView(fontListScrollOuter, ref scrollPosition, fontListScrollInner);
            var listingStandard = fontListScrollInner.BeginListingStandard();

            if (DrawFontChoice(listingStandard, (FontTarget)fontIndex, FontSettings.DefaultFontName, _settings.GetCurrentUIFontName(fontIndex) == FontSettings.DefaultFontName))
            {
                if (_settings.UniformUIfonts)
                    SaveFontAll(FontSettings.DefaultFontName);
                else
                    SaveFont(fontIndex, FontSettings.DefaultFontName);
            }
            listingStandard.GapLine();
            foreach (var name in BundledFonts.Keys.OrderBy(x => x))
            {
                if (name.ToLower().Contains(searchText.ToLower()))
                {
                    if (DrawFontChoice(listingStandard, (FontTarget)fontIndex, name, _settings.GetCurrentUIFontName(fontIndex) == name))
                    {
                        if (_settings.UniformUIfonts)
                            SaveFontAll(name);
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
                    if (DrawFontChoice(listingStandard, (FontTarget)fontIndex, name, _settings.GetCurrentUIFontName(fontIndex) == name))
                    {
                        if (_settings.UniformUIfonts)
                            SaveFontAll(name);
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
                (Text.LineHeight + 4f) * (3 + bundledFontsCount + osFontPathsCount));
            Widgets.BeginScrollView(fontListScrollOuter, ref scrollPosition, fontListScrollInner);
            var listingStandard = fontListScrollInner.BeginListingStandard();

            if (DrawFontChoice(listingStandard, FontTarget.World, FontSettings.DefaultFontName, _settings.CurrentWorldFontName == FontSettings.DefaultFontName))
                SaveWorldFont(FontSettings.DefaultFontName);
            listingStandard.GapLine();
            foreach (var name in BundledFonts.Keys.OrderBy(x => x))
            {
                if (name.ToLower().Contains(searchText.ToLower()))
                {
                    if (DrawFontChoice(listingStandard, FontTarget.World, name, _settings.CurrentWorldFontName == name))
                        SaveWorldFont(name);
                }
            }
            listingStandard.GapLine();
            foreach (var name in OSFontPaths.Keys.OrderBy(x => x))
            {
                if (name.ToLower().Contains(searchText.ToLower()))
                {
                    if (DrawFontChoice(listingStandard, FontTarget.World, name, _settings.CurrentWorldFontName == name))
                        SaveWorldFont(name);
                }
            }

            listingStandard.End();
            Widgets.EndScrollView();
        }

        public void DrawPropertySettings(int toolbarInt, Rect inRect)
        {
            var target = (FontTarget)toolbarInt;
            var view = new Rect(0, 0, inRect.width - 20, Math.Max(inRect.height, _propertyContentHeight[toolbarInt]));
            Widgets.BeginScrollView(inRect, ref _propertyScroll[toolbarInt], view);
            var listingStandard = new Listing_Standard();
            listingStandard.Begin(view);

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

            DrawFallbackSettings(listingStandard, view.width, target);
            _propertyContentHeight[toolbarInt] = listingStandard.CurHeight + 16;
            listingStandard.End();
            Widgets.EndScrollView();
        }

        // Tiny and Small are copies of Medium while uniform fonts are on, and the Medium tab
        // is the only one that can edit them. A change that reaches only one size therefore
        // leaves the other two drawing something the player cannot correct - which is what
        // toggling the checkbox off and on used to work around. This repairs the copies
        // wherever the divergence came from, and only once per distinct Medium state so a
        // font that refuses to bind cannot turn into a per frame font rebuild.
        public void EnsureUniformFonts()
        {
            if (!_settings.UniformOutOfSync()) return;

            var signature = _settings.UniformSignature();
            if (signature == _lastUniformSignature) return;
            _lastUniformSignature = signature;

            _settings.UpdateUniformUIfonts();
            UpdateFont();
            RecalcCustomLineHeights();
            DebugLog($"uniform UI fonts resynced from Medium ({DescribeSettings()})");
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

        public override string SettingsCategory() => "Custom Fonts";// "CustomFonts.Category".Translate();

        private void SaveFont(GameFont fontIndex, string fontName, bool forceUpdate = false)
        {
            if (fontName == _settings.GetCurrentUIFontName(fontIndex) && !forceUpdate) return;
            _settings.SetCurrentUIFontName(fontIndex, fontName);
            UpdateFont(fontIndex);
            RecalcTextMetricsIfNeeded();
        }

        private void ResetToDefault()
        {
            // Clear every scope before rebinding fonts. Uniform mode must not copy an
            // old Medium fallback list back into Tiny/Small during reset or saving.
            _settings.FallbackFontNames.Clear();
            foreach (FontTarget target in Enum.GetValues(typeof(FontTarget)))
                _settings.SetFallbackFonts(target, new string[0]);
            foreach (GameFont size in Enum.GetValues(typeof(GameFont)))
            {
                _settings.SetCurrentUIFontName(size, FontSettings.DefaultFontName);
                _settings.SetScaleFactor(size, 1f);
                _settings.SetVerticalOffset(size, 0);
                _settings.SetFontWidthScaleFactor(size, 1f);
            }
            _settings.UniformUIfonts = true;
            _settings.CurrentWorldFontName = FontSettings.DefaultFontName;
            _settings.WorldScaleFactor = 1f;
            _worldScalePending = false;
            _fallbackConfiguration = null;
            ReleaseWorldFontAsset();
            UpdateFont();
            // Default may already be selected, but its fallback policy or scale changed.
            RefreshWorldFeatureTexts();
            WriteSettings();
        }

        // Every size is written and re-applied unconditionally: SaveFont skips a size whose
        // name already matches, which would leave that size bound to the font it happens to
        // be drawing with right now instead of the one that was just picked.
        private void SaveFontAll(string fontName)
        {
            foreach (GameFont value in Enum.GetValues(typeof(GameFont)))
            {
                _settings.SetCurrentUIFontName(value, fontName);
            }
            UpdateFont();
        }

        private void SaveWorldFont(string fontName)
        {
            fontName = CanonicalFontSelection(fontName);
            if (fontName == _settings.CurrentWorldFontName) return;
            _settings.CurrentWorldFontName = fontName;
            // Unity's Default uses legacy labels, which never request another TMP asset.
            // Release the previous custom asset here even when the next renderer is legacy.
            ReleaseWorldFontAsset();
            ForceLegacyText = UseLegacyWorldText(fontName);
            ForceLegacyText = ForceLegacyText && !HasDefinedFallbacks;
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
                Log.Warning($"[Custom Fonts] unable to refresh world feature texts: {ex.GetBaseException()}");
            }
        }

        public void UpdateFont()
        {
            EnsureFallbackConfiguration();
            _settings.CurrentWorldFontName = CanonicalFontSelection(_settings.CurrentWorldFontName);
            foreach (GameFont value in Enum.GetValues(typeof(GameFont)))
            {
                UpdateFont(value);
            }
            PruneScopedFonts();
            RecalcTextMetricsIfNeeded();
        }

        private void UpdateFont(GameFont fontIndex)
        {
            EnsureFallbackConfiguration();
            var fallbacks = SourcesFor((FontTarget)fontIndex);
            string policy = FallbackPolicy((FontTarget)fontIndex);
            var fontName = _settings.GetCurrentUIFontName(fontIndex);
            if (fontName.StartsWith("(Bundled) ", StringComparison.Ordinal)) SetupBundledFonts();
            if (fontName != FontSettings.DefaultFontName && !BundledFonts.ContainsKey(fontName))
            {
                SetupOSFontPaths();
                fontName = CanonicalFontSelection(fontName);
                _settings.SetCurrentUIFontName(fontIndex, fontName);
            }
            var fontSize = (int)Math.Round(DefaultUIFontSize(fontIndex) * _settings.GetScaleFactor(fontIndex));
            string sizeKey = FontSizeKey(fontSize);
            var isBundled = BundledFonts.ContainsKey(fontName);

            Font font = null;
            var fontKey = DefaultFontKey;

            try
            {
                if (isBundled)
                {
                    font = ResolveBundledFont(fontName, fallbacks, fontSize);
                    fontKey = BundledFontKey(fontName) + "|" + policy + sizeKey;
                }
                else if (fontName != FontSettings.DefaultFontName)
                {
                    fontKey = FileFontKey(fontName) + "|" + policy + sizeKey;
                    font = ResolveFileFont(fontKey, fontName, fallbacks, fontSize);
                }
                else
                {
                    font = ResolveDefaultUIFont(fontIndex, fontSize);
                    fontKey += "|" + policy + sizeKey;
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
                // Keep unavailable portable selections so a refresh or another OS can
                // resolve them later, while rendering with the default for this session.
                font = ResolveDefaultUIFont(fontIndex, fontSize);
                fontKey = DefaultFontKey + "|" + policy + sizeKey;
            }

            string previousKey;
            if (!_appliedFontKeys.TryGetValue(fontIndex, out previousKey) || previousKey != fontKey)
            {
                Log.Message($"[Custom Fonts] Updating font for {fontIndex} with name {fontName}");
            }
            _appliedFontKeys[fontIndex] = fontKey;

            CurrentFonts[fontIndex] = font;
            foreach (var style in UIFontStyles(fontIndex))
            {
                style.font = font;
                style.fontSize = fontSize;
            }
            ApplyFontToTextCaches(fontIndex, font);
            RecalcCustomLineHeights(fontIndex);
            PruneOSFontCache();
            PruneScopedFonts();
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

        // Refresh before the rest of the current GUI event draws with the new font.
        // Changes outside OnGUI stay dirty until the next StartOfOnGUI hook.
        public void RecalcTextMetricsIfNeeded()
        {
            if (!_textMetricsDirty || Event.current == null) return;

            var backup = Text.Font;
            try
            {
                var lineHeights = AccessTools.StaticFieldRefAccess<float[]>(typeof(Text), "lineHeights");
                var spaceBetweenLines = AccessTools.StaticFieldRefAccess<float[]>(typeof(Text), "spaceBetweenLines");
                if (lineHeights == null || spaceBetweenLines == null) return;

                foreach (GameFont value in Enum.GetValues(typeof(GameFont)))
                {
                    var index = (int)value;
                    if (index >= lineHeights.Length || index >= spaceBetweenLines.Length) continue;

                    Text.Font = value;
                    var lineHeight = Text.CalcHeight("W", 999f);
                    lineHeights[index] = lineHeight;
                    spaceBetweenLines[index] = Text.CalcHeight("W\nW", 999f) - lineHeight * 2f;
                }
                _textMetricsDirty = false;

                DebugLog($"refreshed Text line metrics: {DescribeLineMetrics()}");
            }
            catch (Exception ex)
            {
                Log.Warning($"[Custom Fonts] unable to refresh Text line metrics: {ex.Message}");
            }
            finally { Text.Font = backup; }
        }

        private static string BundledFontKey(string fontName) => "bundled:" + fontName;

        private static string FileFontKey(string fontName) => "file:" + fontName;

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
            EnsureFallbackConfiguration();
            var fontName = _settings.CurrentWorldFontName;
            if (fontName.StartsWith("(Bundled) ", StringComparison.Ordinal)) SetupBundledFonts();
            if (fontName != FontSettings.DefaultFontName && !BundledFonts.ContainsKey(fontName))
            {
                SetupOSFontPaths();
                fontName = CanonicalFontSelection(fontName);
                _settings.CurrentWorldFontName = fontName;
            }
            if (fontName == FontSettings.DefaultFontName && !HasDefinedFallbacks)
            {
                ReleaseWorldFontAsset();
                PrepareWorldFontAsset(DefaultTMPFontAsset);
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
                if (fontName == FontSettings.DefaultFontName)
                {
                    fontAsset = CreateDefaultWorldFontAsset();
                }
                else if (BundledFonts.ContainsKey(fontName))
                {
                    source = ResolveBundledFont(fontName, FallbackSources);
                    fontAsset = TMP_FontAsset.CreateFontAsset(source);
                }
                else
                {
                    SetupOSFontPaths();
                    string path;
                    if (OSFontPaths.TryGetValue(fontName, out path))
                    {
                        source = LoadFontFile(path, FallbackSources);
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
                if (!HasDefinedFallbacks)
                {
                    PrepareWorldFontAsset(DefaultTMPFontAsset);
                    return DefaultTMPFontAsset;
                }
                fontAsset = CreateDefaultWorldFontAsset();
                if (fontAsset == null) return DefaultTMPFontAsset;
            }

            _worldFontAsset = fontAsset;
            PrepareWorldFontAsset(fontAsset);
            AttachWorldFallbacks(fontAsset, fontName);
            _worldFontSource = source;
            _worldFontAssetKey = fontName;
            PruneScopedFonts();
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
                ReleaseWorldFontResources(_worldFontAsset);
                if (!IsProtected(_worldFontAsset.sourceFontFile)) QueueDestroy(_worldFontAsset.sourceFontFile);
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
            if (!_pendingDestroy.Any(p => p.Target == target))
                _pendingDestroy.Add(new PendingDestroy { Frame = Time.frameCount, Target = target });
        }

        // Original game fonts and the baked world atlas belong to RimWorld. Imported
        // font variants belong to this mod and stay alive while UI/world references them.
        private bool IsProtected(UnityEngine.Object target)
        {
            foreach (var font in DefaultFonts.Values)
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
                // UI and world now share the same content font. Releasing a TMP asset
                // must not destroy a source still bound to an active UI style or cache.
                if (entry.Target is Font font && font != null && IsFontReferenced(font)) continue;
                _pendingDestroy.RemoveAt(i);
                if (entry.Target == null) continue;
                if (entry.Target is Font released) _fileFontCache.Forget(released);
                DebugLog($"destroy {entry.Target.GetType().Name} '{entry.Target.name}'");
                if (!ReleaseEngineResource(entry.Target)) UnityEngine.Object.Destroy(entry.Target);
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

        // The offset is authored on the Medium tab while uniform fonts are on, so it describes
        // a Medium sized line. Handing the same pixel count to Tiny - roughly half the glyph
        // height - pushes it about twice as far relative to its own text, so the copies are
        // scaled by the size ratio that UpdateFont already sizes the fonts with. With uniform
        // off every size carries the offset its own tab set, and is used as is.
        public float EffectiveVerticalOffset(GameFont fontIndex)
        {
            var offset = (float)_settings.GetVerticalOffset(fontIndex);
            if (!_settings.UniformUIfonts || fontIndex == GameFont.Medium || Math.Abs(offset) < 0.001f) return offset;

            Font mediumFont;
            Font sizeFont;
            if (!DefaultFonts.TryGetValue(GameFont.Medium, out mediumFont) || mediumFont == null) return offset;
            if (!DefaultFonts.TryGetValue(fontIndex, out sizeFont) || sizeFont == null) return offset;
            if (mediumFont.fontSize <= 0) return offset;

            return offset * DefaultUIFontSize(fontIndex) / DefaultUIFontSize(GameFont.Medium);
        }

        internal void CaptureDefaultUIFonts()
        {
            // Text's static constructor loads Unity resources and reads GUI.skin.
            // First StartOfOnGUI is the safe point, before applying any selections.
            if (DefaultFonts.Count != 0) return;
            foreach (var size in PreviewFonts)
            {
                DefaultFonts[size] = Text.fontStyles[(int)size].font;
                DefaultFontStyle[size] = new GUIStyle(Text.fontStyles[(int)size]);
                _defaultContentOffsets[size] = UIFontStyles(size).Select(style => style.contentOffset).ToArray();
            }
        }

        private int DefaultUIFontSize(GameFont size) => DefaultFontStyle[size].fontSize > 0
            ? DefaultFontStyle[size].fontSize : DefaultFonts[size].fontSize;

        private static GUIStyle[] UIFontStyles(GameFont size) => new[] { Text.fontStyles[(int)size], Text.textFieldStyles[(int)size],
            Text.textAreaStyles[(int)size], Text.textAreaReadOnlyStyles[(int)size] };

        public void RecalcCustomLineHeights(GameFont fontIndex)
        {
            var offsetVector = new Vector2(0f, EffectiveVerticalOffset(fontIndex));
            var styles = UIFontStyles(fontIndex);
            for (int i = 0; i < styles.Length; i++)
                styles[i].contentOffset = _defaultContentOffsets[fontIndex][i] + offsetVector;
        }

    }

}
