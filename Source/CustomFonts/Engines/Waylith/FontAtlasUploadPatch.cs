using System;
using System.Reflection;
using Brutal.ImGuiApi;
using HarmonyLib;
using UnityStubs.Gui;

namespace CustomFonts
{
    // The engine uploads ImGui textures, then bakes/captures the TMP atlas. New
    // 48px glyphs can dirty the same native texture after that upload. Flush those
    // updates before EndFrame copies texture IDs into the render snapshot.
    [HarmonyPatch]
    internal static class WaylithFontAtlasUploadPatch
    {
        private static object _gui, _renderer;
        private static MethodInfo _flush;
        internal static int FlushCount { get; private set; }
        private static readonly Type BackendType = AccessTools.TypeByName("RimWorldWaylith.Imgui.ImGuiTextMeshProBackend");
        private static bool Prepare() => BackendType != null && AccessTools.Method(BackendType, "TryCaptureAtlas") != null;
        private static MethodBase TargetMethod() => AccessTools.Method(BackendType, "TryCaptureAtlas");

        [HarmonyPostfix]
        private static void Postfix()
        {
            bool pending = false;
            foreach (var texture in ImGui.GetIO().Fonts.TexList)
                if (texture.Status == ImTextureStatus.WantCreate || texture.Status == ImTextureStatus.WantUpdates)
                { pending = true; break; }
            if (!pending || GuiBackend.Current == null) return;
            if (!ReferenceEquals(_gui, GuiBackend.Current))
            {
                _gui = GuiBackend.Current;
                object host = AccessTools.Field(_gui.GetType(), "host")?.GetValue(_gui);
                _renderer = host == null ? null : AccessTools.Property(host.GetType(), "Renderer")?.GetValue(host);
                _flush = _renderer == null ? null : AccessTools.Method(_renderer.GetType(), "ProcessPendingTextureUpdates");
            }
            if (_flush == null) return;
            _flush.Invoke(_renderer, null);
            FlushCount++;
        }
    }
}
