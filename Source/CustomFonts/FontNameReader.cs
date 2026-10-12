#if RIMWORLD_1_7
using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace CustomFonts
{
    internal static class FontNameReader
    {
        // TTC registration uses the first face, so read that face's names too.
        public static bool TryRead(byte[] data, out string family, out string style)
        {
            family = null;
            style = null;
            try
            {
                bool collection = U32(data, 0) == 0x74746366;
                if (collection && U32(data, 8) == 0) return false;
                int face = collection ? U32(data, 12) : 0;
                int signature = U32(data, face);
                if (signature != 0x00010000 && signature != 0x4f54544f) return false;
                int tables = U16(data, face + 4);
                for (int i = 0; i < tables; i++)
                {
                    int entry = checked(face + 12 + i * 16);
                    if (U32(data, entry) != 0x6e616d65) continue;
                    int offset = U32(data, entry + 8);
                    int length = U32(data, entry + 12);
                    var names = data.AsSpan(offset, length);
                    return TryReadNames(names, out family, out style);
                }
            }
            catch (ArgumentOutOfRangeException) { }
            catch (OverflowException) { }
            return false;
        }

        public static bool TryRead(string path, out string family, out string style)
        {
            family = null;
            style = null;
            using var stream = File.OpenRead(path);
            Span<byte> header = stackalloc byte[16];
            try
            {
                stream.ReadExactly(header);
                bool collection = U32(header, 0) == 0x74746366;
                if (collection && U32(header, 8) == 0) return false;
                int face = collection ? U32(header, 12) : 0;
                stream.Position = face;
                stream.ReadExactly(header.Slice(0, 12));
                int signature = U32(header, 0);
                if (signature != 0x00010000 && signature != 0x4f54544f) return false;
                int tables = U16(header, 4);
                for (int i = 0; i < tables; i++)
                {
                    stream.ReadExactly(header);
                    if (U32(header, 0) != 0x6e616d65) continue;
                    int offset = U32(header, 8);
                    int length = U32(header, 12);
                    // Name-record offsets and lengths are 16-bit; do not allocate a malformed table's declared size.
                    if (length < 6 || offset > stream.Length || length > stream.Length - offset) return false;
                    stream.Position = offset;
                    byte[] names = new byte[Math.Min(length, ushort.MaxValue * 2)];
                    stream.ReadExactly(names);
                    return TryReadNames(names, out family, out style);
                }
            }
            catch (EndOfStreamException) { }
            catch (ArgumentOutOfRangeException) { }
            catch (OverflowException) { }
            return false;
        }

        private static bool TryReadNames(ReadOnlySpan<byte> names, out string family, out string style)
        {
            family = null;
            style = null;
            try
            {
                if (names.Length < 6) return false;
                int count = U16(names, 2);
                int strings = U16(names, 4);
                if (U16(names, 0) > 1 || strings < 6 + count * 12 || strings > names.Length) return false;
                int familyRank = -1, styleRank = -1;
                for (int j = 0; j < count; j++)
                {
                    int record = 6 + j * 12;
                    int platform = U16(names, record);
                    int encoding = U16(names, record + 2);
                    int language = U16(names, record + 4);
                    int id = U16(names, record + 6);
                    if (id != 1 && id != 2) continue;
                    if (platform != 0 && !(platform == 3 && (encoding == 1 || encoding == 10))) continue;
                    int size = U16(names, record + 8);
                    int start = checked(strings + U16(names, record + 10));
                    if ((size & 1) != 0) return false;
                    string value = Encoding.BigEndianUnicode.GetString(names.Slice(start, size)).Trim('\0', ' ');
                    if (value.Length == 0) continue;
                    int rank = platform == 3 && language == 0x409 ? 2 : platform == 0 ? 1 : 0;
                    if (id == 1 && rank > familyRank) { family = value; familyRank = rank; }
                    if (id == 2 && rank > styleRank) { style = value; styleRank = rank; }
                }
                if (family == null) return false;
                if (style == null) style = "Regular";
                return true;
            }
            catch (ArgumentOutOfRangeException) { }
            catch (OverflowException) { }
            return false;
        }

        private static int U16(ReadOnlySpan<byte> data, int offset) => BinaryPrimitives.ReadUInt16BigEndian(data.Slice(offset, 2));
        private static int U32(ReadOnlySpan<byte> data, int offset) => checked((int)BinaryPrimitives.ReadUInt32BigEndian(data.Slice(offset, 4)));
    }
}
#endif
