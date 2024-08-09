using System;

namespace IronPython {
    internal static class StringExtensions {
#if !NETCOREAPP
        public static bool EndsWith(this string str, char value) {
            return str.EndsWith(value.ToString(), StringComparison.Ordinal);
        }

        public static bool StartsWith(this string str, char value) {
            return str.StartsWith(value.ToString(), StringComparison.Ordinal);
        }
#endif
    }
}
