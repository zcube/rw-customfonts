using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace CustomFonts
{
    // A file-path Font is only a TMP source, not a dynamic IMGUI Font. Import font
    // bytes into a small existing Unity Font asset so both renderers use the engine's
    // normal font/atlas code. This avoids OS installation and native platform hooks.
    internal static class UnityFontBundle
    {
        internal const string AssetName = "customfonts-file";

        internal static byte[] Create(byte[] bytes, IEnumerable<FallbackFontSource> fallbacks = null, string[] primaryNames = null)
        {
            var manager = new AssetsManager();
            try
            {
                using (var stream = typeof(UnityFontBundle).Assembly.GetManifestResourceStream("CustomFonts.FontTemplate.bundle"))
                {
                    if (stream == null) throw new InvalidDataException("Unity font template is missing.");
                    var bundle = manager.LoadBundleFile(stream, "font-template", true);
                    var assets = manager.LoadAssetsFileFromBundle(bundle, 0, false);
                    var info = assets.file.GetAssetsOfType(AssetClassID.Font)
                        .Single(i => manager.GetBaseField(assets, i)["m_Name"].AsString == "CustomFontsFile");
                    var template = manager.GetTemplateBaseField(assets, info);
                    template.Children.Single(f => f.Name == "m_FontData").Children.Single().ValueType = AssetValueType.ByteArray;
                    var font = template.MakeValue(assets.file.Reader, info.GetAbsoluteByteOffset(assets.file));
                    font["m_FontData.Array"].AsByteArray = bytes;
                    var sources = FallbackFontSource.WithoutPrimary(fallbacks ?? new FallbackFontSource[0], bytes).ToArray();
                    var pointerTemplate = font["m_FallbackFonts.Array"].Children[0].TemplateField;
                    AssetTypeValueField Pointer(long id)
                    {
                        using (var buffer = new MemoryStream())
                        {
                            using (var writer = new BinaryWriter(buffer, System.Text.Encoding.UTF8, true)) { writer.Write(0); writer.Write(id); }
                            buffer.Position = 0;
                            return pointerTemplate.MakeValue(new AssetsFileReader(buffer));
                        }
                    }
                    long nextId = assets.file.AssetInfos.Max(i => i.PathId);
                    var cloneMethod = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);
                    AssetFileInfo Clone(AssetFileInfo original)
                    {
                        var result = (AssetFileInfo)cloneMethod.Invoke(original, null);
                        result.PathId = checked(++nextId);
                        assets.file.AssetInfos.Add(result);
                        return result;
                    }
                    var fallbackIds = new List<long>();
                    void SetNames(AssetTypeValueField names, IEnumerable<string> values)
                    {
                        var nameTemplate = names.Children[0].TemplateField;
                        names.Children.Clear();
                        foreach (string value in values)
                            using (var buffer = new MemoryStream())
                            {
                                using (var writer = new BinaryWriter(buffer, System.Text.Encoding.UTF8, true))
                                {
                                    byte[] utf8 = System.Text.Encoding.UTF8.GetBytes(value); writer.Write(utf8.Length); writer.Write(utf8);
                                    while (buffer.Position % 4 != 0) writer.Write((byte)0);
                                }
                                buffer.Position = 0; names.Children.Add(nameTemplate.MakeValue(new AssetsFileReader(buffer)));
                            }
                    }
                    var preloadIds = new List<long> { info.PathId, font["m_Texture.m_PathID"].AsLong, font["m_DefaultMaterial.m_PathID"].AsLong };
                    var fallbackNames = new List<string>();
                    string Family(FallbackFontSource source)
                    {
                        string face = OpenTypeNames.Read(source.Bytes);
                        int style = face.LastIndexOf(" (", StringComparison.Ordinal);
                        return style < 0 ? face : face.Substring(0, style);
                    }
                    foreach (var source in sources)
                    {
                        var extra = Clone(info);
                        var fallback = template.MakeValue(assets.file.Reader, info.GetAbsoluteByteOffset(assets.file));
                        fallback["m_Name"].AsString = Family(source);
                        fallback["m_FontData.Array"].AsByteArray = source.Bytes;
                        SetNames(fallback["m_FontNames.Array"], new[] { Family(source) });
                        fallback["m_FallbackFonts.Array"].Children.Clear();
                        // Each dynamic font needs its own material/texture dependencies.
                        long textureId = font["m_Texture.m_PathID"].AsLong;
                        var textureInfo = assets.file.GetAssetInfo(textureId);
                        var texture = Clone(textureInfo);
                        texture.SetNewData(manager.GetBaseField(assets, textureInfo));
                        long materialId = font["m_DefaultMaterial.m_PathID"].AsLong;
                        var materialInfo = assets.file.GetAssetInfo(materialId);
                        var material = Clone(materialInfo);
                        var materialData = manager.GetBaseField(assets, materialInfo);
                        void RemapTexture(AssetTypeValueField field)
                        {
                            if (field.TypeName.StartsWith("PPtr<", StringComparison.Ordinal)
                                && field["m_FileID"].AsInt == 0 && field["m_PathID"].AsLong == textureId) field["m_PathID"].AsLong = texture.PathId;
                            foreach (var child in field.Children) RemapTexture(child);
                        }
                        RemapTexture(materialData);
                        material.SetNewData(materialData);
                        fallback["m_Texture.m_PathID"].AsLong = texture.PathId;
                        fallback["m_DefaultMaterial.m_PathID"].AsLong = material.PathId;
                        extra.SetNewData(fallback);
                        fallbackIds.Add(extra.PathId);
                        preloadIds.AddRange(new[] { extra.PathId, texture.PathId, material.PathId });
                        fallbackNames.Add(Family(source));
                    }
                    font["m_FallbackFonts.Array"].Children = fallbackIds.Select(Pointer).ToList();
                    if (sources.Length != 0 || primaryNames != null)
                    {
                        var names = font["m_FontNames.Array"];
                        string[] ownNames = primaryNames;
                        if (ownNames == null && bytes.Length != 0)
                        {
                            string ownFace = OpenTypeNames.Read(bytes); int style = ownFace.LastIndexOf(" (", StringComparison.Ordinal);
                            ownNames = new[] { style < 0 ? ownFace : ownFace.Substring(0, style) };
                        }
                        SetNames(names, (ownNames ?? new string[0]).Concat(fallbackNames));
                    }
                    info.SetNewData(font);
                    if (sources.Length != 0)
                    {
                        var bundleInfo = assets.file.GetAssetsOfType(AssetClassID.AssetBundle).Single();
                        var bundleRoot = manager.GetBaseField(assets, bundleInfo);
                        bundleRoot["m_PreloadTable.Array"].Children = preloadIds.Select(Pointer).ToList();
                        var container = bundleRoot["m_Container.Array"];
                        var primary = container.Children[0];
                        primary["second.preloadIndex"].AsInt = 0;
                        primary["second.preloadSize"].AsInt = preloadIds.Count;
                        for (int i = 0; i < fallbackIds.Count; i++)
                        {
                            using (var data = new MemoryStream(primary.WriteToByteArray()))
                            {
                                var fallbackEntry = primary.TemplateField.MakeValue(new AssetsFileReader(data));
                                fallbackEntry["first"].AsString = "customfonts-fallback-" + i;
                                fallbackEntry["second.asset.m_PathID"].AsLong = fallbackIds[i];
                                container.Children.Add(fallbackEntry);
                            }
                        }
                        bundleInfo.SetNewData(bundleRoot);
                    }
                    // Unity identifies serialized bundle files by this name. Every import
                    // needs its own identity even while the original rimfonts is loaded.
                    var entry = bundle.file.BlockAndDirInfo.DirectoryInfos[0];
                    entry.Name = "CAB-" + Guid.NewGuid().ToString("N");
                    entry.SetNewData(assets.file);
                    using (var output = new MemoryStream())
                    {
                        using (var writer = new AssetsFileWriter(output)) bundle.file.Write(writer);
                        return output.ToArray();
                    }
                }
            }
            finally { manager.UnloadAll(); }
        }
    }
}
