# -*- coding: iso-8859-1 -*-
# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

'''
This module consists of test cases where IronPython was broken under standard CPython
Python-based modules.
'''

import os
import sys
import locale

from iptest import IronPythonTestCase, is_cli, is_cpython, is_posix, run_test, skipUnlessIronPython

class StdModulesTest(IronPythonTestCase):

    def test_cp8678(self):
        x = iter(range(4))
        expected = ([0, 1], [2, 3])
        actual = []

        for i, j in zip(x, x):
            actual.append([i, j])

        self.assertEqual(len(expected), len(actual))
        for i in range(len(expected)):
            self.assertEqual(expected[i], actual[i])

    def test_cp10825(self):
        import urllib.request
        from time import sleep

        #Give it five chances to connect
        temp_url = None
        err = None

        for i in range(5):
            try:
                temp_url = urllib.request.urlopen("http://www.microsoft.com")
                break
            except Exception as e:
                err = e
                print(".", end="")
                sleep(5)
                continue
        if temp_url is None: raise err

        try:
            self.assertTrue(temp_url.url.startswith("http://www.microsoft.com"))
        finally:
            temp_url.close()

    def test_cp5566(self):
        import base64
        self.assertEqual(base64.decodebytes(b'w/=='), b'\xc3')

        test_str = b"abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789~!@#$%^&*()_+-=[]\{}|;':,.//<>?\""
        test_str+= b"/a/b/c/d/e/f/g/h/i/j/k/l/m/n/o/p/q/r/s/t/u/v/w/x/y/z/A/B/C/D/E/F/G/H/I/J/K/L/M/N/O/P/Q/R/S/T/U/V/W/X/Y/Z/0/1/2/3/4/5/6/7/8/9/~/!/@/#/$/%/^/&/*/(/)/_/+/-/=/[/]/\/{/}/|/;/'/:/,/.///</>/?\""
        self.assertEqual(base64.decodebytes(test_str),
                b'i\xb7\x1dy\xf8!\x8a9%\x9az)\xaa\xbb-\xba\xfc1\xcb0\x01\x081\x05\x18r\t(\xb3\r8\xf4\x11I5\x15Yv\x19\xd3]\xb7\xe3\x9e\xbb\xf3\xdf')

    def test_cp35507(self):
        import base64
        self.assertEqual(base64.b64decode('MTIzNP8='), b'1234\xff')
        self.assertEqual(base64.b64decode(b'MTIzNP8='), b'1234\xff')
        with self.assertRaises(TypeError):
            base64.b64encode('1234\xff')
        self.assertEqual(base64.b64encode(b'1234\xff'), b'MTIzNP8=')

        import pickle
        one = pickle.dumps(1)
        with self.assertRaises(TypeError):
            pickle.loads("I1\n.")
        self.assertEqual(1, pickle.loads(b"I1\n."))
        self.assertEqual(1, pickle.loads(one))
        self.assertEqual(1, pickle.loads(bytes(one)))

    @skipUnlessIronPython()
    def test_cp13618(self):
        import os
        from System.IO.Path import PathSeparator
        self.assertEqual(os.pathsep, PathSeparator)

    def test_cp12907(self):
        #from codeop import compile_command, PyCF_DONT_IMPLY_DEDENT
        from os import unlink

        f_name = os.path.join(self.temporary_dir, "fake_stdout.txt")
        test_list = [
                        ("print(1)",
                            "single", ["1\n"]),
                        ("print(1)",
                            "exec", ["1\n"]),
                        ("1",
                            "single", ["1\n"]),
                        ("1",
                            "exec", []),
                        ("def f(n):\n    return n*n\nprint(f(3))",
                            "exec", ["9\n"]),
                        ("if 1:\n    print(1)\n",
                            "single", ["1\n"]),
                        ("if 1:\n    print(1)\n",
                            "exec", ["1\n"]),
                    ]

        if is_cpython: # https://github.com/IronLanguages/ironpython3/issues/1047
            test_list.append(("if 1:\n    print(1)", "exec", ["1\n"]))

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

                self.assertEqual(lines, expected)

            finally:
                sys.stdout = orig_stdout
        os.unlink(f_name)

        #negative cases
        bad_test_list = [
                        ("def f(n):\n    return n*n\n\nf(3)\n", "single"),
                        ("def f(n):\n    return n*n\n\nf(3)",   "single"),
                        ("def f(n):\n    return n*n\n\nf(3)\n", "single"),
                        ("if 1:\n    print(1)",                  "single"),
                    ]
        if not is_cpython: # https://github.com/IronLanguages/ironpython3/issues/1047
            bad_test_list.append(("if 1:\n    print(1)",                  "exec"))

        for test_case, kind in bad_test_list:
            print(test_case, kind)
            self.assertRaises(SyntaxError, compile, test_case, "", kind, 0x200, 1)

    def test_cp12009(self):
        import os
        import shutil

        dir1 = os.path.join(self.temporary_dir, "temp_test_stdmodules_dir")
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
        import subprocess
        ec = subprocess.call([sys.executable, "-tt", "-c", "import os"])
        self.assertEqual(ec, 0)

    @skipUnlessIronPython()
    def test_cp13401(self):
        import System
        import copy

        #A few special cases
        StringSplitOptions_None = getattr(System.StringSplitOptions, "None")
        self.assertEqual(System.Char.MinValue, copy.copy(System.Char.MinValue))
        self.assertTrue(System.Char.MinValue != copy.copy(System.Char.MaxValue))
        self.assertEqual(StringSplitOptions_None, copy.copy(StringSplitOptions_None))
        self.assertEqual(System.StringSplitOptions.RemoveEmptyEntries, copy.copy(System.StringSplitOptions.RemoveEmptyEntries))
        self.assertTrue(StringSplitOptions_None != copy.copy(System.StringSplitOptions.RemoveEmptyEntries))

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

    def test_cp17819(self):
        import xml.sax
        self.assertEqual(xml.sax._false, 0)

    def test_cp20162(self):
        import collections
        self.assertRaisesMessage(TypeError, "__init__() takes at most 2 arguments (3 given)" if is_cli else "deque() takes at most 2 arguments (3 given)",
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
        save_dir = os.getcwd()
        os.chdir("..") # other tests run in parallel are creating/deleting files in "."
        self.assertEqual(os.listdir("."),
                os.listdir(os.getcwd()))
        self.assertRaises(WindowsError, os.listdir, "")
        os.chdir(save_dir)

    def test_gh1144(self):
        from collections import deque
        a = deque(maxlen=0)
        a.append("a")
        self.assertEqual(len(a), 0)

class StdModulesLocalizedTest(IronPythonTestCase):

    def setUp(self):
        super().setUp()
        self.saved_lc = [(getattr(locale, lc), locale.getlocale(getattr(locale, lc)))
                            for lc in dir(locale)
                            if lc.startswith('LC_') and lc != 'LC_ALL']

    def tearDown(self):
        for lc, setting in self.saved_lc:
            locale.setlocale(lc, setting)
        super().tearDown()

    @skipUnlessIronPython()
    def test_get_set_locale(self):
        locale.setlocale(locale.LC_ALL, 'en-US')
        loc = locale.getlocale(locale.LC_ALL)
        self.assertEqual(loc, ('en_US','ISO8859-1'))

        locale.setlocale(locale.LC_ALL, 'C')
        loc = locale.getlocale(locale.LC_ALL)
        self.assertEqual(loc, (None,None))

        self.assertTrue(locale.setlocale(locale.LC_ALL, '') is not None)
        if not is_posix: # TODO: figure this out
            self.assertTrue(locale.getlocale() is not None)

    def test_cp34188(self):
        import functools
        locale.setlocale(locale.LC_COLLATE,"de_CH")
        self.assertTrue(sorted([u'a', u'z', u'�'], key=functools.cmp_to_key(locale.strcoll)) == sorted([u'a', u'z', u'�'], key=locale.strxfrm))

    def test_time_strptime_am_pm(self):
        import time
        locale.setlocale(locale.LC_TIME, 'C')
        self.assertEqual(time.strptime('12 AM', "%I %p").tm_hour, 0)
        self.assertEqual(time.strptime('12 PM', "%I %p").tm_hour, 12)

        self.assertEqual(time.strptime('12 am', "%I %p").tm_hour, 0)
        self.assertEqual(time.strptime('12 pm', "%I %p").tm_hour, 12)

        self.assertEqual(time.strptime('12 Am', "%I %p").tm_hour, 0)
        self.assertEqual(time.strptime('12 Pm', "%I %p").tm_hour, 12)

        locale.setlocale(locale.LC_TIME, 'en_US')
        self.assertEqual(time.strptime('12 AM', "%I %p").tm_hour, 0)
        self.assertEqual(time.strptime('12 PM', "%I %p").tm_hour, 12)

        self.assertEqual(time.strptime('12 am', "%I %p").tm_hour, 0)
        self.assertEqual(time.strptime('12 pm', "%I %p").tm_hour, 12)

        self.assertEqual(time.strptime('12 Am', "%I %p").tm_hour, 0)
        self.assertEqual(time.strptime('12 Pm', "%I %p").tm_hour, 12)

        locale.setlocale(locale.LC_TIME, "hu_HU")
        self.assertEqual(time.strptime('12 de.', "%I %p").tm_hour, 0)
        self.assertEqual(time.strptime('12 du.', "%I %p").tm_hour, 12)

        self.assertEqual(time.strptime('12 DE.', "%I %p").tm_hour, 0)
        self.assertEqual(time.strptime('12 DU.', "%I %p").tm_hour, 12)

    def test_time_strptime(self):
        import time
        locale.setlocale(locale.LC_TIME, 'C')
        t = time.strptime('HOUR: 12, minute: 10', "Hour: %H, Minute: %M")
        self.assertEqual(t.tm_hour, 12)
        self.assertEqual(t.tm_min, 10)

        locale.setlocale(locale.LC_TIME, 'en_CA')
        t = time.strptime('HOUR: 12, minute: 10', "Hour: %H, Minute: %M")
        self.assertEqual(t.tm_hour, 12)
        self.assertEqual(t.tm_min, 10)

run_test(__name__)
