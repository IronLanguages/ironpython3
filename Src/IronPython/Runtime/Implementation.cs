// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Reflection;

using IronPython.Runtime.Operations;

namespace IronPython.Runtime {
    [PythonType("sys.version_info")]
    public class VersionInfo : PythonTuple {
        public readonly int major;
        public readonly int minor;
        public readonly int micro;
        public readonly string releaselevel;
        public readonly int serial;

        private VersionInfo(int major, int minor, int micro, string releaselevel, int serial)
            : base(new object[] { major, minor, micro, releaselevel, serial }) {
            this.major = major;
            this.minor = minor;
            this.micro = micro;
            this.releaselevel = releaselevel;
            this.serial = serial;
        }

        internal static VersionInfo Instance { get; } = new VersionInfo(CurrentVersion.Major,
                   CurrentVersion.Minor,
                   CurrentVersion.Micro,
                   CurrentVersion.ReleaseLevel,
                   CurrentVersion.ReleaseSerial);

        public override string __repr__(CodeContext context) {
            return string.Format("sys.version_info(major={0}, minor={1}, micro={2}, releaselevel='{3}', serial={4})",
                major, minor, micro, releaselevel, serial);
        }

        internal int GetHexVersion() {
            int hexlevel = 0;
            switch (releaselevel) {
                case "alpha":
                    hexlevel = 0xA;
                    break;

                case "beta":
                    hexlevel = 0xB;
                    break;

                case "candidate":
                    hexlevel = 0xC;
                    break;

                case "final":
                    hexlevel = 0xF;
                    break;
            }

            return (major << 24) |
                   (minor << 16) |
                   (micro << 8) |
                   (hexlevel << 4) |
                   (serial << 0);
        }

        private string GetShortReleaseLevel() {
            switch (releaselevel) {
                case "alpha": return "a";
                case "beta": return "b";
                case "candidate": return "rc";
                case "final": return "f";
                default: return "";
            }
        }

        internal string GetVersionString() {
            return string.Format("{0}.{1}.{2}{3}{4}",
                major,
                minor,
                micro,
                releaselevel != "final" ? GetShortReleaseLevel() : string.Empty,
                releaselevel != "final" ? serial.ToString() : string.Empty);
        }
    }

    internal static class CurrentVersion {
        public static int Major { get; }
        public static int Minor { get; }
        public static int Micro { get; }
        public static string ReleaseLevel { get; }
        public static int ReleaseSerial { get; }
        public static string Series { get; }
        public static string DisplayName { get; }

        static CurrentVersion() {
            var assembly = typeof(CurrentVersion).Assembly;
            var version = assembly.GetName().Version;
            Major = version.Major;
            Minor = version.Minor;
            Micro = version.Build;
            Series = version.ToString(2);
            DisplayName = $"IronPython {version.ToString(3)}";
            var split = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            ReleaseLevel = split[split.Length - 2];
            ReleaseSerial = int.Parse(split[split.Length - 1]);
        }
    }
}
