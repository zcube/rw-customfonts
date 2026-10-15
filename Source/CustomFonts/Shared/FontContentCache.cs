using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace CustomFonts
{
    // Cache lookup, first-face conversion and content identity are engine independent.
    // The engine decides whether an unreferenced font can actually be released.
    internal sealed class FontContentCache<T> where T : class
    {
        private readonly Dictionary<string, string> _revisions = new Dictionary<string, string>(FontSourceCatalog.PathComparer);
        internal readonly Dictionary<string, T> Fonts = new Dictionary<string, T>();
        internal int RevisionCount => _revisions.Count;

        internal T Get(string path, Func<byte[], string, string, T> create, Func<T, bool> alive, string policy = "")
        {
            string sourceKey = path.Length + ":" + path + policy;
            if (_revisions.TryGetValue(sourceKey, out string revision) && Fonts.TryGetValue(revision, out T cached) && alive(cached)) return cached;
            byte[] bytes = FontCollections.FirstFace(FontFiles.Read(path));
            string name = OpenTypeNames.Read(bytes);
            using (var sha = SHA256.Create()) revision = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
            if (policy.Length != 0) revision += ":" + policy;
            if (!Fonts.TryGetValue(revision, out cached) || !alive(cached))
            {
                cached = create(bytes, name, revision);
                if (!alive(cached)) return cached;
                Fonts[revision] = cached;
            }
            _revisions[sourceKey] = revision;
            return cached;
        }

        internal void Invalidate() => _revisions.Clear();
        internal void Clear() { _revisions.Clear(); Fonts.Clear(); }
        internal bool Contains(T font) => Fonts.Values.Any(f => ReferenceEquals(f, font));
        internal void Forget(T font)
        {
            var removed = new HashSet<string>(Fonts.Where(p => ReferenceEquals(p.Value, font)).Select(p => p.Key));
            foreach (string key in removed) Fonts.Remove(key);
            foreach (string path in _revisions.Where(p => removed.Contains(p.Value)).Select(p => p.Key).ToArray()) _revisions.Remove(path);
        }
    }
}
