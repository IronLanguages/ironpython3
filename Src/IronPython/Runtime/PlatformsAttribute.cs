// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace IronPython.Runtime {
    public class PlatformsAttribute : Attribute {
        public enum PlatformFamily {
            Windows,
            Unix
        }

        public static readonly PlatformID[] WindowsFamily = { PlatformID.Win32NT, PlatformID.Win32S, PlatformID.Win32Windows, PlatformID.WinCE, PlatformID.Xbox };
        public static readonly PlatformID[] UnixFamily = { PlatformID.MacOSX, PlatformID.Unix };

        public PlatformID[] ValidPlatforms { get; protected set; }

        public bool IsPlatformValid => ValidPlatforms == null || ValidPlatforms.Length == 0 || Array.IndexOf(ValidPlatforms, ActualPlatform) >= 0;

        private static PlatformID ActualPlatform
            => RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? PlatformID.Unix :
               RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? PlatformID.MacOSX :
               Environment.OSVersion.Platform;

        protected void SetValidPlatforms(PlatformFamily validPlatformFamily) {
            switch (validPlatformFamily) {
                case PlatformFamily.Unix:
                    ValidPlatforms = UnixFamily;
                    break;
                default:
                    ValidPlatforms = WindowsFamily;
                    break;
            }
        }
    }
}