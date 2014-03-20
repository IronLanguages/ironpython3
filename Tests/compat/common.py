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

import nt
import sys
import time
import rulediff

is_cli = (sys.platform == "cli")

result_pass = 0
result_fail = 1

def file_exists(file):
    try:
        nt.stat(file)
        return True
    except:
        return False

def Assert(c):
    if not c: raise AssertionError("Assertion Failed")

def get_environ_variable(key):
    l = [nt.environ[x] for x in nt.environ.keys() if x.lower() == key.lower()]
    if l: return l[0]
    else: return None

def get_all_paths():
    if get_environ_variable('DLR_ROOT') and file_exists(get_environ_variable('DLR_ROOT')+'/External.LCA_RESTRICTED/Languages/ironpython/27/python.exe'):
        #We're in the new world
        if sys.platform == "cli":
            ipython_executable = sys.executable
            compat_test_path   = get_environ_variable('DLR_ROOT') + "/Languages/IronPython/Tests/Compat/"
            cpython_executable = get_environ_variable('DLR_ROOT')+'/External.LCA_RESTRICTED/Languages/ironpython/27/python.exe'
            cpython_lib_path   = get_environ_variable('DLR_ROOT')+'/External.LCA_RESTRICTED/Languages/ironpython/27/Lib'
            
        elif sys.platform == "win32":
            dlr_bin = get_environ_variable('DLR_BIN')
            if dlr_bin:
                if dlr_bin.startswith('"'):
                    # strip quotes when DLR_BIN has spaces, e.g. "bin\Debug\v4Debug"
                    Assert(dlr_bin.endswith('"'))
                    dlr_bin = dlr_bin[1:-1]
            elif not get_environ_variable('THISISSNAP'):
                dlr_bin = get_environ_variable('DLR_ROOT') + '/Bin/Debug'
            cpython_executable = sys.executable
            cpython_lib_path   = sys.prefix + "/Lib"
            ipython_executable = dlr_bin+'/ipy.exe'
            compat_test_path   = get_environ_variable('DLR_ROOT')+'/Languages/IronPython/Tests/Compat/'
        else:
            raise AssertionError        
    else:
        #We're in the old world
        if sys.platform == "cli":
            ipython_executable = sys.executable
            compat_test_path   = sys.prefix + "/Tests/Compat/"
            cpython_executable = sys.prefix + "/External.LCA_RESTRICTED/Python25/Python.exe"
            cpython_lib_path   = sys.prefix + "/External.LCA_RESTRICTED/Python25/Lib"
            
        elif sys.platform == "win32":
            cpython_executable = sys.executable
            cpython_lib_path   = sys.prefix + "/Lib"
            ipython_executable = sys.prefix + "/../../ipy.exe"
            compat_test_path   = sys.prefix + "/../../Tests/Compat/"
        else:
            raise AssertionError
    
    Assert(file_exists(cpython_executable))
    Assert(file_exists(cpython_lib_path))
    Assert(file_exists(ipython_executable))
    Assert(file_exists(compat_test_path))
    
    return cpython_executable, cpython_lib_path, ipython_executable, compat_test_path

cpython_executable, cpython_lib_path, ipython_executable, compat_test_path = get_all_paths()
 
def delete_files(files):
    for f in files: 
        nt.remove(f)

def launch(executable, test):
    return nt.spawnv(0, executable, (executable, test))
        
class my_stdout:
    def __init__(self, o):
        self.stdout = o
    def write(self, s):
        self.stdout.write(s)

# Transforms that make windiff logs more readable

def _common_transform(x):
    # Change +/-0.0 into +0.0
    if isinstance(x, float) and x == 0.0:
        return 0.0
    return x

if sys.platform == 'cli':
    from System import Byte, Int64
    def transform(x):
        x = _common_transform(x)
        if isinstance(x, type) and (x == Byte or x == Int64):
            return int
        return x
else:
    transform = _common_transform

def printwith(head, *arg): 
    print "%s##" %head,
    for x in arg: print transform(x),
    print

def printwithtype(arg):
    t = type(arg)
    if t == float:
        print "float## %.4f" % transform(arg)
    elif t == complex:
        print "complex## %.4f | %.4f" % (transform(arg.real), transform(arg.imag))
    else:
        print "same##", transform(arg)

def fullpath(file):
    return compat_test_path + file

def run_single_test(test, filename = None):
    if filename == None: 
        ret = test()
    else :
        filename = fullpath(filename)
        file = open(filename, "w+")
        saved = sys.stdout
        sys.stdout = my_stdout(file)
        ret = result_fail
        try:
            ret = test()
        finally:
            sys.stdout = saved
            file.close()
    return ret
        
def get_platform_string(current = None):
    ''' return my customized platform string
        if no param, it returns the string based on current python runtime
        if current is provide, this function is a mapping. 
    '''
    if current == None:
        import sys
        current = sys.platform
        
    if current.startswith("win"): return "win"
    if current.startswith("cli"): return "cli"
    return "non"

def create_new_file(filename):
    f = file(filename, "w")
    f.close()

def append_string_to_file(filename, *lines):
    f = file(filename, "a")
    for x in lines:
        f.writelines(x + "\n")
    f.close()

def get_class_name(type):
    typename = str(type)
    return typename[typename.rfind('.')+1:-2]

def get_file_name(type, test, platform = None):
    ''' return the log file for the specified test
    '''
    return "_".join((get_platform_string(platform), type.__module__, get_class_name(type), test)) + ".log"
    
def runtests(type):
    import sys
    obj = type()
    
    failCnt = 0
    for x in dir(type):
        if not x.startswith("test_"): continue
       
        test = getattr(obj, x)
        if not callable(test): continue
        typename = get_class_name(type)

        print 'Running', ('%s\\%s ...' % (typename, x)).ljust(50), 
        log_filename = get_file_name(type, x)
        run_single_test(test, log_filename)
        
        if is_cli:
            cli_log     = log_filename
            win_log     = 'win_' + log_filename[4:]
            dif_log     = 'dif_' + log_filename[4:]
            dif_logfile = open(fullpath(dif_log),"w+")

            print "comparing ...", 
            (val, summary) = rulediff.compare(fullpath(win_log), fullpath(cli_log), dif_logfile)
            
            if val == 0:
                print "PASS "
            else: 
                print "FAIL [%s]" % summary
                print "         windiff %s %s" % (win_log, cli_log)
                print "         notepad %s " % dif_log
                failCnt += 1
            dif_logfile.close()
        else : 
            print "skip comparing"
    
    if failCnt:
        raise AssertionError, "Failed"
            
class MyException: pass
