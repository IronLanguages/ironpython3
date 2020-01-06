# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

"""
This module consists of regression tests for CodePlex and Dev10 IronPython bugs
added primarily by IP developers that need to be folded into other test modules
and packages.

Any test case added to this file should be of the form:
    def test_cp1234(): ...
where 'cp' refers to the fact that the test case is for a regression on CodePlex
(use 'dev10' for Dev10 bugs).  '1234' should refer to the CodePlex or Dev10
Work Item number.
"""

import os
import sys
import unittest

from iptest import IronPythonTestCase, is_cli, is_mono, is_netcoreapp, is_posix, run_test, skipUnlessIronPython, stdout_trapper

class RegressionTest(IronPythonTestCase):

    @unittest.skipIf(is_netcoreapp, 'no System.AppDomain.DoCallBack')
    @skipUnlessIronPython()
    def test_cp18345(self):
        import System
        import time
        class x(object):
            def f(self):
                global z
                z = 100

        System.AppDomain.CurrentDomain.DoCallBack(x().f)
        time.sleep(10)
        self.assertEqual(z, 100)

    @unittest.skipIf(is_netcoreapp, 'TODO: figure out')
    def test_cp17420(self):
        #Create a temporary Python file
        test_file_name = os.path.join(self.temporary_dir, "cp17420.py")
        test_log_name  = os.path.join(self.temporary_dir, "cp17420.log")
        try:
            os.remove(test_log_name)
        except:
            pass

        test_file = '''
output = []
for i in range(0, 100):
    output.append(str(i) + "\\n")

with open(r"%s", "w") as f:
    f.writelines(output)''' % (test_log_name)

        self.write_to_file(test_file_name, test_file)

        #Execute the file from a separate process
        self.assertEqual(self.launch(sys.executable, test_file_name), 0)

        #Verify contents of file
        with open(test_log_name, "r") as temp_file:
            lines = temp_file.readlines()

        self.assertEqual(len(lines), 100)

        os.unlink(test_file_name)
        os.unlink(test_log_name)

    def test_cp17274(self):
        class KOld:
            def __init__(self):
                self.__doc__ = "KOld doc"

        class KNew(object):
            def __init__(self):
                self.__doc__ = "KNew doc"

        class KNewDerived(KNew, KOld):
            def method(self):
                self.__doc__ = "KNewDerived doc"

        class KNewDerivedSpecial(int):
            def __init__(self):
                self.__doc__ = "KNewDerivedSpecial doc"

        self.assertEqual(KOld().__doc__, "KOld doc")
        self.assertEqual(KNew().__doc__, "KNew doc")
        k = KNewDerived()
        self.assertEqual(k.__doc__, "KNew doc")
        k.method()
        self.assertEqual(k.__doc__, "KNewDerived doc")
        self.assertEqual(KNewDerivedSpecial().__doc__, "KNewDerivedSpecial doc")


    @skipUnlessIronPython()
    def test_cp16831(self):
        import clr
        clr.AddReference("IronPythonTest")
        import IronPythonTest
        temp = IronPythonTest.NullableTest()

        temp.BProperty = True
        for i in range(2):
            if not temp.BProperty:
                Fail("Nullable Boolean was set to True")
        for i in range(2):
            if not temp.BProperty==True:
                Fail("Nullable Boolean was set to True")

        temp.BProperty = False
        for i in range(2):
            if temp.BProperty:
                Fail("Nullable Boolean was set to False")
        for i in range(2):
            if not temp.BProperty==False:
                Fail("Nullable Boolean was set to False")

        temp.BProperty = None
        for i in range(2):
            if temp.BProperty:
                Fail("Nullable Boolean was set to None")
        for i in range(2):
            if not temp.BProperty==None:
                Fail("Nullable Boolean was set to None")

    def test_cp_27434(self):
        tests = {
            '\d' : 0,
            '(\d)' : 1,
            '(\d) (\w)' : 2,
            '(?:[\d\.]+) (\w)' : 1,
            '(hello(\w)*world) [\d\.]?' : 2,
            '(hello(\w)*world) ([\d\.]?)' : 3,
            '(hello(\w)*world) (?:[\d\.]?)' : 2,
        }

        import re
        for data, groups in tests.items():
            regex = re.compile(data)
            message = "'%s' should have %d groups, not %d" % (data, groups, regex.groups)
            self.assertTrue(regex.groups == groups, message)

    @skipUnlessIronPython()
    def test_protected_ctor_inheritance_cp20021(self):
        self.load_iron_python_test()
        from IronPythonTest import (
            ProtectedCtorTest, ProtectedCtorTest1, ProtectedCtorTest2,
            ProtectedCtorTest3, ProtectedCtorTest4,
            ProtectedInternalCtorTest, ProtectedInternalCtorTest1,
            ProtectedInternalCtorTest2, ProtectedInternalCtorTest3,
            ProtectedInternalCtorTest4

        )

        # no number:
        protected = [ProtectedCtorTest, ProtectedCtorTest1, ProtectedCtorTest2,
                    ProtectedCtorTest3, ProtectedCtorTest4, ]
        protected_internal = [ProtectedInternalCtorTest, ProtectedInternalCtorTest1,
                            ProtectedInternalCtorTest2, ProtectedInternalCtorTest3,
                            ProtectedInternalCtorTest4, ]

        for zero, one, two, three, four in (protected, protected_internal):
            # calling protected ctors shouldn't work
            self.assertRaises(TypeError, zero)
            self.assertRaises(TypeError, zero.__new__)

            self.assertRaises(TypeError, one, object())
            self.assertRaises(TypeError, one.__new__, object())

            self.assertRaises(TypeError, two, object())
            self.assertRaises(TypeError, two.__new__, two, object())
            self.assertRaises(TypeError, two, object(), object())
            self.assertRaises(TypeError, two.__new__, two, object(), object())

            self.assertRaises(TypeError, three)
            self.assertRaises(TypeError, three.__new__, three)

            three(object())
            three.__new__(ProtectedCtorTest3, object())

            self.assertRaises(TypeError, four, object())
            self.assertRaises(TypeError, four.__new__, four, object())

            four()
            four.__new__(four)

            class myzero(zero):
                def __new__(cls): return zero.__new__(cls)
            class myone(one):
                def __new__(cls): return one.__new__(cls, object())
            class mytwo1(two):
                def __new__(cls): return two.__new__(cls, object())
            class mytwo2(two):
                def __new__(cls): return two.__new__(cls, object(), object())
            class mythree1(three):
                def __new__(cls): return three.__new__(cls)
            class mythree2(three):
                def __new__(cls): return three.__new__(cls, object())
            class myfour1(four):
                def __new__(cls): return four.__new__(cls)
            class myfour2(four):
                def __new__(cls): return four.__new__(cls, object())

            for cls in [myzero, myone, mytwo1, mytwo2, mythree1, mythree2, myfour1, myfour2]:
                cls()

    def test_re_paren_in_char_list_cp20191(self):
        import re
        format_re = re.compile(r'(?P<order1>[<>|=]?)(?P<repeats> *[(]?[ ,0-9]*[)]? *)(?P<order2>[<>|=]?)(?P<dtype>[A-Za-z0-9.]*)')

        self.assertEqual(format_re.match('a3').groups(), ('', '', '', 'a3'))


    def test_struct_uint_bad_value_cp20039(self):
        class x(object):
            def __init__(self, value):
                self.value = value
            def __and__(self, other):
                global andCalled
                andCalled = True
                return self.value
            def __int__(self):
                raise Exception('foo')

        import _struct
        global andCalled
        andCalled = False

        self.assertRaisesRegex(_struct.error, "integer out of range for 'L' format code",
                            _struct.Struct('L').pack, 4294967296)
        self.assertRaisesRegex(_struct.error, "integer out of range for 'L' format code",
                            _struct.Struct('L').pack, -1)
        self.assertRaisesRegex(Exception, "foo",
                            _struct.Struct('L').pack, x(0))
        self.assertRaisesRegex(Exception, "foo", _struct.Struct('L').pack, x(-1))

        self.assertRaisesRegex(_struct.error, "integer out of range for 'I' format code",
                            _struct.Struct('I').pack, 4294967296)
        self.assertRaisesRegex(_struct.error, "integer out of range for 'I' format code",
                            _struct.Struct('I').pack, -1)
        self.assertRaisesRegex(Exception, "foo",
                            _struct.Struct('I').pack, x(0))
        self.assertRaisesRegex(Exception, "foo", _struct.Struct('I').pack, x(-1))

        # __and__ was called in Python2.6 check that this is no longer True
        self.assertTrue(not andCalled)

    def test_reraise_backtrace_cp20051(self):
        '''
        TODO: this test needs far better verification.
        '''
        import sys
        def foo():
            some_exception_raising_code()

        try:
            try:
                foo()
            except:
                excinfo1 = sys.exc_info()[2]
                exc1_list = []
                while excinfo1:
                    exc1_list.append((excinfo1.tb_frame.f_code.co_filename,
                                    excinfo1.tb_frame.f_code.co_name,
                                    excinfo1.tb_frame.f_lineno))
                    excinfo1 = excinfo1.tb_next
                raise
        except Exception as e:
            excinfo2 = sys.exc_info()[2]
            exc2_list = []
            while excinfo2:
                exc2_list.append((excinfo2.tb_frame.f_code.co_filename,
                                excinfo2.tb_frame.f_code.co_name,
                                excinfo2.tb_frame.f_lineno))
                excinfo2 = excinfo2.tb_next

            # CPython reports 2 frames, IroPython includes the re-raise and reports 3
            self.assertTrue(len(exc2_list) >= 2)

    @unittest.skipIf(is_posix, 'No _winreg on posix')
    def test_winreg_error_cp17050(self):
        import winreg
        self.assertEqual(winreg.error, WindowsError)

    @skipUnlessIronPython()
    def test_indexing_value_types_cp20370(self):
        import clr
        if is_netcoreapp:
            clr.AddReference("System.Drawing.Primitives")
        else:
            clr.AddReference("System.Drawing")
        from System.Drawing import Point

        p = Point(1,2)
        l = [None]
        l[0] = p
        self.assertEqual(id(l[0]), id(p))
        self.assertEqual(id(l[0]), id(p))

        x = {}
        x[p] = p
        self.assertEqual(id(list(x.keys())[0]), id(p))
        self.assertEqual(id(list(x.values())[0]), id(p))

        self.load_iron_python_test()

        from IronPythonTest import StructIndexable
        a = StructIndexable()
        a[0] = 1
        self.assertEqual(a[0], 1)

    def test_enumerate_index_increment_cp20016(self):
        def f(item):
            return item[0] in [0, 1]

        self.assertEqual(list(filter(f, enumerate(['a', 'b']))), [(0, 'a'), (1, 'b')])
        self.assertEqual(list(filter(lambda x: x[0] in [0, 1], enumerate([10.0, 27.0]))),
                [(0, 10.0), (1, 27.0)])

    def test_invalid_args_cp20616(self):
        test_cases = {
            lambda: ''.join() : "join() takes exactly one argument (0 given)",
            lambda: ''.join("", "") : "join() takes exactly one argument (2 given)",
            lambda: ''.join("", "", "") : "join() takes exactly one argument (3 given)",
            lambda: ''.replace("", "", "", "") : "replace() takes at most 3 arguments (4 given)",
        }
        if is_cli:
            import clr
            import System
            test_cases.update({
                                lambda: System.String("").PadRight() : "PadRight() takes at least 1 argument (0 given)",
                                lambda: System.String("").PadRight(1, "a", "") : "PadRight() takes at most 2 arguments (3 given)",
                            })
        #CodePlex 21063
        if is_cli:
            for key in test_cases:
                test_cases[key] = test_cases[key].replace("one", "1")

        for key in test_cases:
            temp_lambda = key
            expected_err_msg = test_cases[key]
            self.assertRaisesMessage(TypeError, expected_err_msg, temp_lambda)

    def test_cp19678(self):
        global iterCalled, getItemCalled
        iterCalled = False
        getItemCalled = False
        class o(object):
            def __iter__(self):
                global iterCalled
                iterCalled = True
                return iter([1, 2, 3])
            def __getitem__(self, index):
                global getItemCalled
                getItemCalled = True
                return [1, 2, 3][index]
            def __len__(self):
                return 3

        self.assertEqual(1 in o(), True)
        self.assertEqual(iterCalled, True)
        self.assertEqual(getItemCalled, False)


    def test_exception_multiple_inheritance_cp20208(self):
        class FTPError(Exception): pass
        class FTPOSError(FTPError, OSError): pass

        self.assertEqual(FTPOSError, type(FTPOSError()))

    def test_conversions_cp19675(self):
        class MyFloatType(float):
            def __int__(self):
                return 42
            def __str__(self):
                return 'hello'

        MyFloat = MyFloatType()
        self.assertEqual(int(MyFloat), 42)
        self.assertEqual(str(MyFloat), 'hello')

        class MyFloatType(float): pass
        MyFloat = MyFloatType()
        self.assertEqual(int(MyFloat), 0)
        self.assertEqual(str(MyFloat), '0.0')

        class MyFloatType(float):
            def __new__(cls):
                return float.__new__(cls, 3.14)

        MyFloat = MyFloatType()
        self.assertEqual(MyFloat, 3.14)
        self.assertEqual(int(MyFloat), 3)

    @skipUnlessIronPython()
    def test_type_delegate_conversion(self):
        import clr
        from System import Func

        class x(object): pass
        ctor = Func[object](x)
        self.assertEqual(type(ctor()), x)

    def test_module_alias_cp19656(self):
        old_path = [x for x in sys.path]
        sys.path.append(self.test_dir)
        stuff_mod = os.path.join(self.test_dir, "stuff.py")
        check_mod = os.path.join(self.test_dir, "check.py")

        try:
            self.write_to_file(stuff_mod, "Keys = 3")
            self.write_to_file(check_mod, "def check(module):\n    return module.Keys")
            import stuff
            from check import check
            self.assertEqual(check(stuff), 3)
        finally:
            os.unlink(stuff_mod)
            os.unlink(check_mod)
            sys.path = old_path

    def test_cp24691(self):
        pwd = os.getcwd()
        self.assertEqual(os.path.abspath("bad:"),
                os.path.join(os.getcwd(), "bad:"))

    def test_cp24690(self):
        import errno
        self.assertEqual(errno.errorcode[2],
                "ENOENT")

    @unittest.skipIf(is_netcoreapp, 'https://github.com/IronLanguages/ironpython2/issues/349')
    @unittest.skipIf(is_posix, 'Test does not work on Mono')
    def test_cp24692(self):
        import errno, os, stat
        dir_name = "cp24692_testdir"
        try:
            os.mkdir(dir_name)
            os.chmod(dir_name, stat.S_IREAD)
            try:
                os.rmdir(dir_name)
            except WindowsError as e:
                self.assertEqual(e.errno, errno.EACCES)
            else:
                self.fail()
        finally:
            os.chmod(dir_name, stat.S_IWRITE)
            os.rmdir(dir_name)

    @skipUnlessIronPython()
    def test_cp22735(self):
        import System
        from System import Func

    def test_xxsubtype_bench(self):
        import xxsubtype
        self.assertEqual(type(xxsubtype.bench(xxsubtype, "bench")),
                float)

    def test_str_ljust_cp21483(self):
        self.assertEqual('abc'.ljust(-2147483648), 'abc')
        self.assertEqual('abc'.ljust(-2147483647), 'abc')
        if is_cli:
            self.assertRaises(OverflowError, #"long int too large to convert to int",
                    'abc'.ljust, -2147483649)
        else:
            self.assertEqual('abc'.ljust(-2147483649), 'abc')

    @unittest.skipIf(is_mono, "https://github.com/mono/mono/issues/17192")
    @skipUnlessIronPython()
    def test_help_dir_cp11833(self):
        import System
        self.assertTrue(dir(System).count('Action') == 1)
        from io import StringIO
        oldstdout, sys.stdout = sys.stdout, StringIO()
        try:
            help(System.Action)
        finally:
            sys.stdout = oldstdout
        self.assertTrue(dir(System).count('Action') == 1)

    def test_not___len___cp_24129(self):
        class C(object):
            def __len__(self):
                return 3

        c = C()
        print(bool(c))
        self.assertEqual(not c, False)

    @skipUnlessIronPython()
    def test_cp18912(self):
        import __future__
        feature = __future__.__dict__['with_statement']
        x = compile('x=1', 'ignored', 'exec', feature.compiler_flag)

    def test_cp19789(self):
        class A:
            a = 1

        class B(object):
            b = 2

        class C(A, B):
            pass

        self.assertTrue('a' in dir(A))
        self.assertTrue('b' in dir(B))
        self.assertTrue('a' in dir(C) and 'b' in dir(C))

    def test_cp24573(self):
        def f(a=None):
            pass

        self.assertRaisesRegex(TypeError, "f\(\) got multiple values for keyword argument 'a'",
                            lambda: f(1, a=3))

    @unittest.skipIf(is_netcoreapp, 'requires System.Drawing.Common dependency')
    @skipUnlessIronPython()
    def test_cp24802(self):
        import clr
        clr.AddReference('System.Drawing')
        import System
        p = System.Drawing.Pen(System.Drawing.Color.Blue)
        p.Width = System.Single(3.14)
        self.assertEqual(p.Width, System.Single(3.14))
        p.Width = 4.0
        self.assertEqual(p.Width, 4.0)


    def test_cp23822(self):
        from copy import deepcopy
        def F():
            a = 4
            class C:
                field=7
                def G(self):
                    print(a)
                    b = 4
                    return deepcopy(list(locals().keys()))

            c = C()
            return c.G()

        temp_list = F()
        temp_list.sort()
        self.assertEqual(temp_list, ['a', 'b', 'deepcopy', 'self'])

    def test_cp23823(self):
        from copy import deepcopy
        def f():
            a = 10
            def g1():
                print(a)
                return deepcopy(set(locals().keys()))
            def g2():
                return deepcopy(set(locals().keys()))
            return (g1(), g2())

        self.assertEqual(f(), ({'a', 'deepcopy'}, {'deepcopy'}))

    def cp22692_helper(self, source, flags):
        retVal = []
        err = err1 = err2 = None
        code = code1 = code2 = None
        try:
            code = compile(source, "dummy", "single", flags, 1)
        except SyntaxError as e:
            err = e
        try:
            code1 = compile(source + "\n", "dummy", "single", flags, 1)
        except SyntaxError as e:
            err1 = e
        try:
            code2 = compile(source + "\n\n", "dummy", "single", flags, 1)
        except SyntaxError as e:
            err2 = e
        if not code:
            retVal.append(type(err1))
            retVal.append(type(err2))
        return retVal

    def test_cp22692(self):
        self.assertEqual(self.cp22692_helper("if 1:", 0x200),
                [SyntaxError, IndentationError])
        self.assertEqual(self.cp22692_helper("if 1:", 0),
                [SyntaxError, IndentationError])
        self.assertEqual(self.cp22692_helper("if 1:\n  if 1:", 0x200),
                [IndentationError, IndentationError])
        self.assertEqual(self.cp22692_helper("if 1:\n  if 1:", 0),
                [IndentationError, IndentationError])

    @skipUnlessIronPython()
    def test_cp23545(self):
        import clr
        clr.AddReference("rowantest.defaultmemberscs")
        from Merlin.Testing.DefaultMemberSample import ClassWithDefaultField
        self.assertEqual(repr(ClassWithDefaultField.Field),
                "<field# Field on ClassWithDefaultField>")
        try:
            ClassWithDefaultField.Field = 20
        except ValueError as e:
            self.assertEqual(e.args[0],
                    "assignment to instance field w/o instance")
        self.assertEqual(ClassWithDefaultField().Field, 10)

    def test_cp20174(self):
        old_path = [x for x in sys.path]

        sys.path.append(self.test_dir)
        cp20174_path = os.path.join(self.test_dir, "cp20174")

        try:
            cp20174_init = os.path.join(cp20174_path, "__init__.py")
            self.write_to_file(cp20174_init, "from . import a")

            cp20174_a = os.path.join(cp20174_path,  "a.py")
            self.write_to_file(cp20174_a, """
from .property import x
class C:
    def _get_x(self): return x
    x = property(_get_x)
""")

            cp20174_property = os.path.join(cp20174_path, "property.py")
            self.write_to_file(cp20174_property, "x=1")

            import cp20174
            self.assertEqual(cp20174.property.x, 1)

        finally:
            self.clean_directory(cp20174_path, remove=True)
            sys.path = old_path

    @skipUnlessIronPython()
    def test_cp20370(self):
        import clr
        if is_netcoreapp:
            clr.AddReference("System.Drawing.Primitives")
        else:
            clr.AddReference("System.Drawing")
        from System.Drawing import Point
        p1 = Point(1, 2)
        p2 = Point(3, 4)

        l = [p1]
        self.assertTrue(id(l[-1]) != id(p2))
        l[-1] = p2
        self.assertEqual(id(l[-1]), id(p2))

    @unittest.skipIf(is_netcoreapp, 'throws PlatformNotSupportedException')
    @skipUnlessIronPython()
    def test_cp23878(self):
        import clr
        clr.AddReference("rowantest.delegatedefinitions")
        clr.AddReference("rowantest.typesamples")
        from Merlin.Testing import Delegate, Flag
        from time import sleep

        cwtm = Delegate.ClassWithTargetMethods()
        vi32d = Delegate.VoidInt32Delegate(cwtm.MVoidInt32)
        ar = vi32d.BeginInvoke(32, None, None)
        is_complete = False
        for i in range(100):
            sleep(1)
            if ar.IsCompleted:
                is_complete = True
                break
        self.assertTrue(is_complete)
        self.assertEqual(Flag.Value, 32)

    def test_cp23914(self):
        class C(object):
            def __init__(self,x,y,z):
                print(x,y,z)

        m = type.__call__

        with stdout_trapper() as trapper:
            try:
                l = m(C,1,2,3)
                l = m(C,z=3,y=2,x=1)
            except Exception as e:
                print(e.args[0])

        self.assertEqual(trapper.messages[0:2], ['1 2 3', '1 2 3'])

    @unittest.skipIf(is_cli, 'CPython specific test')
    def test_cp23992(self):
        def f():
            x = 3
            def g():
                return locals()
            l1 = locals()
            l2 = g()
            return (l1, l2)

        t1, t2 = f()
        self.assertEqual(set(t1.keys()), {'x', 'g'})
        self.assertEqual(t2, {})

    @unittest.skip("TODO: import cp20472 is not failing, figure out if the original issue is still relevant")
    def test_cp24169(self):
        import os, sys

        orig_syspath = [x for x in sys.path]
        try:
            sys.path.append(os.path.join(self.test_dir, "encoded_files"))
            import cp20472 #no encoding specified and has non-ascii characters
            self.fail("Line above should had thrown!")
        except SyntaxError as e:
            self.assertTrue(e.msg.startswith("Non-ASCII character '\\xcf' in file"))
            if is_cli:
                self.assertTrue(e.msg.endswith("on line 1, but no encoding declared; see http://www.python.org/peps/pep-0263.html for details"))
            else:
                self.assertTrue(e.msg.endswith("on line 1, but no encoding declared; see http://python.org/dev/peps/pep-0263/ for details"))
            self.assertTrue("%sencoded_files%scp20472.py" % (os.sep, os.sep) in e.msg, e.msg)
        finally:
            sys.path = orig_syspath

    def test_cp24484(self):
        class DictClass(dict):
            def __getattr__(self, name):
                return lambda x: x*20

        class K(object):
            def __init__(self, parent):
                self.parent = parent
            def __getattr__(self, name):
                return getattr(self.parent, name)

        dc = DictClass()
        k = K(dc)
        for i in range(200):
            temp = k.test(20)


    def test_cp23555(self):
        with stdout_trapper() as trapper:
            class Base(object):
                pass

            class Real(Base, float):
                def __new__(cls, *args, **kwargs):
                    print('real new')
                    result = Stub.__new__(cls, *args, **kwargs)
                    return result
                def __init__(self, *args, **kwargs):
                    print('real init')
                def __del__(self):
                    print('real del')

            class Stub(Real):
                def __new__(cls, *args, **kwargs):
                    print('stub new')
                    return float.__new__(Stub, args[0])
                def __init__(self, *args, **kwargs):
                    print('stub init')
                def __del__(self):
                    print("this should never happen; it's just here to ensure I get registered for GC")

            def ConstructReal(x):
                f = Real(x)
                f.__class__ = Real
                return f

            f = ConstructReal(1.0)
            del f

            # ensure __del__ is called
            import gc
            gc.collect()

        self.assertEqual(trapper.messages,
                ['real new', 'stub new', 'stub init', 'real del'])

    def test_cp24677(self):
        class SomeError(Exception):
            pass

        class SomeOtherError(SomeError, IOError):
            pass

        soe = SomeOtherError("some message")

        try:
            raise soe
        except Exception:
            pass

        try:
            raise soe
        except SomeError:
            pass

        try:
            raise soe
        except IOError:
            pass

        try:
            raise soe
        except SomeOtherError:
            pass


    @unittest.skipIf(is_netcoreapp, 'no clr.CompileModules')
    @skipUnlessIronPython()
    def test_gh1357(self):
        filename = os.path.join(self.temporary_dir, 'gh1357.py')
        dll = os.path.join(self.temporary_dir, "test.dll")
        with open(filename, 'w') as f:
            f.write('{(1,): None}')

        import clr
        try:
            clr.CompileModules(dll, filename)
        except:
            Fail('Failed to compile the specified file')
        finally:
            os.unlink(filename)
            os.unlink(dll)

    @skipUnlessIronPython()
    def test_gh1435(self):
        import clr
        code = """
    using System;

    /// <summary>
    /// Some description1.
    /// </summary>
    public class gh1435
    {
        /// <summary>
        /// Some description2.
        /// </summary>
        public static String strFoo= "foo";

        /// <summary>
        /// Some description3.
        /// </summary>
        public gh1435()
        {

        }

        /// <summary>
        /// Some description4.
        /// </summary>
        public int someMethod1()
        {
            return 8;
        }

        /// <summary>
        /// Some description5.
        /// </summary>
        public int someMethod2(string strSome)
        {
            return 8;
        }

        /// <summary>
        /// Some description6.
        /// </summary>
        public int someMethod3(out string strSome)
        {
            strSome = "Some string.";
            return 8;
        }

        /// <summary>
        /// Another description1
        /// </summary>
        public int someMethod4(out string strSome, ref int foo)
        {
            strSome = "Another string";
            foo = 10;
            return 5;
        }
    }
    """

        tmp = self.temporary_dir

        test_cs, test_dll, test_xml = os.path.join(tmp, 'gh1435.cs'), os.path.join(tmp, 'gh1435.dll'), os.path.join(tmp, 'gh1435.xml')

        self.write_to_file(test_cs, code)

        self.assertEqual(self.run_csc('/nologo /doc:{0} /target:library /out:{1} {2}'.format(test_xml, test_dll, test_cs)), 0)

        expected = """Help on method_descriptor:

    someMethod4(...)
        someMethod4(self: clsBar, foo: int) -> (int, str, int)

        Another description1""".replace('\r', '')

        clr.AddReferenceToFileAndPath(test_dll)
        import gh1435
        with stdout_trapper() as trapper:
            help(gh1435.someMethod4)
        self.assertTrue('\n'.join(trapper.messages), expected)

    def test_gh278(self):
        import _random
        r = _random.Random()
        s1 = r.getstate()
        s2 = r.getstate()
        self.assertIsNot(s1, s2)
        self.assertEqual(s1, s2)

    def test_gh1549(self):
        import hashlib
        m = hashlib.md5()
        m.digest()
        m.update(b'foo')
        m.digest()

    def test_gh1284(self):
        import math
        self.assertEqual(round(math.asinh(4.),12),round(math.log(math.sqrt(17.)+4.),12))
        self.assertEqual(round(math.asinh(.4),12),round(math.log(math.sqrt(1.16)+.4),12))
        self.assertEqual(round(math.asinh(-.5),12),round(math.log(math.sqrt(1.25)-.5),12))
        self.assertEqual(round(math.asinh(-6.),12),round(math.log(math.sqrt(37.)-6.),12))

    def test_gh1612(self):
        def stack_depth(frame):
            i = 0
            while frame is not None:
                i += 1
                frame = frame.f_back
            return i

        try:
            depth = stack_depth(sys._getframe())
        except AttributeError:
            return

        def gen():
            while True:
                yield stack_depth(sys._getframe())

        x = gen()
        self.assertEqual(next(x), depth + 1)

        def test():
            self.assertEqual(next(x), depth + 2)

        test()

    def test_gh1629(self):
        self.assertEqual('Bool is True', 'Bool is {}'.format(True))
        self.assertEqual('Bool is 1', 'Bool is {:^}'.format(True))
        self.assertEqual('Bool is     1     ', 'Bool is {:^10}'.format(True))

    def test_ipy3_gh230(self):
        """https://github.com/IronLanguages/ironpython3/pull/230"""
        import inspect
        class test(object): pass

        self.assertFalse(inspect.ismethoddescriptor(test.__weakref__))
        self.assertFalse(inspect.ismethoddescriptor(test.__dict__["__dict__"]))

    def test_ipy3_gh219(self):
        """https://github.com/IronLanguages/ironpython3/pull/219"""
        with self.assertRaises(SyntaxError):
            exec('["a"] = [1]')

        with self.assertRaises(SyntaxError):
            exec('[a + 1] = [1]')

    def test_ipy3_gh215(self):
        """https://github.com/IronLanguages/ironpython3/pull/215"""
        import io
        class Test(io.IOBase): pass
        dir(Test()) # check that this does not StackOverflow

    def test_ipy2_gh206(self):
        """https://github.com/IronLanguages/ironpython2/issues/206"""
        class x0: pass

        class x1(object): pass

        class aco(object):
          def __init__(self):
            self.cnt += 1
            super(aco, self).__init__()
            self.cnt += 1

        class two(x0, x1, aco):
          def __init__(self):
            self.cnt = 0
            super(two, self).__init__()
            self.cnt += 1

        if is_cli:
            try:
                self.assertEqual(two().cnt, 3)
            except SystemError:
                # https://github.com/IronLanguages/ironpython3/issues/451
                pass
            else:
                self.fail("delete the try/except when https://github.com/IronLanguages/ironpython3/issues/451 is fixed")
        else:
            self.assertEqual(two().cnt, 3)

    def test_ipy2_gh292(self):
        """https://github.com/IronLanguages/ironpython2/issues/292"""

        # binary
        self.assertRaises(SyntaxError, eval, "0b")
        self.assertRaises(SyntaxError, eval, "0B")
        self.assertEqual(0b10110, 0x16)
        self.assertEqual(0B1111, 15)

        # hex
        self.assertRaises(SyntaxError, eval, "0x")
        self.assertRaises(SyntaxError, eval, "0X")
        self.assertEqual(0x1000, 4096)
        self.assertEqual(0X123, 0o443)

        # octal
        self.assertRaises(SyntaxError, eval, "0o")
        self.assertRaises(SyntaxError, eval, "0O")
        self.assertEqual(0o777, 0x1ff)
        self.assertEqual(0O125, 85)

    def test_recursion_limit(self):
        """https://github.com/IronLanguages/ironpython2/issues/87"""

        limit = sys.getrecursionlimit()
        try:
            sys.setrecursionlimit(50)

            def getdepth():
                limit = [0]
                def f():
                    limit[0] += 1
                    f()
                try:
                    f()
                except RuntimeError:
                    pass
                return limit[0]

            x = getdepth()

            def f(n):
                if n > 0: return f(n-1)

            f(x)

            with self.assertRaises(RuntimeError):
                f(x+1)
                self.fail()

            f(x)
        finally:
            sys.setrecursionlimit(limit)

    def test_ipy2_gh273(self):
        """https://github.com/IronLanguages/ironpython2/issues/273"""

        import gc

        class A(object):
            cnt = 0
            def __init__(self):
                A.cnt += 1
            def __del__(self):
                A.cnt -= 1

        def test(x): pass

        test(test(test(A()))) # places an instance of A on the interpreter stack
        gc.collect()
        self.assertEqual(A.cnt, 0)

        test(test(0)) # doesn't override the instance of A held by the interpreter stack
        gc.collect()
        self.assertEqual(A.cnt, 0)

        test(test(test(0))) # overrides the instance of A held by the interpreter stack
        gc.collect()
        self.assertEqual(A.cnt, 0)

    def test_ipy2_gh357(self):
        """https://github.com/IronLanguages/ironpython2/issues/357"""

        import unicodedata

        if is_cli:
            self.assertEqual(unicodedata.name(u'\u4e2d'), '<CJK IDEOGRAPH, FIRST>..<CJK IDEOGRAPH, LAST>')
        else:
            self.assertEqual(unicodedata.name(u'\u4e2d'), 'CJK UNIFIED IDEOGRAPH-4E2D')

        self.assertRaises(ValueError, unicodedata.decimal, u'\u4e2d')
        self.assertEqual(unicodedata.decimal(u'\u4e2d', 0), 0)
        self.assertRaises(ValueError, unicodedata.digit, u'\u4e2d')
        self.assertEqual(unicodedata.digit(u'\u4e2d', 0), 0)
        self.assertRaises(ValueError, unicodedata.numeric, u'\u4e2d')
        self.assertEqual(unicodedata.numeric(u'\u4e2d', 0), 0)
        self.assertEqual(unicodedata.category(u'\u4e2d'), 'Lo')
        self.assertEqual(unicodedata.bidirectional(u'\u4e2d'), 'L')
        self.assertEqual(unicodedata.combining(u'\u4e2d'), 0)
        self.assertEqual(unicodedata.east_asian_width(u'\u4e2d'), 'W')
        self.assertEqual(unicodedata.mirrored(u'\u4e2d'), 0)
        self.assertEqual(unicodedata.decomposition(u'\u4e2d'), '')

    def test_ipy2_gh362(self):
        """https://github.com/IronLanguages/ironpython2/issues/362"""

        self.assertFalse(u"".startswith(u"\ufeff"))

        self.assertFalse(u"\xdf".startswith(u"ss"))
        self.assertFalse(u"ss".startswith(u"\xdf"))
        self.assertFalse(u"\xdf".endswith(u"ss"))
        self.assertFalse(u"ss".endswith(u"\xdf"))

    def test_ipy2_gh371(self):
        """https://github.com/IronLanguages/ironpython2/issues/371"""

        prefix = "c:\\f"
        for p in ('oo', 'o*', '?o'):
            self.assertEqual(os.path.abspath(prefix + p), os.path.abspath(prefix) + p)

    def test_ipy2_gh112(self):
        """https://github.com/IronLanguages/ironpython2/issues/112"""

        import io

        path = 'test.tmp'
        with open(path, 'wb') as f:
            f.write(u'hyv\xe4'.encode('UTF-8'))
        try:
            with io.open(path, encoding='ASCII', errors='ignore') as f:
                self.assertEqual(f.read(), "hyv")
        finally:
            os.remove(path)

    @skipUnlessIronPython()
    def test_ipy2_gh39(self):
        """https://github.com/IronLanguages/ironpython2/issues/39"""

        from System.Collections.Generic import List

        rng = range(10000)
        lst = List[object](rng)
        it = iter(lst)

        # Loop compilation occurs after 100 iterations, however it occurs in parallel.
        # Use a number >> 100 so that we actually hit the compiled code.
        for i in rng:
            self.assertEqual(i, next(it))

    @skipUnlessIronPython()
    def test_ipy2_gh25(self):
        """https://github.com/IronLanguages/ironpython2/issues/25"""

        # this is not available on Linux systems
        if is_posix:
            self.assertRaises(AttributeError, lambda: os.startfile('/bin/bash'))
        else:
            self.assertTrue(hasattr(os, 'startfile'))

    def test_ipy2_gh437(self):
        """https://github.com/IronLanguages/ironpython2/issues/437"""
        import weakref
        class SomeWeakReferenceableObject(object): pass

        o = SomeWeakReferenceableObject()
        x = [weakref.ref(o) for i in range(10)]
        self.assertEqual(weakref.getweakrefcount(o), 1)
        
    def test_gh370(self):
        """https://github.com/IronLanguages/ironpython2/issues/370"""
        from xml.etree import ElementTree as ET
        from io import StringIO
        x = ET.iterparse(StringIO('<root/>'))
        y = next(x)
        self.assertTrue(y[0] == 'end' and y[1].tag == 'root')

    @unittest.skipIf(is_cli, "TODO")
    def test_gh463(self):
        """https://github.com/IronLanguages/ironpython2/issues/463"""
        import plistlib
        x = b'<?xml version="1.0" encoding="UTF-8"?><!DOCTYPE plist PUBLIC "-//Apple Computer//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd"><plist version="1.0"><dict><key>A</key><string>B</string></dict></plist>'
        self.assertEqual(plistlib.readPlistFromBytes(x), {'A': 'B'})

    def test_gh483(self):
        """https://github.com/IronLanguages/ironpython2/issues/463"""
        import ast
        tree = ast.parse("print('hello world')")
        c = compile(tree, filename="<ast>", mode="exec")
        self.assertEqual(c.co_filename, "<ast>")

    def test_gh34(self):
        """https://github.com/IronLanguages/ironpython2/issues/34"""
        from collections import OrderedDict

        class Example(OrderedDict):
            def __eq__(self, other):
                return True

        e = Example()
        o = OrderedDict(a=1)
        
        self.assertTrue(e == o, 'e != o')
        self.assertTrue(o == e, 'o != e')
        
        class test(str):
            def __eq__(self,other):
                return True

        self.assertTrue("a" == test("b"), 'strings are not equal')

    def test_traceback_stack(self):
        import sys
        import traceback

        def C():
            raise Exception

        def B():
            C()

        def A():
            try:
                B()
            except:
                return sys.exc_info()[2]

        lineno = C.__code__.co_firstlineno
        tb = A()

        a = traceback.extract_tb(tb)
        b = traceback.extract_stack(tb.tb_frame, 1)
        self.assertEqual(a, [(__file__, 8+lineno, 'A', 'B()'), (__file__, 4+lineno, 'B', 'C()'), (__file__, 1+lineno, 'C', 'raise Exception')])
        self.assertEqual([x[2] for x in b], ['A']) # only check that we're in the proper function, the rest does not work properly

        tb = tb.tb_next
        a = traceback.extract_tb(tb)
        b = traceback.extract_stack(tb.tb_frame, 2)
        self.assertEqual(a, [(__file__, 4+lineno, 'B', 'C()'), (__file__, 1+lineno, 'C', 'raise Exception')])
        self.assertEqual([x[2] for x in b], ['A', 'B']) # only check that we're in the proper function, the rest does not work properly

        tb = tb.tb_next
        a = traceback.extract_tb(tb)
        b = traceback.extract_stack(tb.tb_frame, 3)
        self.assertEqual(a, [(__file__, 1+lineno, 'C', 'raise Exception')])
        self.assertEqual([x[2] for x in b], ['A', 'B', 'C']) # only check that we're in the proper function, the rest does not work properly

    def test_ipy3_gh412(self):
        """https://github.com/IronLanguages/ironpython3/issues/412"""
        def test(a, *args, b=None, **kwargs):
            return (a, args, b, kwargs)

        self.assertEqual(test(1, 2, 3, b=4, c=5, d=6), (1, (2, 3), 4, {'c': 5, 'd': 6}))

    def test_ipy3_gh458(self):
        """https://github.com/IronLanguages/ironpython3/issues/458"""
        class C(object): pass

        d = C.__dict__
        C.abc = 1
        self.assertTrue("abc" in d)

    def test_ipy3_gh463(self):
        """https://github.com/IronLanguages/ironpython3/issues/463"""
        x = iter(range(4))
        self.assertEqual(list(zip(x, x)), [(0, 1), (2, 3)])

        x = iter(range(5))
        self.assertEqual(list(zip(x, x)), [(0, 1), (2, 3)])
        with self.assertRaises(StopIteration):
            next(x)

    def test_ipy3_gh490(self):
        """https://github.com/IronLanguages/ironpython3/issues/490"""

        import types

        class C(object):
            def foo(self):
                pass

        self.assertTrue(isinstance(C.foo, types.FunctionType))
        self.assertTrue(isinstance(C().foo, types.MethodType))

        try:
            C.__new__ = lambda x: x
            C()
        except TypeError:
            self.fail("Throws in Python 2, but allowed in Python 3!")

    def test_ipy3_gh473(self):
        """https://github.com/IronLanguages/ironpython3/issues/473"""

        try:
            x = list(enumerate([1], 1 << 40))
            self.assertEqual(x[0], (1099511627776, 1))
        except OverflowError:
            self.fail("Should allow start index greater than int.MaxValue.")

    def test_ipy2_gh504(self):
        """https://github.com/IronLanguages/ironpython2/issues/504"""
        from xml.etree import ElementTree as ET
        text = ET.fromstring(b"<root>hyv\xc3\xa4</root>").text
        self.assertEqual(text, u"hyv\xe4")

    def test_ipy2_gh505(self):
        """https://github.com/IronLanguages/ironpython2/issues/505"""
        from xml.etree import ElementTree as ET
        text = ET.fromstring("<root>  \n<child>test</child>\n</root>").text
        self.assertEqual(text, "  \n")

    def test_ipy2_gh507(self):
        """https://github.com/IronLanguages/ironpython2/issues/507"""
        from xml.etree import ElementTree as ET
        root = ET.fromstring("""<root xmlns="default" xmlns:prefix="http://uri">
  <child>default namespace</child>
  <prefix:child>namespace "prefix"</prefix:child>
</root>""")
        self.assertEqual((root.tag, root.attrib), ("{default}root", {}))
        self.assertEqual([(child.tag, child.attrib) for child in root], [("{default}child", {}), ("{http://uri}child", {})])

    def test_ipy2_gh519(self):
        """https://github.com/IronLanguages/ironpython2/issues/519"""
        x = set(range(8))
        x.add(16)
        x.remove(0)
        self.assertTrue(16 in x)
        self.assertTrue(16 in set(x))

    def test_ipy2_gh522(self):
        """https://github.com/IronLanguages/ironpython2/issues/522"""
        import sqlite3
        conn = sqlite3.connect(":memory:")
        c = conn.cursor()
        c.execute("CREATE TABLE test (test BLOB);")
        c.execute("INSERT INTO test (test) VALUES (x'');")
        self.assertEqual(len(c.execute("SELECT * FROM test;").fetchone()[0]), 0)

    def test_ipy2_gh528(self):
        class x(int):
            def __hash__(self): return 42

        self.assertEqual(42, hash(x()))

    def test_main_gh1081(self):
        """https://github.com/IronLanguages/main/issues/1081"""
        import io
        import mmap

        test_file_name = os.path.join(self.temporary_dir, "test_main_gh1081.bin")

        with open(test_file_name, "wb") as f:
            f.write(bytearray(range(256)))

        try:
            with io.open(test_file_name, "rb") as f:
                mm = mmap.mmap(f.fileno(), 0, access=mmap.ACCESS_READ)
                try:
                    self.assertEqual(mm[:], bytearray(range(256)))
                finally:
                    mm.close()
        finally:
            os.remove(test_file_name)

    def test_ipy2_gh536(self):
        """https://github.com/IronLanguages/ironpython2/issues/536"""
        import ctypes
        class bar(ctypes.Union):
            _fields_ = [("t", ctypes.c_uint), ("b", ctypes.c_uint)]

        o = bar()
        o.t = 1
        self.assertEqual(1, o.b)
        self.assertEqual(o.t, o.b)

    def test_ipy2_gh584(self):
        """https://github.com/IronLanguages/ironpython2/issues/584"""
        class NoValue(object):
            def __getattr__(self2, attr):
                self.fail()

        noValue = NoValue()

        class test(object):
            defaultValue = noValue

        self.assertIs(test().defaultValue, noValue)

    def test_ipy2_gh546(self):
        """https://github.com/IronLanguages/ironpython2/issues/546"""
        from io import StringIO
        class Test(StringIO): pass
        Test().seek(0)

        from io import BytesIO
        class Test(BytesIO): pass
        Test().seek(0)

    def test_ipy3_gh580(self):
        """https://github.com/IronLanguages/ironpython3/issues/580"""
        bogus_file_descriptor = 12345
        with self.assertRaises(OSError):
            open(bogus_file_descriptor)

    def test_ipy3_gh546(self):
        """https://github.com/IronLanguages/ironpython3/issues/546"""
        import re
        _SECT_TMPL = r"""
            \[                                 # [
            (?P<header>[^]]+)                  # very permissive!
            \]                                 # ]
            """
        SECTCRE = re.compile(_SECT_TMPL, re.VERBOSE)
        string = "[some header]"
        match = SECTCRE.match(string)
        self.assertEqual(match.span(), (0, len(string)))

    def test_ipy2_gh655(self):
        """https://github.com/IronLanguages/ironpython2/issues/655"""
        import pyexpat
        buffer_size = pyexpat.ParserCreate().buffer_size
        self.assertEqual(buffer_size, 8192)

        import xml.etree.ElementTree as ET
        for count in range(buffer_size - 100, buffer_size + 100):
            txt = b'<Data>' + b'1'*count + b'</Data>'
            result = ET.tostring(ET.fromstring(txt))
            self.assertEqual(txt, result)

run_test(__name__)
