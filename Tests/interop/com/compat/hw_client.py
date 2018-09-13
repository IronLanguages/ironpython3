# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

'''
For the time being this is a minimal sanity check designed to ensure IP can access
COM servers implemented in pywin32.
'''

import sys
from interop.com.compat.hw import hw_progid, hw_retval
from iptest.cominterop_util import *

if sys.implementation.name == "ironpython":
    from System import Type, Activator
    type = Type.GetTypeFromProgID(hw_progid)
    com_obj = Activator.CreateInstance(type)

else:
    import win32com.client
    com_obj = win32com.client.Dispatch(hw_progid)

print("dir(obj):", dir(com_obj))
print()

print("comMethod():", com_obj.comMethod(None))
AreEqual(com_obj.comMethod(None), hw_retval)
