using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace CustomFonts
{
    public partial class CustomFonts
    {
        private static bool UseLegacyWorldText(string name) => name == FontSettings.DefaultFontName;
        private static bool _fontEngineInitialized;
        private readonly Dictionary<Font, AssetBundle> _importedFontBundles = new Dictionary<Font, AssetBundle>();

        private static FontPlatform CurrentFontPlatform() => Application.platform == RuntimePlatform.WindowsPlayer
            ? FontPlatform.Windows : Application.platform == RuntimePlatform.OSXPlayer ? FontPlatform.MacOS : FontPlatform.Linux;

        // Creation/validation and resource ownership are the only engine-dependent parts.
        // Unity respects GUIStyle.fontSize, so its dynamic Font stays shared across sizes.
        private static string FontSizeKey(float pixels) => "";
        private Font CreateFileFont(byte[] bytes, string name, string revision, FallbackFontSource[] fallbacks, int pixels)
            => CreateImportedFont(bytes, name, null, fallbacks);
        private Font CreateBundledFont(byte[] bytes, BundledFontData data, FallbackFontSource[] fallbacks, int pixels)
            => CreateImportedFont(bytes, data.Name, null, fallbacks);
        private Font CreateFallbackOnlyFont(FallbackFontSource source) => CreateImportedFont(source.Bytes,
            "CustomFontsFallback-" + source.Revision, null, new FallbackFontSource[0]);

        private Font CreateImportedFont(byte[] bytes, string name, string[] fontNames = null, IEnumerable<FallbackFontSource> fallbacks = null)
        {
            var bundle = AssetBundle.LoadFromMemory(UnityFontBundle.Create(bytes, fallbacks ?? FallbackSources, fontNames));
            if (bundle == null) throw new InvalidDataException("Unity could not import font: " + name);
            try
            {
                Font font = bundle.LoadAsset<Font>(UnityFontBundle.AssetName);
                if (!_fontEngineInitialized)
                {
                    FontEngine.InitializeFontEngine();
                    _fontEngineInitialized = true;
                }
                foreach (string asset in bundle.GetAllAssetNames().Where(n => n.StartsWith("customfonts-fallback-", StringComparison.Ordinal)))
                {
                    Font fallback = bundle.LoadAsset<Font>(asset);
                    if (fallback == null || FontEngine.LoadFontFace(fallback) != FontEngineError.Success)
                        throw new InvalidDataException("Unity could not load defined fallback: " + asset);
                    FontEngine.UnloadFontFace();
                }
                if (font == null || !font.dynamic || bytes.Length != 0 && FontEngine.LoadFontFace(font) != FontEngineError.Success)
                    throw new InvalidDataException("Unity could not load font: " + name);
                FontEngine.UnloadFontFace();
                font.name = name;
                _importedFontBundles.Add(font, bundle);
                return font;
            }
            catch { bundle.Unload(true); throw; }
        }

        private Font ResolveDefaultUIFont(Verse.GameFont size, int pixels)
        {
            Font original = DefaultFonts[size];
            var fallbacks = SourcesFor((FontTarget)size);
            if (fallbacks.Length == 0) return original;
            string key = original.GetInstanceID() + ":" + FallbackPolicy((FontTarget)size);
            if (!_scopedDefaultFonts.TryGetValue(key, out Font font) || font == null)
                _scopedDefaultFonts[key] = font = CreateImportedFont(new byte[0], "CustomFontsDefault-" + original.name, original.fontNames, fallbacks);
            return font;
        }

        private void ResetEngineFallbackPolicy()
        {
            // Policies are part of the cache identity. Existing scopes keep their fonts
            // while a changed scope resolves a new import; unused variants are pruned.
        }

        private bool ReleaseEngineResource(UnityEngine.Object target)
        {
            if (target is Font font && _importedFontBundles.TryGetValue(font, out var bundle))
            {
                _importedFontBundles.Remove(font);
                bundle.Unload(true);
                return true;
            }
            return false;
        }

        private void ReleaseWorldFontResources(TMP_FontAsset asset)
        {
            if (asset.atlasTextures != null)
                foreach (var texture in asset.atlasTextures)
                    if (!IsProtected(texture)) QueueDestroy(texture);
            if (!IsProtected(asset.material)) QueueDestroy(asset.material);
        }

        private static bool UsesWorldFallbackAssets() => true;
        private void VerifyWorldFallbackChain(TMP_FontAsset asset) => Require(asset.fallbackFontAssetTable.Count >= FallbackSources.Length,
            "Unity world asset has the complete ordered TMP fallback chain");
        private Dictionary<string, long> CaptureEngineFontMemory()
        {
            var fonts = Resources.FindObjectsOfTypeAll<Font>();
            var assets = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            var textures = fonts.Where(f => f != null && f.material != null).Select(f => f.material.mainTexture as Texture2D)
                .Concat(assets.Where(a => a != null && a.atlasTextures != null).SelectMany(a => a.atlasTextures)).Where(t => t != null).Distinct();
            return new Dictionary<string, long> {
                ["Font objects"] = fonts.Length, ["TMP assets"] = assets.Length,
                ["imported bundles"] = _importedFontBundles.Count,
                ["atlas bytes"] = textures.Sum(t => EstimateTextureBytes(t))
            };
        }
        private void VerifyRetiredEngineFontResources() => Require(_importedFontBundles.Count == 0,
            "Default cleanup releases all Unity custom imports");
        private void PrepareWorldFontAsset(TMP_FontAsset asset) { }
        private TMP_FontAsset CreateDefaultWorldFontAsset()
        {
            // RimWorld's world Font has no embedded source bytes. Recreating a dynamic
            // asset from it fails; clone its already-baked TMP asset instead. Its atlas
            // and material are borrowed game resources and must remain alive.
            return UnityEngine.Object.Instantiate(DefaultTMPFontAsset);
        }
    }
}
