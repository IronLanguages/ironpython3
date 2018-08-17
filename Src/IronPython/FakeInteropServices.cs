// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

namespace IronPython {
#if NET45
    internal static class RuntimeInformation {
        private static readonly OSPlatform _osPlatform;

        static RuntimeInformation() {
            switch (Environment.OSVersion.Platform) {
                case PlatformID.Unix:
                    if (Directory.Exists("/Applications") & Directory.Exists("/System") & Directory.Exists("/Users") & Directory.Exists("/Volumes"))
                        _osPlatform = OSPlatform.OSX;
                    else
                        _osPlatform = OSPlatform.Linux;
                    break;
                case PlatformID.MacOSX:
                    _osPlatform = OSPlatform.OSX;
                    break;
                default:
                    _osPlatform = OSPlatform.Windows;
                    break;
            }
        }

        public static bool IsOSPlatform(OSPlatform osPlatform) => _osPlatform == osPlatform;
    }

    internal readonly struct OSPlatform : IEquatable<OSPlatform> {
        private readonly string _osPlatform;

        public static OSPlatform Linux { get; } = new OSPlatform("LINUX");

        public static OSPlatform OSX { get; } = new OSPlatform("OSX");

        public static OSPlatform Windows { get; } = new OSPlatform("WINDOWS");

        private OSPlatform(string osPlatform) {
            _osPlatform = osPlatform;
        }

        public bool Equals(OSPlatform other) => _osPlatform.Equals(other._osPlatform);

        public override bool Equals(object obj) => obj is OSPlatform other && Equals(other);

        public override int GetHashCode() => _osPlatform == null ? 0 : _osPlatform.GetHashCode();

        public override string ToString() => _osPlatform ?? string.Empty;

        public static bool operator ==(OSPlatform left, OSPlatform right) => left.Equals(right);

        public static bool operator !=(OSPlatform left, OSPlatform right) => !(left == right);
    }
#endif
}
