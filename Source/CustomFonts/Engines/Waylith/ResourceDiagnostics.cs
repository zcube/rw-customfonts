using Verse;

namespace CustomFonts
{
    public partial class CustomFonts
    {
        private System.Collections.Generic.Dictionary<string, long> CaptureEngineFontMemory()
        {
            var snapshot = WaylithFontLifetime.Snapshot();
            snapshot["late atlas upload frames"] = WaylithFontAtlasVerification.PendingFrames;
            return snapshot;
        }
        private void VerifyRetiredEngineFontResources() => Require(WaylithFontLifetime.LiveCount == 0 && WaylithFontLifetime.PendingCount == 0
            && WaylithFontLifetime.CustomFallbackBufferCount == 0
            && WaylithFontLifetime.SharedSourceCount == 0 && WaylithFontAtlasVerification.PendingFrames == 0,
            "Default cleanup retires all Waylith custom native faces and source buffers");
        partial void LogEngineResourceStats(string reason)
        {
            Log.Message($"[Custom Fonts] Waylith resources ({reason}): "
                + string.Join(", ", WaylithFontLifetime.Snapshot()) + $"; retired={WaylithFontLifetime.ReleasedCount}");
        }

    }
}
