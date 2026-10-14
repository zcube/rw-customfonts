using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityStubs.Gui;
using Verse;

namespace CustomFonts
{
    public partial class CustomFonts
    {
        private readonly Dictionary<string, Font> _registeredFileFonts = new Dictionary<string, Font>();
        private readonly Dictionary<string, string> _fileRevisions = new Dictionary<string, string>();
        private readonly List<string> _fontSourceErrors = new List<string>();
        private readonly Dictionary<string, BundledFontData> _bundledFontSources = new Dictionary<string, BundledFontData>();
        private static readonly System.Reflection.MethodInfo SetFontData = AccessTools.Method(typeof(Font), "SetFontData");

        // Waylith renders world labels through TMP; its legacy TextMesh is only a data shim.
        // Keep the native TMP prefab for Default too, instead of Unity's legacy text path.
        private static bool UseLegacyWorldText(string name) => false;

        private static string DisplayFontName(string name)
        {
            if (name == null || !name.StartsWith("(File) ")) return name;
            int at = name.LastIndexOf(" [", StringComparison.Ordinal);
            if (at < 0 || !name.EndsWith("]")) return name;
            string path = name.Substring(at + 2, name.Length - at - 3);
            return name.Substring(0, at) + " — " + Path.GetFileName(path);
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
            _hasOSFontAssets = true;
            _fontSourceErrors.Clear();
            if (_settings.IncludeSystemFonts)
            {
                var paths = new HashSet<string>(Font.GetPathsToOSFonts(), OperatingSystem.IsWindows()
                    ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
                foreach (string root in AdditionalFontRoots.Get(OperatingSystem.IsLinux(), Environment.GetEnvironmentVariable))
                {
                    if (!Directory.Exists(root)) continue;
                    try
                    {
                        foreach (string path in FontFiles.Enumerate(root, true).Where(FontFiles.Supported)) paths.Add(path);
                    }
                    catch (Exception ex) { SourceError(root, ex); }
                }
                foreach (string path in paths.OrderBy(x => x, StringComparer.Ordinal))
                    AddFontPath(path, false);
            }
            foreach (string source in _settings.FontSourcePaths)
            {
                try
                {
                    string path = FontFiles.Normalize(source, _content.RootDir);
                    foreach (string file in FontFiles.Enumerate(path, _settings.RecursiveFontFolders)
                        .Where(FontFiles.Supported).OrderBy(x => x, StringComparer.Ordinal))
                        AddFontPath(file, true);
                }
                catch (Exception ex) { SourceError(source, ex); }
            }
            DebugLog($"Waylith catalog: {OSFontPaths.Count} font files, {_fontSourceErrors.Count} errors");
        }

        private void AddFontPath(string path, bool custom)
        {
            try
            {
                string name = FontFiles.ReadName(path);
                // Custom choices retain the absolute path, so equal family names never silently
                // select a different file. Old system/world family names remain valid.
                string key = custom ? $"(File) {name} [{path}]" : name;
                if (!OSFontPaths.ContainsKey(key)) OSFontPaths.Add(key, path);
            }
            catch (Exception ex) { SourceError(path, ex); }
        }

        private void SourceError(string path, Exception ex)
        {
            _fontSourceErrors.Add(path + ": " + ex.Message);
            Log.Warning("[Custom Fonts] Font source skipped: " + path + ": " + ex.Message);
        }

        public void SetupBundledFonts()
        {
            if (_hasBundledFonts) return;
            _hasBundledFonts = true;
            try
            {
                if (SetFontData == null) throw new MissingMethodException("Waylith Font.SetFontData");
                foreach (var data in BundledFontReader.Read(Path.Combine(_content.RootDir, "rimfonts")))
                {
                    string name = "(Bundled) " + data.FaceName;
                    if (BundledFonts.ContainsKey(name)) continue;
                    _bundledFontSources.Add(name, data);
                    BundledFonts.Add(name, null);
                }
                Log.Message($"[Custom Fonts] Waylith cataloged {BundledFonts.Count} bundled fonts without retaining source data");
            }
            catch (Exception ex)
            {
                Log.Error($"[Custom Fonts] Unable to read bundled fonts: {ex}");
            }
        }

        private Font ResolveBundledFont(string name)
        {
            if (BundledFonts[name] is Font existing) return existing;
            var data = _bundledFontSources[name];
            byte[] bytes = BundledFontReader.ReadData(Path.Combine(_content.RootDir, "rimfonts"), data);
            var font = new Font { name = data.Name };
            SetFontData.Invoke(font, new object[] { bytes, data.Size, data.RenderingMode });
            BundledFonts[name] = font;
            DebugLog($"Waylith loaded selected bundle font '{name}' ({bytes.Length} bytes)");
            return font;
        }

        private Font ResolveOSFont(string fontKey, string fontName, int fontSize)
        {
            if (_osFontCache.TryGetValue(fontKey, out var cached) && cached != null) return cached;
            SetupOSFontPaths();
            if (!OSFontPaths.TryGetValue(fontName, out string path))
            {
                // Migrate Unity's family-only UI selection to the regular file face if possible.
                path = OSFontPaths.FirstOrDefault(p => p.Key == fontName + " (Regular)").Value
                    ?? OSFontPaths.FirstOrDefault(p => p.Key.StartsWith(fontName + " (", StringComparison.Ordinal)).Value;
            }
            if (path == null) return null;
            Font font = LoadFontFile(path);
            _osFontCache[fontKey] = font;
            return font;
        }

        private Font LoadFontFile(string path)
        {
            // Refresh explicitly invalidates path revisions. Sizes are applied by GUIStyle and
            // must not register another engine font every time the scale slider moves.
            if (_fileRevisions.TryGetValue(path, out string revision)
                && _registeredFileFonts.TryGetValue(revision, out Font existing)) return existing;
            byte[] bytes = FontCollections.FirstFace(FontFiles.Read(path));
            string name = OpenTypeNames.Read(bytes);
            revision = Convert.ToHexString(SHA256.HashData(bytes));
            if (!_registeredFileFonts.TryGetValue(revision, out Font font))
            {
                var backend = GuiBackend.Current ?? throw new InvalidOperationException("Font renderer is not ready.");
                int index = backend.RegisterFont("CustomFonts:" + revision, bytes, 24f, 0);
                if (index < 0) throw new InvalidDataException("Waylith could not register " + path);
                font = new Font { name = name, GameFontIndex = index };
                _registeredFileFonts.Add(revision, font);
                DebugLog($"Waylith registered '{path}' as index {index} ({revision.Substring(0, 12)})");
            }
            _fileRevisions[path] = revision;
            return font;
        }

        private void ReleaseWorldFontResources(TMP_FontAsset asset)
        {
            // Waylith's TMP facade borrows the renderer's atlas. It is not owned by the TMP
            // wrapper and must never enter Unity's per-font texture/material destruction path.
        }

        private bool IsEngineOwned(UnityEngine.Object target) =>
            target is Font font && _registeredFileFonts.ContainsValue(font);

        private void RefreshFontSources()
        {
            _hasInstalledFontNames = false;
            _hasOSFontAssets = false;
            OSFontPaths.Clear();
            _fileRevisions.Clear();
            _osFontCache.Clear();
            ReleaseWorldFontAsset();
            SetupOSInstalledFontNames();
            UpdateFont();
            RefreshWorldFeatureTexts();
            WriteSettings();
        }
    }
}
