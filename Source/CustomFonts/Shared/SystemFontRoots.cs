using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CustomFonts
{
    internal enum FontPlatform { Windows, MacOS, Linux }

    internal static class SystemFontRoots
    {
        internal static IEnumerable<string> Get(FontPlatform platform, string home, string windows,
            string localAppData, Func<string, string> environment)
        {
            var roots = new List<string>();
            if (platform == FontPlatform.Windows)
            {
                if (!string.IsNullOrEmpty(windows)) roots.Add(Path.Combine(windows, "Fonts"));
                if (!string.IsNullOrEmpty(localAppData)) roots.Add(Path.Combine(localAppData, "Microsoft", "Windows", "Fonts"));
            }
            else if (platform == FontPlatform.MacOS)
            {
                roots.Add("/System/Library/Fonts");
                roots.Add("/Library/Fonts");
                if (!string.IsNullOrEmpty(home)) roots.Add(Path.Combine(home, "Library", "Fonts"));
            }
            else
            {
                roots.Add("/usr/share/fonts");
                roots.Add("/usr/local/share/fonts");
                if (!string.IsNullOrEmpty(home))
                {
                    roots.Add(Path.Combine(home, ".fonts"));
                    roots.Add(Path.Combine(home, ".local", "share", "fonts"));
                }
                // Flatpak/Steam Deck expose host fonts here. The shared file loader
                // discovers them directly; neither engine requires a binary patch.
                roots.Add("/run/host/fonts");
                roots.Add("/run/host/user-fonts");
                roots.AddRange(AdditionalFontRoots.Get(true, environment));
            }
            return roots.Distinct(FontSourceCatalog.PathComparer);
        }
    }
}
