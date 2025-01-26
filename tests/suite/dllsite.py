# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Test autoimport of assemblies found in DLLs directory
##

from sys import exit, argv
from iptest.assert_util import *

if argv.count("OKtoRun")==0 or is_cli==False:
    print("Bailing")
    exit(0)

def test_sanity():
    '''
    Sanity checks. All of these are fairly normal imports
    and should work.
    '''
    #first check that our native modules are still present and accounted for...
    import binascii
    import _collections
    import copyreg
    import pickle
    import io
    import datetime
    import errno
    import exceptions
    import gc
    import imp
    import itertools
    import marshal
    import math
    import nt
    import operator
    import re
    import socket
    import _struct
    import _thread
    import time
    
    #next run through our first set of "OK" modules
    for i in range(50):
        mod_name = "foo" + str(i)
        exec("import " + mod_name)
        exec("AreEqual(" + mod_name + ".Foo().BAR," + str(i) + ")")

def test_special_cases():
    '''
    Extraordinary cases that should still be supported
    by IP.
    '''
    #ensure that assemblies reopening the same module and overriding
    #a class work. by "work", this means that the last (alphabetically) DLL
    #should be the one that's imported
    import foo
    AreEqual(foo.Foo().BAR, 4)
    
    #test some unusual DLL filenames
    for partial_ns in ["ZERO", "ONE", "a", "UNDERSCORE", "WHITESPACE", "BIGFILENAME"]:
        mod_name = "foo" + partial_ns
        exec("import " + mod_name)
        exec("AreEqual(" + mod_name + ".Foo().BAR, 1)")
    
    
def test_bad_stuff():
    '''
    Cases where IP should not load an assembly for one
    reason or another.
    '''
    
    #ensure that users cannot override IP native modules
    import sys
    Assert(sys.winver != "HIJACKED")
    import re
    Assert(re.compile != "HIJACKED")

    #ensure corrupted DLLs cannot be loaded
    try:
        import fooCORRUPT
        raise Exception("Corrupted DLL was loaded")
    except ImportError as e:
        pass
    
    #nothing to do for unmanaged DLLs...if the interpreter has made it
    #this far, all is well:)
    
    
    #ensure *.exe's cannot take precedence over *.dlls
    import fooDLLEXE
    AreEqual(fooDLLEXE.Foo().BAR, 1)

    #ensure *.exe's are not autoloaded at all!
    try:
        import fooEXEONLY
        raise Exception("*.exe's should not be autoloaded!")
    except ImportError as e:
        pass
    except SystemError as e:
        print("Work Item #189503")
    
    #ensure *.txt's are not autoloaded at all
    try:
        import fooTXTDLL
        raise Exception("*.txt's should not be autoloaded!")
    except ImportError as e:
        pass
    
run_test(__name__)
