using System;
using System.IO;
using System.Text;

namespace CustomFonts
{
    // Read names without Unity's unimplemented FontEngine shim. TTC files use their first face,
    // matching Waylith's AddFontFromMemoryTTF registration (which has no face-index argument).
    internal static class OpenTypeNames
    {
        static OpenTypeNames() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        internal static string Read(byte[] data)
        {
            using var stream = new MemoryStream(data, false);
            return Read(stream, 0, data.Length);
        }

        // Read only the SFNT directory and name table. origin also allows a font embedded
        // in a bundle to be inspected without allocating its complete m_FontData array.
        internal static string Read(Stream stream, long origin, long fontLength)
        {
            if (!stream.CanSeek || origin < 0 || fontLength < 0 || origin > stream.Length - fontLength)
                throw new InvalidDataException("Invalid font stream bounds.");
            byte[] ReadAt(long at, int length)
            {
                if (at < 0 || length < 0 || at > fontLength - length)
                    throw new InvalidDataException("Truncated OpenType table.");
                stream.Position = origin + at;
                byte[] bytes = new byte[length];
                stream.ReadExactly(bytes);
                return bytes;
            }
            byte[] header = ReadAt(0, 12);
            long face = 0;
            if (U32(header, 0) == 0x74746366) // ttcf
            {
                if (U32(header, 8) == 0) throw new InvalidDataException("Empty font collection.");
                face = U32(ReadAt(12, 4), 0);
                header = ReadAt(face, 12);
            }
            uint signature = U32(header, 0);
            if (signature != 0x00010000 && signature != 0x4f54544f && signature != 0x74727565)
                throw new InvalidDataException("Expected a TTF, OTF or TTC font.");
            int count = U16(header, 4);
            byte[] directory = ReadAt(face + 12, count * 16);
            for (int i = 0; i < count; i++)
            {
                int record = i * 16;
                if (U32(directory, record) != 0x6e616d65) continue; // name
                long offset = U32(directory, record + 8);
                uint tableLength = U32(directory, record + 12);
                if (tableLength < 6 || tableLength > 1024 * 1024)
                    throw new InvalidDataException("Invalid or oversized name table.");
                byte[] table = ReadAt(offset, (int)tableLength);
                int length = table.Length;
                int names = U16(table, 2), strings = U16(table, 4);
                if (6 + names * 12 > length || strings > length)
                    throw new InvalidDataException("Invalid name records.");
                string family = null, style = null;
                int familyRank = -1, styleRank = -1;
                for (int n = 0; n < names; n++)
                {
                    int entry = 6 + n * 12;
                    int platform = U16(table, entry), encoding = U16(table, entry + 2), language = U16(table, entry + 4), id = U16(table, entry + 6);
                    if (id != 1 && id != 2 && id != 16 && id != 17) continue;
                    if (platform != 0 && platform != 1 && platform != 3) continue;
                    if (platform == 1 && encoding != 0) continue;
                    if (platform == 3 && encoding != 0 && encoding != 1 && encoding != 10) continue;
                    int size = U16(table, entry + 8), stringAt = strings + U16(table, entry + 10);
                    if (stringAt > length - size) throw new InvalidDataException("Invalid name string.");
                    if (platform != 1 && size % 2 != 0) continue;
                    string value = (platform == 1 ? Encoding.GetEncoding(10000) : Encoding.BigEndianUnicode)
                        .GetString(table, stringAt, size).Trim('\0', ' ');
                    if (value.Length == 0) continue;
                    int rank = (platform == 1 ? 0 : 1000) + (id >= 16 ? 100 : 0) + (language == 0x409 ? 20 : language == 0 ? 10 : 0)
                        + (platform == 3 ? 3 : platform == 0 ? 2 : 1);
                    if ((id == 1 || id == 16) && rank > familyRank) { family = value; familyRank = rank; }
                    if ((id == 2 || id == 17) && rank > styleRank) { style = value; styleRank = rank; }
                }
                if (family == null) throw new InvalidDataException("Font has no family name.");
                return family + " (" + (style ?? "Regular") + ")";
            }
            throw new InvalidDataException("Font has no name table.");
        }

        private static int U16(byte[] data, int at) => (data[at] << 8) | data[at + 1];
        private static uint U32(byte[] data, int at) => ((uint)U16(data, at) << 16) | (uint)U16(data, at + 2);
    }
}
