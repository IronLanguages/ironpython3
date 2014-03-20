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
from iptest.assert_util import *

# the following are alway true in current context
def always_true():
    exec "AreEqual(1 / 2, 0.5)"
    exec "from __future__ import division; AreEqual(1 / 2, 0.5)"
    AreEqual(1/2, 0.5)
    AreEqual(eval("1/2"), 0.5)

if is_silverlight==False:
    tempfile = path_combine(testpath.temporary_dir, "temp_future.py")

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

if is_silverlight==False:
    def f1(): execfile(tempfile)
    def f2(): exec(compile(code, tempfile, "exec"))
else:
    def f1(): pass
    def f2(): pass
def f3(): exec(code)

always_true()
try:
    import sys
    save = sys.path[:]
    if is_silverlight==False:
        sys.path.append(testpath.temporary_dir)
    
    for code in (code1, code2) :
        if is_silverlight==False:
            write_to_file(tempfile, code)
        
        for f in (f1, f2, f3):
            f()
            always_true()


    ## test import from file
    for code in (code0, code2):
        if is_silverlight:
            break
        
        write_to_file(tempfile, code)
        
        import temp_future
        always_true()
        reloaded_temp_future = reload(temp_future)
        always_true()
    
finally:
    sys.path = save
    if is_silverlight==False:
        delete_files(tempfile)
    
## carry context over class def
class C:
    def check(self):
        exec "AreEqual(1 / 2, 0.5)"
        exec "from __future__ import division; AreEqual(1/2, 0.5)"
        AreEqual(1 / 2, 0.5)
        AreEqual(eval("1/2"), 0.5)

C().check()

# Test future division operators for all numeric types and types inherited from them

class myint(int): pass
class mylong(long): pass
class myfloat(float): pass
class mycomplex(complex): pass

l = [2, 10L, (1+2j), 3.4, myint(7), mylong(5), myfloat(2.32), mycomplex(3, 2), True]

if is_cli or is_silverlight:
    import System
    l.append(System.Int64.Parse("9"))


for a in l:
    for b in l:
        try:
            r = a / b
        except:
            Fail("True division failed: %r(%s) / %r(%s)" % (a, type(a), b, type(b)))

# check division by zero exceptions for true
threes = [ 3, 3L, 3.0 ]
zeroes = [ 0, 0L, 0.0 ]

if is_cli or is_silverlight:
    import System
    threes.append(System.Int64.Parse("3"))
    zeroes.append(System.Int64.Parse("0"))

if is_cli:    
    #Verify true division of a overloaded / operator in a C# class
    add_clr_assemblies("operators")
    from Merlin.Testing.Call import *
    x = AllOpsClass(5)
    y = AllOpsClass(4)    
    z = x/y
    AreEqual(z.Value , 1) #normal division happens since __truediv__ is not found __div__ is called.

for i in threes:
    for j in zeroes:
        try:
            r = i / j
        except ZeroDivisionError:
            pass
        else:
            Fail("Didn't get ZeroDivisionError %s, %s, %s, %s" % (type(i).__name__, type(j).__name__, str(i), str(j)))

# built-in compile method when passing flags
AreEqual( eval(compile("2/3", "<string>", "eval", 0, 1), {}), 0)
AreEqual( eval(compile("2/3", "<string>", "eval", 0), {}), 2/3)

