// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

using Mono.Unix.Native;

using IronPython.Runtime;

[assembly: PythonModule("fcntl", typeof(IronPython.Modules.PythonFcntl), PlatformsAttribute.PlatformFamily.Unix)]
namespace IronPython.Modules;

[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
public static class PythonFcntl {

    public const string __doc__ = """
        This module performs file control and I/O control on file descriptors.
        It is an interface to the fcntl() and ioctl() Unix routines.
        File descriptors can be obtained with the fileno() method of
        a file object.
        """;


    // FD Flags
    public static int FD_CLOEXEC = 1;
    public static int FASYNC => RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? 0x0040 : 0x2000;


    #region Generated FD Commands

    // *** BEGIN GENERATED CODE ***
    // generated by function: generate_FD_commands from: generate_os_codes.py


    [PythonHidden(PlatformID.MacOSX)]
    [SupportedOSPlatform("linux")]
    public static int F_ADD_SEALS => 1033;

    public static int F_DUPFD => 0;

    [PythonHidden(PlatformID.MacOSX)]
    [SupportedOSPlatform("linux")]
    public static int F_EXLCK => 4;

    public static int F_GETFD => 1;

    public static int F_GETFL => 3;

    [PythonHidden(PlatformID.MacOSX)]
    [SupportedOSPlatform("linux")]
    public static int F_GETLEASE => 1025;

    public static int F_GETLK => RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? 7 : 5;

    [PythonHidden(PlatformID.MacOSX)]
    [SupportedOSPlatform("linux")]
    public static int F_GETLK64 => 5;

    public static int F_GETOWN => RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? 5 : 9;

    [PythonHidden(PlatformID.MacOSX)]
    [SupportedOSPlatform("linux")]
    public static int F_GETSIG => 11;

    [PythonHidden(PlatformID.MacOSX)]
    [SupportedOSPlatform("linux")]
    public static int F_GET_SEALS => 1034;

    [PythonHidden(PlatformID.Unix)]
    [SupportedOSPlatform("macos")]
    public static int F_NOCACHE => 48;

    [PythonHidden(PlatformID.MacOSX)]
    [SupportedOSPlatform("linux")]
    public static int F_NOTIFY => 1026;

    [PythonHidden(PlatformID.MacOSX)]
    [SupportedOSPlatform("linux")]
    public static int F_OFD_GETLK => 36;

    [PythonHidden(PlatformID.MacOSX)]
    [SupportedOSPlatform("linux")]
    public static int F_OFD_SETLK => 37;

    [PythonHidden(PlatformID.MacOSX)]
    [SupportedOSPlatform("linux")]
    public static int F_OFD_SETLKW => 38;

    public static int F_RDLCK => RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? 1 : 0;

    [PythonHidden(PlatformID.MacOSX)]
    [SupportedOSPlatform("linux")]
    public static int F_SEAL_GROW => 4;

    [PythonHidden(PlatformID.MacOSX)]
    [SupportedOSPlatform("linux")]
    public static int F_SEAL_SEAL => 1;

    [PythonHidden(PlatformID.MacOSX)]
    [SupportedOSPlatform("linux")]
    public static int F_SEAL_SHRINK => 2;

    [PythonHidden(PlatformID.MacOSX)]
    [SupportedOSPlatform("linux")]
    public static int F_SEAL_WRITE => 8;

    public static int F_SETFD => 2;

    public static int F_SETFL => 4;

    [PythonHidden(PlatformID.MacOSX)]
    [SupportedOSPlatform("linux")]
    public static int F_SETLEASE => 1024;

    public static int F_SETLK => RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? 8 : 6;

    [PythonHidden(PlatformID.MacOSX)]
    [SupportedOSPlatform("linux")]
    public static int F_SETLK64 => 6;

    public static int F_SETLKW => RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? 9 : 7;

    [PythonHidden(PlatformID.MacOSX)]
    [SupportedOSPlatform("linux")]
    public static int F_SETLKW64 => 7;

    public static int F_SETOWN => RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? 6 : 8;

    [PythonHidden(PlatformID.MacOSX)]
    [SupportedOSPlatform("linux")]
    public static int F_SETSIG => 10;

    [PythonHidden(PlatformID.MacOSX)]
    [SupportedOSPlatform("linux")]
    public static int F_SHLCK => 8;

    public static int F_UNLCK => 2;

    public static int F_WRLCK => RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? 3 : 1;

    // *** END GENERATED CODE ***

    #endregion


    #region Generated LOCK Flags

    // *** BEGIN GENERATED CODE ***
    // generated by function: generate_LOCK_flags from: generate_os_codes.py


    public static int LOCK_EX => 0x2;

    [PythonHidden(PlatformID.MacOSX)]
    [SupportedOSPlatform("linux")]
    public static int LOCK_MAND => 0x20;

    public static int LOCK_NB => 0x4;

    [PythonHidden(PlatformID.MacOSX)]
    [SupportedOSPlatform("linux")]
    public static int LOCK_READ => 0x40;

    [PythonHidden(PlatformID.MacOSX)]
    [SupportedOSPlatform("linux")]
    public static int LOCK_RW => 0xc0;

    public static int LOCK_SH => 0x1;

    public static int LOCK_UN => 0x8;

    [PythonHidden(PlatformID.MacOSX)]
    [SupportedOSPlatform("linux")]
    public static int LOCK_WRITE => 0x80;

    // *** END GENERATED CODE ***

    #endregion


    // Linux only
    #region Generated Directory Notify Flags

    // *** BEGIN GENERATED CODE ***
    // generated by function: generate_DN_flags from: generate_os_codes.py


    [PythonHidden(PlatformID.MacOSX)]
    [SupportedOSPlatform("linux")]
    public static int DN_ACCESS => 0x1;

    [PythonHidden(PlatformID.MacOSX)]
    [SupportedOSPlatform("linux")]
    public static int DN_ATTRIB => 0x20;

    [PythonHidden(PlatformID.MacOSX)]
    [SupportedOSPlatform("linux")]
    public static int DN_CREATE => 0x4;

    [PythonHidden(PlatformID.MacOSX)]
    [SupportedOSPlatform("linux")]
    public static int DN_DELETE => 0x8;

    [PythonHidden(PlatformID.MacOSX)]
    [SupportedOSPlatform("linux")]
    public static int DN_MODIFY => 0x2;

    [PythonHidden(PlatformID.MacOSX)]
    [SupportedOSPlatform("linux")]
    public static long DN_MULTISHOT => 0x80000000;

    [PythonHidden(PlatformID.MacOSX)]
    [SupportedOSPlatform("linux")]
    public static int DN_RENAME => 0x10;

    // *** END GENERATED CODE ***

    #endregion
}