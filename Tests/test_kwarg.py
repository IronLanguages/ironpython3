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

#############################################################
# Helper functions for verifying the calls.  On each call
# we set the global dictionary, and then check it afterwards.

def CheckArgDict(a, b, param, kw):
    Assert(argDict['a'] == a, 'a is wrong got ' + repr(argDict['a']) + ' expected ' + repr(a))
    Assert(argDict['b'] == b, 'b is wrong got ' + repr(argDict['b']) + ' expected ' + repr(b))
    Assert(argDict['param'] == param, 'param is wrong got ' + repr(argDict['param']) + ' expected ' + repr(param))
    Assert(argDict['kw'] == kw, 'keywords are wrong got ' + repr(argDict['kw']) + ' expected ' + repr(kw))

def SetArgDict(a, b, param, kw):
    global argDict
    argDict = {}
    argDict['a'] = a
    argDict['b'] = b
    argDict['param'] = param
    argDict['kw'] = kw

def CheckArgDictInit(a, b, param, kw):
    CheckArgDict(type(a), b, param, kw)
    Assert(argDictInit['a'] == a, 'init a is wrong got ' + repr(argDictInit['a']) + ' expected ' + repr(a))
    Assert(argDictInit['b'] == b, 'init b is wrong got ' + repr(argDictInit['b']) + ' expected ' + repr(b))
    Assert(argDictInit['param'] == param, 'init param is wrong got ' + repr(argDictInit['param']) + ' expected ' + repr(param))
    Assert(argDictInit['kw'] == kw, 'init keywords are wrong got ' + repr(argDictInit['kw']) + ' expected ' + repr(kw))

def SetArgDictInit(a, b, param, kw):
    global argDictInit
    argDictInit = {}
    argDictInit['a'] = a
    argDictInit['b'] = b
    argDictInit['param'] = param
    argDictInit['kw'] = kw

#############################################################
# Test methods & classes

# keyword args on functions

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



#######################################################
# Positive test cases

########################
# stand alone method test cases

def testFunc_pw_kw_cases():
    testFunc_pw_kw('abc', b='def', c='cde')
    CheckArgDict('abc', None, (), {'c': 'cde', 'b':'def'})
    testFunc_pw_kw('abc', 'def', c='cde')
    CheckArgDict('abc', None, ('def', ), {'c': 'cde'})
    testFunc_pw_kw(a='abc', b='def', c='cde')
    CheckArgDict('abc', None, (), {'c': 'cde', 'b':'def'})
    testFunc_pw_kw(c='cde', b='def', a='abc')
    CheckArgDict('abc', None, (), {'c': 'cde', 'b':'def'})
    testFunc_pw_kw('abc', 'hgi', 'jkl', b='def', c='cde')
    CheckArgDict('abc', None, ('hgi', 'jkl'), {'c': 'cde', 'b':'def'})
    testFunc_pw_kw('abc', 'hgi', 'jkl')
    CheckArgDict('abc', None, ('hgi', 'jkl'), {})
    testFunc_pw_kw('abc')
    CheckArgDict('abc', None, (), {})
    testFunc_pw_kw('abc', 'cde')
    CheckArgDict('abc', None, ('cde',), {})

def testFunc_kw_cases():
    testFunc_kw('abc', b='def', c='cde')
    CheckArgDict('abc', None, None, {'c': 'cde', 'b':'def'})
    testFunc_kw('abc', c='cde')
    CheckArgDict('abc', None, None, {'c': 'cde'})
    testFunc_kw(a='abc', b='def', c='cde')
    CheckArgDict('abc', None, None, {'c': 'cde', 'b':'def'})
    testFunc_kw(c='cde', b='def', a='abc')
    CheckArgDict('abc', None, None, {'c': 'cde', 'b':'def'})
    testFunc_kw('abc')
    CheckArgDict('abc', None, None, {})

def testFunc_pw_kw_2_cases():
    testFunc_pw_kw_2('abc', b='def', c='cde')
    CheckArgDict('abc', 'def', (), {'c': 'cde'})
    testFunc_pw_kw_2('abc', 'def', c='cde')
    CheckArgDict('abc', 'def', (), {'c': 'cde'})
    testFunc_pw_kw_2(a='abc', b='def', c='cde')
    CheckArgDict('abc', 'def', (), {'c': 'cde'})
    testFunc_pw_kw_2(c='cde', b='def', a='abc')
    CheckArgDict('abc', 'def', (), {'c': 'cde'})
    testFunc_pw_kw_2('abc', 'hgi', 'jkl', d='def', c='cde')
    CheckArgDict('abc', 'hgi', ('jkl',), {'c': 'cde', 'd':'def'})
    testFunc_pw_kw_2('abc', 'hgi', 'jkl')
    CheckArgDict('abc', 'hgi', ('jkl',), {})
    testFunc_pw_kw_2('abc', 'hgi', 'jkl', 'pqr')
    CheckArgDict('abc', 'hgi', ('jkl', 'pqr'), {})
    testFunc_pw_kw_2('abc', 'cde')
    CheckArgDict('abc', 'cde', (), {})

def testFunc_kw_2_cases():
    testFunc_kw_2('abc', b='def', c='cde')
    CheckArgDict('abc', 'def', None, {'c': 'cde'})
    testFunc_kw_2('abc', 'def', c='cde')
    CheckArgDict('abc', 'def', None, {'c': 'cde'})
    testFunc_kw_2(a='abc', b='def', c='cde')
    CheckArgDict('abc', 'def', None, {'c': 'cde'})
    testFunc_kw_2(c='cde', b='def', a='abc')
    CheckArgDict('abc', 'def', None, {'c': 'cde'})
    testFunc_kw_2('abc', 'def')
    CheckArgDict('abc', 'def', None, {})

########################
# class test cases

def testFunc_subcls_pw_kw_cases(o):
    o.testFunc_pw_kw(b='def', c='cde')
    CheckArgDict(o, None, (), {'c': 'cde', 'b':'def'})
    o.testFunc_pw_kw('def', c='cde')
    CheckArgDict(o, None, ('def', ), {'c': 'cde'})
    o.testFunc_pw_kw(b='def', c='cde')
    CheckArgDict(o, None, (), {'c': 'cde', 'b':'def'})
    o.testFunc_pw_kw(c='cde', b='def')
    CheckArgDict(o, None, (), {'c': 'cde', 'b':'def'})
    o.testFunc_pw_kw('hgi', 'jkl', b='def', c='cde')
    CheckArgDict(o, None, ('hgi', 'jkl'), {'c': 'cde', 'b':'def'})
    o.testFunc_pw_kw('hgi', 'jkl')
    CheckArgDict(o, None, ('hgi', 'jkl'), {})
    o.testFunc_pw_kw()
    CheckArgDict(o, None, (), {})
    o.testFunc_pw_kw('cde')
    CheckArgDict(o, None, ('cde',), {})

def testFunc_subcls_kw_cases(o):
    o.testFunc_kw(b='def', c='cde')
    CheckArgDict(o, None, None, {'c': 'cde', 'b':'def'})
    o.testFunc_kw(c='cde')
    CheckArgDict(o, None, None, {'c': 'cde'})
    o.testFunc_kw(b='def', c='cde')
    CheckArgDict(o, None, None, {'c': 'cde', 'b':'def'})
    o.testFunc_kw(c='cde', b='def')
    CheckArgDict(o, None, None, {'c': 'cde', 'b':'def'})
    o.testFunc_kw()
    CheckArgDict(o, None, None, {})

def testFunc_subcls_pw_kw_2_cases(o):
    o.testFunc_pw_kw_2(b='def', c='cde')
    CheckArgDict(o, 'def', (), {'c': 'cde'})
    o.testFunc_pw_kw_2('def', c='cde')
    CheckArgDict(o, 'def', (), {'c': 'cde'})
    o.testFunc_pw_kw_2(b='def', c='cde')
    CheckArgDict(o, 'def', (), {'c': 'cde'})
    o.testFunc_pw_kw_2(c='cde', b='def')
    CheckArgDict(o, 'def', (), {'c': 'cde'})
    o.testFunc_pw_kw_2('hgi', 'jkl', d='def', c='cde')
    CheckArgDict(o, 'hgi', ('jkl',), {'c': 'cde', 'd':'def'})
    o.testFunc_pw_kw_2('hgi', 'jkl')
    CheckArgDict(o, 'hgi', ('jkl',), {})
    o.testFunc_pw_kw_2('hgi', 'jkl', 'pqr')
    CheckArgDict(o, 'hgi', ('jkl', 'pqr'), {})
    o.testFunc_pw_kw_2('cde')
    CheckArgDict(o, 'cde', (), {})

def testFunc_subcls_kw_2_cases(o):
    o.testFunc_kw_2(b='def', c='cde')
    CheckArgDict(o, 'def', None, {'c': 'cde'})
    o.testFunc_kw_2('def', c='cde')
    CheckArgDict(o, 'def', None, {'c': 'cde'})
    o.testFunc_kw_2(b='def', c='cde')
    CheckArgDict(o, 'def', None, {'c': 'cde'})
    o.testFunc_kw_2(c='cde', b='def')
    CheckArgDict(o, 'def', None, {'c': 'cde'})
    o.testFunc_kw_2('def')
    CheckArgDict(o, 'def', None, {})

########################
# new test cases

def testFunc_NewAll():
    v = NewAll()
    CheckArgDict(NewAll, None, (), {})
    AreEqual(type(v), NewAll)
    
    v = NewAll(a='abc')
    CheckArgDict(NewAll, None, (), {'a': 'abc'})
    AreEqual(type(v), NewAll)
    
    v = NewAll('abc')
    CheckArgDict(NewAll, None, ('abc',), {})
    AreEqual(type(v), NewAll)
    
    v = NewAll('abc', 'def')
    CheckArgDict(NewAll, None, ('abc','def'), {})
    AreEqual(type(v), NewAll)
    
    v = NewAll('abc', d='def')
    CheckArgDict(NewAll, None, ('abc',), {'d': 'def'})
    AreEqual(type(v), NewAll)
    
    v = NewAll('abc', 'efg', d='def')
    CheckArgDict(NewAll, None, ('abc','efg'), {'d': 'def'})
    AreEqual(type(v), NewAll)

def testFunc_NewKw():
    v = NewKw()
    CheckArgDict(NewKw, None, None, {})
    AreEqual(type(v), NewKw)
    
    v = NewKw(a='abc')
    CheckArgDict(NewKw, None, None, {'a': 'abc'})
    AreEqual(type(v), NewKw)
    
    v = NewKw(a='abc', b='cde')
    CheckArgDict(NewKw, None, None, {'a': 'abc', 'b':'cde'})
    AreEqual(type(v), NewKw)
    
    v = NewKw(b='cde', a='abc')
    CheckArgDict(NewKw, None, None, {'a': 'abc', 'b':'cde'})
    AreEqual(type(v), NewKw)

def testFunc_NewKwAndExtraParam():
    v = NewKwAndExtraParam('abc')
    CheckArgDict(NewKwAndExtraParam, 'abc', None, {})
    AreEqual(type(v), NewKwAndExtraParam)
    
    v = NewKwAndExtraParam(a='abc')
    CheckArgDict(NewKwAndExtraParam, 'abc', None, {})
    AreEqual(type(v), NewKwAndExtraParam)
    
    v = NewKwAndExtraParam(a='abc', b='cde', e='def')
    CheckArgDict(NewKwAndExtraParam, 'abc', None, {'b':'cde', 'e':'def'})
    AreEqual(type(v), NewKwAndExtraParam)
    
    v = NewKwAndExtraParam(b='cde', e='def', a='abc')
    CheckArgDict(NewKwAndExtraParam, 'abc', None, {'b':'cde', 'e':'def'})
    AreEqual(type(v), NewKwAndExtraParam)

def testFunc_NewKwAndExtraParamAndParams():
    v = NewKwAndExtraParamAndParams('abc')
    CheckArgDict(NewKwAndExtraParamAndParams, 'abc', (), {})
    AreEqual(type(v), NewKwAndExtraParamAndParams)
    
    v = NewKwAndExtraParamAndParams(a='abc')
    CheckArgDict(NewKwAndExtraParamAndParams, 'abc', (), {})
    AreEqual(type(v), NewKwAndExtraParamAndParams)
    
    v = NewKwAndExtraParamAndParams(a='abc', b='cde', e='def')
    CheckArgDict(NewKwAndExtraParamAndParams, 'abc', (), {'b':'cde', 'e':'def'})
    AreEqual(type(v), NewKwAndExtraParamAndParams)
    
    v = NewKwAndExtraParamAndParams(b='cde', e='def', a='abc')
    CheckArgDict(NewKwAndExtraParamAndParams, 'abc', (), {'b':'cde', 'e':'def'})
    AreEqual(type(v), NewKwAndExtraParamAndParams)
    
    v = NewKwAndExtraParamAndParams('abc','cde')
    CheckArgDict(NewKwAndExtraParamAndParams, 'abc', ('cde',), {})
    AreEqual(type(v), NewKwAndExtraParamAndParams)
    
    v = NewKwAndExtraParamAndParams('abc','cde','def')
    CheckArgDict(NewKwAndExtraParamAndParams, 'abc', ('cde','def'), {})
    AreEqual(type(v), NewKwAndExtraParamAndParams)
    
    v = NewKwAndExtraParamAndParams('abc', 'cde', e='def')
    CheckArgDict(NewKwAndExtraParamAndParams, 'abc', ('cde',), {'e':'def'})
    AreEqual(type(v), NewKwAndExtraParamAndParams)
    
    v = NewKwAndExtraParamAndParams('abc', 'cde', e='def', f='ghi')
    CheckArgDict(NewKwAndExtraParamAndParams, 'abc', ('cde',), {'e':'def', 'f':'ghi'})
    AreEqual(type(v), NewKwAndExtraParamAndParams)


########################
# init/new test cases

def testFunc_NewInitAll():
    v = NewInitAll()
    CheckArgDictInit(v, None, (), {})
    AreEqual(type(v), NewInitAll)
    
    v = NewInitAll('abc')
    CheckArgDictInit(v, None, ('abc', ), {})
    AreEqual(type(v), NewInitAll)
    
    v = NewInitAll('abc', 'cde')
    CheckArgDictInit(v, None, ('abc', 'cde'), {})
    AreEqual(type(v), NewInitAll)
    
    v = NewInitAll('abc', d='def')
    CheckArgDictInit(v, None, ('abc', ), {'d':'def'})
    AreEqual(type(v), NewInitAll)
    
    v = NewInitAll('abc', d='def', e='fgi')
    CheckArgDictInit(v, None, ('abc', ), {'d':'def', 'e':'fgi'})
    AreEqual(type(v), NewInitAll)
    
    v = NewInitAll('abc', 'hgi', d='def', e='fgi')
    CheckArgDictInit(v, None, ('abc', 'hgi'), {'d':'def', 'e':'fgi'})
    AreEqual(type(v), NewInitAll)

def testFunc_NewInitKw():
    v = NewInitKw()
    CheckArgDictInit(v, None, None, {})
    AreEqual(type(v), NewInitKw)
    
    v = NewInitKw(d='def')
    CheckArgDictInit(v, None, None, {'d':'def'})
    AreEqual(type(v), NewInitKw)
    
    v = NewInitKw(d='def', e='fgi')
    CheckArgDictInit(v, None, None, {'d':'def', 'e':'fgi'})
    AreEqual(type(v), NewInitKw)
    
    v = NewInitKw(d='def', e='fgi', f='ijk')
    CheckArgDictInit(v, None, None, {'d':'def', 'e':'fgi', 'f':'ijk'})
    AreEqual(type(v), NewInitKw)

def testFunc_NewInitKwAndExtraParam():
    v = NewInitKwAndExtraParam('abc')
    CheckArgDictInit(v, 'abc', None, {})
    AreEqual(type(v), NewInitKwAndExtraParam)
    
    v = NewInitKwAndExtraParam('abc',d='def')
    CheckArgDictInit(v, 'abc', None, {'d':'def'})
    AreEqual(type(v), NewInitKwAndExtraParam)
    
    v = NewInitKwAndExtraParam('abc',d='def', e='fgi')
    CheckArgDictInit(v, 'abc', None, {'d':'def', 'e':'fgi'})
    AreEqual(type(v), NewInitKwAndExtraParam)
    
    v = NewInitKwAndExtraParam('abc', d='def', e='fgi', f='ijk')
    CheckArgDictInit(v, 'abc', None, {'d':'def', 'e':'fgi', 'f':'ijk'})
    AreEqual(type(v), NewInitKwAndExtraParam)
    
    v = NewInitKwAndExtraParam(a='abc')
    CheckArgDictInit(v, 'abc', None, {})
    AreEqual(type(v), NewInitKwAndExtraParam)
    
    v = NewInitKwAndExtraParam(a='abc',d='def')
    CheckArgDictInit(v, 'abc', None, {'d':'def'})
    AreEqual(type(v), NewInitKwAndExtraParam)
    
    v = NewInitKwAndExtraParam(a='abc',d='def', e='fgi')
    CheckArgDictInit(v, 'abc', None, {'d':'def', 'e':'fgi'})
    AreEqual(type(v), NewInitKwAndExtraParam)
    
    v = NewInitKwAndExtraParam(a='abc', d='def', e='fgi', f='ijk')
    CheckArgDictInit(v, 'abc', None, {'d':'def', 'e':'fgi', 'f':'ijk'})
    AreEqual(type(v), NewInitKwAndExtraParam)

def testFunc_NewInitKwAndExtraParamAndParams():
    v = NewInitKwAndExtraParamAndParams('abc')
    CheckArgDict(NewInitKwAndExtraParamAndParams, 'abc', (), {})
    AreEqual(type(v), NewInitKwAndExtraParamAndParams)
    
    v = NewInitKwAndExtraParamAndParams('abc', 'cde')
    CheckArgDict(NewInitKwAndExtraParamAndParams, 'abc', ('cde',), {})
    AreEqual(type(v), NewInitKwAndExtraParamAndParams)
    
    v = NewInitKwAndExtraParamAndParams('abc', 'cde', 'def')
    CheckArgDict(NewInitKwAndExtraParamAndParams, 'abc', ('cde','def'), {})
    AreEqual(type(v), NewInitKwAndExtraParamAndParams)
    
    v = NewKwAndExtraParamAndParams(a='abc', b='cde', e='def')
    CheckArgDict(NewKwAndExtraParamAndParams, 'abc', (), {'b':'cde', 'e':'def'})
    AreEqual(type(v), NewKwAndExtraParamAndParams)
    
    v = NewKwAndExtraParamAndParams('abc', 'cde', e='def', d='ghi')
    CheckArgDict(NewKwAndExtraParamAndParams, 'abc', ('cde',), {'e':'def', 'd':'ghi'})
    AreEqual(type(v), NewKwAndExtraParamAndParams)
    
    v = NewKwAndExtraParamAndParams('abc', e='def', f='ghi')
    CheckArgDict(NewKwAndExtraParamAndParams, 'abc', (), {'e':'def', 'f':'ghi'})
    AreEqual(type(v), NewKwAndExtraParamAndParams)


#######################################################
# Negative test cases


# got multiple values for keyword argument 'a'
def negTestFunc_testFunc_pw_kw_dupArg():
    testFunc_pw_kw('234', a='234')

def negTestFunc_testFunc_kw_dupArg():
    testFunc_kw('234',a='234')

def negTestFunc_ObjectSubClass_testFunc_pw_kw_dupArg():
    o = ObjectSubClass()
    o.testFunc_pw_kw(a='abc')

def negTestFunc_ObjectSubClass_testFunc_kw_dupArg():
    o = ObjectSubClass()
    o.testFunc_kw(a='abc')

def negTestFunc_ObjectSubClass_testFunc_pw_kw_2_dupArg():
    o = ObjectSubClass()
    o.testFunc_pw_kw_2(a='abc')

def negTestFunc_ObjectSubClass_testFunc_kw_2_dupArg():
    o = ObjectSubClass()
    o.testFunc_kw_2(a='abc')

def negTestFunc_ObjectSubClass_testFunc_pw_kw_2_dupArg_2():
    o = ObjectSubClass()
    o.testFunc_pw_kw_2('abc',b='cde')

def negTestFunc_ObjectSubClass_testFunc_kw_2_dupArg_2():
    o = ObjectSubClass()
    o.testFunc_kw_2('abc',b='cde')

def negTestFunc_tooManyArgs():
    testFunc_kw('abc','cde')

def negTestFunc_tooManyArgs2():
    testFunc_kw('abc','cde','efg')

def negTestFunc_missingArg():
    testFunc_kw(x='abc',y='cde')

def negTestFunc_missingArg():
    testFunc_kw(x='abc',y='cde')

def negTestFunc_badKwArgs():
    testFunc_plain(a='abc', x='zy')

def NewSetCls():
    NewAll(cls=NewAll)

def NewNotEnoughArgs():
    NewKw('abc')

def NewNotEnoughArgs2():
    NewKwAndExtraParam()


# other random tests...

# verify named propertys work
def propTest():
    property(fget=ObjectSubClass,doc="prop")
    
# verify we can derive from list  & new up w/ keyword args
class ListSubcls(list):
    def __new__(cls, **kw):
        pass
        
a = ListSubcls(a='abc')

# verify we can call built in types w/ named args & have the args
# set properties.
@skip("silverlight")
@skip("win32")
def test_builtintypes():
    import clr
    clr.AddReferenceByPartialName('System.Windows.Forms')
    import System.Windows.Forms as WinForms
    a = WinForms.Button(Text='abc')
    Assert(a.Text, 'abc')

########################################################
# Known bugs

# this current AVs w/ a workaround (IP BUG 344)
class DoReturn(Exception):
    def __init__(self, *params):
        pass
        
a = DoReturn('abc','cde','efg')
      
#######################################################
# Run all test cases

testFunc_pw_kw_cases()
testFunc_kw_cases()
testFunc_pw_kw_2_cases()
testFunc_kw_2_cases()

subcls = ObjectSubClass()

testFunc_subcls_pw_kw_cases(subcls)
testFunc_subcls_kw_cases(subcls)
testFunc_subcls_pw_kw_2_cases(subcls)
testFunc_subcls_kw_2_cases(subcls)

subcls = OldStyleClass()
testFunc_subcls_pw_kw_cases(subcls)
testFunc_subcls_kw_cases(subcls)
testFunc_subcls_pw_kw_2_cases(subcls)
testFunc_subcls_kw_2_cases(subcls)

testFunc_NewAll()
testFunc_NewKw()
testFunc_NewKwAndExtraParam()
testFunc_NewKwAndExtraParamAndParams()

testFunc_NewInitAll()
testFunc_NewInitKw()
testFunc_NewInitKwAndExtraParam()
testFunc_NewInitKwAndExtraParamAndParams()

propTest()
    
AssertError(TypeError, negTestFunc_testFunc_pw_kw_dupArg)
AssertError(TypeError, negTestFunc_testFunc_kw_dupArg)

AssertError(TypeError, negTestFunc_ObjectSubClass_testFunc_pw_kw_dupArg)
AssertError(TypeError, negTestFunc_ObjectSubClass_testFunc_kw_dupArg)
AssertError(TypeError, negTestFunc_ObjectSubClass_testFunc_pw_kw_2_dupArg)
AssertError(TypeError, negTestFunc_ObjectSubClass_testFunc_kw_2_dupArg)
AssertError(TypeError, negTestFunc_ObjectSubClass_testFunc_pw_kw_2_dupArg_2)
AssertError(TypeError, negTestFunc_ObjectSubClass_testFunc_kw_2_dupArg_2)

AssertError(TypeError, negTestFunc_tooManyArgs)
AssertError(TypeError, negTestFunc_tooManyArgs2)

AssertError(TypeError,negTestFunc_missingArg)

AssertError(TypeError, NewSetCls)
AssertError(TypeError, NewNotEnoughArgs)
AssertError(TypeError, NewNotEnoughArgs2)

@skip("win32")
def test_sysrandom():
    import System
    a = System.Random()
    Assert(a.Next(maxValue=25) < 26)

def Regress444(**kw):
    return kw['kw']
Assert(100 == Regress444(kw=100))


#####################################################################


# using __call__ w/ keyword args should be the same as doing a call w/ kw-args

# user defined function
def f(a): return a

AreEqual(f.__call__(a='abc'), 'abc')


# built-in function
# we are a more lenient and allow kwargs to append
@skip("win32")
def test_appendkwargs():
    a = []
    a.append.__call__(item='abc')
    AreEqual(a, ['abc'])

# types

AreEqual(list.__call__(sequence='abc'), ['a', 'b', 'c'])


# calling dict subtype w/ kwargs:

class x(dict): pass

AreEqual(x(a=1)['a'], 1)

# calling unbound built-in __init__ w/ kw-args
a = []
list.__init__(a, 'abc')
AreEqual(a, ['a', 'b', 'c'])

# and doing it on a sub-class...
class sublist(list): pass
a = []
sublist.__init__(a, 'abc')
AreEqual(a, ['a', 'b', 'c'])


for base in [object, list]:
    class C(base):
        def __init__(self, args=None):
            self.args = args
    c = C(args=1)
    AreEqual(c.args, 1)

    class C(base):
        def __init__(self, type=None): self.type = type
    c = C(type=1)
    AreEqual(c.type, 1)

    class C(base):
        def __init__(self, overloads=None): self.overloads = overloads
    c = C(overloads=1)
    AreEqual(c.overloads, 1)

for base in [int, long, float]:
    class C(int):
        def __new__(cls, args=None): return base()
    c = C(args=1)

def test_kw_splat():
    def foo(**kw): pass
    
    # should raise because only strings are allowed
    try:
        foo(**{2:3})
        AssertUnreachable()
    except TypeError:
        pass
        
    def foo(a, b, **kw): return a, b, kw
    
    AreEqual(foo(1,2,**{'abc':3}), (1, 2, {'abc': 3}))
    AreEqual(foo(1,b=2,**{'abc':3}), (1, 2, {'abc': 3}))
    AreEqual(foo(1,**{'abc':3, 'b':7}), (1, 7, {'abc': 3}))
    AreEqual(foo(a=11,**{'abc':3, 'b':7}), (11, 7, {'abc': 3}))
    
    def f(a, b): return a, b

    AreEqual(f(*(1,), **{'b':7}), (1,7))
    AreEqual(f(*(1,2), **{}), (1,2))
    AreEqual(f(*(), **{'a':2, 'b':7}), (2,7))
    
    try:
        f(**{'a':2, 'b':3, 'c':4})
        AssertUnreachable()
    except TypeError:
        pass
    
def test_sequence_as_stararg():
    def f(x, *args): return x, args
    
    try: f(1, x=2)
    except TypeError: pass
    else: raise AssertionError
    
    try: f(1, x=2, *(3,4))
    except TypeError: pass
    else: raise AssertionError

    try: f(1, *(2))  # 2 is not enumerable
    except TypeError: pass
    else: raise AssertionError

    AreEqual(f(1, *(2,)), (1, (2,)))
    AreEqual(f(1, *[2]), (1, (2,)))
    AreEqual(f(1, *[2,3]), (1, (2,3)))
    AreEqual(f(1, *(2,3)), (1, (2,3)))
    AreEqual(f(1, *("23")), (1, ("2","3")))
    
    def f(*arg, **kw): return arg, kw
    AreEqual(f(1, x=2, *[3,4]), ((1,3,4), {'x':2}))
    AreEqual(f(1, x=2, *(3,4)), ((1,3,4), {'x':2}))
    AreEqual(f(x=2, *[3,4]), ((3,4), {'x':2}))
    AreEqual(f(x=2, *(3,4)), ((3,4), {'x':2}))

run_test(__name__)
