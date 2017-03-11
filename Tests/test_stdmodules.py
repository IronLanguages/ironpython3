# -*- coding: iso-8859-1 -*-
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
This module consists of test cases where IronPython was broken under standard CPython
Python-based modules.
'''

import sys
from iptest.assert_util import *
skiptest("silverlight")

if not is_stdlib():
    print("Need access to CPython's libraries to run this test")
    sys.exit(0)

##GLOBALS######################################################################


##TEST CASES###################################################################
def test_cp8678():
    
    x = iter(list(range(4)))
    expected = ([0, 1], [2, 3])
    actual = []
    
    for i, j in zip(x, x):
        actual.append([i, j])

    AreEqual(len(expected), len(actual))
    for i in range(len(expected)):
        AreEqual(expected[i], actual[i])

@skip("multiple_execute", "cli")
@retry_on_failure
def test_cp10825():
    import urllib.request, urllib.parse, urllib.error
    from time import sleep
    
    #Give it five chances to connect
    temp_url = None
    
    for i in range(5):
        try:
            temp_url = urllib.request.urlopen("http://www.microsoft.com")
            break
        except Exception as e:
            print(".", end=' ')
            sleep(5)
            continue
    if temp_url==None: raise e
    
    try:
        Assert(temp_url.url.startswith("http://www.microsoft.com/"))
    finally:
        temp_url.close()

def test_cp5566():
    import base64
    AreEqual(base64.decodestring('w/=='), '\xc3')
    test_str = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789~!@#$%^&*()_+-=[]\{}|;':,.//<>?\""
    test_str+= "/a/b/c/d/e/f/g/h/i/j/k/l/m/n/o/p/q/r/s/t/u/v/w/x/y/z/A/B/C/D/E/F/G/H/I/J/K/L/M/N/O/P/Q/R/S/T/U/V/W/X/Y/Z/0/1/2/3/4/5/6/7/8/9/~/!/@/#/$/%/^/&/*/(/)/_/+/-/=/[/]/\/{/}/|/;/'/:/,/.///</>/?\""
    
    for str_function in [str, str]:
        AreEqual(base64.decodestring(str_function(test_str)),
                'i\xb7\x1dy\xf8!\x8a9%\x9az)\xaa\xbb-\xba\xfc1\xcb0\x01\x081\x05\x18r\t(\xb3\r8\xf4\x11I5\x15Yv\x19\xd3]\xb7\xe3\x9e\xbb\xf3\xdf')

def test_cp35507():
    import base64
    AreEqual(base64.b64decode('MTIzNP8='), '1234\xff')
    AreEqual(base64.b64decode(b'MTIzNP8='), '1234\xff')
    AreEqual(base64.b64encode('1234\xff'), 'MTIzNP8=')
    AreEqual(base64.b64encode(b'1234\xff'), 'MTIzNP8=')

    import pickle
    one = pickle.dumps(1)
    AreEqual(1, pickle.loads("I1\n."))
    AreEqual(1, pickle.loads(b"I1\n."))
    AreEqual(1, pickle.loads(one))
    AreEqual(1, pickle.loads(bytes(one)))

@skip("win32")
def test_cp13618():
    import os
    from System.IO.Path import PathSeparator
    AreEqual(os.pathsep, PathSeparator)

def test_cp12907():
    #from codeop import compile_command, PyCF_DONT_IMPLY_DEDENT
    from os import unlink
    
    f_name = "fake_stdout.txt"
    test_list = [
                    ("print 1",
                        "single", ["1\n"]),
                    ("print 1",
                        "exec", ["1\n"]),
                    ("1",
                        "single", ["1\n"]),
                    ("1",
                        "exec", []),
                    ("def f(n):\n    return n*n\nprint f(3)",
                        "exec", ["9\n"]),
                    ("if 1:\n    print 1\n",
                        "single", ["1\n"]),
                    ("if 1:\n    print 1\n",
                        "exec", ["1\n"]),
                ]

    if is_cpython: #http://ironpython.codeplex.com/workitem/28221
        test_list.append(("if 1:\n    print 1", "exec", ["1\n"]))
                
    for test_case, kind, expected in test_list:
        
        c = compile(test_case, "", kind, 0x200, 1)
        try:
            orig_stdout = sys.stdout
            
            sys.stdout = open(f_name, "w")
            exec(c)
            sys.stdout.close()
            
            t_file = open(f_name, "r")
            lines = t_file.readlines()
            t_file.close()
            
            AreEqual(lines, expected)
            
        finally:
            sys.stdout = orig_stdout
    os.unlink(f_name)

    #negative cases
    bad_test_list = [
                    ("def f(n):\n    return n*n\n\nf(3)\n", "single"),
                    ("def f(n):\n    return n*n\n\nf(3)",   "single"),
                    ("def f(n):\n    return n*n\n\nf(3)\n", "single"),
                    ("if 1:\n    print 1",                  "single"),
                ]
    if not is_cpython: #http://ironpython.codeplex.com/workitem/28221
        bad_test_list.append(("if 1:\n    print 1",                  "exec"))
                
    for test_case, kind in bad_test_list:
        print(test_case, kind)
        AssertError(SyntaxError, compile, test_case, "", kind, 0x200, 1)

def test_cp12009():
    import os
    import shutil
    
    dir1 = "temp_test_stdmodules_dir"
    dir2 = dir1 + "2"
    
    os.mkdir(dir1)
    f = open(os.path.join(dir1, "stuff.txt"), "w")
    f.close()
    
    try:
        shutil.copytree(dir1, dir2)
        Assert("stuff.txt" in os.listdir(dir2))
    finally:
        for t_dir in [dir1, dir2]:
            os.unlink(os.path.join(t_dir, "stuff.txt"))
            os.rmdir(t_dir)

def test_cp17040():
    if not is_stdlib(): 
        print("Will not run w/o the std library")
        return
        
    ec = os.system("%s -tt -c \"import os\"" %
                   (sys.executable))
    AreEqual(ec, 0)

@skip("win32")
def test_cp13401():
    import copy
    
    #A few special cases
    AreEqual(System.Char.MinValue, copy.copy(System.Char.MinValue))
    Assert(System.Char.MinValue != copy.copy(System.Char.MaxValue))
    AreEqual(System.StringSplitOptions.None, copy.copy(System.StringSplitOptions.None))
    AreEqual(System.StringSplitOptions.RemoveEmptyEntries, copy.copy(System.StringSplitOptions.RemoveEmptyEntries))
    Assert(System.StringSplitOptions.None != copy.copy(System.StringSplitOptions.RemoveEmptyEntries))
    
    #Normal cases
    test_dict = {   System.Byte : [System.Byte.MinValue, System.Byte.MinValue+1, System.Byte.MaxValue, System.Byte.MaxValue-1],
                    System.Char : [],
                    System.Boolean : [True, False],
                    System.SByte   : [System.SByte.MinValue, System.SByte.MinValue+1, System.SByte.MaxValue, System.SByte.MaxValue-1],
                    System.UInt32  : [System.UInt32.MinValue, System.UInt32.MinValue+1, System.UInt32.MaxValue, System.UInt32.MaxValue-1],
                    System.Int64   : [System.Int64.MinValue, System.Int64.MinValue+1, System.Int64.MaxValue, System.Int64.MaxValue-1],
                    System.Double  : [0.00, 3.14],
                    }
    
    for key in list(test_dict.keys()):
        temp_type = key
        Assert(hasattr(temp_type, "__reduce_ex__"), 
               "%s has no attribute '%s'" % (str(temp_type), "__reduce_ex__"))
    
        for temp_value in test_dict[key]:
            x = temp_type(temp_value)
            x_copy = copy.copy(x)
            AreEqual(x, x_copy)
            AreEqual(x, temp_value)

def test_cp7008():
    import os
    import sys
    
    Assert(os.path.isfile(sys.executable))
    Assert(not os.path.isfile('"' + sys.executable + '"'))
    
@skip('win32')
def test_get_set_locale():
    import locale
    locale.setlocale(locale.LC_ALL, 'en-US')
    loc = locale.getlocale(locale.LC_ALL)
    AreEqual(loc, ('en_US','ISO8859-1'))
    
    locale.setlocale(locale.LC_ALL, 'C')
    loc = locale.getlocale(locale.LC_ALL)
    AreEqual(loc, (None,None))

    Assert(locale.setlocale(locale.LC_ALL, '') != None)
    Assert(locale.getlocale() != None)

def test_cp17819():
    import xml.sax
    AreEqual(xml.sax._false, 0)

@runonly("win32")
def test_cp20162():
    import collections
    AssertErrorWithMessage(TypeError, "deque() takes at most 2 arguments (3 given)",
                           collections.deque, 'abc', 2, 2)

def test_cp20603():
    '''
    Just ensure this does not throw a ValueError.
    '''
    import os
    for root, files, dirs in os.walk(''):
        for f in files:
            temp = os.path.join(root, f)

def test_cp21929():
    import os
    AreEqual(os.listdir("."),
             os.listdir(os.getcwd()))
    AssertError(WindowsError, os.listdir, "")

def test_cp34188():
    import locale
    locale.setlocale(locale.LC_COLLATE,"de_CH")
    Assert(sorted(['a', 'z', 'ä'], cmp=locale.strcoll) == sorted(['a', 'z', 'ä'], key=locale.strxfrm))

def test_gh1144():
    from collections import deque
    a = deque(maxlen=0)
    a.append("a")
    AreEqual(len(a), 0)

    
##MAIN#########################################################################
run_test(__name__)
