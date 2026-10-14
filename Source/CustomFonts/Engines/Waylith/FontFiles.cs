using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace CustomFonts
{
    internal static class FontFiles
    {
        internal static string Normalize(string path, string modRoot)
        {
            path = ExpandVariables(path.Trim().Trim('"'), Environment.GetEnvironmentVariable);
            if (path == "~" || path.StartsWith("~/") || path.StartsWith("~\\"))
                path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path.Length > 1 ? path.Substring(2) : "");
            return Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(modRoot, path));
        }

        internal static string ExpandVariables(string path, Func<string, string> environment)
        {
            // Expand once: a dollar/percent in the variable's value is literal path text.
            return Regex.Replace(path, @"%([A-Za-z_][A-Za-z0-9_]*)%|\$\{([A-Za-z_][A-Za-z0-9_]*)\}|\$([A-Za-z_][A-Za-z0-9_]*)",
                match => environment(match.Groups[1].Success ? match.Groups[1].Value
                    : match.Groups[2].Success ? match.Groups[2].Value : match.Groups[3].Value) ?? match.Value);
        }

        internal static bool Supported(string path)
        {
            string ext = Path.GetExtension(path);
            return ext.Equals(".ttf", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".otf", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".ttc", StringComparison.OrdinalIgnoreCase);
        }

        internal static IEnumerable<string> Enumerate(string path, bool recursive)
        {
            if (File.Exists(path))
            {
                if (!Supported(path)) throw new InvalidDataException("Expected a .ttf, .otf or .ttc file.");
                return new[] { path };
            }
            if (!Directory.Exists(path)) throw new DirectoryNotFoundException(path);
            return EnumerateFolder(path, recursive);
        }

        private static IEnumerable<string> EnumerateFolder(string path, bool recursive)
        {
            var pending = new Stack<string>();
            pending.Push(path);
            var options = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                AttributesToSkip = 0,
                ReturnSpecialDirectories = false
            };
            while (pending.Count > 0)
            {
                string folder = pending.Pop();
                // Font file symlinks are valid. Only directory links are skipped to avoid loops.
                foreach (string file in Directory.EnumerateFiles(folder, "*", options)) yield return file;
                if (!recursive) continue;
                var directories = new EnumerationOptions
                {
                    IgnoreInaccessible = true,
                    AttributesToSkip = FileAttributes.ReparsePoint,
                    ReturnSpecialDirectories = false
                };
                foreach (string directory in Directory.EnumerateDirectories(folder, "*", directories)) pending.Push(directory);
            }
        }

        internal static byte[] Read(string path)
        {
            using var stream = File.OpenRead(path);
            if (stream.Length > 128 * 1024 * 1024) throw new InvalidDataException("Font exceeds 128 MiB.");
            byte[] data = new byte[checked((int)stream.Length)];
            stream.ReadExactly(data);
            return data;
        }

        internal static string ReadName(string path)
        {
            using var stream = File.OpenRead(path);
            if (stream.Length > 128 * 1024 * 1024) throw new InvalidDataException("Font exceeds 128 MiB.");
            return OpenTypeNames.Read(stream, 0, stream.Length);
        }
    }
}
