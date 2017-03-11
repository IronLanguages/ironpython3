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
import copyreg
import _random
import imp
    
class testclass(object):
    pass

class myCustom2:
    pass


@skip("cli", "silverlight") #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=21907
def test_constructor_neg():
    class KOld: pass
    
    AssertError(TypeError, copyreg.constructor, KOld)

 
def test_constructor():
    #the argument can be callable
    copyreg.constructor(testclass)
    
    #the argument can not be callable
    AssertError(TypeError,copyreg.constructor,0)
    AssertError(TypeError,copyreg.constructor,"Hello")
    AssertError(TypeError,copyreg.constructor,True)


#__newobj__
def test__newobj__():
    
    #the second argument is omitted
    result = None
    result = copyreg.__newobj__(object)
    Assert(result != None,
           "The method __newobj__ did not return an object")
                     
    #the second argument is an int object
    result = None
    a = 1
    result = copyreg.__newobj__(int,a)
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
    result = copyreg.__newobj__(customtype,c,d,e)
    Assert(result != None,
           "The method __newobj__ did not return an object")


@skip("multiple_execute")
def test_add_extension():
    global obj
    obj = object()

    #The module is system defined module:_random
    copyreg.add_extension(_random,obj,100)

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
    AssertError(ValueError,copyreg.add_extension,module,obj,0)
    AssertError(ValueError,copyreg.add_extension,module,object(),-987654)

    # the key is already registered with code
    AssertError(ValueError,copyreg.add_extension,argu1,object(),100)

    # the code is already in use for key
    AssertError(ValueError,copyreg.add_extension,_random,obj,100009)

@skip("multiple_execute")
def test_remove_extension():
    #delete extension
    copyreg.remove_extension(_random,obj,100)
    import argu1
    copyreg.remove_extension(argu1,obj,1)
    module = True
    copyreg.remove_extension(module,obj,6)

    #remove extension which has not been registed
    AssertError(ValueError,copyreg.remove_extension,_random,obj,2)
    AssertError(ValueError,copyreg.remove_extension,_random,object(),100)
    AssertError(ValueError,copyreg.remove_extension,argu1,obj,1)

    copyreg.add_extension(argu1,obj,1)
    AssertError(ValueError,copyreg.remove_extension,argu1,obj,0)


#_extension_registry
def test_extension_registry():
    #test getattr of the attribute and how the value of this attribute affects other method
    copyreg.add_extension('a','b',123)
    key = copyreg._inverted_registry[123]
    result = copyreg._extension_registry
    code = result[key]
    Assert(code == 123,
            "The _extension_registry attribute did not return the correct value")
            
    copyreg.add_extension('1','2',999)
    result = copyreg._extension_registry
    code = result[('1','2')]
    Assert(code == 999,
            "The _extension_registry attribute did not return the correct value")
    
    #general test, try to set the attribute then to get it
    myvalue = 3885
    copyreg._extension_registry["key"] = myvalue
    result = copyreg._extension_registry["key"]
    Assert(result == myvalue,
           "The set or the get of the attribute failed")
    
#_inverted_registry
def test_inverted_registry():
    copyreg.add_extension('obj1','obj2',64)
    #get
    result = copyreg._inverted_registry[64]
    Assert(result == ('obj1','obj2'),
            "The _inverted_registry attribute did not return the correct value")
    
    #set
    value = ('newmodule','newobj')
    copyreg._inverted_registry[10001] = value
    result = copyreg._inverted_registry[10001]
    Assert(result == value,
            "The setattr of _inverted_registry attribute failed")


#_extensionCache,clear_extension_cache
def test_extension_cache():
    #set and get the attribute
    rand = _random.Random()
    value = rand.getrandbits(8)
    copyreg._extension_cache['cache1'] = value
    result = copyreg._extension_cache['cache1']
    Assert(result == value,
           "The get and set of the attribute failed")
    
    value = rand.getrandbits(16)
    copyreg._extension_cache['cache2'] = value
    result = copyreg._extension_cache['cache2']
    Assert(result == value,
           "The get and set of the attribute failed")

    #change the value of the attribue
    value2 = rand.getrandbits(4)
    copyreg._extension_cache['cache1'] = value2
    result = copyreg._extension_cache['cache1']
    Assert(result == value2,
           "The get and set of the attribute failed")
    
    if 'cache1' not in copyreg._extension_cache or  'cache2' not in copyreg._extension_cache:
        Fail("Set of the attribute failed")
        
    copyreg.clear_extension_cache()
    if  'cache1' in copyreg._extension_cache or 'cache2' in copyreg._extension_cache:
        Fail("The method clear_extension_cache did not work correctly ")

#_reconstructor
def test_reconstructor():
    reconstructor_copy = copyreg._reconstructor
    try:
        obj = copyreg._reconstructor(object, object, None)   
        Assert(type(obj) is object)

        #set,get, the value is a random int
        rand = _random.Random()
        value = rand.getrandbits(8)
        copyreg._reconstructor = value
        result = copyreg._reconstructor
        Assert(result == value,
               "set or get of the attribute failed!")
    
        #the value is a string
        value2 = "value2"
        copyreg._reconstructor = value2
        result = copyreg._reconstructor
        Assert(result == value2,
               "set or get of the attribute failed!")
    
        #the value is a custom type object
        value3 = testclass()
        copyreg._reconstructor = value3
        result = copyreg._reconstructor
        Assert(result == value3,
               "set or get of the attribute failed!")
    finally:               
        copyreg._reconstructor = reconstructor_copy
   
#pickle
def test_pickle():
    def testfun():
        return testclass()
        
    # type is a custom type
    copyreg.pickle(type(testclass), testfun)
    
    #type is a system type
    systype = type(_random.Random())
    copyreg.pickle(systype,_random.Random.random)
    
    #function is not callable
    func = "hello"
    AssertError(TypeError,copyreg.pickle,testclass,func)
    func = 1
    AssertError(TypeError,copyreg.pickle,testclass,func)
    func = _random.Random()
    AssertError(TypeError,copyreg.pickle,testclass,func)
    
#dispatch_table
def test_dispatch_table():
    result = copyreg.dispatch_table
    #CodePlex Work Item 8522
    #AreEqual(5,len(result))
    
    temp = {
            "abc":"abc123",
            "def":"def123",
            "ghi":"ghi123"
           }
    copyreg.dispatch_table = temp
    AreEqual(temp,copyreg.dispatch_table)
    
    temp = {
            1:"abc123",
            2:"def123",
            3:"ghi123"
           }
    copyreg.dispatch_table = temp
    AreEqual(temp,copyreg.dispatch_table)
    
    temp = {
            1:123,
            8:789,
            16:45465
           }
    copyreg.dispatch_table = temp
    AreEqual(temp,copyreg.dispatch_table)
    
    #set dispathc_table as empty
    temp ={}
    copyreg.dispatch_table = temp
    AreEqual(temp,copyreg.dispatch_table)

#pickle_complex
def test_pickle_complex():
    #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=21908
    if not (is_cli or is_silverlight):
        AreEqual(copyreg.pickle_complex(1), (complex, (1, 0)))
    
    #negative tests
    AssertError(AttributeError,copyreg.pickle_complex,"myargu")
    obj2 = myCustom2()
    AssertError(AttributeError,copyreg.pickle_complex,obj2)

run_test(__name__)
