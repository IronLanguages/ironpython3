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

#------------------------------------------------------------------------------
#--Imports
from iptest.assert_util import *
from iptest.process_util import launch, run_csc
import sys

#------------------------------------------------------------------------------
#--Globals

#------------------------------------------------------------------------------
#--Test cases
@skip("win32", "silverlight")
def test_cp18345():
    import System
    import time
    class x(object):
        def f(self):
            global z
            z = 100
            
    System.AppDomain.CurrentDomain.DoCallBack(x().f)
    time.sleep(10)
    AreEqual(z, 100)

#------------------------------------------------------------------------------
@skip("silverlight")
def test_cp17420():
    #Create a temporary Python file
    test_file_name = path_combine(testpath.temporary_dir, "cp17420.py")
    test_log_name  = path_combine(testpath.temporary_dir, "cp17420.log")
    try:
        os.remove(test_log_name)
    except:
        pass
    
    test_file = '''
output = []
for i in xrange(0, 100):
    output.append(str(i) + "\\n")

file(r"%s", "w").writelines(output)''' % (test_log_name)
    
    write_to_file(test_file_name, test_file)

    #Execute the file from a separate process
    AreEqual(launch(sys.executable, test_file_name), 0)
    
    #Verify contents of file
    temp_file = open(test_log_name, "r")
    lines = temp_file.readlines()
    temp_file.close()
    AreEqual(len(lines), 100)

    os.unlink(test_file_name)
    os.unlink(test_log_name)
    
#------------------------------------------------------------------------------
def test_cp17274():
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

    AreEqual(KOld().__doc__, "KOld doc")
    AreEqual(KNew().__doc__, "KNew doc")
    k = KNewDerived()
    AreEqual(k.__doc__, "KNew doc")
    k.method()
    AreEqual(k.__doc__, "KNewDerived doc")
    AreEqual(KNewDerivedSpecial().__doc__, "KNewDerivedSpecial doc")

#------------------------------------------------------------------------------
@skip("win32", "silverlight")
def test_cp16831():
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

def test_cp_27434():
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
        Assert(regex.groups == groups, message)

@skip("win32")
def test_protected_ctor_inheritance_cp20021():
    load_iron_python_test()
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
        AssertError(TypeError, zero)
        AssertError(TypeError, zero.__new__)
        
        AssertError(TypeError, one, object())
        AssertError(TypeError, one.__new__, object())
        
        AssertError(TypeError, two, object())
        AssertError(TypeError, two.__new__, two, object())
        AssertError(TypeError, two, object(), object())
        AssertError(TypeError, two.__new__, two, object(), object())
        
        AssertError(TypeError, three)
        AssertError(TypeError, three.__new__, three)
        
        three(object())
        three.__new__(ProtectedCtorTest3, object())
        
        AssertError(TypeError, four, object())
        AssertError(TypeError, four.__new__, four, object())
        
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

def test_re_paren_in_char_list_cp20191():
    import re
    format_re = re.compile(r'(?P<order1>[<>|=]?)(?P<repeats> *[(]?[ ,0-9]*[)]? *)(?P<order2>[<>|=]?)(?P<dtype>[A-Za-z0-9.]*)')
    
    AreEqual(format_re.match('a3').groups(), ('', '', '', 'a3'))

@skip("silverlight")
def test_struct_uint_bad_value_cp20039():
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

    AssertErrorWithMessage(_struct.error, "integer out of range for 'L' format code",
                           _struct.Struct('L').pack, 4294967296)
    AssertErrorWithMessage(_struct.error, "integer out of range for 'L' format code",
                           _struct.Struct('L').pack, -1)
    AssertErrorWithMessage(Exception, "foo",
                           _struct.Struct('L').pack, x(0))
    AssertErrorWithMessage(Exception, "foo", _struct.Struct('L').pack, x(-1))

    AssertErrorWithMessage(_struct.error, "integer out of range for 'I' format code",
                           _struct.Struct('I').pack, 4294967296)
    AssertErrorWithMessage(_struct.error, "integer out of range for 'I' format code",
                           _struct.Struct('I').pack, -1)
    AssertErrorWithMessage(Exception, "foo",
                           _struct.Struct('I').pack, x(0))
    AssertErrorWithMessage(Exception, "foo", _struct.Struct('I').pack, x(-1))

    # __and__ was called in Python2.6 check that this is no longer True
    Assert(not andCalled)

def test_reraise_backtrace_cp20051():
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
        Assert(len(exc2_list) >= 2)

@skip("silverlight", "posix")
def test_winreg_error_cp17050():
    import winreg
    AreEqual(winreg.error, WindowsError)

@skip("win32", "silverlight")
def test_indexing_value_types_cp20370():
    import clr
    clr.AddReference("System.Drawing")
    from System.Drawing import Point
    
    p = Point(1,2)
    l = [None]
    l[0] = p
    AreEqual(id(l[0]), id(p))
    AreEqual(id(l[0]), id(p))
    
    x = {}
    x[p] = p
    AreEqual(id(list(x.keys())[0]), id(p))
    AreEqual(id(list(x.values())[0]), id(p))
    
    load_iron_python_test()
    
    from IronPythonTest import StructIndexable
    a = StructIndexable()
    a[0] = 1
    AreEqual(a[0], 1)

def test_enumerate_index_increment_cp20016():
    def f(item):
        return item[0] in [0, 1]
    
    AreEqual(list(filter(f, enumerate(['a', 'b']))), [(0, 'a'), (1, 'b')])
    AreEqual([j__ for j__ in enumerate([10.0, 27.0]) if j__[0] in [0, 1]],
             [(0, 10.0), (1, 27.0)])

@skip("silverlight")
def test_invalid_args_cp20616():
    test_cases = {
        lambda: ''.join() : "join() takes exactly one argument (0 given)",
        lambda: ''.join("", "") : "join() takes exactly one argument (2 given)",
        lambda: ''.join("", "", "") : "join() takes exactly one argument (3 given)",
        lambda: ''.replace("", "", "", "") : "replace() takes at most 3 arguments (4 given)",
    }
    if is_cli:
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
        AssertErrorWithMessage(TypeError, expected_err_msg, temp_lambda)

def test_cp19678():
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

    AreEqual(1 in o(), True)
    AreEqual(iterCalled, True)
    AreEqual(getItemCalled, False)


def test_exception_multiple_inheritance_cp20208():
    class FTPError(Exception): pass
    class FTPOSError(FTPError, OSError): pass
    
    AreEqual(FTPOSError, type(FTPOSError()))

def test_conversions_cp19675():
    class MyFloatType(float):
        def __int__(self):
            return 42    
        def __str__(self):
            return 'hello'
            
    MyFloat = MyFloatType()
    AreEqual(int(MyFloat), 42)
    AreEqual(str(MyFloat), 'hello')

    class MyFloatType(float): pass
    MyFloat = MyFloatType()
    AreEqual(int(MyFloat), 0)
    AreEqual(str(MyFloat), '0.0')

    class MyFloatType(float):
        def __new__(cls):
            return float.__new__(cls, 3.14)
    
    MyFloat = MyFloatType()
    AreEqual(MyFloat, 3.14)
    AreEqual(int(MyFloat), 3)
    

@skip("win32")
def test_type_delegate_conversion():
    import clr
    if is_net40:
      from System import Func    
    else:
      clr.AddReference('Microsoft.Scripting.Core')
      from Microsoft.Scripting.Utils import Func    
      
    class x(object): pass
    ctor = Func[object](x)
    AreEqual(type(ctor()), x)


def test_module_alias_cp19656():
    old_path = [x for x in sys.path]
    sys.path.append(testpath.public_testdir);
    stuff_mod = path_combine(testpath.public_testdir, "stuff.py")
    check_mod = path_combine(testpath.public_testdir, "check.py")
    
    try:
        write_to_file(stuff_mod, "Keys = 3")
        write_to_file(check_mod, "def check(module):\n    return module.Keys")
        import stuff
        from check import check
        AreEqual(check(stuff), 3)
    finally:
        import os
        os.unlink(stuff_mod)
        os.unlink(check_mod)
        sys.path = old_path

def test_cp24691():
    import os
    pwd = os.getcwd()
    AreEqual(os.path.abspath("bad:"),
             os.path.join(os.getcwd(), "bad:"))

def test_cp24690():
    import errno
    AreEqual(errno.errorcode[2],
             "ENOENT")

@skip("posix")
def test_cp24692():
    import errno, os, stat
    dir_name = "cp24692_testdir"
    try:
        os.mkdir(dir_name)
        os.chmod(dir_name, stat.S_IREAD)
        try:
            os.rmdir(dir_name)
        except WindowsError as e:
            pass
        AreEqual(e.errno, errno.EACCES)
    finally:
        os.chmod(dir_name, stat.S_IWRITE)
        os.rmdir(dir_name)

# TODO: this test needs to run against Dev10 builds as well
@skip("win32")
def test_cp22735():
    import System
    if System.Environment.Version.Major < 4:
        clr.AddReference("System.Core")
    from System import Func

#------------------------------------------------------------------------------
#--General coverage.  These need to be extended.
def test_xxsubtype_bench():
    import xxsubtype
    AreEqual(type(xxsubtype.bench(xxsubtype, "bench")),
             float)

def test_str_ljust_cp21483():
    AreEqual('abc'.ljust(-2147483648), 'abc')
    AreEqual('abc'.ljust(-2147483647), 'abc')
    AssertError(OverflowError, #"long int too large to convert to int",
                'abc'.ljust, -2147483649)


@skip("win32")
def test_help_dir_cp11833():
    import System
    Assert(dir(System).count('Action') == 1)
    from io import StringIO
    oldstdout, sys.stdout = sys.stdout, StringIO()
    try:
        help(System.Action)
    finally:
        sys.stdout = oldstdout
    Assert(dir(System).count('Action') == 1)


def test_not___len___cp_24129():
    class C(object):
        def __len__(self):
            return 3
    
    c = C()
    print(bool(c))
    AreEqual(not c, False)

@skip("win32")
def test_cp18912():
    import __future__
    feature = __future__.__dict__['with_statement']
    x = compile('x=1', 'ignored', 'exec', feature.compiler_flag)

def test_cp19789():
    class A:
        a = 1
    
    class B(object):
        b = 2
    
    class C(A, B):
        pass
    
    AreEqual(dir(A),
             ['__doc__', '__module__', 'a'])
    Assert('b' in dir(B))
    Assert('a' in dir(C) and 'b' in dir(C))

def test_cp24573():
    def f(a=None):
        pass
        
    AssertErrorWithMessage(TypeError, "f() got multiple values for keyword argument 'a'",
                           lambda: f(1, a=3))

@skip("win32")
def test_cp24802():
    import clr
    clr.AddReference('System.Drawing')
    import System
    p = System.Drawing.Pen(System.Drawing.Color.Blue)
    p.Width = System.Single(3.14)
    AreEqual(p.Width, System.Single(3.14))
    p.Width = 4.0
    AreEqual(p.Width, 4.0)

#------------------------------------------------------------------------------
# This is not a regression, but need to find the right place to move this test to

class MyException(IOError):
    def __str__(self):
        return "MyException is a user sub-type of IOError"

@skip("win32")
def test_clr_exception_has_non_trivial_exception_message():
    import System
    try:
        raise MyException
    except System.Exception as e:
        pass
    AreEqual(e.Message, "MyException")

def test_cp23822():
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
    AreEqual(temp_list, ['a', 'b', 'deepcopy', 'self'])
    
def test_cp23823():
    from copy import deepcopy
    def f():
        a = 10
        def g1():
            print(a)
            return deepcopy(list(locals().keys()))
        def g2():
            return deepcopy(list(locals().keys()))
        return (g1(), g2())
    
    AreEqual(f(), (['a', 'deepcopy'], ['deepcopy']))


def cp22692_helper(source, flags):
    retVal = []
    err = err1 = err2 = None
    code = code1 = code2 = None
    try:
        code = compile(source, "dummy", "single", flags, 1)
    except SyntaxError as err:
        pass
    try:
        code1 = compile(source + "\n", "dummy", "single", flags, 1)
    except SyntaxError as err1:
        pass
    try:
        code2 = compile(source + "\n\n", "dummy", "single", flags, 1)
    except SyntaxError as err2:
        pass
    if not code:
        retVal.append(type(err1))
        retVal.append(type(err2))
    return retVal 

def test_cp22692():
    AreEqual(cp22692_helper("if 1:", 0x200),
             [SyntaxError, IndentationError])
    AreEqual(cp22692_helper("if 1:", 0),
             [SyntaxError, IndentationError])
    AreEqual(cp22692_helper("if 1:\n  if 1:", 0x200),
             [IndentationError, IndentationError])
    AreEqual(cp22692_helper("if 1:\n  if 1:", 0),
             [IndentationError, IndentationError])

@skip("win32")
def test_cp23545():
    import clr
    clr.AddReference("rowantest.defaultmemberscs")
    from Merlin.Testing.DefaultMemberSample import ClassWithDefaultField
    AreEqual(repr(ClassWithDefaultField.Field),
             "<field# Field on ClassWithDefaultField>")
    try:
        ClassWithDefaultField.Field = 20
    except ValueError as e:
        AreEqual(e.message,
                 "assignment to instance field w/o instance")
    AreEqual(ClassWithDefaultField().Field, 10)

def test_cp20174():
    old_path = [x for x in sys.path]

    sys.path.append(testpath.public_testdir)
    cp20174_path = path_combine(testpath.public_testdir, "cp20174")
    
    try:
        cp20174_init = path_combine(cp20174_path, "__init__.py")
        write_to_file(cp20174_init, "import a")
        
        cp20174_a = path_combine(cp20174_path,  "a.py")
        write_to_file(cp20174_a, """
from property import x
class C:
    def _get_x(self): return x
    x = property(_get_x)
""")
        
        cp20174_property = path_combine(cp20174_path, "property.py")
        write_to_file(cp20174_property, "x=1")
        
        import cp20174
        AreEqual(cp20174.property.x, 1)
        
    finally:
        for x in os.listdir(cp20174_path):
            os.unlink(path_combine(cp20174_path, x))
        os.rmdir(cp20174_path)
        sys.path = old_path

@skip("win32")
def test_cp20370():
    import clr
    clr.AddReference("System.Drawing")
    from System.Drawing import Point
    p1 = Point(1, 2)
    p2 = Point(3, 4)
    
    l = [p1]
    Assert(id(l[-1]) != id(p2))
    l[-1] = p2
    AreEqual(id(l[-1]), id(p2))

@skip("win32", "silverlight")
def test_cp23878():
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
    Assert(is_complete)
    AreEqual(Flag.Value, 32)

def test_cp23914():
    class C(object):
        def __init__(self, x,y,z):
            print(x,y,z)
    
    m = type.__call__
    
    import sys
    from io import StringIO
    oldstdout, sys.stdout = sys.stdout, StringIO()
    try:
        l = m(C,1,2,3)
        l = m(C,z=3,y=2,x=1)
        sys.stdout.flush()
    finally:
        temp_stdout = sys.stdout
        sys.stdout = oldstdout
    
    AreEqual(temp_stdout.getvalue(), '1 2 3\n1 2 3\n')

@skip("cli", "silverlight")
def test_cp23992():
    def f():
        x = 3
        def g():
            return locals()
        l1 = locals()
        l2 = g()
        return (l1, l2)
    
    t1, t2 = f()
    AreEqual(list(t1.keys()), ['x', 'g'])
    AreEqual(t2, {})

def test_cp24169():
    import os, sys
    
    orig_syspath = [x for x in sys.path]
    try:
        sys.path.append(os.path.join(os.getcwd(), "encoded_files"))
        import cp20472 #no encoding specified and has non-ascii characters
        raise Exception("Line above should had thrown!")
    except SyntaxError as e:
        Assert(e.msg.startswith("Non-ASCII character '\\xcf' in file"))
        Assert(e.msg.endswith("on line 1, but no encoding declared; see http://www.python.org/peps/pep-0263.html for details"))
        Assert("%sencoded_files%scp20472.py" % (os.sep, os.sep) in e.msg, e.msg)
    finally:
        sys.path = orig_syspath

def test_cp24484():
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


def test_cp23555():
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

    AreEqual(trapper.messages[0:3],
             ['real new', 'stub new', 'stub init']) #'real del']) => CLR GC

def test_cp24677():
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


def test_gh1357():
    import os
    filename = os.path.join(testpath.temporary_dir, 'gh1357.py')
    dll = os.path.join(testpath.temporary_dir, "test.dll")
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

def test_gh1435():
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
    
    tmp = testpath.temporary_dir

    test_cs, test_dll, test_xml = path_combine(tmp, 'gh1435.cs'), path_combine(tmp, 'gh1435.dll'), path_combine(tmp, 'gh1435.xml')

    write_to_file(test_cs, code)

    AreEqual(run_csc("/nologo /doc:" + test_xml + " /target:library /out:" + test_dll + " " + test_cs), 0)
    
    from io import StringIO
    class _Capturing(list):
        def __enter__(self):
            self._stdout = sys.stdout
            sys.stdout = self._stringio = StringIO()
            return self

        def __exit__(self, *args):
            self.extend(self._stringio.getvalue())
            sys.stdout = self._stdout

    expected = """Help on method_descriptor:

someMethod4(...)
    someMethod4(self: clsBar, foo: int) -> (int, str, int)

    Another description1""".replace('\r', '')

    clr.AddReferenceToFileAndPath(test_dll)
    import gh1435
    with _Capturing() as output:
        help(gh1435.someMethod4)
    Assert('\n'.join(output), expected) 

def test_gh278():
    import _random  
    r = _random.Random()
    s1 = r.getstate()
    s2 = r.getstate()
    AreNotSame(s1, s2)
    AreEqual(s1, s2)

    r.jumpahead(100)
    s3 = r.getstate()
    AreNotSame(s3, s1)
    AreNotEqual(s3, s1)

def test_gh1549():
    import hashlib
    m = hashlib.md5()
    m.digest()
    m.update('foo')
    m.digest()
    
def test_gh1284():
    import math
    AreEqual(round(math.asinh(4.),12),round(math.log(math.sqrt(17.)+4.),12))
    AreEqual(round(math.asinh(.4),12),round(math.log(math.sqrt(1.16)+.4),12))
    AreEqual(round(math.asinh(-.5),12),round(math.log(math.sqrt(1.25)-.5),12))
    AreEqual(round(math.asinh(-6.),12),round(math.log(math.sqrt(37.)-6.),12))

def test_gh1612():
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
    AreEqual(next(x), depth + 1)

    def test():
        AreEqual(next(x), depth + 2)

    test()

#------------------------------------------------------------------------------
#--Main
run_test(__name__)
