using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Brutal.ImGuiApi;
using HarmonyLib;
using UnityEngine;
using UnityStubs.Gui;
using Verse;

namespace CustomFonts
{
    // The engine exposes registration but no release API. Preserve its stable
    // font indices while retiring only this mod's native faces, locale/style copies
    // and source bytes. Slots become reusable; active UI/world bindings never move.
    internal static class WaylithFontLifetime
    {
        private static object _host;
        private static IList _fonts, _sources, _metrics, _sizes;
        private static IDictionary _registered, _locales, _styles, _fallbackData;
        private static FieldInfo _generation, _context, _atlasPixels;
        private static object _tmp;
        private static Type _sourceType;
        private static readonly HashSet<int> Owned = new HashSet<int>();
        private static readonly Stack<int> Free = new Stack<int>();
        private static readonly Dictionary<int, int> Pending = new Dictionary<int, int>();
        private static readonly Dictionary<int, string> RegistrationLocales = new Dictionary<int, string>();
        private static readonly Dictionary<string, byte[]> SharedSources = new Dictionary<string, byte[]>();
        internal static int SharedSourceCount => SharedSources.Count;
        private static string _fallbackFolder;
        internal static int LiveCount => Owned.Count;
        internal static int SlotCount => _fonts?.Count ?? 0;
        internal static int PendingCount => Pending.Count;
        internal static int ReleasedCount { get; private set; }
        internal static int CustomFallbackBufferCount => _fallbackData == null ? 0 : _fallbackData.Keys.Cast<string>().Count(IsMaterializedFallback);
        private static bool IsMaterializedFallback(string path) => Path.GetFileName(path).StartsWith("fallback-", StringComparison.Ordinal)
            && FontSourceCatalog.PathComparer.Equals(Path.GetFullPath(Path.GetDirectoryName(path)), _fallbackFolder);

        private static void Bind(object host)
        {
            if (ReferenceEquals(_host, host)) return;
            _host = host;
            var type = host.GetType();
            _fonts = (IList)AccessTools.Field(type, "fonts").GetValue(host);
            _sources = (IList)AccessTools.Field(type, "fontSources").GetValue(host);
            _metrics = (IList)AccessTools.Field(type, "faceMetrics").GetValue(host);
            _sizes = (IList)AccessTools.Field(type, "importSizes").GetValue(host);
            _registered = (IDictionary)AccessTools.Field(type, "registeredFonts").GetValue(host);
            _locales = (IDictionary)AccessTools.Field(type, "localeFonts").GetValue(host);
            _styles = (IDictionary)AccessTools.Field(type, "styledFonts").GetValue(host);
            _fallbackData = (IDictionary)AccessTools.Field(type, "fallbackFileData").GetValue(null);
            _generation = AccessTools.Field(type, "fontGeneration");
            _context = AccessTools.Field(type, "context");
            _sourceType = type.GetNestedType("FontSource", BindingFlags.NonPublic);
            _tmp = AccessTools.Property(type, "TmpFontBackend").GetValue(host);
            _atlasPixels = AccessTools.Field(_tmp.GetType(), "atlasRgba");
            _fallbackFolder = Path.GetFullPath(Path.Combine(GenFilePaths.ConfigFolderPath, "CustomFontsCache"));
            Owned.Clear(); Free.Clear(); Pending.Clear(); RegistrationLocales.Clear(); SharedSources.Clear(); ReleasedCount = 0;
        }

        internal static byte[] ShareSource(byte[] bytes)
        {
            Bind(AccessTools.Field(GuiBackend.Current.GetType(), "host").GetValue(GuiBackend.Current));
            string hash = FallbackFontSource.Hash(bytes);
            if (!SharedSources.TryGetValue(hash, out var shared)) SharedSources[hash] = shared = bytes;
            return shared;
        }

        internal static void PruneSourceData()
        {
            var used = new HashSet<byte[]>(Owned.Select(SourceBytes));
            foreach (string key in SharedSources.Keys.Where(k => !used.Contains(SharedSources[k])).ToArray()) SharedSources.Remove(key);
        }

        internal static int Adopt(int index)
        {
            object host = AccessTools.Field(GuiBackend.Current.GetType(), "host").GetValue(GuiBackend.Current);
            Bind(host);
            Pending.Remove(index);
            if (Owned.Contains(index)) return index;
            if (Free.Count > 0 && index == _fonts.Count - 1)
            {
                int slot = Free.Pop();
                // TextLayout eagerly asks for every host slot, including empty ones.
                // Discard any null locale/style entries made while this slot was free.
                foreach (IDictionary cache in new[] { _locales, _styles })
                    foreach (object key in cache.Keys.Cast<object>().Where(k => (int)((ITuple)k)[0] == slot).ToArray()) cache.Remove(key);
                foreach (IList list in new[] { _fonts, _sources, _metrics, _sizes })
                {
                    list[slot] = list[index];
                    list.RemoveAt(index);
                }
                foreach (object key in _registered.Keys.Cast<object>().Where(k => (int)_registered[k] == index).ToArray())
                    _registered[key] = slot;
                index = slot;
                InvalidateLayouts();
            }
            Owned.Add(index);
            RegistrationLocales[index] = (string)AccessTools.Field(_host.GetType(), "locale").GetValue(_host);
            return index;
        }

        internal static bool HasRegisteredLocale(int index, string locale) => RegistrationLocales.TryGetValue(index, out string registered) && registered == locale;
        internal static ImFontPtr RegisteredFont(int index) => (ImFontPtr)_fonts[index];
        internal static byte[] SourceBytes(int index) => (byte[])AccessTools.Property(_sourceType, "Data").GetValue(_sources[index]);

        internal static void Queue(int index)
        {
            if (Owned.Contains(index) && !Pending.ContainsKey(index)) Pending[index] = Time.frameCount;
        }

        private static void InvalidateLayouts()
        {
            _generation.SetValue(_host, (int)_generation.GetValue(_host) + 1);
            _atlasPixels.SetValue(_tmp, null);
            NativeFontAccess.Invalidate();
        }

        // Must run before ImGui.NewFrame: a native font may still be referenced by
        // editing/layout pointers during OnGUI even after its Unity facade is gone.
        internal static unsafe void BeforeFrame(object host)
        {
            if (!ReferenceEquals(_host, host) || Pending.Count == 0) return;
            var mod = LoadedModManager.GetMod<CustomFonts>();
            if (mod == null) return;
            ImGui.SetCurrentContext((ImGuiContextPtr)_context.GetValue(host));
            var atlas = ImGui.GetIO().Fonts;
            if (atlas.Locked) return;
            bool changed = false;
            foreach (int index in Pending.Keys.ToArray())
            {
                if (mod.IsNativeFontReferenced(index)) { Pending.Remove(index); continue; }
                if (Time.frameCount - Pending[index] < 4) continue;
                var pointers = new HashSet<nint>();
                var original = (ImFontPtr)_fonts[index];
                if (!original.IsNull()) pointers.Add((nint)original.Ptr);
                foreach (IDictionary cache in new[] { _locales, _styles })
                    foreach (object key in cache.Keys.Cast<object>().Where(k => (int)((ITuple)k)[0] == index).ToArray())
                    {
                        var font = (ImFontPtr)cache[key];
                        if (!font.IsNull()) pointers.Add((nint)font.Ptr);
                        cache.Remove(key);
                    }
                foreach (nint pointer in pointers) atlas.RemoveFont(new ImFontPtr((ImFont*)pointer));
                _fonts[index] = default(ImFontPtr);
                _sources[index] = Activator.CreateInstance(_sourceType);
                _metrics[index] = _metrics[1];
                _sizes[index] = _sizes[1];
                foreach (object key in _registered.Keys.Cast<object>().Where(k => (int)_registered[k] == index).ToArray()) _registered.Remove(key);
                WaylithCjkFallbackPatch.Forget(index);
                WaylithWorldGlyphPatch.Forget(index);
                Owned.Remove(index); Pending.Remove(index); Free.Push(index);
                RegistrationLocales.Remove(index);
                ReleasedCount++; changed = true;
            }
            if (!changed) return;
            PruneSourceData();
            // Engine fallback data is borrowed by atlas sources. Free our materialized
            // payload only after no surviving source points at it.
            var used = new HashSet<nint>();
            foreach (var source in atlas.Sources.Span) used.Add(source.FontData);
            // Scan the engine cache as locale/style copies can recreate an entry
            // after registration. A registration-only tracking set would miss it.
            foreach (string path in _fallbackData.Keys.Cast<string>().Where(IsMaterializedFallback).ToArray())
            {
                var data = ((nint, int))_fallbackData[path];
                if (used.Contains(data.Item1)) continue;
                if (data.Item1 != 0) ImGui.MemFree((void*)data.Item1);
                _fallbackData.Remove(path);
            }
            InvalidateLayouts();
        }

        internal static unsafe Dictionary<string, long> Snapshot()
        {
            var atlas = ImGui.GetIO().Fonts;
            long bytes = 0;
            var pointers = new HashSet<nint>();
            foreach (var source in atlas.Sources.Span)
                if (pointers.Add(source.FontData)) bytes += source.FontDataSize;
            long managedBytes = 0;
            var managedSources = new HashSet<byte[]>();
            if (_sources != null)
                foreach (object source in _sources)
                {
                    var data = (byte[])AccessTools.Property(_sourceType, "Data").GetValue(source);
                    if (data != null && managedSources.Add(data)) managedBytes += data.Length;
                }
            var texture = atlas.TexData;
            return new Dictionary<string, long> {
                ["native faces"] = atlas.Fonts.Span.Length, ["owned indices"] = LiveCount, ["host slots"] = SlotCount,
                ["native source bytes"] = bytes, ["managed source bytes"] = managedBytes,
                ["shared source buffers"] = SharedSourceCount,
                ["fallback file cache bytes"] = _fallbackData == null ? 0 : _fallbackData.Values.Cast<object>().Sum(v => (long)(int)((ITuple)v)[1]),
                ["custom fallback buffers"] = CustomFallbackBufferCount,
                ["registered names"] = _registered?.Count ?? 0,
                ["atlas bytes"] = texture.IsNull() ? 0 : (long)texture.Width * texture.Height * texture.BytesPerPixel,
                ["range pins"] = WaylithEmptyGlyphScopePatch.LivePins, ["native pending"] = PendingCount
            };
        }
    }

    [HarmonyPatch]
    internal static class WaylithFontRetirementPatch
    {
        private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("RimWorldWaylith.Imgui.ImGuiHost"), "BeginFrame");
        [HarmonyPrefix]
        private static void Prefix(object __instance) => WaylithFontLifetime.BeforeFrame(__instance);
    }

    // RegisterFont already merged the configured and locale fallbacks. Avoid
    // allocating an identical locale copy until the player changes language.
    [HarmonyPatch]
    internal static class WaylithRegisteredLocalePatch
    {
        private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("RimWorldWaylith.Imgui.ImGuiHost"), "Font", new[] { typeof(int) });
        [HarmonyPrefix]
        private static bool Prefix(int fontIndex, string ___locale, ref ImFontPtr __result)
        {
            if (!WaylithFontLifetime.HasRegisteredLocale(fontIndex, ___locale)) return true;
            __result = WaylithFontLifetime.RegisteredFont(fontIndex);
            return false;
        }
    }
}
