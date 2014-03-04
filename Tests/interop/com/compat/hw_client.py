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

'''
For the time being this is a minimal sanity check designed to ensure IP can access
COM servers implemented in pywin32.
'''

import sys
from interop.com.compat.hw import hw_progid, hw_retval
from iptest.cominterop_util import *

if sys.platform=="cli":
    from System import Type, Activator
    type = Type.GetTypeFromProgID(hw_progid)
    com_obj = Activator.CreateInstance(type)

else:
    import win32com.client
    com_obj = win32com.client.Dispatch(hw_progid)

print "dir(obj):", dir(com_obj)
print

print "comMethod():", com_obj.comMethod(None)
AreEqual(com_obj.comMethod(None), hw_retval)
