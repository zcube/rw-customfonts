using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CustomFonts
{
    internal enum FontTarget { Tiny, Small, Medium, World }

    internal static class FontFallbackLists
    {
        internal static List<string> Normalize(IEnumerable<string> names) => (names ?? Enumerable.Empty<string>())
            .Where(n => !string.IsNullOrWhiteSpace(n) && n != "Default").Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        internal static List<string> Migrate(IEnumerable<string> current, IEnumerable<string> legacy)
            => Normalize(current ?? legacy);
        internal static List<string> Union(IEnumerable<string> current, params IEnumerable<string>[] others)
            => Normalize((current ?? Enumerable.Empty<string>()).Concat(others.SelectMany(n => n ?? Enumerable.Empty<string>())));
        internal static string NamesKey(IEnumerable<string> names)
            => string.Concat(names.Select(n => n.Length + ":" + n));
        internal static string PolicyKey(IEnumerable<FallbackFontSource> sources)
        {
            string revisions = string.Join("/", sources.Select(s => s.Revision).ToArray());
            return revisions.Length == 0 ? "" : FallbackFontSource.Hash(Encoding.UTF8.GetBytes(revisions));
        }
    }
}
