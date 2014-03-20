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
NOTES:
- needs to be rewritten
'''
#------------------------------------------------------------------------------
from iptest.assert_util import *
skiptest("silverlight")

add_clr_assemblies("baseclasscs")

from Merlin.Testing.Accessibility import *
import System

def throw_for_read_private_internal(x):
    AssertError(AttributeError, lambda: x.private_static_field)
    AssertError(AttributeError, lambda: x.private_static_method)
    AssertError(AttributeError, lambda: x.private_static_property)
    AssertError(AttributeError, lambda: x.private_static_event)
    AssertError(AttributeError, lambda: x.private_static_nestedclass)
    AssertError(AttributeError, lambda: x.internal_static_field)
    AssertError(AttributeError, lambda: x.internal_static_method)
    AssertError(AttributeError, lambda: x.internal_static_property)
    AssertError(AttributeError, lambda: x.internal_static_event)
    AssertError(AttributeError, lambda: x.internal_static_nestedclass)

def pass_for_read_protected(x):
    x.protected_static_field
    x.protected_static_method
    #x.protected_static_property  # bug 370438
    #x.protected_static_event     # bug 370432
    if str(x).startswith("<C1 object") or str(x).startswith("<C2 object"):
        print "Skipping (http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=24106)..."
    else:
        x.protected_static_nestedclass
    
def throw_for_read_protected(x):
    AssertError(AttributeError, lambda: x.protected_static_field)
    AssertError(AttributeError, lambda: x.protected_static_method)
    AssertError(AttributeError, lambda: x.protected_static_property)
    AssertError(AttributeError, lambda: x.protected_static_event)
    AssertError(AttributeError, lambda: x.protected_static_nestedclass)

def pass_for_read_public(x):    
    x.public_static_field
    x.public_static_method
    x.public_static_property
    x.public_static_event
    x.public_static_nestedclass

def all_read(x):
    AssertError(AttributeError, lambda: x.private_static_field)
    AssertError(AttributeError, lambda: x.private_static_method)
    AssertError(AttributeError, lambda: x.private_static_property)
    AssertError(AttributeError, lambda: x.private_static_event)
    AssertError(AttributeError, lambda: x.private_static_nestedclass)
    AssertError(AttributeError, lambda: x.internal_static_field)
    AssertError(AttributeError, lambda: x.internal_static_method)
    AssertError(AttributeError, lambda: x.internal_static_property)
    AssertError(AttributeError, lambda: x.internal_static_event)
    AssertError(AttributeError, lambda: x.internal_static_nestedclass)
    x.protected_static_field
    x.protected_static_method
    #x.protected_static_property  # bug 370438
    #x.protected_static_event     # bug 370432
    Assert(not hasattr('x', 'protected_static_nestedclass')) # not supported
    x.public_static_field
    x.public_static_method
    x.public_static_property
    x.public_static_event
    x.public_static_nestedclass
    
    AssertError(AttributeError, lambda: x.private_instance_field)
    AssertError(AttributeError, lambda: x.private_instance_method)
    AssertError(AttributeError, lambda: x.private_instance_property)
    AssertError(AttributeError, lambda: x.private_instance_event)
    AssertError(AttributeError, lambda: x.private_instance_nestedclass)
    AssertError(AttributeError, lambda: x.internal_instance_field)
    AssertError(AttributeError, lambda: x.internal_instance_method)
    AssertError(AttributeError, lambda: x.internal_instance_property)
    AssertError(AttributeError, lambda: x.internal_instance_event)
    AssertError(AttributeError, lambda: x.internal_instance_nestedclass)
    x.protected_instance_field
    x.protected_instance_method
    x.protected_instance_property
    #x.protected_instance_event     # bug 370432
    Assert(not hasattr('x', 'protected_instance_nestedclass')) # not supported
    x.public_instance_field
    x.public_instance_method
    x.public_instance_property
    x.public_instance_event
    x.public_instance_nestedclass
    
def test_access_outside(): 
    class C1(CliClass): 
        __slots__ = []
    
    class C2(DerivedCliClass): 
        __slots__ = []

    for x in [C1, C2, C1(), C2()]:
        all_read(x)
        pass_for_read_protected(x)
        
        # extra methods
        AssertError(AttributeError, lambda: x.get_private_static_property)
        AssertError(AttributeError, lambda: x.remove_private_static_event)
        AssertError(AttributeError, lambda: x.set_internal_static_property)
        AssertError(AttributeError, lambda: x.add_internal_static_event)
        AssertError(AttributeError, lambda: x.set_protected_static_property)
        AssertError(AttributeError, lambda: x.get_public_static_property)
        Assert(hasattr(x, 'add_protected_static_event'))
        x.remove_public_static_event

        AssertError(AttributeError, lambda: x.set_private_instance_property)
        AssertError(AttributeError, lambda: x.add_private_instance_event)
        AssertError(AttributeError, lambda: x.get_internal_instance_property)
        AssertError(AttributeError, lambda: x.remove_internal_instance_event)
        AssertError(AttributeError, lambda: x.get_protected_instance_property)
        AssertError(AttributeError, lambda: x.set_public_instance_property)
        Assert(hasattr(x, 'remove_protected_instance_event'))
        x.add_public_instance_event
        
    def f(*arg): pass
    
    # write 
    for x in [C1, C2]:
        x.protected_static_field = 1
        x.protected_static_property = 2
        #x.protected_static_event += f
        x.public_static_field = 3
        x.public_static_property = 4
        #x.public_static_event += f
    
    for x in [C1(), C2()]: 
        x.protected_instance_field = 11
        Assert(hasattr(x, 'protected_instance_property'))
        #x.protected_instance_event += f
        x.public_instance_field = 13
        x.public_instance_property = 14
        x.public_instance_event += f
        
    # you may find it a bit surprising 
    # also assign it a string, not int
    
    C1.public_static_field
    C1.public_static_field = "this is python"
    C1.public_static_field 
    
    C1.method = 1
    C1.method
    
    C2.private_static_field = "this is python!"   
    AreEqual(C2.private_static_field, "this is python!")

def test_access_inside():
    class C1(CliClass):
        def m(self):
            for x in [C1, self]: all_read(x)
    class C2(DerivedCliClass):
        def m(self):
            for x in [C2, self]: all_read(x)

    for C in [C1, C2]:
        x = C()
        x.m()

# TODO: Try against PythonDerivedType1, PythonDerivedType2?
class PythonType1(CliClass): pass
class PythonDerivedType1(PythonType1): pass
class PythonType2(DerivedCliClass): pass
class PythonDerivedType2(PythonType2): pass

# TODO: cover x.member where x is CLR type or instance.
#       x.protected_instance_method should throw.
def test_reflected_type():
    for C in [CliClass, DerivedCliClass]:
        # hasattr reports false because accessing the attribute
        # raises, this is consistent w/ CPython when accessing a
        # user defined property w/ a getter that raises.
        Assert(not hasattr(C, 'internal_static_field'))
        Assert(hasattr(C, 'protected_static_field'))
        Assert('internal_static_field' not in dir(C))
        Assert('protected_static_field' in dir(C))

        x = C()
        Assert(not hasattr(x, 'protected_instance_field'))
        Assert('protected_instance_field' in dir(C))
        Assert('protected_instance_field' in dir(x))
        AssertError(TypeError, lambda : x.protected_instance_field)

#--MAIN------------------------------------------------------------------------
run_test(__name__)

