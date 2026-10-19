using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CustomFonts
{
    // Disk cache contains only derived data. Failure/corruption always falls back to the source.
    internal sealed class FontMetadataCache
    {
        private sealed class Entry { internal long Length, Modified; internal string Name; }
        private readonly Dictionary<string, Entry> _names = new Dictionary<string, Entry>(FontSourceCatalog.PathComparer);
        private readonly Dictionary<string, string> _displayNames = new Dictionary<string, string>();
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

        internal void ClearDisplayNames() => _displayNames.Clear();

        internal string ReadDisplayName(string path, int language, string fallback)
        {
            string key = language + ":" + path;
            if (_displayNames.TryGetValue(key, out string name)) return name;
            try { name = FontFiles.ReadDisplayName(path, language); }
            catch { name = fallback; }
            _displayNames[key] = name;
            return name;
        }

        internal string ReadName(string path)
        {
            path = Path.GetFullPath(path);
            var file = new FileInfo(path);
            if (_names.TryGetValue(path, out Entry entry) && entry.Length == file.Length && entry.Modified == file.LastWriteTimeUtc.Ticks)
            { Hits++; return entry.Name; }
            Misses++;
            ClearDisplayNames();
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
