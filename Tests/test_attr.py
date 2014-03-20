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

import sys
me = sys.modules[__name__]

def test_delattr():
    class C(object):
        pass
    
    y = C.__delattr__
    y = C().__delattr__
    y = object.__delattr__
    C.a = 10
    Assert(C.a == 10)
    del C.a
    success=0
    try:
        y = C.a
    except AttributeError:
        success = 1
    Assert(success == 1)

######################################################################################

# Non-string attributes on a module

def test_non_string_attrs():
    def CheckObjectKeys(d):
        AreEqual(d.has_key(1), True)
        AreEqual(repr(d).__contains__("1: '1'"), True)
    
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
        AreEqual(mod.newModuleAttr, 'xyz')
    
        CheckDictionary(mod.__dict__)
    
        mod.__dict__[1] = '1'
        AreEqual(dir(mod).__contains__(1), True)
        del mod.__dict__[1]
        
        # Try to replace __dict__
        AssertErrorWithMessage(TypeError, "readonly attribute", SetDictionary, mod, dict(mod.__dict__))
    
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

# CP#34257: __getattr__ on a module subclass

def test_ModuleType_getattr():
    from types import ModuleType
    class ApiModule(ModuleType):
        def __init__(self, name="", importspec="", implprefix=None, attr=None):
            pass
        def __makeattr(self, name):
            return name
        __getattr__ = __makeattr

    t = ApiModule()
    AreEqual(t.Std, 'Std')

##########################################################################
# Decorators starting with Bug #993
def test_decorators():
    def f(x='default'): return x
    
    cm = classmethod(f)
    sm = staticmethod(f)
    p = property(f)
    AreEqual(f.__get__(1)(), 1)
    AreEqual(str(f.__get__(2)), "<bound method ?.f of 2>")
    AreEqual(str(f.__get__(2, list)), "<bound method list.f of 2>")
    
    AreEqual(cm.__get__(1)(), int)
    AreEqual(str(cm.__get__(2)), "<bound method type.f of <type 'int'>>")
    
    AreEqual(sm.__get__(1)(), 'default')
    AreEqual(p.__get__(1), 1)

######################################################################################
# __getattribute__, __setattr__, __delattr__ on builtins
@skip("multiple_execute")
def test_meta_attrs():
    if is_cli or is_silverlight:
        import System
        dateTime = System.DateTime()
    
        AreEqual(dateTime.ToString, dateTime.__getattribute__("ToString"))
        AssertErrorWithMessage(AttributeError, "attribute 'ToString' of 'DateTime' object is read-only", dateTime.__setattr__, "ToString", "foo")
        AssertErrorWithMessage(AttributeError, "attribute 'ToString' of 'DateTime' object is read-only", dateTime.__delattr__, "ToString")
    
        arrayList = System.Collections.Generic.List[int]()
        arrayList.__setattr__("Capacity", 123)
        AreEqual(arrayList.Capacity, 123)
    
    AreEqual(me.__file__, me.__getattribute__("__file__"))
    me.__setattr__("__file__", "foo")
    AreEqual(me.__file__, "foo")
    me.__delattr__("__file__")
    
    class C(object):
        def foo(self): pass
    
    # C.foo is "unbound method" on IronPython but "function" on CPython
    if is_cli or is_silverlight:
        AreEqual(C.foo, C.__getattribute__(C, "foo"))
    else:
        AreEqual(C.foo.im_func, C.__getattribute__(C, "foo"))
    AreEqual(C.__doc__, C.__getattribute__(C, "__doc__"))
    
    # fancy type.__doc__ access...
    x = type.__dict__['__doc__'].__get__

    class C(object): __doc__ = 'foo'

    class D(object): pass

    AreEqual(x(D, None), None)
    AreEqual(x(C, None), 'foo')

    class C(object): __doc__ = 42

    AreEqual(x(C, None), 42)
    
    AssertErrorWithMessage(TypeError, "can't apply this __setattr__ to type object", C.__setattr__, C, "__str__", "foo")
    AssertErrorWithMessage(TypeError, "can't apply this __delattr__ to type object", C.__delattr__, C, "__str__")
    
    s = "hello"
    
    AreEqual(s.center, s.__getattribute__("center"))
    
    AssertErrorWithMessages(AttributeError, "attribute 'center' of 'str' object is read-only",
                                            "'str' object attribute 'center' is read-only", s.__setattr__, "center", "foo")
    
    AssertErrorWithMessages(AttributeError, "attribute 'center' of 'str' object is read-only",
                                            "'str' object attribute 'center' is read-only", s.__delattr__, "center")
    
    AssertError(TypeError, getattr, object(), None)

##########################################################################
# test attribute access checks

def test_access_checks():
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
        AssertError(TypeError, del_class, c)
        AssertError(AttributeError, del_doc, c)
        AssertError(AttributeError, del_module, c)
        AssertError(AttributeError, del_classAttribute, c)
        del c.instanceAttribute
        AssertError(AttributeError, del_instanceAttribute, c)
        AssertError(AttributeError, del_nonExistantAttribute, c)
    
        klass = c.__class__
        del klass.classAttribute
        AssertError(AttributeError, del_classAttribute, klass)
        AssertError(AttributeError, del_nonExistantAttribute, klass)
    
    attr_access(OldStyleClass())
    attr_access(C())

@skip("silverlight", "multiple_execute")
def test_cp13686():
    import toimport
    import sys
    import nt
    mod_list = [toimport, sys, nt]
    
    mod_names = {}
    for mod in mod_list:
        mod_names[mod] = mod.__name__
    
    for mod in mod_list:
        AssertError(AttributeError, getattr, mod, "name")
        setattr(mod, "name", "xyz")
        AreEqual(getattr(mod, "name"), "xyz")
        
        AreEqual(getattr(mod, "__name__"), mod_names[mod])
        setattr(mod, "__name__", "badname")
        AreEqual(getattr(mod, "__name__"), "badname")
    
    if is_cli:
        import System
        AssertError(AttributeError, setattr, System, "name", "xyz")

@skip("win32") # 2.6 feature
def test_hasattr_sys_exit():
    # hasattr shouldn't swallow SystemExit exceptions.
    class x(object):
        def __getattr__(self, name):
            import sys
            sys.exit(1)
    
    a = x()
    try:
        hasattr(a, 'abc')
        AssertUnreachable()
    except SystemExit:
        pass

run_test(__name__)
