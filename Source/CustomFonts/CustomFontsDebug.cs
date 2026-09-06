using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld.Planet;
using UnityEngine;
using TMPro;

namespace CustomFonts
{
    // Diagnostics for the font resource lifetime: the toggleable debug log, the resource
    // counters that make a leak visible, and the stress test that drives font switching hard
    // enough to expose one. None of this runs unless the player turns debug logging on.
    public partial class CustomFonts
    {
        private const float DebugStatsInterval = 30f;
        private float _nextDebugStatsTime;

        // Font switch stress test state; it steps once every few frames so each font is really
        // drawn (and really allocates its atlas) before the next one replaces it.
        private const int StressFramesPerStep = 3;
        private static readonly float[] StressScales = { 0.8f, 1.0f, 1.2f, 1.5f };
        private bool _stressRunning;
        private int _stressIteration;
        private int _stressIterations;
        private int _stressLastFrame = -1;
        private float _stressStartedAt;
        private System.Random _stressRandom;
        private List<string> _stressCandidates;
        private List<string> _stressWorldCandidates;
        private Dictionary<GameFont, string> _stressOriginalNames;
        private Dictionary<GameFont, float> _stressOriginalScales;
        private string _stressOriginalWorldFontName;

        // World map font test state; it drives the real world view so the labels are actually
        // rebuilt and rendered between steps.
        private bool _worldTestRunning;
        private int _worldTestStep;
        private int _worldTestWaitFrames;
        private int _worldTestLastFrame = -1;
        private string _worldTestOriginalFontName;
        private string _worldTestAlternateFontName;

        public bool DebugLogging => _settings.DebugLogging;

        // Cycles through fonts the way a player hammering the font list and the scale slider
        // would. It deliberately runs across frames instead of in one loop: a dynamic font only
        // allocates its glyph atlas once text is actually drawn with it, so a same-frame loop
        // would never touch the memory the test is supposed to measure - and nothing would be
        // visible on screen either.
        public void BeginFontStressTest(int iterations = 200)
        {
            if (_stressRunning)
            {
                Log.Warning("[Custom Fonts] stress test already running");
                return;
            }

            SetupBundledFonts();
            SetupOSInstalledFontNames();

            _stressCandidates = new List<string>();
            _stressCandidates.AddRange(BundledFonts.Keys);
            _stressCandidates.AddRange(_fontNames);
            _stressCandidates.Add(FontSettings.DefaultFontName);
            if (_stressCandidates.Count == 0)
            {
                Log.Warning("[Custom Fonts] stress test skipped, no fonts available");
                return;
            }

            _stressWorldCandidates = new List<string> { _settings.CurrentWorldFontName };
            foreach (var name in BundledFonts.Keys)
            {
                if (_stressWorldCandidates.Count >= 3) break;
                if (!_stressWorldCandidates.Contains(name)) _stressWorldCandidates.Add(name);
            }

            _stressOriginalNames = new Dictionary<GameFont, string>();
            _stressOriginalScales = new Dictionary<GameFont, float>();
            foreach (GameFont value in Enum.GetValues(typeof(GameFont)))
            {
                _stressOriginalNames[value] = _settings.GetCurrentUIFontName(value);
                _stressOriginalScales[value] = _settings.GetScaleFactor(value);
            }
            _stressOriginalWorldFontName = _settings.CurrentWorldFontName;

            _stressRandom = new System.Random(20260907);
            _stressIterations = iterations;
            _stressIteration = 0;
            _stressLastFrame = -1;
            _stressStartedAt = Time.realtimeSinceStartup;
            _stressRunning = true;

            LogResourceStats($"stress test start ({iterations} steps over {_stressCandidates.Count} fonts, " +
                             $"{StressFramesPerStep} frame(s) per step)");
        }

        public void TickFontStressTest()
        {
            if (!_stressRunning) return;

            var frame = Time.frameCount;
            if (frame == _stressLastFrame) return;
            _stressLastFrame = frame;
            if (frame % StressFramesPerStep != 0) return;

            if (_stressIteration >= _stressIterations)
            {
                FinishFontStressTest();
                return;
            }

            var fontName = _stressCandidates[_stressRandom.Next(_stressCandidates.Count)];
            var scale = StressScales[_stressIteration % StressScales.Length];
            foreach (GameFont value in Enum.GetValues(typeof(GameFont)))
            {
                _settings.SetScaleFactor(value, scale);
                SaveFont(value, fontName, true);
            }

            // Same call the world map label patch makes, once per world feature label.
            if (_stressIteration % 25 == 0)
            {
                _settings.CurrentWorldFontName =
                    _stressWorldCandidates[(_stressIteration / 25) % _stressWorldCandidates.Count];
            }
            GetWorldFontAsset();

            VerifyAppliedFonts($"stress step {_stressIteration}");
            _stressIteration++;
        }

        private void FinishFontStressTest()
        {
            _stressRunning = false;

            foreach (GameFont value in Enum.GetValues(typeof(GameFont)))
            {
                _settings.SetScaleFactor(value, _stressOriginalScales[value]);
                SaveFont(value, _stressOriginalNames[value], true);
            }
            _settings.CurrentWorldFontName = _stressOriginalWorldFontName;
            GetWorldFontAsset();

            Log.Message($"[Custom Fonts][debug] stress test finished: {_stressIterations} steps in " +
                        $"{Time.realtimeSinceStartup - _stressStartedAt:F1}s, " +
                        $"{_pendingDestroy.Count} object(s) queued for destruction");
            LogResourceStats("stress test end (destruction still pending)");
            VerifyAppliedFonts("stress test end");

            // Settled numbers are what matters; the queue drains over the next frames.
            _nextDebugStatsTime = Time.realtimeSinceStartup + 5f;

            _stressCandidates = null;
            _stressWorldCandidates = null;
            _stressOriginalNames = null;
            _stressOriginalScales = null;
        }

        // Confirms the font the settings ask for is the one actually bound to RimWorld's styles,
        // so "the change did not show up in the UI" can be told apart from "the change was
        // applied but something drew with a stale style".
        public void VerifyAppliedFonts(string reason)
        {
            foreach (GameFont value in Enum.GetValues(typeof(GameFont)))
            {
                Font expected;
                if (!CurrentFonts.TryGetValue(value, out expected)) continue;

                var style = Text.fontStyles[(int)value];
                if (style.font == expected) continue;

                Log.Warning($"[Custom Fonts] {reason}: {value} style font is " +
                            $"'{(style.font == null ? "<null>" : style.font.name)}' " +
                            $"but '{(expected == null ? "<null>" : expected.name)}' was applied");
            }
        }

        public void DebugLog(string message)
        {
            if (!_settings.DebugLogging) return;
            Log.Message($"[Custom Fonts][debug] {message}");
        }

        public void TickDebugStats()
        {
            if (!_settings.DebugLogging)
            {
                _nextDebugStatsTime = 0f;
                return;
            }
            if (Time.realtimeSinceStartup < _nextDebugStatsTime) return;
            _nextDebugStatsTime = Time.realtimeSinceStartup + DebugStatsInterval;
            LogResourceStats("periodic");
        }

        // Snapshot of everything that the leaks used to grow without bound. Watch the counters
        // across a session: with the mod behaving, fonts / fontAssets / atlas stay flat while
        // switching fonts, moving the sliders and re-entering the world map.
        public void LogResourceStats(string reason)
        {
            var fonts = Resources.FindObjectsOfTypeAll<Font>();
            var fontAssets = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();

            long fontAtlasBytes = 0;
            foreach (var font in fonts)
            {
                if (font == null || font.material == null) continue;
                fontAtlasBytes += EstimateTextureBytes(font.material.mainTexture as Texture2D);
            }

            long fontAssetAtlasBytes = 0;
            foreach (var fontAsset in fontAssets)
            {
                if (fontAsset == null) continue;
                var atlasTextures = fontAsset.atlasTextures;
                if (atlasTextures == null) continue;
                foreach (var atlasTexture in atlasTextures)
                {
                    fontAssetAtlasBytes += EstimateTextureBytes(atlasTexture);
                }
            }

            Log.Message(
                $"[Custom Fonts][debug] stats ({reason})\n" +
                $"  Font objects       : {fonts.Length} (atlas ~{ToMegabytes(fontAtlasBytes)} MB)\n" +
                $"  TMP_FontAsset      : {fontAssets.Length} (atlas ~{ToMegabytes(fontAssetAtlasBytes)} MB)\n" +
                $"  owned OS fonts     : {_osFontCache.Count} [{string.Join(", ", _osFontCache.Keys.ToArray())}]\n" +
                $"  applied font keys  : {string.Join(", ", _appliedFontKeys.Select(pair => $"{pair.Key}={pair.Value}").ToArray())}\n" +
                $"  settings          : {DescribeSettings()}\n" +
                $"  line metrics       : {DescribeLineMetrics()}\n" +
                $"  bound styles       : {DescribeBoundStyles()}\n" +
                $"  world font         : setting '{_settings.CurrentWorldFontName}', cached asset {_worldFontAssetKey ?? "<none>"}\n" +
                $"  pending destroy    : {_pendingDestroy.Count}\n" +
                $"  known OS fonts     : {OSFontPaths.Count} paths, {_fontNames.Count} names, {BundledFonts.Count} bundled\n" +
                $"  mono heap          : {ToMegabytes(GC.GetTotalMemory(false))} MB");
        }

        // Opens the world map, switches the world font, and reports the font that is actually
        // on the live labels at each step. If the switch is not reflected the reported label
        // font simply does not change between the steps.
        public void BeginWorldFontTest()
        {
            if (_worldTestRunning)
            {
                Log.Warning("[Custom Fonts] world font test already running");
                return;
            }
            if (Current.ProgramState != ProgramState.Playing || Find.World == null)
            {
                Log.Warning("[Custom Fonts] world font test needs a loaded game");
                return;
            }

            SetupBundledFonts();
            _worldTestOriginalFontName = _settings.CurrentWorldFontName;

            // Both fonts must be non default: the default world font uses RimWorld's legacy
            // text meshes instead of TextMeshPro, so it would not exercise this path at all.
            _worldTestAlternateFontName = null;
            foreach (var name in BundledFonts.Keys)
            {
                if (name == _worldTestOriginalFontName) continue;
                _worldTestAlternateFontName = name;
                break;
            }
            if (_worldTestAlternateFontName == null)
            {
                Log.Warning("[Custom Fonts] world font test needs at least two bundled fonts");
                return;
            }
            if (_worldTestOriginalFontName == FontSettings.DefaultFontName)
            {
                _worldTestOriginalFontName = _worldTestAlternateFontName;
                foreach (var name in BundledFonts.Keys)
                {
                    if (name == _worldTestOriginalFontName) continue;
                    _worldTestAlternateFontName = name;
                    break;
                }
            }

            _worldTestStep = 0;
            _worldTestWaitFrames = 0;
            _worldTestLastFrame = -1;
            _worldTestRunning = true;
            Log.Message("[Custom Fonts][debug] world font test start: " +
                        $"'{_worldTestOriginalFontName}' -> '{_worldTestAlternateFontName}' -> back");
        }

        public void TickWorldFontTest()
        {
            if (!_worldTestRunning) return;

            var frame = Time.frameCount;
            if (frame == _worldTestLastFrame) return;
            _worldTestLastFrame = frame;
            if (_worldTestWaitFrames > 0)
            {
                _worldTestWaitFrames--;
                return;
            }

            switch (_worldTestStep)
            {
                case 0:
                    SaveWorldFont(_worldTestOriginalFontName);
                    CameraJumper.TryShowWorld();
                    _worldTestWaitFrames = 90;
                    break;
                case 1:
                    LogWorldTextFonts($"world map open, font '{_worldTestOriginalFontName}'");
                    SaveWorldFont(_worldTestAlternateFontName);
                    _worldTestWaitFrames = 90;
                    break;
                case 2:
                    LogWorldTextFonts($"switched to '{_worldTestAlternateFontName}'");
                    SaveWorldFont(_worldTestOriginalFontName);
                    _worldTestWaitFrames = 90;
                    break;
                case 3:
                    LogWorldTextFonts($"switched back to '{_worldTestOriginalFontName}'");
                    CameraJumper.TryHideWorld();
                    _worldTestWaitFrames = 30;
                    break;
                default:
                    _worldTestRunning = false;
                    Log.Message("[Custom Fonts][debug] world font test finished");
                    LogResourceStats("world font test end");
                    return;
            }
            _worldTestStep++;
        }

        // What the world labels are really drawn with right now. Reporting mesh.font alone
        // would only prove the asset was swapped, so this also reports whether the glyphs were
        // actually rasterised into the atlas and whether the labels carry real geometry - a
        // font can be assigned and still draw nothing.
        public void LogWorldTextFonts(string reason)
        {
            try
            {
                var prefab = WorldFeatureTextMesh_TextMeshPro.WorldTextPrefab;
                var prefabMesh = prefab == null ? null : prefab.GetComponent<TextMeshPro>();
                var prefabFont = prefabMesh == null ? "<no prefab>" : DescribeFontAsset(prefabMesh.font);

                var labelFonts = new Dictionary<string, int>();
                var labelCount = 0;
                var activeCount = 0;
                var withGeometry = 0;
                var visibleCount = 0;
                var totalCharacters = 0;
                var totalVertices = 0;
                var maxAlpha = 0f;

                if (Current.ProgramState == ProgramState.Playing && Find.World != null)
                {
                    var features = Find.WorldFeatures;
                    var texts = features == null
                        ? null
                        : HarmonyLib.AccessTools.Field(typeof(WorldFeatures), "texts").GetValue(features)
                            as IEnumerable<WorldFeatureTextMesh>;
                    if (texts != null)
                    {
                        foreach (var text in texts)
                        {
                            labelCount++;
                            var tmpText = text as WorldFeatureTextMesh_TextMeshPro;
                            if (tmpText == null)
                            {
                                AddCount(labelFonts, "<legacy text mesh>");
                                continue;
                            }
                            if (tmpText.Active) activeCount++;

                            var mesh = HarmonyLib.AccessTools
                                .Field(typeof(WorldFeatureTextMesh_TextMeshPro), "textMesh")
                                .GetValue(tmpText) as TextMeshPro;
                            if (mesh == null)
                            {
                                AddCount(labelFonts, "<no mesh>");
                                continue;
                            }

                            AddCount(labelFonts, DescribeFontAsset(mesh.font));

                            var characters = mesh.textInfo == null ? 0 : mesh.textInfo.characterCount;
                            var vertices = mesh.mesh == null ? 0 : mesh.mesh.vertexCount;
                            totalCharacters += characters;
                            totalVertices += vertices;
                            if (vertices > 0) withGeometry++;

                            var alpha = mesh.color.a;
                            if (alpha > maxAlpha) maxAlpha = alpha;
                            if (tmpText.Active && vertices > 0 && alpha > 0.01f) visibleCount++;
                        }
                    }
                }

                var breakdown = labelFonts.Count == 0
                    ? "<none>"
                    : string.Join(", ", labelFonts.Select(pair => $"{pair.Value}x '{pair.Key}'").ToArray());

                Log.Message($"[Custom Fonts][debug] world text fonts ({reason})\n" +
                            $"  setting     : {_settings.CurrentWorldFontName} (legacy text: {ForceLegacyText})\n" +
                            $"  world shown : {IsWorldRendered()}, camera altitude {GetWorldCameraAltitude():F0}\n" +
                            $"  prefab font : {prefabFont}\n" +
                            $"  label fonts : {labelCount} label(s) -> {breakdown}\n" +
                            $"  rasterised  : {DescribeWorldAtlas()}\n" +
                            $"  rendering   : {activeCount} active, {withGeometry} with geometry, " +
                            $"{visibleCount} visible (alpha>0), max alpha {maxAlpha:F2}\n" +
                            $"  geometry    : {totalCharacters} characters, {totalVertices} vertices");
            }
            catch (Exception ex)
            {
                Log.Warning($"[Custom Fonts] unable to read world text fonts: {ex.Message}");
            }
        }

        private static void AddCount(Dictionary<string, int> counts, string key)
        {
            int count;
            counts.TryGetValue(key, out count);
            counts[key] = count + 1;
        }

        // A dynamic TMP font asset starts with an empty atlas; characters only appear in these
        // tables once text has really been rendered with it, so a non zero count is proof the
        // labels were drawn rather than merely assigned the font.
        private string DescribeWorldAtlas()
        {
            var fontAsset = _worldFontAsset;
            if (fontAsset == null) return "<no mod owned world font asset>";
            var characters = fontAsset.characterTable == null ? -1 : fontAsset.characterTable.Count;
            var glyphs = fontAsset.glyphTable == null ? -1 : fontAsset.glyphTable.Count;
            var atlas = fontAsset.atlasTexture;
            var size = atlas == null ? "<no atlas>" : $"{atlas.width}x{atlas.height}";
            return $"{characters} characters, {glyphs} glyphs in atlas {size}";
        }

        private static float GetWorldCameraAltitude()
        {
            try
            {
                var driver = Find.WorldCameraDriver;
                if (driver == null) return float.NaN;
                return HarmonyLib.AccessTools.FieldRefAccess<WorldCameraDriver, float>(driver, "altitude");
            }
            catch
            {
                return float.NaN;
            }
        }

        // RimWorld 1.6 renamed WorldRenderedNow to WorldRendered, so it is resolved by name to
        // keep this file compiling against every supported game version.
        private static System.Reflection.MethodInfo _worldRenderedGetter;

        private static bool IsWorldRendered()
        {
            if (_worldRenderedGetter == null)
            {
                _worldRenderedGetter = HarmonyLib.AccessTools.PropertyGetter(typeof(WorldRendererUtility), "WorldRendered")
                                       ?? HarmonyLib.AccessTools.PropertyGetter(typeof(WorldRendererUtility), "WorldRenderedNow");
                if (_worldRenderedGetter == null) return false;
            }
            return (bool)_worldRenderedGetter.Invoke(null, null);
        }

        private static string DescribeFontAsset(TMP_FontAsset fontAsset)
        {
            if (fontAsset == null) return "<null>";
            return $"{fontAsset.faceInfo.familyName} ({fontAsset.faceInfo.styleName})";
        }

        // Dumps what is actually held in the settings object, so a value that was saved or
        // loaded into the wrong slot shows up directly next to what is bound and drawn.
        public string DescribeSettings()
        {
            var parts = new List<string>();
            foreach (GameFont value in Enum.GetValues(typeof(GameFont)))
            {
                parts.Add($"{value}: '{_settings.GetCurrentUIFontName(value)}' " +
                          $"scale {_settings.GetScaleFactor(value):F1} " +
                          $"offset {_settings.GetVerticalOffset(value)} " +
                          $"width {_settings.GetFontWidthScaleFactor(value):F1}");
            }
            parts.Add($"uniform {_settings.UniformUIfonts}");
            parts.Add($"world '{_settings.CurrentWorldFontName}' scale {_settings.WorldScaleFactor:F1}");
            return string.Join("; ", parts.ToArray());
        }

        public string DescribeLineMetrics()
        {
            var lineHeights = HarmonyLib.AccessTools.StaticFieldRefAccess<float[]>(typeof(Text), "lineHeights");
            var spaceBetweenLines = HarmonyLib.AccessTools.StaticFieldRefAccess<float[]>(typeof(Text), "spaceBetweenLines");
            var parts = new List<string>();
            foreach (GameFont value in Enum.GetValues(typeof(GameFont)))
            {
                var index = (int)value;
                var height = lineHeights != null && index < lineHeights.Length ? lineHeights[index] : float.NaN;
                var spacing = spaceBetweenLines != null && index < spaceBetweenLines.Length ? spaceBetweenLines[index] : float.NaN;
                parts.Add($"{value}=h{height:F1}/s{spacing:F1}");
            }
            return string.Join(", ", parts.ToArray());
        }

        private string DescribeBoundStyles()
        {
            var parts = new List<string>();
            foreach (GameFont value in Enum.GetValues(typeof(GameFont)))
            {
                var style = Text.fontStyles[(int)value];
                var name = style.font == null ? "<null>" : style.font.name;
                parts.Add($"{value}='{name}' {style.fontSize}px");
            }
            return string.Join(", ", parts.ToArray());
        }

        private static string ToMegabytes(long bytes) => (bytes / (1024.0 * 1024.0)).ToString("F1");

        private static long EstimateTextureBytes(Texture2D texture)
        {
            if (texture == null) return 0;
            int bytesPerPixel;
            switch (texture.format)
            {
                case TextureFormat.Alpha8:
                case TextureFormat.R8:
                    bytesPerPixel = 1;
                    break;
                case TextureFormat.RGB24:
                    bytesPerPixel = 3;
                    break;
                default:
                    bytesPerPixel = 4;
                    break;
            }
            return (long)texture.width * texture.height * bytesPerPixel;
        }

        public void DrawDebugSettings(Listing_Standard listingStandard)
        {
            listingStandard.GapLine();
            listingStandard.CheckboxLabeled("CustomFonts.DebugLogging".Translate(), ref _settings.DebugLogging,
                "CustomFonts.DebugLoggingDesc".Translate());
            GUI.enabled = _settings.DebugLogging;
            if (listingStandard.ButtonTextLabeled("CustomFonts.DebugLogStats".Translate(), "CustomFonts.DebugLogNow".Translate()))
            {
                LogResourceStats("manual");
            }
            if (listingStandard.ButtonTextLabeled("CustomFonts.DebugStressTest".Translate(), "CustomFonts.DebugStressTestRun".Translate()))
            {
                BeginFontStressTest();
            }
            if (listingStandard.ButtonTextLabeled("CustomFonts.DebugWorldFontTest".Translate(), "CustomFonts.DebugWorldFontTestRun".Translate()))
            {
                BeginWorldFontTest();
            }
            GUI.enabled = true;
            GUI.enabled = true;
        }
    }
}
