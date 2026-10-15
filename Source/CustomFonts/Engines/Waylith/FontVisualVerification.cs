using System;
using System.Linq;
using UnityStubs.Gui;
using Verse;
using Brutal.ImGuiApi;
using HarmonyLib;
using System.Reflection;

namespace CustomFonts
{
    public partial class CustomFonts
    {
        private string DescribeVisualFontMetrics(string sample) => $"Atlas uploads {WaylithFontAtlasUploadPatch.FlushCount}, pending frames {WaylithFontAtlasVerification.PendingFrames} | " + string.Join(" | ", PreviewFonts.Select(size =>
        {
            var style = Text.fontStyles[(int)size];
            GuiBackend.Current.FontMetrics(style.FontIndex, out _, out float ascent, out _);
            NativeFontAccess.TryMetrics(style.FontIndex, style.fontSize, out var metrics);
            float bottom = 0;
            foreach (char c in sample)
                if (NativeFontAccess.TryGlyph(style.FontIndex, c, style.fontSize, out var glyph) && glyph.visible)
                    bottom = Math.Max(bottom, ascent - metrics.ascent + glyph.y1);
            return $"{size}: bottom {bottom:F1}, baseline {ascent:F1}/{metrics.ascent:F1}, offset {style.contentOffset.y:F1}";
        }));
    }

    [HarmonyPatch]
    internal static class WaylithFontAtlasVerification
    {
        private static readonly bool Enabled = new[] { "customfonts-visual-verify", "customfonts-memory-verify", "customfonts-textfield-verify", "customfonts-verify" }
            .Any(name => FontVerificationOptions.TryGet(name, out _));
        internal static int PendingFrames { get; private set; }
        private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("RimWorldWaylith.Imgui.ImGuiHost"), "EndFrame");
        [HarmonyPostfix]
        private static void Postfix()
        {
            if (!Enabled) return;
            foreach (var texture in ImGui.GetIO().Fonts.TexList)
                if (texture.Status == ImTextureStatus.WantCreate || texture.Status == ImTextureStatus.WantUpdates)
                {
                    PendingFrames++;
                    if (PendingFrames <= 20) Log.Warning($"[Custom Fonts][visual-verify] GPU atlas still pending after frame capture: {texture.Status}, {texture.Width}x{texture.Height}, frame {UnityEngine.Time.frameCount}");
                    break;
                }
        }
    }
}
