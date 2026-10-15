using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityStubs.Gui;

namespace CustomFonts
{
    public partial class CustomFonts
    {
        private static bool UseLegacyWorldText(string name) => false;
        private static FontPlatform CurrentFontPlatform() => OperatingSystem.IsWindows()
            ? FontPlatform.Windows : OperatingSystem.IsMacOS() ? FontPlatform.MacOS : FontPlatform.Linux;

        // Waylith input uses the registered size when it requests size 0. Register
        // each UI size explicitly instead of patching the input implementation.
        private static float FileImportSize(float pixels) => pixels > 0 ? pixels : 24f;
        private static string FontSizeKey(float pixels) => ":px=" + FileImportSize(pixels).ToString(System.Globalization.CultureInfo.InvariantCulture);
        private Font CreateFileFont(byte[] bytes, string name, string revision, FallbackFontSource[] fallbacks, int pixels)
            => RegisterScopedFont(bytes, name, revision, FileImportSize(pixels), 0, fallbacks);
        private Font RegisterScopedFont(byte[] bytes, string name, string revision, float size, int mode, FallbackFontSource[] fallbacks)
        {
            fallbacks = FallbackFontSource.WithoutPrimary(fallbacks, bytes).ToArray();
            var backend = GuiBackend.Current ?? throw new InvalidOperationException("Font renderer is not ready.");
            var previous = WaylithCjkFallbackPatch.PendingSources;
            WaylithCjkFallbackPatch.PendingSources = fallbacks;
            try
            {
                bytes = WaylithFontLifetime.ShareSource(bytes);
                int index = backend.RegisterFont("CustomFonts:" + revision + ":" + size.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + mode, bytes, size, mode);
                if (index < 0) throw new InvalidDataException("Waylith could not register " + name);
                index = WaylithFontLifetime.Adopt(index);
                WaylithCjkFallbackPatch.Register(index, fallbacks);
                DebugLog($"Waylith registered '{name}' as index {index}");
                return new Font { name = name, GameFontIndex = index };
            }
            finally { WaylithCjkFallbackPatch.PendingSources = previous; WaylithFontLifetime.PruneSourceData(); }
        }
        private Font CreateFallbackOnlyFont(FallbackFontSource source) => LoadFontFile(source.Path);
        private Font CreateBundledFont(byte[] bytes, BundledFontData data, FallbackFontSource[] fallbacks, int pixels)
            => RegisterScopedFont(bytes, data.Name, FallbackFontSource.Hash(bytes) + ":" + FontFallbackLists.PolicyKey(fallbacks),
                pixels > 0 ? pixels : data.Size, data.RenderingMode, fallbacks);
        private Font ResolveScopedDefaultFont(Font original, FallbackFontSource[] fallbacks, int pixels = 0)
        {
            if (pixels <= 0) pixels = original.fontSize;
            if (fallbacks.Length == 0 && pixels == original.fontSize) return original;
            string key = original.GameFontIndex + ":" + FontFallbackLists.PolicyKey(fallbacks) + FontSizeKey(pixels);
            if (_scopedDefaultFonts.TryGetValue(key, out var cached)) return cached;
            var gui = GuiBackend.Current;
            object host = AccessTools.Field(gui.GetType(), "host").GetValue(gui);
            object source = ((IList)AccessTools.Field(host.GetType(), "fontSources").GetValue(host))[original.GameFontIndex];
            byte[] bytes = (byte[])AccessTools.Property(source.GetType(), "Data").GetValue(source);
            if (bytes == null) bytes = File.ReadAllBytes((string)AccessTools.Property(source.GetType(), "Path").GetValue(source));
            uint flags = (uint)AccessTools.Property(source.GetType(), "LoaderFlags").GetValue(source);
            int mode = flags == 656u ? 2 : flags == 512u ? 1 : 0;
            return _scopedDefaultFonts[key] = RegisterScopedFont(bytes, "CustomFontsDefault-" + original.name,
                "default:" + key, pixels, mode, fallbacks);
        }
        private bool ReleaseEngineResource(UnityEngine.Object target)
        {
            if (target is Font font) WaylithFontLifetime.Queue(font.GameFontIndex);
            return false; // The small Unity facade also needs to be destroyed.
        }
        internal bool IsNativeFontReferenced(int index) => CurrentFonts.Values.Any(f => f != null && f.GameFontIndex == index)
            || _worldFontAsset != null && _worldFontAsset.fontIndex == index
            || _fallbackFonts.Any(f => f != null && f.GameFontIndex == index)
            || _worldFontSource != null && _worldFontSource.GameFontIndex == index;
        private Font ResolveDefaultUIFont(Verse.GameFont size, int pixels) => ResolveScopedDefaultFont(DefaultFonts[size], SourcesFor((FontTarget)size), pixels);
        private void ResetEngineFallbackPolicy()
        {
            // A policy change selects a different immutable registration. Existing
            // UI/world faces keep their pointers and cached glyphs.
        }
        private void ReleaseWorldFontResources(TMP_FontAsset asset)
        {
            // The TMP facade borrows the native renderer's atlas.
        }
        private static bool UsesWorldFallbackAssets() => false;
        private void VerifyWorldFallbackChain(TMP_FontAsset asset)
        {
            var expected = FallbackFontSource.WithoutPrimary(FallbackSources, WaylithFontLifetime.SourceBytes(asset.fontIndex));
            Require(WaylithCjkFallbackPatch.Sources(asset.fontIndex).Select(s => s.Revision).SequenceEqual(expected.Select(s => s.Revision)),
                "Waylith world native face has the complete ordered fallback policy");
        }
        private void PrepareWorldFontAsset(TMP_FontAsset asset) => WaylithWorldGlyphPatch.Register(asset.fontIndex);
        private TMP_FontAsset CreateDefaultWorldFontAsset()
            => TMP_FontAsset.CreateFontAsset(ResolveScopedDefaultFont(DefaultTMPFontAsset.sourceFontFile ?? DefaultFonts[Verse.GameFont.Medium], FallbackSources));
    }
}
