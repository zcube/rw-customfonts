using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CustomFonts
{
    // Unity IMGUI requires a dynamic Font resource, not just a file-path TMP source.
    // Assemble an uncompressed, four-type Unity resource from a fixed template and
    // the selected TTF bytes. No asset library, compression or Unity file parser is
    // involved: engine type metadata and unchanged field spans stay opaque.
    internal static class UnityFontBundle
    {
        internal const string AssetName = "customfonts-file";
        private sealed class Part { internal int Tag; internal byte[] Bytes; }
        private sealed class Record { internal long Id; internal int Type; internal byte[] Bytes; internal uint Offset; }
        private sealed class Template
        {
            internal byte[] Prefix, Suffix, Texture, Material;
            internal Part[] Font, Bundle;
            internal int[] TexturePointers;
        }
        private static readonly Template Data = ReadTemplate();

        private static Template ReadTemplate()
        {
            using (var stream = typeof(UnityFontBundle).Assembly.GetManifestResourceStream("CustomFonts.FontTemplate.bin"))
            {
                if (stream == null || stream.Length > 64 * 1024) throw new InvalidDataException("Invalid Unity font template.");
                using (var reader = new BinaryReader(stream, Encoding.UTF8))
                {
                    if (Encoding.ASCII.GetString(reader.ReadBytes(8)) != "CFUFNT01") throw new InvalidDataException("Unknown Unity font template.");
                    byte[] Blob()
                    {
                        int length = reader.ReadInt32();
                        if (length < 0 || length > stream.Length - stream.Position) throw new InvalidDataException("Truncated Unity font template.");
                        return reader.ReadBytes(length);
                    }
                    Part[] Parts(int maxTag)
                    {
                        int count = reader.ReadInt32();
                        if (count < 1 || count > 128) throw new InvalidDataException("Invalid template fields.");
                        var parts = new Part[count];
                        for (int i = 0; i < count; i++)
                        {
                            int tag = reader.ReadInt32();
                            if (tag < 0 || tag > maxTag) throw new InvalidDataException("Unknown template field.");
                            parts[i] = new Part { Tag = tag, Bytes = Blob() };
                        }
                        return parts;
                    }
                    var result = new Template { Prefix = Blob(), Suffix = Blob(), Font = Parts(6), Texture = Blob(), Material = Blob() };
                    int pointers = reader.ReadInt32();
                    if (pointers < 1 || pointers > 32) throw new InvalidDataException("Invalid material pointers.");
                    result.TexturePointers = new int[pointers];
                    for (int i = 0; i < pointers; i++)
                    {
                        int offset = reader.ReadInt32();
                        if (offset < 4 || offset > result.Material.Length - 8) throw new InvalidDataException("Invalid texture pointer bounds.");
                        result.TexturePointers[i] = offset;
                    }
                    result.Bundle = Parts(8);
                    if (stream.Position != stream.Length) throw new InvalidDataException("Trailing Unity font template data.");
                    return result;
                }
            }
        }

        internal static byte[] Create(byte[] bytes, IEnumerable<FallbackFontSource> fallbacks = null, string[] primaryNames = null)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            var sources = FallbackFontSource.WithoutPrimary(fallbacks ?? new FallbackFontSource[0], bytes).ToArray();
            string Family(byte[] source)
            {
                string face = OpenTypeNames.Read(source); int style = face.LastIndexOf(" (", StringComparison.Ordinal);
                return style < 0 ? face : face.Substring(0, style);
            }
            string[] fallbackNames = sources.Select(s => Family(s.Bytes)).ToArray();
            string[] ownNames = primaryNames ?? (bytes.Length == 0 ? new string[0] : new[] { Family(bytes) });
            int count = checked(sources.Length + 1);
            long[] fontIds = Enumerable.Range(0, count).Select(i => 2L + i * 3L).ToArray();
            var records = new List<Record>();
            for (int i = 0; i < count; i++)
            {
                long fontId = fontIds[i], textureId = fontId + 1, materialId = fontId + 2;
                byte[] payload = i == 0 ? bytes : sources[i - 1].Bytes;
                string[] names = i == 0 ? ownNames.Concat(fallbackNames).ToArray() : new[] { fallbackNames[i - 1] };
                byte[] font = Pack(writer =>
                {
                    foreach (var part in Data.Font)
                    {
                        switch (part.Tag)
                        {
                            case 0: writer.Write(part.Bytes); break;
                            case 1: String(writer, i == 0 ? "CustomFontsFile" : fallbackNames[i - 1]); break;
                            case 2: Pointer(writer, materialId); break;
                            case 3: Pointer(writer, textureId); break;
                            case 4: writer.Write(payload.Length); writer.Write(payload); Align(writer, 4); break;
                            case 5: writer.Write(names.Length); foreach (string name in names) String(writer, name); break;
                            case 6:
                                writer.Write(i == 0 ? sources.Length : 0);
                                if (i == 0) foreach (long id in fontIds.Skip(1)) Pointer(writer, id);
                                break;
                        }
                    }
                });
                byte[] material = (byte[])Data.Material.Clone();
                using (var writer = new BinaryWriter(new MemoryStream(material)))
                    foreach (int offset in Data.TexturePointers) { writer.BaseStream.Position = offset; writer.Write(textureId); }
                records.Add(new Record { Id = fontId, Type = 0, Bytes = font });
                records.Add(new Record { Id = textureId, Type = 1, Bytes = Data.Texture });
                records.Add(new Record { Id = materialId, Type = 2, Bytes = material });
            }
            long bundleId = 1; // Unity locates the AssetBundle root at path ID 1.
            records.Add(new Record { Id = bundleId, Type = 3, Bytes = Pack(writer =>
            {
                foreach (var part in Data.Bundle)
                {
                    if (part.Tag == 0) writer.Write(part.Bytes);
                    else if (part.Tag == 7)
                    {
                        writer.Write(checked(count * 3));
                        foreach (long id in fontIds) for (int dependency = 0; dependency < 3; dependency++) Pointer(writer, id + dependency);
                    }
                    else if (part.Tag == 8)
                    {
                        writer.Write(count);
                        for (int i = 0; i < count; i++)
                        {
                            String(writer, i == 0 ? AssetName : "customfonts-fallback-" + (i - 1));
                            writer.Write(0); writer.Write(checked(count * 3)); Pointer(writer, fontIds[i]);
                        }
                    }
                }
            }) });
            records.Sort((a, b) => a.Id.CompareTo(b.Id));
            uint dataLength = 0;
            foreach (var record in records)
            {
                dataLength = checked((dataLength + 7) & ~7u); record.Offset = dataLength;
                dataLength = checked(dataLength + (uint)record.Bytes.Length);
            }
            byte[] metadata = Pack(writer =>
            {
                writer.Write(Data.Prefix); writer.Write(records.Count); Align(writer, 4);
                foreach (var record in records)
                { writer.Write(record.Id); writer.Write(record.Offset); writer.Write(record.Bytes.Length); writer.Write(record.Type); }
                writer.Write(Data.Suffix);
            });
            uint dataOffset = checked(((uint)metadata.Length + 20 + 15) & ~15u);
            byte[] assets = Pack(writer =>
            {
                Big32(writer, (uint)metadata.Length); Big32(writer, checked(dataOffset + dataLength));
                Big32(writer, 21); Big32(writer, dataOffset); writer.Write(0); // little-endian serialized format 21
                writer.Write(metadata); PadTo(writer, dataOffset);
                foreach (var record in records) { PadTo(writer, dataOffset + record.Offset); writer.Write(record.Bytes); }
            });
            return Wrap(assets);
        }

        // One uncompressed UnityFS block. Font files are never packed or decompressed.
        private static byte[] Wrap(byte[] assets)
        {
            string identity = "CAB-" + Guid.NewGuid().ToString("N");
            byte[] directory = Pack(writer =>
            {
                writer.Write(new byte[16]); Big32(writer, 1); Big32(writer, (uint)assets.Length); Big32(writer, (uint)assets.Length);
                writer.Write((byte)0); writer.Write((byte)64); Big32(writer, 1);
                Big64(writer, 0); Big64(writer, (ulong)assets.Length); Big32(writer, 4); CString(writer, identity);
            });
            byte[] Header(ulong size) => Pack(writer =>
            {
                CString(writer, "UnityFS"); Big32(writer, 7); CString(writer, "5.x.x"); CString(writer, "2019.4.40f1");
                Big64(writer, size); Big32(writer, (uint)directory.Length); Big32(writer, (uint)directory.Length); Big32(writer, 64); Align(writer, 16);
            });
            byte[] header = Header(0);
            header = Header(checked((ulong)header.Length + (ulong)directory.Length + (ulong)assets.Length));
            return Pack(writer => { writer.Write(header); writer.Write(directory); writer.Write(assets); });
        }

        private static byte[] Pack(Action<BinaryWriter> write)
        {
            using (var buffer = new MemoryStream())
            { using (var writer = new BinaryWriter(buffer, Encoding.UTF8, true)) write(writer); return buffer.ToArray(); }
        }
        private static void Pointer(BinaryWriter writer, long id) { writer.Write(0); writer.Write(id); }
        private static void String(BinaryWriter writer, string text)
        { byte[] utf8 = Encoding.UTF8.GetBytes(text); writer.Write(utf8.Length); writer.Write(utf8); Align(writer, 4); }
        private static void CString(BinaryWriter writer, string text) { writer.Write(Encoding.UTF8.GetBytes(text)); writer.Write((byte)0); }
        private static void Align(BinaryWriter writer, int alignment) => PadTo(writer, (writer.BaseStream.Position + alignment - 1) & ~(alignment - 1L));
        private static void PadTo(BinaryWriter writer, long position)
        { if (position < writer.BaseStream.Position) throw new InvalidDataException("Invalid Unity resource offset."); while (writer.BaseStream.Position < position) writer.Write((byte)0); }
        private static void Big32(BinaryWriter writer, uint value) { for (int shift = 24; shift >= 0; shift -= 8) writer.Write((byte)(value >> shift)); }
        private static void Big64(BinaryWriter writer, ulong value) { for (int shift = 56; shift >= 0; shift -= 8) writer.Write((byte)(value >> shift)); }
    }
}
