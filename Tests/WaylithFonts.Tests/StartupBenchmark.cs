using System.Diagnostics;
using CustomFonts;

internal static class StartupBenchmark
{
    internal static void Run(string[] args)
    {
        string bundle = args.Length > 0 ? args[0] : "rimfonts";
        var platform = OperatingSystem.IsWindows() ? FontPlatform.Windows : OperatingSystem.IsMacOS() ? FontPlatform.MacOS : FontPlatform.Linux;
        var roots = SystemFontRoots.Get(platform, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.GetFolderPath(Environment.SpecialFolder.Windows), Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Environment.GetEnvironmentVariable);
        var timer = Stopwatch.StartNew();
        var files = roots.Where(Directory.Exists).SelectMany(p => FontFiles.Enumerate(p, true)).Where(FontFiles.Supported)
            .Distinct(FontSourceCatalog.PathComparer).ToArray();
        Console.WriteLine($"System enumeration: {files.Length} files, {timer.Elapsed.TotalMilliseconds:F1} ms");
        for (int run = 0; run < 3; run++)
        {
            timer.Restart();
            int valid = 0;
            foreach (string file in files) { try { FontFiles.ReadName(file); valid++; } catch { } }
            Console.WriteLine($"Uncached names {run + 1}: {valid} files, {timer.Elapsed.TotalMilliseconds:F1} ms");
            timer.Restart();
            var catalog = BundledFontReader.Read(bundle).ToArray();
            Console.WriteLine($"Uncached bundle catalog {run + 1}: {catalog.Length} fonts, {timer.Elapsed.TotalMilliseconds:F1} ms");
            timer.Restart();
            var selected = catalog.First(p => p.Name.Contains("HangeulNuri", StringComparison.OrdinalIgnoreCase));
            byte[] bytes = BundledFontReader.ReadDataFullyUnpacked(bundle, selected);
            Console.WriteLine($"Uncached selected bundle {run + 1}: {bytes.Length} bytes, {timer.Elapsed.TotalMilliseconds:F1} ms");
        }
        string root = Path.Combine(Path.GetTempPath(), "customfonts-benchmark-" + Guid.NewGuid().ToString("N"));
        try
        {
            for (int run = 0; run < 3; run++)
            {
                timer.Restart();
                var cache = new FontMetadataCache(root);
                foreach (string file in files) { try { cache.ReadName(file); } catch { } }
                cache.Save();
                Console.WriteLine($"Persistent names {run + 1}: {timer.Elapsed.TotalMilliseconds:F1} ms, hits={cache.Hits}, misses={cache.Misses}");
                timer.Restart();
                var catalog = cache.ReadBundle(bundle, out bool hit, new[] { "(Bundled) HangeulNuri (Bold)" });
                Console.WriteLine($"Persistent bundle catalog {run + 1}: {timer.Elapsed.TotalMilliseconds:F1} ms, hit={hit}");
                timer.Restart();
                byte[] bytes = cache.ReadBundleData(bundle, catalog.First(p => p.Name.Contains("HangeulNuri", StringComparison.OrdinalIgnoreCase)), out hit);
                Console.WriteLine($"Persistent selected bundle {run + 1}: {timer.Elapsed.TotalMilliseconds:F1} ms, hit={hit}");
            }
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
