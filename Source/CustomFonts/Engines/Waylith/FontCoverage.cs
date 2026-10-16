using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CustomFonts
{
    internal static class FontCoverage
    {
        // Some fonts advertise CJK characters but map them to empty outlines (including
        // HangeulNuri's 山). Exclude those from the primary source so fallback
        // fonts can supply them. Spaces/combining marks are intentionally not excluded.
        internal static uint[] EmptyCjk(byte[] data)
        {
            int U16(int p) { Check(p, 2); return data[p] * 256 + data[p + 1]; }
            uint U32(int p) { Check(p, 4); return ((uint)U16(p) << 16) | (uint)U16(p + 2); }
            void Check(int p, int n) { if (p < 0 || n < 0 || p > data.Length - n) throw new InvalidDataException("Truncated font coverage table."); }
            int face = U32(0) == 0x74746366 ? checked((int)U32(12)) : 0;
            var tables = new Dictionary<uint, (int Offset, int Length)>();
            for (int i = 0, count = U16(face + 4); i < count; i++)
            {
                int p = checked(face + 12 + i * 16);
                int offset = checked((int)U32(p + 8)), length = checked((int)U32(p + 12));
                Check(offset, length);
                tables[U32(p)] = (offset, length);
            }
            if (!tables.TryGetValue(0x68656164, out var head) || !tables.TryGetValue(0x6d617870, out var maxp)
                || !tables.TryGetValue(0x6c6f6361, out var loca) || !tables.TryGetValue(0x676c7966, out var glyf)
                || !tables.TryGetValue(0x636d6170, out var cmap)) return Array.Empty<uint>();
            if (head.Length < 54 || maxp.Length < 6 || cmap.Length < 4) throw new InvalidDataException("Invalid font coverage tables.");
            int glyphCount = U16(maxp.Offset + 4), entrySize = U16(head.Offset + 50) == 0 ? 2 : 4;
            if ((glyphCount + 1) * entrySize > loca.Length) throw new InvalidDataException("Invalid glyph locations.");
            uint Location(int i) => entrySize == 2 ? (uint)U16(loca.Offset + i * 2) * 2 : U32(loca.Offset + i * 4);
            bool Empty(uint id)
            {
                if (id == 0 || id >= glyphCount) return false;
                uint start = Location((int)id), end = Location((int)id + 1);
                if (end < start || end > glyf.Length) throw new InvalidDataException("Invalid glyph bounds.");
                return start == end || end - start >= 10 && U16(glyf.Offset + (int)start) == 0;
            }
            int subtable = -1, format = 0, rank = -1;
            int records = U16(cmap.Offset + 2);
            if (4 + records * 8 > cmap.Length) throw new InvalidDataException("Invalid cmap records.");
            for (int i = 0; i < records; i++)
            {
                int p = cmap.Offset + 4 + i * 8, platform = U16(p), encoding = U16(p + 2);
                if (platform != 0 && !(platform == 3 && (encoding == 1 || encoding == 10))) continue;
                uint offset = U32(p + 4);
                if (offset > cmap.Length - 2) throw new InvalidDataException("Invalid cmap offset.");
                int at = cmap.Offset + (int)offset, f = U16(at), r = f == 12 ? 2 : f == 4 ? 1 : -1;
                if (r > rank) { subtable = at; format = f; rank = r; }
            }
            var empty = new SortedSet<uint>();
            if (format == 12)
            {
                uint length = U32(subtable + 4), count = U32(subtable + 12);
                if (length > int.MaxValue || subtable + (long)length > cmap.Offset + (long)cmap.Length
                    || 16L + count * 12L > length) throw new InvalidDataException("Invalid cmap format 12.");
                for (int i = 0; i < count; i++)
                {
                    int p = subtable + 16 + i * 12;
                    uint start = U32(p), end = U32(p + 4), first = U32(p + 8);
                    if (start > end || end > 0x10ffff || first + (ulong)(end - start) >= (uint)glyphCount)
                        throw new InvalidDataException("Invalid cmap group.");
                    for (uint c = start; c <= end; c++) if (IsCjk(c) && Empty(first + c - start)) empty.Add(c);
                }
            }
            else if (format == 4)
            {
                int length = U16(subtable + 2), segments = U16(subtable + 6) / 2;
                if (16 + segments * 8 > length || subtable + (long)length > cmap.Offset + (long)cmap.Length)
                    throw new InvalidDataException("Invalid cmap format 4.");
                for (int i = 0; i < segments; i++)
                {
                    int end = U16(subtable + 14 + i * 2), start = U16(subtable + 16 + segments * 2 + i * 2);
                    int delta = U16(subtable + 16 + segments * 4 + i * 2), at = subtable + 16 + segments * 6 + i * 2;
                    int offset = U16(at);
                    for (int c = start; c <= end; c++)
                    {
                        if (!IsCjk((uint)c)) continue;
                        int id;
                        if (offset == 0) id = (c + delta) & 0xffff;
                        else
                        {
                            int p = at + offset + (c - start) * 2;
                            if (p > subtable + length - 2) throw new InvalidDataException("Invalid cmap glyph index.");
                            id = U16(p);
                            if (id != 0) id = (id + delta) & 0xffff;
                        }
                        if (Empty((uint)id)) empty.Add((uint)c);
                    }
                }
            }
            var ranges = new List<uint>();
            foreach (uint c in empty)
            {
                if (ranges.Count > 0 && ranges[ranges.Count - 1] + 1 == c) ranges[ranges.Count - 1] = c;
                else { ranges.Add(c); ranges.Add(c); }
            }
            if (ranges.Count > 0) ranges.Add(0);
            return ranges.ToArray();
        }

        private static bool IsCjk(uint c) => (c >= 0x2e80 && c <= 0x2fff) || (c >= 0x3001 && c <= 0x303f && (c < 0x302a || c > 0x302f))
            || (c >= 0x3040 && c <= 0x9fff && c != 0x3099 && c != 0x309a && c != 0x3164)
            || (c >= 0xac00 && c <= 0xd7ff) || (c >= 0xf900 && c <= 0xfaff)
            || (c >= 0xff01 && c <= 0xffdc && c != 0xffa0) || (c >= 0x20000 && c <= 0x3ffff);
    }
}
