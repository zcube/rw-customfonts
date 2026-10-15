using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using Verse;
using HarmonyLib;
using RimWorld.Planet;

namespace CustomFonts
{
    public partial class CustomFonts
    {
        private string _fallbackConfiguration;
        private bool _buildingFallbacks;
        private readonly FallbackFontSource[][] _targetFallbackSources = Enumerable.Range(0, 4).Select(_ => new FallbackFontSource[0]).ToArray();
        private readonly string[] _fallbackPolicyKeys = { "", "", "", "" };
        internal FallbackFontSource[] FallbackSources => SourcesFor(FontTarget.World);
        private FallbackFontSource[] SourcesFor(FontTarget target) => _targetFallbackSources[(int)target];
        private string FallbackPolicy(FontTarget target) => _fallbackPolicyKeys[(int)target];
        private readonly List<Font> _fallbackFonts = new List<Font>();
        private readonly List<TMP_FontAsset> _fallbackAssets = new List<TMP_FontAsset>();
        private readonly Vector2[] _propertyScroll = new Vector2[4];
        private readonly float[] _propertyContentHeight = { 400f, 400f, 400f, 400f };
        private string FallbackSignature => string.Join("|", Enumerable.Range(0, 4)
            .Select(i => FontFallbackLists.NamesKey(_settings.GetFallbackFonts((FontTarget)i))).ToArray());
        internal bool HasDefinedFallbacks => FallbackSources.Length != 0;
        private void EnsureFallbackConfiguration()
        {
            if (_buildingFallbacks || _fallbackConfiguration == FallbackSignature) return;
            _buildingFallbacks = true;
            try
            {
                var loaded = new Dictionary<string, FallbackFontSource>(StringComparer.OrdinalIgnoreCase);
                bool changed = false;
                foreach (FontTarget target in Enum.GetValues(typeof(FontTarget)))
                {
                    var sources = new List<FallbackFontSource>();
                    var seen = new HashSet<string>(StringComparer.Ordinal);
                    var names = _settings.GetFallbackFonts(target);
                    for (int i = 0; i < names.Count; i++)
                    {
                        string name = CanonicalFontSelection(names[i]); names[i] = name;
                        try
                        {
                            if (!loaded.TryGetValue(name, out var source))
                            {
                                byte[] bytes;
                                if (name.StartsWith("(Bundled) ", StringComparison.Ordinal))
                                {
                                    SetupBundledFonts();
                                    bytes = FontFiles.Read(_bundledFontSources[name].Path);
                                }
                                else
                                {
                                    SetupOSFontPaths();
                                    string key = FontSourceCatalog.FindKey(OSFontPaths, name);
                                    if (key == null) throw new FileNotFoundException("Fallback font is no longer in the configured sources.", name);
                                    bytes = FontFiles.Read(OSFontPaths[key]);
                                }
                                bytes = FontCollections.FirstFace(bytes);
                                string revision = FallbackFontSource.Hash(bytes);
                                string path = Path.Combine(GenFilePaths.ConfigFolderPath, "CustomFontsCache", "fallback-" + revision + ".ttf");
                                Directory.CreateDirectory(Path.GetDirectoryName(path));
                                if (!File.Exists(path) || !File.ReadAllBytes(path).SequenceEqual(bytes)) File.WriteAllBytes(path, bytes);
                                loaded[name] = source = new FallbackFontSource { Name = name, Path = path, Bytes = bytes, Revision = revision };
                            }
                            if (seen.Add(source.Revision)) sources.Add(source);
                        }
                        catch (Exception ex) { SourceError(name, ex); }
                    }
                    _settings.SetFallbackFonts(target, names);
                    string policy = FontFallbackLists.PolicyKey(sources);
                    bool targetChanged = _fallbackPolicyKeys[(int)target] != policy;
                    changed |= targetChanged;
                    if (target == FontTarget.World && targetChanged)
                    {
                        ReleaseWorldFontAsset();
                        foreach (var asset in _fallbackAssets) { ReleaseWorldFontResources(asset); QueueDestroy(asset); }
                        _fallbackAssets.Clear();
                        foreach (var font in _fallbackFonts) QueueDestroy(font);
                        _fallbackFonts.Clear();
                    }
                    _targetFallbackSources[(int)target] = sources.ToArray();
                    _fallbackPolicyKeys[(int)target] = policy;
                }
                if (changed) ResetEngineFallbackPolicy();
                _fallbackConfiguration = FallbackSignature;
                ForceLegacyText = UseLegacyWorldText(_settings.CurrentWorldFontName) && !HasDefinedFallbacks;
                AccessTools.StaticFieldRefAccess<bool>(typeof(WorldFeatures), "ForceLegacyText") = ForceLegacyText;
            }
            finally { _buildingFallbacks = false; }
        }
        // The isolated regression runner uses the no-target form to exercise the same
        // ordered list in every scope. Player controls always pass an explicit target.
        private void ApplyFallbackChanges(FontTarget? target = null)
        {
            string previousWorldPolicy = FallbackPolicy(FontTarget.World);
            string previousWorldFamily = _settings.CurrentWorldFontName;
            if (target == null)
                foreach (FontTarget scope in Enum.GetValues(typeof(FontTarget)))
                    _settings.SetFallbackFonts(scope, _settings.GetFallbackFonts(FontTarget.Medium));
            _settings.UpdateUniformUIfonts();
            _fallbackConfiguration = null; UpdateFont();
            if (previousWorldPolicy != FallbackPolicy(FontTarget.World) || previousWorldFamily != _settings.CurrentWorldFontName)
                RefreshWorldFeatureTexts();
            WriteSettings();
        }
        private void AttachWorldFallbacks(TMP_FontAsset asset, string primary)
        {
            if (!HasDefinedFallbacks || !UsesWorldFallbackAssets()) return;
            if (_fallbackAssets.Count == 0)
                foreach (var source in FallbackSources)
                {
                    Font font = CreateFallbackOnlyFont(source);
                    _fallbackFonts.Add(font); _fallbackAssets.Add(TMP_FontAsset.CreateFontAsset(font));
                }
            var engineFallbacks = asset.fallbackFontAssetTable ?? new List<TMP_FontAsset>();
            asset.fallbackFontAssetTable = _fallbackAssets.Where((a, i) => FallbackSources[i].Name != primary)
                .Concat(engineFallbacks).Where(a => a != null && a != asset).Distinct().ToList();
        }
        private void AddFallbackFont(FontTarget target, string name)
        {
            var names = _settings.GetFallbackFonts(target);
            if (name == FontSettings.DefaultFontName || names.Contains(name)) return;
            names.Add(name); ApplyFallbackChanges(target);
        }
        private void DrawFallbackSettings(Listing_Standard list, float width, FontTarget target)
        {
            var names = _settings.GetFallbackFonts(target);
            list.GapLine();
            list.Label("CustomFonts.FallbackFonts".Translate());
            list.Label("CustomFonts.FallbackTabHelp".Translate());
            int remove = -1, move = -1, direction = 0;
            for (int i = 0; i < names.Count; i++)
            {
                string name = names[i], label = (i + 1) + ". " + DisplayFontName(name);
                Rect row = list.GetRect(Math.Max(30, Text.CalcHeight(label, width - 105) + 6));
                Widgets.Label(new Rect(row.x, row.y, row.width - 105, row.height), label);
                TooltipHandler.TipRegion(row, name);
                if (i > 0 && Widgets.ButtonText(new Rect(row.xMax - 100, row.y, 28, 26), "↑")) { move = i; direction = -1; }
                if (i + 1 < names.Count && Widgets.ButtonText(new Rect(row.xMax - 70, row.y, 28, 26), "↓")) { move = i; direction = 1; }
                if (Widgets.ButtonText(new Rect(row.xMax - 40, row.y, 40, 26), "×")) remove = i;
            }
            if (names.Count == 0) list.Label("CustomFonts.FallbackEmpty".Translate());
            if (remove >= 0) { names.RemoveAt(remove); ApplyFallbackChanges(target); }
            else if (move >= 0)
            {
                string item = names[move]; names.RemoveAt(move); names.Insert(move + direction, item); ApplyFallbackChanges(target);
            }
            if (list.ButtonText("CustomFonts.UnionFallbacks".Translate()))
            {
                var options = Enum.GetValues(typeof(FontTarget)).Cast<FontTarget>().Where(t => t != target)
                    .Select(t => new FloatMenuOption(TargetLabel(t), () =>
                    {
                        _settings.SetFallbackFonts(target, FontFallbackLists.Union(_settings.GetFallbackFonts(target), _settings.GetFallbackFonts(t)));
                        ApplyFallbackChanges(target);
                    })).ToList();
                options.Add(new FloatMenuOption("CustomFonts.AllTabs".Translate(), () =>
                {
                    _settings.SetFallbackFonts(target, FontFallbackLists.Union(_settings.GetFallbackFonts(target),
                        Enum.GetValues(typeof(FontTarget)).Cast<FontTarget>().Where(t => t != target).Select(t => (IEnumerable<string>)_settings.GetFallbackFonts(t)).ToArray()));
                    ApplyFallbackChanges(target);
                }));
                Find.WindowStack.Add(new FloatMenu(options));
            }
        }
        private static string TargetLabel(FontTarget target)
            => (target == FontTarget.World ? "CustomFonts.FontWorld" : "CustomFonts.Font" + target).Translate("");
        private bool DrawFontChoice(Listing_Standard list, FontTarget target, string name, bool selected)
        {
            Rect row = list.GetRect(Text.LineHeight);
            Rect choose = new Rect(row.x, row.y, row.width - 36, row.height);
            TooltipHandler.TipRegion(choose, name);
            bool result = Widgets.RadioButtonLabeled(choose, DisplayFontName(name), selected);
            bool available = name != FontSettings.DefaultFontName && !selected && !_settings.GetFallbackFonts(target).Contains(name);
            bool previous = GUI.enabled; GUI.enabled = previous && available;
            Rect add = new Rect(row.xMax - 31, row.y, 28, row.height);
            if (Widgets.ButtonText(add, "+")) AddFallbackFont(target, name);
            TooltipHandler.TipRegion(add, "CustomFonts.AddFallbackFont".Translate());
            GUI.enabled = previous;
            return result;
        }
    }
}
