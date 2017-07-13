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

import os
import sys
import unittest

from iptest import is_cli, stdout_trapper, path_modifier, run_test

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

@unittest.skipUnless(is_cli, 'IronPython specific test case')
class ClrUseTest(unittest.TestCase):
    def simpleTester(self, a, b, c):
        global SIMPLE_TEST_COUNT

        import clr
        
        test_name = "clrusetest{}.py".format(SIMPLE_TEST_COUNT)
        SIMPLE_TEST_COUNT += 1
        expected_stdout = '''OK...'''
        
        #create the file
        test_text = SIMPLE_TEST % (str(a), str(c))
        with open(test_name, "w") as f:
            f.writelines(test_text)
        
        try:
            with path_modifier('.') as p:
                with stdout_trapper() as output:
                    new_module = clr.Use(test_name.split(".py")[0])
            
            #run a few easy checks
            self.assertEqual(len(output.messages), 3)
            self.assertEqual(output.messages[0], expected_stdout)
            self.assertEqual(output.messages[2], "b= 42")
            self.assertEqual(new_module.A, a)
            self.assertEqual(new_module.aFunc(None), c)
            self.assertTrue(isinstance(new_module.K, new_module.Klass))
            self.assertEqual(new_module.K.Member, 72)
            new_module.K.Member = "foo"
            self.assertEqual(new_module.K.Member, "foo")
            new_module.K.NewMember = 33
            self.assertEqual(new_module.K.NewMember, 33)
            new_module.K = None
            self.assertEqual(new_module.K, None)
            
            #negative checks
            self.assertRaises(TypeError, new_module.aFunc)
            self.assertRaises(TypeError, new_module.aFunc, 1, 2)
            self.assertRaises(TypeError, new_module.aFunc, 1, 2, 3)
            self.assertTrue(not hasattr(new_module, "a"))
            self.assertTrue(not hasattr(new_module, "simpleTester"))
            try:
                aFunc(7)
                self.fail("Should never get this far")
            except:
                pass
            
            #hard test
            real_module = __import__(test_name.split(".py")[0])
            #for key in dir(real_module): self.assertEqual(real_module.__dict__[key], new_module.__dict__[key])
        finally:
            pass
            # try:
            #     os.remove(test_name)
            # except: pass

    def test_sanity(self):
        self.simpleTester(1, 2, 3)
        #if it worked once, it should work again...
        self.simpleTester(1, 2, 3)
        #None
        self.simpleTester(None, None, None)


    @unittest.skip('skip this test for now until Global namespace is implemented in DLR')
    def test_modified_module(self):
        import clr

        test_name = "test_modified_module.py"

        try:
            #create the file
            with open(test_name, "w") as f:
                f.writelines('''A=1; print "First Run"''')
            
            with path_modifier('.') as p:
                with stdout_trapper() as output:
                    new_module = clr.Use(test_name.split(".py")[0])

            #run checks
            self.assertEqual(output.messages, ["First Run"])
            self.assertEqual(new_module.A, 1)

            #--Do everything again with different values...
            #recreate the file
            with open(test_name, "w") as f:
                f.writelines('''A=2; print "Second Run"''')
            
            with path_modifier('.') as p:
                with stdout_trapper() as output:
                    new_module = clr.Use(test_name.split(".py")[0])

            #run checks
            self.assertEqual(output.messages, [])
            self.assertEqual(new_module.A, 1)
        finally:
            #cleanup
            try:
                os.remove(test_name)
            except: pass
        
run_test(__name__)
