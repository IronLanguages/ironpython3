// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 license.
// See the LICENSE file in the project root for more information.

#nullable enable

namespace System.Runtime.Versioning {
#if !FEATURE_OSPLATFORMATTRIBUTE
    internal abstract class OSPlatformAttribute : Attribute {
        private protected OSPlatformAttribute(string platformName) {
            PlatformName = platformName;
        }
        public string PlatformName { get; }
    }

    [AttributeUsage(AttributeTargets.Assembly |
                AttributeTargets.Class |
                AttributeTargets.Constructor |
                AttributeTargets.Enum |
                AttributeTargets.Event |
                AttributeTargets.Field |
                AttributeTargets.Method |
                AttributeTargets.Module |
                AttributeTargets.Property |
                AttributeTargets.Struct,
                AllowMultiple = true, Inherited = false)]
    internal sealed class SupportedOSPlatformAttribute : OSPlatformAttribute {
        public SupportedOSPlatformAttribute(string platformName) : base(platformName) {
        }
    }

    [AttributeUsage(AttributeTargets.Assembly |
                AttributeTargets.Class |
                AttributeTargets.Constructor |
                AttributeTargets.Enum |
                AttributeTargets.Event |
                AttributeTargets.Field |
                AttributeTargets.Method |
                AttributeTargets.Module |
                AttributeTargets.Property |
                AttributeTargets.Struct,
                AllowMultiple = true, Inherited = false)]
    internal sealed class UnsupportedOSPlatformAttribute : OSPlatformAttribute {
        public UnsupportedOSPlatformAttribute(string platformName) : base(platformName) {
        }

        public UnsupportedOSPlatformAttribute (string platformName, string? message) : base(platformName) {
            Message = message;
        }

        public string? Message { get; }
    }
#endif
}
