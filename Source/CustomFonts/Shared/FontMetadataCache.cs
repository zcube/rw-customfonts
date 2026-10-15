using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace CustomFonts
{
    // Disk cache contains only derived data. Failure/corruption always falls back to the source.
    internal sealed class FontMetadataCache
    {
        private sealed class Entry { internal long Length, Modified; internal string Name; }
        private readonly Dictionary<string, Entry> _names = new Dictionary<string, Entry>(FontSourceCatalog.PathComparer);
        private readonly string _root;
        private bool _dirty;
        internal int Hits, Misses;

        internal FontMetadataCache(string root)
        {
            _root = root;
            try
            {
                string path = Path.Combine(root, "names-v1.bin");
                if (new FileInfo(path).Length > 16 * 1024 * 1024) return;
                using (var reader = new BinaryReader(File.OpenRead(path), Encoding.UTF8))
                {
                    if (reader.ReadInt32() != 1) return;
                    int count = reader.ReadInt32();
                    if (count < 0 || count > 100000) return;
                    var entries = new Dictionary<string, Entry>(FontSourceCatalog.PathComparer);
                    for (int i = 0; i < count; i++) entries.Add(reader.ReadString(), new Entry
                    { Length = reader.ReadInt64(), Modified = reader.ReadInt64(), Name = reader.ReadString() });
                    if (reader.BaseStream.Position != reader.BaseStream.Length) return;
                    foreach (var pair in entries) _names.Add(pair.Key, pair.Value);
                }
            }
            catch { _names.Clear(); }
        }

        internal string ReadName(string path)
        {
            path = Path.GetFullPath(path);
            var file = new FileInfo(path);
            if (_names.TryGetValue(path, out Entry entry) && entry.Length == file.Length && entry.Modified == file.LastWriteTimeUtc.Ticks)
            { Hits++; return entry.Name; }
            Misses++;
            string name = FontFiles.ReadName(path);
            _names[path] = new Entry { Length = file.Length, Modified = file.LastWriteTimeUtc.Ticks, Name = name };
            _dirty = true;
            return name;
        }

        internal void Save()
        {
            if (!_dirty) return;
            Write(Path.Combine(_root, "names-v1.bin"), stream =>
            {
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
                {
                    writer.Write(1); writer.Write(_names.Count);
                    foreach (var pair in _names)
                    { writer.Write(pair.Key); writer.Write(pair.Value.Length); writer.Write(pair.Value.Modified); writer.Write(pair.Value.Name); }
                }
            });
            _dirty = false;
        }

        private string BundlePath(string path)
        {
            using (var sha = SHA256.Create())
                return Path.Combine(_root, "bundle-" + BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(Path.GetFullPath(path)))).Replace("-", ""));
        }

        internal BundledFontData[] ReadBundle(string path, out bool hit, IEnumerable<string> selected = null)
        {
            string cached = BundlePath(path) + ".bin";
            var file = new FileInfo(path);
            hit = false;
            try
            {
                if (new FileInfo(cached).Length > 1024 * 1024) throw new InvalidDataException();
                using (var reader = new BinaryReader(File.OpenRead(cached), Encoding.UTF8))
                {
                    if (reader.ReadInt32() != 1 || reader.ReadInt64() != file.Length || reader.ReadInt64() != file.LastWriteTimeUtc.Ticks)
                        throw new InvalidDataException();
                    int count = reader.ReadInt32();
                    if (count < 1 || count > 1024) throw new InvalidDataException();
                    var entries = new BundledFontData[count];
                    for (int i = 0; i < count; i++)
                    {
                        var entry = new BundledFontData { Name = reader.ReadString(), FaceName = reader.ReadString(), Size = reader.ReadSingle(),
                            RenderingMode = reader.ReadInt32(), AssetsIndex = reader.ReadInt32(), DataOffset = reader.ReadInt64(),
                            DataLength = reader.ReadInt32(), BundleLength = file.Length, BundleModified = file.LastWriteTimeUtc };
                        if (string.IsNullOrWhiteSpace(entry.Name) || string.IsNullOrWhiteSpace(entry.FaceName) || entry.AssetsIndex < 0
                            || entry.DataOffset < 0 || entry.DataLength < 1 || entry.DataLength > 128 * 1024 * 1024) throw new InvalidDataException();
                        entries[i] = entry;
                    }
                    if (reader.BaseStream.Position != reader.BaseStream.Length) throw new InvalidDataException();
                    hit = true;
                    return entries;
                }
            }
            catch { }
            var selections = new HashSet<string>(selected ?? new string[0], StringComparer.Ordinal);
            var result = BundledFontManifest.Read(path) ?? new List<BundledFontData>(BundledFontReader.Read(path,
                entry => selections.Contains("(Bundled) " + entry.FaceName),
                (entry, bytes) => SaveBundleData(path, entry, bytes))).ToArray();
            Write(cached, stream =>
            {
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
                {
                    writer.Write(1); writer.Write(file.Length); writer.Write(file.LastWriteTimeUtc.Ticks); writer.Write(result.Length);
                    foreach (var entry in result)
                    {
                        writer.Write(entry.Name); writer.Write(entry.FaceName); writer.Write(entry.Size); writer.Write(entry.RenderingMode);
                        writer.Write(entry.AssetsIndex); writer.Write(entry.DataOffset); writer.Write(entry.DataLength);
                    }
                }
            });
            return result;
        }

        internal byte[] ReadBundleData(string path, BundledFontData data, out bool hit)
        {
            var file = new FileInfo(path);
            if (file.Length != data.BundleLength || file.LastWriteTimeUtc != data.BundleModified)
                throw new InvalidDataException("Font bundle changed; restart to rebuild its catalog.");
            string cached = BundlePath(path) + "-" + data.AssetsIndex + "-" + data.DataOffset + ".font";
            hit = false;
            try
            {
                // Validate revision, bounds, and checksum before passing cached bytes to native code.
                if (new FileInfo(cached).Length != 4L + 16 + 32 + data.DataLength) throw new InvalidDataException();
                using (var reader = new BinaryReader(File.OpenRead(cached)))
                using (var sha = SHA256.Create())
                {
                    if (reader.ReadInt32() != 1 || reader.ReadInt64() != file.Length || reader.ReadInt64() != file.LastWriteTimeUtc.Ticks)
                        throw new InvalidDataException();
                    byte[] hash = reader.ReadBytes(32), bytes = reader.ReadBytes(data.DataLength);
                    if (bytes.Length != data.DataLength || !Equal(hash, sha.ComputeHash(bytes))) throw new InvalidDataException();
                    hit = true;
                    return bytes;
                }
            }
            catch { }
            byte[] source = BundledFontReader.ReadData(path, data);
            SaveBundleData(path, data, source);
            return source;
        }

        private void SaveBundleData(string path, BundledFontData data, byte[] source)
        {
            string cached = BundlePath(path) + "-" + data.AssetsIndex + "-" + data.DataOffset + ".font";
            Write(cached, stream =>
            {
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
                using (var sha = SHA256.Create())
                { writer.Write(1); writer.Write(data.BundleLength); writer.Write(data.BundleModified.Ticks); writer.Write(sha.ComputeHash(source)); writer.Write(source); }
            });
        }

        private static bool Equal(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        private static void Write(string path, Action<Stream> write)
        {
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using (var stream = File.Create(temp)) write(stream);
                if (File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
            }
            catch { }
            finally { try { if (File.Exists(temp)) File.Delete(temp); } catch { } }
        }
    }
}
