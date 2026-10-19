using System;
using System.Linq;
using UnityEngine;
using Verse;

namespace CustomFonts
{
    public partial class CustomFonts
    {
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
            string[] Filter(string[] names) => names.Where(name => FontSourceCatalog.MatchesSearch(name, DisplayFontName(name), searchText)).ToArray();
            var favorites = Filter(groups.Favorites);
            var bundled = Filter(groups.Bundled);
            var installed = Filter(groups.Installed);
            var outer = new Rect(inRect.x, inRect.y, inRect.width - 10f, inRect.height);
            float width = Math.Max(1f, outer.width - 24f);
            float rowHeight = FontChoiceHeight(FontSettings.DefaultFontName, width);
            float contentHeight = rowHeight * (1 + favorites.Length + bundled.Length + installed.Length) + 24f
                + (favorites.Length > 0 ? 36f : 0f);
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
