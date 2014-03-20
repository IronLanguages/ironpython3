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
"this" indexer
'''
#------------------------------------------------------------------------------

from iptest.assert_util import *
skiptest("silverlight")
add_clr_assemblies("indexerdefinitionscs", "typesamples")

from Merlin.Testing import *
from Merlin.Testing.Indexer import *
from Merlin.Testing.TypeSample import *

def test_this_in_interface():
    for t in [
                StructExplicitImplementInterface,
                ClassExplicitImplementInterface,
                StructImplicitImplementInterface,
                ClassImplicitImplementInterface,
             ]:
        x = t()
        
        #IReturnDouble.set_Item(x, 1, 10)  # bug 363289
        #AreEqual(IReturnDouble.get_Item(x, 1), 10)
    
def test_params():
    x = ClassWithParamsIndexer()
    x.Init()

    for y, z in [(y,z) for y,z in zip(x, range(100)) if z != 0]:
	    AreEqual(y,z)
    
    x[1] = 2
    AreEqual(2, x[1])
        
    x[3, 4] = 5
    AreEqual(5, x[0, 7])
    
    AreEqual(x[()], -100)
    x[()] = 9
    AreEqual(x[()], 9)
    
def test_overload1():
    x = ClassWithIndexerOverloads1()
    x.Init()
    
    for y, z in [(y,z) for y,z in zip(x, range(100)) if z != 0]:
	    AreEqual(y,z)
	    
    AreEqual(x[()], -200)
    x[1, -1, 1, -1] = 3
    AreEqual(x[()], 3)
    
    x[6] = 0
    AreEqual(x[6], 0)
    
    x[2, 3] = 4
    AreEqual(x[1, 6], 4)
    AreEqual(x[7], 7)
    
    x[4, 5, 6] = 7
    AreEqual(7, x[4, 5, 6])
    
    Assert(not hasattr(x, 'get_Item'))
    Assert(not hasattr(x, 'set_Item'))
    
def test_overload2():
    x = ClassWithIndexerOverloads2()

    for y, z in zip(x, range(10)):
	    AreEqual(y,z)
	
    x[1] = 2
    x['1'] = '3'
    AreEqual('3', x['1'])
    AreEqual(2, x[1])
    
def test_basic():
    for t in [  
                ClassWithIndexer,
                StructWithIndexer,   
             ]:
        x = t()
        x.Init()
        
        for y, z in zip(x, range(10)):
            AreEqual(y,z)
	    
        a, b, c = 2, SimpleStruct(3), SimpleClass(4)
        
        x[1] = a
        AreEqual(x[1], a)
        
        x[2, "ab1"] = b
        AreEqual(x[12, "ab"].Flag, b.Flag)
        
        x['ab', 'c', 'd'] = c
        AreEqual(x['a', 'b', 'cd'], c)
        
        a, b, c = 5, SimpleStruct(6), SimpleClass(7)
        Assert(not hasattr(x, 'set_Item'))
        Assert(not hasattr(x, 'get_Item'))
                
        # bad arg count
        AssertErrorWithMatch(TypeError, "expected int, got tuple", lambda: x[()])
        AssertErrorWithMatch(TypeError, "__getitem__\(\) takes at most 3 arguments \(4 given\)", lambda: x[1, 2, 3, 4])
        
        # bad arg type
        AssertErrorWithMatch(TypeError, "expected str, got int", lambda: x[1, 2, 3])
        
        # bad value type
        def f(): x[1] = 'abc'
        AssertErrorWithMatch(TypeError, "expected int, got str", f)
        

def test_readonly():
    x = ReadOnlyIndexer()
    AreEqual(x[1], 10)
    
    def f(): x[2] = 20
    AssertErrorWithMatch(TypeError, 
        "'ReadOnlyIndexer' object does not support item assignment",
        f)

def test_writeonly():
    x = WriteOnlyIndexer()
    Flag.Set(0)
    x[1] = 10
    Flag.Check(11)
    
    AssertErrorWithMatch(TypeError, 
        "'WriteOnlyIndexer' object is unsubscriptable",
        lambda: x[1])


def test_access_from_derived_class():
    x = DerivedClassWithoutIndexer()
    x.Init()
    
    for y, z in zip(x, range(10)):
	    AreEqual(y,z)
    
    x[2] = 4
    AreEqual(4, x[2])
    
    x = DerivedClassExplicitImplementInterface()
    x.Init()
    #IReturnDouble.set_Item(x, 3, 6)                      # bug 363289
    #AreEqual(IReturnDouble.get_Item(x, 3), 6)


def test_new_indexer():
    x = DerivedClassWithNewIndexer()
    
    for y, z in zip(x, range(0, -10, -1)):
	    AreEqual(y,z)
	    
    x[2] = 9
    AreEqual(-18, x[2])
    
    x = DerivedClassWithNewWriteOnlyIndexer()
    x[3] = 10
    #AssertError(TypeError, lambda: x[3])  # bug 362877
    
run_test(__name__)
