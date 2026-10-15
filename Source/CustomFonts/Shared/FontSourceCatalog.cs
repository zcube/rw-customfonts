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
            if (!IsLegacyFile(selection)) return Family(selection);
            int at = selection.LastIndexOf(" [", StringComparison.Ordinal);
            string face = at >= 7 && selection.EndsWith("]", StringComparison.Ordinal)
                ? selection.Substring(7, at - 7) : selection.Substring(7);
            return Family(face);
        }

        private static int StyleRank(string face)
        {
            string family = Family(face);
            string style = face.Length > family.Length ? face.Substring(family.Length + 2).TrimEnd(')') : "Regular";
            return new[] { "Regular", "Normal", "Roman", "Book" }.Any(s => string.Equals(s, style, StringComparison.OrdinalIgnoreCase)) ? 0 : 1;
        }

        internal static string FindKey(IDictionary<string, string> catalog, string selection)
        {
            if (selection == null) return null;
            string exact = catalog.Keys.FirstOrDefault(k => string.Equals(k, selection, StringComparison.OrdinalIgnoreCase));
            if (exact != null) return exact;
            string family = SelectionFamily(selection);
            string key = catalog.Keys.FirstOrDefault(k => string.Equals(k, family, StringComparison.OrdinalIgnoreCase));
            if (key != null) return key;
            if (IsLegacyFile(selection))
            {
                // Compatibility for old engine-generated names. Paths are considered
                // only among configured candidates, never opened from saved selections.
                var comparison = Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                return catalog.FirstOrDefault(p => selection.EndsWith(" [" + p.Value + "]", comparison)).Key;
            }
            return null;
        }

        internal static string DisplayName(string name)
        {
            return IsLegacyFile(name) ? SelectionFamily(name) : name;
        }

        internal static string PortableSelection(IDictionary<string, string> catalog, string selection)
            => FindKey(catalog, selection) ?? (IsLegacyFile(selection) ? SelectionFamily(selection) : selection);

        internal static void Populate(IDictionary<string, string> catalog, IEnumerable<string> systemFiles,
            IEnumerable<string> sources, string modRoot, bool recursive, Func<string, string> readName,
            Action<string, Exception> error)
        {
            var seen = new HashSet<string>(PathComparer);
            var ranks = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var families = catalog.Keys.ToDictionary(k => k, k => k, StringComparer.OrdinalIgnoreCase);
            void Add(string file)
            {
                try
                {
                    file = Path.GetFullPath(file);
                    if (!seen.Add(file)) return;
                    string name = readName(file);
                    if (string.IsNullOrWhiteSpace(name)) throw new InvalidDataException("Font has no face name.");
                    string family = Family(name);
                    if (!families.TryGetValue(family, out string key)) families.Add(family, key = family);
                    int rank = StyleRank(name);
                    if (!catalog.ContainsKey(key) || (ranks.TryGetValue(key, out int previous) && rank < previous))
                    { catalog[key] = file; ranks[key] = rank; }
                }
                catch (Exception ex) { error(file, ex); }
            }
            // Regular faces take precedence; equally suitable configured faces precede
            // OS faces. Source order and sorted paths break remaining ties deterministically.
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
