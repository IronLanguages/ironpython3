# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

## BE PLATFORM NETURAL

import os
import sys
from .test_env import is_cli, is_posix, is_netcoreapp, is_osx

class ProcessUtil(object):
    def has_csc(self):
        try:   self.run_csc("/?")
        except WindowsError: return False
        else:  return True

    def has_vbc(self):
        try:   self.run_vbc("/?")
        except WindowsError: return False
        else:  return True

    def has_ilasm(self):
        try:   self.run_ilasm("/?")
        except WindowsError: return False
        else:  return True

    def run_csc(self, args):
        if is_osx:
            return self.run_tool("/Library/Frameworks/Mono.framework/Versions/Current/Commands/mcs", args)
        elif is_posix:
            return self.run_tool("/usr/bin/mcs", args)
        else:
            return self.run_tool(os.path.join(self.get_clr_dir(),"csc.exe"), args)

    def run_vbc(self, args):
        if is_osx:
            return self.run_tool("/Library/Frameworks/Mono.framework/Versions/Current/Commands/vbnc", args)
        elif is_posix:
            return self.run_tool("/usr/bin/vbnc", args)
        else:
            return self.run_tool(os.path.join(self.get_clr_dir(),"vbc.exe"), args)

    def run_ilasm(self, args):
        if is_osx:
            return self.run_tool("/Library/Frameworks/Mono.framework/Versions/Current/Commands/ilasm", args)
        elif is_posix:
            return self.run_tool("/usr/bin/ilasm", args)
        else:
            return self.run_tool(os.path.join(self.get_clr_dir(),"ilasm.exe"), args)

    def run_tool(self, cmd, args=""):
        import clr
        import System
        if is_netcoreapp:
            clr.AddReference("System.Diagnostics.Process")
        process = System.Diagnostics.Process()
        process.StartInfo.FileName = cmd
        process.StartInfo.Arguments = args
        process.StartInfo.CreateNoWindow = True
        process.StartInfo.UseShellExecute = False
        process.StartInfo.RedirectStandardInput = False
        process.StartInfo.RedirectStandardOutput = False
        process.StartInfo.RedirectStandardError = False
        process.Start()
        process.WaitForExit()
        return process.ExitCode
    
    def launch(self, executable, *params):
        l = [ executable ] + list(params)
        return os.spawnv(0, executable, l)

    def get_clr_dir(self):
        import clr
        import System
        if is_netcoreapp:
            clr.AddReference("System.Runtime.Extensions")
            return System.IO.Path.Combine(System.Environment.GetEnvironmentVariable("windir"), r"Microsoft.NET\Framework\v4.0.30319")
        return System.IO.Path.GetDirectoryName(System.Type.GetType('System.Int32').Assembly.Location)

    clr_dir = property(get_clr_dir)

# one_arg_params = ("-X:Optimize", "-W", "-c", "-X:MaxRecursion", "-X:AssembliesDir")


# def launch_ironpython(pyfile, *args):
#     t = (pyfile, )
#     for arg in args: t += (arg, )
#     return launch(testpath.ipython_executable, *t)

# def launch_cpython(pyfile, *args):
#     t = (pyfile, )
#     for arg in args: t += (arg, )
#     return launch(testpath.cpython_executable, *t)

# def launch_ironpython_with_extensions(pyfile, extensions, args):
#     t = tuple(extensions)
#     t += (pyfile, )
#     for arg in args: t += (arg, )
#     return launch(testpath.ipython_executable, *t)

# def _get_ip_testmode():
#     import System
#     lastConsumesNext = False
#     switches = []
#     for param in System.Environment.GetCommandLineArgs():
#         if param.startswith('-T:') or param.startswith('-O:'): 
#             continue
#         if param.startswith("-"):
#             switches.append(param)
#             if param in one_arg_params:
#                 lastConsumesNext = True
#         else:
#             if lastConsumesNext:
#                  switches.append(param)   
#             lastConsumesNext = False
#     return switches

# def launch_ironpython_changing_extensions(test, add=[], remove=[], additionalScriptParams=()):
#     final = _get_ip_testmode()
#     for param in add:
#         if param not in final: final.append(param)
        
#     for param in remove:
#         if param in final:
#             pos = final.index(param)
#             if pos != -1:
#                 if param in one_arg_params:
#                     del final[pos:pos+2]
#                 else :
#                     del final[pos]
        
#     params = [sys.executable]
#     params.extend(final)
#     params.append(test)
#     params.extend(additionalScriptParams)
    
#     print "Starting process: %s" % params
    
#     return os.spawnv(0, sys.executable, params)





# def run_tlbimp(pathToTypeLib, outputName=None):
#     if outputName:
#         return run_tool("tlbimp.exe", pathToTypeLib+" /out:"+outputName)
#     else: 
#         return run_tool("tlbimp.exe", pathToTypeLib)

# def run_register_com_component(pathToDll):
#     return run_tool("regsvr32.exe",  "/s "+pathToDll)

# def run_unregister_com_component(pathToDll):
#     return run_tool("regsvr32.exe",  "/s /u "+pathToDll)



# def number_of_process(arg):
#     return len([x for x in os.popen('tasklist.exe').readlines() if x.lower().startswith(arg.lower()) ])

# def kill_process(arg):
#     return run_tool("taskkill.exe", '/F /IM %s' % arg)


