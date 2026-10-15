using AssetsTools.NET;
using AssetsTools.NET.Extra;

// Build a small, font-data-free template from the existing Unity bundle. Runtime
// imports replace its one font payload; Unity retains responsibility for rendering.
if (args.Length != 2) throw new ArgumentException("Usage: UnityFontTemplate rimfonts output.bundle");
var manager = new AssetsManager();
try
{
    var bundle = manager.LoadBundleFile(args[0], true);
    var assets = manager.LoadAssetsFileFromBundle(bundle, 0, false);
    long selected = assets.file.GetAssetsOfType(AssetClassID.Font).First().PathId;
    foreach (var info in assets.file.GetAssetsOfType(AssetClassID.Font))
    {
        // char arrays in old Unity type trees otherwise create a value per byte.
        var template = manager.GetTemplateBaseField(assets, info);
        template.Children.Single(f => f.Name == "m_FontData").Children.Single().ValueType = AssetValueType.ByteArray;
        var font = template.MakeValue(assets.file.Reader, info.GetAbsoluteByteOffset(assets.file));
        font["m_FontData.Array"].AsByteArray = Array.Empty<byte>();
        if (info.PathId == selected) font["m_Name"].AsString = "CustomFontsFile";
        info.SetNewData(font);
    }
    var bundleInfo = assets.file.GetAssetsOfType(AssetClassID.AssetBundle).Single();
    var root = manager.GetBaseField(assets, bundleInfo);
    var container = root["m_Container.Array"];
    var entry = container.Children.Single(c => c["second.asset.m_PathID"].AsLong == selected);
    entry["first"].AsString = "customfonts-file";
    container.Children = new List<AssetTypeValueField> { entry };
    root["m_Name"].AsString = "CustomFontsFile";
    bundleInfo.SetNewData(root);
    bundle.file.BlockAndDirInfo.DirectoryInfos[0].SetNewData(assets.file);
    using (var writer = new AssetsFileWriter(args[1])) bundle.file.Write(writer);
    Console.WriteLine($"Template: {new FileInfo(args[1]).Length:N0} bytes");
}
finally { manager.UnloadAll(); }
