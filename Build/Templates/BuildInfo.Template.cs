internal static class BuildInfo {{
    internal const string Configuration = "{0}";

    internal const bool IsDebug =
#if DEBUG
        true;
#else
        false;
#endif
}}
