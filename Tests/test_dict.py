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

#Regression: CodePlex 15715
#Do not move or remove these two lines
x = dir(dict)
x = dir(dict.fromkeys)


from iptest.assert_util import *

import operator
import collections

def test_sanity():
    items = 0
    
    d = {'key1': 'value1', 'key2': 'value2'}
    for key, value in d.items():
        items += 1
        Assert((key, value) == ('key1', 'value1') or (key,value) == ('key2', 'value2'))

    Assert(items == 2)

    Assert(d["key1"] == "value1")
    Assert(d["key2"] == "value2")

    def getitem(d,k):
        d[k]

    AssertError(KeyError, getitem, d, "key3")

    x = d.get("key3")
    Assert(x == None)
    Assert(d["key1"] == d.get("key1"))
    Assert(d["key2"] == d.get("key2"))
    Assert(d.get("key3", "value3") == "value3")

    AssertError(KeyError, getitem, d, "key3")
    Assert(d.setdefault("key3") == None)
    Assert(d.setdefault("key4", "value4") == "value4")
    Assert(d["key3"] == None)
    Assert(d["key4"] == "value4")


    d2= dict(key1 = 'value1', key2 = 'value2')
    Assert(d2['key1'] == 'value1')


#--inherit from a dictionary---------------------------------------------------
def test_dict_inherit():
    class MyDict(dict):
        def __setitem__(self, *args):
                super(MyDict, self).__setitem__(*args)

    a = MyDict()
    a[0] = 'abc'
    AreEqual(a[0], 'abc')
    a[None] = 3
    AreEqual(a[None], 3)


    class MyDict(dict):
        def __setitem__(self, *args):
            dict.__setitem__(self, *args)

    a = MyDict()
    a[0] = 'abc'
    AreEqual(a[0], 'abc')
    a[None] = 3
    AreEqual(a[None], 3)

#------------------------------------------------------------------------------
# verify function environments, FieldIdDict,
# custom old class dict, and module environments
# all local identical to normal dictionaries

def test_function_environments():

    x = {}

    class C: pass

    AreEqual(dir(x), dir(C.__dict__))

    class C:
        xx = 'abc'
        yy = 'def'
        pass

    AreEqual(dir(x), dir(C.__dict__))

    class C:
        x0 = 'abc'
        x1 = 'def'
        x2 = 'aaa'
        x3 = 'aaa'
        pass

    AreEqual(dir(x), dir(C.__dict__))

    class C:
        x0 = 'abc'
        x1 = 'def'
        x2 = 'aaa'
        x3 = 'aaa'
        x4 = 'abc'
        x5 = 'def'
        x6 = 'aaa'
        x7 = 'aaa'
        x0 = 'abc'
        pass

    AreEqual(dir(x), dir(C.__dict__))

    class C:
        x0 = 'abc'
        x1 = 'def'
        x2 = 'aaa'
        x3 = 'aaa'
        x4 = 'abc'
        x5 = 'def'
        x6 = 'aaa'
        x7 = 'aaa'
        x0 = 'abc'
        x10 = 'abc'
        x11 = 'def'
        x12 = 'aaa'
        x13 = 'aaa'
        x14 = 'abc'
        x15 = 'def'
        x16 = 'aaa'
        x17 = 'aaa'
        x10 = 'abc'
        pass

    AreEqual(dir(x), dir(C.__dict__))


    class C:
        x0 = 'abc'
        x1 = 'def'
        x2 = 'aaa'
        x3 = 'aaa'
        x4 = 'abc'
        x5 = 'def'
        x6 = 'aaa'
        x7 = 'aaa'
        x0 = 'abc'
        x10 = 'abc'
        x11 = 'def'
        x12 = 'aaa'
        x13 = 'aaa'
        x14 = 'abc'
        x15 = 'def'
        x16 = 'aaa'
        x17 = 'aaa'
        x10 = 'abc'
        x20 = 'abc'
        x21 = 'def'
        x22 = 'aaa'
        x23 = 'aaa'
        x24 = 'abc'
        x25 = 'def'
        x26 = 'aaa'
        x27 = 'aaa'
        x20 = 'abc'
        x110 = 'abc'
        x111 = 'def'
        x112 = 'aaa'
        x113 = 'aaa'
        x114 = 'abc'
        x115 = 'def'
        x116 = 'aaa'
        x117 = 'aaa'
        x110 = 'abc'
        pass

    AreEqual(dir(x), dir(C.__dict__))


    a = C()
    AreEqual(dir(x), dir(a.__dict__))
    
    a = C()
    a.abc = 'def'
    a.ghi = 'def'
    AreEqual(dir(x), dir(a.__dict__))
    
    if is_cli:
        # cpython does not have __dict__ at the module level?
        #AreEqual(dir(x), dir(__dict__))
        pass

#####################################################################
## coverage for CustomFieldIdDict
def contains(d, *attrs):
    for attr in attrs:
        Assert(attr in d, "didn't find " + str(attr) + " in " + repr(d))
        Assert(d.__contains__(attr), "didn't find " + str(attr) + " in " + repr(d))


def repeat_on_class(C):
    newStyle = "__class__" in dir(C)

    c = C()
    d = C.__dict__
    contains(d, '__doc__', 'x1', 'f1')
    
    ## recursive entries & repr
    C.abc = d
    if not newStyle:
        x = repr(d) # shouldn't stack overflow
    else:
        x = str(d)
    
    Assert(x.find("'abc'") != -1)
    if not newStyle:
        Assert(x.find("{...}") != -1)
    else:    
        Assert(x.find("'abc': <dictproxy object at") != -1)
    del C.abc

    keys, values = list(d.keys()), list(d.values())
    AreEqual(len(keys), len(values))
    contains(keys, '__doc__', 'x1', 'f1')
    
    ## initial length
    l = len(d)
    Assert(l > 3)
    
    # add more attributes
    def f2(self): return 22
    def f3(self): return 33
    
    if not newStyle:
        d['f2'] = f2
        d['x2'] = 20
    
        AreEqual(len(d), l + 2)
        AreEqual(d.__len__(), l + 2)
    
    if not newStyle:
        contains(d, '__doc__', 'x1', 'x2', 'f1', 'f2')
        contains(list(d.keys()), '__doc__', 'x1', 'x2', 'f1', 'f2')
    else:
        contains(d, '__doc__', 'x1', 'f1')
        contains(list(d.keys()), '__doc__', 'x1', 'f1')
        
    AreEqual(d['x1'], 10)
    if not newStyle:
        AreEqual(d['x2'], 20)
    AreEqual(d['f1'](c), 11)
    if not newStyle:
        AreEqual(d['f2'](c), 22)
    AssertError(KeyError, lambda : d['x3'])
    AssertError(KeyError, lambda : d['f3'])
    
    ## get
    AreEqual(d.get('x1'), 10)
    if not newStyle: 
        AreEqual(d.get('x2'), 20)
    AreEqual(d.get('f1')(c), 11)
    if not newStyle:
        AreEqual(d.get('f2')(c), 22)
    
    AreEqual(d.get('x3'), None)
    AreEqual(d.get('x3', 30), 30)
    AreEqual(d.get('f3'), None)
    AreEqual(d.get('f3', f3)(c), 33)
    
    if not newStyle:
        ## setdefault
        AreEqual(d.setdefault('x1'), 10)
        AreEqual(d.setdefault('x1', 30), 10)
        AreEqual(d.setdefault('f1')(c), 11)
        AreEqual(d.setdefault('f1', f3)(c), 11)
        AreEqual(d.setdefault('x2'), 20)
        AreEqual(d.setdefault('x2', 30), 20)
        AreEqual(d.setdefault('f2')(c), 22)
        AreEqual(d.setdefault('f2', f3)(c), 22)
        AreEqual(d.setdefault('x3', 30), 30)
        AreEqual(d.setdefault('f3', f3)(c), 33)
    
    if not newStyle:
        ## pop
        l1 = len(d)
        AreEqual(d.pop('x1', 30), 10)
        AreEqual(len(d), l1-1)
        l1 = len(d)
        AreEqual(d.pop('x2', 30), 20)
        AreEqual(len(d), l1-1)
        l1 = len(d)
        AreEqual(d.pop("xx", 70), 70)
        AreEqual(len(d), l1)
    
    ## has_key
    Assert('f1' in d)
    if not newStyle:
        Assert('f2' in d)
        Assert('f3' in d)
    Assert(('fx' in d) == False)
    
    # subclassing, overriding __getitem__, and passing to
    # eval
    dictType = type(d)
    
    try:
        class newDict(dictType):
            def __getitem__(self, key):
                if key == 'abc':
                    return 'def'
                return super(self, dictType).__getitem__(key)
    except TypeError as ex:
        if not newStyle:
            Assert(ex.message.find('cannot derive from sealed or value types') != -1, ex.message)
        else:
            Assert(ex.message.find('Error when calling the metaclass bases') != -1, ex.message)
    else:
        try:
            nd = newDict()
        except TypeError as e:
            if sys.platform == 'cli':
                import clr
                if clr.GetClrType(dictType).ToString() == 'IronPython.Runtime.Types.NamespaceDictionary':
                    Fail("Error! Threw TypeError when creating newDict deriving from NamespaceDictionary")
        else:
            AreEqual(eval('abc', {}, nd), 'def')
    
    ############### IN THIS POINT, d LOOKS LIKE ###############
    ##  {'f1': f1, 'f2': f2, 'f3': f3, 'x3': 30, '__doc__': 'This is comment', '__module__': '??'}

    ## iteritems
    lk = []
    for (k, v) in d.items():
        lk.append(k)
        exp = None
        if k == 'f1': exp = 11
        elif k == 'f2': exp == 22
        elif k == 'f3': exp == 33
        
        if exp != None:
            AreEqual(v(c), exp)
    
    if not newStyle:
        contains(lk, 'f1', 'f2', 'f3', 'x3', '__doc__')
    else:
        contains(lk, 'f1', '__module__', '__dict__', 'x1', '__weakref__', '__doc__')
        
    # iterkeys
    lk = []
    for k in d.keys():
        lk.append(k)
    
    if not newStyle:
        contains(lk, 'f1', 'f2', 'f3', 'x3', '__doc__')
    else:
        contains(lk, 'f1', '__module__', '__dict__', 'x1', '__weakref__', '__doc__')
    
    # itervalues
    for v in d.values():
        if isinstance(v, collections.Callable):
            exp = v(c)
            Assert(exp in [11, 22, 33])
        elif v is str:
            Assert(v == 'This is comment')
        elif v is int:
            Assert(v == 30)
        
    if not newStyle:        
        ## something fun before destorying it
        l1 = len(d)
        d[dict] = 3    # object as key
        AreEqual(len(d), l1+1)
    
        l1 = len(d)
        d[int] = 4     # object as key
        if is_cli or is_silverlight:
            print("CodePlex 16811")
            return
        AreEqual(len(d), l1+1)
    
        l1 = len(d)
        del d[int]
        AreEqual(len(d), l1-1)
    
        l1 = len(d)
        del d[dict]
        AreEqual(len(d), l1-1)
    
        l1 = len(d)
        del d['x3']
        AreEqual(len(d), l1-1)
    
        l1 = len(d)
        d.popitem()
        AreEqual(len(d), l1-1)
    
        ## object as key
        d[int] = int
        d[str] = "str"
    
        AreEqual(d[int], int)
        AreEqual(d[str], "str")
    
        d.clear()
        AreEqual(len(d), 0)
        AreEqual(d.__len__(), 0)


#------------------------------------------------------------------------------        
def test_customfieldiddict_old():
    class C:
        '''This is comment'''
        x1 = 10
        def f1(self): return 11
    repeat_on_class(C)

def test_customfieldiddict_new():    
    class C(object):
        '''This is comment'''
        x1 = 10
        def f1(self): return 11
    repeat_on_class(C)

#------------------------------------------------------------------------------
def test_customfieldiddict_fromkeys():
    def new_repeat_on_class(C):
        d1 = C.__dict__
        l1 = len(d1)
        d2 = dict.fromkeys(d1)
        l2 = len(d2)
        AreEqual(l1, l2)
        AreEqual(d2['x'], None)
        AreEqual(d2['f'], None)
    
        d2 = dict.fromkeys(d1, 10)
        l2 = len(d2)
        AreEqual(l1, l2)
        AreEqual(d2['x'], 10)
        AreEqual(d2['f'], 10)
        
    class C:
        x = 10
        def f(self): pass
    new_repeat_on_class(C)
    
    class C(object):
        x = 10
        def f(self): pass
    new_repeat_on_class(C)
    
#------------------------------------------------------------------------------
def test_customfieldiddict_compare():
    def new_repeat_on_class(C1, C2):
        d1 = C1.__dict__
        d2 = C2.__dict__
            
        # object as key
        d1[int] = int
        d2[int] = int
        Assert(d1 != d2)
    
        d2['f'] = d1['f']
        Assert([x for x in d1] == [x for x in d2])
    
        Assert(d1.fromkeys([x for x in d1]) >= d2.fromkeys([x for x in d2]))
        Assert(d1.fromkeys([x for x in d1]) <= d2.fromkeys([x for x in d2]))
    
        d1['y'] = 20
        d1[int] = int
    
        Assert(d1.fromkeys([x for x in d1]) > d2.fromkeys([x for x in d2]))
        Assert(d1.fromkeys([x for x in d1]) >= d2.fromkeys([x for x in d2]))
        Assert(d2.fromkeys([x for x in d2]) < d1.fromkeys([x for x in d1]))
        Assert(d2.fromkeys([x for x in d2]) <= d1.fromkeys([x for x in d1]))
    
    class C1:
        x = 10
        def f(self): pass
    class C2:
        x = 10
        def f(self): pass
    
    new_repeat_on_class(C1, C2)
    
    def t_func():
        class C1(object):
            x = 10
            def f(self): pass
        C1.__dict__[1] = 2    
            
    AssertError(TypeError, t_func)

@skip("win32")
def test_dict_to_idict():
    """verify dicts can be converted to IDictionaries"""
    load_iron_python_test()
    from IronPythonTest import DictConversion
    class MyDict(dict): pass
    class KOld: pass
    class KNew(object): pass
    class KOldDerived(KOld): pass
    class KNewDerived(KNew): pass

    test_dicts = [
                    {},
                    {1:100},
                    {None:None},
                    {object:object},
                    {1:100, 2:200},
                    {1:100, 2:200, 3:300, 4:400},
                    MyDict.__dict__,
                    KOld.__dict__,
                    KNew.__dict__, 
                    KOldDerived.__dict__,
                    KNewDerived.__dict__,
                    ]
    
    for temp_dict in test_dicts:
        expected = list(temp_dict.keys()) + list(temp_dict.values())
        expected.sort()
        
        to_idict = list(DictConversion.ToIDictionary(temp_dict))
        to_idict.sort()
        AreEqual(to_idict, expected)
        
        to_idict = list(DictConversion.ToIDictionary(MyDict(temp_dict)))
        to_idict.sort()
        AreEqual(to_idict, expected)
        

#####################################################################
## coverage for FieldIdDict

def test_fieldiddict():

    def func(): pass

    d = func.__dict__

    d['x1'] = 10
    d['f1'] = lambda : 11
    d[int]  = "int"
    d[dict] = {2:20}

    keys, values = list(d.keys()), list(d.values())
    AreEqual(len(keys), len(values))
    contains(keys, 'x1', 'f1', int, dict)

    ## initial length
    l = len(d)
    Assert(l == 4)

    # add more attributes
    d['x2'] = 20
    d['f2'] = lambda x: 22

    AreEqual(len(d), l + 2)
    AreEqual(d.__len__(), l + 2)

    contains(d, 'x1', 'x2', 'f1', 'f2', int, dict)
    contains(list(d.keys()), 'x1', 'x2', 'f1', 'f2', int, dict)

    AreEqual(d['x1'], 10)
    AreEqual(d['x2'], 20)
    AreEqual(d['f1'](), 11)
    AreEqual(d['f2'](9), 22)
    AssertError(KeyError, lambda : d['x3'])
    AssertError(KeyError, lambda : d['f3'])
    
    ## get
    AreEqual(d.get('x1'), 10)
    AreEqual(d.get('x2'), 20)
    AreEqual(d.get('f1')(), 11)
    AreEqual(d.get('f2')(1), 22)

    def f3(): return 33

    AreEqual(d.get('x3'), None)
    AreEqual(d.get('x3', 30), 30)
    AreEqual(d.get('f3'), None)
    AreEqual(d.get('f3', f3)(), 33)
    
    ## setdefault
    AreEqual(d.setdefault('x1'), 10)
    AreEqual(d.setdefault('x1', 30), 10)
    AreEqual(d.setdefault('f1')(), 11)
    AreEqual(d.setdefault('f1', f3)(), 11)
    AreEqual(d.setdefault('x2'), 20)
    AreEqual(d.setdefault('x2', 30), 20)
    AreEqual(d.setdefault('f2')(1), 22)
    AreEqual(d.setdefault('f2', f3)(1), 22)
    AreEqual(d.setdefault('x3', 30), 30)
    AreEqual(d.setdefault('f3', f3)(), 33)
    
    ## pop
    l1 = len(d); AreEqual(d.pop('x1', 30), 10)
    AreEqual(len(d), l1-1)
    l1 = len(d); AreEqual(d.pop('x2', 30), 20)
    AreEqual(len(d), l1-1)
    l1 = len(d); AreEqual(d.pop(int, 70), "int")
    AreEqual(len(d), l1-1)
    l1 = len(d); AreEqual(d.pop("xx", 70), 70)
    AreEqual(len(d), l1)
    
    ## has_key
    Assert('f1' in d)
    Assert('f2' in d)
    Assert('f3' in d)
    Assert(dict in d)
    Assert(('fx' in d) == False)

    ############### IN THIS POINT, d LOOKS LIKE ###############
    # f1, f2, f3, x3, dict as keys

    ## iteritems
    lk = []
    for (k, v) in d.items():
        lk.append(k)
        if k == 'f1': AreEqual(v(), 11)
        elif k == 'f2': AreEqual(v(1), 22)
        elif k == 'f3': AreEqual(v(), 33)
        elif k == 'x3': AreEqual(v, 30)
        elif k == dict: AreEqual(v, {2:20})

    contains(lk, 'f1', 'f2', 'f3', 'x3', dict)

    # iterkeys
    lk = []
    for k in d.keys():
        lk.append(k)

    contains(lk, 'f1', 'f2', 'f3', 'x3', dict)

    # itervalues
    for v in d.values():
        if isinstance(v, collections.Callable):
            try: exp = v(1)
            except: pass
            try: exp = v()
            except: pass
            Assert(exp in [11, 22, 33])
        elif v is dict:
            Assert(v == {2:20})
        elif v is int:
            Assert(v == 30)
            
    ## something fun before destorying it
    l1 = len(d); d[int] = 4     # object as key
    AreEqual(len(d), l1+1)

    l1 = len(d); del d[int]
    AreEqual(len(d), l1-1)
    
    l1 = len(d); del d[dict]
    AreEqual(len(d), l1-1)
    
    l1 = len(d); del d['x3']
    AreEqual(len(d), l1-1)
    
    l1 = len(d); popped_item = d.popitem()
    AreEqual(len(d), l1-1)
    
    ## object as key
    d[int] = int
    d[str] = "str"

    AreEqual(d[int], int)
    AreEqual(d[str], "str")

    d.clear()
    AreEqual(len(d), 0)
    AreEqual(d.__len__(), 0)

    d[int] = int
    AreEqual(len(d), 1)


    ## comparison
    def func1(): pass
    def func2(): pass
    
    d1 = func1.__dict__
    d2 = func2.__dict__
    
    d1['x'] = 10
    d2['x'] = 30
    d1[int] = int
    d2[int] = int
    
    # object as key
    Assert(d1 != d2)
    
    d2['x'] = 10
    Assert(d1 == d2)
    
    Assert(d1 >= d2)
    Assert(d1 <= d2)
    
    d1['y'] = 20
    d1[dict] = "int"
    
    Assert(d1 > d2)
    Assert(d1 >= d2)
    Assert(d2 < d1)
    Assert(d2 <= d1)

#####################################################################

# subclassing dict, overriding __init__
def test_subclass_dict_override__init__():

    class foo(dict):
        def __init__(self, abc):
            self.abc = abc
        
    a = foo('abc')
    AreEqual(a.abc, 'abc')

    # make sure dict.__init__ works

    a = {}
    a.__init__({'abc':'def'})
    AreEqual(a, {'abc':'def'})
    a.__init__({'abcd':'defg'})
    AreEqual(a, {'abc':'def', 'abcd':'defg'})

    # keyword arg contruction
    
    # single kw-arg, should go into dict
    a = dict(b=2)
    AreEqual(a, {'b':2})
    
    # dict value to init, Plus kw-arg
    a = dict({'a':3}, b=2)
    AreEqual(a, {'a':3, 'b':2})

    # more than one
    a = dict({'a':3}, b=2, c=5)
    AreEqual(a, {'a':3, 'b':2, 'c':5})
    
    try:
        dict({'a':3}, {'b':2}, c=5)
        AssertUnreachable()
    except TypeError: pass

#####################################################################

def test_DictionaryUnionEnumerator():
    if is_cli == False:
        return

    class C(object): pass
    c = C()
    d = c.__dict__
    import System

    # Check empty enumerator
    e = System.Collections.IDictionary.GetEnumerator(d)
    AssertError(SystemError, getattr, e, "Key")
    AreEqual(e.MoveNext(), False)
    AssertError(SystemError, getattr, e, "Key")
    
    # Add non-string attribute
    d[1] = 100
    e = System.Collections.IDictionary.GetEnumerator(d)

    AssertError(SystemError, getattr, e, "Key")
    AreEqual(e.MoveNext(), True)
    AreEqual(e.Key, 1)
    AreEqual(e.MoveNext(), False)
    AssertError(SystemError, getattr, e, "Key")
    
    # Add string attribute
    c.attr = 100
    e = System.Collections.IDictionary.GetEnumerator(d)
    AssertError(SystemError, getattr, e, "Key")
    AreEqual(e.MoveNext(), True)
    key1 = e.Key
    AreEqual(e.MoveNext(), True)
    key2 = e.Key
    AreEqual((key1, key2) == (1, "attr") or (key1, key2) == ("attr", 1), True)
    AreEqual(e.MoveNext(), False)
    AssertError(SystemError, getattr, e, "Key")
    
    # Remove non-string attribute
    del d[1]
    e = System.Collections.IDictionary.GetEnumerator(d)
    AssertError(SystemError, getattr, e, "Key")
    AreEqual(e.MoveNext(), True)
    AreEqual(e.Key, "attr")
    AreEqual(e.MoveNext(), False)
    AssertError(SystemError, getattr, e, "Key")
    
    # Remove string attribute and check empty enumerator
    del c.attr
    e = System.Collections.IDictionary.GetEnumerator(d)
    AssertError(SystemError, getattr, e, "Key")
    AreEqual(e.MoveNext(), False)
    AssertError(SystemError, getattr, e, "Key")
    
def test_same_but_different():
    """Test case checks that when two values who are logically different but share hash code & equality
    result in only a single entry"""
    
    AreEqual({-10:0, -10:1}, {-10:1})

#####################################################################
def test_module_dict():
    me = sys.modules[__name__]
    moduleDict = me.__dict__
    AreEqual(isinstance(moduleDict, collections.Mapping), True)
    AreEqual(moduleDict.__contains__("test_module_dict"), True)
    AreEqual(moduleDict["test_module_dict"], test_module_dict)
    AreEqual(list(moduleDict.keys()).__contains__("test_module_dict"), True)

def test_eval_locals_simple():
    class Locals(dict):
        def __getitem__(self, key):
            try:
                return dict.__getitem__(self, key)
            except KeyError as e:
                return 'abc'
    
    locs = Locals()
    AreEqual(eval("unknownvariable", globals(), locs), 'abc')


def test_key_error():
    class c: pass
    class d(object): pass
    
    
    for key in ['abc', 1, c(), d(), 1.0, 1]:
        try:
            {}[key]
        except KeyError as e:
            AreEqual(e.args[0], key)
        
        try:
            del {}[key]
        except KeyError as e:
            AreEqual(e.args[0], key)
            
        try:
            set([]).remove(key)
        except KeyError as e:
            AreEqual(e.args[0], key)

def test_contains():
    class ContainsDict(dict):
        was_called = False
        def __contains__(self, key):
            ContainsDict.was_called = True
            return dict.__contains__(self, key)

    md = ContainsDict()
    md["stuff"] = 1
    
    AreEqual(ContainsDict.was_called, False)
    AreEqual("nothing" in md, False)
    AreEqual("stuff" in md, True)
    AreEqual(ContainsDict.was_called, True)


def test_stdtypes_dict():
    temp_types = [  int,
                    float,
                    complex,
                    bool,
                    bytes,
                    str,
                    list,
                    tuple,
                    range,
                    dict,
                    set,
                    frozenset,
                    type,
                    object,
                ] #+ [eval("types." + x) for x in dir(types) if x.endswith("Type")]
    
    temp_keys = [ None, -1, 0, 1, 2.34, "", "None", int, object, test_stdtypes_dict, [], (None,)]
    
    for temp_type in temp_types:
        for temp_key in temp_keys:
            def tFunc(): temp_type.__dict__[temp_key] = 0
            AssertError(TypeError, tFunc)
    

@skip("silverlight")
def test_main_dict():
    import __main__
    #just make sure this doesn't throw...
    t_list = []
    for w in __main__.__dict__: t_list.append(w)
    
    t_list.sort()
    g_list = list(globals().keys())
    g_list.sort()
    AreEqual(t_list, g_list)
    

def test_update():
    test_cases = (
        #N changes with an empty dict
        ({}, (), {}, {}),
        ({}, ({'k':'v'},), {}, {'k':'v'}),
        ({}, (), {'k':'v'}, {'k':'v'}),
        ({}, ({'k':'v', 'x':'y'},), {}, {'k':'v', 'x':'y'}),
        ({}, (), {'k':'v', 'x':'y'}, {'k':'v', 'x':'y'}),
        ({}, ({'k':'v'},), {'x':'y'}, {'k':'v', 'x':'y'}),

        #N changes with one pre-existing dict element
        ({'a':'b'}, (), {}, {'a':'b'}),
        ({'a':'b'}, ({'k':'v'},), {}, {'a':'b', 'k':'v'}),
        ({'a':'b'}, (), {'k':'v'}, {'a':'b', 'k':'v'}),
        ({'a':'b'}, ({'k':'v', 'x':'y'},), {}, {'a':'b', 'k':'v', 'x':'y'}),
        ({'a':'b'}, (), {'k':'v', 'x':'y'}, {'a':'b', 'k':'v', 'x':'y'}),
        ({'a':'b'}, ({'k':'v'},), {'x':'y'}, {'a':'b', 'k':'v', 'x':'y'}),
    
        #N changes with one pre-existing dict element
        ({'a':'b', 'c':'d'}, (), {}, {'a':'b', 'c':'d'}),
        ({'a':'b', 'c':'d'}, ({'k':'v'},), {}, {'a':'b', 'c':'d', 'k':'v'}),
        ({'a':'b', 'c':'d'}, (), {'k':'v'}, {'a':'b', 'c':'d', 'k':'v'}),
        ({'a':'b', 'c':'d'}, ({'k':'v', 'x':'y'},), {}, {'a':'b', 'c':'d', 'k':'v', 'x':'y'}),
        ({'a':'b', 'c':'d'}, (), {'k':'v', 'x':'y'}, {'a':'b', 'c':'d', 'k':'v', 'x':'y'}),
        ({'a':'b', 'c':'d'}, ({'k':'v'},), {'x':'y'}, {'a':'b', 'c':'d', 'k':'v', 'x':'y'}),
    )
    
    for start_dict, dict_param, kw_params, expected in test_cases:
        try:
            start_dict.update(*dict_param, **kw_params)
        except Exception as e:
            print("ERROR:", start_dict, ".update(*", dict_param, ", **", kw_params, ") failed!")
            raise e
        
        AreEqual(start_dict, expected)

def test_update_argnames():
    expected = {"b": 1}
    result = {}
    result.update(b=1)

    AreEqual(result, expected)

    expected = {"other": 1}
    result = {}
    result.update(other=1)

    AreEqual(result, expected)

    expected = {"other": 1, "otherArgs": 2}
    result = {}
    result.update({"other": 1}, otherArgs=2)

    AreEqual(result, expected)

def test_update_no_setitem():
    # update doesn't call __setitem__
    class mydict(dict):
        def __init__(self, *args, **kwargs):
            dict.__init__(self, *args, **kwargs)
            self.setcalled = False
        def __setitem__(self, index, value):
            self.setcalled = True
            raise Exception()
    
    d = mydict()
    d.update(mydict(abc=2))
    AreEqual(d.setcalled, False)
    
    d.update({'foo': 2})
    AreEqual(d.setcalled, False)
    
def test_keys_not_as_property():
    def f():
        mapping = { 10: 10}
        for k in mapping.keys: pass

    AssertErrorWithMessages(TypeError,
            "iteration over non-sequence of type builtin_function_or_method",
            "'builtin_function_or_method' object is not iterable",
            f)

def test_dict_class_dictionary():
    class KOld:
        KLASS_MEMBER = 3.14
        def aFunc(): pass
        def aMethod(self): pass
        
    class KNew(object):
        KLASS_MEMBER = 3.14
        def aFunc(): pass
        def aMethod(self): pass
        
        
    for K in [KOld, KNew]:
        temp_dict = dict(K.__dict__)
        
        #class member has the correct value?
        AreEqual(K.__dict__["KLASS_MEMBER"], 3.14)
        AreEqual(temp_dict["KLASS_MEMBER"], 3.14)
        
        #methods show up?
        for func_name in ["aFunc", "aMethod"]:
            Assert(func_name in list(K.__dict__.keys()))
            Assert(func_name in list(temp_dict.keys()))
        
    expected_keys = [   '__module__', 'KLASS_MEMBER', 'aFunc', 'aMethod',
                        '__dict__',
                        '__weakref__', '__doc__']
    for expected_key in expected_keys:
        Assert(expected_key in KNew.__dict__, expected_key)
        Assert(expected_key in temp_dict, expected_key)
        

def test_cp15882():
    x = {}
    
    #negative cases
    for bad_stuff in [
                        [1],
                        {}, {1:1}, {(1,2): 1},
                        ]:
        try:
            x[bad_stuff] = 1
            Fail(str(bad_stuff) + " is unhashable")
        except TypeError:
            AreEqual(x, {})
    
    
    #positive cases
    for stuff in [
                    (), (None),
                    (-1), (0), (1), (2),
                    (1, 2), (1, 2, 3),
                    range(3), 1j, object, test_cp15882,
                    (range(3)), (1j), (object), (test_cp15882),
                    (()), ((())),
                    ]:
        for i in range(2):
            x[stuff] = 1
            AreEqual(x[stuff], 1)
            del x[stuff]
            AreEqual(x, {})
            AssertError(KeyError, x.__delitem__, stuff)
    
        for i in range(2):
            x[stuff] = 1
            AreEqual(x[stuff], 1)
            x.__delitem__(stuff)
            AreEqual(x, {})
            AssertError(KeyError, x.__delitem__, stuff)
            

def test_comparison_operators():
    x = {2:3}
    y = {2:4}
    for oper in ('__lt__', '__gt__', '__le__', '__ge__'):
        for data in (y, None, 1, 1.0, 1, (), [], 1j, "abc"):
            AreEqual(getattr(x, oper)(data), NotImplemented)

def test_cp16519():
    __main__ = __import__(__name__)
    __main__.Dict = {"1": "a"}
    AreEqual(__main__.Dict["1"], "a")
    del __main__.Dict
    
    import sys
    sys.Dict = {"1": "b"}
    AreEqual(sys.Dict["1"], "b")
    del sys.Dict

    import testpkg1
    testpkg1.Dict = {"1": "c"}
    AreEqual(testpkg1.Dict["1"], "c")
    del testpkg1.Dict

def test_dict_equality_lookup():
    """dictionaries check object equality before running normal equality"""
    class x(object):
        def __eq__(self, other):
                return False
        def __ne__(self, other):
                return True
    
    a = x()
    d = {}
    d[a] = 42
    AreEqual(d[a], 42)

def test_missing():
    class Foo(dict):
        def __missing__(self, key):
            raise TypeError('Foo.__missing__ should not be called')
    
    f = Foo()
    
    AreEqual(f.setdefault(1, 2), 2)
    AreEqual(f.get(2), None)
    AreEqual(f.get(2, 3), 3)
    AssertError(KeyError, f.pop, 3)
    AreEqual(f.pop(3, 4), 4)
    
    x = {2:3}
    for f in (Foo({'abc':3}), Foo()):
        Assert(x != f)
        Assert(f != x)
        
        AreEqual(x.__eq__(f), False)
        AreEqual(f.__eq__(x), False)
    

def test_cp29914():
	AreEqual(dict(o=42), {'o':42})

def test_dict_comp():
    pass
    
def test_dict_comp():
    AreEqual({locals()['x'] : locals()['x'] for x in (2,3,4)}, {2:2, 3:3, 4:4})
    
    x = 100
    {x:x for x in (2,3,4)}
    AreEqual(x, 100)
    
    class C:
        {x:x for x in (2,3,4)}
    
    AreEqual(hasattr(C, 'x'), False)
    
    class C:
        abc = {locals()['x']:locals()['x'] for x in (2,3,4)}
    
    AreEqual(C.abc, {2:2,3:3,4:4})

    d = {}
    exec(compile("abc = {locals()['x']:locals()['x'] for x in (2,3,4)}", 'exec', 'exec'), d, d)
    AreEqual(d['abc'], {2:2,3:3,4:4})
    
    d = {'y':42}
    exec(compile("abc = {y:y for x in (2,3,4)}", 'exec', 'exec'), d, d)
    AreEqual(d['abc'], {42:42})

    d = {'y':42, 't':(2,3,42)}
    exec(compile("abc = {y:y for x in t if x == y}", 'exec', 'exec'), d, d)
    AreEqual(d['abc'], {42:42})

    t = (2,3,4)
    v = 2
    abc = {v:v for x in t}
    AreEqual(abc, {2:2})

    abc = {x:x for x in t if x == v}
    AreEqual(abc, {2:2})
    
    def f():
        abc = {x:x for x in t if x == v}
        AreEqual(abc, {2:2})
        
    f()
    
    def f():
        abc = {v:v for x in t}
        AreEqual(abc, {2:2})
        
        
    class C:
        abc = {v:v for x in t}
        AreEqual(abc, {2:2})
        
    class C:
        abc = {x:x for x in t if x == v}
        AreEqual(abc, {2:2})

def test_cp32527():
    '''test for duplicate key in dict under specific hash value conditions'''
    d = {'1': 1, '2': 1, '3': 1, 'a7': 1, 'a8': 1}
    #d now has 7 buckets internally, and computed hash for a7 and a8 keys will land on same starting bucket index
    
    #recycle the a7 bucket
    d.pop('a7')
    
    #attempt to update the a8 bucket, which now comes after the recycled a7
    d['a8'] = 5
    
    #if working properly, there will now be a recycled bucket (former home of a7) and a single a8 bucket
    #if not working properly, there will instead be two a8 buckets
    expected = 1
    actual = list(d.keys()).count('a8')
    AreEqual(actual, expected)


run_test(__name__)
