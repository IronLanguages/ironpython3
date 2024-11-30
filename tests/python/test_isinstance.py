# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import os
import sys
import unittest

from iptest import IronPythonTestCase, is_cli, is_mono, is_netcoreapp, big, path_modifier, run_test, skipUnlessIronPython

class IsInstanceTest(IronPythonTestCase):
    def test_file_io(self):
        fname = "testfile.tmp"

        def verify_file(ff, val="Hello"):
            cnt = 0
            for i in ff:
                self.assertTrue(i[0:5] == val)
                cnt += 1
            ff.close()
            self.assertTrue(cnt == 10)

        f = open(fname, "w")
        for i in range(10):
            f.write("Hello " + str(i) + "\n")
        f.close()

        f = open(fname)
        verify_file(f)
        f = open(fname, "r")
        verify_file(f)
        f = open(fname, "r", -1)
        verify_file(f)
        with self.assertRaises(ValueError):
            open(fname, "r", 0)

        if is_cli:
            import System
            fs = System.IO.FileStream(fname, System.IO.FileMode.Open, System.IO.FileAccess.Read)
            f = open(fs)
            verify_file(f, b"Hello")

            ms = System.IO.MemoryStream(30)
            f = open(ms)
            f.write(b"hello")
            f.flush()
            self.assertEqual(ms.Length, 5)
            self.assertEqual(ms.GetBuffer()[0], ord('h'))
            self.assertEqual(ms.GetBuffer()[4], ord('o'))
            ms.Close()

        import os
        os.remove(fname)

    def test_open(self):
        self.assertRaises(TypeError, open, None) # arg must be string
        self.assertRaises(TypeError, open, [])

    def test_compile(self):
        def max(a,b):
            if a>b: return a
            else: return b

        code = compile("max(10, 15)", "<string>", "eval")
        self.assertTrue(eval(code) == 15)

        code = compile("x = [1,2,3,4,5]\nx.reverse()\nself.assertTrue(x == [5,4,3,2,1])", "<string>", "exec")
        d = {"self": self}
        exec(code, d)
        self.assertTrue(d["x"] == [5,4,3,2,1])

        self.assertRaises(ValueError, compile, "2+2", "<string>", "invalid")
        self.assertRaises(SyntaxError, compile, "if 1 < 2: pass", "<string>", "eval")
        self.assertRaises(SyntaxError, compile, "a=2", "<string>", "eval")

        self.assertRaises(SyntaxError, eval, "a=2")


    def test_redirect(self):
        """stdin, stdout redirect and input, raw_input tests"""

        old_stdin = sys.stdin
        old_stdout = sys.stdout
        sys.stdout = open("testfile.tmp", "w")
        print("Into the file")
        print("2+2")
        sys.stdout.close()
        sys.stdout = old_stdout

        sys.stdin = open("testfile.tmp", "r")
        s = input()
        self.assertTrue(s == "Into the file")
        s = eval(input())
        self.assertTrue(s == 4)
        sys.stdin.close()
        sys.stdin = old_stdin

        f = open("testfile.tmp", "r")
        g = open("testfile.tmp", "r")
        s = f.readline()
        t = g.readline()
        self.assertTrue(s == t)
        self.assertTrue(s == "Into the file\n")

        f.close()
        g.close()

        f = open("testfile.tmp", "w")
        f.writelines(["1\n", "2\n", "2\n", "3\n", "4\n", "5\n", "6\n", "7\n", "8\n", "9\n", "0\n"])
        f.close()
        f = open("testfile.tmp", "r")
        l = f.readlines()
        self.assertTrue(l == ["1\n", "2\n", "2\n", "3\n", "4\n", "5\n", "6\n", "7\n", "8\n", "9\n", "0\n"])
        f.close()

        import os
        os.remove("testfile.tmp")

    def test_conversions(self):
        success=False
        try:
            nstr("Hi")
        except NameError:
            success=True
        self.assertEqual(success, True)

        success=False
        try:
            zip2([1,2,3],[4,5,6])
        except NameError:
            success=True
        self.assertEqual(success, True)

        self.assertEqual(bytes(), b"")
        self.assertEqual(str(), u"")

        self.assertEqual(oct(big(0)), "0o0")
        self.assertEqual(hex(12297829382473034410), "0xaaaaaaaaaaaaaaaa") #10581
        self.assertEqual(hex(-big(1)), "-0x1")
        self.assertEqual(int("-01"), -1)
        self.assertEqual(int(" 1 "), 1)
        with self.assertRaises(ValueError):
            int('09', 8)

        int_types = [int]
        if is_cli:
            from iptest import clr_int_types
            int_types.extend(clr_int_types)

        for f in int_types:
            self.assertRaises(ValueError, f, ' -   1  ')
            self.assertRaises(ValueError, f, 'p')
            self.assertRaises(ValueError, f, 't')
            self.assertRaises(ValueError, f, 'a')
            self.assertRaises(ValueError, f, '3.2')
            self.assertRaises(ValueError, f, '0x0R')
            self.assertRaises(ValueError, f, '0A')
            self.assertRaises(ValueError, f, '0x0G')
            self.assertRaises(ValueError, f, '1l')
            self.assertRaises(ValueError, f, '1L')

        self.assertEqual(int(1e100), 10000000000000000159028911097599180468360808563945281389781327557747838772170381060813469985856815104)
        self.assertEqual(int(-1e100), -10000000000000000159028911097599180468360808563945281389781327557747838772170381060813469985856815104)

        self.assertEqual(pow(2,3), 8)

    def test_type_properties(self):
        self.assertEqual(type(type), type.__class__)

    def test_reload_sys(self):
        import importlib
        import sys

        (old_copyright, old_byteorder) = (sys.copyright, sys.byteorder)
        (sys.copyright, sys.byteorder) = ("foo", "foo")

        old_argv = sys.argv
        sys.argv = "foo"

        reloaded_sys = importlib.reload(sys)

        # Most attributes get reset
        if sys.version_info >= (3,5):
            self.assertEqual(("foo", "foo"), (reloaded_sys.copyright, reloaded_sys.byteorder))
        else:
            self.assertEqual((old_copyright, old_byteorder), (reloaded_sys.copyright, reloaded_sys.byteorder))
        # Some attributes are not reset
        self.assertEqual(reloaded_sys.argv, "foo")
        # Put back the original values
        (sys.copyright, sys.byteorder) = (old_copyright, old_byteorder)
        sys.argv = old_argv

    def test_hijacking_builtins(self):
        # BUG 433: CPython allows hijacking of __builtins__, but IronPython does not
        bug_433 = '''
        def foo(arg):
            return "Foo"

        # trying to override an attribute of __builtins__ causes a TypeError
        try:
            __builtins__.oct = foo
            self.assertTrue(False, "Cannot override an attribute of __builtins__")
        except TypeError:
            pass

        # assigning to __builtins__ passes, but doesn't actually affect function semantics
        import custombuiltins
        '''
        # /BUG

    def test_custom_mapping(self):
        class MyMapping:
            def __getitem__(self, index):
                    if index == 'a': return 2
                    if index == 'b': return 5
                    raise IndexError('bad index')

        self.assertEqual(eval('a+b', {}, MyMapping()), 7)

    def test_eval_dicts(self):
        # eval referencing locals / globals
        global global_value
        value_a = 13
        value_b = 17
        global_value = 23
        def eval_using_locals():
            value_a = 3
            value_b = 7
            self.assertEqual(eval("value_a"), 3)
            self.assertEqual(eval("value_b"), 7)
            self.assertEqual(eval("global_value"), 23)
            self.assertEqual(eval("value_a < value_b"), True)
            self.assertEqual(eval("global_value < value_b"), False)
            return True

        self.assertTrue(eval_using_locals())

        if is_cli:
            import System
            if System.BitConverter.IsLittleEndian == True:
                self.assertTrue(sys.byteorder == "little")
            else:
                self.assertTrue(sys.byteorder == "big")

        sortedDir = 3
        sortedDir = dir()
        sortedDir.sort()
        self.assertTrue(dir() == sortedDir)

    def test_getattr(self):
        ## getattr/hasattr: hasattr should eat exception, and return True/False
        class C1:
            def __init__(self):
                self.field = C1
            def method(self):
                return "method"
            def __getattr__(self, attrname):
                if attrname == "lambda":
                    return lambda x: len(x)
                elif attrname == "myassert":
                    raise AssertionError
                else:
                    raise AttributeError(attrname)

        class C2(object):
            def __init__(self):
                self.field = C1
            def method(self):
                return "method"
            def __getattr__(self, attrname):
                if attrname == "lambda":
                    return lambda x: len(x)
                elif attrname == "myassert":
                    raise AssertionError
                else:
                    raise AttributeError(attrname)

        def getattrhelper(t):
            o = t()
            self.assertEqual(getattr(o, "field"), C1)
            self.assertEqual(getattr(o, "method")(), "method")
            self.assertEqual(getattr(o, "lambda")("a"), 1)

            self.assertRaises(AssertionError, getattr, o, "myassert")
            self.assertRaises(AttributeError, getattr, o, "anything")
            self.assertRaises(AttributeError, getattr, o, "else")

            for attrname in ('field', 'method', '__init__', '__getattr__', 'lambda', '__doc__', '__module__'):
                self.assertEqual(hasattr(o, attrname), True)

            with self.assertRaises(AssertionError):
                hasattr(o,"myassert")

            for attrname in ("anything", "else"):
                self.assertEqual(hasattr(o, attrname), False)

        getattrhelper(C1)
        getattrhelper(C2)

    def test_inheritance_ctor(self):
        """derived from python native type, and create instance of them without arg"""
        global flag
        flag = 0
        def myinit(self):
            global flag
            flag = flag + 1

        cnt = 0
        for bt in (tuple, dict, list, str, set, frozenset, int, float, complex):
            nt = type("derived", (bt,), dict())
            inst = nt()
            self.assertEqual(type(inst), nt)

            nt2 = type("derived2", (nt,), dict())
            inst2 = nt2()
            self.assertEqual(type(inst2), nt2)

            nt.__init__ = myinit
            inst = nt()
            cnt += 1
            self.assertEqual(flag, cnt)
            self.assertEqual(type(inst), nt)

    def test_subclassing_builtins(self):
        """sub classing built-ins works correctly.."""
        class C(list):
            def __eq__(self, other):
                return 'Passed'

        self.assertEqual(C() == 1, 'Passed')

    def test_extensible_types_hashing(self):
        """extensible types should hash the same as non-extensibles, and unary operators should work too"""
        for x, y in ( (int, 2), (str, 'abc'), (float, 2.0), (complex, 2+0j) ):
            class foo(x): pass

            self.assertEqual(hash(foo(y)), hash(y))

            if x != str:
                self.assertEqual(-foo(y), -y)
                self.assertEqual(+foo(y), +y)

                if x != complex and x != float:
                    self.assertEqual(~foo(y), ~y)

    def test_kwargs_open(self):
        """can use kw-args w/ open"""
        fname = 'temporary_%d.deleteme' % os.getpid()
        f = open(file=fname, mode='w')
        f.close()
        os.unlink(fname)

    def test_kwargs_primitives(self):
        if sys.version_info >= (3,7):
            # starting with 3.7 these are no longer valid keyword arguments
            with self.assertRaises(TypeError): int(x=1)
            with self.assertRaises(TypeError): float(x=2)
            with self.assertRaises(TypeError): tuple(sequence=range(3))
            with self.assertRaises(TypeError): list(sequence=(0,1))
        else:
            self.assertEqual(int(x=1), 1)
            self.assertEqual(float(x=2), 2.0)
            self.assertEqual(tuple(sequence=range(3)), (0,1,2))
            self.assertEqual(list(sequence=(0,1)), [0,1])

        self.assertEqual(complex(imag=4, real=3), 3 + 4j)
        self.assertEqual(str(object=5), '5')
        self.assertEqual(str(object=b'a', errors='strict'), 'a')

    def test_issubclass(self):
        for (x, y) in [("2", int), (2, int), ("string", str), (None, int), (str, None), (int, 3), (int, (6, 7))]:
            self.assertRaises(TypeError, lambda: issubclass(x, y))

    @skipUnlessIronPython()
    def test_cli_subclasses(self):
        import clr

        self.assertTrue(issubclass(int, int))
        self.assertTrue(not issubclass(str, int))
        self.assertTrue(not issubclass(int, (str, str)))
        self.assertTrue(issubclass(int, (str, int)))

        class basestring_subclass(str):
            pass
        self.assertTrue(issubclass(basestring_subclass, str))

        self.assertEqual(str(None), "None")
        self.assertTrue(issubclass(type(None),type(None)))
        self.assertEqual(str(type(None)), "<class 'NoneType'>")
        self.assertEqual(str(1), "1")
        self.assertTrue('__str__' in dir(None))

        def tryAssignToNoneAttr():
            None.__doc__ = "Nothing!"

        def tryAssignToNoneNotAttr():
            None.notanattribute = "";

        self.assertRaises(AttributeError, tryAssignToNoneAttr)
        self.assertRaises(AttributeError, tryAssignToNoneNotAttr)
        v = None.__doc__
        v = None.__new__
        v = None.__hash__
        self.assertEqual("<class 'NoneType'>", str(type(None)))

        import sys
        self.assertEqual(str(sys), "<module 'sys' (built-in)>")

        import time
        with path_modifier(self.test_dir):
            import toimport

        m = [type(sys), type(time), type(toimport)]
        for i in m:
            for j in m:
                self.assertTrue(issubclass(i,j))

        self.assertRaises(TypeError, type, None, None, None) # arg 1 must be string
        self.assertRaises(TypeError, type, "NewType", None, None) # arg 2 must be tuple
        self.assertRaises(TypeError, type, "NewType", (), None) # arg 3 must be dict


        def splitTest():
            "string".split('')
        self.assertRaises(ValueError, splitTest)

    @skipUnlessIronPython()
    def test_primitive_inheritance(self):
        import System

        def InheritFromType(t):
            class InheritedType(t): pass
            return InheritedType

        self.assertRaises(TypeError, InheritFromType, System.Int64)
        self.assertRaises(TypeError, InheritFromType, System.Boolean)

        # isinstance

        self.assertTrue(isinstance(System.Int64(), System.Int64) == True)
        self.assertTrue(isinstance(System.Boolean(), System.Boolean) == True)

        self.assertTrue(isinstance(1, System.Int64) == False)
        self.assertTrue(isinstance(1, System.Boolean) == False)

        class userClass(object): pass
        self.assertTrue(isinstance(userClass(), System.Int64) == False)
        self.assertTrue(isinstance(userClass(), System.Boolean) == False)

        # issubclass

        self.assertTrue(issubclass(System.Int64, System.Int64) == True)
        self.assertTrue(issubclass(System.Boolean, System.Boolean) == True)

        self.assertTrue(issubclass(type(1), System.Int64) == False)
        self.assertTrue(issubclass(type(1), System.Boolean) == False)

        self.assertTrue(issubclass(userClass, System.Int64) == False)
        self.assertTrue(issubclass(userClass, System.Boolean) == False)

    @skipUnlessIronPython()
    def test_cli_types(self):
        import System
        arrayMapping = {'b': System.SByte, 'h': System.Int16, 'H': System.UInt16, 'i': System.Int32,
                        'I': System.UInt32, 'l': System.Int64, 'L': System.UInt64, 'f': System.Single, 'd': System.Double }

        def tryConstructValues(validate, *args):
            for x in arrayMapping.keys():
                # construct from DynamicType
                y = System.Array[arrayMapping[x]](*args)
                self.assertEqual(y.GetType().GetElementType(), arrayMapping[x]().GetType())
                validate(y, *args)

                # construct from CLR type
                y = System.Array[y.GetType().GetElementType()](*args)
                self.assertEqual(y.GetType().GetElementType(), arrayMapping[x]().GetType())
                validate(y, *args)


        def tryConstructSize(validate, *args):
            for x in arrayMapping.keys():
                # construct from DynamicType
                y = System.Array.CreateInstance(arrayMapping[x], *args)

                self.assertEqual(y.GetType().GetElementType(), arrayMapping[x]().GetType())
                validate(y, *args)

                # construct from CLR type
                y = System.Array.CreateInstance(y.GetType().GetElementType(), *args)
                self.assertEqual(y.GetType().GetElementType(), arrayMapping[x]().GetType())
                validate(y, *args)


        def validateLen(res, *args):
            self.assertEqual(len(res), *args)

        def validateVals(res, *args):
            len(res) == len(args)
            for x in range(len(args[0])):
                try:
                    lhs = int(res[x])
                    rhs = int(args[0][x])
                except:
                    lhs = float(res[x])
                    rhs = float(args[0][x])
                self.assertEqual(lhs, rhs)

        def validateValsIter(res, *args):
            len(res) == len(args)
            for x in range(len(args)):
                print(int(res[x]), args[0][x])
                self.assertEqual(int(res[x]), int(args[0][x]))


        class MyList(object):
            def __len__(self):
                return 4
            def __iter__(self):
                yield 3
                yield 4
                yield 5
                yield 6

        def validateValsIter(res, *args):
            compList = MyList()
            len(res) == len(args)
            index = 0
            for x in compList:
                try:
                    lhs = int(res[index])
                    rhs = int(x)
                except Exception as e:
                    lhs = float(res[index])
                    rhs = float(x)

                self.assertEqual(lhs, rhs)
                index += 1



        tryConstructSize(validateLen, 0)
        tryConstructSize(validateLen, 1)
        tryConstructSize(validateLen, 20)

        tryConstructValues(validateVals, (3,4,5,6))
        tryConstructValues(validateVals, [3,4,5,6])

        tryConstructValues(validateValsIter, MyList())


    def test_metaclass_ctor_init(self):
        """normal meta class construction & initialization"""
        global metaInit
        global instInit
        metaInit = False
        instInit = False
        class MetaType(type):
            def someFunction(cls):
                return "called someFunction"
            def __init__(cls, name, bases, dct):
                global metaInit
                metaInit = True
                super(MetaType, cls).__init__(name, bases,dct)
                cls.xyz = 'abc'

        class MetaInstance(object, metaclass=MetaType):
            def __init__(self):
                global instInit
                instInit = True

        a = MetaInstance()
        self.assertEqual(metaInit, True)
        self.assertEqual(instInit, True)

        self.assertEqual(MetaInstance.xyz, 'abc')
        self.assertEqual(MetaInstance.someFunction(), "called someFunction")


        class MetaInstance(object, metaclass=MetaType):
            def __init__(self, xyz):
                global instInit
                instInit = True
                self.val = xyz

        metaInit = False
        instInit = False

        a = MetaInstance('def')
        self.assertEqual(instInit, True)
        self.assertEqual(MetaInstance.xyz, 'abc')
        self.assertEqual(a.val, 'def')
        self.assertEqual(MetaInstance.someFunction(), "called someFunction")

        # initialization by calling the metaclass type.

        Foo = MetaType('foo', (), {})

        self.assertEqual(type(Foo), MetaType)

        instInit = False
        def newInit(self):
            global instInit
            instInit = True

        Foo = MetaType('foo', (), {'__init__':newInit})
        a = Foo()
        self.assertEqual(instInit, True)

    @skipUnlessIronPython()
    def test_unhashable_types(self):
        import System
        class OldUserClass:
            def foo(): pass
        import _weakref
        from _collections import deque

        self.assertRaises(TypeError, hash, slice(None))
        hashcode = System.Object.GetHashCode(slice(None))

        # weakproxy
        self.assertRaises(TypeError, hash, _weakref.proxy(OldUserClass()))
        hashcode = System.Object.GetHashCode(_weakref.proxy(OldUserClass()))

        # weakcallableproxy
        self.assertRaises(TypeError, hash, _weakref.proxy(OldUserClass().foo))
        hashcode = System.Object.GetHashCode(_weakref.proxy(OldUserClass().foo))

        self.assertRaises(TypeError, hash, deque())
        hashcode = System.Object.GetHashCode(deque())

        self.assertRaises(TypeError, hash, dict())
        hashcode = System.Object.GetHashCode(dict())

        self.assertRaises(TypeError, hash, list())
        hashcode = System.Object.GetHashCode(list())

        self.assertRaises(TypeError, hash, set())
        hashcode = System.Object.GetHashCode(set())


    @skipUnlessIronPython()
    def test_builtin_attributes(self):
        import System
        def AssignMethodOfBuiltin():
            def mylen(): pass
            l = list()
            l.len = mylen
        self.assertRaises(AttributeError, AssignMethodOfBuiltin)

        def DeleteMethodOfBuiltin():
            l = list()
            del l.len
        self.assertRaises(AttributeError, DeleteMethodOfBuiltin)

        def SetAttrOfBuiltin():
            l = list()
            l.attr = 1
        self.assertRaises(AttributeError, SetAttrOfBuiltin)

        def SetDictElementOfBuiltin():
            l = list()
            l.__dict__["attr"] = 1
        self.assertRaises(AttributeError, SetDictElementOfBuiltin)

        def SetAttrOfCLIType():
            d = System.DateTime()
            d.attr = 1
        self.assertRaises(AttributeError, SetAttrOfCLIType)

        def SetDictElementOfCLIType():
            d = System.DateTime()
            d.__dict__["attr"] = 1
        self.assertRaises(AttributeError, SetDictElementOfCLIType)

        self.assertRaisesMessage(TypeError, "vars() argument must have __dict__ attribute", vars, list())
        self.assertRaisesMessage(TypeError, "vars() argument must have __dict__ attribute", vars, System.DateTime())


    @skipUnlessIronPython()
    def test_explicit_interface_impl(self):
        import System
        self.assertEqual(System.IConvertible.ToDouble('32', None), 32.0)


    @skipUnlessIronPython()
    def test_mutable_Valuetypes(self):
        self.load_iron_python_test()
        from IronPythonTest import MySize, BaseClass

        direct_vt = MySize(1, 2)
        embedded_vt = BaseClass()
        embedded_vt.Width = 3
        embedded_vt.Height = 4

        # Read access should still succeed.
        self.assertEqual(direct_vt.width, 1)
        self.assertEqual(embedded_vt.size.width, 3)
        self.assertEqual(embedded_vt.Size.width, 3)

        # But writes to value type fields should fail with ValueError.
        success = 0
        try:
            direct_vt.width = 5
        except ValueError:
            success = 1
        self.assertTrue(success == 0 and direct_vt.width == 1)

        success = 0
        try:
            embedded_vt.size.width = 5
        except ValueError:
            success = 1
        self.assertTrue(success == 0 and embedded_vt.size.width == 3)

        success = 0
        try:
            embedded_vt.Size.width = 5
        except ValueError:
            success = 1
        self.assertTrue(success == 0 and embedded_vt.Size.width == 3)

        import clr
        # ensure .GetType() and calling the helper w/ the type work
        self.assertEqual(clr.GetClrType(str), ''.GetType())
        # and ensure we're not just auto-converting back on both of them
        self.assertEqual(clr.GetClrType(str), str)
        self.assertEqual(clr.GetClrType(str) != str, False)

        # as well as GetPythonType
        clr.AddReference("System.Numerics")
        import System
        self.assertEqual(clr.GetPythonType(System.Numerics.BigInteger), int)
        self.assertEqual(clr.GetPythonType(clr.GetClrType(int)), int)

        # verify we can't create *Ops classes
        from IronPython.Runtime.Operations import DoubleOps
        self.assertRaises(TypeError, DoubleOps)

        # setting mro to an invalid value should result in
        # bases still being correct
        class foo(object): pass

        class bar(foo): pass

        class baz(foo): pass

        def changeBazBase():
            baz.__bases__ = (foo, bar)  # illegal MRO

        self.assertRaises(TypeError, changeBazBase)

        self.assertEqual(baz.__bases__, (foo, ))
        self.assertEqual(baz.__mro__, (baz, foo, object))

        d = {}
        d[None, 1] = 2
        self.assertEqual(d, {(None, 1): 2})


    def test_int_minvalue(self):
        # Test for type of System.Int32.MinValue
        self.assertEqual(type(-2147483648), int)
        self.assertEqual(type(-(2147483648)), int)
        self.assertEqual(type(-int(2147483648)), int)
        self.assertEqual(type(-0x80000000), int)

        self.assertEqual(type(int('-2147483648')), int)
        self.assertEqual(type(int('-80000000', 16)), int)
        self.assertEqual(type(int('-2147483649')), int)
        self.assertEqual(type(int('-80000001', 16)), int)


        if is_cli:
            import clr
            import System

            # verify our str.split doesn't replace CLR's String.Split
            chars = System.Array[str]([' '])
            res = 'a b  c'.Split(chars, System.StringSplitOptions.RemoveEmptyEntries)
            self.assertEqual(res[0], 'a')
            self.assertEqual(res[1], 'b')
            self.assertEqual(res[2], 'c')


    def test_mro(self):
        # valid
        class C(object): pass

        class D(object): pass

        class E(D): pass

        class F(C, E): pass

        self.assertEqual(F.__mro__, (F,C,E,D,object))

        # valid
        class A(object): pass

        class B(object): pass

        class C(A,B): pass

        class D(A,B): pass

        class E(C,D): pass

        self.assertEqual(E.__mro__, (E,C,D,A,B,object))

        # invalid
        class A(object): pass

        class B(object): pass

        class C(A,B): pass

        class D(B,A): pass

        try:
            class E(C,D): pass
            self.fail("Unreachable code reached")
        except TypeError:
            pass


    def test_type_call_kwargs(self):
        self.assertEqual(complex(real=2), (2+0j))

    def test_bad_addition(self):
        try:
            2.0 + "2.0"
            self.fail("Unreachable code reached")
        except TypeError: pass

    def test_class_property(self):
        class foo(object): pass

        self.assertEqual(foo.__class__, type)

        class foo(type): pass

        class bar(object, metaclass=foo):
            pass

        self.assertEqual(bar.__class__, foo)

    def test_metaclass_order(self):
        global metaCalled
        metaCalled = []
        class BaseMeta(type):
            def __new__(cls, name, bases, dict):
                global metaCalled
                metaCalled.append(cls)
                return type.__new__(cls, name, bases, dict)

        class DerivedMeta(BaseMeta): pass

        class A(metaclass=BaseMeta):
            pass

        self.assertEqual(metaCalled, [BaseMeta])

        metaCalled = []

        class B(metaclass=DerivedMeta):
            pass

        self.assertEqual(metaCalled, [DerivedMeta])

        metaCalled = []
        class C(A,B): pass

        self.assertEqual(metaCalled, [DerivedMeta])
        self.assertEqual(type(C).__name__, 'DerivedMeta')


        class E(object):
            def getbases(self):
                raise RuntimeError
            __bases__ = property(getbases)

        class I(object):
            def getclass(self):
                return E()
            __class__ = property(getclass)

        class C(object):
            def getbases(self):
                return ()
            __bases__ = property(getbases)

        self.assertRaises(RuntimeError, isinstance, I(), C())

        class C1(object): pass
        class C2(object): pass

        self.assertEqual(isinstance(C1(), C2), False)
        self.assertEqual(isinstance(C1(), (C2, C2)), False)
        self.assertEqual(isinstance(C1(), (C2, C2, (C2, C2), C2)), False)
        self.assertEqual(isinstance(C1(), (C2, C2, (C2, (C2, C1), C2), C2)), True)

        class C1: pass
        class C2: pass

        self.assertEqual(isinstance(C1(), C2), False)
        self.assertEqual(isinstance(C1(), (C2, C2)), False)
        self.assertEqual(isinstance(C1(), (C2, C2, (C2, C2), C2)), False)
        self.assertEqual(isinstance(C1(), (C2, C2, (C2, (C2, C1), C2), C2)), True)

        class MyInt(int): pass

        self.assertTrue(isinstance(MyInt(), int))

    def test_class_access(self):
        call_tracker = []
        class C(object):
            def getclass(self):
                call_tracker.append("C.getclass")
                return C
            __class__ = property(getclass)

        self.assertEqual(isinstance(C(), object), True)
        self.assertEqual(call_tracker, [])

        self.assertEqual(isinstance(C(), str), False)
        self.assertEqual(call_tracker, ["C.getclass"])

        call_tracker = []
        self.assertEqual(isinstance(C(), (str, (type, str, float))), False)
        self.assertEqual(call_tracker, ['C.getclass', 'C.getclass', 'C.getclass', 'C.getclass'])


    def test_base_access(self):
        call_tracker = []

        class C(object):
            def getbases(self):
                call_tracker.append("C.getbases")
                return (E,)
            __bases__ = property(getbases)

        class E(object):
            def getbases(self):
                call_tracker.append("E.getbases")
                return ()
            __bases__ = property(getbases)

        class D(object):
            def getclass(self):
                call_tracker.append("D.getclass")
                return C()
            __class__ = property(getclass)

        self.assertEqual(isinstance(D(), E()), False)
        self.assertEqual(call_tracker, ['E.getbases', 'D.getclass', 'C.getbases'])

        class I(object):
            def getclass(self):
                return None
            __class__ = property(getclass)

        class C(object):
            def getbases(self):
                return ()
            __bases__ = property(getbases)

        self.assertRaises(TypeError, isinstance, I(), None)
        self.assertRaises(TypeError, isinstance, 3, None)
        self.assertRaises(TypeError, issubclass, int, None)

    @unittest.skipIf(is_netcoreapp or is_mono, "https://github.com/IronLanguages/ironpython2/issues/347")
    def test_tuple_new(self):
        # TypeError: tuple.__new__(str): str is not a subtype of tuple
        self.assertRaises(TypeError, tuple.__new__, str)
        self.assertRaises(TypeError, tuple.__new__, str, 'abc')

run_test(__name__)
