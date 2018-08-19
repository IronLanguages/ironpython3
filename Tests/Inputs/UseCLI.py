# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import clr
import os
import sys

from iptest import IronPythonTestCase, is_netcoreapp

ip_test = os.path.join(sys.prefix, 'IronPythonTest.dll')
if not is_netcoreapp:
    clr.AddReference("System.Core")
    clr.AddReference('System.Windows.Forms')
    from System.Windows.Forms import Form, Control

clr.AddReference("Microsoft.Scripting")
clr.AddReference("Microsoft.Dynamic")
clr.AddReference("IronPython")
clr.AddReferenceToFileAndPath(ip_test)

from IronPythonTest import NestedClass
