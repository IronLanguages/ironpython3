# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import os
import sys
import platform

#------------------------------------------------------------------------------

#--IronPython or something else?
is_cli        =  sys.implementation.name == 'ironpython'
is_ironpython =  is_cli
is_cpython    =  sys.implementation.name == 'cpython'
is_windows    =  sys.platform == 'win32'
is_linux      =  sys.platform == 'linux'
is_osx        =  sys.platform == 'darwin'
is_posix      =  is_linux or is_osx
is_arm64      =  platform.machine().lower() in ['aarch64', 'arm64']

is_netcoreapp =  False
is_netcoreapp21 = False
is_netcoreapp31 = False
is_net60 = False
is_net70 = False
is_net80 = False
is_net90 = False
is_mono = False
is_netstandard = False
if is_ironpython:
    import clr
    is_netcoreapp = clr.IsNetCoreApp
    is_netcoreapp21 = clr.FrameworkDescription.startswith(".NET Core 2.x")
    is_netcoreapp31 = clr.FrameworkDescription.startswith(".NET Core 3.1")
    is_net60 = clr.FrameworkDescription.startswith(".NET 6.0")
    is_net70 = clr.FrameworkDescription.startswith(".NET 7.0")
    is_net80 = clr.FrameworkDescription.startswith(".NET 8.0")
    is_net90 = clr.FrameworkDescription.startswith(".NET 9.0")
    is_mono = clr.IsMono
    is_netstandard = clr.TargetFramework.startswith(".NETStandard")

#--The bittedness of the Python implementation
is_cli32, is_cli64 = False, False
if is_ironpython:
    import System
    is_cli32, is_cli64 = (System.IntPtr.Size == 4), (System.IntPtr.Size == 8)

is_32, is_64 = is_cli32, is_cli64
if not is_ironpython:
    import struct
    ptr_size = struct.calcsize('P')
    is_32, is_64 = (ptr_size == 4), (ptr_size == 8)

#--CLR version we're running on (if any)

is_net40 = False
is_net45 = False
is_net45Or46 = False
is_net46 = False
if is_cli:
    if is_netcoreapp: clr.AddReference("System.Runtime.Extensions")
    version = System.Environment.Version
    is_net40 = version.Major == 4
    is_net45 = is_net40 and version.Minor == 0 and version.Build == 30319 and version.Revision < 42000
    is_net45Or46 = is_net40 and version.Minor == 0 and version.Build == 30319
    is_net46 = is_net40 and version.Minor == 0 and version.Build == 30319 and version.Revision == 42000

#--Newlines
if is_ironpython:
    newline = System.Environment.NewLine
else:
    newline = os.linesep

#--Build flavor of Python being tested
is_debug = False
if is_cli:
    is_debug = clr.IsDebug

#--Are we using peverify to check that all IL generated is valid?
is_peverify_run = False
if is_cli:
    is_peverify_run = is_debug and "-X:SaveAssemblies" in System.Environment.CommandLine

#--We only run certain time consuming test cases in the stress lab
is_stress = False
# if get_env_var("THISISSTRESS")!=None:
#     is_stress = True

#------------------------------------------------------------------------------
