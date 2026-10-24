using CustomFonts;

internal static class FontSourceSnapshotValidation
{
    internal static void Run(string root, Action<bool, string> check)
    {
        string folder = Path.Combine(root, "snapshot");
        string nested = Path.Combine(folder, "nested");
        Directory.CreateDirectory(nested);
        string face = Path.Combine(folder, "face.ttf");
        string Capture(bool recursive = true) => FontSourceSnapshot.Capture(new[] { new FontScanLocation(folder, recursive) });
        string empty = Capture(), shallowEmpty = Capture(false);
        File.WriteAllText(Path.Combine(folder, "notice.txt"), "ignored");
        check(empty == Capture(), "snapshot ignores non-font files");
        File.WriteAllText(face, "metadata only; no valid font payload needed");
        string added = Capture();
        check(added != empty && added == Capture(), "snapshot detects added fonts and remains stable");
        DateTime stamp = File.GetLastWriteTimeUtc(face);
        File.SetLastWriteTimeUtc(face, stamp.AddSeconds(2));
        string touched = Capture();
        check(touched != added, "snapshot detects modification without a length change");
        File.AppendAllText(face, "more");
        File.SetLastWriteTimeUtc(face, stamp.AddSeconds(2));
        check(touched != Capture(), "snapshot detects size changes independently of timestamp");
        string shallow = Capture(false), deep = Capture();
        File.WriteAllText(Path.Combine(nested, "extra.OTF"), "font");
        check(shallow == Capture(false) && deep != Capture(), "snapshot respects folder recursion");
        File.Delete(face);
        check(shallow != Capture(false) && shallowEmpty == Capture(false),
            "snapshot detects removed fonts");
        var missing = new[] { new FontScanLocation(Path.Combine(folder, "missing"), true) };
        string absent = FontSourceSnapshot.Capture(missing);
        check(absent == FontSourceSnapshot.Capture(missing), "missing source snapshots are stable");
        Directory.CreateDirectory(missing[0].Path);
        check(absent != FontSourceSnapshot.Capture(missing), "snapshot detects a newly available source");
        var unresolved = new[] { new FontScanLocation("foreign source", null, true) };
        check(FontSourceSnapshot.Capture(unresolved) == FontSourceSnapshot.Capture(unresolved),
            "unresolved portable sources can be checked without throwing");
        check(FontSourceSnapshot.Capture(unresolved) != FontSourceSnapshot.Capture(new[] { new FontScanLocation("other source", null, true) }),
            "snapshot includes source identity");
    }
}
