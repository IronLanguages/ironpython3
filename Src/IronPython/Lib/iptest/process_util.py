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

## BE PLATFORM NETURAL

import nt
import sys
from assert_util import testpath, is_cli

one_arg_params = ("-X:Optimize", "-W", "-c", "-X:MaxRecursion", "-X:AssembliesDir")

def launch(executable, *params):
    l = [ executable ] + list(params)
    return nt.spawnv(0, executable, l)

def launch_ironpython(pyfile, *args):
    t = (pyfile, )
    for arg in args: t += (arg, )
    return launch(testpath.ipython_executable, *t)

def launch_cpython(pyfile, *args):
    t = (pyfile, )
    for arg in args: t += (arg, )
    return launch(testpath.cpython_executable, *t)

def launch_ironpython_with_extensions(pyfile, extensions, args):
    t = tuple(extensions)
    t += (pyfile, )
    for arg in args: t += (arg, )
    return launch(testpath.ipython_executable, *t)

def _get_ip_testmode():
    import System
    lastConsumesNext = False
    switches = []
    for param in System.Environment.GetCommandLineArgs():
        if param.startswith('-T:') or param.startswith('-O:'): 
            continue
        if param.startswith("-"):
            switches.append(param)
            if param in one_arg_params:
                lastConsumesNext = True
        else:
            if lastConsumesNext:
                 switches.append(param)   
            lastConsumesNext = False
    return switches

def launch_ironpython_changing_extensions(test, add=[], remove=[], additionalScriptParams=()):
    final = _get_ip_testmode()
    for param in add:
        if param not in final: final.append(param)
        
    for param in remove:
        if param in final:
            pos = final.index(param)
            if pos != -1:
                if param in one_arg_params:
                    del final[pos:pos+2]
                else :
                    del final[pos]
        
    params = [sys.executable]
    params.extend(final)
    params.append(test)
    params.extend(additionalScriptParams)
    
    print "Starting process: %s" % params
    
    return nt.spawnv(0, sys.executable, params)

def run_tool(cmd, args=""):
    import System
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

def has_csc():
    try:   run_csc("/?")
    except WindowsError: return False
    else:  return True

def has_vbc():
    try:   run_vbc("/?")
    except WindowsError: return False
    else:  return True

def has_ilasm():
    try:   run_ilasm("/?")
    except WindowsError: return False
    else:  return True

def run_tlbimp(pathToTypeLib, outputName=None):
    if outputName:
        return run_tool("tlbimp.exe", pathToTypeLib+" /out:"+outputName)
    else: 
        return run_tool("tlbimp.exe", pathToTypeLib)

def run_register_com_component(pathToDll):
    return run_tool("regsvr32.exe",  "/s "+pathToDll)

def run_unregister_com_component(pathToDll):
    return run_tool("regsvr32.exe",  "/s /u "+pathToDll)

def run_csc(args):
    import file_util
    return run_tool(file_util.path_combine(get_clr_dir(),"csc.exe"), args)

def run_vbc(args):
    import file_util
    return run_tool(file_util.path_combine(get_clr_dir(),"vbc.exe"), args)

def run_ilasm(args):
    import file_util
    return run_tool(file_util.path_combine(get_clr_dir(),"ilasm.exe"), args)

def number_of_process(arg):
    return len([x for x in nt.popen('tasklist.exe').readlines() if x.lower().startswith(arg.lower()) ])

def kill_process(arg):
    return run_tool("taskkill.exe", '/F /IM %s' % arg)

def get_clr_dir():
    import clr
    from System import Type
    from System.IO import Path
    return Path.GetDirectoryName(Type.GetType('System.Int32').Assembly.Location)
