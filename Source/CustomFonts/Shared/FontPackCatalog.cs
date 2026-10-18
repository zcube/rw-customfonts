using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CustomFonts
{
    internal sealed class FontPackSource
    {
        internal string PackageId, Name;
        // RimWorld content roots, highest priority first (LoadFolders.xml aware).
        internal IEnumerable<string> ContentRoots;
    }

    internal static class FontPackCatalog
    {
        internal static IEnumerable<BundledFontData> Read(IEnumerable<FontPackSource> packs,
            Func<string, string> readName, Action<string, Exception> error)
        {
            var selections = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pack in packs)
            {
                var relativeFiles = new HashSet<string>(FontSourceCatalog.PathComparer);
                foreach (string root in pack.ContentRoots)
                {
                    string folder = Path.Combine(root, "Fonts");
                    if (!Directory.Exists(folder)) continue;
                    string[] files;
                    try { files = FontFiles.Enumerate(folder, true).Where(FontFiles.Supported).OrderBy(p => p, StringComparer.Ordinal).ToArray(); }
                    catch (Exception ex) { error(folder, ex); continue; }
                    foreach (string file in files)
                    {
                        // A version-specific file shadows the same relative path in a lower root.
                        if (!relativeFiles.Add(file.Substring(folder.Length + 1))) continue;
                        BundledFontData data;
                        try
                        {
                            string face = readName(file);
                            if (string.IsNullOrWhiteSpace(face)) throw new InvalidDataException("Font has no face name.");
                            string key = "(Bundled) [" + pack.PackageId.ToLowerInvariant() + "] " + face;
                            if (!selections.Add(key)) continue;
                            data = new BundledFontData
                            {
                                Name = Path.GetFileNameWithoutExtension(file), FaceName = face, Path = file,
                                SelectionKey = key, SourceModName = pack.Name, Size = 16, RenderingMode = 0
                            };
                        }
                        catch (Exception ex) { error(file, ex); continue; }
                        yield return data;
                    }
                }
            }
        }
    }
}
