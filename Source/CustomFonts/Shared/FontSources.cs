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
        private readonly List<string> _fontSourceErrors = new List<string>();
        private string[] _systemFontSearchRoots;
        private string[] SystemFontSearchRoots => _systemFontSearchRoots ?? (_systemFontSearchRoots = SystemFontRoots.Get(CurrentFontPlatform(),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Environment.GetEnvironmentVariable).ToArray());
        private string DisplayFontName(string name)
        {
            if (name != null && _bundledFontSources.TryGetValue(name, out var data) && data.SourceModName != null)
                return "(Bundled) " + data.FaceName + " [" + data.SourceModName + "]";
            return FontSourceCatalog.DisplayName(name);
        }
        private string CanonicalFontSelection(string selection)
        {
            if (selection == FontSettings.DefaultFontName || selection.StartsWith("(Bundled) ", StringComparison.Ordinal)) return selection;
            SetupOSFontPaths();
            return FontSourceCatalog.PortableSelection(OSFontPaths, selection);
        }

        public void SetupOSInstalledFontNames()
        {
            if (_hasInstalledFontNames) return;
            SetupOSFontPaths();
            _fontNames = OSFontPaths.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
            _hasInstalledFontNames = true;
        }

        public void SetupOSFontPaths()
        {
            if (_hasOSFontAssets) return;
            var timer = Stopwatch.StartNew();
            _hasOSFontAssets = true;
            _fontSourceErrors.Clear();
            var systemFiles = new HashSet<string>(FontSourceCatalog.PathComparer);
            if (_settings.IncludeSystemFonts)
            {
                foreach (string root in SystemFontSearchRoots)
                {
                    if (!Directory.Exists(root)) continue;
                    try { systemFiles.UnionWith(FontFiles.Enumerate(root, true).Where(FontFiles.Supported)); }
                    catch (Exception ex) { SourceError(root, ex); }
                }
            }
            FontSourceCatalog.Populate(OSFontPaths, systemFiles, _settings.FontSourcePaths, _content.RootDir,
                _settings.RecursiveFontFolders, MetadataCache.ReadName, SourceError);
            MetadataCache.Save();
            LogFontTiming("file catalog", timer, $"{OSFontPaths.Count} choices; name cache hits={MetadataCache.Hits}, misses={MetadataCache.Misses}");
            DebugLog($"Font catalog: {OSFontPaths.Count} files, {_fontSourceErrors.Count} skipped sources");
        }

        private void SourceError(string path, Exception ex)
        {
            _fontSourceErrors.Add(path + ": " + ex.Message);
            Log.Warning("[Custom Fonts] Font source skipped: " + path + ": " + ex.Message);
        }

        private void RefreshFontSources()
        {
            _hasInstalledFontNames = false;
            _hasOSFontAssets = false;
            _systemFontSearchRoots = null;
            OSFontPaths.Clear();
            _hasBundledFonts = false;
            foreach (Font font in _bundledFontVariants.Values.Distinct()) QueueDestroy(font);
            _bundledFontVariants.Clear();
            _bundledFontSources.Clear();
            _fontPackScans.Clear();
            BundledFonts.Clear();
            ReleaseWorldFontAsset();
            foreach (Font font in _osFontCache.Values.Distinct()) QueueDestroy(font);
            _osFontCache.Clear();
            _appliedFontKeys.Clear();
            _fileFontCache.Invalidate();
            _fallbackConfiguration = null;
            SetupOSInstalledFontNames();
            SetupBundledFonts();
            UpdateFont();
            RefreshWorldFeatureTexts();
            WriteSettings();
        }
    }
}
