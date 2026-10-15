using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CustomFonts
{
    public partial class CustomFonts
    {
        // Reference ownership and cache eviction are shared. Each engine releases
        // only its own native resources after the common deferred-destroy checks.
        private void PruneScopedFonts()
        {
            PruneFontVariants(_scopedDefaultFonts);
            PruneFontVariants(_bundledFontVariants);
            foreach (string name in BundledFonts.Where(p => !ReferenceEquals(p.Value, null)
                && !_bundledFontVariants.ContainsValue(p.Value)).Select(p => p.Key).ToArray())
                BundledFonts[name] = null;
            foreach (Font font in _registeredFileFonts.Values.Distinct().ToArray())
                if (font != null && !IsFontReferenced(font)) QueueDestroy(font);
        }

        private void PruneFontVariants(Dictionary<string, Font> variants)
        {
            foreach (string key in variants.Keys.ToArray())
            {
                Font font = variants[key];
                if (font != null && IsFontReferenced(font)) continue;
                variants.Remove(key);
                QueueDestroy(font);
            }
        }

        private Dictionary<string, long> CaptureFontMemory()
        {
            var snapshot = CaptureEngineFontMemory();
            snapshot["file font cache"] = _registeredFileFonts.Count;
            snapshot["path revisions"] = _fileFontCache.RevisionCount;
            snapshot["bundle variants"] = _bundledFontVariants.Count;
            snapshot["default variants"] = _scopedDefaultFonts.Count;
            return snapshot;
        }

        private void VerifyRetiredFontResources()
        {
            Require(_registeredFileFonts.Count == 0 && _fileFontCache.RevisionCount == 0
                && _bundledFontVariants.Count == 0 && _scopedDefaultFonts.Count == 0,
                "Default cleanup releases all shared font variants and path aliases");
            VerifyRetiredEngineFontResources();
        }
    }
}
