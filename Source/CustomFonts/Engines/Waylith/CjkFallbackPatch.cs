using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Brutal.ImGuiApi;
using HarmonyLib;

namespace CustomFonts
{
    // Keep the selected/locale-specific face first. Only missing glyphs use the other
    // CJK faces. Extend managed source lists BEFORE the engine creates native fonts;
    // merging into a live native font invalidates baked pointers held by TextLayout.
    [HarmonyPatch]
    internal static class WaylithCjkFallbackPatch
    {
        private static object _host;
        private static object _sources;
        [ThreadStatic] internal static FallbackFontSource[] PendingSources;
        private static readonly Dictionary<int, FallbackFontSource[]> FontPolicies = new Dictionary<int, FallbackFontSource[]>();
        internal static void Register(int index, FallbackFontSource[] sources) => FontPolicies[index] = sources;
        internal static void Forget(int index) => FontPolicies.Remove(index);
        internal static FallbackFontSource[] Sources(int index) => FontPolicies.TryGetValue(index, out var sources) ? sources : Array.Empty<FallbackFontSource>();

        private static MethodBase TargetMethod() => AccessTools.Method(
            AccessTools.TypeByName("RimWorldWaylith.Imgui.ImGuiHost"), "Fallbacks");

        [HarmonyPostfix]
        private static void Postfix(ref object __result)
        {
            _sources ??= BuildSources();
            var type = AccessTools.TypeByName("RimWorldWaylith.Imgui.FontFallbacks");
            var sourceType = type.GetNestedType("Source", BindingFlags.Public | BindingFlags.NonPublic);
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(sourceType));
            foreach (var configured in PendingSources ?? Array.Empty<FallbackFontSource>())
                list.Add(Activator.CreateInstance(sourceType, new object[] { configured.Path, 0u, null }));
            // Never mutate the engine's cached locale list: each native font gets its
            // own ordered policy, followed by the game's locale and automatic CJK sources.
            foreach (object source in (IEnumerable)__result) if (!list.Contains(source)) list.Add(source);
            foreach (object source in (IEnumerable)_sources) if (!list.Contains(source)) list.Add(source);
            __result = list;
        }

        internal static void InitializeHost(object host)
        {
            if (ReferenceEquals(_host, host)) return;
            _host = host;
            // Old native fonts remain owned by the engine. New lookups create safe copies
            // with the extended source list; invalidate measurement caches at the same time.
            ((IDictionary)AccessTools.Field(host.GetType(), "localeFonts").GetValue(host)).Clear();
            ((IDictionary)AccessTools.Field(host.GetType(), "styledFonts").GetValue(host)).Clear();
            var generation = AccessTools.Field(host.GetType(), "fontGeneration");
            generation.SetValue(host, (int)generation.GetValue(host) + 1);
        }

        private static object BuildSources()
        {
            var type = AccessTools.TypeByName("RimWorldWaylith.Imgui.FontFallbacks");
            var sourceType = type.GetNestedType("Source", BindingFlags.Public | BindingFlags.NonPublic);
            var result = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(sourceType));
            var resolve = AccessTools.Method(type, "Resolve");
            var pathProperty = AccessTools.Property(sourceType, "Path");
            var faceProperty = AccessTools.Property(sourceType, "FontNo");
            var excludeProperty = AccessTools.Property(sourceType, "ExcludeRanges");
            var seen = new HashSet<(string, uint)>();
            foreach (string locale in new[] { "SC", "TC", "JP", "KR" })
            {
                foreach (object source in (IEnumerable)resolve.Invoke(null, new object[] { locale }))
                {
                    if (excludeProperty.GetValue(source) != null) continue;
                    var key = ((string)pathProperty.GetValue(source), (uint)faceProperty.GetValue(source));
                    if (seen.Add(key)) result.Add(source);
                }
            }
            if (OperatingSystem.IsWindows())
            {
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts");
                foreach (string name in new[] { "simsunb.ttf", "mingliub.ttc", "SimsunExtG.ttf" })
                {
                    string path = Path.Combine(folder, name);
                    if (File.Exists(path) && seen.Add((path, 0)))
                        result.Add(Activator.CreateInstance(sourceType, new object[] { path, 0u, null }));
                }
            }
            return result;
        }
    }

    [HarmonyPatch]
    internal static class WaylithCjkFontInitializationPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            var host = AccessTools.TypeByName("RimWorldWaylith.Imgui.ImGuiHost");
            yield return AccessTools.Method(host, "Font", new[] { typeof(int) });
            yield return AccessTools.Method(host, "StyledFont");
        }

        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void Prefix(object __instance, int __0, out FallbackFontSource[] __state)
        {
            WaylithCjkFallbackPatch.InitializeHost(__instance);
            __state = WaylithCjkFallbackPatch.PendingSources;
            WaylithCjkFallbackPatch.PendingSources = WaylithCjkFallbackPatch.Sources(__0);
        }
        [HarmonyFinalizer]
        private static void Finalizer(FallbackFontSource[] __state) => WaylithCjkFallbackPatch.PendingSources = __state;
    }

    [HarmonyPatch]
    internal static class WaylithCjkDefaultLocalePatch
    {
        private static MethodBase TargetMethod() => AccessTools.Method(
            AccessTools.TypeByName("RimWorldWaylith.Imgui.ImGuiHost"), "Font", new[] { typeof(int) });

        [HarmonyPostfix]
        private static void Postfix(object __instance, int fontIndex, string ___locale,
            object ___localeFonts, object ___fontSources, ref ImFontPtr __result)
        {
            // The empty/English locale otherwise returns fonts created before mod loading.
            if (___locale.Length != 0 || WaylithFontLifetime.HasRegisteredLocale(fontIndex, ___locale)) return;
            var cache = (IDictionary)___localeFonts;
            var key = (fontIndex, "");
            if (!cache.Contains(key))
            {
                object source = ((IList)___fontSources)[fontIndex];
                object flags = AccessTools.Property(source.GetType(), "LoaderFlags").GetValue(source);
                var font = (ImFontPtr)AccessTools.Method(__instance.GetType(), "AddFontAgain")
                    .Invoke(__instance, new[] { source, flags, (object)0u });
                cache[key] = font.IsNull() ? __result : font;
            }
            __result = (ImFontPtr)cache[key];
        }
    }
}
