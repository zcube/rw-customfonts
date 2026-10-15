using System;
using System.Collections.Generic;

namespace CustomFonts
{
    internal static class AdditionalFontRoots
    {
        internal static IEnumerable<string> Get(bool linux, Func<string, string> environment)
        {
            if (!linux) yield break;
            // Supplement the shared default roots with configured Linux XDG locations.
            string home = environment("XDG_DATA_HOME");
            if (!string.IsNullOrEmpty(home) && home.StartsWith("/", StringComparison.Ordinal))
                yield return home.TrimEnd('/') + "/fonts";
            string dirs = environment("XDG_DATA_DIRS");
            if (string.IsNullOrEmpty(dirs)) yield break;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string directory in dirs.Split(':'))
            {
                if (!directory.StartsWith("/", StringComparison.Ordinal)) continue;
                string root = directory.TrimEnd('/') + "/fonts";
                if (seen.Add(root)) yield return root;
            }
        }
    }
}
