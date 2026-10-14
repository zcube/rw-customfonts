using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Verse;
using UnityEngine;
using HarmonyLib;
using RimWorld.Planet;
using RimWorld;
using SettingsHelper;
using TMPro;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;

namespace CustomFonts
{
    public partial class CustomFonts
    {
        private static string DisplayFontName(string name) => name;
        private static bool UseLegacyWorldText(string name) => name == FontSettings.DefaultFontName;
        private static bool _fontEngineInitialized;
        private bool _hasRunHostFonts;
        private bool _hasUnityPlayerSo;
        private bool _hasPlatformInfo;

        public void SetupOSInstalledFontNames()
        {
            if (_hasInstalledFontNames) return;
            _hasInstalledFontNames = true;
            _fontNames = Font.GetOSInstalledFontNames().ToList();
            _fontNames.Sort();
        }

        public void SetupPlatformInfo()
        {
            if (_hasPlatformInfo) return;
            _hasPlatformInfo = true;
            try
            {
                string UnityPlayerSoPath = Path.GetFullPath("UnityPlayer.so");
                _hasUnityPlayerSo = File.Exists(UnityPlayerSoPath);
                _hasRunHostFonts = Directory.Exists("/run/host/fonts");
            }
            catch (Exception ex)
            {
                Log.Message($"[Custom Fonts] error: {ex.Message}");
            }
        }

        private static bool TryGetFaceName(string filePath, out string faceName)
        {
            faceName = null;
            if (!_fontEngineInitialized)
            {
                FontEngine.InitializeFontEngine();
                _fontEngineInitialized = true;
            }

            if (FontEngine.LoadFontFace(filePath) != FontEngineError.Success) return false;
            try
            {
                var faceInfo = FontEngine.GetFaceInfo();
                if (string.IsNullOrEmpty(faceInfo.familyName)) return false;
                faceName = $"{faceInfo.familyName} ({faceInfo.styleName})";
                return true;
            }
            finally
            {
                FontEngine.UnloadFontFace();
            }
        }

        private static bool TryGetFaceName(Font font, out string faceName)
        {
            faceName = null;
            if (!_fontEngineInitialized)
            {
                FontEngine.InitializeFontEngine();
                _fontEngineInitialized = true;
            }

            if (FontEngine.LoadFontFace(font) != FontEngineError.Success) return false;
            try
            {
                var faceInfo = FontEngine.GetFaceInfo();
                if (string.IsNullOrEmpty(faceInfo.familyName)) return false;
                faceName = $"{faceInfo.familyName} ({faceInfo.styleName})";
                return true;
            }
            finally
            {
                FontEngine.UnloadFontFace();
            }
        }

        public void SetupOSFontPaths()
        {
            try
            {
                if (_hasOSFontAssets) return;
                _hasOSFontAssets = true;
                var startedAt = Time.realtimeSinceStartup;
                foreach (var path in Font.GetPathsToOSFonts())
                {
                    string fontName;
                    if (!TryGetFaceName(path, out fontName))
                    {
#if DEBUG
                        Log.Warning($"[Custom Fonts] Unable to read face info for {path}, skipping.");
#endif
                        continue;
                    }
                    if (!OSFontPaths.ContainsKey(fontName))
                    {
                        OSFontPaths.Add(fontName, path);
                    }
                }
                DebugLog($"scanned {OSFontPaths.Count} OS font faces in {(Time.realtimeSinceStartup - startedAt) * 1000f:F0} ms");
            }
            catch (Exception ex)
            {
                Log.Message($"[Custom Fonts] error: {ex.Message}");
            }
        }

        private Font ResolveOSFont(string fontKey, string fontName, int fontSize)
        {
            Font cached;
            if (_osFontCache.TryGetValue(fontKey, out cached) && cached != null) return cached;

            var font = Font.CreateDynamicFontFromOSFont(fontName, fontSize);
            if (font != null)
            {
                _osFontCache[fontKey] = font;
                DebugLog($"created dynamic OS font '{fontName}' @{fontSize} (cache={_osFontCache.Count})");
            }
            else
            {
                _osFontCache.Remove(fontKey);
            }
            return font;
        }

        public int IndexOf(byte[] array, byte[] pattern)
        {
            if (pattern.Length > array.Length)
                return -1;

            for (int i = 0; i <= array.Length - pattern.Length; i++)
            {
                bool found = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (array[i + j] != pattern[j])
                    {
                        found = false;
                        break;
                    }
                }
                if (found)
                    return i;
            }
            return -1;
        }

        public void ReplaceStringInBinaryFile(string filePath, string searchString, string replaceString)
        {
            try
            {
                byte[] searchStringBytes = Encoding.UTF8.GetBytes(searchString);
                byte[] replaceStringBytes = Encoding.UTF8.GetBytes(replaceString);

                using (FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite))
                {
                    byte[] data = new byte[fileStream.Length];
                    fileStream.Read(data, 0, (int)fileStream.Length);
                    int index = IndexOf(data, searchStringBytes);
                    if (index != -1)
                    {
                        Array.Copy(replaceStringBytes, 0, data, index, replaceStringBytes.Length);
                        fileStream.Seek(0, SeekOrigin.Begin);
                        fileStream.Write(data, 0, data.Length);
                        Log.Message($"[Custom Fonts] Updating UnityPlayer.so fonts path from {searchString} to {replaceString}");
                    }
                    else
                    {
                        Log.Message($"[Custom Fonts] {searchString} not found");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Message($"[Custom Fonts] error: {ex.Message}");
            }
        }

        private Font LoadFontFile(string path) => new Font(path);
        private Font ResolveBundledFont(string name) => BundledFonts[name];

        private void ReleaseWorldFontResources(TMP_FontAsset asset)
        {
            var atlasTextures = asset.atlasTextures;
            if (atlasTextures != null)
            {
                foreach (var atlasTexture in atlasTextures)
                {
                    QueueDestroy(atlasTexture);
                }
            }
            QueueDestroy(asset.material);
        }

        private bool IsEngineOwned(UnityEngine.Object target) => false;

        public void SetupBundledFonts()
        {
            if (_hasBundledFonts) return;
            _hasBundledFonts = true;
            var fontAssetPath = Path.Combine(_content.RootDir, "rimfonts");
            var cab = AssetBundle.LoadFromFile(fontAssetPath);
            if (cab == null)
            {
                Log.Message("[Custom Fonts] Unable to load bundled fonts.");
                return;
            }
#if DEBUG
            Log.Message(
                $"[Custom Fonts] Loading font asset at {fontAssetPath}:\n{string.Join("\n", cab.GetAllAssetNames())}");
#endif

            foreach (var font in cab.LoadAllAssets<Font>())
            {
                string faceName;
                if (!TryGetFaceName(font, out faceName))
                {
                    Log.Warning($"[Custom Fonts] Unable to read face info for bundled font {font.name}, skipping.");
                    continue;
                }
                var fontName = $"(Bundled) {faceName}";
                if (!BundledFonts.ContainsKey(fontName))
                {
                    BundledFonts.Add(fontName, font);
                }
            }
            DebugLog($"loaded {BundledFonts.Count} bundled font(s)");
        }

    }
}
