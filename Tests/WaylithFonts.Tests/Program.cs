using System.Text;
using CustomFonts;

if (args.Length == 2 && args[0] == "--validate-fonts")
{
    int count = FontDistributionValidation.Validate(args[1], (valid, message) => { if (!valid) throw new InvalidDataException(message); });
    Console.WriteLine($"Verified {count} loose font files, original hashes, portable names and license notices.");
    return;
}

if (args.Length > 0 && args[0] == "--benchmark")
{
    StartupBenchmark.Run(args.Skip(1).ToArray());
    return;
}

int assertions = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
    assertions++;
}
void Reject(Action action, string name)
{
    try { action(); } catch (InvalidDataException) { assertions++; return; }
    throw new Exception("FAIL: " + name);
}
void RejectWith<T>(Action action, string name) where T : Exception
{
    try { action(); } catch (T) { assertions++; return; }
    throw new Exception("FAIL: " + name);
}
void U16(byte[] b, int at, int v) { b[at] = (byte)(v >> 8); b[at + 1] = (byte)v; }
void U32(byte[] b, int at, uint v) { U16(b, at, (int)(v >> 16)); U16(b, at + 2, (int)v); }
Check(FontVerificationOptions.TryGet(new[] { "game", "-customfonts-verify=D:/개인 Fonts" }, "customfonts-verify", out string verificationPath)
    && verificationPath == "D:/개인 Fonts", "verification options preserve Unicode paths with spaces on old Unity");
Check(FontVerificationOptions.TryGet(new[] { "-customfonts-verify=\"D:/개인 Fonts\"" }, "customfonts-verify", out verificationPath)
    && verificationPath == "D:/개인 Fonts", "verification options accept quoted paths");
Check(!FontVerificationOptions.TryGet(new[] { "-other-customfonts-verify=D:/Fonts" }, "customfonts-verify", out _), "verification remains opt-in with exact option name");
byte[] Font(bool collection = false)
{
    var names = new[] { "Old Family", "Old Style", "테스트 Font", "Regular" };
    byte[][] text = names.Select(Encoding.BigEndianUnicode.GetBytes).ToArray();
    int face = collection ? 16 : 0, table = face + 28, stringOffset = 6 + 12 * text.Length;
    byte[] b = new byte[table + stringOffset + text.Sum(x => x.Length)];
    if (collection) { U32(b, 0, 0x74746366); U32(b, 4, 0x00010000); U32(b, 8, 1); U32(b, 12, 16); }
    U32(b, face, 0x00010000); U16(b, face + 4, 1);
    U32(b, face + 12, 0x6e616d65); U32(b, face + 20, (uint)table); U32(b, face + 24, (uint)(b.Length - table));
    U16(b, table + 2, 4); U16(b, table + 4, stringOffset);
    int cursor = 0;
    for (int i = 0; i < text.Length; i++)
    {
        int at = table + 6 + i * 12;
        U16(b, at, 3); U16(b, at + 2, 1); U16(b, at + 4, 0x409);
        U16(b, at + 6, new[] { 1, 2, 16, 17 }[i]); U16(b, at + 8, text[i].Length); U16(b, at + 10, cursor);
        text[i].CopyTo(b, table + stringOffset + cursor); cursor += text[i].Length;
    }
    return b;
}
Check(OpenTypeNames.Read(Font()) == "테스트 Font (Regular)", "Unicode typographic names");
Check(OpenTypeNames.Read(Font(true)) == "테스트 Font (Regular)", "TTC first face and absolute table offsets");
foreach (bool collection in new[] { false, true })
{
    byte[] font = Font(collection);
    using var stream = new CountingStream(new byte[4 * 1024 * 1024]);
    font.CopyTo(stream.GetBuffer(), 31);
    Check(OpenTypeNames.Read(stream, 31, stream.Length - 31) == "테스트 Font (Regular)",
        "bounded embedded font stream: " + collection);
    Check(stream.BytesRead < 1024, "catalog reads metadata rather than 4 MiB payload: " + collection);
    Reject(() => OpenTypeNames.Read(stream, 31, font.Length - 2), "embedded name table cannot cross font boundary");
}
var standalone = FontCollections.FirstFace(Font(true));
Check(standalone[0] == 0 && standalone[1] == 1 && OpenTypeNames.Read(standalone) == "테스트 Font (Regular)", "TTC repacked as standalone SFNT for Waylith registration");
var ordinary = Font();
Check(ReferenceEquals(ordinary, FontCollections.FirstFace(ordinary)), "standalone font bytes remain unchanged");
Reject(() => FontCollections.FirstFace(Font(true)[..^2]), "truncated collection table rejected");
var otf = Font(); U32(otf, 0, 0x4f54544f);
Check(OpenTypeNames.Read(otf) == "테스트 Font (Regular)", "OTF name table");
string cjk = "汉漢あア한ㄱ㐀𠮷𠀀";
Check(Enumerable.Range(0, cjk.Length).Select(i => UnicodeScalars.At(cjk, i)).Where(i => i >= 0)
    .SequenceEqual(cjk.EnumerateRunes().Select(r => r.Value)), "CJK BMP and supplementary scalar decoding");
Check(UnicodeScalars.At("𠮷", 0) == 0x20bb7 && UnicodeScalars.At("𠮷", 1) == -1, "one glyph per surrogate pair");
Check(UnicodeScalars.At("\ud840x\udfb7", 0) == 0xfffd && UnicodeScalars.At("\ud840x\udfb7", 2) == 0xfffd, "isolated surrogate replacement");
var mixed = Font();
U16(mixed, 28 + 6 + 2 * 12, 1); U16(mixed, 28 + 6 + 2 * 12 + 2, 1);
Check(OpenTypeNames.Read(mixed) == "Old Family (Regular)", "ignore legacy Japanese Mac bytes instead of misdecoding as Latin1");
var macName = Font();
for (int i = 0; i < 4; i++) U16(macName, 28 + 6 + i * 12, 2);
int macEntry = 28 + 6 + 2 * 12;
U16(macName, macEntry, 1); U16(macName, macEntry + 2, 0); U16(macName, macEntry + 8, 4);
int macString = 28 + 54 + ((macName[macEntry + 10] << 8) | macName[macEntry + 11]);
new byte[] { 67, 97, 102, 0x8e }.CopyTo(macName, macString);
Check(OpenTypeNames.Read(macName) == "Café (Regular)", "Mac Roman names decode without engine or code-page providers");
Reject(() => OpenTypeNames.Read(new byte[3]), "truncated header");
Reject(() => OpenTypeNames.Read(Encoding.UTF8.GetBytes("not a font file")), "invalid signature");
var bad = Font(); U32(bad, 20, (uint)(bad.Length - 1));
Reject(() => OpenTypeNames.Read(bad), "out of bounds table");
bad = Font(); U16(bad, 28 + 6 + 10, 65535);
Reject(() => OpenTypeNames.Read(bad), "out of bounds name string");

byte[] CoverageFont(uint scalar, bool supplementary, bool blank = true)
{
    var head = new byte[54];
    var maxp = new byte[6]; U16(maxp, 4, 2);
    var loca = new byte[6]; U16(loca, 4, 5);
    var glyf = new byte[10]; U16(glyf, 0, blank ? 0 : 1);
    var cmap = new byte[supplementary ? 40 : 44];
    U16(cmap, 2, 1); U16(cmap, 4, 3); U16(cmap, 6, supplementary ? 10 : 1); U32(cmap, 8, 12);
    if (supplementary)
    {
        U16(cmap, 12, 12); U32(cmap, 16, 28); U32(cmap, 24, 1);
        U32(cmap, 28, scalar); U32(cmap, 32, scalar); U32(cmap, 36, 1);
    }
    else
    {
        U16(cmap, 12, 4); U16(cmap, 14, 32); U16(cmap, 18, 4);
        U16(cmap, 26, (int)scalar); U16(cmap, 28, 0xffff);
        U16(cmap, 32, (int)scalar); U16(cmap, 34, 0xffff);
        U16(cmap, 36, (1 - (int)scalar) & 0xffff); U16(cmap, 38, 1);
    }
    var tables = new[] { (0x68656164u, head), (0x6d617870u, maxp), (0x6c6f6361u, loca), (0x676c7966u, glyf), (0x636d6170u, cmap) };
    byte[] file = new byte[12 + tables.Length * 16 + tables.Sum(t => t.Item2.Length)];
    U32(file, 0, 0x00010000); U16(file, 4, tables.Length);
    int offset = 12 + tables.Length * 16;
    for (int i = 0; i < tables.Length; i++)
    {
        U32(file, 12 + i * 16, tables[i].Item1); U32(file, 20 + i * 16, (uint)offset);
        U32(file, 24 + i * 16, (uint)tables[i].Item2.Length);
        tables[i].Item2.CopyTo(file, offset); offset += tables[i].Item2.Length;
    }
    return file;
}
Check(FontCoverage.EmptyCjk(CoverageFont(0x5c71, false)).SequenceEqual(new uint[] { 0x5c71, 0x5c71, 0 }), "exclude mapped empty Han glyph for fallback");
Check(FontCoverage.EmptyCjk(CoverageFont(0x20000, true)).SequenceEqual(new uint[] { 0x20000, 0x20000, 0 }), "supplementary cmap coverage");
Check(FontCoverage.EmptyCjk(CoverageFont(0x5c71, false, false)).Length == 0, "preserve nonempty primary glyph");
Check(FontCoverage.EmptyCjk(CoverageFont(0x3000, false)).Length == 0, "preserve ideographic space");
Check(new uint[] { 0x302a, 0x302e, 0x3099, 0x3164, 0xffa0 }.All(c => FontCoverage.EmptyCjk(CoverageFont(c, false)).Length == 0), "preserve combining marks and Hangul fillers");
Reject(() => FontCoverage.EmptyCjk(CoverageFont(0x5c71, false)[..^2]), "truncated coverage table");

string root = Path.Combine(Path.GetTempPath(), "CustomFonts-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(Path.Combine(root, "한글 Fonts", "nested"));
try
{
    string packRoot = Path.Combine(root, "pack");
    string versionRoot = Path.Combine(packRoot, "1.7");
    Directory.CreateDirectory(Path.Combine(packRoot, "Fonts", "nested"));
    Directory.CreateDirectory(Path.Combine(versionRoot, "Fonts"));
    string baseFace = Path.Combine(packRoot, "Fonts", "face.ttf");
    string versionFace = Path.Combine(versionRoot, "Fonts", "face.ttf");
    File.WriteAllBytes(baseFace, Font());
    File.WriteAllBytes(versionFace, Font());
    File.WriteAllBytes(Path.Combine(packRoot, "Fonts", "nested", "extra.TTC"), Font(true));
    File.WriteAllText(Path.Combine(packRoot, "Fonts", "broken.otf"), "invalid");
    File.WriteAllText(Path.Combine(packRoot, "Fonts", "LICENSE.txt"), "license");
    var packErrors = new List<string>();
    var packReads = new List<string>();
    var packScans = new List<(string Id, string Name, int Count)>();
    var pack = new FontPackSource { PackageId = "Author.Pack", Name = "한글 폰트 팩", ContentRoots = new[] { versionRoot, packRoot } };
    var packFonts = FontPackCatalog.Read(new[] { pack }, file =>
    {
        packReads.Add(file);
        return FontFiles.ReadName(file);
    }, (file, _) => packErrors.Add(file), (source, count) => packScans.Add((source.PackageId, source.Name, count))).ToArray();
    Check(packFonts.Length == 1 && packFonts[0].Path == versionFace, "pack uses highest-priority content root and deduplicates same face");
    Check(!packReads.Contains(baseFace) && packReads.Any(f => f.EndsWith("extra.TTC")), "pack shadows relative files and recursively scans collection files");
    Check(packErrors.Count == 1 && packErrors[0].EndsWith("broken.otf"), "invalid pack font does not hide valid siblings or parse license files");
    Check(packFonts[0].SelectionKey == "(Bundled) [author.pack] 테스트 Font (Regular)"
        && packFonts[0].SourceModName == "한글 폰트 팩", "stable package identity is separate from displayed mod name");
    Check(packFonts[0].Size == 16 && packFonts[0].RenderingMode == 0, "pack uses bundled import defaults");
    Check(packScans.Single() == (pack.PackageId, pack.Name, 1), "scan summary counts selectable faces after overrides, duplicates and invalid files");
    var otherPack = new FontPackSource { PackageId = "Author.Other", Name = pack.Name, ContentRoots = new[] { versionRoot } };
    var isolated = FontPackCatalog.Read(new[] { pack, otherPack }, FontFiles.ReadName, (_, _) => { }).ToArray();
    Check(isolated.Length == 2 && isolated[0].SelectionKey != isolated[1].SelectionKey, "same face and display name from different mods remain separately selectable");
    pack.Name = "Renamed Pack";
    Check(FontPackCatalog.Read(new[] { pack }, FontFiles.ReadName, (_, _) => { }).Single().SelectionKey == packFonts[0].SelectionKey,
        "renaming mod does not change saved font selection");
    Check(!FontPackCatalog.Read(new[] { new FontPackSource { PackageId = "empty", ContentRoots = new[] { root } } },
        FontFiles.ReadName, (_, _) => throw new Exception("missing optional Fonts folder"),
        (source, count) => packScans.Add((source.PackageId, source.Name, count))).Any(), "mods without Fonts are ignored");
    Check(packScans.Last().Id == "empty" && packScans.Last().Count == 0, "scanned mods without Fonts remain visible with zero fonts");
    string folder = Path.Combine(root, "한글 Fonts");
    string path = Path.Combine(folder, "custom font.TTF");
    File.WriteAllBytes(path, Font());
    File.WriteAllBytes(Path.Combine(folder, "nested", "other.otf"), Font());
    File.WriteAllText(Path.Combine(folder, "readme.txt"), "not a font");
    Check(FontFiles.Normalize("\"한글 Fonts/custom font.TTF\"", root) == path, "quoted relative Unicode path");
    Check(FontFiles.Normalize(@"한글 Fonts\custom font.TTF", root) == path, "Windows relative separators resolve on current OS");
    Check(FontFiles.NormalizeSeparators(@"Fonts\한글/mixed.ttf", true) == @"Fonts\한글\mixed.ttf", "mixed relative separators on Windows");
    Check(FontFiles.NormalizeSeparators(@"Fonts\한글/mixed.ttf", false) == "Fonts/한글/mixed.ttf", "mixed relative separators on Linux/macOS");
    Check(FontFiles.NormalizeSeparators("C:/Fonts/font.ttf", true) == @"C:\Fonts\font.ttf", "forward separators in Windows drive path");
    Check(FontFiles.NormalizeSeparators("//server/fonts/font.ttf", true) == @"\\server\fonts\font.ttf", "forward separators in Windows UNC path");
    Check(FontFiles.NormalizeSeparators(@"~/Fonts\font.ttf", false) == "~/Fonts/font.ttf", "mixed separators in Unix home path");
    Check(FontFiles.NormalizeSeparators("/Library/Fonts", false) == "/Library/Fonts", "macOS absolute path retained");
    Check(FontFiles.NormalizeSeparators("/run/host/fonts", false) == "/run/host/fonts", "Steam Deck absolute path retained");
    foreach (string foreign in new[] { @"C:\Windows\Fonts", "C:/Windows/Fonts", @"\\server\fonts", "//server/fonts", @"\Windows\Fonts" })
        RejectWith<NotSupportedException>(() => FontFiles.NormalizeSeparators(foreign, false), "Windows absolute path is not reinterpreted on Unix: " + foreign);
    foreach (string foreign in new[] { "/usr/share/fonts", "/Library/Fonts", "/run/host/user-fonts" })
        RejectWith<NotSupportedException>(() => FontFiles.NormalizeSeparators(foreign, true), "Unix absolute path is not mapped to current Windows drive: " + foreign);
    RejectWith<ArgumentException>(() => FontFiles.NormalizeSeparators("C:Fonts", true), "drive-relative path cannot depend on another machine's drive working directory");
    Check(FontFiles.Normalize(path, root) == path, "absolute path");
    Environment.SetEnvironmentVariable("CUSTOMFONTS_TEST_PATH", folder);
    Check(FontFiles.Normalize("%CUSTOMFONTS_TEST_PATH%/custom font.TTF", root) == path, "environment expansion");
    Check(FontFiles.Normalize("$CUSTOMFONTS_TEST_PATH/custom font.TTF", root) == path, "Unix environment expansion");
    Check(FontFiles.Normalize("${CUSTOMFONTS_TEST_PATH}/custom font.TTF", root) == path, "braced Unix environment expansion");
    Check(FontFiles.SameLocation(@"한글 Fonts\custom font.TTF", path, root), "portable relative expression deduplicates against absolute file");
    Check(FontFiles.SameLocation("$CUSTOMFONTS_TEST_PATH/custom font.TTF", path, root), "environment expression deduplicates without rewriting saved text");
    Check(!FontFiles.SameLocation("missing/other.ttf", path, root), "distinct portable paths are not duplicates");
    Check(FontFiles.SameLocation("/another-os/fonts", "/another-os/fonts", root), "foreign expressions can still be compared without failing");
    Check(FontFiles.ExpandVariables("$MISSING/${MISSING}/%MISSING%", _ => null) == "$MISSING/${MISSING}/%MISSING%", "unknown variables preserved");
    Check(FontFiles.ExpandVariables("$FONT", _ => "fonts/$literal%name%") == "fonts/$literal%name%", "expanded path not recursively interpreted");
    Check(FontFiles.Normalize("~/font.ttf", root) == Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "font.ttf"), "home expansion");
    Check(FontFiles.Enumerate(folder, false).Count(FontFiles.Supported) == 1, "non-recursive folder");
    Check(FontFiles.Enumerate(folder, true).Count(FontFiles.Supported) == 2, "recursive folder and extension filtering");
    Check(FontFiles.Enumerate(path, false).Single() == path, "explicit file");
    Check(OpenTypeNames.Read(FontFiles.Read(path)) == "테스트 Font (Regular)", "file content");
    Check(FontFiles.ReadName(path) == "테스트 Font (Regular)", "metadata-only file catalog");
    Reject(() => FontFiles.Enumerate(Path.Combine(folder, "readme.txt"), false).ToList(), "unsupported explicit file");
    Check(FontFiles.Normalize(folder + Path.DirectorySeparatorChar, root) == folder, "folder trailing separator normalized");
    var catalog = new Dictionary<string, string>();
    var sourceErrors = new List<string>();
    string nestedFont = Path.Combine(folder, "nested", "other.otf");
    void Populate(IEnumerable<string> system, IEnumerable<string> custom, bool recursive)
    {
        catalog.Clear(); sourceErrors.Clear();
        FontSourceCatalog.Populate(catalog, system, custom, root, recursive,
            FontFiles.ReadName, (source, _) => sourceErrors.Add(source));
    }
    Populate(new[] { path, path }, new[] { folder, nestedFont, folder }, true);
    Check(catalog.Count == 1 && sourceErrors.Count == 0, "system and configured files collapse to one portable family");
    Check(catalog["테스트 Font"] == path, "equivalent configured faces use source order and sorted paths");
    Check(FontSourceCatalog.DisplayName("(File) 테스트 Font (Regular) [" + path + "]") == "테스트 Font", "legacy file label displays portable family");
    Check(FontSourceCatalog.DisplayName("Legacy OS family") == "Legacy OS family", "existing family selection remains unchanged");
    Check(FontSourceCatalog.Family("Family (Example) (Regular)") == "Family (Example)", "metadata preserves parentheses in family names");
    Check(FontSourceCatalog.FindKey(catalog, "테스트 Font") == "테스트 Font", "legacy Unity family retains its identity");
    Check(FontSourceCatalog.FindKey(catalog, "테스트 Font (Regular)") == "테스트 Font", "old face selection migrates to family");
    Check(FontSourceCatalog.FindKey(catalog, "테스트 Font (Bold)") == "테스트 Font", "old styled face migrates to family default");
    Check(FontSourceCatalog.FindKey(catalog, "테스트 font") == "테스트 Font", "family resolution ignores case");
    string oldFileKey = "(File) Old engine name [" + path + "]";
    Check(FontSourceCatalog.FindKey(catalog, oldFileKey) == "테스트 Font", "unknown old engine file name migrates through configured path");
    string movedSelection = "(File) 테스트 Font (Regular) [/old/other-machine/font.ttf]";
    Check(FontSourceCatalog.FindKey(catalog, movedSelection) == "테스트 Font", "private selection migrates by family despite moved file or OS");
    Populate(Array.Empty<string>(), new[] { folder }, false);
    Check(catalog.Count == 1 && catalog.Keys.Single() == "테스트 Font", "system exclusion retains configured families");
    Check(FontSourceCatalog.FindKey(catalog, "테스트 Font") == "테스트 Font", "family can resolve from a configured folder without OS installation");
    Populate(Array.Empty<string>(), new[] { folder, nestedFont }, false);
    Check(catalog.Count == 1, "explicit nested file deduplicates by family without recursion");
    string invalidFont = Path.Combine(folder, "invalid.ttf");
    File.WriteAllText(invalidFont, "invalid font content");
    Populate(Array.Empty<string>(), new[] { "missing.ttf", folder }, true);
    Check(catalog.Count == 1 && sourceErrors.Count == 2, "missing source and invalid font do not hide valid siblings");
    File.Delete(invalidFont);
    string[] sharedPaths = (Path.DirectorySeparatorChar == '\\'
        ? new[] { "/usr/share/fonts", "/Library/Fonts", "/run/host/fonts" }
        : new[] { @"C:\Windows\Fonts", "C:/Windows/Fonts", @"\\server\fonts" })
        .Concat(new[] { "another-machine-missing", @"한글 Fonts\custom font.TTF" }).ToArray();
    string[] savedSharedPaths = sharedPaths.ToArray();
    Populate(Array.Empty<string>(), sharedPaths, false);
    Check(catalog.Count == 1 && catalog["테스트 Font"] == path && sourceErrors.Count == 4,
        "shared foreign/missing roots are skipped while valid backslash relative source is scanned");
    Check(sharedPaths.SequenceEqual(savedSharedPaths), "scanning preserves shared path expressions for other machines");
    FontSourceCatalog.Populate(catalog = new(), Array.Empty<string>(), new[] { nestedFont, path }, root, false,
        f => f == nestedFont ? throw new UnauthorizedAccessException("test unreadable font") : FontFiles.ReadName(f),
        (_, _) => { });
    Check(catalog.Count == 1 && catalog["테스트 Font"] == path, "unreadable font does not prevent another source from resolving");
    // The family resolver must choose Regular over lexically earlier Bold, regardless
    // of source, and prefer configured sources when both contain a Regular face.
    FontSourceCatalog.Populate(catalog = new(), new[] { nestedFont }, new[] { path }, root, false,
        f => f == path ? "테스트 Font (Bold)" : "테스트 Font (Regular)", (_, _) => { });
    Check(catalog["테스트 Font"] == nestedFont, "OS Regular precedes configured Bold");
    FontSourceCatalog.Populate(catalog = new(), new[] { path }, new[] { nestedFont }, root, false,
        _ => "테스트 Font (Regular)", (_, _) => { });
    Check(catalog["테스트 Font"] == nestedFont, "configured Regular precedes OS Regular");
    FontSourceCatalog.Populate(catalog = new(), new[] { path, nestedFont }, Array.Empty<string>(), root, false,
        f => f == path ? "테스트 Font (Bold)" : "테스트 Font (Normal)", (_, _) => { });
    Check(catalog["테스트 Font"] == nestedFont, "Normal is a regular family face");
    FontSourceCatalog.Populate(catalog = new(), new[] { path, nestedFont }, Array.Empty<string>(), root, false,
        f => f == path ? "Family (Example) (Bold)" : "family (example) (Regular)", (_, _) => { });
    Check(catalog.Count == 1 && FontSourceCatalog.FindKey(catalog, "Family (Example)") == "Family (Example)", "case-insensitive families preserve parenthesized names");
    Populate(Array.Empty<string>(), Array.Empty<string>(), true);
    Check(catalog.Count == 0, "removing sources clears selectable family choices");
    Check(FontSourceCatalog.PortableSelection(catalog, movedSelection) == "테스트 Font", "unavailable old private selection drops machine path and retains family");
    Check(FontSourceCatalog.PortableSelection(catalog, "Missing family") == "Missing family", "unavailable family remains portable for another OS or refresh");
    Check(FontSourceCatalog.PortableSelection(catalog, "Family (Example)") == "Family (Example)", "unavailable new family with parentheses retains exact identity");
    Check(FontSourceCatalog.FindKey(catalog, oldFileKey) == null, "stale path is never opened outside configured sources");

    string[] legacyFallbacks = { "HangeulNuri", "MaruBuri", "HangeulNuri", "Default", "" };
    Check(FontFallbackLists.Migrate(null, legacyFallbacks).SequenceEqual(new[] { "HangeulNuri", "MaruBuri" }), "legacy global fallback migrates to ordered per-tab lists");
    Check(FontFallbackLists.Migrate(Array.Empty<string>(), legacyFallbacks).Count == 0, "explicit empty per-tab fallback does not inherit legacy settings");
    var migratedA = FontFallbackLists.Migrate(null, legacyFallbacks); var migratedB = FontFallbackLists.Migrate(null, legacyFallbacks);
    migratedA.Reverse(); Check(migratedB[0] == "HangeulNuri", "migrated tab lists are independent copies");
    Check(FontFallbackLists.Union(new[] { "A", "B" }, new[] { "b", "C" }, new[] { "D", "A" }).SequenceEqual(new[] { "A", "B", "C", "D" }), "union preserves current priority and deduplicates families ignoring case");
    Check(FontFallbackLists.NamesKey(new[] { "A|B", "C" }) != FontFallbackLists.NamesKey(new[] { "A", "B|C" }), "ordered fallback signatures encode name boundaries");
    var policies = new[] { new FallbackFontSource { Revision = "A" }, new FallbackFontSource { Revision = "B" } };
    Check(FontFallbackLists.PolicyKey(policies) != FontFallbackLists.PolicyKey(policies.Reverse()), "different fallback priority has a different font cache identity");
    Check(FontFallbackLists.PolicyKey(Array.Empty<FallbackFontSource>()) == "", "empty fallback preserves bare content identity");
    var scopedCache = new FontContentCache<object>(); int scopedCreated = 0;
    object ScopedCreate(byte[] bytes, string name, string revision) { scopedCreated++; return new object(); }
    var policyA = scopedCache.Get(path, ScopedCreate, _ => true, "A");
    var policyB = scopedCache.Get(path, ScopedCreate, _ => true, "B");
    Check(!ReferenceEquals(policyA, policyB) && scopedCreated == 2, "same primary with different tab fallback policies creates independent fonts");
    Check(ReferenceEquals(policyA, scopedCache.Get(nestedFont, ScopedCreate, _ => true, "A")), "matching policies share content across paths and UI sizes");
    scopedCache.Invalidate();
    Check(ReferenceEquals(policyB, scopedCache.Get(path, ScopedCreate, _ => true, "B")), "refresh reuses unchanged scoped font variants");
    scopedCache.Get(path, ScopedCreate, _ => true, "A");
    scopedCache.Get(nestedFont, ScopedCreate, _ => true, "A");
    scopedCache.Forget(policyA);
    Check(scopedCache.Fonts.Count == 1 && scopedCache.RevisionCount == 1, "retirement removes all aliases and revisions of the released font");
    Check(ReferenceEquals(policyB, scopedCache.Get(path, ScopedCreate, _ => true, "B")), "retirement preserves another active fallback policy");
    Check(!ReferenceEquals(policyA, scopedCache.Get(path, ScopedCreate, _ => true, "A")) && scopedCreated == 3, "retired font content can be loaded again without stale cache entries");

    var sizedCache = new FontContentCache<object>();
    var tiny = sizedCache.Get(path, ScopedCreate, _ => true, "A:px=12");
    var small = sizedCache.Get(path, ScopedCreate, _ => true, "A:px=14");
    Check(!ReferenceEquals(tiny, small), "different registered UI sizes keep separate renderer fonts");
    Check(ReferenceEquals(small, sizedCache.Get(nestedFont, ScopedCreate, _ => true, "A:px=14")), "same content, fallback policy and pixel size reuse one font across paths");
    sizedCache.Forget(tiny);
    Check(sizedCache.Fonts.Count == 1 && ReferenceEquals(small, sizedCache.Get(path, ScopedCreate, _ => true, "A:px=14")), "retiring a size variant preserves another active size");
    sizedCache.Invalidate();
    Check(ReferenceEquals(small, sizedCache.Get(path, ScopedCreate, _ => true, "A:px=14")), "refresh preserves unchanged registered size variants");
    sizedCache.Forget(small);
    Check(sizedCache.Fonts.Count == 0 && sizedCache.RevisionCount == 0, "retiring all sizes clears their content and path aliases");

    var cache = new FontContentCache<object>();
    int created = 0;
    byte[] payload = null;
    object Create(byte[] bytes, string name, string hash) { created++; payload = bytes; return new object(); }
    object first = cache.Get(path, Create, _ => true);
    object copy = cache.Get(nestedFont, Create, _ => true);
    Check(ReferenceEquals(first, copy) && created == 1, "system/manual paths with equal content share one font creation");
    cache.Invalidate();
    Check(ReferenceEquals(first, cache.Get(path, Create, _ => true)) && created == 1, "refresh reuses unchanged file content");
    byte[] replacement = Font(); replacement[^1] ^= 1; File.WriteAllBytes(path, replacement);
    cache.Invalidate();
    Check(!ReferenceEquals(first, cache.Get(path, Create, _ => true)) && created == 2, "refresh detects changed font content");
    cache.Forget(copy);
    Check(!cache.Contains(first) && cache.Fonts.Count == 1, "released content font is removed from shared cache");
    Check(!ReferenceEquals(copy, cache.Get(nestedFont, Create, _ => true)) && created == 3, "released cached font can be recreated from another path");
    string collectionPath = Path.Combine(root, "collection.ttc"); File.WriteAllBytes(collectionPath, Font(true));
    cache.Get(collectionPath, Create, _ => true);
    Check(payload[0] == 0 && payload[1] == 1 && OpenTypeNames.Read(payload) == "테스트 Font (Regular)", "all engines receive standalone first-face bytes for TTC");
    File.WriteAllBytes(path, Font());
    string imported = Path.Combine(root, "import.bundle");
    byte[] firstImport = UnityFontBundle.Create(FontFiles.Read(path));
    File.WriteAllBytes(imported, firstImport);
    var importedFonts = UnityFontInspection.Read(imported).ToArray();
    Check(importedFonts.Length == 1 && importedFonts[0].Name == "CustomFontsFile", "Unity import includes exactly one font payload");
    Check(importedFonts[0].Bytes.SequenceEqual(FontFiles.Read(path)), "Unity import preserves exact Unicode-source font bytes");
    Check(firstImport.Length < 64 * 1024 + new FileInfo(path).Length, "Unity font template retains no bundled font payloads");
    Check(!firstImport.SequenceEqual(UnityFontBundle.Create(FontFiles.Read(path))), "concurrent Unity imports get separate bundle identities");
    byte[] fallbackData = Font(); fallbackData[^1] ^= 1;
    var fallbackSource = new FallbackFontSource { Name = "private fallback", Path = nestedFont, Bytes = fallbackData, Revision = FallbackFontSource.Hash(fallbackData) };
    string fallbackBundle = Path.Combine(root, "fallback.bundle");
    File.WriteAllBytes(fallbackBundle, UnityFontBundle.Create(FontFiles.Read(path), new[] { fallbackSource, fallbackSource }));
    var fallbackCatalog = UnityFontInspection.Read(fallbackBundle).ToArray();
    Check(fallbackCatalog.Length == 2 && fallbackCatalog.Any(f => f.Name != "CustomFontsFile"), "Unity embeds only primary and deduplicated defined fallback");
    Check(fallbackCatalog.Single(f => f.Name != "CustomFontsFile").Bytes.SequenceEqual(fallbackData), "Unity fallback retains exact private-font bytes");
    File.WriteAllBytes(fallbackBundle, UnityFontBundle.Create(new byte[0], new[] { fallbackSource }, new[] { "Arial" }));
    Check(UnityFontInspection.Read(fallbackBundle).Count() == 1, "default-font wrapper includes only defined fallback data");
    var manyFallbacks = Enumerable.Range(0, 16).Select(i =>
    {
        byte[] data = Font(); data[^1] = (byte)i;
        return new FallbackFontSource { Name = "fallback " + i, Path = nestedFont, Bytes = data, Revision = FallbackFontSource.Hash(data) };
    }).ToArray();
    File.WriteAllBytes(fallbackBundle, UnityFontBundle.Create(FontFiles.Read(path), manyFallbacks));
    Check(UnityFontInspection.Read(fallbackBundle).Count() == 17, "Unity fallback references are not limited by original template font count");
    Check(!FallbackFontSource.WithoutPrimary(new[] { new FallbackFontSource { Bytes = FontFiles.Read(path), Revision = FallbackFontSource.Hash(FontFiles.Read(path)) } }, FontFiles.Read(path)).Any(),
        "primary font cannot reference itself as a fallback");
    byte[] revision = Font(); revision[^1] ^= 1;
    File.WriteAllBytes(imported, UnityFontBundle.Create(revision));
    Check(UnityFontInspection.Read(imported).Single().Bytes.SequenceEqual(revision), "Unity import uses replacement font content");
    string metadataRoot = Path.Combine(root, "metadata-cache");
    var metadata = new FontMetadataCache(metadataRoot);
    Check(metadata.ReadName(path) == FontFiles.ReadName(path) && metadata.Misses == 1, "metadata cache reads source on first lookup");
    metadata.Save();
    metadata = new FontMetadataCache(metadataRoot);
    Check(metadata.ReadName(path) == FontFiles.ReadName(path) && metadata.Hits == 1 && metadata.Misses == 0, "metadata cache survives game restart");
    File.SetLastWriteTimeUtc(path, File.GetLastWriteTimeUtc(path).AddSeconds(2));
    metadata.ReadName(path);
    Check(metadata.Misses == 1, "metadata cache detects modified font revision");
    File.WriteAllBytes(Path.Combine(metadataRoot, "names-v1.bin"), new byte[] { 1, 2 });
    metadata = new FontMetadataCache(metadataRoot);
    Check(metadata.ReadName(path) == FontFiles.ReadName(path) && metadata.Misses == 1, "corrupt name cache falls back to source");
    bool linksCreated = false;
    try
    {
        File.CreateSymbolicLink(Path.Combine(folder, "linked.ttf"), path);
        Directory.CreateSymbolicLink(Path.Combine(folder, "nested", "loop"), folder);
        linksCreated = true;
    }
    catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException || ex is PlatformNotSupportedException)
    {
        Console.WriteLine("SKIP: symbolic link creation unavailable: " + ex.Message);
    }
    if (linksCreated)
    {
        Check(FontFiles.Enumerate(folder, false).Count(FontFiles.Supported) == 2, "font file symlink included");
        Check(FontFiles.Enumerate(folder, true).Count(FontFiles.Supported) == 3, "directory symlink loop skipped");
        Check(FontFiles.ReadName(Path.Combine(folder, "linked.ttf")) == "테스트 Font (Regular)", "linked font metadata readable");
    }
}
finally { Directory.Delete(root, true); }
var xdg = new Dictionary<string, string>
{
    ["XDG_DATA_HOME"] = "/home/한글/custom data/",
    ["XDG_DATA_DIRS"] = "/opt/shared:/srv/fonts-data/:relative::/opt/shared"
};
string Env(string name) => xdg.GetValueOrDefault(name);
Check(AdditionalFontRoots.Get(true, Env).SequenceEqual(new[] { "/home/한글/custom data/fonts", "/opt/shared/fonts", "/srv/fonts-data/fonts" }), "Linux XDG roots, Unicode, multiple dirs and duplicate filtering");
Check(!AdditionalFontRoots.Get(false, Env).Any(), "Windows/macOS do not scan Linux XDG roots");
Check(!AdditionalFontRoots.Get(true, _ => null).Any(), "unset XDG roots reuse engine defaults");
Check(!AdditionalFontRoots.Get(true, _ => "").Any(), "empty XDG roots reuse engine defaults");
Check(!AdditionalFontRoots.Get(true, _ => "relative").Any(), "relative XDG values ignored");
string[] Roots(FontPlatform platform) => SystemFontRoots.Get(platform, "/home/사용자", "C:/Windows", "C:/Users/사용자/AppData/Local", Env)
    .Select(p => p.Replace('\\', '/')).ToArray();
Check(Roots(FontPlatform.Windows).SequenceEqual(new[] { "C:/Windows/Fonts", "C:/Users/사용자/AppData/Local/Microsoft/Windows/Fonts" }), "shared Windows system and per-user roots");
Check(Roots(FontPlatform.MacOS).SequenceEqual(new[] { "/System/Library/Fonts", "/Library/Fonts", "/home/사용자/Library/Fonts" }), "shared macOS system and per-user roots");
Check(Roots(FontPlatform.Linux).SequenceEqual(new[] { "/usr/share/fonts", "/usr/local/share/fonts", "/home/사용자/.fonts", "/home/사용자/.local/share/fonts", "/run/host/fonts", "/run/host/user-fonts", "/home/한글/custom data/fonts", "/opt/shared/fonts", "/srv/fonts-data/fonts" }), "shared Linux user, system, host and XDG roots");
Check(!SystemFontRoots.Get(FontPlatform.Windows, "", "", "", _ => null).Any(), "missing Windows special folders cannot introduce relative scan roots");
if (args.Length > 0)
{
    FontDistributionValidation.Validate(args[0], Check);
    FontPackExampleValidation.Run(Path.Combine(Path.GetFullPath(args[0]), "..", "Examples", "ABeeZeeFontPack"), Check);
}
Console.WriteLine($"PASS: {assertions} font metadata, CJK coverage, Unicode and path assertions");

sealed class CountingStream : MemoryStream
{
    public long BytesRead { get; private set; }
    public CountingStream(byte[] bytes) : base(bytes, 0, bytes.Length, true, true) { }
    public override int Read(Span<byte> buffer)
    {
        int read = base.Read(buffer);
        BytesRead += read;
        return read;
    }
}
