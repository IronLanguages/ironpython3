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

"""Test cases for class-related features specific to CLI"""
# this needs to run before we add ref to Microsoft.Scripting where we'll get the
# non-generic version of Action[T].  Therefore it also can't run in test_interpret_sanity.
import sys
if sys.platform=="win32":
    print("Will not run this test on CPython.  Goodbye.")
    sys.exit(0)
import System

if __name__  == '__main__':
    def f(): print('hello')
    try:
        System.Action(f)
        if not System.Environment.Version.Major>3:
            raise Exception('action[t] test failed')
    except TypeError as e:
        if e.message!="cannot create instances of Action[T] because it is a generic type definition":
            raise Exception(e.message)
    
    import clr
    clr.AddReference("System.Core")
    System.Action(f)


from iptest.assert_util import *
from iptest.warning_util import warning_trapper
skiptest("win32")

load_iron_python_test()

from IronPythonTest import *

def test_inheritance():
    class MyList(System.Collections.Generic.List[int]):
        def get0(self):
            return self[0]

    l = MyList()
    index = l.Add(22)
    Assert(l.get0() == 22)

def test_interface_inheritance():
    #
    # Verify we can inherit from a class that inherits from an interface
    #

    class MyComparer(System.Collections.IComparer):
        def Compare(self, x, y): return 0
    
    class MyDerivedComparer(MyComparer): pass
    
    class MyFurtherDerivedComparer(MyDerivedComparer): pass

    # Check that MyDerivedComparer and MyFurtherDerivedComparer can be used as an IComparer
    array = System.Array[int](list(range(10)))
    
    System.Array.Sort(array, 0, 10, MyComparer())
    System.Array.Sort(array, 0, 10, MyDerivedComparer())
    System.Array.Sort(array, 0, 10, MyFurtherDerivedComparer())
    
def test_inheritance_generic_method():
    #
    # Verify we can inherit from an interface containing a generic method
    #

    class MyGenericMethods(IGenericMethods):
        def Factory0(self, TParam = None):
            self.type = clr.GetClrType(TParam).FullName
            return TParam("123")
        def Factory1(self, x, T):
            self.type = clr.GetClrType(T).FullName
            return T("456") + x
        def OutParam(self, x, T):
            x.Value = T("2")
            return True
        def RefParam(self, x, T):
            x.Value = x.Value + T("10")
        def Wild(self, *args, **kwargs):
            self.args = args
            self.kwargs = kwargs
            self.args[2].Value = kwargs['T2']('1.5')
            return self.args[3][0]
    
    c = MyGenericMethods()
    AreEqual(GenericMethodTester.TestIntFactory0(c), 123)
    AreEqual(c.type, 'System.Int32')
    
    AreEqual(GenericMethodTester.TestStringFactory1(c, "789"), "456789")
    AreEqual(c.type, 'System.String')
    
    AreEqual(GenericMethodTester.TestIntFactory1(c, 321), 777)
    AreEqual(c.type, 'System.Int32')
    
    AreEqual(GenericMethodTester.TestStringFactory0(c), '123')
    AreEqual(c.type, 'System.String')
    
    AreEqual(GenericMethodTester.TestOutParamString(c), '2')
    AreEqual(GenericMethodTester.TestOutParamInt(c), 2)
    
    AreEqual(GenericMethodTester.TestRefParamString(c, '10'), '1010')
    AreEqual(GenericMethodTester.TestRefParamInt(c, 10), 20)
    
    x = System.Collections.Generic.List[int]((2, 3, 4))
    r = GenericMethodTester.GoWild(c, True, 'second', x)
    AreEqual(r.Length, 2)
    AreEqual(r[0], 1.5)

def test_bases():
    #
    # Verify that changing __bases__ works
    #
    
    class MyExceptionComparer(System.Exception, System.Collections.IComparer):
        def Compare(self, x, y): return 0
    class MyDerivedExceptionComparer(MyExceptionComparer): pass
    
    e = MyExceptionComparer()
   
    MyDerivedExceptionComparer.__bases__ = (System.Exception, System.Collections.IComparer)
    MyDerivedExceptionComparer.__bases__ = (MyExceptionComparer,)
    
    class OldType:
        def OldTypeMethod(self): return "OldTypeMethod"
    class NewType:
        def NewTypeMethod(self): return "NewTypeMethod"
    class MyOtherExceptionComparer(System.Exception, System.Collections.IComparer, OldType, NewType):
        def Compare(self, x, y): return 0
    MyExceptionComparer.__bases__ = MyOtherExceptionComparer.__bases__
    AreEqual(e.OldTypeMethod(), "OldTypeMethod")
    AreEqual(e.NewTypeMethod(), "NewTypeMethod")
    Assert(isinstance(e, System.Exception))
    Assert(isinstance(e, System.Collections.IComparer))
    Assert(isinstance(e, MyExceptionComparer))
    
    class MyIncompatibleExceptionComparer(System.Exception, System.Collections.IComparer, System.IDisposable):
        def Compare(self, x, y): return 0
        def Displose(self): pass
    if not is_silverlight:
        AssertErrorWithMatch(TypeError, "__bases__ assignment: 'MyExceptionComparer' object layout differs from 'IronPython.NewTypes.System.Exception#IComparer#IDisposable_*",
                             setattr, MyExceptionComparer, "__bases__", MyIncompatibleExceptionComparer.__bases__)
        AssertErrorWithMatch(TypeError, "__class__ assignment: 'MyExceptionComparer' object layout differs from 'IronPython.NewTypes.System.Exception#IComparer#IDisposable_*",
                             setattr, MyExceptionComparer(), "__class__", MyIncompatibleExceptionComparer().__class__)
    else:
        try:
            setattr(MyExceptionComparer, "__bases__", MyIncompatibleExceptionComparer.__bases__)
        except TypeError as e:
            Assert(e.args[0].startswith("__bases__ assignment: 'MyExceptionComparer' object layout differs from 'IronPython.NewTypes.System.Exception#IComparer#IDisposable_"))
        
        try:
            setattr(MyExceptionComparer(), "__class__", MyIncompatibleExceptionComparer().__class__)
        except TypeError as e:
            Assert(e.args[0].startswith("__class__ assignment: 'MyExceptionComparer' object layout differs from 'IronPython.NewTypes.System.Exception#IComparer#IDisposable_"))


def test_open_generic():    
    # Inherting from an open generic instantiation should fail with a good error message
    try:
        class Foo(System.Collections.Generic.IEnumerable): pass
    except TypeError:
        (exc_type, exc_value, exc_traceback) = sys.exc_info()
        Assert(exc_value.message.__contains__("cannot inhert from open generic instantiation"))

def test_interface_slots():
    
    # slots & interfaces
    class foo(object):
        __slots__ = ['abc']
    
    class bar(foo, System.IComparable):
        def CompareTo(self, other):
                return 23
    
    class baz(bar): pass

def test_op_Implicit_inheritance():
    """should inherit op_Implicit from base classes"""
    a = NewClass()
    AreEqual(int(a), 1002)
    AreEqual(int(a), 1002)
    AreEqual(NewClass.op_Implicit(a), 1002)

def test_symbol_dict():
    """tests to verify that Symbol dictionaries do the right thing in dynamic scenarios
    same as the tests in test_class, but we run this in a module that has imported clr"""
    
    def CheckDictionary(C): 
        # add a new attribute to the type...
        C.newClassAttr = 'xyz'
        AreEqual(C.newClassAttr, 'xyz')
        
        # add non-string index into the class and instance dictionary        
        a = C()
        try:
            a.__dict__[1] = '1'
            C.__dict__[2] = '2'
            AreEqual(1 in a.__dict__, True)
            AreEqual(2 in C.__dict__, True)
            AreEqual(dir(a).__contains__(1), True)
            AreEqual(dir(a).__contains__(2), True)
            AreEqual(dir(C).__contains__(2), True)
            AreEqual(repr(a.__dict__), "{1: '1'}")
            AreEqual(repr(C.__dict__).__contains__("2: '2'"), True)
        except TypeError:
            # new-style classes have dict-proxy, can't do the assignment
            pass 
        
        # replace a class dictionary (containing non-string keys) w/ a normal dictionary
        C.newTypeAttr = 1
        AreEqual(hasattr(C, 'newTypeAttr'), True)
        
        class OldClass: pass
        
        if isinstance(C, type(OldClass)):
            C.__dict__ = dict(C.__dict__)  
            AreEqual(hasattr(C, 'newTypeAttr'), True)
        else:
            try:
                C.__dict__ = {}
                AssertUnreachable()
            except AttributeError:
                pass
        
        # replace an instance dictionary (containing non-string keys) w/ a new one.
        a.newInstanceAttr = 1
        AreEqual(hasattr(a, 'newInstanceAttr'), True)
        a.__dict__  = dict(a.__dict__)
        AreEqual(hasattr(a, 'newInstanceAttr'), True)
    
        a.abc = 'xyz'  
        AreEqual(hasattr(a, 'abc'), True)
        AreEqual(getattr(a, 'abc'), 'xyz')
        
    
    class OldClass: 
        def __init__(self):  pass
    
    class NewClass(object): 
        def __init__(self):  pass
    
    CheckDictionary(OldClass)
    CheckDictionary(NewClass)

def test_generic_TypeGroup():
    # TypeGroup is used to expose "System.IComparable" and "System.IComparable`1" as "System.IComparable"
    
    # repr
    AreEqual(repr(System.IComparable), "<types 'IComparable', 'IComparable[T]'>")

    # Test member access
    AreEqual(System.IComparable.CompareTo(1,1), 0)
    AreEqual(System.IComparable.CompareTo(1,2), -1)
    AreEqual(System.IComparable[int].CompareTo(1,1), 0)
    AreEqual(System.IComparable[int].CompareTo(1,2), -1)
    Assert(dir(System.IComparable).__contains__("CompareTo"))
    Assert(list(vars(System.IComparable).keys()).__contains__("CompareTo"))

    import IronPythonTest
    genericTypes = IronPythonTest.NestedClass.InnerGenericClass
    
    # IsAssignableFrom is SecurityCritical and thus cannot be called via reflection in silverlight,
    # so disable this in interpreted mode.
    if not (is_silverlight):
        # converstion to Type
        Assert(System.Type.IsAssignableFrom(System.IComparable, int))
        AssertError(TypeError, System.Type.IsAssignableFrom, object, genericTypes)

    # Test illegal type instantiation
    try:
        System.IComparable[int, int]
    except ValueError: pass
    else: AssertUnreachable()

    try:
        System.EventHandler(None)
    except TypeError: pass
    else: AssertUnreachable()
    
    def handler():
        pass

    try:
        System.EventHandler(handler)("sender", None)
    except TypeError: pass
    else: AssertUnreachable()    
        
    def handler(s,a):
        pass

    # Test constructor
    if not is_silverlight:
        # GetType is SecurityCritical; can't call via reflection on silverlight
        AreEqual(System.EventHandler(handler).GetType(), System.Type.GetType("System.EventHandler"))
        
        # GetGenericTypeDefinition is SecuritySafe, can't call on Silverlight.
        AreEqual(System.EventHandler[System.EventArgs](handler).GetType().GetGenericTypeDefinition(), System.Type.GetType("System.EventHandler`1"))
    
    # Test inheritance
    class MyComparable(System.IComparable):
        def CompareTo(self, other):
            return self.Equals(other)
    myComparable = MyComparable()
    Assert(myComparable.CompareTo(myComparable))
    
    try:
        class MyDerivedClass(genericTypes): pass
    except TypeError: pass
    else: AssertUnreachable()
    
    # Use a TypeGroup to index a TypeGroup
    t = genericTypes[System.IComparable]
    t = genericTypes[System.IComparable, int]
    try:
        System.IComparable[genericTypes]
    except TypeError: pass
    else: AssertUnreachable()

def test_generic_only_TypeGroup():
    try:
        BinderTest.GenericOnlyConflict()
    except System.TypeLoadException as e:
        Assert(str(e).find('requires a non-generic type') != -1)
        Assert(str(e).find('GenericOnlyConflict') != -1)

def test_autodoc():
    from System.Threading import Thread, ThreadStart
    
    Assert(Thread.__doc__.find('Thread(start: ThreadStart)') != -1)
    
    #Assert(Thread.__new__.__doc__.find('__new__(cls, ThreadStart start)') != -1)
    
    #AreEqual(Thread.__new__.Overloads[ThreadStart].__doc__, '__new__(cls, ThreadStart start)' + newline)
    

#IronPythonTest.TypeDescTests is not available for silverlight
@skip("silverlight")    
def test_type_descs():
    test = TypeDescTests()
    
    # new style tests
    
    class bar(int): pass
    b = bar(2)
    
    class foo(object): pass
    c = foo()
    
    
    #test.TestProperties(...)
    
    res = test.GetClassName(test)
    Assert(res == 'IronPythonTest.TypeDescTests')
    
    #res = test.GetClassName(a)
    #Assert(res == 'list')
    
    
    res = test.GetClassName(c)
    Assert(res == 'foo')
    
    res = test.GetClassName(b)
    Assert(res == 'bar')
    
    res = test.GetConverter(b)
    x = res.ConvertTo(None, None, b, int)
    Assert(x == 2)
    Assert(type(x) == int)
    
    x = test.GetDefaultEvent(b)
    Assert(x == None)
    
    x = test.GetDefaultProperty(b)
    Assert(x == None)
    
    x = test.GetEditor(b, object)
    Assert(x == None)
    
    x = test.GetEvents(b)
    Assert(x.Count == 0)
    
    x = test.GetEvents(b, None)
    Assert(x.Count == 0)
    
    x = test.GetProperties(b)
    Assert(x.Count > 0)
    
    Assert(test.TestProperties(b, [], []))
    bar.foobar = property(lambda x: 42)
    Assert(test.TestProperties(b, ['foobar'], []))
    bar.baz = property(lambda x:42)
    Assert(test.TestProperties(b, ['foobar', 'baz'], []))
    delattr(bar, 'baz')
    Assert(test.TestProperties(b, ['foobar'], ['baz']))
    # Check that adding a non-string entry in the dictionary does not cause any grief.
    b.__dict__[1] = 1;
    Assert(test.TestProperties(b, ['foobar'], ['baz']))
    
    #Assert(test.TestProperties(test, ['GetConverter', 'GetEditor', 'GetEvents', 'GetHashCode'] , []))
    
    
    # old style tests
    
    class foo: pass
    
    a = foo()
    
    Assert(test.TestProperties(a, [], []))
    
    
    res = test.GetClassName(a)
    Assert(res == 'foo')
    
    
    x = test.CallCanConvertToForInt(a)
    Assert(x == False)
    
    x = test.GetDefaultEvent(a)
    Assert(x == None)
    
    x = test.GetDefaultProperty(a)
    Assert(x == None)
    
    x = test.GetEditor(a, object)
    Assert(x == None)
    
    x = test.GetEvents(a)
    Assert(x.Count == 0)
    
    x = test.GetEvents(a, None)
    Assert(x.Count == 0)
    
    x = test.GetProperties(a, (System.ComponentModel.BrowsableAttribute(True), ))
    Assert(x.Count == 0)
    
    foo.bar = property(lambda x:'hello')
    
    Assert(test.TestProperties(a, ['bar'], []))
    delattr(foo, 'bar')
    Assert(test.TestProperties(a, [], ['bar']))
    
    a = a.__class__
    
    Assert(test.TestProperties(a, [], []))
    
    foo.bar = property(lambda x:'hello')
    
    Assert(test.TestProperties(a, [], []))
    delattr(a, 'bar')
    Assert(test.TestProperties(a, [], ['bar']))
    
    x = test.GetClassName(a)
    AreEqual(x, 'classobj')
    
    x = test.CallCanConvertToForInt(a)
    AreEqual(x, False)
    
    x = test.GetDefaultEvent(a)
    AreEqual(x, None)
    
    x = test.GetDefaultProperty(a)
    AreEqual(x, None)
    
    x = test.GetEditor(a, object)
    AreEqual(x, None)
    
    x = test.GetEvents(a)
    AreEqual(x.Count, 0)
    
    x = test.GetEvents(a, None)
    AreEqual(x.Count, 0)
    
    x = test.GetProperties(a)
    Assert(x.Count > 0)
    
    # Ensure GetProperties checks the attribute dictionary
    a = foo()
    a.abc = 42
    x = test.GetProperties(a)
    for prop in x:
        if prop.Name == 'abc':
            break
    else:
        AssertUnreachable()

#silverlight does not support System.Char.Parse
@skip("silverlight")
def test_char():
    for x in range(256):
        c = System.Char.Parse(chr(x))
        AreEqual(c, chr(x))
        AreEqual(chr(x), c)
        
        if c == chr(x): pass
        else: Assert(False)
        
        if not c == chr(x): Assert(False)
        
        if chr(x) == c: pass
        else: Assert(False)
        
        if not chr(x) == c: Assert(False)

def test_repr():
    if not is_silverlight:
        clr.AddReference('System.Drawing')
    
        from System.Drawing import Point
    
        AreEqual(repr(Point(1,2)).startswith('<System.Drawing.Point object'), True)
        AreEqual(repr(Point(1,2)).endswith('[{X=1,Y=2}]>'),True)
    
    # these 3 classes define the same repr w/ different \r, \r\n, \n versions
    a = UnaryClass(3)
    b = BaseClass()
    c = BaseClassStaticConstructor()
    
    ra = repr(a)
    rb = repr(b)
    rc = repr(c)
    
    sa = ra.find('HelloWorld')
    sb = rb.find('HelloWorld')
    sc = rc.find('HelloWorld')
    
    AreEqual(ra[sa:sa+13], rb[sb:sb+13])
    AreEqual(rb[sb:sb+13], rc[sc:sc+13])
    AreEqual(ra[sa:sa+13], 'HelloWorld...') # \r\n should be removed, replaced with ...

def test_explicit_interfaces():
    otdc = OverrideTestDerivedClass()
    AreEqual(otdc.MethodOverridden(), "OverrideTestDerivedClass.MethodOverridden() invoked")
    AreEqual(IOverrideTestInterface.MethodOverridden(otdc), 'IOverrideTestInterface.MethodOverridden() invoked')

    AreEqual(IOverrideTestInterface.x.GetValue(otdc), 'IOverrideTestInterface.x invoked')
    AreEqual(IOverrideTestInterface.y.GetValue(otdc), 'IOverrideTestInterface.y invoked')
    IOverrideTestInterface.x.SetValue(otdc, 'abc')
    AreEqual(OverrideTestDerivedClass.Value, 'abcx')
    IOverrideTestInterface.y.SetValue(otdc, 'abc')
    AreEqual(OverrideTestDerivedClass.Value, 'abcy')
    
    AreEqual(otdc.y, 'OverrideTestDerivedClass.y invoked')

    AreEqual(IOverrideTestInterface.Method(otdc), "IOverrideTestInterface.method() invoked")
    
    AreEqual(hasattr(otdc, 'IronPythonTest_IOverrideTestInterface_x'), False)
    
    # we can also do this the ugly way:
    
    AreEqual(IOverrideTestInterface.x.__get__(otdc, OverrideTestDerivedClass), 'IOverrideTestInterface.x invoked')
    AreEqual(IOverrideTestInterface.y.__get__(otdc, OverrideTestDerivedClass), 'IOverrideTestInterface.y invoked')

    AreEqual(IOverrideTestInterface.__getitem__(otdc, 2), 'abc')
    AreEqual(IOverrideTestInterface.__getitem__(otdc, 2), 'abc')
    AssertError(NotImplementedError, IOverrideTestInterface.__setitem__, otdc, 2, 3)
    try:
        IOverrideTestInterface.__setitem__(otdc, 2, 3)
    except NotImplementedError: pass
    else: AssertUnreachable()

def test_field_helpers():
    otdc = OverrideTestDerivedClass()
    OverrideTestDerivedClass.z.SetValue(otdc, 'abc')
    AreEqual(otdc.z, 'abc')
    AreEqual(OverrideTestDerivedClass.z.GetValue(otdc), 'abc')

def test_field_descriptor():
    AreEqual(MySize.width.__get__(MySize()), 0)
    AreEqual(MySize.width.__get__(MySize(), MySize), 0)

def test_field_const_write():
    try:
        MySize.MaxSize = 23
    except AttributeError as e:
        Assert(str(e).find('MaxSize') != -1)
        Assert(str(e).find('MySize') != -1)

    try:
        ClassWithLiteral.Literal = 23
    except AttributeError as e:
        Assert(str(e).find('Literal') != -1)
        Assert(str(e).find('ClassWithLiteral') != -1)

    try:
        ClassWithLiteral.__dict__['Literal'].__set__(None, 23)
    except AttributeError as e:
        Assert(str(e).find('int') != -1)

    try:
        ClassWithLiteral.__dict__['Literal'].__set__(ClassWithLiteral(), 23)
    except AttributeError as e:
        Assert(str(e).find('int') != -1)

    try:
        MySize().MaxSize = 23
    except AttributeError as e:
        Assert(str(e).find('MaxSize') != -1)
        Assert(str(e).find('MySize') != -1)

    try:
        ClassWithLiteral().Literal = 23
    except AttributeError as e:
        Assert(str(e).find('Literal') != -1)
        Assert(str(e).find('ClassWithLiteral') != -1)

def test_field_const_access():
    AreEqual(MySize().MaxSize, System.Int32.MaxValue)
    AreEqual(MySize.MaxSize, System.Int32.MaxValue)
    AreEqual(ClassWithLiteral.Literal, 5)
    AreEqual(ClassWithLiteral().Literal, 5)

def test_array():
    arr = System.Array[int]([0])
    AreEqual(repr(arr), str(arr))
    AreEqual(repr(System.Array[int]([0, 1])), 'Array[int]((0, 1))')


def test_strange_inheritance():
    """verify that overriding strange methods (such as those that take caller context) doesn't
       flow caller context through"""
    class m(StrangeOverrides):
        def SomeMethodWithContext(self, arg):
            AreEqual(arg, 'abc')
        def ParamsMethodWithContext(self, *arg):
            AreEqual(arg, ('abc', 'def'))
        def ParamsIntMethodWithContext(self, *arg):
            AreEqual(arg, (2,3))

    a = m()
    a.CallWithContext('abc')
    a.CallParamsWithContext('abc', 'def')
    a.CallIntParamsWithContext(2, 3)   

#lib.process_util, file, etc are not available in silverlight
@skip("silverlight")
def test_nondefault_indexers():
    from iptest.process_util import *

    if not has_vbc(): return
    import os
    import _random
    
    r = _random.Random()
    r.seed()
    f = file('vbproptest1.vb', 'w')
    try:
        f.write("""
Public Class VbPropertyTest
private Indexes(23) as Integer
private IndexesTwo(23,23) as Integer
private shared SharedIndexes(5,5) as Integer

Public Property IndexOne(ByVal index as Integer) As Integer
    Get
        return Indexes(index)
    End Get
    Set
        Indexes(index) = Value
    End Set
End Property

Public Property IndexTwo(ByVal index as Integer, ByVal index2 as Integer) As Integer
    Get
        return IndexesTwo(index, index2)
    End Get
    Set
        IndexesTwo(index, index2) = Value
    End Set
End Property

Public Shared Property SharedIndex(ByVal index as Integer, ByVal index2 as Integer) As Integer
    Get
        return SharedIndexes(index, index2)
    End Get
    Set
        SharedIndexes(index, index2) = Value
    End Set
End Property
End Class        
    """)
        f.close()
        
        name = path_combine(testpath.temporary_dir, 'vbproptest%f.dll' % (r.random()))
        x = run_vbc('/target:library vbproptest1.vb "/out:%s"' % name)        
        AreEqual(x, 0)
        
        clr.AddReferenceToFileAndPath(name)
        import VbPropertyTest
        
        x = VbPropertyTest()
        AreEqual(x.IndexOne[0], 0)
        x.IndexOne[1] = 23
        AreEqual(x.IndexOne[1], 23)
        
        AreEqual(x.IndexTwo[0,0], 0)
        x.IndexTwo[1,2] = 5
        AreEqual(x.IndexTwo[1,2], 5)
        
        AreEqual(VbPropertyTest.SharedIndex[0,0], 0)
        VbPropertyTest.SharedIndex[3,4] = 42
        AreEqual(VbPropertyTest.SharedIndex[3,4], 42)
    finally:
        if not f.closed: f.close()
              
        os.unlink('vbproptest1.vb')

@skip("silverlight")
def test_nondefault_indexers_overloaded():
    from iptest.process_util import *

    if not has_vbc(): return
    import os
    import _random
    
    r = _random.Random()
    r.seed()
    f = file('vbproptest1.vb', 'w')
    try:
        f.write("""
Public Class VbPropertyTest
private Indexes(23) as Integer
private IndexesTwo(23,23) as Integer
private shared SharedIndexes(5,5) as Integer

Public Property IndexOne(ByVal index as Integer) As Integer
    Get
        return Indexes(index)
    End Get
    Set
        Indexes(index) = Value
    End Set
End Property

Public Property IndexTwo(ByVal index as Integer, ByVal index2 as Integer) As Integer
    Get
        return IndexesTwo(index, index2)
    End Get
    Set
        IndexesTwo(index, index2) = Value
    End Set
End Property

Public Shared Property SharedIndex(ByVal index as Integer, ByVal index2 as Integer) As Integer
    Get
        return SharedIndexes(index, index2)
    End Get
    Set
        SharedIndexes(index, index2) = Value
    End Set
End Property
End Class

Public Class VbPropertyTest2
Public ReadOnly Property Prop() As string
    get 
        return "test"
    end get
end property

Public ReadOnly Property Prop(ByVal name As String) As string
    get
        return name
    end get
end property

End Class
    """)
        f.close()
        
        name = path_combine(testpath.temporary_dir, 'vbproptest%f.dll' % (r.random()))
        AreEqual(run_vbc('/target:library vbproptest1.vb /out:"%s"' % name), 0)

        clr.AddReferenceToFileAndPath(name)
        import VbPropertyTest, VbPropertyTest2
        
        x = VbPropertyTest()
        AreEqual(x.IndexOne[0], 0)
        x.IndexOne[1] = 23
        AreEqual(x.IndexOne[1], 23)
        
        AreEqual(x.IndexTwo[0,0], 0)
        x.IndexTwo[1,2] = 5
        AreEqual(x.IndexTwo[1,2], 5)
        
        AreEqual(VbPropertyTest.SharedIndex[0,0], 0)
        VbPropertyTest.SharedIndex[3,4] = 42
        AreEqual(VbPropertyTest.SharedIndex[3,4], 42)
        
        AreEqual(VbPropertyTest2().Prop, 'test')
        AreEqual(VbPropertyTest2().get_Prop('foo'), 'foo')
    finally:
        if not f.closed: f.close()
              
        os.unlink('vbproptest1.vb')
        
def test_interface_abstract_events():
    # inherit from an interface or abstract event, and define the event
    for baseType in [IEventInterface, AbstractEvent]:
        class foo(baseType):
            def __init__(self):
                self._events = []            
            def add_MyEvent(self, value):
                AreEqual(type(value), SimpleDelegate)
                self._events.append(value)
            def remove_MyEvent(self, value):
                AreEqual(type(value), SimpleDelegate)
                self._events.remove(value)
            def MyRaise(self):
                for x in self._events: x()
    
        global called
        called = False
        def bar(*args): 
            global called
            called = True
    
        a = foo()
        
        a.MyEvent += bar
        a.MyRaise()
        AreEqual(called, True)
        
        a.MyEvent -= bar        
        called = False        
        a.MyRaise()        
        AreEqual(called, False)
        
        # hook the event from the CLI side, and make sure that raising
        # it causes the CLI side to see the event being fired.
        UseEvent.Hook(a)
        a.MyRaise()
        AreEqual(UseEvent.Called, True)
        UseEvent.Called = False
        UseEvent.Unhook(a)
        a.MyRaise()
        AreEqual(UseEvent.Called, False)

@disabled("Merlin 177188: Fail in Orcas")
def test_dynamic_assembly_ref():
    # verify we can add a reference to a dynamic assembly, and
    # then create an instance of that type
    class foo(object): pass

    clr.AddReference(foo().GetType().Assembly)
    import IronPython.NewTypes.System
    for x in dir(IronPython.NewTypes.System):
        if x.startswith('Object_'):
            t = getattr(IronPython.NewTypes.System, x)            
            x = t(foo)
            break
    else:
        # we should have found our type
        AssertUnreachable()

def test_nonzero():
    from System import Single, Byte, SByte, Int16, UInt16, Int64, UInt64
    for t in [Single, Byte, SByte, Int16, UInt16, Int64, UInt64]:
        Assert(hasattr(t, '__nonzero__'))
        if t(0): AssertUnreachable()
        if not t(1): AssertUnreachable()

def test_virtual_event():
    # inherit from a class w/ a virtual event and a
    # virtual event that's been overridden.  Check both
    # overriding it and not overriding it.
    for baseType in [VirtualEvent, OverrideVirtualEvent]:
        for override in [True, False]:
            class foo(baseType):
                def __init__(self):
                    self._events = []            
                if override:
                    def add_MyEvent(self, value):
                        AreEqual(type(value), SimpleDelegate)
                        self._events.append(value)
                    def remove_MyEvent(self, value):
                        AreEqual(type(value), SimpleDelegate)
                        self._events.remove(value)
                    def add_MyCustomEvent(self, value): pass
                    def remove_MyCustomEvent(self, value): pass
                    def MyRaise(self):
                        for x in self._events: x()
                else:
                    def MyRaise(self):
                        self.FireEvent()                    

            # normal event
            global called
            called = False
            def bar(*args): 
                global called
                called = True
                        
            a = foo()
            a.MyEvent += bar
            a.MyRaise()
                
            AreEqual(called, True)
            
            a.MyEvent -= bar
            
            called = False            
            a.MyRaise()            
            AreEqual(called, False)
        
            # custom event
            a.LastCall = None
            a = foo()
            a.MyCustomEvent += bar
            if override: AreEqual(a.LastCall, None)
            else: Assert(a.LastCall.endswith('Add'))
            
            a.Lastcall = None            
            a.MyCustomEvent -= bar
            if override: AreEqual(a.LastCall, None)
            else: Assert(a.LastCall.endswith('Remove'))


            # hook the event from the CLI side, and make sure that raising
            # it causes the CLI side to see the event being fired.
            UseEvent.Hook(a)
            a.MyRaise()
            AreEqual(UseEvent.Called, True)
            UseEvent.Called = False
            UseEvent.Unhook(a)
            a.MyRaise()
            AreEqual(UseEvent.Called, False)

@skip("silverlight")
def test_property_get_set():
    clr.AddReference("System.Drawing")
    from System.Drawing import Size
    
    temp = Size()
    AreEqual(temp.Width, 0)
    temp.Width = 5
    AreEqual(temp.Width, 5)
        
    for i in range(5):
        temp.Width = i
        AreEqual(temp.Width, i)    

def test_write_only_property_set():
    from IronPythonTest import WriteOnly
    obj = WriteOnly()
    
    AssertError(AttributeError, getattr, obj, 'Writer')

def test_isinstance_interface():
    Assert(isinstance('abc', System.Collections.IEnumerable))

def test_constructor_function():
    '''
    Test to hit IronPython.Runtime.Operations.ConstructionFunctionOps.
    '''
    
    AreEqual(System.DateTime.__new__.__name__, '__new__')
    Assert(System.DateTime.__new__.__doc__.find('__new__(cls: type, year: int, month: int, day: int)') != -1)
                
    if not is_silverlight:
        Assert(System.AssemblyLoadEventArgs.__new__.__doc__.find('__new__(cls: type, loadedAssembly: Assembly)') != -1)

def test_class_property():
    """__class__ should work on standard .NET types and should return the type object associated with that class"""
    AreEqual(System.Environment.Version.__class__, System.Version)

def test_null_str():
    """if a .NET type has a bad ToString() implementation that returns null always return String.Empty in Python"""
    AreEqual(str(RudeObjectOverride()), '')
    AreEqual(RudeObjectOverride().__str__(), '')
    AreEqual(RudeObjectOverride().ToString(), None)
    Assert(repr(RudeObjectOverride()).startswith('<IronPythonTest.RudeObjectOverride object at '))

def test_keyword_construction_readonly():
    # Build is read-only property
    AssertError(AttributeError, System.Version, 1, 0, Build=100)  
    AssertError(AttributeError, ClassWithLiteral, Literal=3)

@skip("silverlight") # no FileSystemWatcher in Silverlight
def test_kw_construction_types():
    for val in [True, False]:
        x = System.IO.FileSystemWatcher('.', EnableRaisingEvents = val)
        AreEqual(x.EnableRaisingEvents, val)

def test_as_bool():
    """verify using expressions in if statements works correctly.  This generates an
    site whose return type is bool so it's important this works for various ways we can
    generate the body of the site, and use the site both w/ the initial & generated targets"""
    clr.AddReference('System') # ensure test passes in ipt
    
    # instance property
    x = System.Uri('http://foo')
    for i in range(2):
        if x.AbsolutePath: pass
        else: AssertUnreachable()
    
    # instance property on type
    for i in range(2):
        if System.Uri.AbsolutePath: pass
        else: AssertUnreachable()
    
    # static property
    for i in range(2):
        if System.Threading.Thread.CurrentThread: pass
        else: AssertUnreachable()
    
    # static field
    for i in range(2):
        if System.String.Empty: AssertUnreachable()
    
    # instance field
    x = NestedClass()
    for i in range(2):
        if x.Field: AssertUnreachable()        
    
    # instance field on type
    for i in range(2):
        if NestedClass.Field: pass
        else: AssertUnreachable()
    
    # math
    for i in range(2):
        if System.Int64(1) + System.Int64(1): pass
        else: AssertUnreachable()

    for i in range(2):
        if System.Int64(1) + System.Int64(1): pass
        else: AssertUnreachable()
    
    # GetItem
    x = System.Collections.Generic.List[str]()
    x.Add('abc')
    for i in range(2):
        if x[0]: pass
        else: AssertUnreachable()
        
    
    
@skip("silverlight") # no Stack on Silverlight
def test_generic_getitem():
    # calling __getitem__ is the same as indexing
    AreEqual(System.Collections.Generic.Stack.__getitem__(int), System.Collections.Generic.Stack[int])
    
    # but __getitem__ on a type takes precedence
    AssertError(TypeError, System.Collections.Generic.List.__getitem__, int)
    x = System.Collections.Generic.List[int]()
    x.Add(0)
    AreEqual(System.Collections.Generic.List[int].__getitem__(x, 0), 0)
    
    # but we can call type.__getitem__ with the instance    
    AreEqual(type.__getitem__(System.Collections.Generic.List, int), System.Collections.Generic.List[int])
    

@skip("silverlight") # no WinForms on Silverlight
def test_multiple_inheritance():
    """multiple inheritance from two types in the same hierarchy should work, this is similar to class foo(int, object)"""
    clr.AddReference("System.Windows.Forms")
    class foo(System.Windows.Forms.Form, System.Windows.Forms.Control): pass
    
def test_struct_no_ctor_kw_args():
    for x in range(2):
        s = Structure(a=3)
        AreEqual(s.a, 3)

def test_nullable_new():
    from System import Nullable
    AreEqual(clr.GetClrType(Nullable[()]).IsGenericType, False)

def test_ctor_keyword_args_newslot():
    """ctor keyword arg assignment contruction w/ new slot properties"""
    x = BinderTest.KeywordDerived(SomeProperty = 'abc')
    AreEqual(x.SomeProperty, 'abc')

    x = BinderTest.KeywordDerived(SomeField = 'abc')
    AreEqual(x.SomeField, 'abc')

def test_enum_truth():
    # zero enums are false, non-zero enums are true
    Assert(not System.StringSplitOptions.None)
    Assert(System.StringSplitOptions.RemoveEmptyEntries)
    AreEqual(System.StringSplitOptions.None.__nonzero__(), False)
    AreEqual(System.StringSplitOptions.RemoveEmptyEntries.__nonzero__(), True)

def test_enum_repr():
    clr.AddReference('IronPython')
    from IronPython.Runtime import ModuleOptions
    AreEqual(repr(ModuleOptions.WithStatement), 'IronPython.Runtime.ModuleOptions.WithStatement')
    AreEqual(repr(ModuleOptions.WithStatement | ModuleOptions.TrueDivision), 
             '<enum IronPython.Runtime.ModuleOptions: TrueDivision, WithStatement>')
    
def test_bad_inheritance():
    """verify a bad inheritance reports the type name you're inheriting from"""
    def f(): 
        class x(System.Single): pass
    def g(): 
        class x(System.Version): pass
    
    AssertErrorWithPartialMessage(TypeError, 'System.Single', f)
    AssertErrorWithPartialMessage(TypeError, 'System.Version', g)

def test_disposable():
    """classes implementing IDisposable should automatically support the with statement"""
    x = DisposableTest()
    
    with x:
        pass
        
    AreEqual(x.Called, True)
    
    Assert(hasattr(x, '__enter__'))
    Assert(hasattr(x, '__exit__'))

    x = DisposableTest()
    x.__enter__()
    try:
        pass
    finally:
        AreEqual(x.__exit__(None, None, None), None)
    
    AreEqual(x.Called, True)
    
    Assert('__enter__' in dir(x))
    Assert('__exit__' in dir(x))
    Assert('__enter__' in dir(DisposableTest))
    Assert('__exit__' in dir(DisposableTest))

def test_dbnull():
    """DBNull should not be true"""
    if System.DBNull.Value:
        AssertUnreachable()


def test_special_repr():
    list = System.Collections.Generic.List[object]()
    AreEqual(repr(list), 'List[object]()')
    
    list.Add('abc')    
    AreEqual(repr(list), "List[object](['abc'])")
    
    list.Add(2)
    AreEqual(repr(list), "List[object](['abc', 2])")
    
    list.Add(list)
    AreEqual(repr(list), "List[object](['abc', 2, [...]])")
    
    dict = System.Collections.Generic.Dictionary[object, object]()
    AreEqual(repr(dict), "Dictionary[object, object]()")
    
    dict["abc"] = "def"
    AreEqual(repr(dict), "Dictionary[object, object]({'abc' : 'def'})")
    
    dict["two"] = "def"
    Assert(repr(dict) == "Dictionary[object, object]({'abc' : 'def', 'two' : 'def'})" or
           repr(dict) == "Dictionary[object, object]({'two' : 'def', 'def' : 'def'})")
           
    dict = System.Collections.Generic.Dictionary[object, object]()
    dict['abc'] = dict
    AreEqual(repr(dict), "Dictionary[object, object]({'abc' : {...}})")

    dict = System.Collections.Generic.Dictionary[object, object]()
    dict[dict] = 'abc'
    
    AreEqual(repr(dict), "Dictionary[object, object]({{...} : 'abc'})")

def test_issubclass():    
    Assert(issubclass(int, clr.GetClrType(int)))

def test_explicit_interface_impl():
    noConflict = ExplicitTestNoConflict()
    oneConflict = ExplicitTestOneConflict()
    
    AreEqual(noConflict.A(), "A")
    AreEqual(noConflict.B(), "B")
    Assert(hasattr(noConflict, "A"))
    Assert(hasattr(noConflict, "B"))
    
    AssertError(AttributeError, lambda : oneConflict.A())
    AreEqual(oneConflict.B(), "B")
    Assert(not hasattr(oneConflict, "A"))
    Assert(hasattr(oneConflict, "B"))
    
@skip("silverlight") # no ArrayList on Silverlight
def test_interface_isinstance():
    l = System.Collections.ArrayList()
    AreEqual(isinstance(l, System.Collections.IList), True)

@skip("silverlight") # no serialization support in Silverlight
def test_serialization():
    """
    TODO:
    - this should become a test module in and of itself
    - way more to test here..
    """
    
    import pickle
    
    # test the primitive data types...    
    data = [1, 1.0, 2j, 2, System.Int64(1), System.UInt64(1), 
            System.UInt32(1), System.Int16(1), System.UInt16(1), 
            System.Byte(1), System.SByte(1), System.Decimal(1),
            System.Char.MaxValue, System.DBNull.Value, System.Single(1.0),
            System.DateTime.Now, None, {}, (), [], {'a': 2}, (42, ), [42, ],
            System.StringSplitOptions.RemoveEmptyEntries,
            ]
    
    data.append(list(data))     # list of all the data..
    data.append(tuple(data))    # tuple of all the data...
    
    class X:
        def __init__(self):
            self.abc = 3
    
    class Y(object):
        def __init__(self):
            self.abc = 3

    # instance dictionaries...
    data.append(X().__dict__)
    data.append(Y().__dict__)

    # recursive list
    l = []
    l.append(l)
    data.append(l)
    
    # dict of all the data
    d = {}
    cnt = 100
    for x in data:
        d[cnt] = x
        cnt += 1
        
    data.append(d)
    
    # recursive dict...
    d1 = {}
    d2 = {}
    
    d1['abc'] = d2
    d1['foo'] = 'baz'
    d2['abc'] = d1
    
    data.append(d1)
    data.append(d2)
    
    for value in data:
        # use cPickle & clr.Serialize/Deserialize directly
        for newVal in (pickle.loads(pickle.dumps(value)), clr.Deserialize(*clr.Serialize(value))):
            AreEqual(type(newVal), type(value))
            try:
                AreEqual(newVal, value)
            except RuntimeError as e:
                # we hit one of our recursive structures...
                AreEqual(e.message, "maximum recursion depth exceeded in cmp")
                Assert(type(newVal) is list or type(newVal) is dict)
    
    # passing an unknown format raises...
    AssertError(ValueError, clr.Deserialize, "unknown", "foo")
    
    al = System.Collections.ArrayList()
    al.Add(2)
    
    gl = System.Collections.Generic.List[int]()
    gl.Add(2)
    
    # lists...
    for value in (al, gl):
        for newX in (pickle.loads(pickle.dumps(value)), clr.Deserialize(*clr.Serialize(value))):
            AreEqual(value.Count, newX.Count)
            for i in range(value.Count):
                AreEqual(value[i], newX[i])
    
    ht = System.Collections.Hashtable()
    ht['foo'] = 'bar'
    
    gd = System.Collections.Generic.Dictionary[str, str]()
    gd['foo'] = 'bar'

    # dictionaries
    for value in (ht, gd):
        for newX in (pickle.loads(pickle.dumps(value)), clr.Deserialize(*clr.Serialize(value))):
            AreEqual(value.Count, newX.Count)
            for key in value.Keys:
                AreEqual(value[key], newX[key])
                
    # interesting cases
    for tempX in [System.Exception("some message")]:
        for newX in (pickle.loads(pickle.dumps(tempX)), clr.Deserialize(*clr.Serialize(tempX))):
            AreEqual(newX.Message, tempX.Message)

    try:
        exec(" print 1")
    except Exception as tempX:
        pass
    newX = pickle.loads(pickle.dumps(tempX))
    for attr in ['args', 'filename', 'text', 'lineno', 'msg', 'offset', 'print_file_and_line',
                 'message',
                 ]:
        AreEqual(eval("newX.%s" % attr), 
                 eval("tempX.%s" % attr))
    

    class K(System.Exception):
        other = "something else"
    tempX = K()
    #CodePlex 16415
    #for newX in (cPickle.loads(cPickle.dumps(tempX)), clr.Deserialize(*clr.Serialize(tempX))):
    #    AreEqual(newX.Message, tempX.Message)
    #    AreEqual(newX.other, tempX.other)
    
    #CodePlex 16415
    tempX = System.Exception
    #for newX in (cPickle.loads(cPickle.dumps(System.Exception)), clr.Deserialize(*clr.Serialize(System.Exception))):
    #    temp_except = newX("another message")
    #    AreEqual(temp_except.Message, "another message")

def test_generic_method_error():
    clr.AddReference('System.Core')
    from System.Linq import Queryable
    AssertErrorWithMessage(TypeError, "The type arguments for method 'First' cannot be inferred from the usage. Try specifying the type arguments explicitly.", Queryable.First, [])

def test_collection_length():
    a = GenericCollection()
    AreEqual(len(a), 0)
    a.Add(1)
    AreEqual(len(a), 1)
    
    Assert(hasattr(a, '__len__'))
    
def test_dict_copy():
    Assert('MaxValue' in int.__dict__.copy())

def test_decimal_bool():
    AreEqual(bool(System.Decimal(0)), False)
    AreEqual(bool(System.Decimal(1)), True)

@skip("silverlight") # no Char.Parse
def test_add_str_char():
    AreEqual('bc' + System.Char.Parse('a'), 'bca')
    AreEqual(System.Char.Parse('a') + 'bc', 'abc')

def test_import_star_enum():
    from System.AttributeTargets import *
    Assert('ReturnValue' in dir())

@skip("silverlight")
def test_cp11971():
    old_syspath = [x for x in sys.path]
    try:
        sys.path.append(testpath.temporary_dir)
        
        #Module using System
        write_to_file(path_combine(testpath.temporary_dir, "cp11971_module.py"), 
                      """def a():
    from System import Array
    return Array.CreateInstance(int, 2, 2)""")

        #Module which doesn't use System directly
        write_to_file(path_combine(testpath.temporary_dir, "cp11971_caller.py"), 
                      """import cp11971_module
A = cp11971_module.a()
if not hasattr(A, 'Rank'):
    raise 'CodePlex 11971'
    """)
    
        #Validations
        import cp11971_caller
        Assert(hasattr(cp11971_caller.A, 'Rank'))
        Assert(hasattr(cp11971_caller.cp11971_module.a(), 'Rank'))
    
    finally:
        sys.path = old_syspath

@skip("silverlight") # no Stack on Silverlight
def test_ienumerable__getiter__():
    
    #--empty list
    called = 0
    x = System.Collections.Generic.List[int]()
    Assert(hasattr(x, "__iter__"))
    for stuff in x:
        called +=1 
    AreEqual(called, 0)
    
    #--add one element to the list
    called = 0
    x.Add(1)
    for stuff in x:
        AreEqual(stuff, 1)
        called +=1
    AreEqual(called, 1)
    
    #--one element list before __iter__ is called
    called = 0
    x = System.Collections.Generic.List[int]()
    x.Add(1)
    for stuff in x:
        AreEqual(stuff, 1)
        called +=1
    AreEqual(called, 1)
    
    #--two elements in the list
    called = 0
    x.Add(2)
    for stuff in x:
        AreEqual(stuff-1, called)
        called +=1
    AreEqual(called, 2)

def test_overload_functions():
    for x in min.Overloads.Functions:
        Assert(x.__doc__.startswith('min('))
        Assert(x.__doc__.find('CodeContext') == -1)
    # multiple accesses should return the same object
    AreEqual(
        id(min.Overloads[object, object]), 
        id(min.Overloads[object, object])
    )

def test_clr_dir():
    Assert('IndexOf' not in clr.Dir('abc'))
    Assert('IndexOf' in clr.DirClr('abc'))

@skip("posix")
def test_array_contains():
    AssertError(KeyError, lambda : System.Array[str].__dict__['__contains__'])

def test_a_override_patching():
    if System.Environment.Version.Major >=4:
        clr.AddReference("System.Core")
    else:
        clr.AddReference("Microsoft.Scripting.Core")

    # derive from object
    class x(object):
        pass
    
    # force creation of GetHashCode built-in function
    TestHelpers.HashObject(x())

    # derive from a type which overrides GetHashCode
    from System.Dynamic import InvokeBinder
    from System.Dynamic import CallInfo
    
    class y(InvokeBinder):
        def GetHashCode(self): return super(InvokeBinder, self).GetHashCode()
    
    # now the super call should work & should include the InvokeBinder new type
    TestHelpers.HashObject(y(CallInfo(0)))

def test_inherited_interface_impl():
    BinderTest.InterfaceTestHelper.Flag = False
    BinderTest.InterfaceTestHelper.GetObject().M()
    AreEqual(BinderTest.InterfaceTestHelper.Flag, True)

    BinderTest.InterfaceTestHelper.Flag = False
    BinderTest.InterfaceTestHelper.GetObject2().M()
    AreEqual(BinderTest.InterfaceTestHelper.Flag, True)

def test_dir():
    # make sure you can do dir on everything in System which 
    # includes special types like ArgIterator and Func
    for attr in dir(System):
        dir(getattr(System, attr))

    if not is_silverlight:
        for x in [System.Collections.Generic.SortedList,
                  System.Collections.Generic.Dictionary,
                  ]:
            temp = dir(x)

def test_family_or_assembly():
    class my(FamilyOrAssembly): pass
        
    obj = my()
    AreEqual(obj.Method(), 42)
    obj.Property = 'abc'
    AreEqual(obj.Property, 'abc')

def test_valuetype_iter():
    from System.Collections.Generic import Dictionary
    d = Dictionary[str, str]()
    d["a"] = "foo"
    d["b"] = "bar"
    it = iter(d)
    AreEqual(it.next().Key, 'a')
    AreEqual(it.next().Key, 'b')

@skip("silverlight", "posix")
def test_abstract_class_no_interface_impl():
    # this can't be defined in C# or VB, it's a class which is 
    # abstract and therefore doesn't implement the interface method
    ilcode = """

//  Microsoft (R) .NET Framework IL Disassembler.  Version 3.5.30729.1
//  Copyright (c) Microsoft Corporation.  All rights reserved.



// Metadata version: v2.0.50727
.assembly extern mscorlib
{
  .publickeytoken = (B7 7A 5C 56 19 34 E0 89 )                         // .z\V.4..
  .ver 2:0:0:0
}
.assembly test
{
  .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilationRelaxationsAttribute::.ctor(int32) = ( 01 00 08 00 00 00 00 00 ) 
  .custom instance void [mscorlib]System.Runtime.CompilerServices.RuntimeCompatibilityAttribute::.ctor() = ( 01 00 01 00 54 02 16 57 72 61 70 4E 6F 6E 45 78   // ....T..WrapNonEx
                                                                                                             63 65 70 74 69 6F 6E 54 68 72 6F 77 73 01 )       // ceptionThrows.
  .hash algorithm 0x00008004
  .ver 0:0:0:0
}
.module test.dll
// MVID: {EFFA8498-8C81-4168-A911-C25D4A2C633A}
.imagebase 0x00400000
.file alignment 0x00000200
.stackreserve 0x00100000
.subsystem 0x0003       // WINDOWS_CUI
.corflags 0x00000001    //  ILONLY
// Image base: 0x00500000


// =============== CLASS MEMBERS DECLARATION ===================

.class interface public abstract auto ansi IFoo
{
  .method public hidebysig newslot abstract virtual 
          instance string  Baz() cil managed
  {
  } // end of method IFoo::Baz

} // end of class IFoo

.class public abstract auto ansi beforefieldinit AbstractILTest
       extends [mscorlib]System.Object
       implements IFoo
{
  .method public hidebysig static string 
          Helper(class IFoo x) cil managed
  {
    // Code size       12 (0xc)
    .maxstack  1
    .locals init (string V_0)
    IL_0000:  nop
    IL_0001:  ldarg.0
    IL_0002:  callvirt   instance string IFoo::Baz()
    IL_0007:  stloc.0
    IL_0008:  br.s       IL_000a

    IL_000a:  ldloc.0
    IL_000b:  ret
  } // end of method foo::Helper

  .method family hidebysig specialname rtspecialname 
          instance void  .ctor() cil managed
  {
    // Code size       7 (0x7)
    .maxstack  8
    IL_0000:  ldarg.0
    IL_0001:  call       instance void [mscorlib]System.Object::.ctor()
    IL_0006:  ret
  } // end of method foo::.ctor

} // end of class foo
"""
    from iptest.process_util import run_ilasm
    testilcode = path_combine(testpath.temporary_dir, 'testilcode.il')

    f = file(testilcode, 'w+')
    f.write(ilcode)
    f.close()
    try:
        run_ilasm("/dll " + testilcode)
        
        clr.AddReferenceToFileAndPath(path_combine(testpath.temporary_dir, 'testilcode.dll'))
        import AbstractILTest
        
        class x(AbstractILTest):
            def Baz(self): return "42"
            
        a = x()
        AreEqual(AbstractILTest.Helper(a), "42")
    finally:
        import os
        os.unlink(testilcode)

def test_field_assign():
    """assign to an instance field through the type"""
    
    from IronPythonTest.BinderTest import KeywordBase
    
    def f():
        KeywordBase.SomeField = 42
    
    AssertError(ValueError, f)

def test_event_validates_callable():
    def f(): DelegateTest.StaticEvent += 3
    AssertErrorWithMessage(TypeError, "event addition expected callable object, got int", f)

def test_struct_assign():
    with warning_trapper() as wt:
        from IronPythonTest.BinderTest import ValueTypeWithFields
        from System import Array
        arr = Array.CreateInstance(ValueTypeWithFields, 10)        

        # no warning method
        ValueTypeWithFields.X.SetValue(arr[0], 42)
        AreEqual(wt.messages, [])
        
        # should warn
        arr[0].X = 42
        AreEqual([x.message for x in wt.messages], ['Setting field X on value type ValueTypeWithFields may result in updating a copy.  Use ValueTypeWithFields.X.SetValue(instance, value) if this is safe.  For more information help(ValueTypeWithFields.X.SetValue).'])

def test_ctor_field_assign_conversions():
    from IronPythonTest.BinderTest import  ValueTypeWithFields
    res = ValueTypeWithFields(Y=42)
    res.Y = 42
    AreEqual(ValueTypeWithFields(Y=42), res)
    
    class myint(int): pass
        
    AreEqual(ValueTypeWithFields(Y=myint(42)), res)

def test_iterator_dispose():
    # getting an enumerator from an enumerable should dispose the new enumerator
    import clr
    box = clr.StrongBox[bool](False)
    ietest = EnumerableTest(box)
    for x in ietest:
        pass
        
    AreEqual(box.Value, True)
    
    # enumerating on an enumerator shouldn't dispose the box
    box = clr.StrongBox[bool](False)
    ietest = MyEnumerator(box)
    for x in ietest:
        pass
        
    AreEqual(box.Value, False)

def test_system_doc():
    try:
        # may or may not get documentation depending on XML files availability
        x = System.__doc__
    except:
        AssertUnreachable()

def test_scope_getvariable():
    import clr
    clr.AddReference('IronPython')
    clr.AddReference('Microsoft.Scripting')
    from IronPython.Hosting import Python
    from Microsoft.Scripting import ScopeVariable
    
    scope = Python.CreateEngine().CreateScope()
    var = scope.GetScopeVariable('foo')
    AreEqual(type(var), ScopeVariable)

def test_weird_compare():
    a, b = WithCompare(), WithCompare()
    AreEqual(cmp(a, b), cmp(id(a), id(b)))
    Assert('__cmp__' not in WithCompare.__dict__)

@disabled("No guarantee IronRuby is available.")
def test_load_ruby():
    sys.path.append(path_combine(testpath.public_testdir, r'XLang'))
    rubyfile = clr.Use('some_ruby_file')
    AreEqual(rubyfile.f(), 42)
    
def test_convert_int64_to_float():
    AreEqual(float(System.Int64(42)), 42.0)
    AreEqual(type(float(System.Int64(42))), float)

@skip("silverlight")
def test_cp24004():
    Assert("Find" in System.Array.__dict__)

@skip("silverlight")
def test_cp23772():
    a = System.Array
    x = a[int]([1, 2, 3])
    f = lambda x: x == 2
    g = a.Find[int]
    AreEqual(g.__call__(match=f, array=x),
             2)

def test_cp23938():
    dt = System.DateTime()
    x = dt.ToString
    y = dt.__getattribute__("ToString")
    AreEqual(x, y)
    z = dt.__getattribute__(*("ToString",))
    AreEqual(x, z)
    
    AreEqual(None.__getattribute__(*("__class__",)),
             None.__getattribute__("__class__"))
    
    class Base(object):
        def __getattribute__(self, name):
            return object.__getattribute__(*(self, name))

    class Derived(Base):
        def __getattr__(self, name):
            if name == "bar":
                return 23
            raise AttributeError(*(name,))
        def __getattribute__(self, name):
            return Base.__getattribute__(*(self, name))

    a = Derived(*())
    AreEqual(a.bar, 23)


def test_nothrow_attr_access():
    AreEqual(hasattr('System', 'does_not_exist'), False)
    AreEqual(hasattr(type, '__all__'), False)

@skip("cli") # only run on Silverlight
def test_silverlight_access_isolated_storage():
    import System
    try:
        System.IO.IsolatedStorage.IsolatedStorageFile.GetUserStoreForSite()
    except System.MethodAccessException:
        # bad exception, CLR is rejecting our call
        Assert(False)
    except: 
        # IsolatedStorage may not actually be available
        pass
    
@skip("silverlight", "posix")
def test_xaml_support():
    text = """<custom:XamlTestObject 
   xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
   xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
   x:Name="TestName"
   xmlns:custom="clr-namespace:IronPythonTest;assembly=IronPythonTest" Event="MyEventHandler">
    <custom:InnerXamlTextObject x:Name="Foo">
        <custom:InnerXamlTextObject x:Name="Bar">
            <custom:InnerXamlTextObject2 Name="Baz">
            </custom:InnerXamlTextObject2>
        </custom:InnerXamlTextObject>
    </custom:InnerXamlTextObject>
</custom:XamlTestObject>"""

    import wpf
    import clr
    clr.AddReference('System.Xml')
    f = file('test.xaml', 'w')
            
    try:
        f.write(text)
        f.close()
        
        # easy negative tests
        AssertError(TypeError, wpf.LoadComponent, None)
        AssertError(TypeError, wpf.LoadComponent, None, 'test.xaml')

        # try it again w/ a passed in module
        class MyXamlRootObject(XamlTestObject):
            def MyEventHandler(self, arg):
                return arg * 2
        
        def inputs():
            yield 'test.xaml'
            yield System.IO.FileStream('test.xaml', System.IO.FileMode.Open)
            yield System.Xml.XmlReader.Create('test.xaml')
            yield System.IO.StreamReader('test.xaml')
            
        for inp in inputs():
            inst = wpf.LoadComponent(MyXamlRootObject(), inp)
            AreEqual(inst.Method(42), 84)
            AreEqual(type(inst.Foo), InnerXamlTextObject)
            AreEqual(type(inst.Bar), InnerXamlTextObject)
            AreEqual(inst.Foo.MyName, 'Foo')
            AreEqual(inst.Baz.Name, 'Baz')
            Assert(inst.Foo is not inst.Bar)
            
            if isinstance(inp, System.IDisposable):
                inp.Dispose()                          
                
        
        import imp
        mod = imp.new_module('foo')
        
        class MyXamlRootObject(XamlTestObject):
            pass
                
        for inp in inputs():
            # null input
            AssertError(TypeError, wpf.LoadComponent, mod, None)
            
            # wrong type of root object
            AssertError(Exception, wpf.LoadComponent, mod, inp)
            
            if isinstance(inp, System.IDisposable):
                inp.Dispose()
                
        for inp in inputs():
            # root object missing event handler
            AssertError(System.Xaml.XamlObjectWriterException, wpf.LoadComponent, MyXamlRootObject(), inp)

            if isinstance(inp, System.IDisposable):
                inp.Dispose()

    finally:
        import os
        os.unlink('test.xaml')


@skip("silverlight")
def test_extension_methods():
    import clr, imp
    clr.AddReference('System.Core')
    
    test_cases = [    
"""
# add reference via type
from System.Linq import Enumerable
AssertDoesNotContain(dir([]), 'Where')
clr.ImportExtensions(Enumerable)    
AssertContains(dir([]), 'Where')
AreEqual(list([2,3,4].Where(lambda x: x == 2)), [2])    
""",
"""
# add reference via namespace
import System
AssertDoesNotContain(dir([]), 'Where')
clr.ImportExtensions(System.Linq)
AssertContains(dir([]), 'Where')
AreEqual(list([2,3,4].Where(lambda x: x == 2)), [2])
""",
"""
# add reference via namespace, add new namespace w/ more specific type
import System
from IronPythonTest.ExtensionMethodTest import LinqCollision
AssertDoesNotContain(dir([]), 'Where')
clr.ImportExtensions(System.Linq)
AssertContains(dir([]), 'Where')
AreEqual(list([2,3,4].Where(lambda x: x == 2)), [2])
clr.ImportExtensions(LinqCollision)
AreEqual([2,3,4].Where(lambda x: x == 2), 42)
""",

"""
class UserType(object): pass
class UserTypeWithValue(object):
    def __init__(self):
        self.BaseClass = 200
class UserTypeWithSlots(object):
    __slots__ = 'BaseClass'
class UserTypeWithSlotsWithValue(object):
    __slots__ = 'BaseClass'
    def __init__(self):
        self.BaseClass = 100

AssertError(AttributeError, lambda : UserType().BaseClass)
AssertError(AttributeError, lambda : UserTypeWithSlots().BaseClass)
AreEqual(UserTypeWithValue().BaseClass, 200)

import clr
from IronPythonTest.ExtensionMethodTest import ClassRelationship
clr.ImportExtensions(ClassRelationship)

AreEqual(object().BaseClass(), 23)
AreEqual([].BaseClass(), 23)
AreEqual({}.BaseClass(), 23)

AreEqual(UserType().BaseClass(), 23)

# dict takes precedence
x = UserType()
x.BaseClass = 100
AreEqual(x.BaseClass, 100)

# slots take precedence
AssertError(AttributeError, lambda : UserTypeWithSlots().BaseClass())
AreEqual(UserTypeWithSlotsWithValue().BaseClass, 100)

# dict takes precedence
AreEqual(UserTypeWithValue().BaseClass, 200)
""",
"""
import clr
from IronPythonTest.ExtensionMethodTest import ClassRelationship
clr.ImportExtensions(ClassRelationship)

AreEqual([].Interface(), 23)
AreEqual([].GenericInterface(), 23)
AreEqual([].GenericInterfaceAndMethod(), 23)
AreEqual([].GenericMethod(), 23)

AreEqual(System.Array[int]([2,3,4]).Array(), 23)
AreEqual(System.Array[int]([2,3,4]).ArrayAndGenericMethod(), 23)
AreEqual(System.Array[int]([2,3,4]).GenericMethod(), 23)

AreEqual(object().GenericMethod(), 23)
""",
"""
import clr
import System
from System import Linq

clr.ImportExtensions(Linq) 

class Product(object):
    def __init__(self, cat, id, qtyOnHand ):
        self.Cat = cat
        self.ID = id
        self.QtyOnHand = qtyOnHand
        self.Q = self.QtyOnHand

products = [Product(prod[0], prod[1], prod[2]) for prod in 
    ('DrillRod', 'DR123', 45), ('Flange', 'F423', 12), ('Gizmo', 'G9872', 214), ('Sprocket', 'S534', 42)]
    
pd = products.Where(lambda prod: prod.Q < 40).Select(lambda prod: (prod.Cat, prod.ID) ) 
AreEqual(''.join(str(prod) for prod in pd), "('Flange', 'F423')")
# blows: "Type System.Collections.Generic.IEnumerable`1[TSource] contains generic parameters"

pd = products.Where(lambda prod: prod.Q < 40).AsEnumerable().Select(lambda prod: (prod.Cat, prod.ID) ) 
AreEqual(''.join(str(prod) for prod in pd), "('Flange', 'F423')")

pd = products.Where(lambda prod: prod.Q < 40)               #ok
AreEqual(''.join((str(prod.Cat) + str(prod.ID) + str(prod.Q) for prod in pd)), 'FlangeF42312')
    
pd2 = pd.Select(lambda prod: (prod.Cat, prod.ID) )        #blows, same exception
AreEqual(''.join("Cat: {0}, ID: {1}".format(prod[0], prod[1]) for prod in pd2), "Cat: Flange, ID: F423")

pd2 = products.Select(lambda prod: (prod.Cat, prod.ID) )    #ok

AreEqual(''.join("Cat: {0}, ID: {1}".format(prod[0], prod[1]) for prod in pd2), 'Cat: DrillRod, ID: DR123Cat: Flange, ID: F423Cat: Gizmo, ID: G9872Cat: Sprocket, ID: S534')

pd2 = list(pd).Select(lambda prod: (prod.Cat, prod.ID) )    #ok
AreEqual(''.join("Cat: {0}, ID: {1}".format(prod[0], prod[1]) for prod in pd2), 'Cat: Flange, ID: F423')
   
pd = products.Where(lambda prod: prod.Q < 30).ToList()    #blows, same exception
AreEqual(''.join("Cat: {0}, ID: {1}".format(prod.Cat, prod.ID) for prod in pd), 'Cat: Flange, ID: F423')

pd = list( products.Where(lambda prod: prod.Q < 30) )       #ok
AreEqual(''.join("Cat: {0}, ID: {1}".format(prod.Cat, prod.ID) for prod in pd), 'Cat: Flange, ID: F423')

# ok
pd = list( products.Where(lambda prod: prod.Q < 40) ).Select(lambda prod: "Cat: {0}, ID: {1}, Qty: {2}".format(prod.Cat, prod.ID, prod.Q))
AreEqual(''.join(prod for prod in pd), 'Cat: Flange, ID: F423, Qty: 12')

# ok
pd = ( list(products.Where(lambda prod: prod.Q < 40))
        .Select(lambda prod: "Cat: {0}, ID: {1}, Qty: {2}".format(prod.Cat, prod.ID, prod.Q)) )
AreEqual(''.join(prod for prod in pd), 'Cat: Flange, ID: F423, Qty: 12')

"""
]
    
    for test_case in test_cases:
        try:
            with file('temp_module.py', 'w+') as f:
                f.write('from iptest.assert_util import *\n')
                f.write(test_case)

            import temp_module
            del sys.modules['temp_module']
        finally:
            os.unlink('temp_module.py')
    
    
#--MAIN------------------------------------------------------------------------
run_test(__name__)
