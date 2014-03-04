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

from iptest.assert_util import *
import copy_reg
import _random
import imp
    
class testclass(object):
    pass

class myCustom2:
    pass


@skip("cli", "silverlight") #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=21907
def test_constructor_neg():
    class KOld: pass
    
    AssertError(TypeError, copy_reg.constructor, KOld)

 
def test_constructor():
    #the argument can be callable
    copy_reg.constructor(testclass)
    
    #the argument can not be callable
    AssertError(TypeError,copy_reg.constructor,0)
    AssertError(TypeError,copy_reg.constructor,"Hello")
    AssertError(TypeError,copy_reg.constructor,True)


#__newobj__
def test__newobj__():
    
    #the second argument is omitted
    result = None
    result = copy_reg.__newobj__(object)
    Assert(result != None,
           "The method __newobj__ did not return an object")
                     
    #the second argument is an int object
    result = None
    a = 1
    result = copy_reg.__newobj__(int,a)
    Assert(result != None,
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
    result = copy_reg.__newobj__(customtype,c,d,e)
    Assert(result != None,
           "The method __newobj__ did not return an object")


@skip("multiple_execute")
def test_add_extension():
    global obj
    obj = object()

    #The module is system defined module:_random
    copy_reg.add_extension(_random,obj,100)

    #The module is a custom mudole or the module argument is not a type of module
    global mod
    mod = imp.new_module('module')
    sys.modules['argu1'] = mod
    import argu1
    copy_reg.add_extension(argu1,obj,1)

    module = True
    copy_reg.add_extension(module,obj,6)

    # the value is zero or less than zero
    module = "module"
    AssertError(ValueError,copy_reg.add_extension,module,obj,0)
    AssertError(ValueError,copy_reg.add_extension,module,object(),-987654)

    # the key is already registered with code
    AssertError(ValueError,copy_reg.add_extension,argu1,object(),100)

    # the code is already in use for key
    AssertError(ValueError,copy_reg.add_extension,_random,obj,100009)

@skip("multiple_execute")
def test_remove_extension():
    #delete extension
    copy_reg.remove_extension(_random,obj,100)
    import argu1
    copy_reg.remove_extension(argu1,obj,1)
    module = True
    copy_reg.remove_extension(module,obj,6)

    #remove extension which has not been registed
    AssertError(ValueError,copy_reg.remove_extension,_random,obj,2)
    AssertError(ValueError,copy_reg.remove_extension,_random,object(),100)
    AssertError(ValueError,copy_reg.remove_extension,argu1,obj,1)

    copy_reg.add_extension(argu1,obj,1)
    AssertError(ValueError,copy_reg.remove_extension,argu1,obj,0)


#_extension_registry
def test_extension_registry():
    #test getattr of the attribute and how the value of this attribute affects other method
    copy_reg.add_extension('a','b',123)
    key = copy_reg._inverted_registry[123]
    result = copy_reg._extension_registry
    code = result[key]
    Assert(code == 123,
            "The _extension_registry attribute did not return the correct value")
            
    copy_reg.add_extension('1','2',999)
    result = copy_reg._extension_registry
    code = result[('1','2')]
    Assert(code == 999,
            "The _extension_registry attribute did not return the correct value")
    
    #general test, try to set the attribute then to get it
    myvalue = 3885
    copy_reg._extension_registry["key"] = myvalue
    result = copy_reg._extension_registry["key"]
    Assert(result == myvalue,
           "The set or the get of the attribute failed")
    
#_inverted_registry
def test_inverted_registry():
    copy_reg.add_extension('obj1','obj2',64)
    #get
    result = copy_reg._inverted_registry[64]
    Assert(result == ('obj1','obj2'),
            "The _inverted_registry attribute did not return the correct value")
    
    #set
    value = ('newmodule','newobj')
    copy_reg._inverted_registry[10001] = value
    result = copy_reg._inverted_registry[10001]
    Assert(result == value,
            "The setattr of _inverted_registry attribute failed")


#_extensionCache,clear_extension_cache
def test_extension_cache():
    #set and get the attribute
    rand = _random.Random()
    value = rand.getrandbits(8)
    copy_reg._extension_cache['cache1'] = value
    result = copy_reg._extension_cache['cache1']
    Assert(result == value,
           "The get and set of the attribute failed")
    
    value = rand.getrandbits(16)
    copy_reg._extension_cache['cache2'] = value
    result = copy_reg._extension_cache['cache2']
    Assert(result == value,
           "The get and set of the attribute failed")

    #change the value of the attribue
    value2 = rand.getrandbits(4)
    copy_reg._extension_cache['cache1'] = value2
    result = copy_reg._extension_cache['cache1']
    Assert(result == value2,
           "The get and set of the attribute failed")
    
    if not copy_reg._extension_cache.has_key('cache1') or  not copy_reg._extension_cache.has_key('cache2'):
        Fail("Set of the attribute failed")
        
    copy_reg.clear_extension_cache()
    if  copy_reg._extension_cache.has_key('cache1') or copy_reg._extension_cache.has_key('cache2'):
        Fail("The method clear_extension_cache did not work correctly ")

#_reconstructor
def test_reconstructor():
    reconstructor_copy = copy_reg._reconstructor
    try:
        obj = copy_reg._reconstructor(object, object, None)   
        Assert(type(obj) is object)

        #set,get, the value is a random int
        rand = _random.Random()
        value = rand.getrandbits(8)
        copy_reg._reconstructor = value
        result = copy_reg._reconstructor
        Assert(result == value,
               "set or get of the attribute failed!")
    
        #the value is a string
        value2 = "value2"
        copy_reg._reconstructor = value2
        result = copy_reg._reconstructor
        Assert(result == value2,
               "set or get of the attribute failed!")
    
        #the value is a custom type object
        value3 = testclass()
        copy_reg._reconstructor = value3
        result = copy_reg._reconstructor
        Assert(result == value3,
               "set or get of the attribute failed!")
    finally:               
        copy_reg._reconstructor = reconstructor_copy
   
#pickle
def test_pickle():
    def testfun():
        return testclass()
        
    # type is a custom type
    copy_reg.pickle(type(testclass), testfun)
    
    #type is a system type
    systype = type(_random.Random())
    copy_reg.pickle(systype,_random.Random.random)
    
    #function is not callable
    func = "hello"
    AssertError(TypeError,copy_reg.pickle,testclass,func)
    func = 1
    AssertError(TypeError,copy_reg.pickle,testclass,func)
    func = _random.Random()
    AssertError(TypeError,copy_reg.pickle,testclass,func)
    
#dispatch_table
def test_dispatch_table():
    result = copy_reg.dispatch_table
    #CodePlex Work Item 8522
    #AreEqual(5,len(result))
    
    temp = {
            "abc":"abc123",
            "def":"def123",
            "ghi":"ghi123"
           }
    copy_reg.dispatch_table = temp
    AreEqual(temp,copy_reg.dispatch_table)
    
    temp = {
            1:"abc123",
            2:"def123",
            3:"ghi123"
           }
    copy_reg.dispatch_table = temp
    AreEqual(temp,copy_reg.dispatch_table)
    
    temp = {
            1:123,
            8:789,
            16:45465
           }
    copy_reg.dispatch_table = temp
    AreEqual(temp,copy_reg.dispatch_table)
    
    #set dispathc_table as empty
    temp ={}
    copy_reg.dispatch_table = temp
    AreEqual(temp,copy_reg.dispatch_table)

#pickle_complex
def test_pickle_complex():
    #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=21908
    if not (is_cli or is_silverlight):
        AreEqual(copy_reg.pickle_complex(1), (complex, (1, 0)))
    
    #negative tests
    AssertError(AttributeError,copy_reg.pickle_complex,"myargu")
    obj2 = myCustom2()
    AssertError(AttributeError,copy_reg.pickle_complex,obj2)

run_test(__name__)
