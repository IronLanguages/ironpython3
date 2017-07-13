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

import sys
import unittest
me = sys.modules[__name__]

from iptest import IronPythonTestCase, is_cli, is_posix, path_modifier, run_test

class AttrTest(IronPythonTestCase):

    def test_delattr(self):
        class C(object):
            pass
        
        y = C.__delattr__
        y = C().__delattr__
        y = object.__delattr__
        C.a = 10
        self.assertTrue(C.a == 10)
        del C.a
        success=0
        try:
            y = C.a
        except AttributeError:
            success = 1
        self.assertTrue(success == 1)

######################################################################################

# Non-string attributes on a module

    def test_non_string_attrs(self):
        def CheckObjectKeys(d):
            self.assertEqual(d.has_key(1), True)
            self.assertEqual(repr(d).__contains__("1: '1'"), True)
        
        def SetDictionary(mod, dict):
            mod.__dict__ = dict
        
        def CheckDictionary(d):
            # add non-string index into the class and instance dictionary
            d[1] = '1'
            CheckObjectKeys(d)
            # Remove the non-string key since it causes problems later on
            del d[1]
        
        def CheckModule(mod):
            # add a new attribute to the type...
            mod.newModuleAttr = 'xyz'
            self.assertEqual(mod.newModuleAttr, 'xyz')
        
            CheckDictionary(mod.__dict__)
        
            mod.__dict__[1] = '1'
            self.assertEqual(dir(mod).__contains__(1), True)
            del mod.__dict__[1]
            
            # Try to replace __dict__
            self.assertRaisesMessage(TypeError, "readonly attribute", SetDictionary, mod, dict(mod.__dict__))
        
        # modules support object keys
        CheckModule(me)
        
        # old style classes support module keys
        class C:
            a = 1
            b = 2
        
        CheckDictionary(C.__dict__)
            
        # function locals support module keys
        def f():
            return locals()
            
        CheckDictionary(f())
        
        # closure locals support module keys
        def f():
            a = 3
            def g():
                x = a
                return locals()
            return g()
        
        CheckDictionary(f())
        
        # closure locals w/ locals at both levels supports module keys
        def f():
            a = 3
            def g():
                x = a
                return locals()
            y = locals()
            return g()
        
        CheckDictionary(f())
        
        # This is disabled since it causes recursion. We should define another test module to reload
        # reload(me)
        # CheckObjectKeys(me)

    def test_ModuleType_getattr(self):
        """CP34257: __getattr__ on a module subclass"""
        from types import ModuleType
        class ApiModule(ModuleType):
            def __init__(self, name="", importspec="", implprefix=None, attr=None):
                pass
            def __makeattr(self, name):
                return name
            __getattr__ = __makeattr

        t = ApiModule()
        self.assertEqual(t.Std, 'Std')

    def test_decorators(self):
        """Decorators starting with Bug #993"""
        def f(x='default'): return x
        
        cm = classmethod(f)
        sm = staticmethod(f)
        p = property(f)
        self.assertEqual(f.__get__(1)(), 1)
        self.assertEqual(str(f.__get__(2)), "<bound method ?.f of 2>")
        self.assertEqual(str(f.__get__(2, list)), "<bound method list.f of 2>")
        
        self.assertEqual(cm.__get__(1)(), int)
        self.assertEqual(str(cm.__get__(2)), "<bound method type.f of <type 'int'>>")
        
        self.assertEqual(sm.__get__(1)(), 'default')
        self.assertEqual(p.__get__(1), 1)

    #TODO: @skip("multiple_execute")
    def test_meta_attrs(self):
        """__getattribute__, __setattr__, __delattr__ on builtins"""
        if is_cli:
            import System
            dateTime = System.DateTime()
        
            self.assertEqual(dateTime.ToString, dateTime.__getattribute__("ToString"))
            self.assertRaisesMessage(AttributeError, "'DateTime' object attribute 'ToString' is read-only", dateTime.__setattr__, "ToString", "foo")
            self.assertRaisesMessage(AttributeError, "'DateTime' object attribute 'ToString' is read-only", dateTime.__delattr__, "ToString")
        
            arrayList = System.Collections.Generic.List[int]()
            arrayList.__setattr__("Capacity", 123)
            self.assertEqual(arrayList.Capacity, 123)
        
        self.assertEqual(me.__file__, me.__getattribute__("__file__"))
        me.__setattr__("__file__", "foo")
        self.assertEqual(me.__file__, "foo")
        me.__delattr__("__file__")
        
        class C(object):
            def foo(self): pass
        
        # C.foo is "unbound method" on IronPython but "function" on CPython
        if is_cli:
            self.assertEqual(C.foo, C.__getattribute__(C, "foo"))
        else:
            self.assertEqual(C.foo.im_func, C.__getattribute__(C, "foo"))
        self.assertEqual(C.__doc__, C.__getattribute__(C, "__doc__"))
        
        # fancy type.__doc__ access...
        x = type.__dict__['__doc__'].__get__

        class C(object): __doc__ = 'foo'

        class D(object): pass

        self.assertEqual(x(D, None), None)
        self.assertEqual(x(C, None), 'foo')

        class C(object): __doc__ = 42

        self.assertEqual(x(C, None), 42)
        
        self.assertRaisesMessage(TypeError, "can't apply this __setattr__ to type object", C.__setattr__, C, "__str__", "foo")
        self.assertRaisesMessage(TypeError, "can't apply this __delattr__ to type object", C.__delattr__, C, "__str__")
        
        s = "hello"
        
        self.assertEqual(s.center, s.__getattribute__("center"))
        
        self.assertRaisesMessage(AttributeError, "'str' object attribute 'center' is read-only", s.__setattr__, "center", "foo")
        
        self.assertRaisesMessage(AttributeError, "'str' object attribute 'center' is read-only", s.__delattr__, "center")
        
        self.assertRaises(TypeError, getattr, object(), None)

    def test_access_checks(self):
        """test attribute access checks"""
        class OldStyleClass:
            classAttribute = 1
            def __init__(self):
                self.instanceAttribute = 2
        
        class C(object):
            classAttribute = 1
            def __init__(self):
                self.instanceAttribute = 2
        
        def del_class(c):
            del c.__class__
        
        def del_doc(c):
            del c.__doc__
        
        def del_module(c):
            del c.__module__
        
        def del_classAttribute(c):
            del c.classAttribute
        
        def del_instanceAttribute(c):
            del c.instanceAttribute
        
        def del_nonExistantAttribute(c):
            del c.nonExistantAttribute
        
        def attr_access(c):
            self.assertRaises(TypeError, del_class, c)
            self.assertRaises(AttributeError, del_doc, c)
            self.assertRaises(AttributeError, del_module, c)
            self.assertRaises(AttributeError, del_classAttribute, c)
            del c.instanceAttribute
            self.assertRaises(AttributeError, del_instanceAttribute, c)
            self.assertRaises(AttributeError, del_nonExistantAttribute, c)
        
            klass = c.__class__
            del klass.classAttribute
            self.assertRaises(AttributeError, del_classAttribute, klass)
            self.assertRaises(AttributeError, del_nonExistantAttribute, klass)
        
        attr_access(OldStyleClass())
        attr_access(C())

    #TODO: @skip("multiple_execute")
    def test_cp13686(self):
        with path_modifier(self.test_dir) as p:
            import toimport
            import sys
            mod_list = [toimport, sys]
            if is_posix:
                import posix
                mod_list.append(posix)
            else:
                import nt
                mod_list.append(nt)
            
            mod_names = {}
            for mod in mod_list:
                mod_names[mod] = mod.__name__
            
            for mod in mod_list:
                self.assertRaises(AttributeError, getattr, mod, "name")
                setattr(mod, "name", "xyz")
                self.assertEqual(getattr(mod, "name"), "xyz")
                
                self.assertEqual(getattr(mod, "__name__"), mod_names[mod])
                setattr(mod, "__name__", "badname")
                self.assertEqual(getattr(mod, "__name__"), "badname")
            
            if is_cli:
                import System
                self.assertRaises(AttributeError, setattr, System, "name", "xyz")

    @unittest.skipUnless(is_cli, 'IronPython specific test') # 2.6 feature
    def test_hasattr_sys_exit(self):
        # hasattr shouldn't swallow SystemExit exceptions.
        class x(object):
            def __getattr__(self, name):
                import sys
                sys.exit(1)
        
        a = x()
        try:
            hasattr(a, 'abc')
            self.fail('Should not reach this point')
        except SystemExit:
            pass

run_test(__name__)
