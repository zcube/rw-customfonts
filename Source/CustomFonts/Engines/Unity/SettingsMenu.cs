using System;
using System.IO;
using UnityEngine;
using Verse;

namespace CustomFonts
{
    public partial class CustomFonts
    {
        public void DrawEngineSettings(Rect inRect)
        {
            SetupPlatformInfo();
            var listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);

            listingStandard.Label("CustomFonts.SteamDeckPatch".Translate());
            GUI.enabled = false;
            var __hasUnityPlayerSo = _hasUnityPlayerSo;
            listingStandard.CheckboxLabeled("CustomFonts.HasUnityPlayerSo".Translate(), ref __hasUnityPlayerSo);
            var __hasRunHostFonts = _hasRunHostFonts;
            listingStandard.CheckboxLabeled("CustomFonts.HasRunHostFonts".Translate(), ref __hasRunHostFonts);
            GUI.enabled = _hasUnityPlayerSo && _hasRunHostFonts;
            if (listingStandard.ButtonTextLabeled("CustomFonts.ToRunHostFonts".Translate(), "CustomFonts.Patch".Translate()))
            {
                string UnityPlayerSoPath = Path.GetFullPath("UnityPlayer.so");
                ReplaceStringInBinaryFile(UnityPlayerSoPath, "/usr/share/fonts", "/run/host/fonts/");
            }
            GUI.enabled = _hasUnityPlayerSo;
            if (listingStandard.ButtonTextLabeled("CustomFonts.ToUsrShareFonts".Translate(), "CustomFonts.Patch".Translate()))
            {
                string UnityPlayerSoPath = Path.GetFullPath("UnityPlayer.so");
                ReplaceStringInBinaryFile(UnityPlayerSoPath, "/run/host/fonts/", "/usr/share/fonts");
            }
            GUI.enabled = true;

            DrawDebugSettings(listingStandard);

            listingStandard.End();
        }

    }
}
