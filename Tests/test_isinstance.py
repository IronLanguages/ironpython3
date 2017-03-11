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

import sys
import imp

@skip("silverlight")
def test_file_io():
    def verify_file(ff):
        cnt = 0
        for i in ff:
            Assert(i[0:5] == "Hello")
            cnt += 1
        ff.close()
        Assert(cnt == 10)
    
    f = file("testfile.tmp", "w")
    for i in range(10):
        f.write("Hello " + str(i) + "\n")
    f.close()
    
    f = file("testfile.tmp")
    verify_file(f)
    f = file("testfile.tmp", "r")
    verify_file(f)
    f = file("testfile.tmp", "r", -1)
    verify_file(f)
    f = open("testfile.tmp")
    verify_file(f)
    f = open("testfile.tmp", "r")
    verify_file(f)
    f = open("testfile.tmp", "r", -1)
    verify_file(f)
    f = open("testfile.tmp", "r", 0)
    verify_file(f)

    if is_cli:
        import System
        fs = System.IO.FileStream("testfile.tmp", System.IO.FileMode.Open, System.IO.FileAccess.Read)
        f = open(fs)
        verify_file(f)
        
        ms = System.IO.MemoryStream(30)
        f = open(ms)
        f.write("hello")
        f.flush()
        AreEqual(ms.Length, 5)
        AreEqual(ms.GetBuffer()[0], ord('h'))
        AreEqual(ms.GetBuffer()[4], ord('o'))
        ms.Close()

    import os
    os.remove("testfile.tmp")

# more tests for 'open'
@skip("silverlight")
def test_open():
    AssertError(TypeError, open, None) # arg must be string
    AssertError(TypeError, open, [])
    AssertError(TypeError, open, 1)

def test_compile():
    def max(a,b):
        if a>b: return a
        else: return b
    
    code = compile("max(10, 15)", "<string>", "eval")
    Assert(eval(code) == 15)
    
    code = compile("x = [1,2,3,4,5]\nx.reverse()\nAssert(x == [5,4,3,2,1])", "<string>", "exec")
    exec(code)
    Assert(x == [5,4,3,2,1])
    
    AssertError(ValueError, compile, "2+2", "<string>", "invalid")
    AssertError(SyntaxError, compile, "if 1 < 2: pass", "<string>", "eval")
    AssertError(SyntaxError, compile, "a=2", "<string>", "eval")
    
    AssertError(SyntaxError, eval, "a=2")

@skip("silverlight")
def test_redirect():
    # stdin, stdout redirect and input, raw_input tests
    
    old_stdin = sys.stdin
    old_stdout = sys.stdout
    sys.stdout = file("testfile.tmp", "w")
    print("Into the file")
    print("2+2")
    sys.stdout.close()
    sys.stdout = old_stdout
    
    sys.stdin = file("testfile.tmp", "r")
    s = input()
    Assert(s == "Into the file")
    s = eval(input())
    Assert(s == 4)
    sys.stdin.close()
    sys.stdin = old_stdin
    
    f = file("testfile.tmp", "r")
    g = file("testfile.tmp", "r")
    s = f.readline()
    t = g.readline()
    Assert(s == t)
    Assert(s == "Into the file\n")
    
    f.close()
    g.close()
    
    f = file("testfile.tmp", "w")
    f.writelines(["1\n", "2\n", "2\n", "3\n", "4\n", "5\n", "6\n", "7\n", "8\n", "9\n", "0\n"])
    f.close()
    f = file("testfile.tmp", "r")
    l = f.readlines()
    Assert(l == ["1\n", "2\n", "2\n", "3\n", "4\n", "5\n", "6\n", "7\n", "8\n", "9\n", "0\n"])
    f.close()

    import os
    os.remove("testfile.tmp")

def test_conversions():
    success=False
    try:
        nstr("Hi")
    except NameError:
        success=True
    AreEqual(success, True)
    
    success=False
    try:
        zip2([1,2,3],[4,5,6])
    except NameError:
        success=True
    AreEqual(success, True)
    
    AreEqual(str(), "")
    AreEqual(str(), "")
    
    AreEqual(oct(int(0)), "0L")
    AreEqual(hex(12297829382473034410), "0xaaaaaaaaaaaaaaaaL") #10581
    AreEqual(hex(-1), "-0x1L")
    AreEqual(int("-01L"), -1)
    AreEqual(int(" 1 "), 1)
    AreEqual(int(" -   1  "), -1)
    AreEqual(int("   -   1 "), -1)
    
    for f in [ int, int ]:
        AssertError(ValueError, f, 'p')
        AssertError(ValueError, f, 't')
        AssertError(ValueError, f, 'a')
        AssertError(ValueError, f, '3.2')
        AssertError(ValueError, f, '0x0R')
        AssertError(ValueError, f, '09', 8)
        AssertError(ValueError, f, '0A')
        AssertError(ValueError, f, '0x0G')
    
    AssertError(ValueError, int, '1l')
    
    
    
    AreEqual(int(1e100), 10000000000000000159028911097599180468360808563945281389781327557747838772170381060813469985856815104)
    AreEqual(int(-1e100), -10000000000000000159028911097599180468360808563945281389781327557747838772170381060813469985856815104)
    
    
    
    AreEqual(pow(2,3), 8)


def test_type_properties():
    #################
    # Type properties
    AreEqual(type(type), type.__class__)
    
    #################


def test_reload_sys():
    import sys
    
    (old_copyright, old_byteorder) = (sys.copyright, sys.byteorder)
    (sys.copyright, sys.byteorder) = ("foo", "foo")
    
    (old_argv, old_exc_type) = (sys.argv, sys.exc_info()[0])
    (sys.argv, sys.exc_info()[0]) = ("foo", "foo")
    
    reloaded_sys = imp.reload(sys)
    
    # Most attributes get reset
    AreEqual((old_copyright, old_byteorder), (reloaded_sys.copyright, reloaded_sys.byteorder))
    # Some attributes are not reset
    AreEqual((reloaded_sys.argv, reloaded_sys.exc_type), ("foo", "foo"))
    # Put back the original values
    (sys.copyright, sys.byteorder) = (old_copyright, old_byteorder)
    (sys.argv, sys.exc_info()[0]) = (old_argv, old_exc_type)
    

def test_hijacking_builtins():
    # BUG 433: CPython allows hijacking of __builtins__, but IronPython does not
    bug_433 = '''
    def foo(arg):
        return "Foo"
    
    # trying to override an attribute of __builtins__ causes a TypeError
    try:
        __builtins__.oct = foo
        Assert(False, "Cannot override an attribute of __builtins__")
    except TypeError:
        pass
    
    # assigning to __builtins__ passes, but doesn't actually affect function semantics
    import custombuiltins
    '''
    # /BUG

def test_custom_mapping():
    class MyMapping:
        def __getitem__(self, index):
                if index == 'a': return 2
                if index == 'b': return 5
                raise IndexError('bad index')
    
    AreEqual(eval('a+b', {}, MyMapping()), 7)

def test_eval_dicts():
    # eval referencing locals / globals
    global global_value
    value_a = 13
    value_b = 17
    global_value = 23
    def eval_using_locals():
        value_a = 3
        value_b = 7
        AreEqual(eval("value_a"), 3)
        AreEqual(eval("value_b"), 7)
        AreEqual(eval("global_value"), 23)
        AreEqual(eval("value_a < value_b"), True)
        AreEqual(eval("global_value < value_b"), False)
        return True
    
    Assert(eval_using_locals())
    
    if is_cli or is_silverlight:
        if System.BitConverter.IsLittleEndian == True:
            Assert(sys.byteorder == "little")
        else:
            Assert(sys.byteorder == "big")
    
    
    sortedDir = 3
    sortedDir = dir()
    sortedDir.sort()
    Assert(dir() == sortedDir)

def test_getattr():
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
        AreEqual(getattr(o, "field"), C1)
        AreEqual(getattr(o, "method")(), "method")
        AreEqual(getattr(o, "lambda")("a"), 1)
    
        AssertError(AssertionError, getattr, o, "myassert")
        AssertError(AttributeError, getattr, o, "anything")
        AssertError(AttributeError, getattr, o, "else")
    
        for attrname in ('field', 'method', '__init__', '__getattr__', 'lambda', '__doc__', '__module__'):
            AreEqual(hasattr(o, attrname), True)
    
        for attrname in ("myassert", "anything", "else"):
            AreEqual(hasattr(o,attrname), False)
    
    getattrhelper(C1)
    getattrhelper(C2)

## derived from python native type, and create instance of them without arg

def test_inheritance_ctor():
    global flag
    flag = 0
    def myinit(self):
        global flag
        flag = flag + 1
    
    cnt = 0
    for bt in (tuple, dict, list, str, set, frozenset, int, float, complex):
        nt = type("derived", (bt,), dict())
        inst = nt()
        AreEqual(type(inst), nt)
        
        nt2 = type("derived2", (nt,), dict())
        inst2 = nt2()
        AreEqual(type(inst2), nt2)
    
        nt.__init__ = myinit
        inst = nt()
        cnt += 1
        AreEqual(flag, cnt)
        AreEqual(type(inst), nt)
    
# sub classing built-ins works correctly..
@skip("silverlight")
def test_subclassing_builtins():
    class MyFile(file):
        myfield = 0
    
    f = MyFile('temporary.deleteme','w')
    AreEqual(f.myfield, 0)
    f.close()
    import os
    os.unlink('temporary.deleteme')
    
    
    class C(list):
        def __eq__(self, other):
            return 'Passed'
    
    AreEqual(C() == 1, 'Passed')

# extensible types should hash the same as non-extensibles, and unary operators
# should work too
def test_extensible_types_hashing():
    for x, y in ( (int, 2), (str, 'abc'), (float, 2.0), (int, 2), (complex, 2+0j) ):
        class foo(x): pass
        
        AreEqual(hash(foo(y)), hash(y))
        
        if x != str:
            AreEqual(-foo(y), -y)
            AreEqual(+foo(y), +y)
            
            if x != complex and x != float:
                AreEqual(~foo(y), ~y)
    
    


# can use kw-args w/ file
@skip("silverlight")
def test_kwargs_file():
    f = file(name='temporary.deleteme', mode='w')
    f.close()
    os.unlink('temporary.deleteme')



def test_kwargs_primitives():
    AreEqual(int(x=1), 1)
    AreEqual(float(x=2), 2.0)
    AreEqual(int(x=3), 3)
    AreEqual(complex(imag=4, real=3), 3 + 4j)
    AreEqual(str(object=5), '5')
    AreEqual(str(string='a', errors='strict'), 'a')
    AreEqual(tuple(sequence=list(range(3))), (0,1,2))
    AreEqual(list(sequence=(0,1)), [0,1])

#####################################################################

def test_issubclass():
    for (x, y) in [("2", int), (2, int), ("string", str), (None, int), (str, None), (int, 3), (int, (6, 7))]:
        AssertError(TypeError, lambda: issubclass(x, y))

@skip('win32')
def test_cli_subclasses():
    import clr
    
    Assert(issubclass(int, int))
    Assert(not issubclass(str, int))
    Assert(not issubclass(int, (str, str)))
    Assert(issubclass(int, (str, int)))

    Assert(issubclass(bytes, str))
    Assert(issubclass(str, str))
    Assert(issubclass(str, str))
    Assert(issubclass(str, str))
    class basestring_subclass(str):
        pass
    Assert(issubclass(basestring_subclass, str))

    Assert(str(None) == "None")
    Assert(issubclass(type(None),type(None)))
    Assert(str(type(None)) == "<type 'NoneType'>")
    Assert(str(1) == "1")
    Assert('__str__' in dir(None))
    
    def tryAssignToNoneAttr():
        None.__doc__ = "Nothing!"
    
    def tryAssignToNoneNotAttr():
        None.notanattribute = "";
    
    AssertError(AttributeError, tryAssignToNoneAttr)
    AssertError(AttributeError, tryAssignToNoneNotAttr)
    v = None.__doc__
    v = None.__new__
    v = None.__hash__
    AreEqual("<type 'NoneType'>", str(type(None)))
    
    import sys
    AreEqual(str(sys), "<module 'sys' (built-in)>")
    
    import time
    import toimport
    
    m = [type(sys), type(time), type(toimport)]
    for i in m:
        for j in m:
            Assert(issubclass(i,j))
    
    AssertError(TypeError, type, None, None, None) # arg 1 must be string
    AssertError(TypeError, type, "NewType", None, None) # arg 2 must be tuple
    AssertError(TypeError, type, "NewType", (), None) # arg 3 must be dict
    
    
    def splitTest():
        "string".split('')
    AssertError(ValueError, splitTest)

#####################################################################################
# IronPython does not allow extending System.Int64 and System.Boolean. So we have
# some directed tests for this.

@skip('win32')
def test_primitive_inheritance():
    import System
    
    def InheritFromType(t):
        class InheritedType(t): pass
        return InheritedType
    
    AssertError(TypeError, InheritFromType, System.Int64)
    AssertError(TypeError, InheritFromType, System.Boolean)
    
    # isinstance
    
    Assert(isinstance(System.Int64(), System.Int64) == True)
    Assert(isinstance(System.Boolean(), System.Boolean) == True)
    
    Assert(isinstance(1, System.Int64) == False)
    Assert(isinstance(1, System.Boolean) == False)
    
    class userClass(object): pass
    Assert(isinstance(userClass(), System.Int64) == False)
    Assert(isinstance(userClass(), System.Boolean) == False)
    
    # issubclass
    
    Assert(issubclass(System.Int64, System.Int64) == True)
    Assert(issubclass(System.Boolean, System.Boolean) == True)
    
    Assert(issubclass(type(1), System.Int64) == False)
    Assert(issubclass(type(1), System.Boolean) == False)
    
    Assert(issubclass(userClass, System.Int64) == False)
    Assert(issubclass(userClass, System.Boolean) == False)

#####################################################################################

@skip('win32')
def test_cli_types():
    import System
    arrayMapping = {'b': System.SByte, 'h': System.Int16, 'H': System.UInt16, 'i': System.Int32,
                    'I': System.UInt32, 'l': System.Int64, 'L': System.UInt64, 'f': System.Single, 'd': System.Double }
                    
    def tryConstructValues(validate, *args):
        for x in list(arrayMapping.keys()):
            # construct from DynamicType
            y = System.Array[arrayMapping[x]](*args)
            if not is_silverlight: #BUG DDB #76340
                AreEqual(y.GetType().GetElementType(), arrayMapping[x]().GetType())
            validate(y, *args)
    
            # construct from CLR type
            if not is_silverlight: #BUG DDB #76340
                y = System.Array[y.GetType().GetElementType()](*args)
                AreEqual(y.GetType().GetElementType(), arrayMapping[x]().GetType())
                validate(y, *args)
                
    
    def tryConstructSize(validate, *args):
        for x in list(arrayMapping.keys()):
            # construct from DynamicType
            y = System.Array.CreateInstance(arrayMapping[x], *args)
            
            if not is_silverlight: #BUG DDB #76340
                AreEqual(y.GetType().GetElementType(), arrayMapping[x]().GetType())
            validate(y, *args)
        
            # construct from CLR type
            if not is_silverlight: #BUG DDB #76340
                y = System.Array.CreateInstance(y.GetType().GetElementType(), *args)
                AreEqual(y.GetType().GetElementType(), arrayMapping[x]().GetType())
                validate(y, *args)
                
    
    def validateLen(res, *args):
        AreEqual(len(res), *args)
    
    def validateVals(res, *args):
        len(res) == len(args)
        for x in range(len(args[0])):
            try:
                lhs = int(res[x])
                rhs = int(args[0][x])
            except:
                lhs = float(res[x])
                rhs = float(args[0][x])
            AreEqual(lhs, rhs)
    
    def validateValsIter(res, *args):
        len(res) == len(args)
        for x in range(len(args)):
            print(int(res[x]), args[0][x])
            AreEqual(int(res[x]), int(args[0][x]))
        
        
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
            
            AreEqual(lhs, rhs)
            index += 1
        
        
            
    tryConstructSize(validateLen, 0)
    tryConstructSize(validateLen, 1)
    tryConstructSize(validateLen, 20)
    
    tryConstructValues(validateVals, (3,4,5,6))
    tryConstructValues(validateVals, [3,4,5,6])
    
    tryConstructValues(validateValsIter, MyList())


#############################################
# metaclass tests

# normal meta class construction & initialization
def test_metaclass_ctor_init():
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
    AreEqual(metaInit, True)
    AreEqual(instInit, True)
    
    AreEqual(MetaInstance.xyz, 'abc')
    AreEqual(MetaInstance.someFunction(), "called someFunction")
    
    
    class MetaInstance(object, metaclass=MetaType):
        def __init__(self, xyz):
            global instInit
            instInit = True
            self.val = xyz
            
    metaInit = False
    instInit = False
    
    a = MetaInstance('def')
    AreEqual(instInit, True)
    AreEqual(MetaInstance.xyz, 'abc')
    AreEqual(a.val, 'def')
    AreEqual(MetaInstance.someFunction(), "called someFunction")
    
    # initialization by calling the metaclass type.
    
    Foo = MetaType('foo', (), {})
    
    AreEqual(type(Foo), MetaType)
    
    instInit = False
    def newInit(self):
        global instInit
        instInit = True
        
    Foo = MetaType('foo', (), {'__init__':newInit})
    a = Foo()
    AreEqual(instInit, True)
    


# verify two instances of an old class compare differently

def test_oldclass_compare():
    class C: pass
    
    a = C()
    b = C()
    
    AreEqual(cmp(a,b) == 0, False)

#################################################################
# check that unhashable types cannot be hashed by Python
# However, they should be hashable using System.Object.GetHashCode

@skip('win32', 'silverlight') # TODO: _weakref support in SL
def test_unhashable_types():
    import System
    class OldUserClass:
        def foo(): pass
    import _weakref
    from _collections import deque
    
    AssertError(TypeError, hash, slice(None))
    hashcode = System.Object.GetHashCode(slice(None))
    
    # weakproxy
    AssertError(TypeError, hash, _weakref.proxy(OldUserClass()))
    hashcode = System.Object.GetHashCode(_weakref.proxy(OldUserClass()))
    
    # weakcallableproxy
    AssertError(TypeError, hash, _weakref.proxy(OldUserClass().foo))
    hashcode = System.Object.GetHashCode(_weakref.proxy(OldUserClass().foo))
    
    AssertError(TypeError, hash, deque())
    hashcode = System.Object.GetHashCode(deque())
    
    AssertError(TypeError, hash, dict())
    hashcode = System.Object.GetHashCode(dict())
    
    AssertError(TypeError, hash, list())
    hashcode = System.Object.GetHashCode(list())
    
    AssertError(TypeError, hash, set())
    hashcode = System.Object.GetHashCode(set())

#################################################################
# Check that attributes of built-in types cannot be deleted

@skip('win32')
def test_builtin_attributes():
    import System
    def AssignMethodOfBuiltin():
        def mylen(): pass
        l = list()
        l.len = mylen
    AssertError(AttributeError, AssignMethodOfBuiltin)
    
    def DeleteMethodOfBuiltin():
        l = list()
        del l.len
    AssertError(AttributeError, DeleteMethodOfBuiltin)
    
    def SetAttrOfBuiltin():
        l = list()
        l.attr = 1
    AssertError(AttributeError, SetAttrOfBuiltin)
    
    def SetDictElementOfBuiltin():
        l = list()
        l.__dict__["attr"] = 1
    AssertError(AttributeError, SetDictElementOfBuiltin)
    
    def SetAttrOfCLIType():
        d = System.DateTime()
        d.attr = 1
    AssertError(AttributeError, SetAttrOfCLIType)
    
    def SetDictElementOfCLIType():
        d = System.DateTime()
        d.__dict__["attr"] = 1
    AssertError(AttributeError, SetDictElementOfCLIType)
    
    AssertErrorWithMessage(TypeError, "vars() argument must have __dict__ attribute", vars, list())
    AssertErrorWithMessage(TypeError, "vars() argument must have __dict__ attribute", vars, System.DateTime())

#################################################################
# verify a class w/ explicit interface implementation gets
# it's interfaces shown

@skip('win32')
def test_explicit_interface_impl():
    import System
    AreEqual(System.IConvertible.ToDouble('32', None), 32.0)


@skip('win32')
def test_mutable_Valuetypes():
    load_iron_python_test()
    from IronPythonTest import MySize, BaseClass
    
    direct_vt = MySize(1, 2)
    embedded_vt = BaseClass()
    embedded_vt.Width = 3
    embedded_vt.Height = 4
    
    # Read access should still succeed.
    AreEqual(direct_vt.width, 1)
    AreEqual(embedded_vt.size.width, 3)
    AreEqual(embedded_vt.Size.width, 3)
    
    # But writes to value type fields should fail with ValueError.
    success = 0
    try:
        direct_vt.width = 5
    except ValueError:
        success = 1
    Assert(success == 0 and direct_vt.width == 1)
    
    success = 0
    try:
        embedded_vt.size.width = 5
    except ValueError:
        success = 1
    Assert(success == 0 and embedded_vt.size.width == 3)
    
    success = 0
    try:
        embedded_vt.Size.width = 5
    except ValueError:
        success = 1
    Assert(success == 0 and embedded_vt.Size.width == 3)
    
    import clr
    # ensure .GetType() and calling the helper w/ the type work
    AreEqual(clr.GetClrType(str), ''.GetType())
    # and ensure we're not just auto-converting back on both of them
    AreEqual(clr.GetClrType(str), str)
    AreEqual(clr.GetClrType(str) != str, False)
    
    # as well as GetPythonType
    import System
    AreEqual(clr.GetPythonType(System.Int32), int)
    AreEqual(clr.GetPythonType(clr.GetClrType(int)), int)

    # verify we can't create *Ops classes
    from IronPython.Runtime.Operations import DoubleOps
    AssertError(TypeError, DoubleOps)
    
    # setting mro to an invalid value should result in
    # bases still being correct
    class foo(object): pass
    
    class bar(foo): pass
    
    class baz(foo): pass
    
    def changeBazBase():
        baz.__bases__ = (foo, bar)  # illegal MRO
    
    AssertError(TypeError, changeBazBase)
    
    AreEqual(baz.__bases__, (foo, ))
    AreEqual(baz.__mro__, (baz, foo, object))
    
    d = {}
    d[None, 1] = 2
    AreEqual(d, {(None, 1): 2})

#######################################################
def test_int_minvalue():
    # Test for type of System.Int32.MinValue
    AreEqual(type(-2147483648), int)
    AreEqual(type(-(2147483648)), int)
    AreEqual(type(-2147483648), int)
    AreEqual(type(-0x80000000), int)
    
    AreEqual(type(int('-2147483648')), int)
    AreEqual(type(int('-80000000', 16)), int)
    AreEqual(type(int('-2147483649')), int)
    AreEqual(type(int('-80000001', 16)), int)
    
    
    if is_cli:
        import clr
        import System
        
        # verify our str.split doesn't replace CLR's String.Split
        chars = System.Array[str]([' '])
        res = 'a b  c'.Split(chars, System.StringSplitOptions.RemoveEmptyEntries)
        AreEqual(res[0], 'a')
        AreEqual(res[1], 'b')
        AreEqual(res[2], 'c')


#######################################################
# MRO Tests

def test_mro():
    # valid
    class C(object): pass
    
    class D(object): pass
    
    class E(D): pass
    
    class F(C, E): pass
    
    AreEqual(F.__mro__, (F,C,E,D,object))
    
    # valid
    class A(object): pass
    
    class B(object): pass
    
    class C(A,B): pass
    
    class D(A,B): pass
    
    class E(C,D): pass
    
    AreEqual(E.__mro__, (E,C,D,A,B,object))
    
    # invalid
    class A(object): pass
    
    class B(object): pass
    
    class C(A,B): pass
    
    class D(B,A): pass
    
    try:
        class E(C,D): pass
        AssertUnreachable()
    except TypeError:
        pass
        

#######################################################
# calling a type w/ kw-args

def test_type_call_kwargs():
    AreEqual(complex(real=2), (2+0j))


#######################################################
def test_bad_addition():
    try:
        2.0 + "2.0"
        AssertUnreachable()
    except TypeError: pass

#### (sometype).__class__ should be defined and correct

def test_class_property():
    class foo(object): pass
    
    AreEqual(foo.__class__, type)
    
    class foo(type): pass
    
    class bar(object, metaclass=foo):
        pass
        
    AreEqual(bar.__class__, foo)


#### metaclass order:

def test_metaclass_order():
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
        
    AreEqual(metaCalled, [BaseMeta])
    
    metaCalled = []
        
    class B(metaclass=DerivedMeta):
        pass
        
    AreEqual(metaCalled, [DerivedMeta])
    
    metaCalled = []
    class C(A,B): pass
    
    AreEqual(metaCalled, [BaseMeta, DerivedMeta])
    AreEqual(type(C).__name__, 'DerivedMeta')
    
    
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
    
    AssertError(RuntimeError, isinstance, I(), C())
    
    class C1(object): pass
    class C2(object): pass
    
    
    AreEqual(isinstance(C1(), C2), False)
    AreEqual(isinstance(C1(), C2), False)
    AreEqual(isinstance(C1(), (C2, (C2, C2))), False)
    AreEqual(isinstance(C1(), (C2, (C2, (C2, C1), C2))), True)
    
    class C1: pass
    class C2: pass
    
    
    AreEqual(isinstance(C1(), C2), False)
    AreEqual(isinstance(C1(), C2), False)
    AreEqual(isinstance(C1(), (C2, (C2, C2))), False)
    AreEqual(isinstance(C1(), (C2, (C2, (C2, C1), C2))), True)
    
    class MyInt(int): pass
    
    Assert(isinstance(MyInt(), int))

# __class__ accesses

def test_class_access():
    call_tracker = []
    class C(object):
        def getclass(self):
            call_tracker.append("C.getclass")
            return C
        __class__ = property(getclass)
    
    AreEqual(isinstance(C(), object), True)
    AreEqual(call_tracker, [])
    
    AreEqual(isinstance(C(), str), False)
    AreEqual(call_tracker, ["C.getclass"])
    
    call_tracker = []
    AreEqual(isinstance(C(), (str, (type, str, float))), False)
    AreEqual(call_tracker, ['C.getclass', 'C.getclass', 'C.getclass', 'C.getclass'])
    

# __bases__ access

def test_base_access():
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
    
    AreEqual(isinstance(D(), E()), False)
    AreEqual(call_tracker, ['E.getbases', 'D.getclass', 'C.getbases'])
    
    class I(object):
        def getclass(self):
            return None
        __class__ = property(getclass)
    
    class C(object):
        def getbases(self):
            return ()
        __bases__ = property(getbases)
    
    AssertError(TypeError, isinstance, I(), None)
    AssertError(TypeError, isinstance, 3, None)
    AssertError(TypeError, issubclass, int, None)


def test_tuple_new():
    # TypeError: tuple.__new__(str): str is not a subtype of tuple
    #!!!AssertError(TypeError, tuple.__new__, str)
    #!!!AssertError(TypeError, tuple.__new__, str, 'abc')
    pass

########################################################################
# stand-alone tests

# override pow, delete it, and it should be gone
import operator
pow = 7
AreEqual(pow, 7)
del pow
AreEqual(hasattr(pow, '__call__'), True)

try:
    del pow
    AreEqual(True,False)
except NameError:
    pass

@skip("silverlight", "Merlin bug #404247: this test doesn't work when the file is executed from non-Python host (thost)" )
def test_file():
    AreEqual(file_var_present, 1)

file_var_present = list(vars().keys()).count('__file__')

run_test(__name__)
