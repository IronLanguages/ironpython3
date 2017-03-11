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

hw_progid = "DlrPyWin32.HW"
hw_retval = "Hello World"

class K:
    _reg_clsid_ = "{E06A849F-71CB-4670-8ABE-312C49EB5E70}" #pythoncom.CreateGuid()
    _reg_desc_ = "pywin32 COM server"
    _reg_progid_ = hw_progid

    _public_methods_ = ['comMethod']
    
    def comMethod(self, p1):
        print(p1)
        return hw_retval
        
def install_pywin32_server():
    import win32com.server.register 
    win32com.server.register.UseCommandLine(K)
