using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace CustomFonts
{
    internal struct FontScanLocation
    {
        internal readonly string Identity, Path;
        internal readonly bool Recursive;
        internal FontScanLocation(string path, bool recursive) : this(path, path, recursive) { }
        internal FontScanLocation(string identity, string path, bool recursive)
        { Identity = identity; Path = path; Recursive = recursive; }
    }

    // Metadata-only: never open font payloads or create native fonts just to check
    // whether a cached catalog is still current. Preserve source precedence/order.
    internal static class FontSourceSnapshot
    {
        internal static string Capture(IEnumerable<FontScanLocation> locations)
        {
            using (var bytes = new MemoryStream())
            {
                using (var writer = new BinaryWriter(bytes, Encoding.UTF8, true))
                    foreach (var location in locations)
                    {
                        writer.Write(location.Identity ?? ""); writer.Write(location.Path ?? "");
                        writer.Write(location.Recursive);
                        try
                        {
                            if (location.Path == null) { writer.Write("unresolved"); continue; }
                            var files = FontFiles.Enumerate(location.Path, location.Recursive)
                                .Where(FontFiles.Supported).Distinct(FontSourceCatalog.PathComparer)
                                .OrderBy(path => path, FontSourceCatalog.PathComparer).ToArray();
                            writer.Write("files"); writer.Write(files.Length);
                            foreach (string path in files)
                            {
                                writer.Write(path);
                                try
                                {
                                    var file = new FileInfo(path);
                                    writer.Write(file.Exists);
                                    if (file.Exists) { writer.Write(file.Length); writer.Write(file.LastWriteTimeUtc.Ticks); }
                                }
                                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                                { writer.Write(ex.GetType().FullName); }
                            }
                        }
                        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException
                            || ex is ArgumentException || ex is NotSupportedException)
                        { writer.Write(ex.GetType().FullName); }
                    }
                using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes.ToArray()));
            }
        }
    }
}
