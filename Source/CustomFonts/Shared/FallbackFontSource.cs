using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace CustomFonts
{
    internal sealed class FallbackFontSource
    {
        internal string Name, Path, Revision;
        internal byte[] Bytes;
        internal static string Hash(byte[] bytes)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
        }
        internal static IEnumerable<FallbackFontSource> WithoutPrimary(IEnumerable<FallbackFontSource> sources, byte[] primary)
        {
            string hash = primary.Length == 0 ? "" : Hash(primary);
            return sources.Where(s => s.Revision != hash).GroupBy(s => s.Revision).Select(g => g.First());
        }
    }
}
