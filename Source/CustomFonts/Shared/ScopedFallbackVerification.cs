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
        private readonly bool _scopedFallbackVerify = FontVerificationOptions.TryGet("customfonts-scoped-fallback-verify", out _);
        private readonly bool _scopedFallbackRestart = FontVerificationOptions.TryGet("customfonts-scoped-fallback-restart-verify", out _);
        private int _scopedStep, _scopedFrame;
        private string[] _scopedKeys;
        private TMP_FontAsset _scopedWorldBefore;
        private WorldFeatureTextMesh _scopedLabelBefore;
        private IEnumerable<WorldFeatureTextMesh> ScopedWorldLabels =>
            (IEnumerable<WorldFeatureTextMesh>)AccessTools.Field(typeof(WorldFeatures), "texts").GetValue(Find.WorldFeatures);
        public void TickScopedFallbackVerification()
        {
            if ((!_scopedFallbackVerify && !_scopedFallbackRestart) || _scopedStep < 0
                || Current.ProgramState != ProgramState.Playing || Event.current.type != EventType.Repaint || Time.frameCount < _scopedFrame) return;
            try
            {
                if (_scopedFallbackRestart)
                {
                    if (_scopedStep == 0) { ShowFallbackTestWorld(); _scopedStep = 1; _scopedFrame = Time.frameCount + 180; return; }
                    Require(!_settings.UniformUIfonts && _settings.GetFallbackFonts(FontTarget.Small)[0] != _settings.GetFallbackFonts(FontTarget.Medium)[0],
                        "independent UI fallback order survives restart");
                    VerifyScopedFallbackGlyphs(false); VerifyWorldFallbackRendering();
                    Log.Message("[Custom Fonts][scoped-fallback] PASS: independent UI/world priorities and visible glyphs survive restart");
                    _scopedStep = -1; return;
                }
                switch (_scopedStep)
                {
                    case 0:
                        FontVerificationOptions.TryGet("customfonts-scoped-fallback-verify", out string path);
                        if (_settings.FallbackFontNames.Count > 0)
                            Require(Enum.GetValues(typeof(FontTarget)).Cast<FontTarget>().All(t => _settings.GetFallbackFonts(t).SequenceEqual(FontFallbackLists.Normalize(_settings.FallbackFontNames))),
                                "legacy global settings migrate to every tab");
                        _settings.UniformUIfonts = false; _settings.FontSourcePaths.Clear(); _settings.FontSourcePaths.Add(path);
                        _settings.IncludeSystemFonts = true;
                        foreach (FontTarget t in Enum.GetValues(typeof(FontTarget))) _settings.GetFallbackFonts(t).Clear();
                        RefreshFontSources();
                        _scopedKeys = OSFontPaths.Where(p => FontFiles.Enumerate(path, true).Contains(p.Value, FontSourceCatalog.PathComparer))
                            .Select(p => p.Key).OrderBy(k => k, StringComparer.Ordinal).Take(2).ToArray();
                        Require(_scopedKeys.Length == 2, "two private families available");
                        SaveFontAll("Arial"); SaveWorldFont("Arial");
                        _settings.SetFallbackFonts(FontTarget.Tiny, new[] { _scopedKeys[0] });
                        _settings.SetFallbackFonts(FontTarget.Small, new[] { _scopedKeys[1] });
                        _settings.SetFallbackFonts(FontTarget.Medium, _scopedKeys);
                        _settings.SetFallbackFonts(FontTarget.World, _scopedKeys.Reverse());
                        ApplyFallbackChanges(FontTarget.Medium); ShowFallbackTestWorld();
                        break;
                    case 1:
                        VerifyScopedFallbackGlyphs(false); VerifyWorldFallbackRendering();
                        Require(PreviewFonts.Select(f => CurrentFonts[f]).Distinct().Count() == 3, "different UI policies use independent font variants");
                        _scopedWorldBefore = GetWorldFontAsset();
                        _scopedLabelBefore = ScopedWorldLabels.First();
                        _settings.SetFallbackFonts(FontTarget.Medium, FontFallbackLists.Union(_settings.GetFallbackFonts(FontTarget.Medium),
                            _settings.GetFallbackFonts(FontTarget.Tiny), _settings.GetFallbackFonts(FontTarget.Small)));
                        _settings.UniformUIfonts = true; ApplyFallbackChanges(FontTarget.Medium);
                        break;
                    case 2:
                        VerifyScopedFallbackGlyphs(false); VerifyWorldFallbackRendering();
                        foreach (var size in PreviewFonts)
                            Require(ReferenceEquals(CurrentFonts[size], CurrentFonts[GameFont.Medium])
                                == (FontSizeKey(Text.fontStyles[(int)size].fontSize) == FontSizeKey(Text.fontStyles[(int)GameFont.Medium].fontSize)),
                                "uniform policies reuse a variant only when the engine's size key matches: " + size);
                        Require(ReferenceEquals(_scopedWorldBefore, GetWorldFontAsset()), "UI union leaves the world font asset unchanged");
                        Require(ScopedWorldLabels.Contains(_scopedLabelBefore), "UI union preserves existing world label objects");
                        _settings.UniformUIfonts = false;
                        _settings.SetFallbackFonts(FontTarget.Small, _scopedKeys.Reverse()); ApplyFallbackChanges(FontTarget.Small);
                        break;
                    case 3:
                        VerifyScopedFallbackGlyphs(false); VerifyWorldFallbackRendering();
                        Require(ReferenceEquals(_scopedWorldBefore, GetWorldFontAsset()), "Small-only priority change does not rebuild the world asset");
                        Require(ScopedWorldLabels.Contains(_scopedLabelBefore), "Small-only priority change preserves world label objects");
                        _settings.GetFallbackFonts(FontTarget.World).Clear(); SaveWorldFont(_scopedKeys[0]); ApplyFallbackChanges(FontTarget.World);
                        Require(!HasDefinedFallbacks && SourcesFor(FontTarget.Medium).Length == 2, "world removal preserves UI fallbacks");
                        break;
                    case 4:
                        VerifyScopedFallbackGlyphs(false);
                        Require(GetWorldFontAsset() != null && _fallbackAssets.Count == 0, "world primary renders without defined fallback assets");
                        _settings.SetFallbackFonts(FontTarget.World, _scopedKeys.Reverse()); ApplyFallbackChanges(FontTarget.World); SaveWorldFont("Arial");
                        SaveFontAll(FontSettings.DefaultFontName); SaveWorldFont(FontSettings.DefaultFontName);
                        break;
                    case 5:
                        VerifyScopedFallbackGlyphs(true); VerifyWorldFallbackRendering(); VerifyDefaultWorldResourceLifetime();
                        SaveFontAll("Arial"); SaveWorldFont("Arial"); WriteSettings();
                        break;
                    default:
                        VerifyScopedFallbackGlyphs(false); VerifyWorldFallbackRendering();
                        Require(PreviewFonts.All(f => _settings.GetCurrentUIFontName(f) == "Arial"), "portable primary families retained");
                        Log.Message("[Custom Fonts][scoped-fallback] PASS: per-tab UI/world glyph priorities, ordered union, uniform sharing, independent removal, delayed Default/restoration");
                        _scopedStep = -1; return;
                }
                _scopedStep++; _scopedFrame = Time.frameCount + (_scopedStep >= 5 ? 1200 : 180);
            }
            catch (Exception ex) { _scopedStep = -1; Log.Error("[Custom Fonts][scoped-fallback] FAIL: " + ex); }
        }
    }
}
