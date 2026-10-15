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
        private void VerifyEngineSizeFallbacks()
        {
            foreach (var size in PreviewFonts)
            {
                var style = Text.fontStyles[(int)size];
                int pixels = style.fontSize;
                GuiBackend.Current.FontMetrics(style.FontIndex, out float importSize, out _, out _);
                Require(importSize == pixels && NativeFontAccess.SourcesUseSize(style.FontIndex, pixels),
                    "primary and every merged fallback register at the UI size: " + size + "/" + pixels);
                Require(GuiBackend.Current.MeasureText(style.FontIndex, 0, style.fontStyle, "Wgj 한글", -1)
                    == GuiBackend.Current.MeasureText(style.FontIndex, pixels, style.fontStyle, "Wgj 한글", -1),
                    "size-0 input uses the same primary/fallback metrics: " + size);
            }
            Require(WaylithFontAtlasVerification.PendingFrames == 0, "all size changes upload before snapshot capture");
        }
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
        private static readonly bool Enabled = new[] { "customfonts-visual-verify", "customfonts-memory-verify", "customfonts-textfield-verify", "customfonts-verify", "customfonts-size-verify", "customfonts-size-restart-verify" }
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
