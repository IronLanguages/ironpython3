# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import io
import os
import sys
import unittest

from iptest import is_cli, long, run_test, skipUnlessIronPython

class OutputCatcher(object):
    def __enter__(self):
        self.sys_stdout_bak = sys.stdout
        self.sys_stderr_bak = sys.stderr

        sys.stdout = io.StringIO()
        sys.stderr = io.StringIO()

        return self

    def __exit__(self, type, value, traceback):
        sys.stdout.flush()
        sys.stderr.flush()

        self.stdout = sys.stdout.getvalue()
        self.stderr = sys.stderr.getvalue()

        sys.stdout = self.sys_stdout_bak
        sys.stderr = self.sys_stderr_bak

class Python26Test(unittest.TestCase):
    def test_class_decorators(self):
        global called
        called = False
        def f(x):
            global called
            called = True
            return x

        @f
        class x(object): pass

        self.assertEqual(called, True)
        self.assertEqual(type(x), type)

        called = False

        @f
        def abc(): pass

        self.assertEqual(called, True)

        def f(*args):
            def g(x):
                return x
            global called
            called = args
            return g


        @f('foo', 'bar')
        class x(object): pass
        self.assertEqual(called, ('foo', 'bar'))
        self.assertEqual(type(x), type)

        @f('foo', 'bar')
        def x(): pass
        self.assertEqual(called, ('foo', 'bar'))

        def f():
            exec("""@f\nif True: pass\n""")
        self.assertRaises(SyntaxError, f)

    def test_binary_numbers(self):
        self.assertEqual(0b01, 1)
        self.assertEqual(0b10, 2)
        self.assertEqual(0b100000000000000000000000000000000, 4294967296)

        self.assertEqual(type(0b01), int)
        self.assertEqual(type(0b10), int)
        self.assertEqual(type(0b01111111111111111111111111111111), int)
        self.assertEqual(type(0b10000000000000000000000000000000), long)
        self.assertEqual(type(-0b01111111111111111111111111111111), int)
        self.assertEqual(type(-0b10000000000000000000000000000000), int)
        self.assertEqual(type(-0b10000000000000000000000000000001), long)
        self.assertEqual(type(0b00000000000000000000000000000000000001), int)

        self.assertEqual(0b01, 0B01)

    def test_print_function(self):
        self.assertRaises(TypeError, print, sep = 42)
        self.assertRaises(TypeError, print, end = 42)
        self.assertRaises(TypeError, print, abc = 42)
        self.assertRaises(AttributeError, print, file = 42)

        filename = 'abc%d.txt' % os.getpid()

        import sys
        oldout = sys.stdout
        sys.stdout = open(filename, 'w')
        try:
            print('foo')
            print('foo', end = 'abc')
            print()
            print('foo', 'bar', sep = 'abc')

            sys.stdout.close()
            with open(filename) as f:
                self.assertEqual(f.readlines(), ['foo\n', 'fooabc\n', 'fooabcbar\n'])
        finally:
            os.unlink(filename)
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

            self.assertEqual(sys.stdout.text, 'foo\n' + 'fooabc\n' + 'fooabcbar\n' + 'None\n')
        finally:
            sys.stdout = oldout

    def test_user_mappings(self):
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
        l = [(k,v, type(k), type(v)) for k,v in f(**x()).items()]

        l.sort(key=lambda x: str(x[0]))

        self.assertEqual(l, [(str('a'), str('abc'), str, str), (str('b'), str('abc'), str, str), (str('c'), str('abc'), str, str), (str('foo'), str('abc'), ms, str), ])

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

    def test_type_subclasscheck(self):
        global called
        called = []
        class metatype(type):
            def __subclasscheck__(self, sub):
                called.append((self, sub))
                return True

        class myclass(metaclass=metatype): pass

        self.assertEqual(issubclass(int, myclass), True)
        self.assertEqual(called, [(myclass, int)])
        called = []

        self.assertEqual(isinstance(myclass(), int), False)
        self.assertEqual(called, [])

    def test_type_instancecheck(self):
        global called
        called = []
        class metatype(type):
            def __instancecheck__(self, inst):
                called.append((self, inst))
                return True

        class myclass(metaclass=metatype): pass

        self.assertEqual(isinstance(4, myclass), True)
        self.assertEqual(called, [(myclass, 4)])

    def test_complex(self):
        strs = ["5", "2.61e-5", "3e+010", "1.40e09"]
        vals = [5, 2.61e-5, 3e10, 1.4e9]

        strings = ["j", "+j", "-j"] + strs + list(map(lambda x: x + "j", strs))
        values = [1j, 1j, -1j] + vals + list(map(lambda x: x * 1j, vals))
        neg_strings = []
        neg_values = []

        for s0,v0 in zip(strs, vals):
            for s1,v1 in zip(strs, vals):
                for sign,mult in [("+",1), ("-",-1)]:
                    newstrs_pos = [s0+sign+s1+"j", s1+sign+s0+"j"]
                    newvals_pos = [complex(v0,v1*mult), complex(v1,v0*mult)]
                    strings += newstrs_pos
                    strings += list(map(lambda x: "(" + x + ")", newstrs_pos))
                    values += newvals_pos * 2

                    newstrs_neg = [s0+"j"+sign+s1, s1+"j"+sign+s0]
                    newvals_neg = [complex(v1*mult,v0), complex(v0*mult,v1)]
                    neg_strings += newstrs_neg
                    neg_strings += map(lambda x: "(" + x + ")", newstrs_neg)
                    neg_values += newvals_neg * 2

        for s,v in zip(strings, values):
            self.assertEqual(complex(s), v)
            self.assertEqual(v, complex(v.__repr__()))

        for s,v in zip(neg_strings, neg_values):
            self.assertRaisesRegex(ValueError, "complex\(\) arg is a malformed string",
                                        complex, s)

    def test_deque(self):
        from _collections import deque

        # make sure __init__ clears existing contents
        x = deque([6,7,8,9])
        x.__init__(deque([1,2,3]))
        self.assertEqual(x, deque([1,2,3]))
        x.__init__()
        self.assertEqual(x, deque())

        # test functionality with maxlen
        x = deque(maxlen=5)
        for i in range(5):
            x.append(i)
            self.assertEqual(x, deque(range(i+1)))
        x.append(5)
        self.assertEqual(x, deque([1,2,3,4,5]))
        x.appendleft(100)
        self.assertEqual(x, deque([100,1,2,3,4]))
        x.extend(range(10))
        self.assertEqual(x, deque([5,6,7,8,9]))
        x.extendleft(range(10,20))
        self.assertEqual(x, deque([19,18,17,16,15]))
        x.remove(19)
        self.assertEqual(x, deque([18,17,16,15]))
        x.rotate()
        self.assertEqual(x, deque([15,18,17,16]))
        x.rotate(-8)
        self.assertEqual(x, deque([15,18,17,16]))
        x.pop()
        self.assertEqual(x, deque([15,18,17]))
        x.rotate(-1)
        self.assertEqual(x, deque([18,17,15]))
        x.popleft()
        self.assertEqual(x, deque([17,15]))
        x.extendleft(range(4))
        self.assertEqual(x, deque([3,2,1,0,17]))
        x.rotate(3)
        self.assertEqual(x, deque([1,0,17,3,2]))
        x.extend(range(3))
        self.assertEqual(x, deque([3,2,0,1,2]))
        y = x.__copy__()
        self.assertEqual(x, y)
        x.extend(range(4))
        y.extend(range(4))
        self.assertEqual(x, y)

    def test_set_multiarg(self):
        from iptest.type_util import myset, myfrozenset

        s1 = [2, 4, 5]
        s2 = [4, 7, 9, 10]
        s3 = [2, 4, 5, 6]

        for A in (set, myset):
            for B in (set, frozenset, myset, myfrozenset):
                as1, as2, as3 = A(s1), A(s2), A(s3)
                bs1, bs2, bs3 = B(s1), B(s2), B(s3)

                self.assertEqual(as1.union(as2, as3), A([2, 4, 5, 6, 7, 9, 10]))
                self.assertEqual(as1.intersection(as2, as3), A([4]))
                self.assertEqual(as2.difference(as3, A([2, 7, 8])), A([9, 10]))

                self.assertEqual(bs1.union(as2, as3), A([2, 4, 5, 6, 7, 9, 10]))
                self.assertEqual(bs1.intersection(as2, as3), A([4]))
                self.assertEqual(bs2.difference(as3, A([2, 7, 8])), A([9, 10]))

                self.assertEqual(as1.union(bs2, as3), A([2, 4, 5, 6, 7, 9, 10]))
                self.assertEqual(as1.intersection(as2, bs3), A([4]))
                self.assertEqual(as2.difference(as3, B([2, 7, 8])), A([9, 10]))

                as1.update(as2, bs3)
                self.assertEqual(as1, B([2, 4, 5, 6, 7, 9, 10]))
                as2.difference_update(bs3, A([2, 7, 8]))
                self.assertEqual(as2, A([9, 10]))
                as3.intersection_update(bs2, bs1)
                self.assertEqual(as3, B([4]))

    def test_attrgetter(self):
        import operator

        tests = ['abc', 3, ['d','e','f'], (1,4,9)]

        get0 = operator.attrgetter('__class__.__name__')
        get1 = operator.attrgetter('__class__..')
        get2 = operator.attrgetter('__class__..__name__')
        get3 = operator.attrgetter('__class__.__name__.__class__.__name__')

        for x in tests:
            self.assertEqual(x.__class__.__name__, get0(x))
            self.assertRaises(AttributeError, get1, x)
            self.assertRaises(AttributeError, get2, x)
            self.assertEqual('str', get3(x))

    def test_im_aliases(self):
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

        fs = [a.f, b.f, c.f, d.f]
        gs = [c.g, d.g]
        f2s = [e.f, f.f]
        all = fs + gs + f2s

        for r in [fs, f2s]:
            for s in [fs, f2s]:
                if r == s:
                    for x in r:
                        for y in r:
                            self.assertEqual(x.__func__, y.__func__)
                else:
                    for x in r:
                        for y in s:
                            if x != y:
                                self.assertNotEqual(x.__self__, y.__self__)
                                self.assertNotEqual(x.__func__, y.__func__)


        self.assertEqual(a.f.__func__, c.f.__func__)
        self.assertEqual(b.f.__func__, d.f.__func__)
        self.assertNotEqual(a.f.__self__, b.f.__self__)
        self.assertNotEqual(b.f.__self__, d.f.__self__)

    def test_tuple_index(self):
        t = (1,4,3,0,3)

        self.assertRaises(TypeError, t.index)
        self.assertRaises(TypeError, t.index, 3, 'a')
        self.assertRaises(TypeError, t.index, 3, 2, 'a')
        self.assertRaises(TypeError, t.index, 3, 2, 5, 1)

        self.assertRaises(ValueError, t.index, 5)
        self.assertRaises(ValueError, t.index, 'a')

        self.assertEqual(t.index(1), 0)
        self.assertEqual(t.index(3), 2)
        self.assertEqual(t.index(3, 2), 2)
        self.assertEqual(t.index(3, 3), 4)
        self.assertRaises(ValueError, t.index, 3, 3, 4)
        self.assertEqual(t.index(3, 3, 5), 4)
        self.assertEqual(t.index(3, 3, 100), 4)

        self.assertEqual(t.index(3, -1), 4)
        self.assertEqual(t.index(3, -3, -1), 2)
        self.assertEqual(t.index(3, 2, -2), 2)
        self.assertRaises(ValueError, t.index, 3, 3, -1)
        self.assertRaises(ValueError, t.index, 3, -2, 0)

    def test_tuple_count(self):
        t = ('1','2','3',1,3,3,2,3,1,'1','1',3,'3')

        self.assertRaises(TypeError, t.count)
        self.assertRaises(TypeError, t.count, 1, 2)

        self.assertEqual(t.count('1'), 3)
        self.assertEqual(t.count('2'), 1)
        self.assertEqual(t.count('3'), 2)
        self.assertEqual(t.count(1), 2)
        self.assertEqual(t.count(2), 1)
        self.assertEqual(t.count(3), 4)

    def test_builtin_next(self):
        from _collections import deque
        values = [1,2,3,4]
        iterable_list = [list, tuple, set, deque]

        for iterable in iterable_list:
            i = iter(iterable(values))
            self.assertEqual(next(i), 1)
            self.assertEqual(next(i), 2)
            self.assertEqual(next(i), 3)
            self.assertEqual(next(i), 4)
            self.assertRaises(StopIteration, next, i)

            i = iter(iterable(values))
            self.assertEqual(next(i, False), 1)
            self.assertEqual(next(i, False), 2)
            self.assertEqual(next(i, False), 3)
            self.assertEqual(next(i, False), 4)
            self.assertEqual(next(i, False), False)
            self.assertEqual(next(i, False), False)

        i = iter('abcdE')
        self.assertEqual(next(i), 'a')
        self.assertEqual(next(i), 'b')
        self.assertEqual(next(i), 'c')
        self.assertEqual(next(i), 'd')
        self.assertEqual(next(i), 'E')
        self.assertRaises(StopIteration, next, i)

        i = iter('edcbA')
        self.assertEqual(next(i, False), 'e')
        self.assertEqual(next(i, False), 'd')
        self.assertEqual(next(i, False), 'c')
        self.assertEqual(next(i, False), 'b')
        self.assertEqual(next(i, False), 'A')
        self.assertEqual(next(i, False), False)
        self.assertEqual(next(i, False), False)

    def test_sys_flags(self):
        import sys
        self.assertIn('flags', dir(sys))

        # Assertion helpers
        def IsInt(x):
            self.assertEqual(type(x), int)
        def IsFlagInt(x):
            self.assertIn(x, [0,1,2])

        # Check repr
        self.assertEqual(repr(type(sys.flags)), "<class 'sys.flags'>")
        self.assertTrue(repr(sys.flags).startswith("sys.flags(debug="))
        self.assertTrue(repr(sys.flags).endswith(")"))

        # Check attributes
        attrs = set(dir(sys.flags))
        structseq_attrs = set(["n_fields", "n_sequence_fields", "n_unnamed_fields"])
        flag_attrs = set(['bytes_warning', 'debug', 'dont_write_bytecode', 'ignore_environment', 'inspect', 'interactive', 'no_site', 'no_user_site', 'optimize', 'quiet', 'verbose'])
        if not is_cli:
            flag_attrs.update({'hash_randomization', 'isolated'})
            if sys.version_info >= (3,7):
                flag_attrs.update({'dev_mode', 'utf8_mode'})
        expected_attrs = structseq_attrs.union(flag_attrs, dir(object), dir(tuple))

        self.assertEqual(attrs, set(dir(type(sys.flags))))
        self.assertEqual(expected_attrs - attrs, set()) # check for missing attrs
        self.assertEqual(attrs - expected_attrs, set()) # check for too many attrs

        for attr in structseq_attrs.union(flag_attrs):
            IsInt(getattr(sys.flags, attr))
        for attr in flag_attrs:
            IsFlagInt(getattr(sys.flags, attr))
        self.assertEqual(sys.flags.n_sequence_fields, len(flag_attrs))
        self.assertEqual(sys.flags.n_fields, sys.flags.n_sequence_fields)
        self.assertEqual(sys.flags.n_unnamed_fields, 0)

        # Test tuple-like functionality

        # __add__
        x = sys.flags + ()
        y = sys.flags + (7,)
        z = sys.flags + (6,5,4,3,2)

        # __len__
        self.assertEqual(len(sys.flags), len(flag_attrs))
        self.assertEqual(len(sys.flags), len(x))
        self.assertEqual(len(sys.flags), len(y) - 1)
        self.assertEqual(len(sys.flags), len(z) - 5)

        # __eq__
        self.assertEqual(sys.flags, x)
        self.assertTrue(sys.flags == x)
        self.assertTrue(x == sys.flags)
        self.assertTrue(sys.flags == sys.flags)
        # __ne__
        self.assertFalse(sys.flags != sys.flags)
        self.assertTrue(sys.flags != z)
        self.assertTrue(z != sys.flags)
        # __ge__
        self.assertTrue(sys.flags >= sys.flags)
        self.assertTrue(sys.flags >= x)
        self.assertTrue(x >= sys.flags)
        self.assertFalse(sys.flags >= y)
        self.assertTrue(z >= sys.flags)
        # __le__
        self.assertTrue(sys.flags <= sys.flags)
        self.assertTrue(sys.flags <= x)
        self.assertTrue(x <= sys.flags)
        self.assertTrue(sys.flags <= y)
        self.assertFalse(y <= sys.flags)
        # __gt__
        self.assertFalse(sys.flags > sys.flags)
        self.assertFalse(sys.flags > x)
        self.assertFalse(x > sys.flags)
        self.assertFalse(sys.flags > y)
        self.assertTrue(z > sys.flags)
        # __lt__
        self.assertFalse(sys.flags < sys.flags)
        self.assertFalse(sys.flags < x)
        self.assertFalse(x < sys.flags)
        self.assertTrue(sys.flags < y)
        self.assertFalse(y < sys.flags)

        # __mul__
        self.assertEqual(sys.flags * 2, x * 2)
        self.assertEqual(sys.flags * 5, x * 5)
        # __rmul__
        self.assertEqual(5 * sys.flags, x * 5)

        # __contains__
        self.assertEqual(0 in sys.flags, 0 in x)
        self.assertEqual(1 in sys.flags, 1 in x)
        self.assertEqual(2 in sys.flags, 2 in x)
        self.assertFalse(3 in sys.flags)

        # __getitem__
        for i in range(len(sys.flags)):
            self.assertEqual(sys.flags[i], x[i])
        # __getslice__
        self.assertEqual(sys.flags[:], x[:])
        self.assertEqual(sys.flags[2:], x[2:])
        self.assertEqual(sys.flags[3:6], x[3:6])
        self.assertEqual(sys.flags[1:-1], x[1:-1])
        self.assertEqual(sys.flags[-7:11], x[-7:11])
        self.assertEqual(sys.flags[-10:-5], x[-10:-5])

        # other sequence ops
        self.assertEqual(set(sys.flags), set(x))
        self.assertEqual(list(sys.flags), list(x))
        count = 0
        for f in sys.flags:
            count += 1
            IsFlagInt(f)
        self.assertEqual(count, len(sys.flags))

        # sanity check
        if (sys.dont_write_bytecode):
            self.assertEqual(sys.flags.dont_write_bytecode, 1)
        else:
            self.assertEqual(sys.flags.dont_write_bytecode, 0)

    def test_functools_reduce(self):
        import _functools

        words = ["I", "am", "the", "walrus"]
        combine = lambda s,t: s + " " + t

        self.assertTrue(hasattr(_functools, "reduce"))

        self.assertEqual(_functools.reduce(combine, words), "I am the walrus")

    def test_log(self):
        import math

        zeros = [-1, -1.0, long(-1), 0, 0.0, long(0)]
        nonzeros = [2, 2.0, long(2)]
        ones = [1, 1.0, long(1)]

        if is_cli: # https://github.com/IronLanguages/ironpython3/issues/52
            self.assertNotEqual(type(zeros[0]), type(zeros[2]))
        else:
            self.assertEqual(type(zeros[0]), type(zeros[2]))

        for z0 in zeros:
            self.assertRaises(ValueError, math.log, z0)
            self.assertRaises(ValueError, math.log10, z0)
            for z in zeros:
                self.assertRaises(ValueError, math.log, z0, z)
            for n in nonzeros + ones:
                self.assertRaises(ValueError, math.log, z0, n)
                self.assertRaises(ValueError, math.log, n, z0)

        for one in ones:
            for n in nonzeros:
                self.assertRaises(ZeroDivisionError, math.log, n, one)

    def test_trunc(self):
        import sys, math

        test_values = [-1, 0, 1, long(-1), long(0), long(1), -1.0, 0.0, 1.0, sys.maxsize + 0.5,
                    -sys.maxsize - 0.5, 9876543210, -9876543210, -1e100, 1e100]

        for value in test_values:
            self.assertEqual(long(value), math.trunc(value))
            self.assertTrue(isinstance(math.trunc(value), int))

    # A small extension of CPython's test_struct.py, which does not make sure that empty
    # dictionaries are interpreted as false
    def test_struct_bool(self):
        import _struct
        for prefix in tuple("<>!=")+('',):
            format = str(prefix + '?')
            packed = _struct.pack(format, {})
            unpacked = _struct.unpack(format, packed)

            self.assertEqual(len(unpacked), 1)
            self.assertFalse(unpacked[0])


    ###############################################################################
    ##PEP 3110
    def test_pep3110(self):
        global ValueError
        orig_ValueError = ValueError

        #--Make sure the undesired CPython 2.6 behavior still works
        try:
            raise TypeError("abc")
        except TypeError as ValueError:
            self.assertEqual(ValueError.args[0], "abc")
            self.assertTrue(isinstance(ValueError, TypeError))
        ValueError = orig_ValueError

        try:
            raise TypeError("abc")
        except TypeError as ValueError:
            self.assertEqual(ValueError.args[0], "abc")
        finally:
            ValueError = orig_ValueError
        self.assertEqual(ValueError, orig_ValueError)

        #negative
        try:
            try:
                raise IOError("abc")
            except TypeError as ValueError:
                Fail("IOError is not the same as TypeError")
        except IOError:
            pass
        self.assertEqual(ValueError, orig_ValueError)

        try:
            try:
                raise IOError("abc")
            except TypeError as ValueError:
                Fail("IOError is not the same as TypeError")
            finally:
                pass
        except IOError:
            pass
        self.assertEqual(ValueError, orig_ValueError)

        #--Make sure the desired CPython 2.5 behavior still works
        try:
            raise TypeError("xyz")
        except (TypeError, ValueError) as e:
            self.assertEqual(e.args[0], "xyz")
        e = None

        try:
            raise TypeError("xyz")
        except (TypeError, ValueError) as e:
            self.assertEqual(e.args[0], "xyz")
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
            except (TypeError, ValueError) as e:
                Fail("IOError is not the same as TypeError or ValueError")
        except IOError:
            pass
        self.assertEqual(e, None)

        try:
            try:
                raise IOError("xyz")
            except (TypeError, ValueError) as e:
                Fail("IOError is not the same as TypeError or ValueError")
            finally:
                pass
        except IOError:
            pass
        self.assertEqual(e, None)

        #--Now test 'except ... as ...:'
        try:
            raise TypeError("abc")
        except TypeError as ValueError:
            self.assertEqual(ValueError.args[0], "abc")
            self.assertTrue(isinstance(ValueError, TypeError))
        ValueError = orig_ValueError

        try:
            raise TypeError("abc")
        except TypeError as e:
            self.assertEqual(e.args[0], "abc")
            self.assertTrue(isinstance(e, TypeError))
        e = None

        try:
            raise TypeError("abc")
        except TypeError as ValueError:
            self.assertEqual(ValueError.args[0], "abc")
            self.assertTrue(isinstance(ValueError, TypeError))
        finally:
            ValueError = orig_ValueError
        self.assertEqual(ValueError, orig_ValueError)

        try:
            raise TypeError("abc")
        except TypeError as e:
            self.assertEqual(e.args[0], "abc")
            self.assertTrue(isinstance(e, TypeError))
        finally:
            e = None
        self.assertEqual(e, None)

        try:
            raise IOError("abc")
        except TypeError as e:
            Fail("IOError is not the same as TypeError")
        except IOError as e:
            self.assertEqual(e.args[0], "abc")
        e = None

        try:
            raise IOError("abc")
        except TypeError as e:
            Fail("IOError is not the same as TypeError")
        except IOError as e:
            self.assertEqual(e.args[0], "abc")
        e = None

        try:
            raise IOError("abc")
        except TypeError as e:
            Fail("IOError is not the same as TypeError")
        except Exception as e:
            self.assertEqual(e.args[0], "abc")
        e = None

        try:
            raise IOError("abc")
        except TypeError as e:
            Fail("IOError is not the same as TypeError")
        except:
            pass

        #neg
        try:
            try:
                raise IOError("abc")
            except TypeError as e:
                Fail("IOError is not the same as TypeError")
        except IOError as e:
            self.assertEqual(e.args[0], "abc")
        e = None

        try:
            try:
                raise IOError("abc")
            except TypeError as ValueError:
                Fail("IOError is not the same as TypeError")
        except IOError as e:
            self.assertEqual(e.args[0], "abc")
        e = None

        try:
            try:
                raise IOError("abc")
            except TypeError as e:
                Fail("IOError is not the same as TypeError")
        except IOError as e:
            self.assertEqual(e.args[0], "abc")
        e = None

        try:
            try:
                raise IOError("abc")
            except TypeError as ValueError:
                Fail("IOError is not the same as TypeError")
            finally:
                pass
        except Exception as e:
            self.assertEqual(e.args[0], "abc")
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
    def test_pep3112(self):
        self.assertEqual(len("abc"), 3)

        #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=19521
        self.assertEqual(len("\u751f"), 1)

    ##PEP##########################################################################
    def test_pep19546(self):
        '''
        Just a small sanity test.  CPython's test_int.py covers this PEP quite
        well.
        '''
        #Octal
        self.assertEqual(0O21, 17)
        self.assertEqual(0O21, 0o21)
        self.assertEqual(0o0, 0)
        self.assertEqual(-0o1, -1)
        self.assertEqual(0o17777777776, 2147483646)
        self.assertEqual(0o17777777777, 2147483647)
        self.assertEqual(0o20000000000, 2147483648)
        self.assertEqual(-0o17777777777, -2147483647)
        self.assertEqual(-0o20000000000, -2147483648)
        self.assertEqual(-0o20000000001, -2147483649)

        #Binary
        self.assertEqual(0B11, 3)
        self.assertEqual(0B11, 0b11)
        self.assertEqual(0b0, 0)
        self.assertEqual(-0b1, -1)
        self.assertEqual(0b1111111111111111111111111111110, 2147483646)
        self.assertEqual(0b1111111111111111111111111111111, 2147483647)
        self.assertEqual(0b10000000000000000000000000000000, 2147483648)
        self.assertEqual(-0b1111111111111111111111111111111, -2147483647)
        self.assertEqual(-0b10000000000000000000000000000000, -2147483648)
        self.assertEqual(-0b10000000000000000000000000000001, -2147483649)

        #bin and oct
        test_cases = [  (0B11, "0b11", "0o3"),
                        (2147483648, "0b10000000000000000000000000000000", "0o20000000000"),
                        (long(-2147483649), "-0b10000000000000000000000000000001", "-0o20000000001"),
                        (long(-1),          "-0b1", "-0o1"),
                        (-0b10000000000000000000000000000000, "-0b10000000000000000000000000000000", "-0o20000000000"),
                        (-0o17777777777, "-0b1111111111111111111111111111111", "-0o17777777777"),
                        (0o17777777777, "0b1111111111111111111111111111111", "0o17777777777"),
                        ]
        for val, bin_exp, oct_exp in test_cases:
            self.assertEqual(bin(val), bin_exp)
            self.assertEqual(oct(val), oct_exp)

    @skipUnlessIronPython()
    def test_pep3141(self):
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
            self.assertTrue(isinstance(x, Complex))

        for x in [
                    #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=23147
                    System.Char.MaxValue,
                    System.Single(8), System.Decimal(10),
                    System.SByte(0), System.Byte(1),
                    System.Int16(2), System.UInt16(3), System.UInt32(5), System.Int64(6), System.UInt64(7),
                    ]:
            self.assertTrue(not isinstance(x, Complex), x)

        #--Real
        for x in [
                    System.Double(9), System.Int32(4), System.Boolean(1),
                    ]:
            self.assertTrue(isinstance(x, Real))

        for x in [
                    #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=23147
                    System.Char.MaxValue,
                    System.Single(8), System.Decimal(10),
                    System.SByte(0), System.Byte(1),
                    System.Int16(2), System.UInt16(3), System.UInt32(5), System.Int64(6), System.UInt64(7),
                    ]:
            self.assertTrue(not isinstance(x, Real))


        #--Rational
        for x in [
                    System.Int32(4), System.Boolean(1),
                    ]:
            self.assertTrue(isinstance(x, Rational))

        for x in [
                    System.Double(9),
                    #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=23147
                    System.Char.MaxValue,
                    System.Single(8), System.Decimal(10),
                    System.SByte(0), System.Byte(1),
                    System.Int16(2), System.UInt16(3), System.UInt32(5), System.Int64(6), System.UInt64(7),
                    ]:
            self.assertTrue(not isinstance(x, Rational))

        #--Integral
        for x in [
                    System.Int32(4), System.Boolean(1),
                    ]:
            self.assertTrue(isinstance(x, Integral))

        for x in [
                    System.Double(9),
                    #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=23147
                    System.Char.MaxValue,
                    System.Single(8), System.Decimal(10),
                    System.SByte(0), System.Byte(1),
                    System.Int16(2), System.UInt16(3), System.UInt32(5), System.Int64(6), System.UInt64(7),
                    ]:
            self.assertTrue(not isinstance(x, Integral))

        #--Number
        for x in [
                    System.Double(9), System.Int32(4), System.Boolean(1),
                    ]:
            self.assertTrue(isinstance(x, Number))

        for x in [
                    #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=23147
                    System.Char.MaxValue,
                    System.Single(8), System.Decimal(10),
                    System.SByte(0), System.Byte(1),
                    System.Int16(2), System.UInt16(3), System.UInt32(5), System.Int64(6), System.UInt64(7),
                    ]:
            self.assertTrue(not isinstance(x, Number))


    def test_generatorexit(self):
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

        self.assertTrue(not isinstance(GeneratorExit(), Exception))
        self.assertTrue(isinstance(GeneratorExit(), BaseException))


    def test_nt_environ_clear_unsetenv(self):
        bak = dict(os.environ)
        os.environ["BLAH"] = "BLAH"
        magic_command = "echo %BLAH%"

        try:
            ec = os.system(magic_command)
            self.assertEqual(ec, 0)

            os.environ.clear()

            ec = os.system(magic_command)
            self.assertTrue(ec != 0, str(ec))

        finally:
            os.environ.update(bak)

    def test_socket_error_inheritance(self):
        import socket
        e = socket.error()
        self.assertTrue(isinstance(e, IOError))


##mmap#######################################################################
# These should be added to CPython's test_mmap.py when merging
import mmap, gc
PAGESIZE = mmap.PAGESIZE

# use str() to get around unicode_literals
TESTFN = str('testfile_%d.tmp' % os.getpid())

class Python26MmapTest(unittest.TestCase):
        def force_gc_collect(self):
            import gc
            for i in range(10):
                gc.collect()

        def mmap_tearDown(self):
            self.force_gc_collect()
            try:
                os.unlink(TESTFN)
            except OSError:
                pass

        def test_mmap_move_large(self):
            P5 = PAGESIZE * 5
            P10 = PAGESIZE * 10

            a = b"a"
            b = b"b"
            c = b"c"

            f = open(TESTFN, 'wb+')

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
                m[src] = ord(c)
                m.move(dest, src, length)
                self.assertEqual(m.find(c), dest)
                self.assertEqual(m.find(b), dest + 1)
                self.assertEqual(m.find(a, dest), dest + length)
                self.assertEqual(m.rfind(c), src)
                self.assertEqual(m.rfind(c, 0, src), dest)

                m.write(a * P5)
                m.write(b * P5)
                m.seek(0)

                # overlapping forward move
                a_span = 117
                src = P5 - a_span
                dest = P5 + 289
                length = PAGESIZE + 158
                m[src] = ord(c)
                m.move(dest, src, length)
                self.assertEqual(m.find(c), src)
                self.assertEqual(m.find(b), src + a_span)
                self.assertEqual(m.find(c, src + 1), dest)
                self.assertEqual(m.find(a, dest), dest + 1)
                self.assertEqual(m.find(b, dest), dest + a_span)
                self.assertEqual(m.find(a, dest + a_span), -1)

                m.write(a * P5)
                m.write(b * P5)
                m.seek(0)

                # overlapping backward move
                a_span = 131
                src = P5 - a_span
                dest = src - a_span - 60
                length = PAGESIZE + 267
                m[src] = ord(c)
                m.move(dest, src, length)
                self.assertEqual(m.find(c), dest)
                self.assertEqual(m.find(b), dest + a_span)
                self.assertEqual(m.find(c, dest + 1), -1)
                self.assertEqual(m.find(a, dest + a_span), -1)

            finally:
                m.close()

            self.mmap_tearDown()

        def test_mmap_find_large(self):
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

                self.assertEqual(m.find(b'foo'), P10 - 1)
                self.assertEqual(m.find(b'foo', P10 - 1), P10 - 1)
                self.assertEqual(m.find(b'foo', P10), P10 + 2)
                self.assertEqual(m.find(b'foo', P10 + 173), P11 + 5)
                self.assertEqual(m.find(b'foo', P10 + 173, P11 + 7), -1)

            finally:
                m.close()
                try:
                    f.close()
                except OSError:
                    pass

            self.mmap_tearDown()

        def test_mmap_rfind_large(self):
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

                self.assertEqual(m.rfind(b'foo'), P11 + 5)
                self.assertEqual(m.rfind(b'foo', 651, P11 - 117), P10 + 2)
                self.assertEqual(m.rfind(b'foo', 475, P10 + 5), P10 + 2)
                self.assertEqual(m.rfind(b'foo', 475, P10 + 4), P10 - 1)
                self.assertEqual(m.rfind(b'foo', P10 + 4, P11 + 7), -1)
                self.assertEqual(m.rfind(b'foo', P10 + 4), P11 + 5)

            finally:
                m.close()
                try:
                    f.close()
                except OSError:
                    pass

            self.mmap_tearDown()

#--MAIN------------------------------------------------------------------------
run_test(__name__)
