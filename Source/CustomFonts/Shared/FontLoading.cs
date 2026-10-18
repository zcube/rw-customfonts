using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using Verse;

namespace CustomFonts
{
    public partial class CustomFonts
    {
        private readonly FontContentCache<Font> _fileFontCache = new FontContentCache<Font>();
        private Dictionary<string, Font> _registeredFileFonts => _fileFontCache.Fonts;
        private readonly Dictionary<string, BundledFontData> _bundledFontSources = new Dictionary<string, BundledFontData>();
        private readonly Dictionary<string, Font> _bundledFontVariants = new Dictionary<string, Font>();
        private readonly Dictionary<string, Font> _scopedDefaultFonts = new Dictionary<string, Font>();
        private sealed class FontPackScan
        {
            internal string PackageId, Name;
            internal int FontCount;
        }
        private readonly List<FontPackScan> _fontPackScans = new List<FontPackScan>();

        private Font LoadFontFile(string path, FallbackFontSource[] fallbacks = null, int pixels = 0)
        {
            var timer = Stopwatch.StartNew();
            fallbacks = fallbacks ?? new FallbackFontSource[0];
            Font font = _fileFontCache.Get(path, (bytes, name, revision) => CreateFileFont(bytes, name, revision, fallbacks, pixels),
                f => f != null, FontFallbackLists.PolicyKey(fallbacks) + FontSizeKey(pixels));
            LogFontTiming("file load", timer, Path.GetFileName(path));
            return font;
        }

        private Font ResolveFileFont(string fontKey, string fontName, FallbackFontSource[] fallbacks, int pixels)
        {
            if (_osFontCache.TryGetValue(fontKey, out Font cached) && cached != null) return cached;
            SetupOSFontPaths();
            string key = FontSourceCatalog.FindKey(OSFontPaths, fontName);
            if (key == null) return null;
            Font font = LoadFontFile(OSFontPaths[key], fallbacks, pixels);
            if (font != null) _osFontCache[fontKey] = font;
            return font;
        }

        public void SetupBundledFonts()
        {
            if (_hasBundledFonts) return;
            var timer = Stopwatch.StartNew();
            _hasBundledFonts = true;
            try
            {
                // Only packs after this mod participate; content discovery happens after mod loading.
                var packs = LoadedModManager.RunningModsListForReading
                    .SkipWhile(mod => mod != _content).Skip(1)
                    .Select(mod => new FontPackSource
                    {
                        PackageId = mod.PackageId, Name = mod.Name,
                        ContentRoots = mod.foldersToLoadDescendingOrder
                    });
                var catalog = BundledFontCatalog.Read(Path.Combine(_content.RootDir, "Fonts"))
                    .Concat(FontPackCatalog.Read(packs, MetadataCache.ReadName, SourceError,
                        (pack, count) => _fontPackScans.Add(new FontPackScan
                        { PackageId = pack.PackageId, Name = pack.Name, FontCount = count })));
                foreach (var data in catalog)
                {
                    string name = data.SelectionKey ?? "(Bundled) " + data.FaceName;
                    if (BundledFonts.ContainsKey(name)) continue;
                    _bundledFontSources.Add(name, data);
                    BundledFonts.Add(name, null);
                }
                MetadataCache.Save();
                Log.Message($"[Custom Fonts] Cataloged {BundledFonts.Count} bundled fonts without retaining source data");
                LogFontTiming("bundled file catalog", timer, $"{BundledFonts.Count} choices; metadata only");
            }
            catch (Exception ex) { Log.Error($"[Custom Fonts] Unable to read bundled fonts: {ex}"); }
        }

        private Font ResolveBundledFont(string name, FallbackFontSource[] fallbacks = null, int pixels = 0)
        {
            fallbacks = fallbacks ?? new FallbackFontSource[0];
            var data = _bundledFontSources[name];
            string key = name + "|" + FontFallbackLists.PolicyKey(fallbacks) + FontSizeKey(pixels > 0 ? pixels : data.Size);
            if (_bundledFontVariants.TryGetValue(key, out Font existing) && existing != null) return existing;
            var timer = Stopwatch.StartNew();
            byte[] bytes = FontCollections.FirstFace(FontFiles.Read(data.Path));
            LogFontTiming("bundled file read", timer, name);
            timer.Restart();
            Font font = CreateBundledFont(bytes, data, fallbacks, pixels);
            LogFontTiming("bundled font native create", timer, name);
            BundledFonts[name] = font;
            _bundledFontVariants[key] = font;
            DebugLog($"Loaded selected bundled file font '{name}' ({bytes.Length} bytes)");
            return font;
        }

        private bool IsFontReferenced(Font font) => CurrentFonts.Values.Any(f => f == font)
            || _worldFontSource == font || _worldFontAsset != null && _worldFontAsset.sourceFontFile == font
            || _osFontCache.Values.Any(f => f == font) || _fallbackFonts.Contains(font);
    }
}
