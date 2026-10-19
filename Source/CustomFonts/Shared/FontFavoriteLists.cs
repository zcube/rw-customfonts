using System;
using System.Collections.Generic;
using System.Linq;

namespace CustomFonts
{
    internal sealed class FontChoiceGroups
    {
        internal string[] Favorites, Bundled, Installed;
    }
    internal static class FontFavoriteLists
    {
        internal static List<string> Normalize(IEnumerable<string> names) => (names ?? Enumerable.Empty<string>())
            .Where(n => !string.IsNullOrWhiteSpace(n) && !string.Equals(n, "Default", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        internal static List<string> Toggle(IEnumerable<string> names, string name)
        {
            var result = Normalize(names);
            if (string.IsNullOrWhiteSpace(name) || string.Equals(name, "Default", StringComparison.OrdinalIgnoreCase)) return result;
            int at = result.FindIndex(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
            if (at >= 0) result.RemoveAt(at); else result.Add(name);
            return result;
        }
        internal static FontChoiceGroups Create(IEnumerable<string> favorites, IEnumerable<string> bundled, IEnumerable<string> installed)
        {
            var bundles = bundled.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToArray();
            var files = installed.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToArray();
            var available = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string name in bundles.Concat(files)) if (!available.ContainsKey(name)) available.Add(name, name);
            var selected = Normalize(favorites).Where(available.ContainsKey).Select(n => available[n]).ToArray();
            var chosen = new HashSet<string>(selected, StringComparer.OrdinalIgnoreCase);
            return new FontChoiceGroups {
                Favorites = selected,
                Bundled = bundles.Where(n => !chosen.Contains(n)).ToArray(),
                Installed = files.Where(n => !chosen.Contains(n)).ToArray()
            };
        }
    }
}
