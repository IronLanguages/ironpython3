# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import os
import sys
import time
import rulediff

is_cli = (sys.implementation.name == "ironpython")

result_pass = 0
result_fail = 1

def file_exists(file):
    try:
        os.stat(file)
        return True
    except:
        return False

def Assert(c):
    if not c: raise AssertionError("Assertion Failed")

def get_environ_variable(key):
    l = [os.environ[x] for x in list(os.environ.keys()) if x.lower() == key.lower()]
    if l: return l[0]
    else: return None

def get_all_paths():
    #We're in the new world
    if sys.implementation.name == "ironpython":
        ipython_executable = sys.executable
        compat_test_path   = os.path.join(get_environ_variable('DLR_ROOT'), "Languages", "IronPython", "Tests", "compat")
        if os.name=="posix":
            cpython_executable = '/usr/bin/python2.7'
            cpython_lib_path   = '/usr/lib/python2.7'
        else:
            cpython_executable = os.path.join(get_environ_variable('DLR_ROOT'), 'External.LCA_RESTRICTED', 'Languages', 'IronPython', '27', 'python.exe')
            cpython_lib_path   = os.path.join(get_environ_variable('DLR_ROOT'), 'External.LCA_RESTRICTED', 'Languages', 'IronPython', '27', 'Lib')

            
    elif sys.platform in ["win32", "darwin"] or sys.platform.startswith("linux"):
        dlr_bin = get_environ_variable('DLR_BIN')
        if dlr_bin:
            if dlr_bin.startswith('"'):
                # strip quotes when DLR_BIN has spaces, e.g. "bin\Debug\v4Debug"
                Assert(dlr_bin.endswith('"'))
                dlr_bin = dlr_bin[1:-1]
        elif not get_environ_variable('THISISSNAP'):
            dlr_bin = os.path.join(get_environ_variable('DLR_ROOT'), 'bin', 'Debug')
        cpython_executable = sys.executable
        cpython_lib_path   = sys.path[0]
        ipython_executable = os.path.join(dlr_bin, 'ipy.exe')
        compat_test_path   = os.path.join(get_environ_variable('DLR_ROOT'), 'Languages', 'IronPython', 'Tests', 'compat')

    else:
        raise AssertionError

    Assert(file_exists(cpython_executable))
    Assert(file_exists(cpython_lib_path))
    if is_cli:
        Assert(file_exists(ipython_executable))
    Assert(file_exists(compat_test_path))
    
    return cpython_executable, cpython_lib_path, ipython_executable, compat_test_path

cpython_executable, cpython_lib_path, ipython_executable, compat_test_path = get_all_paths()
 
def delete_files(files):
    for f in files: 
        os.remove(f)

def launch(executable, test):
    return os.spawnv(0, executable, (executable, test))
        
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

if sys.implementation.name == "ironpython":
    from System import Byte, Int64
    def transform(x):
        x = _common_transform(x)
        if isinstance(x, type) and (x == Byte or x == Int64):
            return int
        return x
else:
    transform = _common_transform

def printwith(head, *arg): 
    print("%s##" %head, end=' ')
    for x in arg: print(transform(x), end=' ')
    print()

def printwithtype(arg):
    t = type(arg)
    if t == float:
        print("float## %.4f" % transform(arg))
    elif t == complex:
        print("complex## %.4f | %.4f" % (transform(arg.real), transform(arg.imag)))
    else:
        print("same##", transform(arg))

def fullpath(file):
    return os.path.join(compat_test_path, file)

def run_single_test(test, filename = None):
    if not filename: 
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
    
    if any(current.startswith(x) for x in ["win", "linux", "darwin"]): return "nat"
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

        print('Running', ('%s\\%s ...' % (typename, x)).ljust(50), end=' ') 
        log_filename = get_file_name(type, x)
        run_single_test(test, log_filename)
        
        if is_cli:
            cli_log     = log_filename
            nat_log     = 'nat_' + log_filename[4:]
            dif_log     = 'dif_' + log_filename[4:]
            dif_logfile = open(fullpath(dif_log),"w+")

            print("comparing ...", end=' ') 
            (val, summary) = rulediff.compare(fullpath(nat_log), fullpath(cli_log), dif_logfile)
            
            if val == 0:
                print("PASS ")
            else: 
                print("FAIL [%s]" % summary)
                print("         windiff %s %s" % (nat_log, cli_log))
                print("         notepad %s " % dif_log)
                failCnt += 1
            dif_logfile.close()
        else : 
            print("skip comparing")
    
    if failCnt:
        raise AssertionError("Failed")
            
class MyException: pass
