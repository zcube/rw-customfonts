using System;

namespace CustomFonts
{
    internal static class FontVerificationOptions
    {
        internal static bool TryGet(string name, out string value) => TryGet(Environment.GetCommandLineArgs(), name, out value);

        internal static bool TryGet(string[] args, string name, out string value)
        {
            string key = "-" + name;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith(key + "=", StringComparison.Ordinal))
                {
                    value = args[i].Substring(key.Length + 1).Trim('"');
                    return true;
                }
            }
            value = null;
            return false;
        }
    }
}
