using System;
using System.Collections.Generic;
using System.Reflection;
using Brutal.ImGuiApi;
using HarmonyLib;
using UnityStubs.Gui;
using Verse;

namespace CustomFonts
{
    internal static class NativeFontAccess
    {
        private static object _gui;
        private static object _host;
        private static MethodInfo _fontMethod;
        private static string _language;
        private static readonly Dictionary<int, ImFontPtr> Fonts = new Dictionary<int, ImFontPtr>();
        internal static int Generation { get; private set; }

        private static ImFontPtr Font(int index)
        {
            var gui = GuiBackend.Current;
            string language = LanguageDatabase.activeLanguage?.folderName ?? "";
            if (!ReferenceEquals(_gui, gui) || language != _language)
            {
                _gui = gui;
                _language = language;
                _host = gui == null ? null : AccessTools.Field(gui.GetType(), "host")?.GetValue(gui);
                _fontMethod = _host == null ? null : AccessTools.Method(_host.GetType(), "Font", new[] { typeof(int) });
                Fonts.Clear();
                Generation++;
            }
            if (_fontMethod == null || index < 0) return default;
            if (!Fonts.TryGetValue(index, out var font))
            {
                // Use exactly the language-specific fallback chain used by DrawText.
                font = (ImFontPtr)_fontMethod.Invoke(_host, new object[] { index });
                Fonts[index] = font;
            }
            return font;
        }

        internal static bool TryGlyph(int index, int scalar, float size, out TmpGlyphMetrics glyph)
        {
            glyph = default;
            if (scalar < 0 || scalar > 0x10ffff || size <= 0) return false;
            var font = Font(index);
            if (font.IsNull()) return false;
            var baked = font.GetFontBaked(size);
            if (baked.IsNull()) return false;
            baked.LastUsedFrame = ImGui.GetFrameCount();
            var native = baked.FindGlyphNoFallback((uint)scalar);
            if (native.IsNull()) return false;
            glyph = new TmpGlyphMetrics
            {
                found = true,
                visible = native.X1 > native.X0 && native.Y1 > native.Y0,
                advanceX = native.AdvanceX,
                x0 = native.X0, y0 = native.Y0, x1 = native.X1, y1 = native.Y1,
                u0 = native.U0, v0 = native.V0, u1 = native.U1, v1 = native.V1
            };
            return true;
        }

        internal static bool TryMetrics(int index, float size, out TmpFontMetrics metrics)
        {
            metrics = default;
            var font = Font(index);
            if (font.IsNull()) return false;
            var baked = font.GetFontBaked(size);
            if (baked.IsNull()) return false;
            baked.LastUsedFrame = ImGui.GetFrameCount();
            metrics = new TmpFontMetrics
            {
                found = true, bakedPixelSize = size, ascent = baked.Ascent,
                descent = baked.Descent, lineHeight = baked.Ascent - baked.Descent
            };
            return true;
        }
    }
}
