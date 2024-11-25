# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

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
