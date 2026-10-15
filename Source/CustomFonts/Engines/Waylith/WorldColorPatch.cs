using HarmonyLib;
using TMPro;
using UnityEngine;

namespace CustomFonts
{
    // Labels are built with alpha zero, then RimWorld fades their color in. Waylith's
    // TMP shim updates material.color in LateUpdate but keeps the original vertex
    // colors, leaving the draw fully transparent. Apply the tint through the material
    // once, with white vertices; never regenerate the already curved world geometry.
    [HarmonyPatch(typeof(TextMeshPro), "LateUpdate")]
    internal static class WaylithWorldColorPatch
    {
        [HarmonyPrefix]
        private static void Prefix(TextMeshPro __instance)
        {
            if (__instance.gameObject.layer != LayerMask.NameToLayer("WorldText")
                || !__instance.gameObject.activeInHierarchy || __instance.mesh == null) return;
            var info = __instance.textInfo?.meshInfo;
            if (info == null || info.Length == 0 || info[0].colors32 == null || info[0].colors32.Length == 0) return;
            var colors = info[0].colors32;
            var first = colors[0];
            if (first.r == 255 && first.g == 255 && first.b == 255 && first.a == 255) return;
            var white = new Color32(255, 255, 255, 255);
            for (int i = 0; i < colors.Length; i++) colors[i] = white;
            __instance.mesh.colors32 = colors;
            __instance.mesh.UploadMeshData(false);
        }
    }
}
