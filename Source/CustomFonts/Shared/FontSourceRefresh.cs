using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CustomFonts
{
    public partial class CustomFonts
    {
        private string _fileSourceSnapshot, _bundledSourceSnapshot;
        private bool _fontSettingsOpen;

        private IEnumerable<FontScanLocation> FileSourceLocations()
        {
            if (_settings.IncludeSystemFonts)
                foreach (string path in SystemFontSearchRoots) yield return new FontScanLocation(path, true);
            foreach (string expression in _settings.FontSourcePaths)
            {
                string resolved;
                try { resolved = FontFiles.Normalize(expression, _content.RootDir); }
                catch (Exception) { resolved = null; } // Shared profiles may contain foreign/missing roots.
                yield return new FontScanLocation(expression, resolved, _settings.RecursiveFontFolders);
            }
        }

        private IEnumerable<FontPackSource> ActiveFontPacks() => Verse.LoadedModManager.RunningModsListForReading
            .SkipWhile(mod => mod != _content).Skip(1).Select(mod => new FontPackSource
            { PackageId = mod.PackageId, Name = mod.Name, ContentRoots = mod.foldersToLoadDescendingOrder });

        private IEnumerable<FontScanLocation> BundledSourceLocations()
        {
            yield return new FontScanLocation(Path.Combine(_content.RootDir, "Fonts"), true);
            foreach (var pack in ActiveFontPacks())
                foreach (string root in pack.ContentRoots)
                {
                    string folder = Path.Combine(root, "Fonts");
                    yield return new FontScanLocation(pack.PackageId + ":" + folder, folder, true);
                }
        }

        private bool RefreshChangedFontsOnSettingsOpen()
        {
            if (_fontSettingsOpen) return false;
            _fontSettingsOpen = true;
            string files = FontSourceSnapshot.Capture(FileSourceLocations());
            string bundles = FontSourceSnapshot.Capture(BundledSourceLocations());
            if (files == _fileSourceSnapshot && bundles == _bundledSourceSnapshot) return false;
            RefreshFontSources();
            DebugLog("Font sources changed while settings were closed; refreshed the catalogs.");
            return true;
        }

        internal void CloseFontSettings()
        {
            _fontSettingsOpen = false;
            ReleaseFontPreviewMemory();
        }
    }
}
