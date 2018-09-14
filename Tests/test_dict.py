# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

#Regression: CodePlex 15715
#Do not move or remove these two lines
x = dir(dict)
x = dir(dict.fromkeys)

import operator
import os
import unittest
import sys

from iptest import IronPythonTestCase, is_cli, path_modifier, run_test, source_root

class DictTest(IronPythonTestCase):
    def test_sanity(self):
        items = 0
        
        d = {'key1': 'value1', 'key2': 'value2'}
        for key, value in d.iteritems():
            items += 1
            self.assertTrue((key, value) == ('key1', 'value1') or (key,value) == ('key2', 'value2'))

        self.assertTrue(items == 2)

        self.assertTrue(d["key1"] == "value1")
        self.assertTrue(d["key2"] == "value2")

        def getitem(d,k):
            d[k]

        self.assertRaises(KeyError, getitem, d, "key3")

        x = d.get("key3")
        self.assertTrue(x == None)
        self.assertTrue(d["key1"] == d.get("key1"))
        self.assertTrue(d["key2"] == d.get("key2"))
        self.assertTrue(d.get("key3", "value3") == "value3")

        self.assertRaises(KeyError, getitem, d, "key3")
        self.assertTrue(d.setdefault("key3") == None)
        self.assertTrue(d.setdefault("key4", "value4") == "value4")
        self.assertTrue(d["key3"] == None)
        self.assertTrue(d["key4"] == "value4")


        d2= dict(key1 = 'value1', key2 = 'value2')
        self.assertTrue(d2['key1'] == 'value1')


    def test_dict_inherit(self):
        class MyDict(dict):
            def __setitem__(self, *args):
                    super(MyDict, self).__setitem__(*args)

        a = MyDict()
        a[0] = 'abc'
        self.assertEqual(a[0], 'abc')
        a[None] = 3
        self.assertEqual(a[None], 3)


        class MyDict(dict):
            def __setitem__(self, *args):
                dict.__setitem__(self, *args)

        a = MyDict()
        a[0] = 'abc'
        self.assertEqual(a[0], 'abc')
        a[None] = 3
        self.assertEqual(a[None], 3)

    def test_function_environments(self):
        """verify function environments, FieldIdDict, custom old class dict, and module environments all local identical to normal dictionaries"""
        x = {}

        class C: pass

        self.assertEqual(dir(x), dir(C.__dict__))

        class C:
            xx = 'abc'
            yy = 'def'
            pass

        self.assertEqual(dir(x), dir(C.__dict__))

        class C:
            x0 = 'abc'
            x1 = 'def'
            x2 = 'aaa'
            x3 = 'aaa'
            pass

        self.assertEqual(dir(x), dir(C.__dict__))

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

        self.assertEqual(dir(x), dir(C.__dict__))

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

        self.assertEqual(dir(x), dir(C.__dict__))


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

        self.assertEqual(dir(x), dir(C.__dict__))


        a = C()
        self.assertEqual(dir(x), dir(a.__dict__))
        
        a = C()
        a.abc = 'def'
        a.ghi = 'def'
        self.assertEqual(dir(x), dir(a.__dict__))
        
        if is_cli:
            # cpython does not have __dict__ at the module level?
            #self.assertEqual(dir(x), dir(__dict__))
            pass

#####################################################################
## coverage for CustomFieldIdDict
    def contains(self, d, *attrs):
        for attr in attrs:
            self.assertTrue(attr in d, "didn't find " + str(attr) + " in " + repr(d))
            self.assertTrue(d.__contains__(attr), "didn't find " + str(attr) + " in " + repr(d))


    def repeat_on_class(self, C):
        newStyle = "__class__" in dir(C)

        c = C()
        d = C.__dict__
        self.contains(d, '__doc__', 'x1', 'f1')
        
        ## recursive entries & repr
        C.abc = d
        if not newStyle:
            x = repr(d) # shouldn't stack overflow
        else:
            x = str(d)
        
        self.assertTrue(x.find("'abc'") != -1)
        if not newStyle:
            self.assertTrue(x.find("{...}") != -1)
        else:    
            self.assertTrue(x.find("'abc': <dictproxy object at") != -1)
        del C.abc

        keys, values = d.keys(), d.values()
        self.assertEqual(len(keys), len(values))
        self.contains(keys, '__doc__', 'x1', 'f1')
        
        ## initial length
        l = len(d)
        self.assertTrue(l > 3)
        
        # add more attributes
        def f2(self): return 22
        def f3(self): return 33
        
        if not newStyle:
            d['f2'] = f2
            d['x2'] = 20
        
            self.assertEqual(len(d), l + 2)
            self.assertEqual(d.__len__(), l + 2)
        
        if not newStyle:
            self.contains(d, '__doc__', 'x1', 'x2', 'f1', 'f2')
            self.contains(d.keys(), '__doc__', 'x1', 'x2', 'f1', 'f2')
        else:
            self.contains(d, '__doc__', 'x1', 'f1')
            self.contains(d.keys(), '__doc__', 'x1', 'f1')
            
        self.assertEqual(d['x1'], 10)
        if not newStyle:
            self.assertEqual(d['x2'], 20)
        self.assertEqual(d['f1'](c), 11)
        if not newStyle:
            self.assertEqual(d['f2'](c), 22)
        self.assertRaises(KeyError, lambda : d['x3'])
        self.assertRaises(KeyError, lambda : d['f3'])
        
        ## get
        self.assertEqual(d.get('x1'), 10)
        if not newStyle: 
            self.assertEqual(d.get('x2'), 20)
        self.assertEqual(d.get('f1')(c), 11)
        if not newStyle:
            self.assertEqual(d.get('f2')(c), 22)
        
        self.assertEqual(d.get('x3'), None)
        self.assertEqual(d.get('x3', 30), 30)
        self.assertEqual(d.get('f3'), None)
        self.assertEqual(d.get('f3', f3)(c), 33)
        
        if not newStyle:
            ## setdefault
            self.assertEqual(d.setdefault('x1'), 10)
            self.assertEqual(d.setdefault('x1', 30), 10)
            self.assertEqual(d.setdefault('f1')(c), 11)
            self.assertEqual(d.setdefault('f1', f3)(c), 11)
            self.assertEqual(d.setdefault('x2'), 20)
            self.assertEqual(d.setdefault('x2', 30), 20)
            self.assertEqual(d.setdefault('f2')(c), 22)
            self.assertEqual(d.setdefault('f2', f3)(c), 22)
            self.assertEqual(d.setdefault('x3', 30), 30)
            self.assertEqual(d.setdefault('f3', f3)(c), 33)
        
        if not newStyle:
            ## pop
            l1 = len(d)
            self.assertEqual(d.pop('x1', 30), 10)
            self.assertEqual(len(d), l1-1)
            l1 = len(d)
            self.assertEqual(d.pop('x2', 30), 20)
            self.assertEqual(len(d), l1-1)
            l1 = len(d)
            self.assertEqual(d.pop("xx", 70), 70)
            self.assertEqual(len(d), l1)
        
        ## has_key
        self.assertTrue(d.has_key('f1'))
        if not newStyle:
            self.assertTrue(d.has_key('f2'))
            self.assertTrue(d.has_key('f3'))
        self.assertTrue(d.has_key('fx') == False)
        
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
                self.assertTrue(ex.message.find('cannot derive from sealed or value types') != -1, ex.message)
            else:
                self.assertTrue(ex.message.find('Error when calling the metaclass bases') != -1, ex.message)
        else:
            try:
                nd = newDict()
            except TypeError as e:
                if sys.implementation.name == "ironpython":
                    import clr
                    if clr.GetClrType(dictType).ToString() == 'IronPython.Runtime.Types.NamespaceDictionary':
                        self.fail("Error! Threw TypeError when creating newDict deriving from NamespaceDictionary")
            else:
                self.assertEqual(eval('abc', {}, nd), 'def')
        
        ############### IN THIS POINT, d LOOKS LIKE ###############
        ##  {'f1': f1, 'f2': f2, 'f3': f3, 'x3': 30, '__doc__': 'This is comment', '__module__': '??'}

        ## iteritems
        lk = []
        for (k, v) in d.iteritems():
            lk.append(k)
            exp = None
            if k == 'f1': exp = 11
            elif k == 'f2': exp == 22
            elif k == 'f3': exp == 33
            
            if exp <> None:
                self.assertEqual(v(c), exp)
        
        if not newStyle:
            self.contains(lk, 'f1', 'f2', 'f3', 'x3', '__doc__')
        else:
            self.contains(lk, 'f1', '__module__', '__dict__', 'x1', '__weakref__', '__doc__')
            
        # iterkeys
        lk = []
        for k in d.iterkeys():
            lk.append(k)
        
        if not newStyle:
            self.contains(lk, 'f1', 'f2', 'f3', 'x3', '__doc__')
        else:
            self.contains(lk, 'f1', '__module__', '__dict__', 'x1', '__weakref__', '__doc__')
        
        # itervalues
        for v in d.itervalues():
            if callable(v):
                exp = v(c)
                self.assertTrue(exp in [11, 22, 33])
            elif v is str:
                self.assertTrue(v == 'This is comment')
            elif v is int:
                self.assertTrue(v == 30)
            
        if not newStyle:
            ## something fun before destorying it
            l1 = len(d)
            d[dict] = 3    # object as key
            self.assertEqual(len(d), l1+1)
        
            l1 = len(d)
            d[int] = 4     # object as key
            if is_cli:
                print "CodePlex 16811"
                return
            self.assertEqual(len(d), l1+1)
        
            l1 = len(d)
            del d[int]
            self.assertEqual(len(d), l1-1)
        
            l1 = len(d)
            del d[dict]
            self.assertEqual(len(d), l1-1)
        
            l1 = len(d)
            del d['x3']
            self.assertEqual(len(d), l1-1)
        
            l1 = len(d)
            d.popitem()
            self.assertEqual(len(d), l1-1)
        
            ## object as key
            d[int] = int
            d[str] = "str"
        
            self.assertEqual(d[int], int)
            self.assertEqual(d[str], "str")
        
            d.clear()
            self.assertEqual(len(d), 0)
            self.assertEqual(d.__len__(), 0)


    def test_customfieldiddict_old(self):
        class C:
            '''This is comment'''
            x1 = 10
            def f1(self): return 11
        self.repeat_on_class(C)

    def test_customfieldiddict_new(self):
        class C(object):
            '''This is comment'''
            x1 = 10
            def f1(self): return 11
        self.repeat_on_class(C)

    def test_customfieldiddict_fromkeys(self):
        def new_repeat_on_class(C):
            d1 = C.__dict__
            l1 = len(d1)
            d2 = dict.fromkeys(d1)
            l2 = len(d2)
            self.assertEqual(l1, l2)
            self.assertEqual(d2['x'], None)
            self.assertEqual(d2['f'], None)
        
            d2 = dict.fromkeys(d1, 10)
            l2 = len(d2)
            self.assertEqual(l1, l2)
            self.assertEqual(d2['x'], 10)
            self.assertEqual(d2['f'], 10)
            
        class C:
            x = 10
            def f(self): pass
        new_repeat_on_class(C)
        
        class C(object):
            x = 10
            def f(self): pass
        new_repeat_on_class(C)
    
    def test_customfieldiddict_compare(self):
        def new_repeat_on_class(C1, C2):
            d1 = C1.__dict__
            d2 = C2.__dict__
                
            # object as key
            d1[int] = int
            d2[int] = int
            self.assertTrue(d1 <> d2)
        
            d2['f'] = d1['f']
            self.assertTrue([x for x in d1] == [x for x in d2])
        
            self.assertTrue(d1.fromkeys([x for x in d1]) >= d2.fromkeys([x for x in d2]))
            self.assertTrue(d1.fromkeys([x for x in d1]) <= d2.fromkeys([x for x in d2]))
        
            d1['y'] = 20
            d1[int] = int
        
            self.assertTrue(d1.fromkeys([x for x in d1]) > d2.fromkeys([x for x in d2]))
            self.assertTrue(d1.fromkeys([x for x in d1]) >= d2.fromkeys([x for x in d2]))
            self.assertTrue(d2.fromkeys([x for x in d2]) < d1.fromkeys([x for x in d1]))
            self.assertTrue(d2.fromkeys([x for x in d2]) <= d1.fromkeys([x for x in d1]))
        
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
                
        self.assertRaises(TypeError, t_func)

    @unittest.skipUnless(is_cli, 'IronPython specific test')
    def test_dict_to_idict(self):
        """verify dicts can be converted to IDictionaries"""
        self.load_iron_python_test()
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
            expected = temp_dict.keys() + temp_dict.values()
            expected.sort()
            
            to_idict = list(DictConversion.ToIDictionary(temp_dict))
            to_idict.sort()
            self.assertEqual(to_idict, expected)
            
            to_idict = list(DictConversion.ToIDictionary(MyDict(temp_dict)))
            to_idict.sort()
            self.assertEqual(to_idict, expected)
        

    def test_fieldiddict(self):
        """coverage for FieldIdDict"""

        def func(): pass

        d = func.__dict__

        d['x1'] = 10
        d['f1'] = lambda : 11
        d[int]  = "int"
        d[dict] = {2:20}

        keys, values = d.keys(), d.values()
        self.assertEqual(len(keys), len(values))
        self.contains(keys, 'x1', 'f1', int, dict)

        ## initial length
        l = len(d)
        self.assertTrue(l == 4)

        # add more attributes
        d['x2'] = 20
        d['f2'] = lambda x: 22

        self.assertEqual(len(d), l + 2)
        self.assertEqual(d.__len__(), l + 2)

        self.contains(d, 'x1', 'x2', 'f1', 'f2', int, dict)
        self.contains(d.keys(), 'x1', 'x2', 'f1', 'f2', int, dict)

        self.assertEqual(d['x1'], 10)
        self.assertEqual(d['x2'], 20)
        self.assertEqual(d['f1'](), 11)
        self.assertEqual(d['f2'](9), 22)
        self.assertRaises(KeyError, lambda : d['x3'])
        self.assertRaises(KeyError, lambda : d['f3'])
        
        ## get
        self.assertEqual(d.get('x1'), 10)
        self.assertEqual(d.get('x2'), 20)
        self.assertEqual(d.get('f1')(), 11)
        self.assertEqual(d.get('f2')(1), 22)

        def f3(): return 33

        self.assertEqual(d.get('x3'), None)
        self.assertEqual(d.get('x3', 30), 30)
        self.assertEqual(d.get('f3'), None)
        self.assertEqual(d.get('f3', f3)(), 33)
        
        ## setdefault
        self.assertEqual(d.setdefault('x1'), 10)
        self.assertEqual(d.setdefault('x1', 30), 10)
        self.assertEqual(d.setdefault('f1')(), 11)
        self.assertEqual(d.setdefault('f1', f3)(), 11)
        self.assertEqual(d.setdefault('x2'), 20)
        self.assertEqual(d.setdefault('x2', 30), 20)
        self.assertEqual(d.setdefault('f2')(1), 22)
        self.assertEqual(d.setdefault('f2', f3)(1), 22)
        self.assertEqual(d.setdefault('x3', 30), 30)
        self.assertEqual(d.setdefault('f3', f3)(), 33)
        
        ## pop
        l1 = len(d); self.assertEqual(d.pop('x1', 30), 10)
        self.assertEqual(len(d), l1-1)
        l1 = len(d); self.assertEqual(d.pop('x2', 30), 20)
        self.assertEqual(len(d), l1-1)
        l1 = len(d); self.assertEqual(d.pop(int, 70), "int")
        self.assertEqual(len(d), l1-1)
        l1 = len(d); self.assertEqual(d.pop("xx", 70), 70)
        self.assertEqual(len(d), l1)
        
        ## has_key
        self.assertTrue(d.has_key('f1'))
        self.assertTrue(d.has_key('f2'))
        self.assertTrue(d.has_key('f3'))
        self.assertTrue(d.has_key(dict))
        self.assertTrue(d.has_key('fx') == False)

        ############### IN THIS POINT, d LOOKS LIKE ###############
        # f1, f2, f3, x3, dict as keys

        ## iteritems
        lk = []
        for (k, v) in d.iteritems():
            lk.append(k)
            if k == 'f1': self.assertEqual(v(), 11)
            elif k == 'f2': self.assertEqual(v(1), 22)
            elif k == 'f3': self.assertEqual(v(), 33)
            elif k == 'x3': self.assertEqual(v, 30)
            elif k == dict: self.assertEqual(v, {2:20})

        self.contains(lk, 'f1', 'f2', 'f3', 'x3', dict)

        # iterkeys
        lk = []
        for k in d.iterkeys():
            lk.append(k)

        self.contains(lk, 'f1', 'f2', 'f3', 'x3', dict)

        # itervalues
        for v in d.itervalues():
            if callable(v):
                try: exp = v(1)
                except: pass
                try: exp = v()
                except: pass
                self.assertTrue(exp in [11, 22, 33])
            elif v is dict:
                self.assertTrue(v == {2:20})
            elif v is int:
                self.assertTrue(v == 30)
                
        ## something fun before destorying it
        l1 = len(d); d[int] = 4     # object as key
        self.assertEqual(len(d), l1+1)

        l1 = len(d); del d[int]
        self.assertEqual(len(d), l1-1)
        
        l1 = len(d); del d[dict]
        self.assertEqual(len(d), l1-1)
        
        l1 = len(d); del d['x3']
        self.assertEqual(len(d), l1-1)
        
        l1 = len(d); popped_item = d.popitem()
        self.assertEqual(len(d), l1-1)
        
        ## object as key
        d[int] = int
        d[str] = "str"

        self.assertEqual(d[int], int)
        self.assertEqual(d[str], "str")

        d.clear()
        self.assertEqual(len(d), 0)
        self.assertEqual(d.__len__(), 0)

        d[int] = int
        self.assertEqual(len(d), 1)


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
        self.assertTrue(d1 <> d2)
        
        d2['x'] = 10
        self.assertTrue(d1 == d2)
        
        self.assertTrue(d1 >= d2)
        self.assertTrue(d1 <= d2)
        
        d1['y'] = 20
        d1[dict] = "int"
        
        self.assertTrue(d1 > d2)
        self.assertTrue(d1 >= d2)
        self.assertTrue(d2 < d1)
        self.assertTrue(d2 <= d1)


    def test_subclass_dict_override__init__(self):
        """subclassing dict, overriding __init__"""

        class foo(dict):
            def __init__(self, abc):
                self.abc = abc
            
        a = foo('abc')
        self.assertEqual(a.abc, 'abc')

        # make sure dict.__init__ works

        a = {}
        a.__init__({'abc':'def'})
        self.assertEqual(a, {'abc':'def'})
        a.__init__({'abcd':'defg'})
        self.assertEqual(a, {'abc':'def', 'abcd':'defg'})

        # keyword arg contruction
        
        # single kw-arg, should go into dict
        a = dict(b=2)
        self.assertEqual(a, {'b':2})
        
        # dict value to init, Plus kw-arg
        a = dict({'a':3}, b=2)
        self.assertEqual(a, {'a':3, 'b':2})

        # more than one
        a = dict({'a':3}, b=2, c=5)
        self.assertEqual(a, {'a':3, 'b':2, 'c':5})
        
        try:
            dict({'a':3}, {'b':2}, c=5)
            self.fail('Should not reach this code')
        except TypeError: pass


    @unittest.skipUnless(is_cli, 'IronPython specific test')
    def test_DictionaryUnionEnumerator(self):
        class C(object): pass
        c = C()
        d = c.__dict__
        import System

        # Check empty enumerator
        e = System.Collections.IDictionary.GetEnumerator(d)
        self.assertRaises(SystemError, getattr, e, "Key")
        self.assertEqual(e.MoveNext(), False)
        self.assertRaises(SystemError, getattr, e, "Key")
        
        # Add non-string attribute
        d[1] = 100
        e = System.Collections.IDictionary.GetEnumerator(d)

        self.assertRaises(SystemError, getattr, e, "Key")
        self.assertEqual(e.MoveNext(), True)
        self.assertEqual(e.Key, 1)
        self.assertEqual(e.MoveNext(), False)
        self.assertRaises(SystemError, getattr, e, "Key")
        
        # Add string attribute
        c.attr = 100
        e = System.Collections.IDictionary.GetEnumerator(d)
        self.assertRaises(SystemError, getattr, e, "Key")
        self.assertEqual(e.MoveNext(), True)
        key1 = e.Key
        self.assertEqual(e.MoveNext(), True)
        key2 = e.Key
        self.assertEqual((key1, key2) == (1, "attr") or (key1, key2) == ("attr", 1), True)
        self.assertEqual(e.MoveNext(), False)
        self.assertRaises(SystemError, getattr, e, "Key")
        
        # Remove non-string attribute
        del d[1]
        e = System.Collections.IDictionary.GetEnumerator(d)
        self.assertRaises(SystemError, getattr, e, "Key")
        self.assertEqual(e.MoveNext(), True)
        self.assertEqual(e.Key, "attr")
        self.assertEqual(e.MoveNext(), False)
        self.assertRaises(SystemError, getattr, e, "Key")
        
        # Remove string attribute and check empty enumerator
        del c.attr
        e = System.Collections.IDictionary.GetEnumerator(d)
        self.assertRaises(SystemError, getattr, e, "Key")
        self.assertEqual(e.MoveNext(), False)
        self.assertRaises(SystemError, getattr, e, "Key")
    
    def test_same_but_different(self):
        """Test case checks that when two values who are logically different but share hash code & equality result in only a single entry"""
        
        self.assertEqual({-10:0, -10L:1}, {-10:1})


    def test_module_dict(self):
        me = sys.modules[__name__]
        moduleDict = me.__dict__
        self.assertEqual(operator.isMappingType(moduleDict), True)
        self.assertEqual(moduleDict.__contains__("DictTest"), True)
        self.assertEqual(moduleDict["DictTest"], DictTest)
        self.assertEqual(moduleDict.keys().__contains__("DictTest"), True)

    def test_eval_locals_simple(self):
        class Locals(dict):
            def __getitem__(self, key):
                try:
                    return dict.__getitem__(self, key)
                except KeyError as e:
                    return 'abc'
        
        locs = Locals()
        self.assertEqual(eval("unknownvariable", globals(), locs), 'abc')


    def test_key_error(self):
        class c: pass
        class d(object): pass
        
        
        for key in ['abc', 1, c(), d(), 1.0, 1L]:
            try:
                {}[key]
            except KeyError as e:
                self.assertEqual(e.args[0], key)
            
            try:
                del {}[key]
            except KeyError as e:
                self.assertEqual(e.args[0], key)
                
            try:
                set([]).remove(key)
            except KeyError as e:
                self.assertEqual(e.args[0], key)

    def test_contains(self):
        class ContainsDict(dict):
            was_called = False
            def __contains__(self, key):
                ContainsDict.was_called = True
                return dict.__contains__(self, key)

        md = ContainsDict()
        md["stuff"] = 1
        
        self.assertEqual(ContainsDict.was_called, False)
        self.assertEqual("nothing" in md, False)
        self.assertEqual("stuff" in md, True)
        self.assertEqual(ContainsDict.was_called, True)


    def test_stdtypes_dict(self):
        temp_types = [  int,
                        long,
                        float,
                        complex,
                        bool,
                        str,
                        unicode,
                        basestring,
                        list,
                        tuple,
                        xrange,
                        dict,
                        set,
                        frozenset,
                        type,
                        object,
                    ] #+ [eval("types." + x) for x in dir(types) if x.endswith("Type")]
        
        temp_types.append(file)
        
        
        temp_keys = [ None, -1, 0, 1, 2.34, "", "None", int, object, self.test_stdtypes_dict, [], (None,)]
        
        for temp_type in temp_types:
            for temp_key in temp_keys:
                def tFunc(): temp_type.__dict__[temp_key] = 0
                self.assertRaises(TypeError, tFunc)
    


    def test_main_dict(self):
        import __main__
        #just make sure this doesn't throw...
        t_list = []
        for w in __main__.__dict__: t_list.append(w)
        
        t_list.sort()
        g_list = globals().keys()
        g_list.sort()
        self.assertEqual(t_list, g_list)
    

    def test_update(self):
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
                print "ERROR:", start_dict, ".update(*", dict_param, ", **", kw_params, ") failed!"
                raise e
            
            self.assertEqual(start_dict, expected)

    def test_update_argnames(self):
        expected = {"b": 1}
        result = {}
        result.update(b=1)

        self.assertEqual(result, expected)

        expected = {"other": 1}
        result = {}
        result.update(other=1)

        self.assertEqual(result, expected)

        expected = {"other": 1, "otherArgs": 2}
        result = {}
        result.update({"other": 1}, otherArgs=2)

        self.assertEqual(result, expected)

    def test_update_no_setitem(self):
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
        self.assertEqual(d.setcalled, False)
        
        d.update({'foo': 2})
        self.assertEqual(d.setcalled, False)
    
    def test_keys_not_as_property(self):
        def f():
            mapping = { 10: 10}
            for k in mapping.keys: pass

        self.assertRaisesMessages(TypeError,
                "iteration over non-sequence of type builtin_function_or_method",
                "'builtin_function_or_method' object is not iterable",
                f)

    def test_dict_class_dictionary(self):
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
            self.assertEqual(K.__dict__["KLASS_MEMBER"], 3.14)
            self.assertEqual(temp_dict["KLASS_MEMBER"], 3.14)
            
            #methods show up?
            for func_name in ["aFunc", "aMethod"]:
                self.assertTrue(func_name in K.__dict__.keys())
                self.assertTrue(func_name in temp_dict.keys())
            
        expected_keys = [   '__module__', 'KLASS_MEMBER', 'aFunc', 'aMethod',
                            '__dict__',
                            '__weakref__', '__doc__']
        for expected_key in expected_keys:
            self.assertTrue(KNew.__dict__.has_key(expected_key), expected_key)
            self.assertTrue(temp_dict.has_key(expected_key), expected_key)
        

    def test_cp15882(self):
        x = {}
        
        #negative cases
        for bad_stuff in [
                            [1],
                            {}, {1:1}, {(1,2): 1},
                            set()]:
            try:
                x[bad_stuff] = 1
                self.fail(str(bad_stuff) + " is unhashable")
            except TypeError:
                self.assertEqual(x, {})
        
        
        #positive cases
        for stuff in [
                        (), (None),
                        (-1), (0), (1), (2),
                        (1, 2), (1, 2, 3),
                        xrange(3), 1j, object, self.test_cp15882,
                        (xrange(3)), (1j), (object), (self.test_cp15882),
                        (()), ((())),
                        ]:
            for i in xrange(2):
                x[stuff] = 1
                self.assertEqual(x[stuff], 1)
                del x[stuff]
                self.assertEqual(x, {})
                self.assertRaises(KeyError, x.__delitem__, stuff)
        
            for i in xrange(2):
                x[stuff] = 1
                self.assertEqual(x[stuff], 1)
                x.__delitem__(stuff)
                self.assertEqual(x, {})
                self.assertRaises(KeyError, x.__delitem__, stuff)
            
    def test_cp35348(self):
        empty = {}        # underlying type: EmptyDictionaryStorage
        emptied = {1:1}   # underlying type: CommonDictionaryStorage
        del emptied[1]
        not_empty = {42:1}

        #negative cases
        for bad_stuff in [
                            [1],
                            {}, {1:1}, {(1,2): 1},
                            set()]:
            try:
                dummy = bad_stuff in empty
                self.fail(str(bad_stuff) + " is unhashable")
            except TypeError:
                pass
            try:
                dummy = bad_stuff in emptied
                self.fail(str(bad_stuff) + " is unhashable")
            except TypeError:
                pass
            try:
                dummy = bad_stuff in not_empty
                self.fail(str(bad_stuff) + " is unhashable")
            except TypeError:
                pass

        class C1(object):
            pass
        c1=C1()
        class C2:
            pass
        c2=C2()

        #positive cases
        for stuff in [
                        (), (None),
                        (-1), (0), (1), (2),
                        (1, 2), (1, 2, 3),
                        xrange(3), 1j, object, self.test_cp35348,
                        (xrange(3)), (1j), (object), (self.test_cp35348),
                        (()), ((())), c1, c2,
                        ]:
            self.assertFalse(stuff in empty)
            self.assertFalse(stuff in emptied)
            self.assertFalse(stuff in not_empty)

        for stuff in [
                        (), (None),
                        (-1), (0), (1), (2),
                        (1, 2), (1, 2, 3),
                        xrange(3), 1j, object, self.test_cp35348,
                        (xrange(3)), (1j), (object), (self.test_cp35348),
                        (()), ((())), c1, c2,
                        ]:
            emptied[stuff] = 'test_cp35348'
            self.assertTrue(stuff in emptied)
            del emptied[stuff]
            self.assertEqual(len(empty), 0)
            not_empty[stuff] = 'test_cp35348'
            self.assertTrue(stuff in not_empty)
            del not_empty[stuff]
            self.assertEqual(len(not_empty), 1)

    def test_cp35667(self):
        try:
            self.assertFalse(type([]) in {})
            self.assertFalse(type({}) in {})
            d = {list:1, dict:2}
            self.assertTrue(list in d)
            self.assertTrue(dict in d)
        except Exception as ex:
            self.assertTrue(False, "unexpected exception: %s" % ex)


    def test_comparison_operators(self):
        x = {2:3}
        y = {2:4}
        for oper in ('__lt__', '__gt__', '__le__', '__ge__'):
            for data in (y, None, 1, 1.0, 1L, (), [], 1j, "abc"):
                self.assertEqual(getattr(x, oper)(data), NotImplemented)

    def test_cp16519(self):
        __main__ = __import__(__name__)
        __main__.Dict = {"1": "a"}
        self.assertEqual(__main__.Dict["1"], "a")
        del __main__.Dict
        
        import sys
        sys.Dict = {"1": "b"}
        self.assertEqual(sys.Dict["1"], "b")
        del sys.Dict

        with path_modifier(os.path.join(source_root(), 'Tests')):
            import testpkg1
            testpkg1.Dict = {"1": "c"}
            self.assertEqual(testpkg1.Dict["1"], "c")
            del testpkg1.Dict

    def test_dict_equality_lookup(self):
        """dictionaries check object equality before running normal equality"""
        class x(object):
            def __eq__(self, other):
                    return False
            def __ne__(self, other):
                    return True
        
        a = x()
        d = {}
        d[a] = 42
        self.assertEqual(d[a], 42)

    def test_missing(self):
        class Foo(dict):
            def __missing__(self, key):
                raise TypeError('Foo.__missing__ should not be called')
        
        f = Foo()
        
        self.assertEqual(f.setdefault(1, 2), 2)
        self.assertEqual(f.get(2), None)
        self.assertEqual(f.get(2, 3), 3)
        self.assertRaises(KeyError, f.pop, 3)
        self.assertEqual(f.pop(3, 4), 4)
        
        x = {2:3}
        for f in (Foo({'abc':3}), Foo()):
            self.assertTrue(x != f)
            self.assertTrue(f != x)
            
            self.assertEqual(x.__eq__(f), False)
            self.assertEqual(f.__eq__(x), False)
    

    def test_cp29914(self):
        self.assertEqual(dict(o=42), {'o':42})

    def test_cp32527(self):
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
        actual = d.keys().count('a8')
        self.assertEqual(actual, expected)

    def test_cp34770(self):
        # Entries added with Int64/UInt64 should be findable with Python long
        from System import Int64, UInt64
        i64 = Int64(1110766100758387874)
        u64 = UInt64(9223372036854775808)
        
        m = {}
        m[i64] = 'a'
        self.assertEqual(m[1110766100758387874L], 'a')
        
        m[u64] = 'b'
        self.assertEqual(m[9223372036854775808L], 'b')

run_test(__name__)
