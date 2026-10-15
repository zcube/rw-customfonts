using System;
using System.Reflection;
using UnityEngine;
using UnityStubs.Gui;

namespace CustomFonts
{
    // 1.7.4980 added an optional richText parameter. Resolve the diagnostic
    // call at runtime so one DLL can verify both versions of the interface.
    internal static class WaylithTextMeasurement
    {
        private static readonly MethodInfo MeasureMethod = FindMethod();

        private static MethodInfo FindMethod()
        {
            var original = new[] { typeof(int), typeof(float), typeof(FontStyle), typeof(string), typeof(float) };
            var richText = new[] { typeof(int), typeof(float), typeof(FontStyle), typeof(string), typeof(float), typeof(bool) };
            return typeof(IGuiBackend).GetMethod("MeasureText", richText)
                ?? typeof(IGuiBackend).GetMethod("MeasureText", original)
                ?? throw new MissingMethodException(typeof(IGuiBackend).FullName, "MeasureText");
        }

        internal static Vector2 Measure(IGuiBackend backend, int index, float size, FontStyle style, string text, float wrapWidth)
        {
            object[] args = MeasureMethod.GetParameters().Length == 6
                ? new object[] { index, size, style, text, wrapWidth, true }
                : new object[] { index, size, style, text, wrapWidth };
            return (Vector2)MeasureMethod.Invoke(backend, args);
        }
    }
}
