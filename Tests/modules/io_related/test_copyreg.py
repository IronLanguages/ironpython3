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

import copyreg
import imp
import random
import sys
import unittest

from iptest import run_test

class testclass(object):
    pass

class myCustom2:
    pass

class CopyRegTest(unittest.TestCase):

    @unittest.expectedFailure
    def test_constructor_neg(self):
        'https://github.com/IronLanguages/main/issues/443'
        class KOld: pass
        
        self.assertRaises(TypeError, copyreg.constructor, KOld)

 
    def test_constructor(self):
        #the argument can be callable
        copyreg.constructor(testclass)
        
        #the argument can not be callable
        self.assertRaises(TypeError,copyreg.constructor,0)
        self.assertRaises(TypeError,copyreg.constructor,"Hello")
        self.assertRaises(TypeError,copyreg.constructor,True)


    def test__newobj__(self):
        
        #the second argument is omitted
        result = None
        result = copyreg.__newobj__(object)
        self.assertTrue(result != None,
            "The method __newobj__ did not return an object")
                        
        #the second argument is an int object
        result = None
        a = 1
        result = copyreg.__newobj__(int,a)
        self.assertTrue(result != None,
            "The method __newobj__ did not return an object")
            
        #the method accept multiple arguments
        reseult = None
        class customtype(object):
            def __new__(cls,b,c,d):
                return object.__new__(cls)
            def __init__(self):
                pass
        c = True
        d = "argu"
        e = 3
        result = copyreg.__newobj__(customtype,c,d,e)
        self.assertTrue(result != None,
            "The method __newobj__ did not return an object")


    #TODO: @skip("multiple_execute")
    def test_add_extension(self):
        global obj
        obj = object()

        #The module is system defined module:random
        copyreg.add_extension(random,obj,100)

        #The module is a custom mudole or the module argument is not a type of module
        global mod
        mod = imp.new_module('module')
        sys.modules['argu1'] = mod
        import argu1
        copyreg.add_extension(argu1,obj,1)

        module = True
        copyreg.add_extension(module,obj,6)

        # the value is zero or less than zero
        module = "module"
        self.assertRaises(ValueError,copyreg.add_extension,module,obj,0)
        self.assertRaises(ValueError,copyreg.add_extension,module,object(),-987654)

        # the key is already registered with code
        self.assertRaises(ValueError,copyreg.add_extension,argu1,object(),100)

        # the code is already in use for key
        self.assertRaises(ValueError,copyreg.add_extension,random,obj,100009)

    #TODO: @skip("multiple_execute")
    def test_remove_extension(self):
        #delete extension
        copyreg.remove_extension(random,obj,100)
        import argu1
        copyreg.remove_extension(argu1,obj,1)
        module = True
        copyreg.remove_extension(module,obj,6)

        #remove extension which has not been registed
        self.assertRaises(ValueError,copyreg.remove_extension,random,obj,2)
        self.assertRaises(ValueError,copyreg.remove_extension,random,object(),100)
        self.assertRaises(ValueError,copyreg.remove_extension,argu1,obj,1)

        copyreg.add_extension(argu1,obj,1)
        self.assertRaises(ValueError,copyreg.remove_extension,argu1,obj,0)


    def test_extension_registry(self):
        #test getattr of the attribute and how the value of this attribute affects other method
        copyreg.add_extension('a','b',123)
        key = copyreg._inverted_registry[123]
        result = copyreg._extension_registry
        code = result[key]
        self.assertTrue(code == 123,
                "The _extension_registry attribute did not return the correct value")
                
        copyreg.add_extension('1','2',999)
        result = copyreg._extension_registry
        code = result[('1','2')]
        self.assertTrue(code == 999,
                "The _extension_registry attribute did not return the correct value")
        
        #general test, try to set the attribute then to get it
        myvalue = 3885
        copyreg._extension_registry["key"] = myvalue
        result = copyreg._extension_registry["key"]
        self.assertTrue(result == myvalue,
            "The set or the get of the attribute failed")
    
    def test_inverted_registry(self):
        copyreg.add_extension('obj1','obj2',64)
        #get
        result = copyreg._inverted_registry[64]
        self.assertTrue(result == ('obj1','obj2'),
                "The _inverted_registry attribute did not return the correct value")
        
        #set
        value = ('newmodule','newobj')
        copyreg._inverted_registry[10001] = value
        result = copyreg._inverted_registry[10001]
        self.assertTrue(result == value,
                "The setattr of _inverted_registry attribute failed")


    def test_extension_cache(self):
        #set and get the attribute
        rand = random.Random()
        value = rand.getrandbits(8)
        copyreg._extension_cache['cache1'] = value
        result = copyreg._extension_cache['cache1']
        self.assertTrue(result == value,
            "The get and set of the attribute failed")
        
        value = rand.getrandbits(16)
        copyreg._extension_cache['cache2'] = value
        result = copyreg._extension_cache['cache2']
        self.assertTrue(result == value,
            "The get and set of the attribute failed")

        #change the value of the attribue
        value2 = rand.getrandbits(4)
        copyreg._extension_cache['cache1'] = value2
        result = copyreg._extension_cache['cache1']
        self.assertTrue(result == value2,
            "The get and set of the attribute failed")
        
        if not copyreg._extension_cache.has_key('cache1') or  not copyreg._extension_cache.has_key('cache2'):
            Fail("Set of the attribute failed")
            
        copyreg.clear_extension_cache()
        if  copyreg._extension_cache.has_key('cache1') or copyreg._extension_cache.has_key('cache2'):
            Fail("The method clear_extension_cache did not work correctly ")

    def test_reconstructor(self):
        reconstructor_copy = copyreg._reconstructor
        try:
            obj = copyreg._reconstructor(object, object, None)   
            self.assertTrue(type(obj) is object)

            #set,get, the value is a random int
            rand = random.Random()
            value = rand.getrandbits(8)
            copyreg._reconstructor = value
            result = copyreg._reconstructor
            self.assertTrue(result == value,
                "set or get of the attribute failed!")
        
            #the value is a string
            value2 = "value2"
            copyreg._reconstructor = value2
            result = copyreg._reconstructor
            self.assertTrue(result == value2,
                "set or get of the attribute failed!")
        
            #the value is a custom type object
            value3 = testclass()
            copyreg._reconstructor = value3
            result = copyreg._reconstructor
            self.assertTrue(result == value3,
                "set or get of the attribute failed!")
        finally:               
            copyreg._reconstructor = reconstructor_copy
   

    def test_pickle(self):
        def testfun():
            return testclass()
            
        # type is a custom type
        copyreg.pickle(type(testclass), testfun)
        
        #type is a system type
        systype = type(random.Random())
        copyreg.pickle(systype,random.Random.random)
        
        #function is not callable
        func = "hello"
        self.assertRaises(TypeError,copyreg.pickle,testclass,func)
        func = 1
        self.assertRaises(TypeError,copyreg.pickle,testclass,func)
        func = random.Random()
        self.assertRaises(TypeError,copyreg.pickle,testclass,func)
    
    def test_dispatch_table(self):
        result = copyreg.dispatch_table
        #CodePlex Work Item 8522
        #self.assertEqual(5,len(result))
        
        temp = {
                "abc":"abc123",
                "def":"def123",
                "ghi":"ghi123"
            }
        copyreg.dispatch_table = temp
        self.assertEqual(temp,copyreg.dispatch_table)
        
        temp = {
                1:"abc123",
                2:"def123",
                3:"ghi123"
            }
        copyreg.dispatch_table = temp
        self.assertEqual(temp,copyreg.dispatch_table)
        
        temp = {
                1:123,
                8:789,
                16:45465
            }
        copyreg.dispatch_table = temp
        self.assertEqual(temp,copyreg.dispatch_table)
        
        #set dispathc_table as empty
        temp ={}
        copyreg.dispatch_table = temp
        self.assertEqual(temp,copyreg.dispatch_table)

    def test_pickle_complex(self):
        #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=21908
        #if not is_cli:
        self.assertEqual(copyreg.pickle_complex(1), (complex, (1, 0)))
        
        #negative tests
        self.assertRaises(AttributeError,copyreg.pickle_complex,"myargu")
        obj2 = myCustom2()
        self.assertRaises(AttributeError,copyreg.pickle_complex,obj2)

run_test(__name__)
