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

##
## Test __future__ related areas where __future__ is enabled in the module scope;
##

from __future__ import division

import os
import unittest

from iptest import IronPythonTestCase, is_cli, path_modifier, run_test

assert_code = '''
def CustomAssert(c):
    if not c: raise AssertionError("Assertion Failed")
    
'''

code1  = assert_code + '''
exec "CustomAssert(1/2 == 0.5)"
exec "from __future__ import division; CustomAssert(1/2 == 0.5)"
CustomAssert(1/2 == 0.5)
CustomAssert(eval('1/2') == 0.5)
'''

code2 = "from __future__ import division\n" + code1

# this is true if the code is imported as module
code0 = assert_code + '''
exec "CustomAssert(1/2 == 0)"
exec "from __future__ import division; CustomAssert(1/2 == 0.5)"
CustomAssert(1/2 == 0)
CustomAssert(eval('1/2') == 0)
'''

def f1(tempfile): execfile(tempfile)
def f2(code, tempfile): exec(compile(code, tempfile, "exec"))
def f3(code): exec(code)

class C:
    def __init__(self, selph):
        self.selph = selph
    def check(self):
        exec "self.selph.assertEqual(1 / 2, 0.5)"
        exec "from __future__ import division; self.selph.assertEqual(1/2, 0.5)"
        self.selph.assertEqual(1 / 2, 0.5)
        self.selph.assertEqual(eval("1/2"), 0.5)

# the following are always true in current context
def always_true(self):
    exec "self.assertEqual(1 / 2, 0.5)"
    exec "from __future__ import division; self.assertEqual(1 / 2, 0.5)"
    self.assertEqual(1/2, 0.5)
    self.assertEqual(eval("1/2"), 0.5)

class FutureTest(IronPythonTestCase):
    def setUp(self):
        super(FutureTest, self).setUp()
        self.tempfile = os.path.join(self.temporary_dir, "temp_future.py")

    def test_always_true(self):
        always_true(self)

        try:
            import sys
            with path_modifier(self.temporary_dir):
                for code in (code1, code2) :
                    self.write_to_file(self.tempfile, code)
                    
                    f1(self.tempfile)
                    always_true(self)
                    f2(code, self.tempfile)
                    always_true(self)
                    f3(code)
                    always_true(self)

                ## test import from file
                for code in (code0, code2):
                    self.write_to_file(self.tempfile, code)
                    
                    import temp_future
                    always_true(self)
                    reloaded_temp_future = reload(temp_future)
                    always_true(self)
        finally:
            self.delete_files(self.tempfile)
    
    def test_class_context(self):
        """carry context over class def"""
        C(self).check()

    def test_future_division_inherited(self):
        """Test future division operators for all numeric types and types inherited from them"""

        class myint(int): pass
        class mylong(long): pass
        class myfloat(float): pass
        class mycomplex(complex): pass

        l = [2, 10L, (1+2j), 3.4, myint(7), mylong(5), myfloat(2.32), mycomplex(3, 2), True]

        if is_cli:
            import System
            l.append(System.Int64.Parse("9"))

        for a in l:
            for b in l:
                try:
                    r = a / b
                except:
                    self.fail("True division failed: %r(%s) / %r(%s)" % (a, type(a), b, type(b)))

        # check division by zero exceptions for true
        threes = [ 3, 3L, 3.0 ]
        zeroes = [ 0, 0L, 0.0 ]

        if is_cli:
            import System
            threes.append(System.Int64.Parse("3"))
            zeroes.append(System.Int64.Parse("0"))

        if is_cli:    
            #Verify true division of a overloaded / operator in a C# class
            self.add_clr_assemblies("operators")
            from Merlin.Testing.Call import AllOpsClass
            x = AllOpsClass(5)
            y = AllOpsClass(4)
            z = x/y
            self.assertEqual(z.Value , 1) #normal division happens since __truediv__ is not found __div__ is called.

        for i in threes:
            for j in zeroes:
                try:
                    r = i / j
                except ZeroDivisionError:
                    pass
                else:
                    self.fail("Didn't get ZeroDivisionError %s, %s, %s, %s" % (type(i).__name__, type(j).__name__, str(i), str(j)))

    def test_builtin_compile(self):
        """built-in compile method when passing flags"""
        self.assertEqual( eval(compile("2/3", "<string>", "eval", 0, 1), {}), 0)
        self.assertEqual( eval(compile("2/3", "<string>", "eval", 0), {}), 2/3)

run_test(__name__)

