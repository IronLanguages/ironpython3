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
Operations on event type.
'''
#------------------------------------------------------------------------------
from iptest.assert_util import *
skiptest("silverlight")

add_clr_assemblies("eventdefinitions", "typesamples")

from Merlin.Testing import *
from Merlin.Testing.Event import *

def test_basic():
    # t1 is where the event is declared
    for t1 in [  ClassImplicitlyImplementInterface, 
                #StructImplicitlyImplementInterface,  # bug: 361955
                ClassWithSimpleEvent,
                #StructWithSimpleEvent,               # bug: 361955  
             ]:
             
        # t2 is where the handler is defined
        for t2 in [ TargetClass, TargetStruct ]:
            o = t2()
    
            # try both static and instance method
            for (double, square, negate, bad) in [
                    (t2.s_Double, t2.s_Square, t2.s_Negate, t2.s_Throw), 
                    (o.i_Double, o.i_Square, o.i_Negate, o.i_Throw), 
                ]:
                
                # no duplicate handlers, add/remove
                x = t1()
                
                Flag.Set(0)
                AreEqual(x.CallInside(1), -1)
                Flag.Check(0)
                
                Flag.Set(0)
                x.OnAction += double
                AreEqual(x.CallInside(2), 4)
                Flag.Check(1)
                
                Flag.Set(0)
                x.OnAction += square
                AreEqual(x.CallInside(3), 9)
                Flag.Check(101)
                
                Flag.Set(0)
                x.OnAction += negate
                AreEqual(x.CallInside(4), -4)
                Flag.Check(111)
                
                Flag.Set(0)
                x.OnAction -= square
                AreEqual(x.CallInside(5), -5)
                Flag.Check(11)

                Flag.Set(0)        
                x.OnAction -= double 
                AreEqual(x.CallInside(6), -6)
                Flag.Check(10)
                
                Flag.Set(0)
                x.OnAction -= negate
                AreEqual(x.CallInside(7), -1)
                Flag.Check(0)
                
                # duplicate: which one get removed
                x = t1()
                
                x.OnAction += double 
                x.OnAction += square
                x.OnAction += double
                x.OnAction += double 
                
                Flag.Set(0)
                AreEqual(x.CallInside(8), 16)
                Flag.Check(103)
                
                x.OnAction -= double
                AreEqual(x.CallInside(9), 18)

                x.OnAction -= double    # verify the last one is removed
                Flag.Set(0)
                AreEqual(x.CallInside(10), 100)  # bug 361971
                Flag.Check(101)
                
                x.OnAction -= double 
                AreEqual(x.CallInside(11), 121)

                x.OnAction -= square
                AreEqual(x.CallInside(12), -1)
                
                # remove from empty invocation list
                x.OnAction -= double 
                Flag.Set(0)
                AreEqual(x.CallInside(13), -1)
                Flag.Check(0)
                
                # troubling event handler in the middle
                x = t1()
                x.OnAction += double 
                x.OnAction += bad
                x.OnAction += negate
                
                Flag.Set(0)
                AssertError(Exception, lambda: x.CallInside(14))
                Flag.Check(1)  # this also verified double was added/thus called first
                
                
                # different handler handling path:
                #  - explicitly created delegate objects (d_xxx)
                #  - mixed 
                
                x = t1()
                
                d_double = Int32Int32Delegate(double)
                d_negate = Int32Int32Delegate(negate)
                d_square = Int32Int32Delegate(square)

                x.OnAction += d_double
                x.OnAction += d_square
                x.OnAction += double
                
                Flag.Set(0)
                AreEqual(x.CallInside(15), 30)
                Flag.Check(102)
                
                x.OnAction += d_negate
                AreEqual(x.CallInside(16), -16)
                
                x.OnAction -= d_square
                Flag.Set(0)
                AreEqual(x.CallInside(17), -17)
                Flag.Check(12)
                
                x.OnAction -= negate                # remove the "native" 
                AreEqual(x.CallInside(18), -18)
                
                x.OnAction -= d_negate              # remove the 'stub'ed
                AreEqual(x.CallInside(19), 38)
                
                x.OnAction -= negate        # list is not empty, try to remove the not-in-list
                x.OnAction -= d_negate      # same
                AreEqual(x.CallInside(20), 40)
                
                x.OnAction -= double 
                x.OnAction -= d_double 
                AreEqual(x.CallInside(21), -1)
                
                
def test_things_from_bound_event():
    #print ClassWithEvents.StaticEvent
    #print ClassWithEvents.InstanceEvent
    pass
    
def test_explicitly_implemented_event():
    t1 = StructExplicitlyImplementInterface()
    #t1.OnAction += TargetClass.s_Double
    
    #print IInterface.OnAction        # bound event
    #print dir(IInterface.OnAction)
    #print IInterface.OnAction.Event  # reflected event
    #IInterface.OnAction.Add(t1, TargetClass.s_Double)

@skip("multiple_execute")
def test_static_event():
    for t1 in [
                    ClassWithStaticEvent, 
                    #StructWithStaticEvent
              ]:
              
        x = t1()
        
        tc, ts = TargetClass(), TargetStruct()
        
        t1.OnAction += TargetClass.s_Double
        t1.OnAction += TargetStruct.s_Negate
        t1.OnAction += tc.i_Square
        
        Flag.Set(0)
        AreEqual(x.CallInside(30), 900)
        Flag.Set(111)
        
        t1.OnAction += ts.i_Negate
        Flag.Set(0)
        AreEqual(x.CallInside(31), -31)
        Flag.Check(121)
        
        t1.OnAction -= TargetStruct.s_Negate
        t1.OnAction -= ts.i_Double  # not added before
        t1.OnAction -= tc.i_Square 
        
        Flag.Set(0)
        AreEqual(x.CallInside(32), -32)
        Flag.Set(11)
        
        Flag.Set(0)
        t1.OnAction -= ts.i_Negate
        AreEqual(x.CallInside(33), 66)
        Flag.Set(1)

def test_access_static_event_from_derived_type():
    def f1(): DerivedClassWithStaticEvent.OnAction += TargetClass.s_Double
    def f2(): DerivedClassWithStaticEvent.OnAction -= TargetStruct().s_Double
    
    for f in [f1, f2]:
        AssertErrorWithMessage(AttributeError, 
            "attribute 'OnAction' of 'DerivedClassWithStaticEvent' object is read-only", 
            f)
    
    #x = DerivedClassWithStaticEvent()
    #def f1(): x.OnAction += TargetClass.s_Double
    #def f2(): x.OnAction -= TargetStruct().s_Double
    #f1()

def test_assignment():
    x = ClassWithSimpleEvent()
    
    # 362440
    def f(): x.OnAction = TargetClass.s_Double
    AssertErrorWithMessage(AttributeError, 
        "attribute 'OnAction' of 'ClassWithSimpleEvent' object is read-only", 
        f)

def test_add_sub():
    x = ClassWithSimpleEvent()
    
    def f(): x.OnAction = x.OnAction + TargetClass.s_Negate   
    AssertErrorWithMessage(TypeError, 
        "unsupported operand type(s) for +: 'BoundEvent' and 'builtin_function_or_method'", 
        f)
    
    def f(): x.OnAction - TargetClass.s_Negate
    AssertErrorWithMessage(TypeError, 
        "unsupported operand type(s) for -: 'BoundEvent' and 'builtin_function_or_method'", 
        f)
        
def test_iadd_isub():
    x = ClassWithSimpleEvent()
    
    # 362447
    #x.OnAction.__iadd__(TargetClass.s_Negate)
    #AreEqual(x.CallInside(101), -101)
    #x.OnAction.__isub__(TargetClass.s_Negate)
    #AreEqual(x.CallInside(102), -1)

def test_add_method_descriptor():    
    x = ClassWithSimpleEvent()
    
    x.OnAction += TargetClass.s_Negate
    x.OnAction += TargetClass.i_Double  # method
    Flag.Set(0)
    AssertErrorWithMessage(TypeError, 
        "i_Double() takes exactly 2 arguments (1 given)", 
        lambda: x.CallInside(4))
    Flag.Check(10)
    
def test_call_outside():
    x = ClassWithSimpleEvent()
    
    x.OnAction += TargetClass.s_Negate
    #x.OnAction(3)  # 362449
    

run_test(__name__)

