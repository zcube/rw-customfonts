using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CustomFonts
{
    internal static class FontSourceCatalog
    {
        internal static StringComparer PathComparer => Path.DirectorySeparatorChar == '\\'
            ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        private static bool IsLegacyFile(string name) => name != null && name.StartsWith("(File) ", StringComparison.Ordinal);

        // Metadata names always end with the OpenType style. Family names themselves
        // may contain parentheses; remove only that final, generated style suffix.
        internal static string Family(string face)
        {
            if (face == null) return null;
            int at = face.LastIndexOf(" (", StringComparison.Ordinal);
            return at >= 0 && face.EndsWith(")", StringComparison.Ordinal) ? face.Substring(0, at) : face;
        }

        internal static string SelectionFamily(string selection)
        {
            return Family(SelectionFace(selection));
        }

        private static string SelectionFace(string selection)
        {
            if (!IsLegacyFile(selection)) return selection;
            int at = selection.LastIndexOf(" [", StringComparison.Ordinal);
            string face = at >= 7 && selection.EndsWith("]", StringComparison.Ordinal)
                ? selection.Substring(7, at - 7) : selection.Substring(7);
            return face;
        }

        internal static string Style(string face)
        {
            string family = Family(face);
            return face != null && family != face ? face.Substring(family.Length + 2, face.Length - family.Length - 3) : "Regular";
        }

        private static int StyleRank(string face)
        {
            string style = Style(face);
            var defaults = new[] { "Regular", "Normal", "Roman", "Book" };
            for (int i = 0; i < defaults.Length; i++)
                if (string.Equals(defaults[i], style, StringComparison.OrdinalIgnoreCase)) return i;
            return defaults.Length;
        }

        internal static string FindKey(IDictionary<string, string> catalog, string selection)
        {
            if (selection == null) return null;
            string exact = catalog.Keys.FirstOrDefault(k => string.Equals(k, selection, StringComparison.OrdinalIgnoreCase));
            if (exact != null) return exact;
            string face = SelectionFace(selection);
            string key = catalog.Keys.FirstOrDefault(k => string.Equals(k, face, StringComparison.OrdinalIgnoreCase));
            if (key != null) return key;
            if (IsLegacyFile(selection) && Family(face) == face)
            {
                // Compatibility for old engine-generated names. Paths are considered
                // only among configured candidates, never opened from saved selections.
                var comparison = Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                key = catalog.FirstOrDefault(p => selection.EndsWith(" [" + p.Value + "]", comparison)).Key;
                if (key != null) return key;
            }
            // Old selections contain only a family. Resolve those deterministically,
            // but never turn an unavailable explicit style into another face.
            return catalog.Keys.Where(k => string.Equals(Family(k), face, StringComparison.OrdinalIgnoreCase))
                .OrderBy(StyleRank).ThenBy(k => k, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
        }

        internal static bool MatchesStyle(string name, string style)
            => string.IsNullOrEmpty(style) || string.Equals(Style(name), style, StringComparison.OrdinalIgnoreCase);

        internal static string[] Styles(IEnumerable<string> names) => names.Select(Style)
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => StyleRank("Font (" + s + ")"))
            .ThenBy(s => s, StringComparer.OrdinalIgnoreCase).ToArray();

        internal static bool MatchesSearch(string selection, string display, string query)
            => string.IsNullOrEmpty(query)
                || (display != null && display.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                || (selection != null && selection.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);

        // RimWorld language folders are independent of the OS/.NET current culture.
        internal static int DisplayLanguage(string folder)
        {
            // Installed folders include native labels, e.g. "Korean (한국어)".
            folder = (folder ?? "English").Split('(')[0].Trim();
            switch (folder)
            {
                case "Korean": return 0x412;
                case "Japanese": return 0x411;
                case "ChineseSimplified": return 0x804;
                case "ChineseTraditional": return 0x404;
                case "PortugueseBrazilian": return 0x416;
                case "Portuguese": return 0x816;
                case "SpanishLatin": return 0x80a;
                case "Spanish": return 0xc0a;
                case "English": return 0x409;
            }
            // Most other game folders use the English culture name (German, French,
            // Russian, etc.). Resolve deterministically without consulting OS culture.
            foreach (var culture in System.Globalization.CultureInfo.GetCultures(System.Globalization.CultureTypes.SpecificCultures)
                .OrderBy(c => c.LCID))
                if (string.Equals(culture.EnglishName.Split('(')[0].Trim().Replace(" ", ""), folder,
                    StringComparison.OrdinalIgnoreCase)) return culture.LCID;
            return 0x409;
        }

        internal static string DisplayName(string name)
        {
            string face = SelectionFace(name);
            if (face == null) return null;
            if (face.StartsWith("(Bundled) ", StringComparison.Ordinal))
            {
                face = face.Substring(10);
                if (face.StartsWith("[", StringComparison.Ordinal))
                {
                    int end = face.IndexOf("] ", StringComparison.Ordinal);
                    if (end >= 0) face = face.Substring(end + 2);
                }
            }
            return face.EndsWith(" (Regular)", StringComparison.OrdinalIgnoreCase)
                ? face.Substring(0, face.Length - 10) : face;
        }

        internal static string PortableSelection(IDictionary<string, string> catalog, string selection)
            => FindKey(catalog, selection) ?? SelectionFace(selection);

        internal static void Populate(IDictionary<string, string> catalog, IEnumerable<string> systemFiles,
            IEnumerable<string> sources, string modRoot, bool recursive, Func<string, string> readName,
            Action<string, Exception> error)
        {
            var seen = new HashSet<string>(PathComparer);
            var faces = new HashSet<string>(catalog.Keys, StringComparer.OrdinalIgnoreCase);
            void Add(string file)
            {
                try
                {
                    file = Path.GetFullPath(file);
                    if (!seen.Add(file)) return;
                    string name = readName(file);
                    if (string.IsNullOrWhiteSpace(name)) throw new InvalidDataException("Font has no face name.");
                    if (faces.Add(name)) catalog[name] = file;
                }
                catch (Exception ex) { error(file, ex); }
            }
            // Keep every family/style. For duplicate faces, configured sources precede
            // OS files; source order and sorted paths break remaining ties.
            foreach (string source in sources)
            {
                try
                {
                    string path = FontFiles.Normalize(source, modRoot);
                    foreach (string file in FontFiles.Enumerate(path, recursive).Where(FontFiles.Supported)
                        .OrderBy(x => x, StringComparer.Ordinal)) Add(file);
                }
                catch (Exception ex) { error(source, ex); }
            }
            foreach (string file in systemFiles.OrderBy(x => x, StringComparer.Ordinal)) Add(file);
        }
    }
}
