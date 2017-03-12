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

array_list_options = []
if is_cli or is_silverlight:
    array_list_options.append(System.Collections.Generic.List[int])
    if not is_silverlight:
        array_list_options.append(System.Collections.ArrayList)

for ArrayList in array_list_options:
    l = ArrayList()
    
    index = l.Add(22)
    Assert(l[0] == 22)
    l[0] = 33
    Assert(l[0] == 33)

import sys
Assert('<stdin>' in str(sys.stdin))
#Assert('<stdout>' in str(sys.stdout))
#Assert('<stderr>' in str(sys.stderr))


# getting a property from a class should return the property,
# getting it from an instance should do the descriptor check
def test_sanity():
    class foo(object):
        def myset(self, value): pass
        def myget(self): return "hello"
        prop = property(fget=myget,fset=myset)

    AreEqual(type(foo.prop), property)

    a = foo()
    AreEqual(a.prop, 'hello')


@skip("win32")
def test_builtinclrtype_set():
    # setting an instance property on a built-in type should
    # throw that you can't set on built-in types
    for ArrayList in array_list_options:
        def setCount():
            ArrayList.Count = 23

        AssertError(AttributeError, setCount)
    
        # System.String.Empty is a read-only static field
        AssertError(AttributeError, setattr, System.String, "Empty", "foo")


# a class w/ a metaclass that has a property
# defined should hit the descriptor when getting
# it on the class.
def test_metaclass():
    class MyType(type):
        def myget(self): return 'hello'
        aaa = property(fget=myget)

    class foo(object, metaclass=MyType):
        pass

    AreEqual(foo.aaa, 'hello')


def test_reflected_property():
    # ReflectedProperty tests
    for ArrayList in array_list_options:
        alist = ArrayList()
        AreEqual(ArrayList.Count.__set__(None, 5), None)
        AssertError(TypeError, ArrayList.Count, alist, 5)
        AreEqual(alist.Count, 0)
        AreEqual(str(ArrayList.__dict__['Count']), '<property# Count on %s>' % ArrayList.__name__)
    
        def tryDelReflectedProp():
            del ArrayList.Count

        AssertError(AttributeError, tryDelReflectedProp)

    
@skip("win32")
def test_reflected_extension_property_ops():
    '''
    Test to hit IronPython.RunTime.Operations.ReflectedExtensionPropertyOps
    '''
    t_list = [  (complex.__dict__['real'], 'complex', 'float', 'real'),
                (complex.__dict__['imag'], 'complex', 'float', 'imag'),
                ]
    
    for stuff, typename, returnType, propName in t_list:
        expected = "Get: " + propName + "(self: " + typename + ") -> " + returnType + newline
        Assert(stuff.__doc__.startswith(expected), stuff.__doc__)
                
                
def test_class_doc():
    AreEqual(object.__dict__['__class__'].__doc__, "the object's class")
    
def test_prop_doc_only():
    # define a property w/ only the doc

    x = property(None, None, doc = 'Holliday')
    AreEqual(x.fget, None)
    AreEqual(x.fset, None)
    AreEqual(x.fdel, None)
    AreEqual(x.__doc__, 'Holliday')
 
def test_member_lookup_newclass():
    class NewC(object):
        def xprop_setter(self, xprop):
            self._xprop = xprop
    
        xprop = property(lambda self: self._xprop,
                         xprop_setter)
        
        def __init__(self):
            self._xprop = 42
            self.xmember = 42

    c = NewC()
    c.__dict__['xprop'] = 43
    c.__dict__['xmember'] = 43
    AreEqual(c.xprop, 42)
    AreEqual(c.xmember, 43)
    
    c.xprop = 41
    c.xmember = 41
    AreEqual(c.xprop, 41)
    AreEqual(c.xmember, 41)
    AreEqual(c.__dict__['xprop'], 43)
    AreEqual(c.__dict__['xmember'], 41)


def test_inheritance():
    class MyProperty(property):
        def __init__(self, *args):
            property.__init__(self, *args)
            
    x = MyProperty(1,2,3)
    
    AreEqual(x.fget, 1)
    AreEqual(x.fset, 2)
    AreEqual(x.fdel, 3)


    class MyProperty(property):
        def __init__(self, *args):
            property.__init__(self, *args)
        def __get__(self, *args):
            return 42
        def __set__(self, inst, value):
            inst.foo = value
        def __delete__(self, *args):
            inst.bar = 'baz'
            
    class MyClass(object):
        x = MyProperty()
    
    inst = MyClass()
    AreEqual(inst.x, 42)

    inst.x = 'abc'
    AreEqual(inst.foo, 'abc')
    
    del inst.x
    AreEqual(inst.bar, 'baz')

def test_property_mutation():
    class x(object): pass
    
    prop = property()
    x.foo = prop
    inst = x()
    for i in range(42):
        prop.__init__(lambda self: i)
        AreEqual(inst.foo, i)
        

def test_property_doc():
    def getter(self):
        """getter doc"""
    
    AreEqual(property(getter).__doc__, "getter doc")
    AreEqual(property(None).__doc__, None)
    AreEqual(property(None, getter, getter).__doc__, None)
    Assert(type(property.__doc__) is str)
    
    def assignerror(): 
        property.__doc__ = None
    AssertErrorWithMessage(TypeError, "can't set attributes of built-in/extension type 'property'", assignerror)

def test_class_assign():
    """assigning to a property through the class should replace the
       property in the class dictionary"""
    class x(object):
        def set(self, value):
            AssertUnreachable()
        prop = property(lambda x:42, set)
    
    x.prop = 42
    AreEqual(x.__dict__['prop'], 42)

def test_assign():
    x = property()
    
    for attr in ['__doc__', 'fdel', 'fget', 'fset']:
        AssertErrorWithMessage(TypeError, "readonly attribute", lambda : setattr(x, attr, 'abc'))

run_test(__name__)
