# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import sys
import nt

from iptest.cominterop_util import is_pywin32
if not is_pywin32:
    print("pywin32 is not installed.  Skipping this test.")
    sys.exit(0)

if sys.platform=="win32":

    #Make sure we'll have access to pywin32
    if sys.prefix + "\\Lib" not in sys.path:
        sys.path.append(sys.prefix + "\\Lib")
    
    #Make sure we'll have access to cominterop_util
    if "." not in sys.path: sys.path.append(".")
    
#Next make sure pywintypes25.dll is in %Path%
cpy_location = nt.environ["SystemDrive"] + "\\Python" + sys.winver.replace(".", "")
if cpy_location not in nt.environ["Path"]:
    nt.putenv("Path", nt.environ["Path"] + ";" + cpy_location)

if sys.platform=="win32":
    #At this point it should be possible to install the pywin32com server
    from .hw import install_pywin32_server
    install_pywin32_server()


#--Run tests-------------------------------------------------------------------
#TODO