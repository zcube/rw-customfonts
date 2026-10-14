using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using Brutal.ImGuiApi;
using Brutal.Pointers;
using HarmonyLib;
using Verse;

namespace CustomFonts
{
    [HarmonyPatch]
    internal static class WaylithEmptyGlyphScopePatch
    {
        // Weak keys prevent failed registrations from retaining source byte arrays.
        // Positive ranges stay pinned: native font sources borrow them for the session.
        private sealed class RangePointer { internal nint Value; }
        private static readonly ConditionalWeakTable<byte[], RangePointer> Ranges = new ConditionalWeakTable<byte[], RangePointer>();
        [ThreadStatic] internal static nint Pending;

        private static IEnumerable<MethodBase> TargetMethods()
        {
            var host = AccessTools.TypeByName("RimWorldWaylith.Imgui.ImGuiHost");
            yield return AccessTools.Method(host, "RegisterFont");
            yield return AccessTools.Method(host, "AddFontAgain");
        }

        [HarmonyPrefix]
        private static void Prefix(MethodBase __originalMethod, object[] __args, out nint __state)
        {
            __state = Pending;
            byte[] bytes = __originalMethod.Name == "RegisterFont" ? (byte[])__args[1]
                : (byte[])AccessTools.Property(__args[0].GetType(), "Data").GetValue(__args[0]);
            Pending = 0;
            if (bytes == null) return;
            if (!Ranges.TryGetValue(bytes, out var cached))
            {
                cached = new RangePointer();
                try
                {
                    uint[] ranges = FontCoverage.EmptyCjk(bytes);
                    if (ranges.Length != 0) cached.Value = GCHandle.Alloc(ranges, GCHandleType.Pinned).AddrOfPinnedObject();
                }
                catch (Exception ex) when (ex is InvalidDataException || ex is OverflowException)
                {
                    Log.Warning("[Custom Fonts] Unable to inspect empty CJK glyphs: " + ex.Message);
                }
                Ranges.Add(bytes, cached);
            }
            Pending = cached.Value;
        }

        [HarmonyFinalizer]
        private static void Finalizer(nint __state) => Pending = __state;
    }

    [HarmonyPatch]
    internal static class WaylithEmptyGlyphConfigPatch
    {
        private static MethodBase TargetMethod() => AccessTools.Method(
            AccessTools.TypeByName("RimWorldWaylith.Imgui.ImGuiHost"), "NewFontConfig");

        [HarmonyPostfix]
        private static unsafe void Postfix(ref ImFontConfig __result)
        {
            if (WaylithEmptyGlyphScopePatch.Pending == 0) return;
            __result.GlyphExcludeRanges = new Ptr<uint>((uint*)WaylithEmptyGlyphScopePatch.Pending);
            WaylithEmptyGlyphScopePatch.Pending = 0; // Only the primary face, not its fallbacks.
        }
    }
}
