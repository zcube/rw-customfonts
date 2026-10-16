using CustomFonts;
using System.Security.Cryptography;

internal static class FontDistributionValidation
{
    internal static int Validate(string directory, Action<bool, string> check)
    {
        var catalog = BundledFontCatalog.Read(directory).ToArray();
        check(catalog.Length == 2, "loose catalog has both Sarasa UI J/K fonts");
        check(Directory.EnumerateFiles(directory).Where(FontFiles.Supported).Select(Path.GetFileName).OrderBy(n => n)
            .SequenceEqual(catalog.Select(entry => Path.GetFileName(entry.Path)).OrderBy(n => n)),
            "distributed font files exactly match the OFL catalog");
        check(!typeof(BundledFontData).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Any(field => field.FieldType == typeof(byte[])), "catalog descriptors retain no font payloads");
        string notices = Path.Combine(directory, "..", "About", "FontLicenses");
        string mapping = File.ReadAllText(Path.Combine(notices, "fonts.csv"));
        foreach (var entry in catalog)
        {
            byte[] bytes = FontFiles.Read(entry.Path);
            string hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            check(bytes.Length == entry.Length && hash == entry.Sha256 && mapping.Contains(hash), "exact original and licensed payload: " + entry.Name);
            check(OpenTypeNames.Read(bytes) == entry.FaceName, "portable old selection name: " + entry.FaceName);
            string notice = File.ReadAllText(Path.Combine(notices, Path.GetFileName(entry.Path) + ".LICENSE.txt"));
            check(notice.Contains("SIL Open Font License v1.1") && notice.Contains("Permission & Conditions"), "bundled font retains OFL license: " + entry.Name);
            check(notice.Contains(hash), "per-file notice identifies payload: " + entry.Name);
        }
        check(!BundledFontCatalog.Read(Path.Combine(directory, "not-existing")).Any(), "missing loose font directory is harmless");
        return catalog.Length;
    }
}
