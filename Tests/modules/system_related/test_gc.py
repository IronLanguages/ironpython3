# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import gc
import _random
import unittest

from iptest import is_cli, run_test

debug_list = [ 1, #DEBUG_STATS
               2, #DEBUG_COLLECTABLE
               4, #DEBUG_UNCOLLECTABLE
               8, #DEBUG_INSTANCES
               16,#DEBUG_OBJECTS
               32,#DEBUG_SAVEALL
               62#DEBUG_LEAK
              ]

class GcTest(unittest.TestCase):

    # CodePlex Work Item# 8202
    def test_000_get_debug(self):
        """get_debug should return 0 if set_debug has not been used"""
        self.assertEqual(gc.get_debug(), 0)

    def test_get_objects(self):
        if is_cli:
            self.assertRaises(NotImplementedError, gc.get_objects)
        else:
            gc.get_objects()

    def test_set_threshold(self):
        """get_threshold, set_threshold"""

        #the method has three arguments
        gc.set_threshold(0,-2,2)
        result = gc.get_threshold()
        self.assertEqual(result[0],0)
        self.assertEqual(result[1],-2)
        self.assertEqual(result[2],2)

        ##the method has two argument
        gc.set_threshold(0,128)
        result = gc.get_threshold()
        self.assertEqual(result[0],0)
        self.assertEqual(result[1],128)
        #CodePlex Work Item 8523
        self.assertEqual(result[2],2)


        #the method has only one argument
        gc.set_threshold(-10009)
        result= gc.get_threshold()
        self.assertEqual(result[0],-10009)
        #CodePlex Work Item 8523
        self.assertEqual(result[1],128)
        self.assertEqual(result[2],2)

        #the argument is a random int
        for i in range(1,65535,6):
            gc.set_threshold(i)
            result = gc.get_threshold()
            self.assertEqual(result[0],i)

        #a argument is a float
        #CodePlex Work Item 8522
        self.assertRaises(TypeError,gc.set_threshold,2.1)
        self.assertRaises(TypeError,gc.set_threshold,3,-1.3)

        #a argument is a string
        #CodePlex Work Item 8522
        self.assertRaises(TypeError,gc.set_threshold,"1")
        self.assertRaises(TypeError,gc.set_threshold,"str","xdv#4")
        self.assertRaises(TypeError,gc.set_threshold,2,"1")
        self.assertRaises(TypeError,gc.set_threshold,31,-123,"asdfasdf","1")

        #a argument is a object
        #CodePlex Work Item 8522
        o  = object()
        o2 = object()
        self.assertRaises(TypeError,gc.set_threshold,o)
        self.assertRaises(TypeError,gc.set_threshold,o,o2)
        self.assertRaises(TypeError,gc.set_threshold,1,-123,o)
        o  = _random.Random()
        o2 = _random.Random()
        self.assertRaises(TypeError,gc.set_threshold,o)
        self.assertRaises(TypeError,gc.set_threshold,o,o2)
        self.assertRaises(TypeError,gc.set_threshold,8,64,o)

    def test_get_referrers(self):
        if is_cli:
            self.assertRaises(NotImplementedError, gc.get_referrers,1,"hello",True)
            self.assertRaises(NotImplementedError, gc.get_referrers)
        else:
            gc.get_referrers(1,"hello",True)
            gc.get_referrers()

            class TempClass: pass
            tc = TempClass()
            self.assertEqual(gc.get_referrers(TempClass).count(tc), 1)


    def test_get_referents(self):
        if is_cli:
            self.assertRaises(NotImplementedError, gc.get_referents,1,"hello",True)
            self.assertRaises(NotImplementedError, gc.get_referents)
        else:
            gc.get_referents(1,"hello",True)
            gc.get_referents()

            class TempClass: pass
            self.assertEqual(gc.get_referents(TempClass).count('TempClass'), 1)

    def test_enable(self):
        gc.enable()
        result = gc.isenabled()
        self.assertTrue(result,"enable Method can't set gc.isenabled as true.")

    def test_disable(self):
        if is_cli:
            import warnings
            with warnings.catch_warnings(record=True) as m:
                warnings.simplefilter("always")
                gc.disable()

            self.assertEqual('IronPython has no support for disabling the GC', m[0].message.args[0])
        else:
            gc.disable()
            result = gc.isenabled()
            self.assertTrue(result == False,"enable Method can't set gc.isenabled as false.")


    def test_isenabled(self):
        gc.enable()
        result = gc.isenabled()
        self.assertTrue(result,"enable Method can't set gc.isenabled as true.")

        if not is_cli:
            gc.disable()
            result = gc.isenabled()
            self.assertTrue(result == False,"enable Method can't set gc.isenabled as false.")

    def test_collect(self):
        if is_cli:
            i = gc.collect() # returns # of bytes collected, could be anything
        else:
            for debug in debug_list:
                gc.set_debug(debug)
                gc.collect()
        gc.collect(0)

        #Negative
        self.assertRaises(ValueError, gc.collect, -1)
        self.assertRaises(ValueError, gc.collect, 2147483647)

    def test_setdebug(self):
        if is_cli:
            for debug in debug_list:
                self.assertRaises(NotImplementedError, gc.set_debug,debug)
                self.assertEqual(0,gc.get_debug())
        else:
            for debug in debug_list:
                gc.set_debug(debug)
                self.assertEqual(debug,gc.get_debug())

    def test_garbage(self):
        i = len(gc.garbage)
        self.assertEqual(0,i)

    def test_gc(self):
        self.assertTrue(not hasattr(gc, 'gc'))

    def test_debug_stats(self):
        self.assertEqual(1,gc.DEBUG_STATS)
        self.assertEqual(2,gc.DEBUG_COLLECTABLE)
        self.assertEqual(4,gc.DEBUG_UNCOLLECTABLE)
        self.assertEqual(8,gc.DEBUG_INSTANCES)
        self.assertEqual(16,gc.DEBUG_OBJECTS)
        self.assertEqual(32,gc.DEBUG_SAVEALL)
        self.assertEqual(62,gc.DEBUG_LEAK)


    def test_get_debug(self):
        state = [0,gc.DEBUG_STATS,gc.DEBUG_COLLECTABLE,gc.DEBUG_UNCOLLECTABLE,gc.DEBUG_INSTANCES,gc.DEBUG_OBJECTS,gc.DEBUG_SAVEALL,gc.DEBUG_LEAK]
        result = gc.get_debug()
        if result not in state:
            self.fail("Returned value of getdebug method is not valid value:" + str(result))

run_test(__name__)