# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

'''
OVERVIEW:
This script outputs a log file showing missing modules, module members, etc 
from:
- builtin CPython modules 
- *.pyd files
- modules which IronPython recreates

Also, it creates another log file showing extra methods IP implements which
it should not.

USAGE:
    ipy cmodule.py C:\Python26

NOTES:
- the BUILTIN_MODULES list needs to be updated using the info found at 
  pydoc.org (e.g., http://pydoc.org/2.5.1/) for every new IronPython
  release corresponding to a new major CPython release
'''

import sys
import gc
import nt
import re
from clr_helpers import Process, File, Directory

CPY_DIR = sys.argv[1]  #E.g., C:\Python26

#--GLOBALS---------------------------------------------------------------------

#CPython builtin modules
BUILTIN_MODULES =  [
                    "__builtin__",
                    "_ast", #CodePlex 21088
                    "_bisect", #CodePlex 21392
                    "_codecs",
                    "_codecs_cn", #CodePlex 15507
                    "_codecs_hk", #CodePlex 15507
                    "_codecs_iso2022", #CodePlex 21394
                    "_codecs_jp", #CodePlex 15507
                    "_codecs_kr", #CodePlex 15507
                    "_codecs_tw", #CodePlex 15507
                    "_collections", 
                    "_csv", #CodePlex 21395
                    "_functools",
                    "_heapq", #CodePlex 21396
                    "_hotshot", #CodePlex 21397
                    "_json", #CodePlex 19581
                    "_locale",
                    "_lsprof", #CodePlex 21398
                    "_md5",
                    "_multibytecodec", #CodePlex 21399
                    "_random",
                    "_sha",
                    "_sha256",
                    "_sha512",
                    "_sre",
                    "_struct",
                    "_symtable", #IronPython incompat
                    "_subprocess", #CodePlex 15512
                    #"_types",  Can't import this in CPython 2.6 either...
                    "_warnings", 
                    "_weakref", 
                    "_winreg", 
                    "array", 
                    "aidoop", #CodePlex 21400
                    "binascii", 
                    "cPickle", 
                    "cStringIO", 
                    "cmath", 
                    "datetime", 
                    "errno",
                    "exceptions",
                    "future_builtins", #CodePlex 19580
                    "gc",
                    "imageop", #Deprecated in CPy 2.6.  Removed in Cpy 3.0
                    "imp",
                    "itertools", 
                    "marshal",
                    "math", 
                    "mmap", #CodePlex 21401
                    "msvcrt", #CodePlex 21402
                    "nt", 
                    "operator", 
                    "parser", #CodePlex 1347 - Won't fix
                    "signal", #CodePlex 16414
                    "strop", #CodePlex 21403
                    "sys",
                    "thread",
                    "time", 
                    "xxsubtype",
                    "zipimport", #CodePlex 391
                    "zlib", #CodePlex 2590
                    "_subprocess",
                    "msvcrt",
                    ]
 
#Most of these are standard *.py modules which IronPython overrides for one 
#reason or another
OVERRIDDEN_MODULES =  [ 
            "copy_reg",
            "socket",
        ]

MODULES = BUILTIN_MODULES + OVERRIDDEN_MODULES
           
#Add any extension modules found in DLLs or Lib
for x in nt.listdir(CPY_DIR + "\\DLLs"):
    if x.endswith(".pyd"):
        MODULES.append(x.split(".pyd", 1)[0])
        
for x in nt.listdir(CPY_DIR + "\\Lib"):
    if x.endswith(".pyd"):
        MODULES.append(x.split(".pyd", 1)[0])

#Modules we don't implement found in DLLs or Lib and
#the reason why:
# bz2 - TODO?
# pyexpat - CodePlex 20023
# unicodedata - CodePlex 21404
# winsound - CodePlex 21405
# _bsddb - CodePlex 362
# _ctypes - CodePlex 374
# _ctypes_test - dependent upon CodePlex 374
# _elementtree - CodePlex 21407
# _hashlib - CodePlex 21408
# _msi - CodePlex 21409
# _multiprocessing - CodePlex 19542
# _socket - N/A.  We already implement socket.py directly in C#
# _sqlite3 - CodePlex 21410
# _ssl - CodePlex 21411
# _testcapi N/A. This tests the C API for CPython
# _tkinter - TODO?


#Let the user override modules from the command-line
if len(sys.argv)==3:
    MODULES = [sys.argv[2]]



#TODO: each of these members attached to objects include MANY more
#      members IP does not implement
recursive_functions = [   'capitalize', 'center', 'count', 'decode',
                    'encode', 'endswith', 'expandtabs', 'find', 'index', 
                    'isalnum', 'isalpha', 'isdigit', 'islower', 'isspace', 
                    'istitle', 'isupper', 'join', 'ljust', 'lower', 'lstrip', 
                    'partition', 'replace', 'rfind', 'rindex', 'rjust', 
                    'rpartition', 'rsplit', 'rstrip', 'split', 'splitlines', 
                    'startswith', 'strip', 'swapcase', 'title', 'translate', 
                    'upper', 'zfill',
                    'denominator', 'numerator', 'imag', 'conjugate', 'real',
                    ]

#The maximum recursion depth used when examining the attributes of any 
#CPython module.
MAX_DEPTH = 10


IGNORE_LIST = [ "__builtin__.print",
                ]

BUG_REPORT_PRE = """Implement rest of %s module


IP VERSION AFFECTED: %s
FLAGS PASSED TO IPY.EXE: None
OPERATING SYSTEMS AFFECTED: All

DESCRIPTION
"""

REGEX_FILTER = "\."
REGEX_FILTER += "(__del__)|(__new__)|(__eq__)|(__ne__)|(__gt__)|(__lt__)"
REGEX_FILTER += "|(__ge__)|(__le__)|(__subclasshook__)|(__sizeof__)|(__trunc__)"
REGEX_FILTER += "|(__cmp__)|(__radd__)|(__contains__)|(__mod__)|(__mul__)"
REGEX_FILTER += "|(__rmod__)|(__rmul__)|(__sub__)|(__div__)|(__float__)|(__index__)"
REGEX_FILTER += "|(__int__)|(__iter__)|(__long__)|(__setslice__)|(__unicode__)"
REGEX_FILTER += "|(__weakref__)|(__get__)|(__delete__)|(__package__)"
REGEX_FILTER += "|(conjugate)|(fromhex)|(hex)|(imag)|(real)|(as_integer_ratio)|(is_integer)" #New float methods in 2.6
REGEX_FILTER += "$"

#--FUNCTIONS-------------------------------------------------------------------
def ip_missing(mod_attr):
    '''
    Logs a module or module attribute IP is missing.
    '''
    IPY_SHOULD_IMPL.write(mod_attr + "\n")
    IPY_SHOULD_IMPL.flush() 
    #print mod_attr
    
def ip_extra(mod_attr):
    '''
    Logs a module attribute IP provides, but should not.
    '''
    IPY_SHOULD_NOT_IMPL.write(mod_attr + "\n")
    IPY_SHOULD_NOT_IMPL.flush() 
    #print mod_attr    


def get_cpython_results(name, level=0, temp_mod=None):
    '''
    Recursively gets all attributes of a CPython module up to a depth of
    MAX_DEPTH.
    '''
    #from the name determine the module
    if "." in name:
        mod_name, rest_of_name = name.split(".", 1)
        rest_of_name += "."
    else:
        #we're looking at the module for the first time...import it
        mod_name, rest_of_name = name, ""
        
        try:
            temp_mod = __import__(mod_name)
        except ImportError as e:
            ip_missing(mod_name)
            return

    #Get the results of:
    #   python.exe -c 'import abc;print dir(abc.xyz)'
    proc = Process()
    proc.StartInfo.FileName = CPY_DIR + "\\python.exe"
    proc.StartInfo.Arguments = "-c \"import " + mod_name + ";print dir(" + name + ")\""
    proc.StartInfo.UseShellExecute = False
    proc.StartInfo.RedirectStandardOutput = True
    if (not proc.Start()):
        raise "CPython process failed to start"
    else:
        cpymod_dir = proc.StandardOutput.ReadToEnd()
        
    #Convert "['a', 'b']" to a real (sorted) list
    cpymod_dir = eval(cpymod_dir)
    cpymod_dir.sort()
    
    #Determine what IronPython implements
    if level==0:
        ipymod_dir_str = "dir(temp_mod)"
    else:
        ipymod_dir_str = "dir(temp_mod." + name.split(".", 1)[1] + ")"

    
    try:
        ipymod_dir = eval(ipymod_dir_str)
        #Ensure this is also present CPython
        for x in [y for y in ipymod_dir if cpymod_dir.count(y)==0]:
            ip_extra(name + "." + x)
    except TypeError as e:
        #CodePlex 15715
        if not ipymod_dir_str.endswith(".fromkeys)"):
            print("ERROR:", ipymod_dir_str)
            raise e
    
    #Look through all attributes the CPython version of the 
    #module implements
    for x in cpymod_dir:
        if name + "." + x in IGNORE_LIST:
            print("Will not reflect on", name + "." + x)
            return
    
        #Check if IronPython is missing the CPython attribute
        try:    
            temp = eval("temp_mod." + rest_of_name + x)
        except AttributeError as e:
            ip_missing(name + "." + x)
            continue
        
        #Skip these as they will recurse forever
        if x.startswith("__") and x.endswith("__"):
            continue
        #Skip these as they overload the log files
        elif x in ["_Printer__setup", "im_class", "_Printer__name", "func_code", "func_dict", "func_globals"]:
            continue 
        #Each of these functions has many __*__ methods
        elif x in recursive_functions and level > 2:
            continue
        #Skip these as they recurse forever
        elif name.startswith("datetime") and x in ["min", "max", "resolution"]:
            continue
        elif level>=MAX_DEPTH:
            print("Recursion too deep:", name, x)
            continue
        get_cpython_results(name + "." + x, level+1, temp_mod)
    
    return

def gen_bug_report(mod_name, needs_to_be_implemented, needs_to_be_removed):
    bug_report_name = "bug_reports\\%s.log" % mod_name
    bug_report = open(bug_report_name, "w")
    bug_report.write(BUG_REPORT_PRE % (mod_name, str(sys.winver)))
    
    bug_report.write("-------------------------------------------------------\n")
    bug_report.write("""After filtering out Python special method names, 
IronPython is still MISSING implementations for the 
following module attributes:
""")
    for x in needs_to_be_implemented:
        if re.search(REGEX_FILTER, x)==None:
            bug_report.write("    " + x)
    bug_report.write("\n\n")
    
    bug_report.write("-------------------------------------------------------\n")
    bug_report.write("""After filtering out Python special method names, 
IronPython is still PROVIDING implementations for the 
following module attributes which should NOT exist:
""")
    for x in needs_to_be_removed:
        if re.search(REGEX_FILTER, x)==None:
            bug_report.write("    " + x)
    bug_report.write("\n\n")
    
    #--unfiltered list of attributes to be added
    if len(needs_to_be_implemented)>0:
        bug_report.write("-------------------------------------------------------\n")
        bug_report.write("""Complete list of module attributes IronPython is still 
missing implementations for:
""")
        for x in needs_to_be_implemented:
            bug_report.write("    " + x)
        bug_report.write("\n\n\n")
    
    #--unfiltered list of attributes to be removed
    if len(needs_to_be_removed)>0:
        bug_report.write("-------------------------------------------------------\n")
        bug_report.write("""Complete list of module attributes that should be removed 
from IronPython:
""")
        for x in needs_to_be_removed:
            bug_report.write("    " + x)
    
    bug_report.close()
    return bug_report_name
    


#--MAIN------------------------------------------------------------------------

try:
    nt.mkdir(nt.getcwd() + "\\bug_reports")
except:
    pass

for mod_name in MODULES:
    #--First figure out what's missing and what's extra in a module
    #  and write this to disk.
    ipy_should_impl_filename = "bug_reports\\mod_to_impl_%s.log" % mod_name
    ipy_should_not_impl_filename = "bug_reports\\mod_rm_impl_%s.log" % mod_name
    IPY_SHOULD_IMPL = open(ipy_should_impl_filename, "w")
    IPY_SHOULD_NOT_IMPL = open(ipy_should_not_impl_filename, "w")

    get_cpython_results(mod_name)
    
    IPY_SHOULD_IMPL.close()
    IPY_SHOULD_NOT_IMPL.close()
    
    #--Next generate a human-readable bug report suitable for
    #  CodePlex.
    
    #filtered attributes which need to be added
    with open(ipy_should_impl_filename, "r") as to_impl_file:
        needs_to_be_implemented = to_impl_file.readlines()
    
    #filtered attributes to remove
    with open(ipy_should_not_impl_filename, "r") as to_rm_file:
        needs_to_be_removed = to_rm_file.readlines()
    
    gen_bug_report(mod_name, needs_to_be_implemented, needs_to_be_removed)
    
    #--Cleanup
    for x in [ipy_should_impl_filename, ipy_should_not_impl_filename]:
        File.Delete(x)
    
    #--TODO: we could automatically update bug descriptions on CodePlex at this
    #  point...
    