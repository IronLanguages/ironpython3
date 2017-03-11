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
This module tests the clr.Use feature with Python modules only and does not explore
cross language scenarios (handled by other tests).

TODO:
- EVERYTHING!  This incomplete test is being checked in now to prevent regressions
'''

from iptest.assert_util import *
skiptest("win32")
    
import System
import clr
import sys


#------------------------------------------------------------------------------
#--GLOBALS

SIMPLE_TEST = '''
A = %s

def aFunc(b):
    global A
    print "A=", A
    print "b=", b
    return %s

class Klass(object):
    Member = 72
K = Klass()

if __name__=="__main__":
    print "Should never be in __main__"
    raise "in __main__"
else:
    print "OK..."
    aFunc(42)
'''

#------------------------------------------------------------------------------
#--HELPERS
SIMPLE_TEST_COUNT = 0

def simpleTester(a, b, c):
    global SIMPLE_TEST
    global SIMPLE_TEST_COUNT
    
    import os
    
    test_name = "clrusetest" + str(SIMPLE_TEST_COUNT) + ".py"
    SIMPLE_TEST_COUNT = SIMPLE_TEST_COUNT + 1
    new_stdout_name = "new_stdout.log"
    expected_stdout = '''OK...\n'''
    
    #create the file
    test_text = SIMPLE_TEST % (str(a), str(c))
    f = open(test_name, "w")
    f.writelines(test_text)
    f.close()
    
    #take control of stdout
    old_stdout = sys.stdout
    new_stdout = open(new_stdout_name, "w")
    sys.stdout = new_stdout
    
    #clr.Use
    name = test_name.split(".py")[0]
    new_module = clr.Use(name)
    
    #give stdout back
    sys.stdout = old_stdout
    new_stdout.close()
    new_stdout = open(new_stdout_name, "r")
    new_stdout_lines = new_stdout.readlines()
    new_stdout.close()
    
    #run a few easy checks
    AreEqual(len(new_stdout_lines), 3)
    AreEqual(new_stdout_lines[0], expected_stdout)
    AreEqual(new_stdout_lines[2], "b= 42\n")
    AreEqual(new_module.A, a)
    AreEqual(new_module.aFunc(None), c)
    Assert(isinstance(new_module.K, new_module.Klass))
    AreEqual(new_module.K.Member, 72)
    new_module.K.Member = "foo"
    AreEqual(new_module.K.Member, "foo")
    new_module.K.NewMember = 33
    AreEqual(new_module.K.NewMember, 33)
    new_module.K = None
    AreEqual(new_module.K, None)
    
    #negative checks
    AssertError(TypeError, new_module.aFunc)
    AssertError(TypeError, new_module.aFunc, 1, 2)
    AssertError(TypeError, new_module.aFunc, 1, 2, 3)
    Assert(not hasattr(new_module, "a"))
    Assert(not hasattr(new_module, "simpleTester"))
    try:
        aFunc(7)
        raise "Should never get this far"
    except:
        pass
    
    #hard test
    real_module = __import__(test_name.split(".py")[0])
    #for key in dir(real_module): AreEqual(real_module.__dict__[key], new_module.__dict__[key])

    #cleanup
    os.remove(test_name)
    os.remove(new_stdout_name)

#------------------------------------------------------------------------------
#--TESTS

@skip("silverlight")
def test_sanity():
    simpleTester(1, 2, 3)
    #if it worked once, it should work again...
    simpleTester(1, 2, 3)
    #None
    simpleTester(None, None, None)

@skip("silverlight")
# TODO: skip this test for now until Global namespace is implemented in DLR
def __test_modified_module():
    test_name = "test_modified_module.py"
    new_stdout_name = "new_stdout.log"
    
    #create the file
    f = open(test_name, "w")
    f.writelines('''A=1; print "First Run"''')
    f.close()
    
    #take control of stdout
    old_stdout = sys.stdout
    new_stdout = open(new_stdout_name, "w")
    sys.stdout = new_stdout
    
    #clr.Use
    new_module = clr.Use(test_name.split(".py")[0])
    
    #give stdout back
    sys.stdout = old_stdout
    new_stdout.close()
    new_stdout = open(new_stdout_name, "r")
    new_stdout_lines = new_stdout.readlines()
    new_stdout.close()
    
    #run checks
    AreEqual(new_stdout_lines, ["First Run\n"])
    AreEqual(new_module.A, 1)
    
    #--Do everything again with different values...
    #recreate the file
    f = open(test_name, "w")
    f.writelines('''A=2; print "Second Run"''')
    f.close()
    
    #take control of stdout
    old_stdout = sys.stdout
    new_stdout = open(new_stdout_name, "w")
    sys.stdout = new_stdout
    
    #clr.Use
    new_module = clr.Use(test_name.split(".py")[0])
    
    #give stdout back
    sys.stdout = old_stdout
    new_stdout.close()
    new_stdout = open(new_stdout_name, "r")
    new_stdout_lines = new_stdout.readlines()
    new_stdout.close()
    
    #run checks
    AreEqual(new_stdout_lines, [])
    AreEqual(new_module.A, 1)
    
    
    #cleanup
    os.remove(test_name)
    os.remove(new_stdout_name)
        
def test_module():
    pass
        
def test_package():
    pass

def test_pytypes():
    #subclasses...
    pass
    
def test_clrtypes():
    # Test whether the correct overload of bytes constructor is used
    class subclass_overload_test(bytes):
        pass
    inst = subclass_overload_test('a')
    
def test_pyinstances():
    pass
    
def test_clrinstances():
    pass
    
def test_recursive():
    pass
    
def test_negative():
    
    #case sensitivity
    
    #legal names - hidden members???
    
    #large number of identifiers in module
    
    #bad scope
    
    #try deleting an identifier/class/etc
    
    #overwriting identifiers
    
    #dynamically adding members to modules/classes/instances???
    
    #byref parameter modifications
    
    #callbacks...
    
    #thread safety
    
    #error stack
    
    #reload(module)...
    
    #stuff.py, stuff.js, stuff.vb
    
    #DLRPATH
    
    #long/bad/etc module names
    
    #non-existant module
    
    #good file extension, but empty or of another language
    
    pass

def test_perf():
    pass


#------------------------------------------------------------------------------
run_test(__name__)
