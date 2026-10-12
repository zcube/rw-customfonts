#if RIMWORLD_1_7
using System;
using System.IO;
using HarmonyLib;
using UnityEngine;
using UnityStubs.Gui;

namespace CustomFonts
{
    internal static class WaylithFontLoader
    {
        private static readonly System.Reflection.MethodInfo SetFontData = AccessTools.Method(typeof(Font), "SetFontData");
        private static readonly System.Reflection.FieldInfo FontData = AccessTools.Field(typeof(Font), "_fontData");

        public static bool TryGetFaceName(byte[] data, out string faceName)
        {
            faceName = null;
            if (data == null || !FontNameReader.TryRead(data, out var family, out var style)) return false;
            faceName = family + " (" + style + ")";
            return true;
        }

        public static bool TryGetFaceName(Font font, out string faceName) => TryGetFaceName((byte[])FontData.GetValue(font), out faceName);

        public static bool TryGetFaceName(string path, out string faceName)
        {
            faceName = null;
            if (!FontNameReader.TryRead(path, out var family, out var style)) return false;
            faceName = family + " (" + style + ")";
            return true;
        }

        public static Font Load(string path, string name)
        {
            if (GuiBackend.Current == null) throw new InvalidOperationException("The GUI font backend is not ready.");
            var timer = System.Diagnostics.Stopwatch.StartNew();
            var data = File.ReadAllBytes(path);
            long readMs = timer.ElapsedMilliseconds;
            var font = Load(data, name);
            Verse.Log.Message($"[Custom Fonts] OS font '{name}': read {data.Length:N0} bytes in {readMs} ms, validate/register {timer.ElapsedMilliseconds - readMs} ms");
            return font;
        }

        public static Font Load(byte[] data, string name)
        {
            if (GuiBackend.Current == null) throw new InvalidOperationException("The GUI font backend is not ready.");
            if (!TryGetFaceName(data, out _)) throw new InvalidDataException("Invalid or unsupported font: " + name);
            var font = new Font { name = name };
            // Use one import size; GUIStyle supplies the actual size when drawing.
            SetFontData.Invoke(font, new object[] { data, 22f, 0 });
            if (font.GameFontIndex < 3) throw new InvalidDataException("The GUI backend could not register font: " + name);
            return font;
        }
    }
}
#endif
