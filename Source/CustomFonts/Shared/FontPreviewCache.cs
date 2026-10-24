using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace CustomFonts
{
    // Only disposable, derived alpha masks live here. Never serialize a native font,
    // atlas pointer or texture. Bounds apply before allocating or decompressing data.
    internal sealed class FontPreviewImage
    {
        internal const int DefaultPixelSize = 13, MinPixelSize = 12, MaxPixelSize = 40, MaxWidth = 768, MaxHeight = 2048;
        internal static int ClampSize(int pixels) => Math.Max(MinPixelSize, Math.Min(MaxPixelSize, pixels));
        internal static int HeightFor(int pixels) => ClampSize(pixels) * 2;
        internal int Width, Height = HeightFor(DefaultPixelSize);
        internal byte[] Alpha;
    }

    internal sealed class FontPreviewCache
    {
        private readonly string _root;
        internal FontPreviewCache(string root)
        {
            _root = root;
            // Disk holds the entire visited catalog. Do not scan, evict or impose
            // a capacity limit: memory residency is controlled independently.
            try { Directory.CreateDirectory(root); } catch { }
        }
        internal static string Key(string path, long length, long modified, string language, string label, string engine,
            int pixelSize = FontPreviewImage.DefaultPixelSize, int wrapWidth = 0)
        {
            string identity = "preview-v5-centered\n" + engine + "\n" + path + "\n" + length + "\n" + modified
                + "\n" + language + "\n" + label + "\n" + FontPreviewImage.ClampSize(pixelSize)
                + "\n" + Math.Max(0, Math.Min(FontPreviewImage.MaxWidth, wrapWidth));
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(identity))).Replace("-", "");
        }
        private string PathFor(string key)
        {
            if (key == null || key.Length != 64 || key.Any(c => !Uri.IsHexDigit(c)))
                throw new ArgumentException("Invalid preview cache key.");
            return Path.Combine(_root, key + ".preview");
        }
        internal FontPreviewImage Read(string key)
        {
            try
            {
                string path = PathFor(key);
                if (new FileInfo(path).Length > FontPreviewImage.MaxWidth * FontPreviewImage.MaxHeight + 1024) return null;
                using (var stream = File.OpenRead(path))
                using (var reader = new BinaryReader(stream))
                {
                    if (reader.ReadInt32() != 1) return null;
                    int width = reader.ReadInt32(), height = reader.ReadInt32();
                    if (width < 1 || width > FontPreviewImage.MaxWidth || (height < FontPreviewImage.HeightFor(FontPreviewImage.MinPixelSize) || height > FontPreviewImage.MaxHeight)) return null;
                    var alpha = new byte[width * height];
                    using (var zip = new DeflateStream(stream, CompressionMode.Decompress))
                    {
                        FontFiles.ReadExactly(zip, alpha);
                        if (zip.ReadByte() != -1) return null;
                    }
                    return new FontPreviewImage { Width = width, Height = height, Alpha = alpha };
                }
            }
            catch { return null; }
        }
        internal void Clear()
        {
            // This directory contains only derived previews, across every locale and engine.
            // Keep font metadata and fallback files in the parent cache directory intact.
            if (!Directory.Exists(_root)) return;
            foreach (string path in Directory.EnumerateFiles(_root, "*.preview")) File.Delete(path);
            foreach (string path in Directory.EnumerateFiles(_root, "*.preview.tmp")) File.Delete(path);
        }
        internal void Write(string key, FontPreviewImage image)
        {
            string temp = null;
            try
            {
                if (image.Width < 1 || image.Width > FontPreviewImage.MaxWidth
                    || image.Height < FontPreviewImage.HeightFor(FontPreviewImage.MinPixelSize) || image.Height > FontPreviewImage.MaxHeight
                    || image.Alpha.Length != image.Width * image.Height) return;
                string path = PathFor(key);
                temp = path + ".tmp";
                using (var stream = File.Create(temp))
                {
                    using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
                    { writer.Write(1); writer.Write(image.Width); writer.Write(image.Height); }
                    using (var zip = new DeflateStream(stream, CompressionMode.Compress))
                        zip.Write(image.Alpha, 0, image.Alpha.Length);
                }
                if (File.Exists(path)) File.Delete(path);
                File.Move(temp, path);
            }
            catch { }
            finally { try { if (temp != null && File.Exists(temp)) File.Delete(temp); } catch { } }
        }
    }
}
