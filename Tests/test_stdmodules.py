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

import os
import sys
import unittest

from iptest import IronPythonTestCase, is_netstandard, is_cpython, run_test, skipUnlessIronPython

class StdModulesTest(IronPythonTestCase):

    def test_cp8678(self):
        from itertools import izip
        x = iter(range(4))
        expected = ([0, 1], [2, 3])
        actual = []
        
        for i, j in izip(x, x):
            actual.append([i, j])

        self.assertEqual(len(expected), len(actual))
        for i in xrange(len(expected)):
            self.assertEqual(expected[i], actual[i])

    def test_cp10825(self):
        import urllib
        from time import sleep
        
        #Give it five chances to connect
        temp_url = None
        
        for i in xrange(5):
            try:
                temp_url = urllib.urlopen("http://www.microsoft.com")
                break
            except Exception, e:
                print ".",
                sleep(5)
                continue
        if temp_url==None: raise e
        
        try:
            self.assertTrue(temp_url.url.startswith("http://www.microsoft.com"))
        finally:
            temp_url.close()

    def test_cp5566(self):
        import base64
        self.assertEqual(base64.decodestring('w/=='), '\xc3')
        test_str = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789~!@#$%^&*()_+-=[]\{}|;':,.//<>?\""
        test_str+= "/a/b/c/d/e/f/g/h/i/j/k/l/m/n/o/p/q/r/s/t/u/v/w/x/y/z/A/B/C/D/E/F/G/H/I/J/K/L/M/N/O/P/Q/R/S/T/U/V/W/X/Y/Z/0/1/2/3/4/5/6/7/8/9/~/!/@/#/$/%/^/&/*/(/)/_/+/-/=/[/]/\/{/}/|/;/'/:/,/.///</>/?\""
        
        for str_function in [str, unicode]:
            self.assertEqual(base64.decodestring(str_function(test_str)),
                    'i\xb7\x1dy\xf8!\x8a9%\x9az)\xaa\xbb-\xba\xfc1\xcb0\x01\x081\x05\x18r\t(\xb3\r8\xf4\x11I5\x15Yv\x19\xd3]\xb7\xe3\x9e\xbb\xf3\xdf')

    def test_cp35507(self):
        import base64
        self.assertEqual(base64.b64decode('MTIzNP8='), '1234\xff')
        self.assertEqual(base64.b64decode(b'MTIzNP8='), '1234\xff')
        self.assertEqual(base64.b64encode('1234\xff'), 'MTIzNP8=')
        self.assertEqual(base64.b64encode(b'1234\xff'), 'MTIzNP8=')

        import cPickle
        one = cPickle.dumps(1)
        self.assertEqual(1, cPickle.loads("I1\n."))
        self.assertEqual(1, cPickle.loads(b"I1\n."))
        self.assertEqual(1, cPickle.loads(one))
        self.assertEqual(1, cPickle.loads(bytes(one)))

    @skipUnlessIronPython()
    def test_cp13618(self):
        import os
        from System.IO.Path import PathSeparator
        self.assertEqual(os.pathsep, PathSeparator)

    def test_cp12907(self):
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
                exec c
                sys.stdout.close()
                
                t_file = open(f_name, "r")
                lines = t_file.readlines()
                t_file.close()
                
                self.assertEqual(lines, expected)
                
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
            print test_case, kind
            self.assertRaises(SyntaxError, compile, test_case, "", kind, 0x200, 1)

    def test_cp12009(self):
        import os
        import shutil
        
        dir1 = "temp_test_stdmodules_dir"
        dir2 = dir1 + "2"
        
        os.mkdir(dir1)
        f = open(os.path.join(dir1, "stuff.txt"), "w")
        f.close()
        
        try:
            shutil.copytree(dir1, dir2)
            self.assertTrue("stuff.txt" in os.listdir(dir2))
        finally:
            for t_dir in [dir1, dir2]:
                os.unlink(os.path.join(t_dir, "stuff.txt"))
                os.rmdir(t_dir)

    def test_cp17040(self):
        ec = os.system("%s -tt -c \"import os\"" %
                    (sys.executable))
        self.assertEqual(ec, 0)

    @unittest.skipIf(is_netstandard, "figure out why this doesn't work")
    @skipUnlessIronPython()
    def test_cp13401(self):
        import System
        import copy
        
        #A few special cases
        self.assertEqual(System.Char.MinValue, copy.copy(System.Char.MinValue))
        self.assertTrue(System.Char.MinValue != copy.copy(System.Char.MaxValue))
        self.assertEqual(System.StringSplitOptions.None, copy.copy(System.StringSplitOptions.None))
        self.assertEqual(System.StringSplitOptions.RemoveEmptyEntries, copy.copy(System.StringSplitOptions.RemoveEmptyEntries))
        self.assertTrue(System.StringSplitOptions.None != copy.copy(System.StringSplitOptions.RemoveEmptyEntries))
        
        #Normal cases
        test_dict = {   System.Byte : [System.Byte.MinValue, System.Byte.MinValue+1, System.Byte.MaxValue, System.Byte.MaxValue-1],
                        System.Char : [],
                        System.Boolean : [True, False],
                        System.SByte   : [System.SByte.MinValue, System.SByte.MinValue+1, System.SByte.MaxValue, System.SByte.MaxValue-1],
                        System.UInt32  : [System.UInt32.MinValue, System.UInt32.MinValue+1, System.UInt32.MaxValue, System.UInt32.MaxValue-1],
                        System.Int64   : [System.Int64.MinValue, System.Int64.MinValue+1, System.Int64.MaxValue, System.Int64.MaxValue-1],
                        System.Double  : [0.00, 3.14],
                        }
        
        for key in test_dict.keys():
            temp_type = key
            self.assertTrue(hasattr(temp_type, "__reduce_ex__"), 
                "%s has no attribute '%s'" % (str(temp_type), "__reduce_ex__"))
        
            for temp_value in test_dict[key]:
                x = temp_type(temp_value)
                x_copy = copy.copy(x)
                self.assertEqual(x, x_copy)
                self.assertEqual(x, temp_value)

    def test_cp7008(self):
        import os
        import sys
        
        self.assertTrue(os.path.isfile(sys.executable))
        self.assertTrue(not os.path.isfile('"' + sys.executable + '"'))
    
    @skipUnlessIronPython()
    def test_get_set_locale(self):
        import locale
        locale.setlocale(locale.LC_ALL, 'en-US')
        loc = locale.getlocale(locale.LC_ALL)
        self.assertEqual(loc, ('en_US','ISO8859-1'))
        
        locale.setlocale(locale.LC_ALL, 'C')
        loc = locale.getlocale(locale.LC_ALL)
        self.assertEqual(loc, (None,None))

        self.assertTrue(locale.setlocale(locale.LC_ALL, '') != None)
        if not (is_netstandard and is_posix): # TODO: figure this out
            self.assertTrue(locale.getlocale() != None)

    def test_cp17819(self):
        import xml.sax
        self.assertEqual(xml.sax._false, 0)

    #@unittest.skipIf(is_cli, 'CPython')
    def test_cp20162(self):
        import collections
        self.assertRaisesMessage(TypeError, "deque() takes at most 2 arguments (3 given)",
                            collections.deque, 'abc', 2, 2)

    def test_cp20603(self):
        '''
        Just ensure this does not throw a ValueError.
        '''
        import os
        for root, files, dirs in os.walk(''):
            for f in files:
                temp = os.path.join(root, f)

    def test_cp21929(self):
        import os
        self.assertEqual(os.listdir("."),
                os.listdir(os.getcwd()))
        self.assertRaises(WindowsError, os.listdir, "")

    @unittest.skipIf(is_netstandard, 'netstandard missing FEATURE_SORTKEY')
    def test_cp34188(self):
        import locale
        locale.setlocale(locale.LC_COLLATE,"de_CH")
        self.assertTrue(sorted([u'a', u'z', u'�'], cmp=locale.strcoll) == sorted([u'a', u'z', u'�'], key=locale.strxfrm))

    def test_gh1144(self):
        from collections import deque
        a = deque(maxlen=0)
        a.append("a")
        self.assertEqual(len(a), 0)

run_test(__name__)