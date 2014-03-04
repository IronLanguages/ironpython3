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

from __future__ import print_function
from __future__ import unicode_literals

from iptest.assert_util import *
import os


def test_class_decorators():
    global called
    called = False
    def f(x): 
        global called
        called = True
        return x

    @f
    class x(object): pass
    
    AreEqual(called, True)
    AreEqual(type(x), type)

    called = False
    
    @f
    def abc(): pass
    
    AreEqual(called, True)
    
    def f(*args):
        def g(x):
            return x
        global called
        called = args
        return g
    
    
    @f('foo', 'bar')
    class x(object): pass
    AreEqual(called, ('foo', 'bar'))
    AreEqual(type(x), type)

    @f('foo', 'bar')
    def x(): pass
    AreEqual(called, ('foo', 'bar'))
    
    def f():
        exec """@f\nif True: pass\n"""
    AssertError(SyntaxError, f)

def test_binary_numbers():
    AreEqual(0b01, 1)
    AreEqual(0b10, 2)
    AreEqual(0b100000000000000000000000000000000, 4294967296)

    AreEqual(0b01L, 1)
    AreEqual(0b10L, 2)
    AreEqual(0b100000000000000000000000000000000L, 4294967296)

    AreEqual(type(0b01), int)
    AreEqual(type(0b10), int)
    AreEqual(type(0b01111111111111111111111111111111), int)
    AreEqual(type(0b10000000000000000000000000000000), long)
    AreEqual(type(-0b01111111111111111111111111111111), int)
    AreEqual(type(-0b10000000000000000000000000000000), int)
    AreEqual(type(-0b10000000000000000000000000000001), long)
    AreEqual(type(0b00000000000000000000000000000000000001), int)

    AreEqual(type(0b01L), long)
    AreEqual(type(0b10L), long)
    AreEqual(type(0b01l), long)
    AreEqual(type(0b10l), long)
    AreEqual(type(0b100000000000000000000000000000000L), long)
    AreEqual(0b01, 0B01)

def test_print_function():
    AssertError(TypeError, print, sep = 42)
    AssertError(TypeError, print, end = 42)
    AssertError(TypeError, print, abc = 42)
    AssertError(AttributeError, print, file = 42)
    
    import sys
    oldout = sys.stdout
    sys.stdout = file('abc.txt', 'w')
    try:
        print('foo')
        print('foo', end = 'abc')
        print()
        print('foo', 'bar', sep = 'abc')        
        
        sys.stdout.close()        
        f = file('abc.txt')
        AreEqual(f.readlines(), ['foo\n', 'fooabc\n', 'fooabcbar\n'])
    finally:
        sys.stdout = oldout

    class myfile(object):
        def __init__(self):
            self.text = ''
        def write(self, text):
            self.text += text
    
    sys.stdout = myfile()
    try:
        print('foo')
        print('foo', end = 'abc')
        print()
        print('foo', 'bar', sep = 'abc')
        print(None)

        AreEqual(sys.stdout.text, 'foo\n' + 'fooabc\n' + 'fooabcbar\n' + 'None\n')
    finally:
        sys.stdout = oldout
        
        


def test_user_mappings():
    # **args should support arbitrary mapping types
    class ms(str): pass
    
    class x(object):
        def __getitem__(self, key):
            #print('gi', key, type(key))
            return str('abc')
        def keys(self): 
            return [str('a'), str('b'), str('c'), ms('foo')] 
    
    def f(**args): return args
    f(**x())
    l = [(k,v, type(k), type(v)) for k,v in f(**x()).iteritems()]

    def mycmp(a, b):
        r = cmp(str(a[0]), str(b[0]))
        return r
    l.sort(cmp = mycmp)
    
    AreEqual(l, [(str('a'), str('abc'), str, str), (str('b'), str('abc'), str, str), (str('c'), str('abc'), str, str), (str('foo'), str('abc'), ms, str), ])
    
    class y(object):
        def __getitem__(self, key):
                return lambda x:x
        def keys(self): 
            return ['key']
    
    max([2,3,4], **y())
    
    class y(object):
        def __getitem__(self, key):
                return lambda x:x
        def keys(self): 
            return [ms('key')]
    
    
    max([2,3,4], **y())

def test_type_subclasscheck():
    global called
    called = []
    class metatype(type):
        def __subclasscheck__(self, sub):
            called.append((self, sub))
            return True
            
    class myclass(object): __metaclass__ = metatype
    
    AreEqual(issubclass(int, myclass), True)
    AreEqual(called, [(myclass, int)])
    called = []
    
    AreEqual(isinstance(myclass(), int), False)
    AreEqual(called, [])
    
def test_type_instancecheck():
    global called
    called = []
    class metatype(type):
        def __instancecheck__(self, inst):
            called.append((self, inst))
            return True
            
    class myclass(object): __metaclass__ = metatype

    AreEqual(isinstance(4, myclass), True)
    AreEqual(called, [(myclass, 4)])

def test_complex():
    strs = ["5", "2.61e-5", "3e+010", "1.40e09"]
    vals = [5, 2.61e-5, 3e10, 1.4e9]
    
    strings = ["j", "+j", "-j"] + strs + map(lambda x: x + "j", strs)
    values = [1j, 1j, -1j] + vals + map(lambda x: x * 1j, vals)
    neg_strings = []
    neg_values = []


    for s0,v0 in zip(strs, vals):
        for s1,v1 in zip(strs, vals):
            for sign,mult in [("+",1), ("-",-1)]:
                newstrs_pos = [s0+sign+s1+"j", s1+sign+s0+"j"]
                newvals_pos = [complex(v0,v1*mult), complex(v1,v0*mult)]
                strings += newstrs_pos
                strings += map(lambda x: "(" + x + ")", newstrs_pos)
                values += newvals_pos * 2

                newstrs_neg = [s0+"j"+sign+s1, s1+"j"+sign+s0]
                newvals_neg = [complex(v1*mult,v0), complex(v0*mult,v1)]
                if is_cpython: #http://ironpython.codeplex.com/workitem/28384
                    neg_strings += newstrs_neg
                    neg_strings += map(lambda x: "(" + x + ")", newstrs_neg)
                    neg_values += newvals_neg * 2
                else:
                    strings += newstrs_neg
                    strings += map(lambda x: "(" + x + ")", newstrs_neg)
                    values += newvals_neg * 2
    
    for s,v in zip(strings, values):
        AreEqual(complex(s), v)
        AreEqual(v, complex(v.__repr__()))

    for s,v in zip(neg_strings, neg_values):
        AssertErrorWithPartialMessage(ValueError, "complex() arg is a malformed string",
                                      complex, s)

def test_deque():
    if not is_stdlib():
        try:
            import collections
            Fail("collections should not be a builtin in 2.6")
        except ImportError, e:
            pass

    from _collections import deque

    # make sure __init__ clears existing contents
    x = deque([6,7,8,9])
    x.__init__(deque([1,2,3]))
    AreEqual(x, deque([1,2,3]))
    x.__init__()
    AreEqual(x, deque())

    # test functionality with maxlen
    x = deque(maxlen=5)
    for i in xrange(5):
        x.append(i)
        AreEqual(x, deque(range(i+1)))
    x.append(5)
    AreEqual(x, deque([1,2,3,4,5]))
    x.appendleft(100)
    AreEqual(x, deque([100,1,2,3,4]))
    x.extend(range(10))
    AreEqual(x, deque([5,6,7,8,9]))
    x.extendleft(range(10,20))
    AreEqual(x, deque([19,18,17,16,15]))
    x.remove(19)
    AreEqual(x, deque([18,17,16,15]))
    x.rotate()
    AreEqual(x, deque([15,18,17,16]))
    x.rotate(-8)
    AreEqual(x, deque([15,18,17,16]))
    x.pop()
    AreEqual(x, deque([15,18,17]))
    x.rotate(-1)
    AreEqual(x, deque([18,17,15]))
    x.popleft()
    AreEqual(x, deque([17,15]))
    x.extendleft(range(4))
    AreEqual(x, deque([3,2,1,0,17]))
    x.rotate(3)
    AreEqual(x, deque([1,0,17,3,2]))
    x.extend(range(3))
    AreEqual(x, deque([3,2,0,1,2]))
    y = x.__copy__()
    AreEqual(x, y)
    x.extend(range(4))
    y.extend(range(4))
    AreEqual(x, y)

def test_set_multiarg():
    from iptest.type_util import myset, myfrozenset
    
    s1 = [2, 4, 5]
    s2 = [4, 7, 9, 10]
    s3 = [2, 4, 5, 6]
    
    for A in (set, myset):
        for B in (set, frozenset, myset, myfrozenset):
            as1, as2, as3 = A(s1), A(s2), A(s3)
            bs1, bs2, bs3 = B(s1), B(s2), B(s3)
            
            AreEqual(as1.union(as2, as3), A([2, 4, 5, 6, 7, 9, 10]))
            AreEqual(as1.intersection(as2, as3), A([4]))
            AreEqual(as2.difference(as3, A([2, 7, 8])), A([9, 10]))
            
            AreEqual(bs1.union(as2, as3), A([2, 4, 5, 6, 7, 9, 10]))
            AreEqual(bs1.intersection(as2, as3), A([4]))
            AreEqual(bs2.difference(as3, A([2, 7, 8])), A([9, 10]))
            
            AreEqual(as1.union(bs2, as3), A([2, 4, 5, 6, 7, 9, 10]))
            AreEqual(as1.intersection(as2, bs3), A([4]))
            AreEqual(as2.difference(as3, B([2, 7, 8])), A([9, 10]))
            
            as1.update(as2, bs3)
            AreEqual(as1, B([2, 4, 5, 6, 7, 9, 10]))
            as2.difference_update(bs3, A([2, 7, 8]))
            AreEqual(as2, A([9, 10]))
            as3.intersection_update(bs2, bs1)
            AreEqual(as3, B([4]))

def test_attrgetter():
    import operator
    
    tests = ['abc', 3, ['d','e','f'], (1,4,9)]
    
    get0 = operator.attrgetter('__class__.__name__')
    get1 = operator.attrgetter('__class__..')
    get2 = operator.attrgetter('__class__..__name__')
    get3 = operator.attrgetter('__class__.__name__.__class__.__name__')
    
    for x in tests:
        AreEqual(x.__class__.__name__, get0(x))
        AssertError(AttributeError, get1, x)
        AssertError(AttributeError, get2, x)
        AreEqual('str', get3(x))

def test_im_aliases():
    def func(): pass
    class foo(object):
        def f(self): pass
    class bar(foo):
        def g(self): pass
    class yak(foo):
        def f(self): pass
    
    a = foo()
    b = foo()
    c = bar()
    d = bar()
    e = yak()
    f = yak()
    
    fs = [foo.f, a.f, b.f, c.f, d.f]
    gs = [bar.g, c.g, d.g]
    f2s = [yak.f, e.f, f.f]
    all = fs + gs + f2s
    
    for x in all:
        AreEqual(x.im_self, x.__self__)
        AreEqual(x.im_func, x.__func__)
    
    for r in [fs, f2s]:
        for s in [fs, f2s]:
            if r == s:
                for x in r:
                    if x.__func__ != None:
                        for y in r:
                            AreEqual(x.__func__, y.__func__)
            else:
                for x in r:
                    if x.__func__ != None and x.__self__ != None:
                        for y in s:
                            if x != y:
                                AreNotEqual(x.__self__, y.__self__)
                                AreNotEqual(x.__func__, y.__func__)
    
    
    AreEqual(a.f.__func__, c.f.__func__)
    AreEqual(b.f.__func__, d.f.__func__)
    AreNotEqual(a.f.__self__, b.f.__self__)
    AreNotEqual(b.f.__self__, d.f.__self__)

def test_tuple_index():
    t = (1,4,3,0,3)
    
    AssertError(TypeError, t.index)
    AssertError(TypeError, t.index, 3, 'a')
    AssertError(TypeError, t.index, 3, 2, 'a')
    AssertError(TypeError, t.index, 3, 2, 5, 1)
    
    AssertError(ValueError, t.index, 5)
    AssertError(ValueError, t.index, 'a')
    
    AreEqual(t.index(1), 0)
    AreEqual(t.index(3), 2)
    AreEqual(t.index(3, 2), 2)
    AreEqual(t.index(3, 3), 4)
    AssertError(ValueError, t.index, 3, 3, 4)
    AreEqual(t.index(3, 3, 5), 4)
    AreEqual(t.index(3, 3, 100), 4)
    
    AreEqual(t.index(3, -1), 4)
    AreEqual(t.index(3, -3, -1), 2)
    AreEqual(t.index(3, 2, -2), 2)
    AssertError(ValueError, t.index, 3, 3, -1)
    AssertError(ValueError, t.index, 3, -2, 0)

def test_tuple_count():
    t = ('1','2','3',1,3,3,2,3,1,'1','1',3,'3')
    
    AssertError(TypeError, t.count)
    AssertError(TypeError, t.count, 1, 2)
    
    AreEqual(t.count('1'), 3)
    AreEqual(t.count('2'), 1)
    AreEqual(t.count('3'), 2)
    AreEqual(t.count(1), 2)
    AreEqual(t.count(2), 1)
    AreEqual(t.count(3), 4)

def test_builtin_next():
    from _collections import deque
    values = [1,2,3,4]
    iterable_list = [list, tuple, set, deque]
    
    for iterable in iterable_list:
        i = iter(iterable(values))
        AreEqual(next(i), 1)
        AreEqual(next(i), 2)
        AreEqual(next(i), 3)
        AreEqual(next(i), 4)
        AssertError(StopIteration, next, i)
        
        i = iter(iterable(values))
        AreEqual(next(i, False), 1)
        AreEqual(next(i, False), 2)
        AreEqual(next(i, False), 3)
        AreEqual(next(i, False), 4)
        AreEqual(next(i, False), False)
        AreEqual(next(i, False), False)
    
    i = iter('abcdE')
    AreEqual(next(i), 'a')
    AreEqual(next(i), 'b')
    AreEqual(next(i), 'c')
    AreEqual(next(i), 'd')
    AreEqual(next(i), 'E')
    AssertError(StopIteration, next, i)
    
    i = iter('edcbA')
    AreEqual(next(i, False), 'e')
    AreEqual(next(i, False), 'd')
    AreEqual(next(i, False), 'c')
    AreEqual(next(i, False), 'b')
    AreEqual(next(i, False), 'A')
    AreEqual(next(i, False), False)
    AreEqual(next(i, False), False)

def test_sys_flags():
    import sys
    AssertContains(dir(sys), 'flags')
    
    # Assertion helpers
    def IsInt(x):
        Assert(type(x) == int)
    def IsFlagInt(x):
        Assert(x in [0,1,2])
    
    # Check repr
    AreEqual(repr(type(sys.flags)), "<type 'sys.flags'>")
    Assert(repr(sys.flags).startswith("sys.flags(debug="))
    Assert(repr(sys.flags).endswith(")"))
    
    # Check attributes
    attrs = set(dir(sys.flags))
    structseq_attrs = set(["n_fields", "n_sequence_fields", "n_unnamed_fields"])
    flag_attrs = set(['bytes_warning', 'debug', 'division_new', 'division_warning', 'dont_write_bytecode', 'ignore_environment', 'inspect', 'interactive', 'no_site', 'no_user_site', 'optimize', 'py3k_warning', 'tabcheck', 'unicode', 'verbose'])
    expected_attrs = structseq_attrs.union(flag_attrs, dir(object), dir(tuple))
    expected_attrs -= set(["index", "count", "__iter__", "__getnewargs__", "__radd__"]) # tuple fields that don't appear in sys.flags
    
    AreEqual(attrs, set(dir(type(sys.flags))))
    attrs -= set(['__iter__']) # ignore '__iter__', which appears in IPy but not CPy
    AreEqual(expected_attrs - attrs, set()) # check for missing attrs
    AreEqual(attrs - expected_attrs, set()) # check for too many attrs
    
    for attr in structseq_attrs.union(flag_attrs):
        IsInt(getattr(sys.flags, attr))
    for attr in flag_attrs:
        IsFlagInt(getattr(sys.flags, attr))
    AreEqual(sys.flags.n_sequence_fields, len(flag_attrs))
    AreEqual(sys.flags.n_fields, sys.flags.n_sequence_fields)
    AreEqual(sys.flags.n_unnamed_fields, 0)
    
    # Test tuple-like functionality
    
    # __add__
    x = sys.flags + ()
    y = sys.flags + (7,)
    z = sys.flags + (6,5,4,3,2)
    
    # __len__
    AreEqual(len(sys.flags), len(flag_attrs))
    AreEqual(len(sys.flags), len(x))
    AreEqual(len(sys.flags), len(y) - 1)
    AreEqual(len(sys.flags), len(z) - 5)
    
    # __eq__
    AreEqual(sys.flags, x)
    Assert(sys.flags == x)
    Assert(x == sys.flags)
    Assert(sys.flags == sys.flags)
    # __ne__
    AssertFalse(sys.flags != sys.flags)
    Assert(sys.flags != z)
    Assert(z != sys.flags)
    # __ge__
    Assert(sys.flags >= sys.flags)
    Assert(sys.flags >= x)
    Assert(x >= sys.flags)
    AssertFalse(sys.flags >= y)
    Assert(z >= sys.flags)
    # __le__
    Assert(sys.flags <= sys.flags)
    Assert(sys.flags <= x)
    Assert(x <= sys.flags)
    Assert(sys.flags <= y)
    AssertFalse(y <= sys.flags)
    # __gt__
    AssertFalse(sys.flags > sys.flags)
    AssertFalse(sys.flags > x)
    AssertFalse(x > sys.flags)
    AssertFalse(sys.flags > y)
    Assert(z > sys.flags)
    # __lt__
    AssertFalse(sys.flags < sys.flags)
    AssertFalse(sys.flags < x)
    AssertFalse(x < sys.flags)
    Assert(sys.flags < y)
    AssertFalse(y < sys.flags)
    
    # __mul__
    AreEqual(sys.flags * 2, x * 2)
    AreEqual(sys.flags * 5, x * 5)
    # __rmul__
    AreEqual(5 * sys.flags, x * 5)
    
    # __contains__
    AreEqual(0 in sys.flags, 0 in x)
    AreEqual(1 in sys.flags, 1 in x)
    AreEqual(2 in sys.flags, 2 in x)
    AssertFalse(3 in sys.flags)
    
    # __getitem__
    for i in range(len(sys.flags)):
        AreEqual(sys.flags[i], x[i])
    # __getslice__
    AreEqual(sys.flags[:], x[:])
    AreEqual(sys.flags[2:], x[2:])
    AreEqual(sys.flags[3:6], x[3:6])
    AreEqual(sys.flags[1:-1], x[1:-1])
    AreEqual(sys.flags[-7:11], x[-7:11])
    AreEqual(sys.flags[-10:-5], x[-10:-5])
    
    # other sequence ops
    AreEqual(set(sys.flags), set(x))
    AreEqual(list(sys.flags), list(x))
    count = 0
    for f in sys.flags:
        count += 1
        IsFlagInt(f)
    AreEqual(count, len(sys.flags))
    
    # sanity check
    if (sys.dont_write_bytecode):
        AreEqual(sys.flags.dont_write_bytecode, 1)
    else:
        AreEqual(sys.flags.dont_write_bytecode, 0)
    if (sys.py3kwarning):
        AreEqual(sys.flags.py3k_warning, 1)
    else:
        AreEqual(sys.flags.py3k_warning, 0)

def test_functools_reduce():
    import _functools
    
    words = ["I", "am", "the", "walrus"]
    combine = lambda s,t: s + " " + t
    
    Assert(hasattr(_functools, "reduce"))
    
    AreEqual(_functools.reduce(combine, words), "I am the walrus")
    AreEqual(_functools.reduce(combine, words), reduce(combine, words))

def test_log():
    import math
    
    zeros = [-1, -1.0, -1L, 0, 0.0, 0L]
    nonzeros = [2, 2.0, 2L]
    ones = [1, 1.0, 1L]
    
    AreNotEqual(type(zeros[0]), type(zeros[2]))
    
    for z0 in zeros:
        AssertError(ValueError, math.log, z0)
        AssertError(ValueError, math.log10, z0)
        for z in zeros:
            AssertError(ValueError, math.log, z0, z)
        for n in nonzeros + ones:
            AssertError(ValueError, math.log, z0, n)
            AssertError(ValueError, math.log, n, z0)
    
    for one in ones:
        for n in nonzeros:
            AssertError(ZeroDivisionError, math.log, n, one)

def test_trunc():
    import sys, math
    
    test_values = [-1, 0, 1, -1L, 0L, 1L, -1.0, 0.0, 1.0, sys.maxint + 0.5,
                   -sys.maxint - 0.5, 9876543210, -9876543210, -1e100, 1e100]
    
    for value in test_values:
        AreEqual(long(value), math.trunc(value))
        if type(value) == float:
            AreEqual(type(math.trunc(value)) == int, -sys.maxint - 1 <= value <= sys.maxint)
        else:
            AreEqual(type(value), type(math.trunc(value)))

# A small extension of CPython's test_struct.py, which does not make sure that empty
# dictionaries are interpreted as false
def test_struct_bool():
    import _struct
    for prefix in tuple("<>!=")+('',):
        format = str(prefix + '?')
        packed = _struct.pack(format, {})
        unpacked = _struct.unpack(format, packed)

        AreEqual(len(unpacked), 1)
        AssertFalse(unpacked[0])


###############################################################################
##PEP 3110
orig_ValueError = ValueError

#--Make sure the undesired CPython 2.6 behavior still works
try:
    raise TypeError("abc")
except TypeError, ValueError:
    AreEqual(ValueError.message, "abc")
    Assert(isinstance(ValueError, TypeError))
ValueError = orig_ValueError
    
try:
    raise TypeError("abc")
except TypeError, ValueError:
    AreEqual(ValueError.message, "abc")
finally:
    ValueError = orig_ValueError
AreEqual(ValueError, orig_ValueError)

#negative
try:
    try:
        raise IOError("abc")
    except TypeError, ValueError:
        Fail("IOError is not the same as TypeError")
except IOError:
    pass
AreEqual(ValueError, orig_ValueError)

try:
    try:
        raise IOError("abc")
    except TypeError, ValueError:
        Fail("IOError is not the same as TypeError")
    finally:
        pass
except IOError:
    pass
AreEqual(ValueError, orig_ValueError)

#--Make sure the desired CPython 2.5 behavior still works
try:
    raise TypeError("xyz")
except (TypeError, ValueError), e:
    AreEqual(e.message, "xyz")
e = None

try:
    raise TypeError("xyz")
except (TypeError, ValueError), e:
    AreEqual(e.message, "xyz")
finally:
    pass
e = None

try:
    raise TypeError("xyz")
except (TypeError, ValueError):
    pass

try:
    raise TypeError("xyz")
except (TypeError, ValueError):
    pass
finally:
    pass

#negative
try:
    try:
        raise IOError("xyz")
    except (TypeError, ValueError), e:
        Fail("IOError is not the same as TypeError or ValueError")
except IOError:
    pass
AreEqual(e, None)

try:
    try:
        raise IOError("xyz")
    except (TypeError, ValueError), e:
        Fail("IOError is not the same as TypeError or ValueError")
    finally:
        pass
except IOError:
    pass
AreEqual(e, None)

#--Now test 'except ... as ...:'
try:
    raise TypeError("abc")
except TypeError as ValueError:
    AreEqual(ValueError.message, "abc")
    Assert(isinstance(ValueError, TypeError))
ValueError = orig_ValueError

try:
    raise TypeError("abc")
except TypeError as e:
    AreEqual(e.message, "abc")
    Assert(isinstance(e, TypeError))
e = None

try:
    raise TypeError("abc")
except TypeError as ValueError:
    AreEqual(ValueError.message, "abc")
    Assert(isinstance(ValueError, TypeError))
finally:
    ValueError = orig_ValueError
AreEqual(ValueError, orig_ValueError)

try:
    raise TypeError("abc")
except TypeError as e:
    AreEqual(e.message, "abc")
    Assert(isinstance(e, TypeError))
finally:
    e = None
AreEqual(e, None)

try:
    raise IOError("abc")
except TypeError, e:
    Fail("IOError is not the same as TypeError")
except TypeError as e:
    Fail("IOError is not the same as TypeError")
except IOError, e:
    AreEqual(e.message, "abc")
e = None

try:
    raise IOError("abc")
except TypeError, e:
    Fail("IOError is not the same as TypeError")
except TypeError as e:
    Fail("IOError is not the same as TypeError")
except IOError as e:
    AreEqual(e.message, "abc")
e = None

try:
    raise IOError("abc")
except TypeError, e:
    Fail("IOError is not the same as TypeError")
except TypeError as e:
    Fail("IOError is not the same as TypeError")
except Exception as e:
    AreEqual(e.message, "abc")
e = None
    
try:
    raise IOError("abc")
except TypeError, e:
    Fail("IOError is not the same as TypeError")
except TypeError as e:
    Fail("IOError is not the same as TypeError")
except:
    pass

#neg    
try:
    try:
        raise IOError("abc")
    except TypeError, e:
        Fail("IOError is not the same as TypeError")
    except TypeError as e:
        Fail("IOError is not the same as TypeError")
except IOError as e:
    AreEqual(e.message, "abc")
e = None

try:
    try:
        raise IOError("abc")
    except TypeError as ValueError:
        Fail("IOError is not the same as TypeError")
except IOError, e:
    AreEqual(e.message, "abc")
e = None

try:
    try:
        raise IOError("abc")
    except TypeError as e:
        Fail("IOError is not the same as TypeError")
except IOError as e:
    AreEqual(e.message, "abc")
e = None

try:
    try:
        raise IOError("abc")
    except TypeError as ValueError:
        Fail("IOError is not the same as TypeError")
    finally:
        pass
except Exception, e:
    AreEqual(e.message, "abc")
e = None

try:
    try:
        raise IOError("abc")
    except TypeError as e:
        Fail("IOError is not the same as TypeError")
    finally:
        pass
except IOError:
    pass

##PEP 3112#####################################################################
def test_pep3112():
    AreEqual(len("abc"), 3)
    
    #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=19521
    AreEqual(len("\u751f"), 1)

##PEP##########################################################################
def test_pep19546():
    '''
    Just a small sanity test.  CPython's test_int.py covers this PEP quite 
    well.
    '''
    #Octal
    AreEqual(0O21, 17)
    AreEqual(021, 0o21)
    AreEqual(0O21, 0o21)
    AreEqual(0o0, 0)
    AreEqual(-0o1, -1)
    AreEqual(0o17777777776, 2147483646)
    AreEqual(0o17777777777, 2147483647)
    AreEqual(0o20000000000, 2147483648)
    AreEqual(-0o17777777777, -2147483647)
    AreEqual(-0o20000000000, -2147483648)
    AreEqual(-0o20000000001, -2147483649)
    
    #Binary
    AreEqual(0B11, 3)
    AreEqual(0B11, 0b11)
    AreEqual(0b0, 0)
    AreEqual(-0b1, -1)
    AreEqual(0b1111111111111111111111111111110, 2147483646)
    AreEqual(0b1111111111111111111111111111111, 2147483647)
    AreEqual(0b10000000000000000000000000000000, 2147483648)
    AreEqual(-0b1111111111111111111111111111111, -2147483647)
    AreEqual(-0b10000000000000000000000000000000, -2147483648)
    AreEqual(-0b10000000000000000000000000000001, -2147483649)
    
    #bin and future_builtins.oct
    from future_builtins import oct as fb_oct
    test_cases = [  (0B11, "0b11", "0o3"),
                    (2147483648, "0b10000000000000000000000000000000", "0o20000000000"),
                    (-2147483649L, "-0b10000000000000000000000000000001", "-0o20000000001"),
                    (-1L,          "-0b1", "-0o1"),
                    (-0b10000000000000000000000000000000, "-0b10000000000000000000000000000000", "-0o20000000000"),
                    (-0o17777777777, "-0b1111111111111111111111111111111", "-0o17777777777"),
                    (0o17777777777, "0b1111111111111111111111111111111", "0o17777777777"),
                    ]
    for val, bin_exp, oct_exp in test_cases:
        AreEqual(bin(val), bin_exp)
        AreEqual(fb_oct(val), oct_exp)
        
@skip("win32")
def test_pep3141():
    '''
    This is already well covered by CPython's test_abstract_numbers.py. Just 
    check a few .NET interop cases as well to see what happens.
    '''
    import System
    from numbers import Complex, Real, Rational, Integral, Number
    
    #--Complex
    for x in [
                System.Double(9), System.Int32(4), System.Boolean(1), 
                ]:
        Assert(isinstance(x, Complex))
    
    for x in [
                #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=23147
                System.Char.MaxValue, 
                System.Single(8), System.Decimal(10),
                System.SByte(0), System.Byte(1),
                System.Int16(2), System.UInt16(3), System.UInt32(5), System.Int64(6), System.UInt64(7),
                ]:
        Assert(not isinstance(x, Complex), x)
        
    #--Real
    for x in [
                System.Double(9), System.Int32(4), System.Boolean(1), 
                ]:
        Assert(isinstance(x, Real))
    
    for x in [
                #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=23147
                System.Char.MaxValue, 
                System.Single(8), System.Decimal(10),
                System.SByte(0), System.Byte(1),
                System.Int16(2), System.UInt16(3), System.UInt32(5), System.Int64(6), System.UInt64(7),
                ]:
        Assert(not isinstance(x, Real))
    
    
    #--Rational
    for x in [
                System.Int32(4), System.Boolean(1), 
                ]:
        Assert(isinstance(x, Rational))
    
    for x in [
                System.Double(9), 
                #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=23147
                System.Char.MaxValue, 
                System.Single(8), System.Decimal(10),
                System.SByte(0), System.Byte(1),
                System.Int16(2), System.UInt16(3), System.UInt32(5), System.Int64(6), System.UInt64(7),
                ]:
        Assert(not isinstance(x, Rational))
    
    #--Integral
    for x in [
                System.Int32(4), System.Boolean(1), 
                ]:
        Assert(isinstance(x, Integral))
    
    for x in [
                System.Double(9), 
                #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=23147
                System.Char.MaxValue, 
                System.Single(8), System.Decimal(10),
                System.SByte(0), System.Byte(1),
                System.Int16(2), System.UInt16(3), System.UInt32(5), System.Int64(6), System.UInt64(7),
                ]:
        Assert(not isinstance(x, Integral))

    #--Number
    for x in [ 
                System.Double(9), System.Int32(4), System.Boolean(1), 
                ]:
        Assert(isinstance(x, Number))
    
    for x in [  
                #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=23147
                System.Char.MaxValue, 
                System.Single(8), System.Decimal(10),
                System.SByte(0), System.Byte(1),
                System.Int16(2), System.UInt16(3), System.UInt32(5), System.Int64(6), System.UInt64(7),
                ]:
        Assert(not isinstance(x, Number))


##PEP352#######################################################################
import sys
import cStringIO

class OutputCatcher(object):
    def __enter__(self):
        self.sys_stdout_bak = sys.stdout
        self.sys_stderr_bak = sys.stderr
        
        sys.stdout = cStringIO.StringIO()
        sys.stderr = cStringIO.StringIO()
        
        return self
    
    def __exit__(self, type, value, traceback):
        sys.stdout.flush()
        sys.stderr.flush()
        
        self.stdout = sys.stdout.getvalue()
        self.stderr = sys.stderr.getvalue()
        
        sys.stdout = self.sys_stdout_bak
        sys.stderr = self.sys_stderr_bak

def test_exception_message_deprecated():
    import exceptions
    x = exceptions.AssertionError()
    
    with OutputCatcher() as output:
        x.message

    expected = ""
    Assert(expected in output.stderr, output.stderr)

def test_generatorexit():
    try:
        raise GeneratorExit()
    except Exception:
        Fail("Should not have caught this GeneratorExit")
    except GeneratorExit:
        pass
        
    try:
        raise GeneratorExit()
    except Exception:
        Fail("Should not have caught this GeneratorExit")
    except GeneratorExit:
        pass
    finally:
        pass
    
    Assert(not isinstance(GeneratorExit(), Exception))
    Assert(isinstance(GeneratorExit(), BaseException))
    

def test_nt_environ_clear_unsetenv():
    bak = eval(str(os.environ))
    os.environ["BLAH"] = "BLAH"
    magic_command = "echo %BLAH%"
    
    try:
        ec = os.system(magic_command)
        AreEqual(ec, 0)
        
        os.environ.clear()
        
        if is_cpython:
            ec = os.system(magic_command)
            Assert(ec != 0, str(ec))
        #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=23205
        else:
            AssertError(WindowsError, os.system, magic_command)
            
    finally:
        os.environ.update(bak)

def test_socket_error_inheritance():
    import socket
    e = socket.error()
    Assert(isinstance(e, IOError))


##mmap#######################################################################
# These should be added to CPython's test_mmap.py when merging
if is_net40 or not is_cli:
    import mmap, gc
    PAGESIZE = mmap.PAGESIZE

    # use str() to get around unicode_literals
    TESTFN = str('testfile.tmp')
    a = str("a")
    b = str("b")
    c = str("c")

    def force_gc_collect():
        import gc
        for i in xrange(10):
            gc.collect()

    def mmap_tearDown():
        force_gc_collect()
        try:
            os.unlink(TESTFN)
        except OSError:
            pass

    def test_mmap_move_large():
        P5 = PAGESIZE * 5
        P10 = PAGESIZE * 10

        f = open(TESTFN, 'w+')

        f.write(a * P5)
        f.write(b * P5)
        f.flush()

        try:
            m = mmap.mmap(f.fileno(), P10)
        finally:
            f.close()

        try:
            # non-overlapping move
            src = P5 + 301
            dest = 153
            length = PAGESIZE + 179
            m[src] = c
            m.move(dest, src, length)
            AreEqual(m.find(c), dest)
            AreEqual(m.find(b), dest + 1)
            AreEqual(m.find(a, dest), dest + length)
            AreEqual(m.rfind(c), src)
            AreEqual(m.rfind(c, 0, src), dest)

            m.write(a * P5)
            m.write(b * P5)
            m.seek(0)

            # overlapping forward move
            a_span = 117
            src = P5 - a_span
            dest = P5 + 289
            length = PAGESIZE + 158
            m[src] = c
            m.move(dest, src, length)
            AreEqual(m.find(c), src)
            AreEqual(m.find(b), src + a_span)
            AreEqual(m.find(c, src + 1), dest)
            AreEqual(m.find(a, dest), dest + 1)
            AreEqual(m.find(b, dest), dest + a_span)
            AreEqual(m.find(a, dest + a_span), -1)

            m.write(a * P5)
            m.write(b * P5)
            m.seek(0)

            # overlapping backward move
            a_span = 131
            src = P5 - a_span
            dest = src - a_span - 60
            length = PAGESIZE + 267
            m[src] = c
            m.move(dest, src, length)
            AreEqual(m.find(c), dest)
            AreEqual(m.find(b), dest + a_span)
            AreEqual(m.find(c, dest + 1), -1)
            AreEqual(m.find(a, dest + a_span), -1)

        finally:
            m.close()

        mmap_tearDown();

    def test_mmap_find_large():
        P10 = PAGESIZE * 10
        P11 = PAGESIZE * 11
        f = open(TESTFN, 'w+')
        try:
            f.write((P10 - 1) * '\0')
            f.write('foofoo')
            f.write('\0' * PAGESIZE)
            f.write('foo')
            f.flush()
            m = mmap.mmap(f.fileno(), P11 + 8)
            f.close()

            AreEqual(m.find('foo'), P10 - 1)
            AreEqual(m.find('foo', P10 - 1), P10 - 1)
            AreEqual(m.find('foo', P10), P10 + 2)
            AreEqual(m.find('foo', P10 + 173), P11 + 5)
            AreEqual(m.find('foo', P10 + 173, P11 + 7), -1)

        finally:
            m.close()
            try:
                f.close()
            except OSError:
                pass

        mmap_tearDown()

    def test_mmap_rfind_large():
        P10 = PAGESIZE * 10
        P11 = PAGESIZE * 11
        f = open(TESTFN, 'w+')
        try:
            f.write((P10 - 1) * '\0')
            f.write('foofoo')
            f.write('\0' * PAGESIZE)
            f.write('foo')
            f.flush()
            m = mmap.mmap(f.fileno(), P11 + 8)
            f.close()

            AreEqual(m.rfind('foo'), P11 + 5)
            AreEqual(m.rfind('foo', 651, P11 - 117), P10 + 2)
            AreEqual(m.rfind('foo', 475, P10 + 5), P10 + 2)
            AreEqual(m.rfind('foo', 475, P10 + 4), P10 - 1)
            AreEqual(m.rfind('foo', P10 + 4, P11 + 7), -1)
            AreEqual(m.rfind('foo', P10 + 4), P11 + 5)

        finally:
            m.close()
            try:
                f.close()
            except OSError:
                pass

        mmap_tearDown()


#--MAIN------------------------------------------------------------------------
run_test(__name__)
