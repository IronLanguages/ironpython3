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
import gc
import _random


debug_list = [ 1, #DEBUG_STATS
               2, #DEBUG_COLLECTABLE
               4, #DEBUG_UNCOLLECTABLE
               8, #DEBUG_INSTANCES
               16,#DEBUG_OBJECTS
               32,#DEBUG_SAVEALL
               62#DEBUG_LEAK
              ]

#get_objects
def test_get_objects():
    if is_cli or is_silverlight:
        AssertError(NotImplementedError, gc.get_objects)
    else:
        gc.get_objects()

#get_threshold, set_threshold
def test_set_threshold():
    #the method has three arguments
    gc.set_threshold(0,-2,2)
    result = gc.get_threshold()
    AreEqual(result[0],0)
    AreEqual(result[1],-2)
    AreEqual(result[2],2)
    
    ##the method has two argument
    gc.set_threshold(0,128)
    result = gc.get_threshold()
    AreEqual(result[0],0)
    AreEqual(result[1],128)
    #CodePlex Work Item 8523
    #AreEqual(result[2],2)
    
   
    #the method has only one argument
    gc.set_threshold(-10009)
    result= gc.get_threshold()
    AreEqual(result[0],-10009)
    #CodePlex Work Item 8523
    #AreEqual(result[1],128)
    #AreEqual(result[2],2)
    
    #the argument is a random int
    for i in xrange(1,65535,6):
        gc.set_threshold(i)
        result = gc.get_threshold()
        AreEqual(result[0],i)
    
    #a argument is a float
    #CodePlex Work Item 8522
    #gc.set_threshold(2.1)
    #gc.set_threshold(3,-1.3)
    
    #a argument is a string
    #CodePlex Work Item 8522
    #AssertError(TypeError,gc.set_threshold,"1")
    #AssertError(TypeError,gc.set_threshold,"str","xdv#4")
    #AssertError(TypeError,gc.set_threshold,2,"1")
    #AssertError(TypeError,gc.set_threshold,31,-123,"asdfasdf","1")
    
    #a argument is a object
    #CodePlex Work Item 8522
    #o  = object()
    #o2 = object()
    #AssertError(TypeError,gc.set_threshold,o)
    #AssertError(TypeError,gc.set_threshold,o,o2)
    #AssertError(TypeError,gc.set_threshold,1,-123,o)
    #o  = _random.Random()
    #o2 = _random.Random()
    #AssertError(TypeError,gc.set_threshold,o)
    #AssertError(TypeError,gc.set_threshold,o,o2)
    #AssertError(TypeError,gc.set_threshold,8,64,o)
    
#get_referrers
def test_get_referrers():
    if is_cli or is_silverlight:
        AssertError(NotImplementedError, gc.get_referrers,1,"hello",True)
        AssertError(NotImplementedError, gc.get_referrers)
    else:
        gc.get_referrers(1,"hello",True)
        gc.get_referrers()
        
        class TempClass: pass
        tc = TempClass()
        AreEqual(gc.get_referrers(TempClass).count(tc), 1)
    
    
#get_referents
def test_get_referents():
    if is_cli or is_silverlight:
        AssertError(NotImplementedError, gc.get_referents,1,"hello",True)
        AssertError(NotImplementedError, gc.get_referents)
    else:
        gc.get_referents(1,"hello",True)
        gc.get_referents()
        
        class TempClass: pass
        AreEqual(gc.get_referents(TempClass).count('TempClass'), 1)

#enable
def test_enable():
    gc.enable()
    result = gc.isenabled()
    Assert(result,"enable Method can't set gc.isenabled as true.")
    
#disable
def test_disable():
    if is_cli or is_silverlight:
        from iptest.warning_util import warning_trapper
        w = warning_trapper()
        w.hook()
        gc.disable()
        m = w.finish()        
        AreEqual(m[0].message, 'IronPython has no support for disabling the GC')
    else:
        gc.disable()
        result = gc.isenabled()
        Assert(result == False,"enable Method can't set gc.isenabled as false.")

#isenabled
def test_isenabled():
    gc.enable()
    result = gc.isenabled()
    Assert(result,"enable Method can't set gc.isenabled as true.")
    
    if not is_cli and not is_silverlight:
        gc.disable()
        result = gc.isenabled()
        Assert(result == False,"enable Method can't set gc.isenabled as false.")

#collect
@skip("silverlight")
def test_collect():
    if is_cli:
        i = gc.collect() # returns # of bytes collected, could be anything
    else:
        for debug in debug_list:
            gc.set_debug(debug)
            gc.collect()
    gc.collect(0)
    
    #Negative
    AssertError(ValueError, gc.collect, -1)
    AssertError(ValueError, gc.collect, 2147483647)
    
#set_dubug,get_debug
def test_setdebug():
    if is_cli or is_silverlight:
        for debug in debug_list:
            AssertError(NotImplementedError, gc.set_debug,debug)
            AreEqual(None,gc.get_debug())
    else:
        for debug in debug_list:
            gc.set_debug(debug)
            AreEqual(debug,gc.get_debug())


#garbage
def test_garbage():
    i = len(gc.garbage)
    AreEqual(0,i)
    
#gc
def test_gc():
    Assert(not hasattr(gc, 'gc'))
    
#test DEBUG_STATS,DEBUG_COLLECTABLE,DEBUG_UNCOLLECTABLE,DEBUG_INSTANCES,DEBUG_OBJECTS,DEBUG_SAVEALL and DEBUG_LEAK
def test_debug_stats():
    AreEqual(1,gc.DEBUG_STATS)
    AreEqual(2,gc.DEBUG_COLLECTABLE)
    AreEqual(4,gc.DEBUG_UNCOLLECTABLE)
    AreEqual(8,gc.DEBUG_INSTANCES)
    AreEqual(16,gc.DEBUG_OBJECTS)
    AreEqual(32,gc.DEBUG_SAVEALL)
    AreEqual(62,gc.DEBUG_LEAK)
 

@skip("cli", "silverlight") #CodePlex Work Item 8202
def test_get_debug():
    state = [0,gc.DEBUG_STATS,gc.DEBUG_COLLECTABLE,gc.DEBUG_UNCOLLECTABLE,gc.DEBUG_INSTANCES,gc.DEBUG_OBJECTS,gc.DEBUG_SAVEALL,gc.DEBUG_LEAK]
    result = gc.get_debug()
    if result not in state:
        Fail("Returned value of getdebug method is not valid value:" + str(result))

#CodePlex Work Item# 8202
#if gc.get_debug()!=0:
#    raise "Failed - get_debug should return 0 if set_debug has not been used"
   
run_test(__name__)
