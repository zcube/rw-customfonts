using System;
using System.IO;
using System.Linq;
using HarmonyLib;
using Verse;

namespace CustomFonts
{
    // RimWorld only counts its built-in content types. Font-only add-ons are also
    // real content; recognize them without requiring dummy Defs or an assembly.
    [HarmonyPatch(typeof(ModContentPack), nameof(ModContentPack.AnyContentLoaded))]
    internal static class FontPackContentPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ModContentPack __instance, ref bool __result)
        {
            if (__result) return;
            var main = LoadedModManager.GetMod<CustomFonts>();
            if (main != null) __result = main.IsFontExpansionPack(__instance);
        }
    }

    public partial class CustomFonts
    {
        internal bool IsFontExpansionPack(ModContentPack pack)
        {
            var mods = LoadedModManager.RunningModsListForReading;
            if (mods.IndexOf(pack) <= mods.IndexOf(_content)) return false;
            foreach (string root in pack.foldersToLoadDescendingOrder)
            {
                string folder = Path.Combine(root, "Fonts");
                if (!Directory.Exists(folder)) continue;
                try { if (FontFiles.Enumerate(folder, true).Any(FontFiles.Supported)) return true; }
                catch (Exception) { /* Discovery reports individual source errors later. */ }
            }
            return false;
        }
    }
}
