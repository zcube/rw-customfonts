using System.IO;

namespace CustomFonts
{
    internal static partial class FontFiles
    {
        internal static string ReadName(string path)
        {
            using (var stream = File.OpenRead(path))
            {
                if (stream.Length > 128 * 1024 * 1024) throw new InvalidDataException("Font exceeds 128 MiB.");
                return OpenTypeNames.Read(stream, 0, stream.Length);
            }
        }
    }
}
