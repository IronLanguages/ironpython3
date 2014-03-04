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

global flag

def new_classes():
    class C1: pass
    class C2(object): pass
    
    return C1, C2

# used as handler for __getattr__ and __getattribute__
def return_100(self, name):        return 100
def throw_attribute_error(self, name): raise AttributeError
def throw_assertion_error(self, name): raise AssertionError

def test_getattr_alone():
    C1, C2 = new_classes()
    
    def __init__(self): self.x = 10
    def plus_100(self, name):
        if self.__class__ == C2:
            pass
    
    for C in [C1, C2]:
        C.__init__ = __init__
        C.y = 20
        c = C()
        
        C.__getattr__ = return_100
        AreEqual([c.x, c.y, c.z], [10, 20, 100])
        
        def access_z(): return c.z
        
        C.__getattr__ = throw_attribute_error
        AreEqual([c.x, c.y], [10, 20])
        
        AssertError(AttributeError, access_z)
        
        C.__getattr__ = throw_assertion_error
        AssertError(AssertionError, access_z)
        
        C.__getattr__ = lambda self, name: self.x + 200  # access attribute inside
        AreEqual([c.x, c.y, c.z], [10, 20, 210])
        
        del C.__getattr__
        AssertError(AttributeError, access_z)
        
def test_setattr_alone():
    global flag
    
    C1, C2 = new_classes()
    
    def f(self): self.x = 10
    def simply_record(self, name, value): global flag; flag = "%s %s" % (name, value)
    def simply_throw(self, name, value):  raise AssertionError
    def add_10_via_dict(self, name, value):
        self.__dict__[name] = value + 10
        
    def add_20_via_object(self, name, value):
        if self.__class__ == C2:
            object.__setattr__(self, name, value + 20)
        if self.__class__ == C1:
            self.__dict__[name] = value + 20
    
    for C in [C1, C2]:
        C.set_something = f
        
        c = C()
        c.x = 0
        C.__setattr__ = simply_record
        flag = 0
        c.set_something()
        AreEqual(flag, "x 10")
        AreEqual(c.x, 0)  # unchanged
        
        c.y = 20
        AreEqual(flag, "y 20")
        AssertError(AttributeError, lambda: c.y)
        
        C.__setattr__ = simply_throw
        AssertError(AssertionError, c.set_something)  # even if c.x already exists

        C.z = 30   # ok: class variable
        AreEqual(c.z, 30)
        
        C.__setattr__ = add_10_via_dict
        c.set_something()
        AreEqual(c.x, 20)
        
        C.__setattr__ = add_20_via_object
        c.u = 50
        AreEqual(c.u, 70)
        
        del C.__setattr__
        
        c.z = 40
        AreEqual([c.z, C.z], [40, 30])

def test_delattr_only():
    C1, C2 = new_classes()
    # low pri


@disabled("bug 365168")
def test_negative1():
    class C:
        def __setattr__(self, name, value):
            object.__setattr__(self, name, value)

    try:  C().x = 1
    except TypeError: pass
    else: Fail("should have thrown: can't apply this __setattr__ to instance object")
    
    class C:
        def __getattr__(self, name):
            object.__getattribute__(self, name)
    
    AssertErrorWithMessage(AttributeError, "'instance' object has no attribute 'x'", lambda: C().x)

def test_bad_signatures():
    C1, C2 = new_classes()
    
    def bad1(self): pass
    def bad2(self, x): pass
    def bad3(self, x, y): pass
    def bad4(self, x, y, z): pass
    
    for C in [C1, C2]:
        c = C()
        
        def f(): c.x = 1
        
        for bad_for_get in [bad1, bad3]:
            C.__getattr__ = bad_for_get
            AssertError(TypeError, lambda: c.x)
        
        for bad_for_set in [bad2, bad4]:
            C.__setattr__ = bad_for_set
            AssertError(TypeError, f)

    for bad_for_getattribute in [bad1, bad3]:
        C2.__getattribute__ = bad_for_getattribute
        AssertError(TypeError, lambda: c.x)

def test_getattribute_only():
    class C:
        def __getattribute__(self, name):
            return 10
    c = C()
    AssertError(AttributeError, lambda: c.x)   # __getattribute__ only works for new-style
    
    class C(object):
        def set_y(self): self.y = 30
            
    c = C()
    f = c.set_y
    
    c.x = 10
    C.__getattribute__ = return_100
    AreEqual(100, c.x)
    
    c.x = 20
    
    def plus_100(self, name):
        try:
            return object.__getattribute__(self, name) + 100
        except AttributeError:
            return 200

    C.__getattribute__ = plus_100
    AreEqual(120, c.x)
    f()
    AreEqual(130, c.y)
    AreEqual(200, c.z)
    
    C.__getattribute__ = throw_attribute_error
    AssertError(AttributeError, lambda: c.x)

    C.__getattribute__ = throw_assertion_error
    AssertError(AssertionError, lambda: c.x)
    
    del C.__getattribute__
    AreEqual(c.x, 20)
    AreEqual(c.y, 30)
    AssertError(AttributeError, lambda: c.z)

def test_getattr_and_getattribute_together():
    class C(object): pass

    c = C()
    C.__getattr__ = lambda *args: 20
    
    C.__getattribute__ = lambda *args: 30
    AreEqual(c.x, 30)

    C.__getattribute__ = throw_attribute_error
    AreEqual(c.x, 20)

    C.__getattribute__ = throw_assertion_error
    AssertError(AssertionError, lambda: c.x)

    C.__getattribute__ = lambda *args: C.__getattr__(*args)
    AreEqual(c.x, 20)

def test_subclassing():
    C1, C2 = new_classes()
    
    ## new style
    class D(C2): pass
    d = D()
    d.x = 10
    
    C2.__getattr__ = return_100
    AreEqual(d.y, 100)
    del C2.__getattr__
    
    def f(self, name, value): self.__dict__[name] = value + 10
    C2.__setattr__ = f
    d.x = 20
    AreEqual(d.x, 30)
    del C2.__setattr__
    
    C2.__getattribute__ = return_100
    #AreEqual(d.x, 100)  # bug 365242
    
    ## old style
    class D(C1): pass
    d = D()
    C1.__getattr__ = return_100
    #AssertError(AttributeError, lambda: d.y)  # (no?) dynamism for old style, bug 365266
    
    class D(C1): pass
    d = D()
    d.x = 10
    AreEqual([d.x, d.y], [10, 100])
    
    C1.__setattr__ = f
    class D(C1): pass
    d = D()
    d.x = 20
    AreEqual([d.x, d.y], [30, 100])
    
    C1.__getattribute__ = lambda *args: 200  # __getattribute__ not honored
    class D(C1): pass
    d = D()
    AreEqual([d.x, d.y], [100, 100])

@disabled("bug 369042")
def test_delete_getattribute():
    class B(object):
        def __getattribute__(self, name): pass
    class D(B): pass
    
    def f(): del D.__getattribute__
    AssertError(AttributeError, f)

run_test(__name__)
