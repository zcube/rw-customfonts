using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace CustomFonts
{
    internal static partial class FontFiles
    {
        internal static string Normalize(string path, string modRoot)
        {
            path = ExpandVariables(path.Trim().Trim('"'), Environment.GetEnvironmentVariable);
            path = NormalizeSeparators(path, Path.DirectorySeparatorChar == '\\');
            if (path == "~" || path.StartsWith("~/") || path.StartsWith("~\\"))
                path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path.Length > 1 ? path.Substring(2) : "");
            path = Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(modRoot, path));
            return path.Length > Path.GetPathRoot(path).Length ? path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) : path;
        }

        internal static string NormalizeSeparators(string path, bool windows)
        {
            // Shared settings can contain absolute locations from several machines.
            // Do not reinterpret C:\\Fonts as a Unix relative path, or /usr/share/fonts
            // as a directory on the current Windows drive. Relative paths and ~/paths
            // accept either separator on every platform.
            bool drive = path.Length >= 2 && ((path[0] >= 'A' && path[0] <= 'Z')
                || (path[0] >= 'a' && path[0] <= 'z')) && path[1] == ':';
            bool network = path.StartsWith("\\\\", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal);
            bool unixRoot = path.StartsWith("/", StringComparison.Ordinal) && !network;
            bool windowsRoot = path.StartsWith("\\", StringComparison.Ordinal);
            if ((windows && unixRoot) || (!windows && (drive || network || windowsRoot)))
                throw new NotSupportedException("Font path belongs to another operating system: " + path);
            if (drive && (path.Length < 3 || (path[2] != '/' && path[2] != '\\')))
                throw new ArgumentException("Font drive paths must be absolute: " + path);
            return windows ? path.Replace('/', '\\') : path.Replace('\\', '/');
        }

        internal static bool SameLocation(string first, string second, string modRoot)
        {
            try { return FontSourceCatalog.PathComparer.Equals(Normalize(first, modRoot), Normalize(second, modRoot)); }
            catch (Exception) { return string.Equals(first, second, StringComparison.Ordinal); }
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
            while (pending.Count > 0)
            {
                string folder = pending.Pop();
                string[] files, directories;
                try { files = Directory.GetFiles(folder); }
                catch (UnauthorizedAccessException) { continue; }
                catch (IOException) { continue; }
                // Font file symlinks are valid. Only directory links are skipped to avoid loops.
                foreach (string file in files) yield return file;
                if (!recursive) continue;
                try { directories = Directory.GetDirectories(folder); }
                catch (UnauthorizedAccessException) { continue; }
                catch (IOException) { continue; }
                foreach (string directory in directories)
                {
                    try
                    {
                        if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) == 0) pending.Push(directory);
                    }
                    catch (UnauthorizedAccessException) { }
                    catch (IOException) { }
                }
            }
        }

        internal static byte[] Read(string path)
        {
            using (var stream = File.OpenRead(path))
            {
                if (stream.Length > 128 * 1024 * 1024) throw new InvalidDataException("Font exceeds 128 MiB.");
                byte[] data = new byte[checked((int)stream.Length)];
                ReadExactly(stream, data);
                return data;
            }
        }

        internal static void ReadExactly(Stream stream, byte[] data)
        {
            int read = 0;
            while (read < data.Length)
            {
                int count = stream.Read(data, read, data.Length - read);
                if (count == 0) throw new EndOfStreamException();
                read += count;
            }
        }
    }
}
