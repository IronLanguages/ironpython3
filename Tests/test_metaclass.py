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

# ref: http://docs.python.org/ref/metaclasses.html

class Old:
    def method(self): return 10

class New(object):
    def method(self): return 10

def g_f_modify(new_base=None, new_name=None):
    def f_modify(name, bases, dict):
        if new_name:
            name = new_name
        if new_base:
            bases = new_base + bases
            
        dict['version'] = 2.4
        return type(name, bases, dict)
        
    return f_modify

def g_c_modify(new_base=None, new_name=None):
    class c_modify(type):
        def __new__(cls, name, bases, dict):
            if new_name:
                name = new_name
            if new_base:
                bases = new_base + bases
                
            dict['version'] = 2.4
            return super(c_modify, cls).__new__(cls, name, bases, dict)
            
    return c_modify

# Modifying the class dictionary prior to the class being created.
def test_modify():
    def _check(T):
        x = T()
        AreEqual(x.version, 2.4)
        AreEqual(x.method(), 10)
        AreEqual(x.__class__.__name__, "D")
    
    for f in [ g_f_modify, g_c_modify ]:
        class C(object, metaclass=f((New,), "D")):
            pass
        _check(C)
        
        class C(metaclass=f((New,), "D")):
            pass
        _check(C)
        
        class C(object, metaclass=f((Old,), "D")):
            pass
        _check(C)

        try:
            class C(metaclass=f((Old,), "D")): pass
        except TypeError: pass
        else: Fail("Should have thrown")

class dash_attributes(type):
    def __new__(metaclass, name, bases, dict):
        new_dict = {}
        for key, val in dict.items():
            new_key = key[0].lower()
            
            for x in key[1:]:
                if not x.islower():
                    new_key += "_" + x.lower()
                else:
                    new_key += x
                
            new_dict[new_key] = val
        
        return super(dash_attributes, metaclass).__new__(metaclass, name, bases, new_dict)

def test_dash_attribute():
    class C(object, metaclass=dash_attributes):
        def WriteLine(self, *arg): return 4
    
    x = C()
    Assert(not hasattr(C, "WriteLine"))
    AreEqual(x.write_line(), 4)

def test_basic():
    def try_metaclass(t):
        class C(object, metaclass=t):
            def method(self): return 10
        
        x = C()
        AreEqual(x.method(), 10)
    
    try_metaclass(type)
    #try_metaclass(type(Old))  # bug 364938
    try_metaclass(dash_attributes)
    try_metaclass(sub_type1)
    
    ## subclassing
    class C1(object, metaclass=g_c_modify()):
        pass
    class C2(metaclass=g_f_modify()):
        pass

    # not defining __metaclass__
    for C in [C1, C2]:
        class D(C): pass
        
        Assert(hasattr(D, "version"))
        AreEqual(D().version, 2.4)
    
    # redefining __metaclass__
    try:
        class D(C1, metaclass=dash_attributes): pass
    except TypeError: pass
    else: Fail("metaclass conflict expected")
    
    class D(C2, metaclass=dash_attributes):
        def StartSomethingToday(self): pass
        
    Assert(hasattr(D, "version"))
    Assert(hasattr(D, "start_something_today"))

def test_find_metaclass():
    class A1: pass
    class A2(object): pass
    AreEqual(A2.__class__, type)
    
    class B1(metaclass=dash_attributes):
        pass
    class B2(object, metaclass=dash_attributes):
        pass

    global __metaclass__
    __metaclass__ = lambda *args: 100

    class C1:
        def __metaclass__(*args): return 200
    AreEqual(C1, 200)

    class C2(object):
        def __metaclass__(*args): return 200
    AreEqual(C2, 200)
    
    class D1: pass
    AreEqual(D1, 100)
    
    class D2(object): pass
    AreEqual(D2.__class__, type)

    # base order: how to see the effect of the order???
    for x in [
                A1,
                #A2,        # bug 364991
             ]:
        for y in [B1, B2]:
            class E(x, y):
                def PythonMethod(self): pass
            Assert(hasattr(E, "python_method"))

            class E(y, x):
                def PythonMethod(self): pass
            Assert(hasattr(E, "python_method"))
    
    del __metaclass__
    
    class F1: pass
    Assert(F1 != 100)


global flag  # to track which __metaclass__'es get invoked
flag = 0

class sub_type1(type):
    def __new__(cls, name, bases, dict):
        global flag
        flag += 1
        return super(sub_type1, cls).__new__(cls, name, bases, dict)

class sub_type2(type):
    def __new__(cls, name, bases, dict):
        global flag
        flag += 10
        return super(sub_type2, cls).__new__(cls, name, bases, dict)

class sub_type3(sub_type2): # subclass
    def __new__(cls, name, bases, dict):
        global flag
        flag += 100
        return super(sub_type3, cls).__new__(cls, name, bases, dict)
        
def test_conflict():
    global flag
    
    class C1(object): pass
    class C2(object, metaclass=sub_type1):
        pass
    class C3(object, metaclass=sub_type2):
        pass
    class C4(object, metaclass=sub_type3):
        pass
    
    flag = 0
    class D(C1, C2): pass
    #AreEqual(flag, 1)   # bug 364991
    flag = 0
    class D(C2, C1): pass
    #AreEqual(flag, 1)
    flag = 0
    class D(C3, C4): pass  # C4 derive from C3
    #AreEqual(flag, 120)
    flag = 0
    class D(C3, C1, C4): pass
    #AreEqual(flag, 120)
    flag = 0
    class D(C4, C1): pass
    #AreEqual(flag, 110)
    
    def f1():
        class D(C2, C3): pass
    def f2():
        class D(C1, C2, C3): pass
    def f3():
        class D(C2, C1, C3): pass
    
    for f in [
            f1,
            #f2,   # bug 364991
            f3,
        ]:
        AssertError(TypeError, f)
        
def test_bad_choices():
    def create(x):
        class C(object, metaclass=x):
            pass
    
    for x in [
                #None,   # bug 364967
                1,
                [],
                lambda name, bases, dict, extra: 1,
                lambda name, bases: 1,
                Old,
                New,
             ]:
        AssertError(TypeError, create, x)

# copied from test_class.py
def test_metaclass_call_override():
    """overriding __call__ on a metaclass should work"""
    class mytype(type):
        def __call__(self, *args):
            return args
    
    class myclass(object, metaclass=mytype):
        pass
        
    AreEqual(myclass(1,2,3), (1,2,3))
    
def test_metaclass():
    global __metaclass__, recvArgs
    
    # verify we can use a function as a metaclass in the dictionary
    recvArgs = None
    def funcMeta(*args):
        global recvArgs
        recvArgs = args
        
    class foo(metaclass=funcMeta):
        pass
    
    AreEqual(recvArgs, ('foo', (), {'__module__' : __name__, '__metaclass__' : funcMeta}))
    
    class foo(object, metaclass=funcMeta):
        pass
    
    AreEqual(recvArgs, ('foo', (object, ), {'__module__' : __name__, '__metaclass__' : funcMeta}))
            
    
    # verify setting __metaclass__ to default old-style type works
    
    class classType: pass
    classType = type(classType)     # get classObj for tests
    __metaclass__ = classType
    class c: pass
    AreEqual(type(c), classType)
    del(__metaclass__)
    
    
    # verify setting __metaclass__ to default new-style type works
    __metaclass__ = type
    class c: pass
    AreEqual(type(c), type)
    del(__metaclass__)
    
    # try setting it a different way - by getting it from a type
    class c(object): pass
    __metaclass__  = type(c)
    class xyz: pass
    AreEqual(type(xyz), type(c))
    del(__metaclass__)
    
    # verify setting __metaclass__ at module scope to a function works
    __metaclass__ = funcMeta
    recvArgs = None
    class foo: pass
    AreEqual(recvArgs, ('foo', (), {'__module__' : __name__}))  # note no __metaclass__ becauses its not in our dict
    
    # clean up __metaclass__ for other tests
    del(__metaclass__)

def test_arguments():
    class MetaType(type):
        def __init__(cls, name, bases, dict):
            super(MetaType, cls).__init__(name, bases, dict)

    class Base(object, metaclass=MetaType):
        pass
    
      
    class A(Base):
        def __init__(self, a, b='b', c='12', d='', e=''):
            self.val = a + b + c + d + e

    a = A('hello')
    AreEqual(a.val, 'hellob12')

    b = ('there',)
    a = A('hello', *b)
    AreEqual(a.val, 'hellothere12')
    
    c = ['42','23']
    a = A('hello', *c)
    AreEqual(a.val, 'hello4223')
    
    x = ()
    y = {'d': 'boom'}
    a = A('hello', *x, **y)
    AreEqual(a.val, 'hellob12boom')
    
def test_getattr_optimized():
    class Meta(type):
        def __getattr__(self, attr):
            if attr == 'b':
                return 'b'
            raise AttributeError(attr)

    class A(metaclass=Meta):
        pass

    for i in range(110):
        AreEqual('A', A.__name__) # after 100 iterations: see https://github.com/IronLanguages/main/issues/1269
        AreEqual('b', A.b)
    
run_test(__name__)
