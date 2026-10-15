using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using TMPro;
using UnityEngine;
using Verse;

namespace CustomFonts
{
    public partial class CustomFonts
    {
        private List<string> VerificationFallbackNames => _settings.GetFallbackFonts(FontTarget.Medium);
        private int _fallbackTestStep, _fallbackTestFrame;
        private string[] _fallbackTestKeys;
        private readonly bool _fallbackVerifyEnabled = FontVerificationOptions.TryGet("customfonts-fallback-verify", out _);
        private readonly bool _fallbackRestartVerifyEnabled = FontVerificationOptions.TryGet("customfonts-fallback-restart-verify", out _);
        public void TickFallbackVerification()
        {
            if ((!_fallbackVerifyEnabled && !_fallbackRestartVerifyEnabled) || _fallbackTestStep < 0
                || Current.ProgramState != ProgramState.Playing || Event.current.type != EventType.Repaint || Time.frameCount < _fallbackTestFrame) return;
            try
            {
                if (_fallbackRestartVerifyEnabled)
                {
                    if (_fallbackTestStep == 0)
                    {
                        ShowFallbackTestWorld(); _fallbackTestStep = 1; _fallbackTestFrame = Time.frameCount + 180; return;
                    }
                    Require(VerificationFallbackNames.Count == 2, "saved fallback list survives restart");
                    VerifyFallbackRendering();
                    Log.Message("[Custom Fonts][fallback-verify] PASS: saved fallback order and rendered glyphs survive restart");
                    _fallbackTestStep = -1; return;
                }
                switch (_fallbackTestStep)
                {
                    case 0:
                        FontVerificationOptions.TryGet("customfonts-fallback-verify", out string path);
                        _settings.FontSourcePaths.Clear(); _settings.FontSourcePaths.Add(path);
                        _settings.IncludeSystemFonts = true; foreach (FontTarget t in Enum.GetValues(typeof(FontTarget))) _settings.GetFallbackFonts(t).Clear(); RefreshFontSources();
                        VerifySharedFontCatalog();
                        _fallbackTestKeys = OSFontPaths.Where(p => FontFiles.Enumerate(path, true).Contains(p.Value, FontSourceCatalog.PathComparer))
                            .Select(p => p.Key).OrderBy(k => k, StringComparer.Ordinal).Take(2).ToArray();
                        Require(_fallbackTestKeys.Length == 2, "two private fallback faces available");
                        string arial = FontSourceCatalog.FindKey(OSFontPaths, "Arial");
                        Require(arial != null, "Latin primary face available");
                        SaveFontAll(arial + " (Regular)");
                        SaveWorldFont("(File) " + arial + " (Regular) [/former-machine/arial.ttf]");
                        Require(PreviewFonts.All(f => _settings.GetCurrentUIFontName(f) == arial)
                            && _settings.CurrentWorldFontName == arial, "UI/world face and moved-file selections migrate to family");
                        VerificationFallbackNames.AddRange(_fallbackTestKeys.Select(k => "(File) " + k + " (Regular) [/former-machine/" + k + ".ttf]"));
                        ApplyFallbackChanges();
                        Require(VerificationFallbackNames.SequenceEqual(_fallbackTestKeys), "fallback moved-file selections migrate to portable families");
                        ShowFallbackTestWorld();
                        break;
                    case 1:
                        VerifyFallbackRendering();
                        VerificationFallbackNames.Reverse(); ApplyFallbackChanges();
                        break;
                    case 2:
                        VerifyFallbackRendering();
                        VerificationFallbackNames.RemoveAt(0); ApplyFallbackChanges();
                        break;
                    case 3:
                        VerifyFallbackRendering();
                        // Exercise removal with a primary that covers Hangul. Leaving
                        // Arial here intentionally removes all Korean coverage in Unity
                        // and makes the live test display replacement squares.
                        SaveFontAll(_fallbackTestKeys[0]); SaveWorldFont(_fallbackTestKeys[0]);
                        VerificationFallbackNames.Clear(); ApplyFallbackChanges();
                        break;
                    case 4:
                        Require(!HasDefinedFallbacks && _fallbackAssets.Count == 0, "fallback removal clears managed chain");
                        VerificationFallbackNames.AddRange(_fallbackTestKeys); ApplyFallbackChanges();
                        SaveFontAll(FontSettings.DefaultFontName); SaveWorldFont(FontSettings.DefaultFontName);
                        break;
                    case 5:
                        VerifyDefaultFallbackRendering();
                        VerifyWorldFallbackRendering();
                        Require(GetWorldFontAsset().fallbackFontAssetTable.Take(2).SequenceEqual(_fallbackAssets), "Default world text receives defined fallbacks first");
                        Require(!ForceLegacyText, "defined world fallback uses TMP renderer");
                        string selected = FontSourceCatalog.FindKey(OSFontPaths, "Arial");
                        SaveFontAll(selected); SaveWorldFont(selected);
                        WriteSettings();
                        break;
                    case 6:
                        VerifyFallbackRendering();
                        Require(BundledFonts.Values.All(f => f == null), "private fallback setup does not load unrelated bundle fonts");
                        VerificationFallbackNames.Clear(); VerificationFallbackNames.Add("(Bundled) HangeulNuri (Bold)"); ApplyFallbackChanges();
                        break;
                    case 7:
                        VerifyFallbackRendering();
                        VerificationFallbackNames.Clear(); VerificationFallbackNames.Add(FontSourceCatalog.FindKey(OSFontPaths, "Malgun Gothic")); ApplyFallbackChanges();
                        break;
                    case 8:
                        VerifyFallbackRendering();
                        VerificationFallbackNames.Clear(); VerificationFallbackNames.AddRange(_fallbackTestKeys); ApplyFallbackChanges();
                        break;
                    case 9:
                        VerifyFallbackRendering();
                        SaveFontAll(FontSettings.DefaultFontName); SaveWorldFont(FontSettings.DefaultFontName);
                        break;
                    case 10:
                        VerifyDefaultFallbackRendering(); VerifyWorldFallbackRendering();
                        VerifyDefaultWorldResourceLifetime();
                        string restored = FontSourceCatalog.FindKey(OSFontPaths, "Arial");
                        SaveFontAll(restored); SaveWorldFont(restored); WriteSettings();
                        break;
                    default:
                        VerifyFallbackRendering();
                        Log.Message("[Custom Fonts][fallback-verify] PASS: family migration, ordered private/bundled/system fallbacks, reorder/removal, delayed Default/restored UI and live world rendering");
                        _fallbackTestStep = -1; return;
                }
                _fallbackTestStep++; _fallbackTestFrame = Time.frameCount + (_fallbackTestStep >= 10 ? 1200 : 180);
            }
            catch (Exception ex) { _fallbackTestStep = -1; Log.Error("[Custom Fonts][fallback-verify] FAIL: " + ex); }
        }
        private void VerifyFallbackRendering()
        {
            Require(HasDefinedFallbacks && FallbackSources[0].Name == VerificationFallbackNames[0], "configured fallback order retained");
            VerifyDefinedFallbackGlyphs();
            VerifyWorldFallbackRendering();
        }
        private void VerifyWorldFallbackRendering()
        {
            TMP_FontAsset asset = GetWorldFontAsset();
            Require(asset != null, "world asset exists");
            VerifyWorldFallbackChain(asset);
            var root = new GameObject("CustomFonts fallback verification");
            try
            {
                var label = root.AddComponent<TextMeshPro>(); label.font = asset; label.text = "Fallback 한글폰트"; label.fontSize = 24;
                label.ForceMeshUpdate(true, true);
                Require(label.textInfo != null && label.textInfo.meshInfo.Sum(m => m.vertexCount) >= 4 * "Fallback한글폰트".Length,
                    "mixed Latin/Hangul world text produces glyph geometry");
                VerifyDefinedFallbackWorld(label);
                VerifyLiveFallbackLabels(asset);
            }
            finally { UnityEngine.Object.Destroy(root); }
        }
        private static void ShowFallbackTestWorld()
        {
            CameraJumper.TryShowWorld();
            // Fix the isolated quick-test camera at a scale where feature labels are
            // visible; testing only a hidden mesh would miss disappearance on the map.
            var driver = Find.WorldCameraDriver;
            AccessTools.Field(typeof(WorldCameraDriver), "altitude").SetValue(driver, 250f);
            AccessTools.Field(typeof(WorldCameraDriver), "desiredAltitude")?.SetValue(driver, 250f);
        }
        private void VerifyLiveFallbackLabels(TMP_FontAsset expected)
        {
            var labels = (IEnumerable<WorldFeatureTextMesh>)AccessTools.Field(typeof(WorldFeatures), "texts").GetValue(Find.WorldFeatures);
            int visible = 0;
            foreach (var label in labels.OfType<WorldFeatureTextMesh_TextMeshPro>())
            {
                var mesh = (TextMeshPro)AccessTools.Field(typeof(WorldFeatureTextMesh_TextMeshPro), "textMesh").GetValue(label);
                Require(mesh != null && mesh.font == expected, "live world label retains the selected asset");
                if (!label.Active || mesh.color.a <= 0.01f) continue;
                Require(mesh.textInfo.meshInfo.Sum(m => m.vertexCount) > 0 && mesh.fontMaterial?.mainTexture != null,
                    "visible world label retains glyph geometry and its atlas after font cleanup");
                VerifyDefinedFallbackWorld(mesh); visible++;
            }
            Require(visible > 0, "fallback check includes visible world labels");
            VerifyWorldFallbackBackend();
            Log.Message($"[Custom Fonts][fallback-verify] live world: {visible} visible labels, selection={_settings.CurrentWorldFontName}, frame={Time.frameCount}");
        }
        partial void VerifyWorldFallbackBackend();
    }
}
