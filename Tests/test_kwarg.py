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

import unittest

from iptest import IronPythonTestCase, is_netcoreapp, is_posix, run_test, skipUnlessIronPython

#############################################################
# Helper functions for verifying the calls.  On each call
# we set the global dictionary, and then check it afterwards.

def SetArgDict(a, b, param, kw):
    global argDict
    argDict = {}
    argDict['a'] = a
    argDict['b'] = b
    argDict['param'] = param
    argDict['kw'] = kw

def SetArgDictInit(a, b, param, kw):
    global argDictInit
    argDictInit = {}
    argDictInit['a'] = a
    argDictInit['b'] = b
    argDictInit['param'] = param
    argDictInit['kw'] = kw

def testFunc_plain(a,b):
    SetArgDict(a, b, None, None)

def testFunc_pw_kw(a, *param, **kw):
    SetArgDict(a, None, param, kw)

def testFunc_kw(a, **kw):
    SetArgDict(a, None, None, kw)

def testFunc_pw_kw_2(a, b, *param, **kw):
    SetArgDict(a, b, param, kw)

def testFunc_kw_2(a, b, **kw):
    SetArgDict(a, b, None, kw)

# keyword args on a new-style class

class ObjectSubClass(object):
    def testFunc_pw_kw(a, *param, **kw):
        SetArgDict(a, None, param, kw)
    
    def testFunc_kw(a, **kw):
        SetArgDict(a, None, None, kw)
    
    def testFunc_pw_kw_2(a, b, *param, **kw):
        SetArgDict(a, b, param, kw)
    
    def testFunc_kw_2(a, b, **kw):
        SetArgDict(a, b, None, kw)

# keyword args on an old-style class
        
class OldStyleClass:
    def testFunc_pw_kw(a, *param, **kw):
        SetArgDict(a, None, param, kw)
    
    def testFunc_kw(a, **kw):
        SetArgDict(a, None, None, kw)
    
    def testFunc_pw_kw_2(a, b, *param, **kw):
        SetArgDict(a, b, param, kw)
    
    def testFunc_kw_2(a, b, **kw):
        SetArgDict(a, b, None, kw)

#### kw args on new

class NewAll(object):
    def __new__(cls, *param, **kw):
        SetArgDict(cls, None, param, kw)
        return object.__new__(cls)

class NewKw(object):
    def __new__(cls, **kw):
        SetArgDict(cls, None, None, kw)
        return object.__new__(cls)

class NewKwAndExtraParam(object):
    def __new__(cls, a, **kw):
        SetArgDict(cls, a, None, kw)
        return object.__new__(cls)

class NewKwAndExtraParamAndParams(object):
    def __new__(cls, a, *param, **kw):
        SetArgDict(cls, a, param, kw)
        return object.__new__(cls)
                

#### kw args on new w/ a corresponding init
    
class NewInitAll(object):
    def __new__(cls, *param, **kw):
        SetArgDict(cls, None, param, kw)
        return object.__new__(cls, param, kw)
    def __init__(cls, *param, **kw):
        SetArgDictInit(cls, None, param, kw)

class NewInitKw(object):
    def __new__(cls, **kw):
        SetArgDict(cls, None, None, kw)
        return object.__new__(cls, kw)
    def __init__(cls, **kw):
        SetArgDictInit(cls, None, None, kw)


class NewInitKwAndExtraParam(object):
    def __new__(cls, a, **kw):
        SetArgDict(cls, a, None, kw)
        return object.__new__(cls, a, kw)
    def __init__(cls, a, **kw):
        SetArgDictInit(cls, a, None, kw)

class NewInitKwAndExtraParamAndParams(object):
    def __new__(cls, a, *param, **kw):
        SetArgDict(cls, a, param, kw)
        return object.__new__(cls, a, param, kw)
    def __init__(cls, a, *param, **kw):
        SetArgDictInit(cls, a, param, kw)

class KwargTest(unittest.TestCase):
    def CheckArgDict(self, a, b, param, kw):
        self.assertTrue(argDict['a'] == a, 'a is wrong got ' + repr(argDict['a']) + ' expected ' + repr(a))
        self.assertTrue(argDict['b'] == b, 'b is wrong got ' + repr(argDict['b']) + ' expected ' + repr(b))
        self.assertTrue(argDict['param'] == param, 'param is wrong got ' + repr(argDict['param']) + ' expected ' + repr(param))
        self.assertTrue(argDict['kw'] == kw, 'keywords are wrong got ' + repr(argDict['kw']) + ' expected ' + repr(kw))

    def CheckArgDictInit(self, a, b, param, kw):
        self.CheckArgDict(type(a), b, param, kw)
        self.assertTrue(argDictInit['a'] == a, 'init a is wrong got ' + repr(argDictInit['a']) + ' expected ' + repr(a))
        self.assertTrue(argDictInit['b'] == b, 'init b is wrong got ' + repr(argDictInit['b']) + ' expected ' + repr(b))
        self.assertTrue(argDictInit['param'] == param, 'init param is wrong got ' + repr(argDictInit['param']) + ' expected ' + repr(param))
        self.assertTrue(argDictInit['kw'] == kw, 'init keywords are wrong got ' + repr(argDictInit['kw']) + ' expected ' + repr(kw))

    def test_pw_kw_cases(self):
        testFunc_pw_kw('abc', b='def', c='cde')
        self.CheckArgDict('abc', None, (), {'c': 'cde', 'b':'def'})
        testFunc_pw_kw('abc', 'def', c='cde')
        self.CheckArgDict('abc', None, ('def', ), {'c': 'cde'})
        testFunc_pw_kw(a='abc', b='def', c='cde')
        self.CheckArgDict('abc', None, (), {'c': 'cde', 'b':'def'})
        testFunc_pw_kw(c='cde', b='def', a='abc')
        self.CheckArgDict('abc', None, (), {'c': 'cde', 'b':'def'})
        testFunc_pw_kw('abc', 'hgi', 'jkl', b='def', c='cde')
        self.CheckArgDict('abc', None, ('hgi', 'jkl'), {'c': 'cde', 'b':'def'})
        testFunc_pw_kw('abc', 'hgi', 'jkl')
        self.CheckArgDict('abc', None, ('hgi', 'jkl'), {})
        testFunc_pw_kw('abc')
        self.CheckArgDict('abc', None, (), {})
        testFunc_pw_kw('abc', 'cde')
        self.CheckArgDict('abc', None, ('cde',), {})

    def test_kw_cases(self):
        testFunc_kw('abc', b='def', c='cde')
        self.CheckArgDict('abc', None, None, {'c': 'cde', 'b':'def'})
        testFunc_kw('abc', c='cde')
        self.CheckArgDict('abc', None, None, {'c': 'cde'})
        testFunc_kw(a='abc', b='def', c='cde')
        self.CheckArgDict('abc', None, None, {'c': 'cde', 'b':'def'})
        testFunc_kw(c='cde', b='def', a='abc')
        self.CheckArgDict('abc', None, None, {'c': 'cde', 'b':'def'})
        testFunc_kw('abc')
        self.CheckArgDict('abc', None, None, {})

    def test_pw_kw_2_cases(self):
        testFunc_pw_kw_2('abc', b='def', c='cde')
        self.CheckArgDict('abc', 'def', (), {'c': 'cde'})
        testFunc_pw_kw_2('abc', 'def', c='cde')
        self.CheckArgDict('abc', 'def', (), {'c': 'cde'})
        testFunc_pw_kw_2(a='abc', b='def', c='cde')
        self.CheckArgDict('abc', 'def', (), {'c': 'cde'})
        testFunc_pw_kw_2(c='cde', b='def', a='abc')
        self.CheckArgDict('abc', 'def', (), {'c': 'cde'})
        testFunc_pw_kw_2('abc', 'hgi', 'jkl', d='def', c='cde')
        self.CheckArgDict('abc', 'hgi', ('jkl',), {'c': 'cde', 'd':'def'})
        testFunc_pw_kw_2('abc', 'hgi', 'jkl')
        self.CheckArgDict('abc', 'hgi', ('jkl',), {})
        testFunc_pw_kw_2('abc', 'hgi', 'jkl', 'pqr')
        self.CheckArgDict('abc', 'hgi', ('jkl', 'pqr'), {})
        testFunc_pw_kw_2('abc', 'cde')
        self.CheckArgDict('abc', 'cde', (), {})

    def test_kw_2_cases(self):
        testFunc_kw_2('abc', b='def', c='cde')
        self.CheckArgDict('abc', 'def', None, {'c': 'cde'})
        testFunc_kw_2('abc', 'def', c='cde')
        self.CheckArgDict('abc', 'def', None, {'c': 'cde'})
        testFunc_kw_2(a='abc', b='def', c='cde')
        self.CheckArgDict('abc', 'def', None, {'c': 'cde'})
        testFunc_kw_2(c='cde', b='def', a='abc')
        self.CheckArgDict('abc', 'def', None, {'c': 'cde'})
        testFunc_kw_2('abc', 'def')
        self.CheckArgDict('abc', 'def', None, {})


    def _testFunc_subcls_pw_kw_cases(self, o):
        o.testFunc_pw_kw(b='def', c='cde')
        self.CheckArgDict(o, None, (), {'c': 'cde', 'b':'def'})
        o.testFunc_pw_kw('def', c='cde')
        self.CheckArgDict(o, None, ('def', ), {'c': 'cde'})
        o.testFunc_pw_kw(b='def', c='cde')
        self.CheckArgDict(o, None, (), {'c': 'cde', 'b':'def'})
        o.testFunc_pw_kw(c='cde', b='def')
        self.CheckArgDict(o, None, (), {'c': 'cde', 'b':'def'})
        o.testFunc_pw_kw('hgi', 'jkl', b='def', c='cde')
        self.CheckArgDict(o, None, ('hgi', 'jkl'), {'c': 'cde', 'b':'def'})
        o.testFunc_pw_kw('hgi', 'jkl')
        self.CheckArgDict(o, None, ('hgi', 'jkl'), {})
        o.testFunc_pw_kw()
        self.CheckArgDict(o, None, (), {})
        o.testFunc_pw_kw('cde')
        self.CheckArgDict(o, None, ('cde',), {})

    def _testFunc_subcls_kw_cases(self, o):
        o.testFunc_kw(b='def', c='cde')
        self.CheckArgDict(o, None, None, {'c': 'cde', 'b':'def'})
        o.testFunc_kw(c='cde')
        self.CheckArgDict(o, None, None, {'c': 'cde'})
        o.testFunc_kw(b='def', c='cde')
        self.CheckArgDict(o, None, None, {'c': 'cde', 'b':'def'})
        o.testFunc_kw(c='cde', b='def')
        self.CheckArgDict(o, None, None, {'c': 'cde', 'b':'def'})
        o.testFunc_kw()
        self.CheckArgDict(o, None, None, {})

    def _testFunc_subcls_pw_kw_2_cases(self, o):
        o.testFunc_pw_kw_2(b='def', c='cde')
        self.CheckArgDict(o, 'def', (), {'c': 'cde'})
        o.testFunc_pw_kw_2('def', c='cde')
        self.CheckArgDict(o, 'def', (), {'c': 'cde'})
        o.testFunc_pw_kw_2(b='def', c='cde')
        self.CheckArgDict(o, 'def', (), {'c': 'cde'})
        o.testFunc_pw_kw_2(c='cde', b='def')
        self.CheckArgDict(o, 'def', (), {'c': 'cde'})
        o.testFunc_pw_kw_2('hgi', 'jkl', d='def', c='cde')
        self.CheckArgDict(o, 'hgi', ('jkl',), {'c': 'cde', 'd':'def'})
        o.testFunc_pw_kw_2('hgi', 'jkl')
        self.CheckArgDict(o, 'hgi', ('jkl',), {})
        o.testFunc_pw_kw_2('hgi', 'jkl', 'pqr')
        self.CheckArgDict(o, 'hgi', ('jkl', 'pqr'), {})
        o.testFunc_pw_kw_2('cde')
        self.CheckArgDict(o, 'cde', (), {})

    def _testFunc_subcls_kw_2_cases(self, o):
        o.testFunc_kw_2(b='def', c='cde')
        self.CheckArgDict(o, 'def', None, {'c': 'cde'})
        o.testFunc_kw_2('def', c='cde')
        self.CheckArgDict(o, 'def', None, {'c': 'cde'})
        o.testFunc_kw_2(b='def', c='cde')
        self.CheckArgDict(o, 'def', None, {'c': 'cde'})
        o.testFunc_kw_2(c='cde', b='def')
        self.CheckArgDict(o, 'def', None, {'c': 'cde'})
        o.testFunc_kw_2('def')
        self.CheckArgDict(o, 'def', None, {})

    def test_NewAll(self):
        v = NewAll()
        self.CheckArgDict(NewAll, None, (), {})
        self.assertEqual(type(v), NewAll)
        
        v = NewAll(a='abc')
        self.CheckArgDict(NewAll, None, (), {'a': 'abc'})
        self.assertEqual(type(v), NewAll)
        
        v = NewAll('abc')
        self.CheckArgDict(NewAll, None, ('abc',), {})
        self.assertEqual(type(v), NewAll)
        
        v = NewAll('abc', 'def')
        self.CheckArgDict(NewAll, None, ('abc','def'), {})
        self.assertEqual(type(v), NewAll)
        
        v = NewAll('abc', d='def')
        self.CheckArgDict(NewAll, None, ('abc',), {'d': 'def'})
        self.assertEqual(type(v), NewAll)
        
        v = NewAll('abc', 'efg', d='def')
        self.CheckArgDict(NewAll, None, ('abc','efg'), {'d': 'def'})
        self.assertEqual(type(v), NewAll)

    def test_NewKw(self):
        v = NewKw()
        self.CheckArgDict(NewKw, None, None, {})
        self.assertEqual(type(v), NewKw)
        
        v = NewKw(a='abc')
        self.CheckArgDict(NewKw, None, None, {'a': 'abc'})
        self.assertEqual(type(v), NewKw)
        
        v = NewKw(a='abc', b='cde')
        self.CheckArgDict(NewKw, None, None, {'a': 'abc', 'b':'cde'})
        self.assertEqual(type(v), NewKw)
        
        v = NewKw(b='cde', a='abc')
        self.CheckArgDict(NewKw, None, None, {'a': 'abc', 'b':'cde'})
        self.assertEqual(type(v), NewKw)

    def test_NewKwAndExtraParam(self):
        v = NewKwAndExtraParam('abc')
        self.CheckArgDict(NewKwAndExtraParam, 'abc', None, {})
        self.assertEqual(type(v), NewKwAndExtraParam)
        
        v = NewKwAndExtraParam(a='abc')
        self.CheckArgDict(NewKwAndExtraParam, 'abc', None, {})
        self.assertEqual(type(v), NewKwAndExtraParam)
        
        v = NewKwAndExtraParam(a='abc', b='cde', e='def')
        self.CheckArgDict(NewKwAndExtraParam, 'abc', None, {'b':'cde', 'e':'def'})
        self.assertEqual(type(v), NewKwAndExtraParam)
        
        v = NewKwAndExtraParam(b='cde', e='def', a='abc')
        self.CheckArgDict(NewKwAndExtraParam, 'abc', None, {'b':'cde', 'e':'def'})
        self.assertEqual(type(v), NewKwAndExtraParam)

    def test_NewKwAndExtraParamAndParams(self):
        v = NewKwAndExtraParamAndParams('abc')
        self.CheckArgDict(NewKwAndExtraParamAndParams, 'abc', (), {})
        self.assertEqual(type(v), NewKwAndExtraParamAndParams)
        
        v = NewKwAndExtraParamAndParams(a='abc')
        self.CheckArgDict(NewKwAndExtraParamAndParams, 'abc', (), {})
        self.assertEqual(type(v), NewKwAndExtraParamAndParams)
        
        v = NewKwAndExtraParamAndParams(a='abc', b='cde', e='def')
        self.CheckArgDict(NewKwAndExtraParamAndParams, 'abc', (), {'b':'cde', 'e':'def'})
        self.assertEqual(type(v), NewKwAndExtraParamAndParams)
        
        v = NewKwAndExtraParamAndParams(b='cde', e='def', a='abc')
        self.CheckArgDict(NewKwAndExtraParamAndParams, 'abc', (), {'b':'cde', 'e':'def'})
        self.assertEqual(type(v), NewKwAndExtraParamAndParams)
        
        v = NewKwAndExtraParamAndParams('abc','cde')
        self.CheckArgDict(NewKwAndExtraParamAndParams, 'abc', ('cde',), {})
        self.assertEqual(type(v), NewKwAndExtraParamAndParams)
        
        v = NewKwAndExtraParamAndParams('abc','cde','def')
        self.CheckArgDict(NewKwAndExtraParamAndParams, 'abc', ('cde','def'), {})
        self.assertEqual(type(v), NewKwAndExtraParamAndParams)
        
        v = NewKwAndExtraParamAndParams('abc', 'cde', e='def')
        self.CheckArgDict(NewKwAndExtraParamAndParams, 'abc', ('cde',), {'e':'def'})
        self.assertEqual(type(v), NewKwAndExtraParamAndParams)
        
        v = NewKwAndExtraParamAndParams('abc', 'cde', e='def', f='ghi')
        self.CheckArgDict(NewKwAndExtraParamAndParams, 'abc', ('cde',), {'e':'def', 'f':'ghi'})
        self.assertEqual(type(v), NewKwAndExtraParamAndParams)


    def test_NewInitAll(self):
        v = NewInitAll()
        self.CheckArgDictInit(v, None, (), {})
        self.assertEqual(type(v), NewInitAll)
        
        v = NewInitAll('abc')
        self.CheckArgDictInit(v, None, ('abc', ), {})
        self.assertEqual(type(v), NewInitAll)
        
        v = NewInitAll('abc', 'cde')
        self.CheckArgDictInit(v, None, ('abc', 'cde'), {})
        self.assertEqual(type(v), NewInitAll)
        
        v = NewInitAll('abc', d='def')
        self.CheckArgDictInit(v, None, ('abc', ), {'d':'def'})
        self.assertEqual(type(v), NewInitAll)
        
        v = NewInitAll('abc', d='def', e='fgi')
        self.CheckArgDictInit(v, None, ('abc', ), {'d':'def', 'e':'fgi'})
        self.assertEqual(type(v), NewInitAll)
        
        v = NewInitAll('abc', 'hgi', d='def', e='fgi')
        self.CheckArgDictInit(v, None, ('abc', 'hgi'), {'d':'def', 'e':'fgi'})
        self.assertEqual(type(v), NewInitAll)

    def test_NewInitKw(self):
        v = NewInitKw()
        self.CheckArgDictInit(v, None, None, {})
        self.assertEqual(type(v), NewInitKw)
        
        v = NewInitKw(d='def')
        self.CheckArgDictInit(v, None, None, {'d':'def'})
        self.assertEqual(type(v), NewInitKw)
        
        v = NewInitKw(d='def', e='fgi')
        self.CheckArgDictInit(v, None, None, {'d':'def', 'e':'fgi'})
        self.assertEqual(type(v), NewInitKw)
        
        v = NewInitKw(d='def', e='fgi', f='ijk')
        self.CheckArgDictInit(v, None, None, {'d':'def', 'e':'fgi', 'f':'ijk'})
        self.assertEqual(type(v), NewInitKw)

    def test_NewInitKwAndExtraParam(self):
        v = NewInitKwAndExtraParam('abc')
        self.CheckArgDictInit(v, 'abc', None, {})
        self.assertEqual(type(v), NewInitKwAndExtraParam)
        
        v = NewInitKwAndExtraParam('abc',d='def')
        self.CheckArgDictInit(v, 'abc', None, {'d':'def'})
        self.assertEqual(type(v), NewInitKwAndExtraParam)
        
        v = NewInitKwAndExtraParam('abc',d='def', e='fgi')
        self.CheckArgDictInit(v, 'abc', None, {'d':'def', 'e':'fgi'})
        self.assertEqual(type(v), NewInitKwAndExtraParam)
        
        v = NewInitKwAndExtraParam('abc', d='def', e='fgi', f='ijk')
        self.CheckArgDictInit(v, 'abc', None, {'d':'def', 'e':'fgi', 'f':'ijk'})
        self.assertEqual(type(v), NewInitKwAndExtraParam)
        
        v = NewInitKwAndExtraParam(a='abc')
        self.CheckArgDictInit(v, 'abc', None, {})
        self.assertEqual(type(v), NewInitKwAndExtraParam)
        
        v = NewInitKwAndExtraParam(a='abc',d='def')
        self.CheckArgDictInit(v, 'abc', None, {'d':'def'})
        self.assertEqual(type(v), NewInitKwAndExtraParam)
        
        v = NewInitKwAndExtraParam(a='abc',d='def', e='fgi')
        self.CheckArgDictInit(v, 'abc', None, {'d':'def', 'e':'fgi'})
        self.assertEqual(type(v), NewInitKwAndExtraParam)
        
        v = NewInitKwAndExtraParam(a='abc', d='def', e='fgi', f='ijk')
        self.CheckArgDictInit(v, 'abc', None, {'d':'def', 'e':'fgi', 'f':'ijk'})
        self.assertEqual(type(v), NewInitKwAndExtraParam)

    def test_NewInitKwAndExtraParamAndParams(self):
        v = NewInitKwAndExtraParamAndParams('abc')
        self.CheckArgDict(NewInitKwAndExtraParamAndParams, 'abc', (), {})
        self.assertEqual(type(v), NewInitKwAndExtraParamAndParams)
        
        v = NewInitKwAndExtraParamAndParams('abc', 'cde')
        self.CheckArgDict(NewInitKwAndExtraParamAndParams, 'abc', ('cde',), {})
        self.assertEqual(type(v), NewInitKwAndExtraParamAndParams)
        
        v = NewInitKwAndExtraParamAndParams('abc', 'cde', 'def')
        self.CheckArgDict(NewInitKwAndExtraParamAndParams, 'abc', ('cde','def'), {})
        self.assertEqual(type(v), NewInitKwAndExtraParamAndParams)
        
        v = NewKwAndExtraParamAndParams(a='abc', b='cde', e='def')
        self.CheckArgDict(NewKwAndExtraParamAndParams, 'abc', (), {'b':'cde', 'e':'def'})
        self.assertEqual(type(v), NewKwAndExtraParamAndParams)
        
        v = NewKwAndExtraParamAndParams('abc', 'cde', e='def', d='ghi')
        self.CheckArgDict(NewKwAndExtraParamAndParams, 'abc', ('cde',), {'e':'def', 'd':'ghi'})
        self.assertEqual(type(v), NewKwAndExtraParamAndParams)
        
        v = NewKwAndExtraParamAndParams('abc', e='def', f='ghi')
        self.CheckArgDict(NewKwAndExtraParamAndParams, 'abc', (), {'e':'def', 'f':'ghi'})
        self.assertEqual(type(v), NewKwAndExtraParamAndParams)

    def test_list_derive(self):
        """verify we can derive from list  & new up w/ keyword args"""
        class ListSubcls(list):
            def __new__(cls, **kw):
                pass
                
        a = ListSubcls(a='abc')

    def test_negTestFunc_testFunc_pw_kw_dupArg(self):
        self.assertRaises(TypeError, testFunc_pw_kw, '234', a='234')

    def test_negTestFunc_testFunc_kw_dupArg(self):
        self.assertRaises(TypeError, testFunc_kw, '234', a='234')

    @unittest.expectedFailure
    def test_negTestFunc_ObjectSubClass_testFunc_pw_kw_dupArg(self):
        o = ObjectSubClass()
        self.assertRaises(TypeError, o.testFunc_pw_kw, a='abc')

    @unittest.expectedFailure
    def test_negTestFunc_ObjectSubClass_testFunc_kw_dupArg(self):
        o = ObjectSubClass()
        self.assertRaises(TypeError, o.testFunc_kw, a='abc')

    def test_negTestFunc_ObjectSubClass_testFunc_pw_kw_2_dupArg(self):
        o = ObjectSubClass()
        self.assertRaises(TypeError, o.testFunc_pw_kw_2, a='abc')

    def test_negTestFunc_ObjectSubClass_testFunc_kw_2_dupArg(self):
        o = ObjectSubClass()
        self.assertRaises(TypeError, o.testFunc_kw_2, a='abc')

    def test_negTestFunc_ObjectSubClass_testFunc_pw_kw_2_dupArg_2(self):
        o = ObjectSubClass()
        self.assertRaises(TypeError, o.testFunc_pw_kw_2, 'abc',b='cde')

    def test_negTestFunc_ObjectSubClass_testFunc_kw_2_dupArg_2(self):
        o = ObjectSubClass()
        self.assertRaises(TypeError, o.testFunc_kw_2, 'abc',b='cde')

    def test_negTestFunc_tooManyArgs(self):
        self.assertRaises(TypeError, testFunc_kw, 'abc','cde')

    def test_negTestFunc_tooManyArgs2(self):
        self.assertRaises(TypeError, testFunc_kw, 'abc','cde','efg')

    def test_negTestFunc_missingArg(self):
        self.assertRaises(TypeError, testFunc_kw, x='abc',y='cde')

    def test_negTestFunc_missingArg(self):
        self.assertRaises(TypeError, testFunc_kw, x='abc',y='cde')

    def test_negTestFunc_badKwArgs(self):
        self.assertRaises(TypeError, testFunc_plain, a='abc', x='zy')

    def test_NewSetCls(self):
        try:
            NewAll(cls=NewAll)
            self.assertUnreachable()
        except TypeError:
            pass

    def test_NewNotEnoughArgs(self):
        self.assertRaises(TypeError, NewKw, 'abc')

    def test_NewNotEnoughArgs2(self):
        self.assertRaises(TypeError, NewKwAndExtraParam)

    def test_property(self):
        """verify named propertys work"""
        property(fget=ObjectSubClass,doc="prop")

    @unittest.skipIf(is_netcoreapp or is_posix, 'no System.Windows.Forms')
    @skipUnlessIronPython()
    def test_builtintypes(self):
        """verify we can call built in types w/ named args & have the args set properties."""
        import clr
        clr.AddReferenceByPartialName('System.Windows.Forms')
        import System.Windows.Forms as WinForms
        a = WinForms.Button(Text='abc')
        self.assertEqual(a.Text, 'abc')

    def test_workaround(self):
        #this current AVs w/ a workaround (IP BUG 344)
        class DoReturn(Exception):
            def __init__(self, *params):
                pass
                
        a = DoReturn('abc','cde','efg')
      
    def test_object_subclass(self):
        subcls = ObjectSubClass()

        self._testFunc_subcls_pw_kw_cases(subcls)
        self._testFunc_subcls_kw_cases(subcls)
        self._testFunc_subcls_pw_kw_2_cases(subcls)
        self._testFunc_subcls_kw_2_cases(subcls)

        subcls = OldStyleClass()
        self._testFunc_subcls_pw_kw_cases(subcls)
        self._testFunc_subcls_kw_cases(subcls)
        self._testFunc_subcls_pw_kw_2_cases(subcls)
        self._testFunc_subcls_kw_2_cases(subcls)

    @skipUnlessIronPython()
    def test_sysrandom(self):
        import System
        a = System.Random()
        self.assertTrue(a.Next(maxValue=25) < 26)

    def test_regress444(self):
        def Regress444(**kw):
            return kw['kw']
        self.assertTrue(100 == Regress444(kw=100))

    def test_user_defined_function(self):
        def f(a): return a
        self.assertEqual(f.__call__(a='abc'), 'abc')


    @skipUnlessIronPython()
    def test_appendkwargs(self):
        a = []
        a.append.__call__(item='abc')
        self.assertEqual(a, ['abc'])

    def test_types(self):
        self.assertEqual(list.__call__(sequence='abc'), ['a', 'b', 'c'])

    def test_dict_subtype(self):
        """calling dict subtype w/ kwargs:"""

        class x(dict): pass
        self.assertEqual(x(a=1)['a'], 1)

    def test_unbound_builtin(self):
        """calling unbound built-in __init__ w/ kw-args"""
        a = []
        list.__init__(a, 'abc')
        self.assertEqual(a, ['a', 'b', 'c'])

        # and doing it on a sub-class...
        class sublist(list): pass
        a = []
        sublist.__init__(a, 'abc')
        self.assertEqual(a, ['a', 'b', 'c'])

    def test_subtypes(self):
        for base in [object, list]:
            class C(base):
                def __init__(self, args=None):
                    self.args = args
            c = C(args=1)
            self.assertEqual(c.args, 1)

            class C(base):
                def __init__(self, type=None): self.type = type
            c = C(type=1)
            self.assertEqual(c.type, 1)

            class C(base):
                def __init__(self, overloads=None): self.overloads = overloads
            c = C(overloads=1)
            self.assertEqual(c.overloads, 1)

        for base in [int, long, float]:
            class C(int):
                def __new__(cls, args=None): return base()
            c = C(args=1)

    def test_kw_splat(self):
        def foo(**kw): pass
        
        # should raise because only strings are allowed
        try:
            foo(**{2:3})
            self.assertUnreachable()
        except TypeError:
            pass
            
        def foo(a, b, **kw): return a, b, kw
        
        self.assertEqual(foo(1,2,**{'abc':3}), (1, 2, {'abc': 3}))
        self.assertEqual(foo(1,b=2,**{'abc':3}), (1, 2, {'abc': 3}))
        self.assertEqual(foo(1,**{'abc':3, 'b':7}), (1, 7, {'abc': 3}))
        self.assertEqual(foo(a=11,**{'abc':3, 'b':7}), (11, 7, {'abc': 3}))
        
        def f(a, b): return a, b

        self.assertEqual(f(*(1,), **{'b':7}), (1,7))
        self.assertEqual(f(*(1,2), **{}), (1,2))
        self.assertEqual(f(*(), **{'a':2, 'b':7}), (2,7))
        
        try:
            f(**{'a':2, 'b':3, 'c':4})
            self.assertUnreachable()
        except TypeError:
            pass
    
    def test_sequence_as_stararg(self):
        def f(x, *args): return x, args
        
        self.assertRaises(TypeError, f, 1, x=2)
        # try: f(1, x=2)
        # except TypeError: pass
        # else: raise AssertionError

        self.assertRaises(TypeError, f, x=2, *(3,4))
        # try: f(1, x=2, *(3,4))
        # except TypeError: pass
        # else: raise AssertionError

        try: f(1, *(2))  # 2 is not enumerable
        except TypeError: pass
        else: self.assertUnreachable()

        self.assertEqual(f(1, *(2,)), (1, (2,)))
        self.assertEqual(f(1, *[2]), (1, (2,)))
        self.assertEqual(f(1, *[2,3]), (1, (2,3)))
        self.assertEqual(f(1, *(2,3)), (1, (2,3)))
        self.assertEqual(f(1, *("23")), (1, ("2","3")))
        
        def f(*arg, **kw): return arg, kw
        self.assertEqual(f(1, x=2, *[3,4]), ((1,3,4), {'x':2}))
        self.assertEqual(f(1, x=2, *(3,4)), ((1,3,4), {'x':2}))
        self.assertEqual(f(x=2, *[3,4]), ((3,4), {'x':2}))
        self.assertEqual(f(x=2, *(3,4)), ((3,4), {'x':2}))

run_test(__name__)
