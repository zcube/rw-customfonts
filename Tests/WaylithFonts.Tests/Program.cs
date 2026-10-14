using System.Text;
using CustomFonts;

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
void U16(byte[] b, int at, int v) { b[at] = (byte)(v >> 8); b[at + 1] = (byte)v; }
void U32(byte[] b, int at, uint v) { U16(b, at, (int)(v >> 16)); U16(b, at + 2, (int)v); }
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
    string folder = Path.Combine(root, "한글 Fonts");
    string path = Path.Combine(folder, "custom font.TTF");
    File.WriteAllBytes(path, Font());
    File.WriteAllBytes(Path.Combine(folder, "nested", "other.otf"), Font());
    File.WriteAllText(Path.Combine(folder, "readme.txt"), "not a font");
    Check(FontFiles.Normalize("\"한글 Fonts/custom font.TTF\"", root) == path, "quoted relative Unicode path");
    Check(FontFiles.Normalize(path, root) == path, "absolute path");
    Environment.SetEnvironmentVariable("CUSTOMFONTS_TEST_PATH", folder);
    Check(FontFiles.Normalize("%CUSTOMFONTS_TEST_PATH%/custom font.TTF", root) == path, "environment expansion");
    Check(FontFiles.Normalize("$CUSTOMFONTS_TEST_PATH/custom font.TTF", root) == path, "Unix environment expansion");
    Check(FontFiles.Normalize("${CUSTOMFONTS_TEST_PATH}/custom font.TTF", root) == path, "braced Unix environment expansion");
    Check(FontFiles.ExpandVariables("$MISSING/${MISSING}/%MISSING%", _ => null) == "$MISSING/${MISSING}/%MISSING%", "unknown variables preserved");
    Check(FontFiles.ExpandVariables("$FONT", _ => "fonts/$literal%name%") == "fonts/$literal%name%", "expanded path not recursively interpreted");
    Check(FontFiles.Normalize("~/font.ttf", root) == Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "font.ttf"), "home expansion");
    Check(FontFiles.Enumerate(folder, false).Count(FontFiles.Supported) == 1, "non-recursive folder");
    Check(FontFiles.Enumerate(folder, true).Count(FontFiles.Supported) == 2, "recursive folder and extension filtering");
    Check(FontFiles.Enumerate(path, false).Single() == path, "explicit file");
    Check(OpenTypeNames.Read(FontFiles.Read(path)) == "테스트 Font (Regular)", "file content");
    Check(FontFiles.ReadName(path) == "테스트 Font (Regular)", "metadata-only file catalog");
    Reject(() => FontFiles.Enumerate(Path.Combine(folder, "readme.txt"), false).ToList(), "unsupported explicit file");
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
#if BUNDLE_TESTS
if (args.Length > 0)
{
    var catalog = BundledFontReader.Read(args[0]).ToArray();
    Check(catalog.Length == 15, "installed bundle catalog has all 15 fonts");
    Check(!typeof(BundledFontData).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
        .Any(field => field.FieldType == typeof(byte[])), "bundle descriptors retain no font byte arrays");
    foreach (var entry in catalog)
    {
        byte[] bytes = BundledFontReader.ReadData(args[0], entry);
        Check(bytes.Length == entry.DataLength && OpenTypeNames.Read(bytes) == entry.FaceName,
            "selected bundle payload matches metadata: " + entry.Name);
    }
    Console.WriteLine($"Bundle source data deferred: {catalog.Sum(font => (long)font.DataLength):N0} bytes across {catalog.Length} fonts");
}
#endif
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
