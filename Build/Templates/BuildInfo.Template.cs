internal static class BuildInfo {{
    internal const string Configuration = "{0}";

    internal const bool IsDebug =
#if DEBUG
        true;
#else
        false;
#endif

    internal const bool IsCLR2 =
#if CLR2
        true;
#else
        false;
#endif

    internal const bool IsCLR4 =
#if CLR4
        true;
#else
        false;
#endif

    internal const bool IsSilverlight =
#if SILVERLIGHT
        true;
#else
        false;
#endif
}}