using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;
using CustomFonts;

internal static class FontPackExampleValidation
{
    internal static void Run(string root, Action<bool, string> check)
    {
        var metadata = XDocument.Load(Path.Combine(root, "About", "About.xml")).Root;
        string id = metadata.Element("packageId").Value;
        string name = metadata.Element("name").Value;
        check(id == "zcubekr.customfonts.example.abeezee", "example package matches game integration scenario");
        check(metadata.Element("modDependencies").Elements("li").Any(d => (string)d.Element("packageId") == "zcubekr.customfonts")
            && metadata.Element("loadAfter").Elements("li").Any(d => d.Value == "zcubekr.customfonts"),
            "real example declares dependency and ordering for game registration");
        check(metadata.Element("supportedVersions").Elements("li").Select(e => e.Value).SequenceEqual(new[] { "1.4", "1.5", "1.6", "1.7" }),
            "example declares all supported engine versions");
        check(!Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Any(f => Path.GetExtension(f).Equals(".dll", StringComparison.OrdinalIgnoreCase)
            || Path.GetExtension(f).Equals(".cs", StringComparison.OrdinalIgnoreCase)), "example is font-only without assembly or source code");
        using var provenance = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "provenance.json")));
        check(provenance.RootElement.GetProperty("commit").GetString() == "47d54ca1ebb14fd864695f1d39dc734f39017c33", "example upstream revision is pinned");
        foreach (var entry in provenance.RootElement.GetProperty("files").EnumerateArray())
        {
            string relative = entry.GetProperty("file").GetString();
            byte[] bytes = File.ReadAllBytes(Path.Combine(root, relative));
            check(bytes.Length == entry.GetProperty("bytes").GetInt32()
                && Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() == entry.GetProperty("sha256").GetString(),
                "example retains exact upstream bytes: " + relative);
        }
        string license = File.ReadAllText(Path.Combine(root, "Fonts", "OFL.txt"));
        check(license.Contains("Copyright 2011 The ABeeZee Project Authors") && license.Contains("SIL OPEN FONT LICENSE Version 1.1")
            && license.Contains("PERMISSION & CONDITIONS") && license.Contains("DISCLAIMER"), "example includes full upstream OFL and copyright notice");
        var errors = new List<string>();
        var catalog = FontPackCatalog.Read(new[] { new FontPackSource { PackageId = id, Name = name, ContentRoots = new[] { root } } },
            FontFiles.ReadName, (file, ex) => errors.Add(file + ex)).ToArray();
        check(errors.Count == 0 && catalog.Length == 2, "real example registers two bundled styles without source errors");
        check(catalog.Select(f => f.FaceName).Order().SequenceEqual(new[] { "ABeeZee (Italic)", "ABeeZee (Regular)" }), "real OpenType metadata preserves Regular and Italic");
        check(catalog.All(f => f.SourceModName == name && f.SelectionKey.StartsWith("(Bundled) [" + id + "] ")),
            "real pack provides mod-name labels and stable package-scoped selection keys");
        string temporary = Path.Combine(Path.GetTempPath(), "CustomFonts-example-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        try
        {
            var regular = catalog.Single(f => f.FaceName.EndsWith("(Regular)"));
            var italic = catalog.Single(f => f.FaceName.EndsWith("(Italic)"));
            byte[] primary = FontFiles.Read(regular.Path), fallbackBytes = FontFiles.Read(italic.Path);
            var fallback = new FallbackFontSource { Name = italic.SelectionKey, Path = italic.Path, Bytes = fallbackBytes,
                Revision = FallbackFontSource.Hash(fallbackBytes) };
            string bundle = Path.Combine(temporary, "example.bundle");
            File.WriteAllBytes(bundle, UnityFontBundle.Create(primary, new[] { fallback }));
            var imported = UnityFontInspection.Read(bundle);
            check(imported.Length == 2 && imported.Any(f => f.Bytes.SequenceEqual(primary)) && imported.Any(f => f.Bytes.SequenceEqual(fallbackBytes)),
                "real pack primary and fallback bytes survive Unity import serialization");
            var cache = new FontContentCache<object>();
            int loads = 0;
            var first = cache.Get(regular.Path, (bytes, face, revision) => { loads++; return new object(); }, _ => true);
            var again = cache.Get(regular.Path, (bytes, face, revision) => { loads++; return new object(); }, _ => true);
            check(ReferenceEquals(first, again) && loads == 1, "real pack file content loads once and reuses its cached registration");
        }
        finally { Directory.Delete(temporary, true); }
    }
}
