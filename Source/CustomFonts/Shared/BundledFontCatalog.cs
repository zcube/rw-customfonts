using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace CustomFonts
{
    internal sealed class BundledFontData
    {
        internal string Name, FaceName, Path, Sha256;
        internal float Size;
        internal int RenderingMode, Length;
    }

    // Only the small embedded index is read at startup. Selected primary/fallback
    // sources use FontFiles just like OS and user fonts; there is no extraction cache.
    internal static class BundledFontCatalog
    {
        internal static IEnumerable<BundledFontData> Read(string directory)
        {
            using (var stream = typeof(BundledFontCatalog).Assembly.GetManifestResourceStream("CustomFonts.BundledFonts.tsv"))
            {
                if (stream == null) throw new InvalidDataException("Missing bundled font index.");
                using (var reader = new StreamReader(stream))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                        string[] fields = line.Split('\t');
                        if (fields.Length != 6 || fields[0] != System.IO.Path.GetFileName(fields[0]) || !fields[0].EndsWith(".ttf", StringComparison.Ordinal))
                            throw new InvalidDataException("Invalid bundled font index.");
                        string path = System.IO.Path.Combine(directory, fields[0]);
                        if (!File.Exists(path)) continue;
                        yield return new BundledFontData
                        {
                            Name = System.IO.Path.GetFileNameWithoutExtension(fields[0]), FaceName = fields[1], Path = path,
                            Size = float.Parse(fields[2], CultureInfo.InvariantCulture), RenderingMode = int.Parse(fields[3], CultureInfo.InvariantCulture),
                            Length = int.Parse(fields[4], CultureInfo.InvariantCulture), Sha256 = fields[5]
                        };
                    }
                }
            }
        }
    }
}
