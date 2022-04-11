# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import sys
import unittest
from iptest import IronPythonTestCase, is_cli, is_debug, is_mono, is_netcoreapp, is_netcoreapp21, is_posix, big, run_test, skipUnlessIronPython

if is_cli:
    import clr
    import System

@skipUnlessIronPython()
class CliClassTestCase(IronPythonTestCase):

    def assertNotWarns(self, warning, callable, *args, **kwds):
        import warnings
        with warnings.catch_warnings(record=True) as warning_list:
            warnings.simplefilter('always')
            result = callable(*args, **kwds)
            self.assertFalse(any(item.category == warning for item in warning_list))

    def setUp(self):
        super(CliClassTestCase, self).setUp()

        self.load_iron_python_test()

    def test_inheritance(self):
        import System
        class MyList(System.Collections.Generic.List[int]):
            def get0(self):
                return self[0]

        l = MyList()
        index = l.Add(22)
        self.assertTrue(l.get0() == 22)

    def test_interface_inheritance(self):
        """Verify we can inherit from a class that inherits from an interface"""

        class MyComparer(System.Collections.IComparer):
            def Compare(self, x, y): return 0

        class MyDerivedComparer(MyComparer): pass

        class MyFurtherDerivedComparer(MyDerivedComparer): pass

        # Check that MyDerivedComparer and MyFurtherDerivedComparer can be used as an IComparer
        array = System.Array[int](list(range(10)))

        System.Array.Sort(array, 0, 10, MyComparer())
        System.Array.Sort(array, 0, 10, MyDerivedComparer())
        System.Array.Sort(array, 0, 10, MyFurtherDerivedComparer())

    def test_inheritance_generic_method(self):
        """Verify we can inherit from an interface containing a generic method"""

        from IronPythonTest import IGenericMethods, GenericMethodTester

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
        self.assertEqual(GenericMethodTester.TestIntFactory0(c), 123)
        self.assertEqual(c.type, 'System.Int32')

        self.assertEqual(GenericMethodTester.TestStringFactory1(c, "789"), "456789")
        self.assertEqual(c.type, 'System.String')

        self.assertEqual(GenericMethodTester.TestIntFactory1(c, 321), 777)
        self.assertEqual(c.type, 'System.Int32')

        self.assertEqual(GenericMethodTester.TestStringFactory0(c), '123')
        self.assertEqual(c.type, 'System.String')

        self.assertEqual(GenericMethodTester.TestOutParamString(c), '2')
        self.assertEqual(GenericMethodTester.TestOutParamInt(c), 2)

        self.assertEqual(GenericMethodTester.TestRefParamString(c, '10'), '1010')
        self.assertEqual(GenericMethodTester.TestRefParamInt(c, 10), 20)

        x = System.Collections.Generic.List[System.Int32]((2, 3, 4))
        r = GenericMethodTester.GoWild(c, True, 'second', x)
        self.assertEqual(r.Length, 2)
        self.assertEqual(r[0], 1.5)

        x = System.Collections.Generic.List[int]((2, 3, 4))
        r = GenericMethodTester.GoWildBig(c, True, 'second', x)
        self.assertEqual(r.Length, 2)
        self.assertEqual(r[0], 1.5)

    def test_bases(self):
        #
        # Verify that changing __bases__ works
        #

        class MyExceptionComparer(System.Exception, System.Collections.IComparer):
            def Compare(self, x, y): return 0
        class MyDerivedExceptionComparer(MyExceptionComparer): pass

        e = MyExceptionComparer()

        MyDerivedExceptionComparer.__bases__ = (System.Exception, System.Collections.IComparer)
        MyDerivedExceptionComparer.__bases__ = (MyExceptionComparer,)

        class NewType:
            def NewTypeMethod(self): return "NewTypeMethod"
        class MyOtherExceptionComparer(System.Exception, System.Collections.IComparer, NewType):
            def Compare(self, x, y): return 0
        MyExceptionComparer.__bases__ = MyOtherExceptionComparer.__bases__
        self.assertEqual(e.NewTypeMethod(), "NewTypeMethod")
        self.assertTrue(isinstance(e, System.Exception))
        self.assertTrue(isinstance(e, System.Collections.IComparer))
        self.assertTrue(isinstance(e, MyExceptionComparer))

        class MyIncompatibleExceptionComparer(System.Exception, System.Collections.IComparer, System.IDisposable):
            def Compare(self, x, y): return 0
            def Displose(self): pass

        self.assertRaisesRegex(TypeError, "__bases__ assignment: 'MyExceptionComparer' object layout differs from 'IronPython.NewTypes.System.Exception#IComparer#IDisposable_*",
                                setattr, MyExceptionComparer, "__bases__", MyIncompatibleExceptionComparer.__bases__)
        self.assertRaisesRegex(TypeError, "__class__ assignment: 'MyExceptionComparer' object layout differs from 'IronPython.NewTypes.System.Exception#IComparer#IDisposable_*",
                                setattr, MyExceptionComparer(), "__class__", MyIncompatibleExceptionComparer().__class__)

    def test_open_generic(self):
        # Inherting from an open generic instantiation should fail with a good error message
        try:
            class Foo(System.Collections.Generic.IEnumerable): pass
        except TypeError:
            (exc_type, exc_value, exc_traceback) = sys.exc_info()
            self.assertTrue(str(exc_value).__contains__("cannot inhert from open generic instantiation"))

    def test_interface_slots(self):

        # slots & interfaces
        class foo(object):
            __slots__ = ['abc']

        class bar(foo, System.IComparable):
            def CompareTo(self, other):
                    return 23

        class baz(bar): pass

    def test_op_Implicit_inheritance(self):
        """should inherit op_Implicit from base classes"""
        from IronPythonTest import NewClass
        a = NewClass()
        self.assertEqual(int(a), 1002)
        self.assertEqual(int(a), 1002)
        self.assertEqual(NewClass.op_Implicit(a), 1002)

    def test_symbol_dict(self):
        """tests to verify that Symbol dictionaries do the right thing in dynamic scenarios
        same as the tests in test_class, but we run this in a module that has imported clr"""

        def CheckDictionary(C):
            # add a new attribute to the type...
            C.newClassAttr = 'xyz'
            self.assertEqual(C.newClassAttr, 'xyz')

            # add non-string index into the class and instance dictionary
            a = C()
            try:
                a.__dict__[1] = '1'
                C.__dict__[2] = '2'
                self.assertEqual(1 in a.__dict__, True)
                self.assertEqual(2 in C.__dict__, True)
                self.assertEqual(dir(a).__contains__(1), True)
                self.assertEqual(dir(a).__contains__(2), True)
                self.assertEqual(dir(C).__contains__(2), True)
                self.assertEqual(repr(a.__dict__), "{1: '1'}")
                self.assertEqual(repr(C.__dict__).__contains__("2: '2'"), True)
            except TypeError:
                # new-style classes have dict-proxy, can't do the assignment
                pass

            # replace a class dictionary (containing non-string keys) w/ a normal dictionary
            C.newTypeAttr = 1
            self.assertEqual(hasattr(C, 'newTypeAttr'), True)

            try:
                C.__dict__ = {}
                self.fail("Unreachable code reached")
            except AttributeError:
                pass

            # replace an instance dictionary (containing non-string keys) w/ a new one.
            a.newInstanceAttr = 1
            self.assertEqual(hasattr(a, 'newInstanceAttr'), True)
            a.__dict__  = dict(a.__dict__)
            self.assertEqual(hasattr(a, 'newInstanceAttr'), True)

            a.abc = 'xyz'
            self.assertEqual(hasattr(a, 'abc'), True)
            self.assertEqual(getattr(a, 'abc'), 'xyz')

        class NewClass(object):
            def __init__(self):  pass

        CheckDictionary(NewClass)

    def test_generic_TypeGroup(self):
        # TypeGroup is used to expose "System.IComparable" and "System.IComparable`1" as "System.IComparable"

        # repr
        self.assertEqual(repr(System.IComparable), "<types 'IComparable', 'IComparable[T]'>")

        # Test member access
        self.assertEqual(System.IComparable.CompareTo(1,1), 0)
        self.assertEqual(System.IComparable.CompareTo(1,2), -1)
        self.assertEqual(System.IComparable[int].CompareTo(1,1), 0)
        self.assertEqual(System.IComparable[int].CompareTo(1,2), -1)
        self.assertEqual(System.IComparable[System.Int32].CompareTo(System.Int32(1),System.Int32(1)), 0)
        self.assertEqual(System.IComparable[System.Int32].CompareTo(System.Int32(1),System.Int32(2)), -1)
        self.assertEqual(System.IComparable[int].CompareTo(big(1),big(1)), 0)
        self.assertEqual(System.IComparable[int].CompareTo(big(1),big(2)), -1)
        self.assertTrue(dir(System.IComparable).__contains__("CompareTo"))
        self.assertTrue(list(vars(System.IComparable).keys()).__contains__("CompareTo"))

        import IronPythonTest
        genericTypes = IronPythonTest.NestedClass.InnerGenericClass

        # converstion to Type
        self.assertTrue(System.Type.IsAssignableFrom(System.IComparable, int))
        self.assertRaises(TypeError, System.Type.IsAssignableFrom, object, genericTypes)

        # Test illegal type instantiation
        try:
            System.IComparable[int, int]
        except ValueError: pass
        else: self.fail("Unreachable code reached")

        try:
            System.EventHandler(None)
        except TypeError: pass
        else: self.fail("Unreachable code reached")

        def handler():
            pass

        try:
            System.EventHandler(handler)("sender", None)
        except TypeError: pass
        else: self.fail("Unreachable code reached")

        def handler(s,a):
            pass

        # Test constructor
        self.assertEqual(System.EventHandler(handler).GetType(), System.Type.GetType("System.EventHandler"))

        self.assertEqual(System.EventHandler[System.EventArgs](handler).GetType().GetGenericTypeDefinition(), System.Type.GetType("System.EventHandler`1"))

        # Test inheritance
        class MyComparable(System.IComparable):
            def CompareTo(self, other):
                return self.Equals(other)
        myComparable = MyComparable()
        self.assertTrue(myComparable.CompareTo(myComparable))

        try:
            class MyDerivedClass(genericTypes): pass
        except TypeError: pass
        else: self.fail("Unreachable code reached")

        # Use a TypeGroup to index a TypeGroup
        t = genericTypes[System.IComparable]
        t = genericTypes[System.IComparable, int]
        try:
            System.IComparable[genericTypes]
        except TypeError: pass
        else: self.fail("Unreachable code reached")

    def test_generic_only_TypeGroup(self):
        from IronPythonTest import BinderTest
        try:
            BinderTest.GenericOnlyConflict()
        except System.TypeLoadException as e:
            self.assertTrue(str(e).find('requires a non-generic type') != -1)
            self.assertTrue(str(e).find('GenericOnlyConflict') != -1)

    def test_autodoc(self):
        import os
        from System.Threading import Thread, ThreadStart

        self.assertTrue(Thread.__doc__.find('Thread(start: ThreadStart)') != -1)

        self.assertTrue(Thread.__new__.__doc__.find('__new__(cls: type, start: ThreadStart)') != -1)

        # self.assertEqual(Thread.__new__.Overloads[ThreadStart].__doc__, '__new__(cls : type, start: ThreadStart)' + os.linesep)


    def test_type_descs(self):
        from IronPythonTest import TypeDescTests
        if is_netcoreapp:
            clr.AddReference("System.ComponentModel.Primitives")

        test = TypeDescTests()

        # new style tests

        class bar(int): pass
        b = bar(2)

        class foo(object): pass
        c = foo()


        #test.TestProperties(...)

        res = test.GetClassName(test)
        self.assertTrue(res == 'IronPythonTest.TypeDescTests')

        #res = test.GetClassName(a)
        #self.assertTrue(res == 'list')


        res = test.GetClassName(c)
        self.assertTrue(res == 'foo')

        res = test.GetClassName(b)
        self.assertTrue(res == 'bar')

        res = test.GetConverter(b)
        x = res.ConvertTo(None, None, b, int)
        self.assertTrue(x == 2)
        self.assertTrue(type(x) == int)

        x = test.GetDefaultEvent(b)
        self.assertTrue(x == None)

        x = test.GetDefaultProperty(b)
        self.assertTrue(x == None)

        x = test.GetEditor(b, object)
        self.assertTrue(x == None)

        x = test.GetEvents(b)
        self.assertTrue(x.Count == 0)

        x = test.GetEvents(b, None)
        self.assertTrue(x.Count == 0)

        x = test.GetProperties(b)
        self.assertTrue(x.Count > 0)

        self.assertTrue(test.TestProperties(b, [], []))
        bar.foobar = property(lambda x: 42)
        self.assertTrue(test.TestProperties(b, ['foobar'], []))
        bar.baz = property(lambda x:42)
        self.assertTrue(test.TestProperties(b, ['foobar', 'baz'], []))
        delattr(bar, 'baz')
        self.assertTrue(test.TestProperties(b, ['foobar'], ['baz']))
        # Check that adding a non-string entry in the dictionary does not cause any grief.
        b.__dict__[1] = 1
        self.assertTrue(test.TestProperties(b, ['foobar'], ['baz']))

        #self.assertTrue(test.TestProperties(test, ['GetConverter', 'GetEditor', 'GetEvents', 'GetHashCode'] , []))


        # old style tests

        class foo: pass

        a = foo()

        self.assertTrue(test.TestProperties(a, [], []))


        res = test.GetClassName(a)
        self.assertTrue(res == 'foo')


        x = test.CallCanConvertToForInt(a)
        self.assertTrue(x == False)

        x = test.GetDefaultEvent(a)
        self.assertTrue(x == None)

        x = test.GetDefaultProperty(a)
        self.assertTrue(x == None)

        x = test.GetEditor(a, object)
        self.assertTrue(x == None)

        x = test.GetEvents(a)
        self.assertTrue(x.Count == 0)

        x = test.GetEvents(a, None)
        self.assertTrue(x.Count == 0)

        x = test.GetProperties(a, (System.ComponentModel.BrowsableAttribute(True), ))
        self.assertTrue(x.Count == 0)

        foo.bar = property(lambda x:'hello')

        self.assertTrue(test.TestProperties(a, ['bar'], []))
        delattr(foo, 'bar')
        self.assertTrue(test.TestProperties(a, [], ['bar']))

        a = a.__class__

        self.assertTrue(test.TestProperties(a, [], []))

        foo.bar = property(lambda x:'hello')

        self.assertTrue(test.TestProperties(a, [], []))
        delattr(a, 'bar')
        self.assertTrue(test.TestProperties(a, [], ['bar']))

        x = test.GetClassName(a)
        self.assertEqual(x, 'IronPython.Runtime.Types.PythonType')

        x = test.CallCanConvertToForInt(a)
        self.assertEqual(x, False)

        x = test.GetDefaultEvent(a)
        self.assertEqual(x, None)

        x = test.GetDefaultProperty(a)
        self.assertEqual(x, None)

        x = test.GetEditor(a, object)
        self.assertEqual(x, None)

        x = test.GetEvents(a)
        self.assertEqual(x.Count, 0)

        x = test.GetEvents(a, None)
        self.assertEqual(x.Count, 0)

        x = test.GetProperties(a)
        self.assertTrue(x.Count == 0)

        # Ensure GetProperties checks the attribute dictionary
        a = foo()
        a.abc = 42
        x = test.GetProperties(a)
        for prop in x:
            if prop.Name == 'abc':
                break
        else:
            self.fail("Unreachable code reached")

    def test_char(self):
        for x in range(256):
            c = System.Char.Parse(chr(x))
            self.assertEqual(c, chr(x))
            self.assertEqual(chr(x), c)

            if c == chr(x): pass
            else: self.assertTrue(False)

            if not c == chr(x): self.assertTrue(False)

            if chr(x) == c: pass
            else: self.assertTrue(False)

            if not chr(x) == c: self.assertTrue(False)

    def test_repr(self):
        from IronPythonTest import UnaryClass, BaseClass, BaseClassStaticConstructor
        if is_netcoreapp:
            clr.AddReference('System.Drawing.Primitives')
        else:
            clr.AddReference('System.Drawing')

        from System.Drawing import Point

        self.assertEqual(repr(Point(1,2)).startswith('<System.Drawing.Point object'), True)
        self.assertEqual(repr(Point(1,2)).endswith('[{X=1,Y=2}]>'),True)

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

        self.assertEqual(ra[sa:sa+13], rb[sb:sb+13])
        self.assertEqual(rb[sb:sb+13], rc[sc:sc+13])
        self.assertEqual(ra[sa:sa+13], 'HelloWorld...') # \r\n should be removed, replaced with ...

    def test_explicit_interfaces(self):
        from IronPythonTest import OverrideTestDerivedClass, IOverrideTestInterface
        otdc = OverrideTestDerivedClass()
        self.assertEqual(otdc.MethodOverridden(), "OverrideTestDerivedClass.MethodOverridden() invoked")
        self.assertEqual(IOverrideTestInterface.MethodOverridden(otdc), 'IOverrideTestInterface.MethodOverridden() invoked')

        self.assertEqual(IOverrideTestInterface.x.GetValue(otdc), 'IOverrideTestInterface.x invoked')
        self.assertEqual(IOverrideTestInterface.y.GetValue(otdc), 'IOverrideTestInterface.y invoked')
        IOverrideTestInterface.x.SetValue(otdc, 'abc')
        self.assertEqual(OverrideTestDerivedClass.Value, 'abcx')
        IOverrideTestInterface.y.SetValue(otdc, 'abc')
        self.assertEqual(OverrideTestDerivedClass.Value, 'abcy')

        self.assertEqual(otdc.y, 'OverrideTestDerivedClass.y invoked')

        self.assertEqual(IOverrideTestInterface.Method(otdc), "IOverrideTestInterface.method() invoked")

        self.assertEqual(hasattr(otdc, 'IronPythonTest_IOverrideTestInterface_x'), False)

        # we can also do this the ugly way:

        self.assertEqual(IOverrideTestInterface.x.__get__(otdc, OverrideTestDerivedClass), 'IOverrideTestInterface.x invoked')
        self.assertEqual(IOverrideTestInterface.y.__get__(otdc, OverrideTestDerivedClass), 'IOverrideTestInterface.y invoked')

        self.assertEqual(IOverrideTestInterface.__getitem__(otdc, 2), 'abc')
        self.assertEqual(IOverrideTestInterface.__getitem__(otdc, 2), 'abc')
        self.assertRaises(NotImplementedError, IOverrideTestInterface.__setitem__, otdc, 2, 3)
        try:
            IOverrideTestInterface.__setitem__(otdc, 2, 3)
        except NotImplementedError: pass
        else: self.fail("Unreachable code reached")

    def test_field_helpers(self):
        from IronPythonTest import OverrideTestDerivedClass
        otdc = OverrideTestDerivedClass()
        OverrideTestDerivedClass.z.SetValue(otdc, 'abc')
        self.assertEqual(otdc.z, 'abc')
        self.assertEqual(OverrideTestDerivedClass.z.GetValue(otdc), 'abc')

    def test_field_descriptor(self):
        from IronPythonTest import MySize
        self.assertEqual(MySize.width.__get__(MySize()), 0)
        self.assertEqual(MySize.width.__get__(MySize(), MySize), 0)

    def test_field_const_write(self):
        from IronPythonTest import MySize, ClassWithLiteral
        try:
            MySize.MaxSize = 23
        except AttributeError as e:
            self.assertTrue(str(e).find('MaxSize') != -1)
            self.assertTrue(str(e).find('MySize') != -1)

        try:
            ClassWithLiteral.Literal = 23
        except AttributeError as e:
            self.assertTrue(str(e).find('Literal') != -1)
            self.assertTrue(str(e).find('ClassWithLiteral') != -1)

        try:
            ClassWithLiteral.__dict__['Literal'].__set__(None, 23)
        except AttributeError as e:
            self.assertTrue(str(e).find('int') != -1)

        try:
            ClassWithLiteral.__dict__['Literal'].__set__(ClassWithLiteral(), 23)
        except AttributeError as e:
            self.assertTrue(str(e).find('int') != -1)

        try:
            MySize().MaxSize = 23
        except AttributeError as e:
            self.assertTrue(str(e).find('MaxSize') != -1)
            self.assertTrue(str(e).find('MySize') != -1)

        try:
            ClassWithLiteral().Literal = 23
        except AttributeError as e:
            self.assertTrue(str(e).find('Literal') != -1)
            self.assertTrue(str(e).find('ClassWithLiteral') != -1)

    def test_field_const_access(self):
        from IronPythonTest import MySize, ClassWithLiteral
        self.assertEqual(MySize().MaxSize, System.Int32.MaxValue)
        self.assertEqual(MySize.MaxSize, System.Int32.MaxValue)
        self.assertEqual(ClassWithLiteral.Literal, 5)
        self.assertEqual(ClassWithLiteral().Literal, 5)

    def test_array(self):
        arr = System.Array[int]([0])
        self.assertEqual(repr(arr), str(arr))
        self.assertEqual(repr(System.Array[int]([0, 1])), 'Array[int]((0, 1))')


    def test_strange_inheritance(self):
        """verify that overriding strange methods (such as those that take caller context) doesn't
        flow caller context through"""
        from IronPythonTest import StrangeOverrides
        s = self
        class m(StrangeOverrides):
            def SomeMethodWithContext(self, arg):
                s.assertEqual(arg, 'abc')
            def ParamsMethodWithContext(self, *arg):
                s.assertEqual(arg, ('abc', 'def'))
            def ParamsIntMethodWithContext(self, *arg):
                s.assertEqual(arg, (2,3))

        a = m()
        a.CallWithContext('abc')
        a.CallParamsWithContext('abc', 'def')
        a.CallIntParamsWithContext(2, 3)

    def test_nondefault_indexers(self):

        if not self.has_vbc(): return
        import os
        import _random

        r = _random.Random()
        r.seed()
        fname = 'vbproptest1_%id.vb' % os.getpid()
        self.write_to_file(fname, """
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
End Class""")

        try:
            name = os.path.join(self.temporary_dir, 'vbproptest%f.dll' % (r.random()))
            x = self.run_vbc('/target:library %s "/out:%s"' % (fname, name))
            self.assertEqual(x, 0)

            clr.AddReferenceToFileAndPath(name)
            import VbPropertyTest

            x = VbPropertyTest()
            self.assertEqual(x.IndexOne[0], 0)
            x.IndexOne[1] = 23
            self.assertEqual(x.IndexOne[1], 23)

            self.assertEqual(x.IndexTwo[0,0], 0)
            x.IndexTwo[1,2] = 5
            self.assertEqual(x.IndexTwo[1,2], 5)

            self.assertEqual(VbPropertyTest.SharedIndex[0,0], 0)
            VbPropertyTest.SharedIndex[3,4] = 42
            self.assertEqual(VbPropertyTest.SharedIndex[3,4], 42)
        finally:
            os.unlink(fname)


    def test_nondefault_indexers_overloaded(self):
        if not self.has_vbc(): return
        import os
        import _random

        r = _random.Random()
        r.seed()
        fname = 'vbproptest1_%d.vb' % os.getpid()
        self.write_to_file(fname, """
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

End Class""")

        try:
            name = os.path.join(self.temporary_dir, 'vbproptest%f.dll' % (r.random()))
            self.assertEqual(self.run_vbc('/target:library %s /out:"%s"' % (fname, name)), 0)

            clr.AddReferenceToFileAndPath(name)
            import VbPropertyTest, VbPropertyTest2

            x = VbPropertyTest()
            self.assertEqual(x.IndexOne[0], 0)
            x.IndexOne[1] = 23
            self.assertEqual(x.IndexOne[1], 23)

            self.assertEqual(x.IndexTwo[0,0], 0)
            x.IndexTwo[1,2] = 5
            self.assertEqual(x.IndexTwo[1,2], 5)

            self.assertEqual(VbPropertyTest.SharedIndex[0,0], 0)
            VbPropertyTest.SharedIndex[3,4] = 42
            self.assertEqual(VbPropertyTest.SharedIndex[3,4], 42)

            self.assertEqual(VbPropertyTest2().Prop, 'test')
            self.assertEqual(VbPropertyTest2().get_Prop('foo'), 'foo')
        finally:
            os.unlink(fname)

    def test_interface_abstract_events(self):
        from IronPythonTest import IEventInterface, AbstractEvent, SimpleDelegate, UseEvent
        s = self
        # inherit from an interface or abstract event, and define the event
        for baseType in [IEventInterface, AbstractEvent]:
            class foo(baseType):
                def __init__(self):
                    self._events = []
                def add_MyEvent(self, value):
                    s.assertIsInstance(value, SimpleDelegate)
                    self._events.append(value)
                def remove_MyEvent(self, value):
                    s.assertIsInstance(value, SimpleDelegate)
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
            self.assertEqual(called, True)

            a.MyEvent -= bar
            called = False
            a.MyRaise()
            self.assertEqual(called, False)

            # hook the event from the CLI side, and make sure that raising
            # it causes the CLI side to see the event being fired.
            UseEvent.Hook(a)
            a.MyRaise()
            self.assertEqual(UseEvent.Called, True)
            UseEvent.Called = False
            UseEvent.Unhook(a)
            a.MyRaise()
            self.assertEqual(UseEvent.Called, False)

    @unittest.skipIf(is_debug, "assertion failure")
    def test_dynamic_assembly_ref(self):
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
            self.fail('No type found!')

    def test_nonzero(self):
        from System import Single, Byte, SByte, Int16, UInt16, Int64, UInt64
        for t in [Single, Byte, SByte, Int16, UInt16, Int64, UInt64]:
            self.assertTrue(hasattr(t, '__bool__'))
            if t(0): self.fail("Unreachable code reached")
            if not t(1): self.fail("Unreachable code reached")

    def test_virtual_event(self):
        from IronPythonTest import VirtualEvent, OverrideVirtualEvent, SimpleDelegate, UseEvent
        s = self
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
                            s.assertIsInstance(value, SimpleDelegate)
                            self._events.append(value)
                        def remove_MyEvent(self, value):
                            s.assertIsInstance(value, SimpleDelegate)
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

                self.assertTrue(called)

                a.MyEvent -= bar

                called = False
                a.MyRaise()
                self.assertFalse(called)

                # custom event
                a.LastCall = None
                a = foo()
                a.MyCustomEvent += bar
                if override: self.assertEqual(a.LastCall, None)
                else: self.assertTrue(a.LastCall.endswith('Add'))

                a.Lastcall = None
                a.MyCustomEvent -= bar
                if override: self.assertEqual(a.LastCall, None)
                else: self.assertTrue(a.LastCall.endswith('Remove'))


                # hook the event from the CLI side, and make sure that raising
                # it causes the CLI side to see the event being fired.
                UseEvent.Hook(a)
                a.MyRaise()
                self.assertTrue(UseEvent.Called)
                UseEvent.Called = False
                UseEvent.Unhook(a)
                a.MyRaise()
                self.assertFalse(UseEvent.Called)


    def test_property_get_set(self):
        if is_netcoreapp:
            clr.AddReference("System.Drawing.Primitives")
        else:
            clr.AddReference("System.Drawing")
        from System.Drawing import Size

        temp = Size()
        self.assertEqual(temp.Width, 0)
        temp.Width = 5
        self.assertEqual(temp.Width, 5)

        for i in range(5):
            temp.Width = i
            self.assertEqual(temp.Width, i)

    def test_write_only_property_set(self):
        from IronPythonTest import WriteOnly
        obj = WriteOnly()

        self.assertRaises(AttributeError, getattr, obj, 'Writer')

    def test_isinstance_interface(self):
        self.assertTrue(isinstance('abc', System.Collections.IEnumerable))

    def test_constructor_function(self):
        '''
        Test to hit IronPython.Runtime.Operations.ConstructionFunctionOps.
        '''

        self.assertEqual(System.DateTime.__new__.__name__, '__new__')
        self.assertTrue(System.DateTime.__new__.__doc__.find('__new__(cls: type, year: Int32, month: Int32, day: Int32)') != -1)

        self.assertTrue(System.AssemblyLoadEventArgs.__new__.__doc__.find('__new__(cls: type, loadedAssembly: Assembly)') != -1)

    def test_class_property(self):
        """__class__ should work on standard .NET types and should return the type object associated with that class"""
        self.assertEqual(System.Environment.Version.__class__, System.Version)

    def test_null_str(self):
        """if a .NET type has a bad ToString() implementation that returns null always return String.Empty in Python"""
        from IronPythonTest import RudeObjectOverride
        self.assertEqual(str(RudeObjectOverride()), '')
        self.assertEqual(RudeObjectOverride().__str__(), '')
        self.assertEqual(RudeObjectOverride().ToString(), None)
        self.assertTrue(repr(RudeObjectOverride()).startswith('<IronPythonTest.RudeObjectOverride object at '))

    def test_keyword_construction_readonly(self):
        from IronPythonTest import ClassWithLiteral
        self.assertRaises(AttributeError, System.Version, 1, 0, Build=100)
        self.assertRaises(AttributeError, ClassWithLiteral, Literal=3)

    def test_kw_construction_types(self):
        if is_netcoreapp:
            clr.AddReference("System.IO.FileSystem.Watcher")

        for val in [True, False]:
            x = System.IO.FileSystemWatcher('.', EnableRaisingEvents = val)
            self.assertEqual(x.EnableRaisingEvents, val)

    def test_as_bool(self):
        """verify using expressions in if statements works correctly.  This generates an
        site whose return type is bool so it's important this works for various ways we can
        generate the body of the site, and use the site both w/ the initial & generated targets"""
        from IronPythonTest import NestedClass
        if is_netcoreapp:
            clr.AddReference("System.Runtime")
            clr.AddReference("System.Threading.Thread")
        else:
            clr.AddReference('System') # ensure test passes in ipy

        # instance property
        x = System.Uri('http://foo')
        for i in range(2):
            if x.AbsolutePath: pass
            else: self.fail('instance property')

        # instance property on type
        for i in range(2):
            if System.Uri.AbsolutePath: pass
            else: self.fail('instance property on type')

        # static property
        for i in range(2):
            if System.Threading.Thread.CurrentThread: pass
            else: self.fail('static property')

        # static field
        for i in range(2):
            if System.String.Empty: self.fail('static field')

        # instance field
        x = NestedClass()
        for i in range(2):
            if x.Field: self.fail('instance field')

        # instance field on type
        for i in range(2):
            if NestedClass.Field: pass
            else: self.fail('instance field on type')

        # math
        for i in range(2):
            if System.Int64(1) + System.Int64(1): pass
            else: self.fail('math')

        for i in range(2):
            if System.Int64(1) + System.Int64(1): pass
            else: self.fail('math')

        # GetItem
        x = System.Collections.Generic.List[str]()
        x.Add('abc')
        for i in range(2):
            if x[0]: pass
            else: self.fail('GetItem')



    def test_generic_getitem(self):
        if is_netcoreapp:
            clr.AddReference("System.Collections")

        # calling __getitem__ is the same as indexing
        self.assertEqual(System.Collections.Generic.Stack.__getitem__(int), System.Collections.Generic.Stack[int])

        # but __getitem__ on a type takes precedence
        self.assertRaises(TypeError, System.Collections.Generic.List.__getitem__, int)
        x = System.Collections.Generic.List[int]()
        x.Add(0)
        self.assertEqual(System.Collections.Generic.List[int].__getitem__(x, 0), 0)

        # but we can call type.__getitem__ with the instance
        self.assertEqual(type.__getitem__(System.Collections.Generic.List, int), System.Collections.Generic.List[int])


    @unittest.skipIf(is_netcoreapp, 'no System.Windows.Forms')
    def test_multiple_inheritance(self):
        """multiple inheritance from two types in the same hierarchy should work, this is similar to class foo(int, object)"""
        clr.AddReference("System.Windows.Forms")
        class foo(System.Windows.Forms.Form, System.Windows.Forms.Control): pass

    def test_struct_no_ctor_kw_args(self):
        from IronPythonTest import Structure
        for x in range(2):
            s = Structure(a=3)
            self.assertEqual(s.a, 3)

    def test_nullable_new(self):
        from System import Nullable
        self.assertEqual(clr.GetClrType(Nullable[()]).IsGenericType, False)

    def test_ctor_keyword_args_newslot(self):
        """ctor keyword arg assignment contruction w/ new slot properties"""
        from IronPythonTest import BinderTest
        x = BinderTest.KeywordDerived(SomeProperty = 'abc')
        self.assertEqual(x.SomeProperty, 'abc')

        x = BinderTest.KeywordDerived(SomeField = 'abc')
        self.assertEqual(x.SomeField, 'abc')

    def test_enum_truth(self):
        # zero enums are false, non-zero enums are true
        StringSplitOptionsNone = getattr(System.StringSplitOptions, "None")
        self.assertTrue(not StringSplitOptionsNone)
        self.assertTrue(System.StringSplitOptions.RemoveEmptyEntries)
        self.assertEqual(StringSplitOptionsNone.__bool__(), False)
        self.assertEqual(System.StringSplitOptions.RemoveEmptyEntries.__bool__(), True)

    def test_enum_repr(self):
        clr.AddReference('IronPython')
        from IronPython.Runtime import ModuleOptions
        self.assertEqual(repr(ModuleOptions.ShowClsMethods), 'IronPython.Runtime.ModuleOptions.ShowClsMethods')
        self.assertEqual(repr(ModuleOptions.ShowClsMethods | ModuleOptions.Optimized),
                '<enum IronPython.Runtime.ModuleOptions: ShowClsMethods, Optimized>')

    def test_bad_inheritance(self):
        """verify a bad inheritance reports the type name you're inheriting from"""
        def f():
            class x(System.Single): pass
        def g():
            class x(System.Version): pass

        self.assertRaisesPartialMessage(TypeError, 'System.Single', f)
        self.assertRaisesPartialMessage(TypeError, 'System.Version', g)

    @unittest.skipIf(is_netcoreapp21, "TODO: figure out")
    def test_disposable(self):
        """classes implementing IDisposable should automatically support the with statement"""
        from IronPythonTest import DisposableTest

        x = DisposableTest()

        with x:
            pass

        self.assertEqual(x.Called, True)

        self.assertTrue(hasattr(x, '__enter__'))
        self.assertTrue(hasattr(x, '__exit__'))

        x = DisposableTest()
        x.__enter__()
        try:
            pass
        finally:
            self.assertEqual(x.__exit__(None, None, None), None)

        self.assertEqual(x.Called, True)

        self.assertTrue('__enter__' in dir(x))
        self.assertTrue('__exit__' in dir(x))
        self.assertTrue('__enter__' in dir(DisposableTest))
        self.assertTrue('__exit__' in dir(DisposableTest))

    def test_dbnull(self):
        """DBNull should not be true"""
        if System.DBNull.Value:
            self.fail('System.DBNull.Value should not be true')

    def test_special_repr(self):
        import System
        list = System.Collections.Generic.List[object]()
        self.assertEqual(repr(list), 'List[object]()')

        list.Add('abc')
        self.assertEqual(repr(list), "List[object](['abc'])")

        list.Add(2)
        self.assertEqual(repr(list), "List[object](['abc', 2])")

        list.Add(list)
        self.assertEqual(repr(list), "List[object](['abc', 2, [...]])")

        dict = System.Collections.Generic.Dictionary[object, object]()
        self.assertEqual(repr(dict), "Dictionary[object, object]()")

        dict["abc"] = "def"
        self.assertEqual(repr(dict), "Dictionary[object, object]({'abc' : 'def'})")

        dict["two"] = "def"
        self.assertTrue(repr(dict) == "Dictionary[object, object]({'abc' : 'def', 'two' : 'def'})" or
            repr(dict) == "Dictionary[object, object]({'two' : 'def', 'def' : 'def'})")

        dict = System.Collections.Generic.Dictionary[object, object]()
        dict['abc'] = dict
        self.assertEqual(repr(dict), "Dictionary[object, object]({'abc' : {...}})")

        dict = System.Collections.Generic.Dictionary[object, object]()
        dict[dict] = 'abc'

        self.assertEqual(repr(dict), "Dictionary[object, object]({{...} : 'abc'})")

    def test_issubclass(self):
        self.assertTrue(issubclass(int, clr.GetClrType(int)))

    def test_explicit_interface_impl(self):
        from IronPythonTest import ExplicitTestNoConflict, ExplicitTestOneConflict
        noConflict = ExplicitTestNoConflict()
        oneConflict = ExplicitTestOneConflict()

        self.assertEqual(noConflict.A(), "A")
        self.assertEqual(noConflict.B(), "B")
        self.assertTrue(hasattr(noConflict, "A"))
        self.assertTrue(hasattr(noConflict, "B"))

        self.assertRaises(AttributeError, lambda : oneConflict.A())
        self.assertEqual(oneConflict.B(), "B")
        self.assertTrue(not hasattr(oneConflict, "A"))
        self.assertTrue(hasattr(oneConflict, "B"))

    def test_interface_isinstance(self):
        l = System.Collections.ArrayList()
        self.assertEqual(isinstance(l, System.Collections.IList), True)

    def test_serialization(self):
        """
        TODO:
        - this should become a test module in and of itself
        - way more to test here..
        """

        import pickle

        # test the primitive data types...
        data = [1, 1.0, 2j, big(3), True, "xyz",
                System.Int64(1), System.UInt64(1),
                System.Int32(1), System.UInt32(1),
                System.Int16(1), System.UInt16(1),
                System.Byte(1), System.SByte(1),
                #System.IntPtr(-1), System.UIntPtr(2), # TODO: IntPtrOps.cs
                System.Decimal(1), System.Single(1.0),
                System.Char.MaxValue, System.DBNull.Value,
                System.DateTime.Now, None, {}, (), [], {'a': 2}, (42, ), [42, ],
                System.StringSplitOptions.RemoveEmptyEntries,
                ]

        if is_netcoreapp and not is_netcoreapp21:
            clr.AddReference("System.Text.Json")
            data.append(System.Text.Json.JsonValueKind.Object) # byte-based enum

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
                self.assertEqual(type(newVal), type(value))
                try:
                    self.assertEqual(newVal, value)
                except RuntimeError as e:
                    # we hit one of our recursive structures...
                    self.assertEqual(str(e), "maximum recursion depth exceeded")
                    self.assertTrue(type(newVal) is list or type(newVal) is dict)

        # passing an unknown format raises...
        self.assertRaises(ValueError, clr.Deserialize, "unknown", "foo")

        al = System.Collections.ArrayList()
        al.Add(2)

        gl = System.Collections.Generic.List[int]()
        gl.Add(2)

        # lists...
        for value in (al, gl):
            for newX in (pickle.loads(pickle.dumps(value)), clr.Deserialize(*clr.Serialize(value))):
                self.assertEqual(value.Count, newX.Count)
                for i in range(value.Count):
                    self.assertEqual(value[i], newX[i])

        ht = System.Collections.Hashtable()
        ht['foo'] = 'bar'

        gd = System.Collections.Generic.Dictionary[str, str]()
        gd['foo'] = 'bar'

        # dictionaries
        for value in (ht, gd):
            for newX in (pickle.loads(pickle.dumps(value)), clr.Deserialize(*clr.Serialize(value))):
                self.assertEqual(value.Count, newX.Count)
                for key in value.Keys:
                    self.assertEqual(value[key], newX[key])

        # interesting cases
        for tempX in [System.Exception("some message")]:
            for newX in (pickle.loads(pickle.dumps(tempX)), clr.Deserialize(*clr.Serialize(tempX))):
                self.assertEqual(newX.Message, tempX.Message)

        try:
            exec(" print 1")
        except Exception as err:
            tempX = err
        newX = pickle.loads(pickle.dumps(tempX))
        for attr in ['args', 'filename', 'text', 'lineno', 'msg', 'offset', 'print_file_and_line']:
            self.assertEqual(eval("newX.%s" % attr),
                    eval("tempX.%s" % attr))


        class K(System.Exception):
            other = "something else"
        tempX = K()
        #CodePlex 16415
        #for newX in (cPickle.loads(cPickle.dumps(tempX)), clr.Deserialize(*clr.Serialize(tempX))):
        #    self.assertEqual(newX.Message, tempX.Message)
        #    self.assertEqual(newX.other, tempX.other)

        #CodePlex 16415
        tempX = System.Exception
        #for newX in (cPickle.loads(cPickle.dumps(System.Exception)), clr.Deserialize(*clr.Serialize(System.Exception))):
        #    temp_except = newX("another message")
        #    self.assertEqual(temp_except.Message, "another message")

    def test_generic_method_error(self):
        if is_netcoreapp:
            clr.AddReference("System.Linq.Queryable")
        else:
            clr.AddReference('System.Core')
        from System.Linq import Queryable
        self.assertRaisesMessage(TypeError, "The type arguments for method 'First' cannot be inferred from the usage. Try specifying the type arguments explicitly.", Queryable.First, [])

    def test_collection_length(self):
        from IronPythonTest import GenericCollection
        a = GenericCollection()
        self.assertEqual(len(a), 0)
        a.Add(1)
        self.assertEqual(len(a), 1)

        self.assertTrue(hasattr(a, '__len__'))

    def test_dict_copy(self):
        self.assertTrue('MaxValue' in System.Int32.__dict__.copy())

    def test_decimal_bool(self):
        self.assertEqual(bool(System.Decimal(0)), False)
        self.assertEqual(bool(System.Decimal(1)), True)

    def test_add_str_char(self):
        self.assertEqual('bc' + System.Char.Parse('a'), 'bca')
        self.assertEqual(System.Char.Parse('a') + 'bc', 'abc')

    def test_import_star_enum(self):
        d = {}
        exec("from System.AttributeTargets import *", d, d)
        self.assertTrue('ReturnValue' in d)

    def test_cp11971(self):
        import os
        old_syspath = [x for x in sys.path]
        try:
            sys.path.append(self.temporary_dir)

            #Module using System
            self.write_to_file(os.path.join(self.temporary_dir, "cp11971_module.py"),
                      """def a():
    from System import Array
    return Array.CreateInstance(int, 2, 2)""")

            #Module which doesn't use System directly
            self.write_to_file(os.path.join(self.temporary_dir, "cp11971_caller.py"),
                      """import cp11971_module
A = cp11971_module.a()
if not hasattr(A, 'Rank'):
    raise 'CodePlex 11971'
    """)

            #Validations
            import cp11971_caller
            self.assertTrue(hasattr(cp11971_caller.A, 'Rank'))
            self.assertTrue(hasattr(cp11971_caller.cp11971_module.a(), 'Rank'))

        finally:
            sys.path = old_syspath

    def test_ienumerable__getiter__(self):

        #--empty list
        called = 0
        x = System.Collections.Generic.List[int]()
        self.assertTrue(hasattr(x, "__iter__"))
        for stuff in x:
            called +=1
        self.assertEqual(called, 0)

        #--add one element to the list
        called = 0
        x.Add(1)
        for stuff in x:
            self.assertEqual(stuff, 1)
            called +=1
        self.assertEqual(called, 1)

        #--one element list before __iter__ is called
        called = 0
        x = System.Collections.Generic.List[int]()
        x.Add(1)
        for stuff in x:
            self.assertEqual(stuff, 1)
            called +=1
        self.assertEqual(called, 1)

        #--two elements in the list
        called = 0
        x.Add(2)
        for stuff in x:
            self.assertEqual(stuff-1, called)
            called +=1
        self.assertEqual(called, 2)

    def test_overload_functions(self):
        for x in min.Overloads.Functions:
            self.assertTrue(x.__doc__.startswith('min('))
            self.assertTrue(x.__doc__.find('CodeContext') == -1)
        # multiple accesses should return the same object
        self.assertEqual(
            id(min.Overloads[object, object]),
            id(min.Overloads[object, object])
        )

    def test_clr_dir(self):
        self.assertTrue('IndexOf' not in clr.Dir('abc'))
        self.assertTrue('IndexOf' in clr.DirClr('abc'))

    def test_int32_bigint_equivalence(self):
        import math

        # properties
        for i in range(-10, 10):
            bi = big(i)
            self.assertEqual(i.IsEven, bi.IsEven)
            self.assertEqual(i.IsOne, bi.IsOne)
            self.assertEqual(i.IsPowerOfTwo, bi.IsPowerOfTwo)
            self.assertEqual(i.IsZero, bi.IsZero)
            self.assertEqual(i.Sign, bi.Sign)
            # static properties
            self.assertEqual(i.Zero, bi.Zero)
            self.assertEqual(i.One, bi.One)
            self.assertEqual(i.MinusOne, bi.MinusOne)

        # methods
        i = System.Int32(1234567890)
        bi = big(i)
        self.assertEqual(i.ToByteArray(), bi.ToByteArray())
        if hasattr(int, 'GetByteCount'):
            self.assertEqual(i.GetByteCount(), bi.GetByteCount())
        if hasattr(int, 'GetBitLength'):
            self.assertEqual(i.GetBitLength(), bi.GetBitLength())

        # static methods
        for i in [0, 1, 2, 7<<30, 1<<32, (1<<32)-1, (1<<32)+1]:
            for i2 in [i, -i]:
                ii = int(i2) # convert to Int32 if possible
                bi = big(i2)
                self.assertEqual((1).Negate(ii), int.Negate(bi))
                self.assertEqual((1).Abs(ii), int.Abs(bi))
                self.assertEqual((1).Pow(ii, 5), int.Pow(bi, 5))
                self.assertEqual((1).ModPow(ii, 5, 3), int.ModPow(bi, 5, 3))
                if ii >= 0:
                    self.assertEqual((1).Log(ii), int.Log(bi))
                    self.assertEqual((1).Log10(ii), int.Log10(bi))
                    self.assertEqual((1).Log(ii, 7.2), int.Log(bi, 7.2))
                else:
                    self.assertTrue(math.isnan((1).Log(ii)))
                    self.assertTrue(math.isnan(int.Log(bi)))
                    self.assertTrue(math.isnan((1).Log10(ii)))
                    self.assertTrue(math.isnan(int.Log10(bi)))
                    self.assertTrue(math.isnan((1).Log(ii, 7.2)))
                    self.assertTrue(math.isnan(int.Log(bi, 7.2)))

                for j in [0, 1, 2, 7<<30, 1<<32, (1<<32)-1, (1<<32)+1]:
                    for j2 in [j, -j]:
                        jj = int(j2) # convert to Int32 if possible
                        bj = big(j2)
                        self.assertEqual((1).Compare(ii, jj), int.Compare(bi, bj))
                        self.assertEqual((1).Min(ii, jj), int.Min(bi, bj))
                        self.assertEqual((1).Max(ii, jj), int.Max(bi, bj))
                        self.assertEqual((1).Add(ii, jj), int.Add(bi, bj))
                        self.assertEqual((1).Subtract(ii, jj), int.Subtract(bi, bj))
                        self.assertEqual((1).Multiply(ii, jj), int.Multiply(bi, bj))
                        self.assertEqual((1).GreatestCommonDivisor(ii, jj), int.GreatestCommonDivisor(bi, bj))
                        if jj != 0:
                            self.assertEqual((1).Divide(ii, jj), int.Divide(bi, bj))
                            self.assertEqual((1).DivRem(ii, jj), int.DivRem(bi, bj))
                            self.assertEqual((1).Remainder(ii, jj), int.Remainder(bi, bj))

    def test_array_contains(self):
        if is_mono: # for whatever reason this is defined on Mono
            System.Array[str].__dict__['__contains__']
        else:
            self.assertRaises(KeyError, lambda : System.Array[str].__dict__['__contains__'])

    def test_a_override_patching(self):
        from IronPythonTest import TestHelpers

        if is_netcoreapp:
            clr.AddReference("System.Dynamic.Runtime")
            clr.AddReference("System.Linq.Expressions")
        else:
            clr.AddReference("System.Core")

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

    def test_inherited_interface_impl(self):
        from IronPythonTest import BinderTest
        BinderTest.InterfaceTestHelper.Flag = False
        BinderTest.InterfaceTestHelper.GetObject().M()
        self.assertEqual(BinderTest.InterfaceTestHelper.Flag, True)

        BinderTest.InterfaceTestHelper.Flag = False
        BinderTest.InterfaceTestHelper.GetObject2().M()
        self.assertEqual(BinderTest.InterfaceTestHelper.Flag, True)

    def test_dir(self):
        # make sure you can do dir on everything in System which
        # includes special types like ArgIterator and Func
        for attr in dir(System):
            dir(getattr(System, attr))

        if is_netcoreapp:
            clr.AddReference("System.Collections")

        for x in [System.Collections.Generic.SortedList,
                    System.Collections.Generic.Dictionary,
                    ]:
            temp = dir(x)

    def test_family_or_assembly(self):
        from IronPythonTest import FamilyOrAssembly
        class my(FamilyOrAssembly): pass

        obj = my()
        self.assertEqual(obj.Method(), 42)
        obj.Property = 'abc'
        self.assertEqual(obj.Property, 'abc')

    def test_valuetype_iter(self):
        from System.Collections.Generic import Dictionary
        d = Dictionary[str, str]()
        d["a"] = "foo"
        d["b"] = "bar"
        it = iter(d)
        self.assertEqual(it.__next__().Key, 'a')
        self.assertEqual(it.__next__().Key, 'b')

    @unittest.skipIf(is_mono, "Causes an abort on mono, needs debug")
    def test_abstract_class_no_interface_implself(self):
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
        import os
        testilcode = os.path.join(self.temporary_dir, 'testilcode_%d.il' % os.getpid())

        self.write_to_file(testilcode, ilcode)
        try:
            self.run_ilasm("/dll " + testilcode)

            clr.AddReferenceToFileAndPath(os.path.join(self.temporary_dir, 'testilcode_%d.dll' % os.getpid()))
            import AbstractILTest

            class x(AbstractILTest):
                def Baz(self): return "42"

            a = x()
            self.assertEqual(AbstractILTest.Helper(a), "42")
        finally:
            os.unlink(testilcode)

    def test_field_assign(self):
        """assign to an instance field through the type"""

        from IronPythonTest.BinderTest import KeywordBase

        def f():
            KeywordBase.SomeField = 42

        self.assertRaises(ValueError, f)

    def test_event_validates_callable(self):
        from IronPythonTest import DelegateTest
        def f(): DelegateTest.StaticEvent += 3
        self.assertRaisesMessage(TypeError, "event addition expected callable object, got int", f)

    def test_struct_assign(self):
        from IronPythonTest.BinderTest import ValueTypeWithFields
        from System import Array

        def noWarnMethod():
            arr = Array.CreateInstance(ValueTypeWithFields, 10)
            ValueTypeWithFields.X.SetValue(arr[0], 42)

        def warnMethod():
            arr = Array.CreateInstance(ValueTypeWithFields, 10)
            arr[0].X = 42

        self.assertNotWarns(RuntimeWarning, noWarnMethod)
        self.assertWarns(RuntimeWarning, warnMethod)

    def test_ctor_field_assign_conversions(self):
        from IronPythonTest.BinderTest import ValueTypeWithFields
        res = ValueTypeWithFields(Y=42)
        res.Y = 42
        self.assertEqual(ValueTypeWithFields(Y=42), res)

        class myint(int): pass

        self.assertEqual(ValueTypeWithFields(Y=myint(42)), res)

    def test_iterator_dispose(self):
        # getting an enumerator from an enumerable should dispose the new enumerator
        from IronPythonTest import EnumerableTest, MyEnumerator
        box = clr.StrongBox[bool](False)
        ietest = EnumerableTest(box)
        for x in ietest:
            pass

        self.assertEqual(box.Value, True)

        # enumerating on an enumerator shouldn't dispose the box
        box = clr.StrongBox[bool](False)
        ietest = MyEnumerator(box)
        for x in ietest:
            pass

        self.assertEqual(box.Value, False)

    def test_system_doc(self):
        try:
            # may or may not get documentation depending on XML files availability
            x = System.__doc__
        except:
            self.fail('test_system_doc')

    def test_scope_getvariable(self):
        import clr
        clr.AddReference('IronPython')
        clr.AddReference('Microsoft.Scripting')
        from IronPython.Hosting import Python
        from Microsoft.Scripting import ScopeVariable

        scope = Python.CreateEngine().CreateScope()
        var = scope.GetScopeVariable('foo')
        self.assertEqual(type(var), ScopeVariable)

    def test_weird_compare(self):
        from IronPythonTest import WithCompare
        self.assertTrue('__cmp__' not in WithCompare.__dict__) # TODO: revisit this once we decide how to map CompareTo to Python

    def test_convert_int64_to_float(self):
        self.assertEqual(float(System.Int64(42)), 42.0)
        self.assertEqual(type(float(System.Int64(42))), float)

    def test_int_constructor_overflow(self):
        from iptest import clr_int_types, myint, myfloat

        val = 1 << 64
        for t in clr_int_types:
            self.assertRaises(OverflowError, t, val)
            self.assertRaises(OverflowError, t, str(val))
            self.assertRaises(OverflowError, t, float(val))
            self.assertRaises(OverflowError, t, myint(val))
            self.assertRaises(OverflowError, t, myfloat(val))

    def test_cp24004(self):
        self.assertTrue("Find" in System.Array.__dict__)

    def test_cp23772(self):
        a = System.Array
        x = a[int]([1, 2, 3])
        f = lambda x: x == 2
        g = a.Find[int]
        self.assertEqual(g.__call__(match=f, array=x), 2)

    def test_cp23938(self):
        dt = System.DateTime()
        x = dt.ToString
        y = dt.__getattribute__("ToString")
        self.assertEqual(x, y)
        z = dt.__getattribute__(*("ToString",))
        self.assertEqual(x, z)

        self.assertEqual(None.__getattribute__(*("__class__",)),
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
        self.assertEqual(a.bar, 23)


    def test_nothrow_attr_access(self):
        self.assertEqual(hasattr('System', 'does_not_exist'), False)
        self.assertEqual(hasattr(type, '__all__'), False)

    @unittest.skipIf(is_netcoreapp or is_posix, 'No WPF available')
    def test_xaml_support(self):
        from IronPythonTest import XamlTestObject, InnerXamlTextObject
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

        import os
        import wpf
        import clr
        clr.AddReference('System.Xml')
        fname = 'test_%d.xaml' % os.getpid()
        self.write_to_file(fname, text)

        try:
            # easy negative tests
            self.assertRaises(TypeError, wpf.LoadComponent, None)
            self.assertRaises(TypeError, wpf.LoadComponent, None, fname)

            # try it again w/ a passed in module
            class MyXamlRootObject(XamlTestObject):
                def MyEventHandler(self, arg):
                    return arg * 2

            def inputs():
                yield fname
                yield System.IO.FileStream(fname, System.IO.FileMode.Open)
                yield System.Xml.XmlReader.Create(fname)
                yield System.IO.StreamReader(fname)

            for inp in inputs():
                inst = wpf.LoadComponent(MyXamlRootObject(), inp)
                self.assertEqual(inst.Method(42), 84)
                self.assertEqual(type(inst.Foo), InnerXamlTextObject)
                self.assertEqual(type(inst.Bar), InnerXamlTextObject)
                self.assertEqual(inst.Foo.MyName, 'Foo')
                self.assertEqual(inst.Baz.Name, 'Baz')
                self.assertTrue(inst.Foo is not inst.Bar)

                if isinstance(inp, System.IDisposable):
                    inp.Dispose()


            import imp
            mod = imp.new_module('foo')

            class MyXamlRootObject(XamlTestObject):
                pass

            for inp in inputs():
                # null input
                self.assertRaises(TypeError, wpf.LoadComponent, mod, None)

                # wrong type of root object
                self.assertRaises(Exception, wpf.LoadComponent, mod, inp)

                if isinstance(inp, System.IDisposable):
                    inp.Dispose()

            for inp in inputs():
                # root object missing event handler
                self.assertRaises(System.Xaml.XamlObjectWriterException, wpf.LoadComponent, MyXamlRootObject(), inp)

                if isinstance(inp, System.IDisposable):
                    inp.Dispose()

        finally:
            os.unlink(fname)

    @unittest.skipIf(is_netcoreapp, "TODO: figure out")
    def test_extension_methods(self):
        import clr, imp, os
        if is_netcoreapp:
            clr.AddReference('System.Linq')
        else:
            clr.AddReference('System.Core')

        test_cases = [
"""
# add reference via type
import clr
from System.Linq import Enumerable
class TheTestCase(IronPythonTestCase):
    def test_reference_via_type(self):
        self.assertNotIn('Where', dir([]))
        clr.ImportExtensions(Enumerable)
        self.assertIn('Where', dir([]))
        self.assertEqual(list([2,3,4].Where(lambda x: x == 2)), [2])
""",
"""
# add reference via namespace
import clr
import System
class TheTestCase(IronPythonTestCase):
    def test_reference_via_namespace(self):
        self.assertNotIn('Where', dir([]))
        clr.ImportExtensions(System.Linq)
        self.assertIn('Where', dir([]))
        self.assertEqual(list([2,3,4].Where(lambda x: x == 2)), [2])
""",
"""
# add reference via namespace, add new namespace w/ more specific type
import clr
import System
from IronPythonTest.ExtensionMethodTest import LinqCollision
class TheTestCase(IronPythonTestCase):
    def test_namespace_reference(self):
        self.assertNotIn('Where', dir([]))
        clr.ImportExtensions(System.Linq)
        self.assertIn('Where', dir([]))
        self.assertEqual(list([2,3,4].Where(lambda x: x == 2)), [2])
        clr.ImportExtensions(LinqCollision)
        self.assertEqual([2,3,4].Where(lambda x: x == 2), 42)
""",

"""
import clr
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

class TheTestCase(IronPythonTestCase):
    def test_user_type(self):
        self.assertRaises(AttributeError, lambda : UserType().BaseClass)
        self.assertRaises(AttributeError, lambda : UserTypeWithSlots().BaseClass)
        self.assertEqual(UserTypeWithValue().BaseClass, 200)

        import clr
        from IronPythonTest.ExtensionMethodTest import ClassRelationship
        clr.ImportExtensions(ClassRelationship)

        self.assertEqual(object().BaseClass(), 23)
        self.assertEqual([].BaseClass(), 23)
        self.assertEqual({}.BaseClass(), 23)

        self.assertEqual(UserType().BaseClass(), 23)

        # dict takes precedence
        x = UserType()
        x.BaseClass = 100
        self.assertEqual(x.BaseClass, 100)

        # slots take precedence
        self.assertRaises(AttributeError, lambda : UserTypeWithSlots().BaseClass())
        self.assertEqual(UserTypeWithSlotsWithValue().BaseClass, 100)

        # dict takes precedence
        self.assertEqual(UserTypeWithValue().BaseClass, 200)
""",
"""
import clr
import System
from IronPythonTest.ExtensionMethodTest import ClassRelationship
clr.ImportExtensions(ClassRelationship)

class TheTestCase(IronPythonTestCase):
    def test_class_relationship(self):
        self.assertEqual([].Interface(), 23)
        self.assertEqual([].GenericInterface(), 23)
        self.assertEqual([].GenericInterfaceAndMethod(), 23)
        self.assertEqual([].GenericMethod(), 23)

        self.assertEqual(System.Array[System.Int32]([2,3,4]).Array(), 23)
        self.assertEqual(System.Array[int]([2,3,4]).Array(), 23)
        self.assertEqual(System.Array[int]([2,3,4]).ArrayAndGenericMethod(), 23)
        self.assertEqual(System.Array[int]([2,3,4]).GenericMethod(), 23)

        self.assertEqual(object().GenericMethod(), 23)
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

class TheTestCase(IronPythonTestCase):
    def test_extension_method(self):
        products = [Product(prod[0], prod[1], prod[2]) for prod in
            (('DrillRod', 'DR123', 45), ('Flange', 'F423', 12), ('Gizmo', 'G9872', 214), ('Sprocket', 'S534', 42))]

        pd = products.Where(lambda prod: prod.Q < 40).Select(lambda prod: (prod.Cat, prod.ID) )
        self.assertEqual(''.join(str(prod) for prod in pd), "('Flange', 'F423')")
        # blows: "Type System.Collections.Generic.IEnumerable`1[TSource] contains generic parameters"

        pd = products.Where(lambda prod: prod.Q < 40).AsEnumerable().Select(lambda prod: (prod.Cat, prod.ID) )
        self.assertEqual(''.join(str(prod) for prod in pd), "('Flange', 'F423')")

        pd = products.Where(lambda prod: prod.Q < 40)               #ok
        self.assertEqual(''.join((str(prod.Cat) + str(prod.ID) + str(prod.Q) for prod in pd)), 'FlangeF42312')

        pd2 = pd.Select(lambda prod: (prod.Cat, prod.ID) )        #blows, same exception
        self.assertEqual(''.join("Cat: {0}, ID: {1}".format(prod[0], prod[1]) for prod in pd2), "Cat: Flange, ID: F423")

        pd2 = products.Select(lambda prod: (prod.Cat, prod.ID) )    #ok

        self.assertEqual(''.join("Cat: {0}, ID: {1}".format(prod[0], prod[1]) for prod in pd2), 'Cat: DrillRod, ID: DR123Cat: Flange, ID: F423Cat: Gizmo, ID: G9872Cat: Sprocket, ID: S534')

        pd2 = list(pd).Select(lambda prod: (prod.Cat, prod.ID) )    #ok
        self.assertEqual(''.join("Cat: {0}, ID: {1}".format(prod[0], prod[1]) for prod in pd2), 'Cat: Flange, ID: F423')

        pd = products.Where(lambda prod: prod.Q < 30).ToList()    #blows, same exception
        self.assertEqual(''.join("Cat: {0}, ID: {1}".format(prod.Cat, prod.ID) for prod in pd), 'Cat: Flange, ID: F423')

        pd = list( products.Where(lambda prod: prod.Q < 30) )       #ok
        self.assertEqual(''.join("Cat: {0}, ID: {1}".format(prod.Cat, prod.ID) for prod in pd), 'Cat: Flange, ID: F423')

        # ok
        pd = list( products.Where(lambda prod: prod.Q < 40) ).Select(lambda prod: "Cat: {0}, ID: {1}, Qty: {2}".format(prod.Cat, prod.ID, prod.Q))
        self.assertEqual(''.join(prod for prod in pd), 'Cat: Flange, ID: F423, Qty: 12')

        # ok
        pd = ( list(products.Where(lambda prod: prod.Q < 40))
                .Select(lambda prod: "Cat: {0}, ID: {1}, Qty: {2}".format(prod.Cat, prod.ID, prod.Q)) )
        self.assertEqual(''.join(prod for prod in pd), 'Cat: Flange, ID: F423, Qty: 12')
"""
]
        temp_module = 'temp_module_%d' % os.getpid()
        fname = temp_module + '.py'
        for test_case in test_cases:
            try:
                old_path = [x for x in sys.path]
                sys.path.append('.')
                with open(fname, 'w+') as f:
                    f.write('''
from test import support
from iptest import IronPythonTestCase
''')
                    f.write(test_case)
                    f.write('''

support.run_unittest(TheTestCase)''')

                __import__(temp_module)
                del sys.modules[temp_module]
            finally:
                os.unlink(fname)
                sys.path = [x for x in old_path]



run_test(__name__)
