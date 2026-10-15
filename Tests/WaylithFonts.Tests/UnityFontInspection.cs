using System.Text;

// Read the documented UnityFS/SerializedFile structures independently of the writer.
// This test-only reader rejects compression, out-of-file spans and broken references.
// Native Unity regression runs separately validate that the resources actually render.
internal static class UnityFontInspection
{
    internal sealed record Font(string Name, byte[] Bytes);
    private sealed record Node(int Level, int Flags, int Size, int Meta, uint Name);
    private sealed record Type(int Class, Node[] Nodes, byte[] Strings);
    private sealed record ObjectInfo(long Id, uint Offset, int Length, int Type);
    private static uint Big32(BinaryReader r) => System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(r.ReadBytes(4));
    private static ulong Big64(BinaryReader r) => System.Buffers.Binary.BinaryPrimitives.ReadUInt64BigEndian(r.ReadBytes(8));
    private static void Align(BinaryReader r, int n) => r.BaseStream.Position = (r.BaseStream.Position + n - 1) & ~(n - 1L);
    private static string CString(BinaryReader r) { var b = new List<byte>(); byte c; while ((c = r.ReadByte()) != 0) b.Add(c); return Encoding.UTF8.GetString(b.ToArray()); }
    private static void Require(bool condition) { if (!condition) throw new InvalidDataException("Invalid uncompressed Unity font resource."); }

    internal static Font[] Read(string path)
    {
        using var source = File.OpenRead(path);
        using var stream = new MemoryStream(); source.CopyTo(stream); stream.Position = 0;
        using var r = new BinaryReader(stream);
        Require(CString(r) == "UnityFS");
        uint version = Big32(r); CString(r); CString(r);
        Require(Big64(r) == (ulong)stream.Length);
        uint compressed = Big32(r), uncompressed = Big32(r), flags = Big32(r);
        Require((flags & 63) == 0 && (flags & 128) == 0 && compressed == uncompressed);
        if (version >= 7) Align(r, 16);
        long directory = stream.Position;
        r.ReadBytes(16); Require(Big32(r) == 1);
        uint rawSize = Big32(r); Require(Big32(r) == rawSize);
        Require((r.ReadByte() & 63) == 0 && (r.ReadByte() & 63) == 0);
        Require(Big32(r) == 1); ulong entryOffset = Big64(r), entryLength = Big64(r);
        Require(Big32(r) == 4); CString(r);
        long start = checked(directory + compressed + (long)entryOffset);
        Require(entryLength == rawSize && start + (long)entryLength == stream.Length);
        byte[] assetBytes = stream.ToArray()[(int)start..];
        stream.SetLength(0); stream.Write(assetBytes); stream.Position = 0; start = 0;
        uint metadataSize = Big32(r), fileSize = Big32(r);
        Require(Big32(r) == 21); uint dataOffset = Big32(r);
        Require(r.ReadInt32() == 0 && fileSize == entryLength && dataOffset >= 20 + metadataSize);
        CString(r); r.ReadInt32(); Require(r.ReadBoolean());
        int typeCount = r.ReadInt32(); Require(typeCount == 4);
        var types = new List<Type>();
        for (int i = 0; i < typeCount; i++)
        {
            int id = r.ReadInt32(); r.ReadBoolean(); r.ReadInt16();
            if (id == 114) r.ReadBytes(16);
            r.ReadBytes(16);
            int count = r.ReadInt32(), stringLength = r.ReadInt32();
            var nodes = new Node[count];
            for (int j = 0; j < count; j++)
            {
                r.ReadUInt16(); int level = r.ReadByte(), nodeFlags = r.ReadByte(); r.ReadUInt32(); uint name = r.ReadUInt32();
                int size = r.ReadInt32(); r.ReadInt32(); int meta = r.ReadInt32(); r.ReadUInt64();
                nodes[j] = new Node(level, nodeFlags, size, meta, name);
            }
            byte[] strings = r.ReadBytes(stringLength);
            int dependencies = r.ReadInt32(); r.ReadBytes(dependencies * 4);
            types.Add(new Type(id, nodes, strings));
        }
        int objects = r.ReadInt32(); Align(r, 4);
        var entries = new List<ObjectInfo>();
        for (int i = 0; i < objects; i++) entries.Add(new ObjectInfo(r.ReadInt64(), r.ReadUInt32(), r.ReadInt32(), r.ReadInt32()));
        Require(entries.Select(e => e.Id).Distinct().Count() == objects);
        Require(entries.Select(e => e.Id).SequenceEqual(entries.Select(e => e.Id).Order()));
        Require(entries.Single(e => types[e.Type].Class == 142).Id == 1);
        var results = new List<Font>();
        foreach (var entry in entries)
        {
            Require(entry.Offset + (long)entry.Length <= fileSize - dataOffset);
            var type = types[entry.Type]; if (type.Class != 128) continue;
            stream.Position = start + dataOffset + entry.Offset;
            string name = null; byte[] bytes = null;
            string Name(Node node) => node.Name == (0x80000000u | 427) ? "m_Name" :
                (node.Name & 0x80000000u) != 0 ? "" : Encoding.UTF8.GetString(type.Strings, (int)node.Name,
                    Array.IndexOf(type.Strings, (byte)0, (int)node.Name) - (int)node.Name);
            int End(int at) { int end = at + 1; while (end < type.Nodes.Length && type.Nodes[end].Level > type.Nodes[at].Level) end++; return end; }
            void Skip(int at)
            {
                var node = type.Nodes[at]; int end = End(at);
                if ((node.Flags & 1) != 0)
                {
                    int count = r.ReadInt32(); Require(count >= 0 && count <= fileSize);
                    int element = End(at + 1);
                    if (type.Nodes[element].Size >= 0) stream.Position += checked((long)count * type.Nodes[element].Size);
                    else for (int i = 0; i < count; i++) Skip(element);
                }
                else if (at + 1 == end) { Require(node.Size >= 0); stream.Position += node.Size; }
                else for (int child = at + 1; child < end; child = End(child)) Skip(child);
                if ((node.Meta & 0x4000) != 0) Align(r, 4);
            }
            for (int field = 1; field < type.Nodes.Length; field = End(field))
            {
                string fieldName = Name(type.Nodes[field]); long position = stream.Position;
                if (fieldName == "m_Name") name = Encoding.UTF8.GetString(r.ReadBytes(r.ReadInt32()));
                else if (fieldName == "m_FontData") bytes = r.ReadBytes(r.ReadInt32());
                else if (fieldName == "m_Texture" || fieldName == "m_DefaultMaterial")
                {
                    Require(r.ReadInt32() == 0); long id = r.ReadInt64();
                    Require(entries.Any(e => e.Id == id && types[e.Type].Class == (fieldName == "m_Texture" ? 28 : 21)));
                }
                else if (fieldName == "m_FallbackFonts")
                {
                    int count = r.ReadInt32();
                    for (int i = 0; i < count; i++) { Require(r.ReadInt32() == 0); long id = r.ReadInt64(); Require(id != entry.Id && entries.Any(e => e.Id == id && types[e.Type].Class == 128)); }
                }
                stream.Position = position; Skip(field);
                Require(stream.Position <= start + dataOffset + entry.Offset + entry.Length);
            }
            Require(stream.Position == start + dataOffset + entry.Offset + entry.Length && name != null && bytes != null);
            if (bytes.Length != 0) results.Add(new Font(name, bytes));
        }
        return results.ToArray();
    }
}
