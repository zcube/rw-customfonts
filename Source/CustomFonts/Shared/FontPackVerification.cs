using System;
using System.IO;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld.Planet;
using System.Linq;
using TMPro;
using UnityEngine;
using Verse;

namespace CustomFonts
{
    public partial class CustomFonts
    {
        private const string ExamplePackId = "zcubekr.customfonts.example.abeezee";
        private const string ExamplePackRegular = "(Bundled) [" + ExamplePackId + "] ABeeZee (Regular)";
        private const string ExamplePackItalic = "(Bundled) [" + ExamplePackId + "] ABeeZee (Italic)";
        private int _packStep, _packFrame;
        private readonly bool _packVerify = FontVerificationOptions.TryGet("customfonts-pack-verify", out _);
        partial void VerifyFontPackUiGlyphs();

        // Explicitly opt-in: the runner always supplies an isolated save-data folder.
        public void TickFontPackVerification()
        {
            if (!_packVerify || _packStep < 0 || Current.ProgramState != ProgramState.Playing
                || Event.current.type != EventType.Repaint || Time.frameCount < _packFrame) return;
            FontVerificationOptions.TryGet("customfonts-pack-verify", out string mode);
            try
            {
                if (_packStep == 0)
                {
                    SetupBundledFonts();
                    var mods = LoadedModManager.RunningModsListForReading;
                    var pack = mods.FirstOrDefault(m => m.PackageId.Equals(ExamplePackId, StringComparison.OrdinalIgnoreCase));
                    int count = _bundledFontSources.Values.Count(d => d.SelectionKey != null && d.SelectionKey.StartsWith("(Bundled) [" + ExamplePackId + "] ", StringComparison.Ordinal));
                    if (mode == "disabled" || mode == "before")
                    {
                        Require(count == 0 && !BundledFonts.ContainsKey(ExamplePackRegular), "excluded packs do not register fonts");
                        Require(mode == "disabled" ? pack == null : pack != null && mods.IndexOf(pack) < mods.IndexOf(_content),
                            "negative test uses the requested actual game mod order");
                        CompleteFontPackVerification("PASS: " + mode + " pack excluded by actual mod registration"); return;
                    }
                    Require(mode == "active" || mode == "restart", "known font-pack verification mode");
                    Require(pack != null && mods.IndexOf(pack) > mods.IndexOf(_content) && count == 2,
                        "actual active mod after Custom Fonts registers both example styles");
                    Require(pack.AnyContentLoaded(), "game recognizes a font-only pack as loaded content");
                    Require(DisplayFontName(ExamplePackRegular) == "(Bundled) ABeeZee (Regular) [" + pack.Name + "]",
                        "font label includes the actual mod display name");
                    if (mode == "restart")
                    {
                        Require(PreviewFonts.All(s => _settings.GetCurrentUIFontName(s) == ExamplePackRegular)
                            && _settings.CurrentWorldFontName == ExamplePackRegular, "pack primary selections survive restart");
                        VerifyFontPackFallbacks(); ShowFallbackTestWorld();
                    }
                    else
                    {
                        Require(BundledFonts[ExamplePackRegular] == null && BundledFonts[ExamplePackItalic] == null,
                            "unselected pack styles are cataloged without native font creation");
                        _settings.FontSourcePaths.Clear(); _settings.IncludeSystemFonts = false; _settings.RecursiveFontFolders = false;
                        _settings.UniformUIfonts = false;
                        foreach (FontTarget t in Enum.GetValues(typeof(FontTarget))) _settings.GetFallbackFonts(t).Clear();
                        RefreshFontSources();
                        Require(OSFontPaths.Count == 0 && BundledFonts.ContainsKey(ExamplePackRegular),
                            "pack discovery needs neither system fonts, user paths nor recursive-folder option");
                        foreach (var size in PreviewFonts) _settings.SetScaleFactor(size, 1f);
                        SaveFontAll(ExamplePackRegular); SaveWorldFont(ExamplePackRegular); ShowFallbackTestWorld();
                        Require(BundledFonts[ExamplePackItalic] == null, "choosing Regular leaves Italic unloaded");
                    }
                }
                else
                {
                    VerifyFontPackRendering();
                    if (mode == "restart")
                    {
                        VerifyFontPackFallbacks();
                        CompleteFontPackVerification("PASS: restart retains package keys, mod labels, primary/fallback settings and rendered UI/world glyphs"); return;
                    }
                    switch (_packStep)
                    {
                        case 1:
                            SaveFontAll(ExamplePackItalic); SaveWorldFont(ExamplePackItalic); break;
                        case 2:
                            foreach (FontTarget t in Enum.GetValues(typeof(FontTarget))) _settings.SetFallbackFonts(t, new[] { ExamplePackItalic });
                            SaveFontAll(ExamplePackRegular); SaveWorldFont(ExamplePackRegular); break;
                        case 3:
                            VerifyFontPackFallbacks();
                            int count = BundledFonts.Count; RefreshFontSources();
                            Require(BundledFonts.Count == count && PreviewFonts.All(s => _settings.GetCurrentUIFontName(s) == ExamplePackRegular),
                                "refresh preserves pack catalog cardinality and saved selection"); break;
                        default:
                            VerifyFontPackFallbacks(); WriteSettings();
                            CompleteFontPackVerification("PASS: actual mod order, lazy style discovery, mod-name labels, Regular/Italic UI/world rasterization, pack fallbacks and refresh; restart profile saved"); return;
                    }
                }
                _packStep++; _packFrame = Time.frameCount + 120;
            }
            catch (Exception ex) { CompleteFontPackVerification("FAIL: " + ex); }
        }

        private void VerifyFontPackFallbacks()
        {
            foreach (FontTarget t in Enum.GetValues(typeof(FontTarget)))
                Require(_settings.GetFallbackFonts(t).SequenceEqual(new[] { ExamplePackItalic })
                    && SourcesFor(t).Length == 1 && SourcesFor(t)[0].Name == ExamplePackItalic
                    && SourcesFor(t)[0].Revision == FallbackFontSource.Hash(FontFiles.Read(_bundledFontSources[ExamplePackItalic].Path)),
                    "pack fallback resolves the actual font bytes for " + t);
            VerifyWorldFallbackChain(GetWorldFontAsset());
        }

        private void VerifyFontPackRendering()
        {
            foreach (var size in PreviewFonts)
            {
                Require(CurrentFonts[size] != null && !ReferenceEquals(CurrentFonts[size], DefaultFonts[size])
                    && ReferenceEquals(Text.fontStyles[(int)size].font, CurrentFonts[size]), "pack font bound to UI: " + size);
                Require(Text.fontStyles[(int)size].CalcSize(new GUIContent("ABeeZee Wgj")).x > 0, "pack UI text measures: " + size);
            }
            VerifyFontPackUiGlyphs();
            var asset = GetWorldFontAsset();
            Require(asset != null && asset.sourceFontFile != null && asset != DefaultTMPFontAsset, "world uses imported pack font");
            var root = new GameObject("CustomFonts pack verification");
            try
            {
                var label = root.AddComponent<TextMeshPro>(); label.font = asset; label.text = "ABeeZeeWgj"; label.fontSize = 24;
                label.ForceMeshUpdate(true, true);
                Require(label.textInfo != null && label.textInfo.characterCount == 10
                    && label.textInfo.meshInfo.Sum(m => m.vertexCount) >= 40, "pack world text produces ten actual glyph quads");
                Require(label.textInfo.characterInfo.Take(10).Select(c => c.character).SequenceEqual("ABeeZeeWgj"),
                    "world glyphs retain requested characters instead of replacement squares");
            }
            finally { UnityEngine.Object.Destroy(root); }
            var labels = (IEnumerable<WorldFeatureTextMesh>)AccessTools.Field(typeof(WorldFeatures), "texts").GetValue(Find.WorldFeatures);
            int rendered = 0;
            foreach (var feature in labels.OfType<WorldFeatureTextMesh_TextMeshPro>())
            {
                var mesh = (TextMeshPro)AccessTools.Field(typeof(WorldFeatureTextMesh_TextMeshPro), "textMesh").GetValue(feature);
                Require(mesh != null && ReferenceEquals(mesh.font, asset), "live world label uses selected pack font");
                if (feature.Active && mesh.color.a > 0.01f && mesh.textInfo.meshInfo.Sum(m => m.vertexCount) > 0) rendered++;
            }
            Require(rendered > 0, "pack test includes visible live world-label geometry");
        }

        private void CompleteFontPackVerification(string result)
        {
            _packStep = -1;
            string message = "[Custom Fonts][pack-verify] " + result;
            if (result.StartsWith("PASS", StringComparison.Ordinal)) Log.Message(message); else Log.Error(message);
            if (FontVerificationOptions.TryGet("customfonts-pack-result", out string path)) File.WriteAllText(path, message + Environment.NewLine);
        }
    }
}
