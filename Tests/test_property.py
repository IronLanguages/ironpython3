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

import os
import sys
import unittest

from iptest import IronPythonTestCase, is_cli, is_netstandard, run_test, skipUnlessIronPython

class PropertyTest(IronPythonTestCase):
    def setUp(self):
        super(PropertyTest, self).setUp()
        import System

        self.array_list_options = []
        if is_cli:
            self.array_list_options.append(System.Collections.Generic.List[int])
            if is_netstandard:
                import clr
                clr.AddReference("System.Collections.NonGeneric")
            self.array_list_options.append(System.Collections.ArrayList)

    def test_stdin(self):
        self.assertTrue('<stdin>' in str(sys.stdin))
        self.assertTrue('<stdout>' in str(sys.stdout))
        self.assertTrue('<stderr>' in str(sys.stderr))

    def test_array_list(self):        
        for ArrayList in self.array_list_options:
            l = ArrayList()
            
            index = l.Add(22)
            self.assertTrue(l[0] == 22)
            l[0] = 33
            self.assertTrue(l[0] == 33)

    # getting a property from a class should return the property,
    # getting it from an instance should do the descriptor check
    def test_sanity(self):
        class foo(object):
            def myset(self, value): pass
            def myget(self): return "hello"
            prop = property(fget=myget,fset=myset)

        self.assertEqual(type(foo.prop), property)

        a = foo()
        self.assertEqual(a.prop, 'hello')


    @skipUnlessIronPython()
    def test_builtinclrtype_set(self):
        """setting an instance property on a built-in type should throw that you can't set on built-in types"""
        import System
        for ArrayList in self.array_list_options:
            def setCount():
                ArrayList.Count = 23

            self.assertRaises(AttributeError, setCount)
        
            # System.String.Empty is a read-only static field
            self.assertRaises(AttributeError, setattr, System.String, "Empty", "foo")


    # a class w/ a metaclass that has a property
    # defined should hit the descriptor when getting
    # it on the class.
    def test_metaclass(self):
        class MyType(type):
            def myget(self): return 'hello'
            aaa = property(fget=myget)

        class foo(object):
            __metaclass__ = MyType

        self.assertEqual(foo.aaa, 'hello')


    def test_reflected_property(self):
        # ReflectedProperty tests
        for ArrayList in self.array_list_options:
            alist = ArrayList()
            self.assertEqual(ArrayList.Count.__set__(None, 5), None)
            self.assertRaises(TypeError, ArrayList.Count, alist, 5)
            self.assertEqual(alist.Count, 0)
            self.assertEqual(str(ArrayList.__dict__['Count']), '<property# Count on %s>' % ArrayList.__name__)
        
            def tryDelReflectedProp():
                del ArrayList.Count

            self.assertRaises(AttributeError, tryDelReflectedProp)

    
    @skipUnlessIronPython()
    def test_reflected_extension_property_ops(self):
        '''
        Test to hit IronPython.RunTime.Operations.ReflectedExtensionPropertyOps
        '''
        t_list = [  (complex.__dict__['real'], 'complex', 'float', 'real'),
                    (complex.__dict__['imag'], 'complex', 'float', 'imag'),
                    ]
        
        for stuff, typename, returnType, propName in t_list:
            expected = "Get: " + propName + "(self: " + typename + ") -> " + returnType + os.linesep
            self.assertTrue(stuff.__doc__.startswith(expected), stuff.__doc__)

    def test_class_doc(self):
        self.assertEqual(object.__dict__['__class__'].__doc__, "the object's class")
    
    def test_prop_doc_only(self):
        """define a property w/ only the doc"""

        x = property(None, None, doc = 'Holliday')
        self.assertEqual(x.fget, None)
        self.assertEqual(x.fset, None)
        self.assertEqual(x.fdel, None)
        self.assertEqual(x.__doc__, 'Holliday')
 
    def test_member_lookup_oldclass(self):
        class OldC:
            xprop = property(lambda self: self._xprop)
            def __init__(self):
                self._xprop = 42
                self.xmember = 42
                
        c = OldC()
        c.__dict__['xprop'] = 43
        c.__dict__['xmember'] = 43
        self.assertEqual(c.xprop, 43)
        self.assertEqual(c.xmember, 43)
        
        c.xprop   = 41
        c.xmember = 41
        self.assertEqual(c.xprop, 41)
        self.assertEqual(c.xmember, 41)
        self.assertEqual(c.__dict__['xprop'], 41)
        self.assertEqual(c.__dict__['xmember'], 41)


    def test_member_lookup_newclass(self):
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
        self.assertEqual(c.xprop, 42)
        self.assertEqual(c.xmember, 43)
        
        c.xprop = 41
        c.xmember = 41
        self.assertEqual(c.xprop, 41)
        self.assertEqual(c.xmember, 41)
        self.assertEqual(c.__dict__['xprop'], 43)
        self.assertEqual(c.__dict__['xmember'], 41)


    def test_inheritance(self):
        class MyProperty(property):
            def __init__(self, *args):
                property.__init__(self, *args)

        x = MyProperty(1,2,3)
        
        self.assertEqual(x.fget, 1)
        self.assertEqual(x.fset, 2)
        self.assertEqual(x.fdel, 3)

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
        self.assertEqual(inst.x, 42)

        inst.x = 'abc'
        self.assertEqual(inst.foo, 'abc')
        
        del inst.x
        self.assertEqual(inst.bar, 'baz')

    def test_property_mutation(self):
        class x(object): pass
        
        prop = property()
        x.foo = prop
        inst = x()
        for i in xrange(42):
            prop.__init__(lambda self: i)
            self.assertEqual(inst.foo, i)

    def test_property_doc(self):
        def getter(self):
            """getter doc"""
        
        self.assertEqual(property(getter).__doc__, "getter doc")
        self.assertEqual(property(None).__doc__, None)
        self.assertEqual(property(None, getter, getter).__doc__, None)
        self.assertTrue(type(property.__doc__) is str)
        
        def assignerror(): 
            property.__doc__ = None
        self.assertRaisesMessage(TypeError, "can't set attributes of built-in/extension type 'property'", assignerror)

    def test_class_assign(self):
        """assigning to a property through the class should replace the
        property in the class dictionary"""
        class x(object):
            def set(self, value):
                AssertUnreachable()
            prop = property(lambda x:42, set)
        
        x.prop = 42
        self.assertEqual(x.__dict__['prop'], 42)

    def test_assign(self):
        x = property()
        
        for attr in ['__doc__', 'fdel', 'fget', 'fset']:
            self.assertRaisesMessage(TypeError, "readonly attribute", lambda : setattr(x, attr, 'abc'))


run_test(__name__)
