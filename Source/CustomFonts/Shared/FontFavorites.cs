using System;
using System.Linq;
using UnityEngine;
using Verse;

namespace CustomFonts
{
    public partial class CustomFonts
    {
        private string _fontStyleFilter;
        private void DrawFontSearch(Listing_Standard list)
        {
            Rect searchRow = list.GetRect(30f);
            Widgets.Label(new Rect(searchRow.x, searchRow.y, 52f, 28f), "CustomFonts.Search".Translate());
            string query = Widgets.TextField(new Rect(searchRow.x + 54f, searchRow.y,
                Math.Max(1f, searchRow.width - 54f), 28f), searchText).Trim();
            if (query != searchText) { searchText = query; scrollPosition.y = 0f; }
            Rect styleRow = list.GetRect(30f);
            Widgets.Label(new Rect(styleRow.x, styleRow.y, 52f, 28f), "CustomFonts.Style".Translate());
            var styleRect = new Rect(styleRow.x + 54f, styleRow.y, Math.Max(1f, styleRow.width - 54f), 28f);
            string allStyles = "CustomFonts.AllStyles".Translate();
            if (Widgets.ButtonText(styleRect, _fontStyleFilter ?? allStyles))
            {
                var options = new System.Collections.Generic.List<FloatMenuOption>
                {
                    new FloatMenuOption(allStyles, () => { _fontStyleFilter = null; scrollPosition.y = 0f; })
                };
                foreach (string style in FontSourceCatalog.Styles(BundledFonts.Keys.Concat(OSFontPaths.Keys)))
                    options.Add(new FloatMenuOption(style, () => { _fontStyleFilter = style; scrollPosition.y = 0f; }));
                Find.WindowStack.Add(new FloatMenu(options));
            }
            TooltipHandler.TipRegion(styleRect, "CustomFonts.StyleFilterHelp".Translate());
        }
        private bool IsFavoriteFont(string name) => _settings.FavoriteFontNames
            .Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
        private void ToggleFavoriteFont(string name)
        {
            _settings.FavoriteFontNames = FontFavoriteLists.Toggle(_settings.FavoriteFontNames, name);
            scrollPosition.y = 0;
            WriteSettings();
        }
        private void DrawFontPicker(FontTarget target, Rect inRect, string current, Action<string> select)
        {
            var groups = FontFavoriteLists.Create(_settings.FavoriteFontNames, BundledFonts.Keys, OSFontPaths.Keys);
            string[] Filter(string[] names) => names.Where(name => FontSourceCatalog.MatchesStyle(name, _fontStyleFilter)
                && FontSourceCatalog.MatchesSearch(name, DisplayFontName(name), searchText)).ToArray();
            var favorites = Filter(groups.Favorites);
            var bundled = Filter(groups.Bundled);
            var installed = Filter(groups.Installed);
            var outer = new Rect(inRect.x, inRect.y, inRect.width - 10f, inRect.height);
            float width = Math.Max(1f, outer.width - 24f);
            float contentHeight = FontChoiceHeight(FontSettings.DefaultFontName, width) + 24f
                + (favorites.Length > 0 ? 36f : 0f);
            foreach (string name in favorites.Concat(bundled).Concat(installed)) contentHeight += FontChoiceHeight(name, width);
            var view = new Rect(0, 0, width, Math.Max(outer.height, contentHeight));
            scrollPosition.x = 0;
            scrollPosition.y = Mathf.Clamp(scrollPosition.y, 0, Math.Max(0, view.height - outer.height));
            _choiceVisibleTop = scrollPosition.y;
            _choiceVisibleBottom = scrollPosition.y + outer.height;
            Widgets.BeginScrollView(outer, ref scrollPosition, view);
            var list = new Listing_Standard();
            list.Begin(new Rect(0, 0, width, 1000000f)); list.ColumnWidth = width;
            try
            {
                void Choices(string[] names)
                {
                    foreach (string name in names)
                        if (DrawFontChoice(list, target, name, current == name)) select(name);
                }
                if (DrawFontChoice(list, target, FontSettings.DefaultFontName, current == FontSettings.DefaultFontName))
                    select(FontSettings.DefaultFontName);
                list.GapLine();
                if (favorites.Length > 0)
                {
                    GUI.Label(list.GetRect(24f), "CustomFonts.Favorites".Translate(), PreviewProgressStyle);
                    Choices(favorites); list.GapLine();
                }
                Choices(bundled); list.GapLine();
                Choices(installed);
            }
            finally { list.End(); Widgets.EndScrollView(); }
        }
    }
}
