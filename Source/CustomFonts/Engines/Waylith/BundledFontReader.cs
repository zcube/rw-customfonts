using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace CustomFonts
{
    internal sealed class BundledFontData
    {
        internal string Name;
        internal string FaceName;
        internal float Size;
        internal int RenderingMode;
        internal int AssetsIndex;
        internal long DataOffset;
        internal int DataLength;
        internal long BundleLength;
        internal DateTime BundleModified;
    }

    internal static class BundledFontReader
    {
        internal static IEnumerable<BundledFontData> Read(string path)
        {
            var file = new FileInfo(path);
            var manager = new AssetsManager();
            try
            {
                var bundle = manager.LoadBundleFile(path, true);
                for (int i = 0; i < bundle.file.BlockAndDirInfo.DirectoryInfos.Count; i++)
                {
                    if ((bundle.file.BlockAndDirInfo.DirectoryInfos[i].Flags & 4) == 0) continue;
                    var assets = manager.LoadAssetsFileFromBundle(bundle, i, false);
                    foreach (var info in assets.file.GetAssetsOfType(AssetClassID.Font))
                    {
                        var data = new BundledFontData { AssetsIndex = i, BundleLength = file.Length,
                            BundleModified = file.LastWriteTimeUtc };
                        var reader = assets.file.Reader;
                        long start = info.GetAbsoluteByteOffset(assets.file);
                        long end = start + info.ByteSize;
                        reader.Position = start;
                        foreach (var field in manager.GetTemplateBaseField(assets, info).Children)
                        {
                            if (field.Name == "m_FontData")
                            {
                                var array = field.Children.Single(x => x.Name == "Array");
                                if (!array.IsArray || array.Children.Count != 2 ||
                                    (array.Children[1].Type != "UInt8" && array.Children[1].Type != "char"))
                                    throw new InvalidDataException("Expected a font byte array.");
                                // Skip the payload instead of allocating the complete byte[]
                                // or the compatibility loader's per-byte value-field objects.
                                data.DataLength = reader.ReadInt32();
                                data.DataOffset = reader.Position;
                                if (data.DataLength < 0 || data.DataLength > 128 * 1024 * 1024 ||
                                    data.DataOffset > end - data.DataLength)
                                    throw new InvalidDataException("Invalid bundled font bounds.");
                                if (data.DataLength > 0)
                                    data.FaceName = OpenTypeNames.Read(reader.BaseStream, data.DataOffset, data.DataLength);
                                reader.Position = data.DataOffset + data.DataLength;
                                if (array.IsAligned || field.IsAligned) reader.Align();
                            }
                            else
                            {
                                var value = field.MakeValue(reader);
                                if (field.Name == "m_Name") data.Name = value.AsString;
                                else if (field.Name == "m_FontSize") data.Size = value.AsFloat;
                                else if (field.Name == "m_FontRenderingMode") data.RenderingMode = value.AsInt;
                            }
                            if (reader.Position > end) throw new InvalidDataException("Truncated font asset.");
                        }
                        if (data.DataLength > 0) yield return data;
                    }
                }
            }
            finally { manager.UnloadAll(); }
        }

        // Catalog entries retain metadata and offsets, never source bytes or an open bundle.
        internal static byte[] ReadData(string path, BundledFontData data)
        {
            var file = new FileInfo(path);
            if (file.Length != data.BundleLength || file.LastWriteTimeUtc != data.BundleModified)
                throw new InvalidDataException("Font bundle changed; restart to rebuild its catalog.");
            var manager = new AssetsManager();
            try
            {
                var bundle = manager.LoadBundleFile(path, true);
                var assets = manager.LoadAssetsFileFromBundle(bundle, data.AssetsIndex, false);
                var stream = assets.file.Reader.BaseStream;
                if (data.DataOffset < 0 || data.DataLength < 0 || data.DataOffset > stream.Length - data.DataLength)
                    throw new InvalidDataException("Invalid bundled font bounds.");
                stream.Position = data.DataOffset;
                byte[] bytes = new byte[data.DataLength];
                stream.ReadExactly(bytes);
                return bytes;
            }
            finally { manager.UnloadAll(); }
        }
    }
}
