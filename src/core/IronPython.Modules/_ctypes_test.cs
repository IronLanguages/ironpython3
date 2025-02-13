// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

#if FEATURE_CTYPES

using System;
using System.IO;
using System.Runtime.InteropServices;

using Mono.Unix.Native;

using IronPython.Runtime;
using System.Runtime.Versioning;

[assembly: PythonModule("_ctypes_test", typeof(IronPython.Modules.CTypesTest))]
namespace IronPython.Modules {
    public static class CTypesTest {

        public static string __file__ = Path.Combine(FindRoot(), "tests", "suite", GetPydName());

        private static string FindRoot() {
            // we start at the current directory and look up until we find the "src" directory
            var current = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var found = false;
            while (!found && !string.IsNullOrEmpty(current)) {
                var test = Path.Combine(current, "src", "core", "IronPython.StdLib", "lib");
                if (Directory.Exists(test)) {
                    return current;
                }

                current = Path.GetDirectoryName(current);
            }
            return string.Empty;
        }

        private static string GetPydName() {
            string OS  = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win"
                : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "darwin"
                : "linux";

            string arch;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                arch = IsArchitecutreArm64() ? "_arm64" : "_x86_64";
            } else {
                arch = Environment.Is64BitProcess ? "64" : "32";
            }

            return string.Format("_ctypes_test_{0}{1}.pyd", OS, arch);
        }

        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("macos")]
        private static bool IsArchitecutreArm64() {
#if NETCOREAPP
            return RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
#else
            if (Syscall.uname(out Utsname info) == 0) {
                return info.machine is "arm64" or "aarch64";
            }
            return false;
#endif
        }
    }
}

#endif
