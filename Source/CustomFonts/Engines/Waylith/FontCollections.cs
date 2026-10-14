using System;
using System.IO;

namespace CustomFonts
{
    internal static class FontCollections
    {
        // Waylith's registration metrics reader only understands standalone SFNT files.
        // Repack the first collection face in memory, leaving the original file intact.
        internal static byte[] FirstFace(byte[] data)
        {
            uint Read(int p) { if (p < 0 || p > data.Length - 4) throw new InvalidDataException("Truncated font collection."); return ((uint)data[p] << 24) | ((uint)data[p + 1] << 16) | ((uint)data[p + 2] << 8) | data[p + 3]; }
            if (data.Length < 4 || Read(0) != 0x74746366) return data;
            if (Read(8) == 0) throw new InvalidDataException("Empty font collection.");
            int face = checked((int)Read(12));
            uint version = Read(face);
            if (version != 0x00010000 && version != 0x4f54544f && version != 0x74727565)
                throw new InvalidDataException("Unsupported collection face.");
            int count = (int)(Read(face + 4) >> 16), header = checked(12 + count * 16), length = header;
            if (face > data.Length - header) throw new InvalidDataException("Truncated collection directory.");
            for (int i = 0; i < count; i++)
            {
                int p = face + 12 + i * 16;
                uint at = Read(p + 8), size = Read(p + 12);
                if ((ulong)at + size > (ulong)data.Length) throw new InvalidDataException("Invalid collection table bounds.");
                length = checked(length + (((int)size + 3) & ~3));
                if (length > 128 * 1024 * 1024) throw new InvalidDataException("Collection face exceeds 128 MiB.");
            }
            var result = new byte[length];
            Array.Copy(data, face, result, 0, header);
            int offset = header, head = -1;
            for (int i = 0; i < count; i++)
            {
                int source = face + 12 + i * 16, target = 12 + i * 16;
                int size = checked((int)Read(source + 12));
                Array.Copy(data, checked((int)Read(source + 8)), result, offset, size);
                if (Read(source) == 0x68656164 && size >= 12)
                { head = offset; Write(result, head + 8, 0); }
                Write(result, target + 8, (uint)offset);
                Write(result, target + 4, Checksum(result, offset, size));
                offset += (size + 3) & ~3;
            }
            if (head >= 0) Write(result, head + 8, unchecked(0xb1b0afba - Checksum(result, 0, result.Length)));
            return result;
        }

        private static void Write(byte[] bytes, int p, uint value)
        { bytes[p] = (byte)(value >> 24); bytes[p + 1] = (byte)(value >> 16); bytes[p + 2] = (byte)(value >> 8); bytes[p + 3] = (byte)value; }

        private static uint Checksum(byte[] bytes, int p, int length)
        {
            uint sum = 0;
            for (int i = 0; i < length; i += 4)
            {
                uint value = 0;
                for (int j = 0; j < 4; j++) value = (value << 8) | (i + j < length ? bytes[p + i + j] : 0u);
                sum = unchecked(sum + value);
            }
            return sum;
        }
    }
}
