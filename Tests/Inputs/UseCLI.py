#####################################################################################
#
#  Copyright (c) Microsoft Corporation. All rights reserved.
#
# This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
# copy of the license can be found in the License.html file at the root of this distribution. If 
# you cannot locate the  Apache License, Version 2.0, please send an email to 
# ironpy@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
# by the terms of the Apache License, Version 2.0.
#
# You must not remove this notice, or any other, from this software.
#
#
#####################################################################################

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
