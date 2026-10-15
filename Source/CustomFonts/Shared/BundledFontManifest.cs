using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace CustomFonts
{
    internal static class BundledFontManifest
    {
        internal static BundledFontData[] Read(string path)
        {
            try
            {
                using (var stream = typeof(BundledFontManifest).Assembly.GetManifestResourceStream("CustomFonts.BundledFonts.catalog"))
                {
                    if (stream == null) return null;
                    using (var reader = new BinaryReader(stream, Encoding.UTF8)) return Read(reader, path);
                }
            }
            catch { return null; }
        }

        private static BundledFontData[] Read(BinaryReader reader, string path)
        {
            var file = new FileInfo(path);
            if (reader.ReadInt32() != 1 || reader.ReadInt64() != file.Length) return null;
            byte[] expected = reader.ReadBytes(32);
            using (var source = File.OpenRead(path))
            using (var sha = SHA256.Create())
                if (!expected.SequenceEqual(sha.ComputeHash(source))) return null;
            int count = reader.ReadInt32();
            if (count < 1 || count > 1024) throw new InvalidDataException("Invalid font manifest.");
            var entries = new BundledFontData[count];
            for (int i = 0; i < count; i++)
            {
                var entry = new BundledFontData { Name = reader.ReadString(), FaceName = reader.ReadString(), Size = reader.ReadSingle(),
                    RenderingMode = reader.ReadInt32(), AssetsIndex = reader.ReadInt32(), DataOffset = reader.ReadInt64(), DataLength = reader.ReadInt32(),
                    BundleLength = file.Length, BundleModified = file.LastWriteTimeUtc };
                if (string.IsNullOrWhiteSpace(entry.Name) || string.IsNullOrWhiteSpace(entry.FaceName) || entry.AssetsIndex < 0
                    || entry.DataOffset < 0 || entry.DataLength < 1 || entry.DataLength > 128 * 1024 * 1024) throw new InvalidDataException("Invalid font manifest.");
                entries[i] = entry;
            }
            if (reader.BaseStream.Position != reader.BaseStream.Length) throw new InvalidDataException("Invalid font manifest.");
            return entries;
        }

        // Build-time only. The original bundle remains byte-for-byte unchanged.
        internal static void Write(string path, string output)
        {
            if (File.Exists(output))
            {
                try { using (var reader = new BinaryReader(File.OpenRead(output))) if (Read(reader, path) != null) return; }
                catch { }
            }
            var entries = BundledFontReader.Read(path).ToArray();
            using (var writer = new BinaryWriter(File.Create(output), Encoding.UTF8))
            {
                writer.Write(1); writer.Write(new FileInfo(path).Length);
                using (var source = File.OpenRead(path))
                using (var sha = SHA256.Create()) writer.Write(sha.ComputeHash(source));
                writer.Write(entries.Length);
                foreach (var entry in entries)
                {
                    writer.Write(entry.Name); writer.Write(entry.FaceName); writer.Write(entry.Size); writer.Write(entry.RenderingMode);
                    writer.Write(entry.AssetsIndex); writer.Write(entry.DataOffset); writer.Write(entry.DataLength);
                }
            }
        }
    }
}
