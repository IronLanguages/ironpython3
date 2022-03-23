# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import sys
import unittest

from iptest import big, run_test

from IndicesTest import test_indices

x="abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ"
class SliceTest(unittest.TestCase):
    test_indices = test_indices

    def test_string(self):
        self.assertTrue( x[10] == 'k')
        self.assertTrue( x[20] == 'u')
        self.assertTrue( x[30] == 'E')
        self.assertTrue( x[-10] == 'Q')
        self.assertTrue(x[-3] == 'X')
        self.assertTrue(x[14:20] == 'opqrst')
        self.assertTrue(x[20:14] == '')
        self.assertTrue(x[-30:-5] == 'wxyzABCDEFGHIJKLMNOPQRSTU')
        self.assertTrue(x[-5:-30] == '')
        self.assertTrue(x[3:40:2] == 'dfhjlnprtvxzBDFHJLN')
        self.assertTrue(x[40:3:2] == '')
        self.assertTrue(x[3:40:-2] == '')
        self.assertTrue(x[40:3:-2] == 'OMKIGECAywusqomkige')
        self.assertTrue(x[-40:-4:-2] == '')
        self.assertTrue(x[-4:-40:-2] == 'WUSQOMKIGECAywusqo')
        self.assertTrue(x[-40:-4:2] == 'moqsuwyACEGIKMOQSU')
        self.assertTrue(x[-4:-40:2] == '')
        self.assertTrue(x[-40:-5:-2] == '')
        self.assertTrue(x[-5:-40:-2] == 'VTRPNLJHFDBzxvtrpn')
        self.assertTrue(x[-40:-5:2] == 'moqsuwyACEGIKMOQSU')
        self.assertTrue(x[-5:-40:2] == '')
        self.assertTrue(x[-40:-6:-2] == '')
        self.assertTrue(x[-6:-40:-2] == 'USQOMKIGECAywusqo')
        self.assertTrue(x[-40:-6:2] == 'moqsuwyACEGIKMOQS')
        self.assertTrue(x[-6:-40:2] == '')
        self.assertTrue(x[-49:-5:-3] == '')
        self.assertTrue(x[-5:-49:-3] == 'VSPMJGDAxurolif')
        self.assertTrue(x[-49:-5:3] == 'dgjmpsvyBEHKNQT')
        self.assertTrue(x[-5:-49:3] == '')
        self.assertTrue(x[-50:-5:-3] == '')
        self.assertTrue(x[-5:-50:-3] == 'VSPMJGDAxurolif')
        self.assertTrue(x[-50:-5:3] == 'cfiloruxADGJMPS')
        self.assertTrue(x[-5:-50:3] == '')
        self.assertTrue(x[-51:-5:-3] == '')
        self.assertTrue(x[-5:-51:-3] == 'VSPMJGDAxurolifc')
        self.assertTrue(x[-51:-5:3] == 'behknqtwzCFILORU')
        self.assertTrue(x[-5:-51:3] == '')

    def test_list_tuple(self):
        """check for good behaviour of slices on lists in general"""
        l=list(x)
        self.assertTrue( l[10] == 'k')
        self.assertTrue( l[20] == 'u')
        self.assertTrue( l[30] == 'E')
        self.assertTrue( l[-10] == 'Q')
        self.assertTrue(l[-3] == 'X')
        self.assertTrue(l[14:20] == list('opqrst'))
        self.assertTrue(l[20:14] == [])
        self.assertTrue(l[-51:-5:-3] == [])
        self.assertTrue(l[-5:-51:3] == [])

        self.assertTrue((1, 2, 3, 4, 5)[1:-1][::-1] == (4, 3, 2))
        self.assertTrue([1, 2, 3, 4, 5][1:-1][::-1] == [4, 3, 2])
        self.assertTrue((9, 7, 5, 3) == (1, 2, 3, 4, 5, 6, 7, 8, 9, 0)[1:-1][::-2])
        self.assertTrue([9, 7, 5, 3] == [1, 2, 3, 4, 5, 6, 7, 8, 9, 0][1:-1][::-2])
        self.assertTrue((2, 4, 6, 8) == (1, 2, 3, 4, 5, 6, 7, 8, 9, 0)[1:-1][::2])
        self.assertTrue([2, 4, 6, 8] == [1, 2, 3, 4, 5, 6, 7, 8, 9, 0][1:-1][::2])
        self.assertTrue((2, 5, 8) == (1, 2, 3, 4, 5, 6, 7, 8, 9, 0)[1:-1][::3])
        self.assertTrue([2, 5, 8] == [1, 2, 3, 4, 5, 6, 7, 8, 9, 0][1:-1][::3])

    def test_assign(self):
        l = list(x)
        l[2:50] = "10"
        self.assertTrue(l == list("ab10YZ"))
        l = list(x)
        l[2:50:2] = "~!@#$%^&*()-=_+[]{}|;:/?"
        self.assertTrue(l == list("ab~d!f@h#j$l%n^p&r*t(v)x-z=B_D+F[H]J{L}N|P;R:T/V?XYZ"))

    def test_negative(self):
        l = list(range(10))
        def f1(): l[::3] = [1]
        def f2(): l[::3] = list(range(5))
        def f3(): l[::3] = (1,)
        def f4(): l[::3] = (1, 2, 3, 4, 5, 6)

        for f in (f1, f2, f3, f4):
            self.assertRaises(ValueError, f)

    def test__new__(self):
        self.assertEqual(slice(3) == slice(0, 3, 1), False)
        self.assertEqual(slice(3) == slice(None, 3, None), True)
        self.assertEqual(slice(3) == 3, False)

        self.assertEqual(list(range(10))[slice(None,None,2)], [0,2,4,6,8])

    def test_coverage(self):
        # ToString
        self.assertEqual(str(slice(1,2,3)), 'slice(1, 2, 3)')

    def test_getslice1(self):
        """verify __getslice__ is used for sequence types"""
        class C(list):
            def __getitem__(self, index):
                return (index.start, index.stop)

        a = C()
        self.assertEqual(a[32:197], (32,197))

    def test_getslice_setslice2(self):
        """positive values work w/o len defined"""
        class C(object):
            def __getitem__(self, index):
                return 'Ok'
            def __setitem__(self, index, value):
                self.lastCall = 'set'
            def __delitem__(self, index):
                self.lastCall = 'delete'

        a = C()
        self.assertEqual(a[5:10], 'Ok')

        a.lastCall = ''
        a[5:10] = 'abc'
        self.assertEqual(a.lastCall, 'set')

        a.lastCall = ''
        del(a[5:10])
        self.assertEqual(a.lastCall, 'delete')

    def test_getslice_setslice3(self):
        """all values work w/ length defined"""
        class C(object):
            def __init__(self):
                self.calls = []
            def __getitem__(self, index):
                self.calls.append('get')
                return 'Ok'
            def __setitem__(self, index, value):
                self.calls.append('set')
            def __delitem__(self, index):
                self.calls.append('delete')
            def __len__(self):
                self.calls.append('len')
                return 5

        a = C()
        self.assertEqual(a[3:5], 'Ok')
        self.assertEqual(a.calls, ['get'])

        a = C()
        a[3:5] = 'abc'
        self.assertEqual(a.calls, ['set'])

        a = C()
        del(a[3:5])
        self.assertEqual(a.calls, ['delete'])

        # but call length if it's negative (and we should only call length once)
        a = C()
        self.assertEqual(a[-1:5], 'Ok')
        self.assertEqual(a.calls, ['get'])

        a = C()
        self.assertEqual(a[1:-5], 'Ok')
        self.assertEqual(a.calls, ['get'])

        a = C()
        self.assertEqual(a[-1:-5], 'Ok')
        self.assertEqual(a.calls, ['get'])

        a = C()
        a[-1:5] = 'abc'
        self.assertEqual(a.calls, ['set'])

        a = C()
        a[1:-5] = 'abc'
        self.assertEqual(a.calls, ['set'])

        a = C()
        a[-1:-5] = 'abc'
        self.assertEqual(a.calls, ['set'])

        a = C()
        del(a[-1:5])
        self.assertEqual(a.calls, ['delete'])

        a = C()
        del(a[1:-5])
        self.assertEqual(a.calls, ['delete'])

        a = C()
        del(a[-1:-5])
        self.assertEqual(a.calls, ['delete'])

    def test_simple_slicing(self):
        """verify simple slicing works correctly, even in the face of __getitem__ and friends"""
        class only_slice(object):
            def __getitem__(self, index):
                self.res = 'get', index.start, index.stop
            def __setitem__(self, index, value):
                self.res = 'set', index.start, index.stop, value
            def __delitem__(self, index):
                self.res = 'del', index.start, index.stop

        class mixed_slice(object):
            def __getitem__(self, index):
                if isinstance(index, slice):
                    self.res = 'get', index.start, index.stop
                else:
                    raise Exception()
            def __setitem__(self, index, value):
                if isinstance(index, slice):
                    self.res = 'set', index.start, index.stop, value
                else:
                    raise Exception()
            def __delitem__(self, index):
                if isinstance(index, slice):
                    self.res = 'del', index.start, index.stop
                else:
                    raise Exception()

        for mytype in [only_slice, mixed_slice]:
            x = mytype()
            x[:]
            self.assertEqual(x.res, ('get', None, None))

            x[0:]
            self.assertEqual(x.res, ('get', 0, None))

            x[1:]
            self.assertEqual(x.res, ('get', 1, None))

            x[:100]
            self.assertEqual(x.res, ('get', None, 100))

            x[:] = 2
            self.assertEqual(x.res, ('set', None, None, 2))

            x[0:] = 2
            self.assertEqual(x.res, ('set', 0, None, 2))

            x[1:] = 2
            self.assertEqual(x.res, ('set', 1, None, 2))

            x[:100] = 2
            self.assertEqual(x.res, ('set', None, 100, 2))

            del x[:]
            self.assertEqual(x.res, ('del', None, None))

            del x[0:]
            self.assertEqual(x.res, ('del', 0, None))

            del x[1:]
            self.assertEqual(x.res, ('del', 1, None))

            del x[:100]
            self.assertEqual(x.res, ('del', None, 100))

    def test_slice_getslice_forbidden(self):
        """providing no value for step forbids calling __getslice__"""
        class foo:
            def __getslice__(self, i, j):
                return 42
            def __getitem__(self, index):
                return 23

        self.assertEqual(foo()[::], 23)
        self.assertEqual(foo()[::None], 23)

    def test_slice_setslice_forbidden(self):
        """providing no value for step forbids calling __setslice__"""
        global setVal
        class foo:
            def __setslice__(self, i, j, value):
                global setVal
                setVal = i, j, value
            def __setitem__(self, index, value):
                global setVal
                setVal = index, value

        foo()[::] = 23
        self.assertEqual(setVal, (slice(None, None, None), 23))
        foo()[::None] =  23
        self.assertEqual(setVal, (slice(None, None, None), 23))

    def test_slice_delslice_forbidden(self):
        """providing no value for step forbids calling __delslice__"""
        global setVal
        class foo:
            def __delslice__(self, i, j, value):
                global setVal
                setVal = i, j, value
            def __delitem__(self, index):
                global setVal
                setVal = index

        del foo()[::]
        self.assertEqual(setVal, slice(None, None, None))
        del foo()[::None]
        self.assertEqual(setVal, slice(None, None, None))

    def test_getslice_missing_values(self):
        # missing values are different from passing None explicitly
        class myint(int): pass

        class foo:
            def __getitem__(self, index):
                return (index)
            def __len__(self): return 42

        def validate_slice_result(result, value):
            self.assertEqual(result, value)
            self.assertEqual(result.__class__, slice)

        # only numeric types are passed to __getslice__
        validate_slice_result(foo()[:], slice(None))
        validate_slice_result(foo()[big(2):], slice(2, None))
        validate_slice_result(foo()[2<<64:], slice(36893488147419103232, None))
        validate_slice_result(foo()[:big(2)], slice(2))
        validate_slice_result(foo()[:2<<64], slice(36893488147419103232))
        validate_slice_result(foo()[big(2):big(3)], slice(2, 3))
        validate_slice_result(foo()[2<<64:3<<64], slice(36893488147419103232, 55340232221128654848))
        validate_slice_result(foo()[myint(2):], slice(2, None))
        validate_slice_result(foo()[:myint(2)], slice(2))
        validate_slice_result(foo()[myint(2):myint(3)], slice(2, 3))
        validate_slice_result(foo()[True:], slice(True, None))
        validate_slice_result(foo()[:True], slice(True))
        validate_slice_result(foo()[False:True], slice(False, True))

        def test_slice(foo):
            self.assertEqual(foo()[None:], slice(None, None))
            self.assertEqual(foo()[:None], slice(None, None))
            self.assertEqual(foo()[None:None], slice(None, None))
            self.assertEqual(foo()['abc':], slice('abc', None))
            self.assertEqual(foo()[:'abc'], slice(None, 'abc'))
            self.assertEqual(foo()['abc':'def'], slice('abc', 'def'))
            self.assertEqual(foo()[2.0:], slice(2.0, None))
            self.assertEqual(foo()[:2.0], slice(None, 2.0))
            self.assertEqual(foo()[2.0:3.0], slice(2.0, 3.0))
            self.assertEqual(foo()[1j:], slice(1j, None))
            self.assertEqual(foo()[:1j], slice(None, 1j))
            self.assertEqual(foo()[2j:3j], slice(2j, 3j))

        test_slice(foo)

        class foo:
            def __getitem__(self, index):
                return (index)
            def __len__(self): return 42

        self.assertEqual(foo()[:], slice(None))
        test_slice(foo)

    def test_setslice_missing_values(self):
        # missing values are different from passing None explicitly
        class myint(int): pass

        global setVal
        class foo:
            def __setslice__(self, i, j, value):
                global setVal
                setVal = (i, j, value)
            def __setitem__(self, index, value):
                global setVal
                setVal = (index, value)
            def __len__(self): return 42

        # only numeric types are passed to __getslice__
        foo()[:] = 123
        self.assertEqual(setVal, (slice(None), 123))
        foo()[big(2):] = 123
        self.assertEqual(setVal, (slice(2, None), 123))
        foo()[2<<64:] = 123
        self.assertEqual(setVal, (slice(36893488147419103232, None), 123))
        foo()[:big(2)] = 123
        self.assertEqual(setVal, (slice(2), 123))
        foo()[:2<<64] = 123
        self.assertEqual(setVal, (slice(36893488147419103232), 123))
        foo()[big(2):big(3)] = 123
        self.assertEqual(setVal, (slice(2, 3), 123))
        foo()[2<<64:3<<64] = 123
        self.assertEqual(setVal, (slice(36893488147419103232, 55340232221128654848), 123))
        foo()[myint(2):] = 123
        self.assertEqual(setVal,  (slice(2, None), 123))
        foo()[:myint(2)] = 123
        self.assertEqual(setVal, (slice(2), 123))
        foo()[myint(2):myint(3)] = 123
        self.assertEqual(setVal, (slice(2, 3), 123))
        foo()[True:] = 123
        self.assertEqual(setVal, (slice(True, None), 123))
        foo()[:True] = 123
        self.assertEqual(setVal, (slice(True), 123))
        foo()[False:True] = 123
        self.assertEqual(setVal, (slice(False, True), 123))

        def test_slice(foo):
            foo()[None:] = 123
            self.assertEqual(setVal, (slice(None, None), 123))
            foo()[:None] = 123
            self.assertEqual(setVal, (slice(None, None), 123))
            foo()[None:None] = 123
            self.assertEqual(setVal, (slice(None, None), 123))
            foo()['abc':] = 123
            self.assertEqual(setVal, (slice('abc', None), 123))
            foo()[:'abc'] = 123
            self.assertEqual(setVal, (slice(None, 'abc'), 123))
            foo()['abc':'def'] = 123
            self.assertEqual(setVal, (slice('abc', 'def'), 123))
            foo()[2.0:] = 123
            self.assertEqual(setVal, (slice(2.0, None), 123))
            foo()[:2.0] = 123
            self.assertEqual(setVal, (slice(None, 2.0), 123))
            foo()[2.0:3.0] = 123
            self.assertEqual(setVal, (slice(2.0, 3.0), 123))
            foo()[1j:] = 123
            self.assertEqual(setVal, (slice(1j, None), 123))
            foo()[:1j] = 123
            self.assertEqual(setVal, (slice(None, 1j), 123))
            foo()[2j:3j] = 123
            self.assertEqual(setVal, (slice(2j, 3j), 123))

        test_slice(foo)

        class foo:
            def __setitem__(self, index, value):
                global setVal
                setVal = index, value
            def __len__(self): return 42

        foo()[:] = 123
        self.assertEqual(setVal, (slice(None), 123))
        test_slice(foo)


    def test_delslice_missing_values(self):
        # missing values are different from passing None explicitly
        class myint(int): pass

        global setVal
        class foo:
            def __delslice__(self, i, j):
                global setVal
                setVal = (i, j)
            def __delitem__(self, index):
                global setVal
                setVal = index
            def __len__(self): return 42

        # only numeric types are passed to __getslice__
        del foo()[:]
        self.assertEqual(setVal, slice(None))
        del foo()[big(2):]
        self.assertEqual(setVal, slice(2, None))
        del foo()[2<<64:]
        self.assertEqual(setVal, slice(36893488147419103232, None))
        del foo()[:big(2)]
        self.assertEqual(setVal, slice(2))
        del foo()[:2<<64]
        self.assertEqual(setVal, slice(36893488147419103232))
        del foo()[big(2):big(3)]
        self.assertEqual(setVal, slice(2, 3))
        del foo()[2<<64:3<<64]
        self.assertEqual(setVal, slice(36893488147419103232, 55340232221128654848))
        del foo()[myint(2):]
        self.assertEqual(setVal, slice(2, None))
        del foo()[:myint(2)]
        self.assertEqual(setVal, slice(2))
        del foo()[myint(2):myint(3)]
        self.assertEqual(setVal, slice(2, 3))
        del foo()[:True]
        self.assertEqual(setVal, slice(True))
        del foo()[True:]
        self.assertEqual(setVal, slice(True, None))
        del foo()[False:True]
        self.assertEqual(setVal, slice(False, True))

        def test_slice(foo):
            del foo()[None:]
            self.assertEqual(setVal, slice(None, None))
            del foo()[:None]
            self.assertEqual(setVal, slice(None, None))
            del foo()[None:None]
            self.assertEqual(setVal, slice(None, None))
            del foo()['abc':]
            self.assertEqual(setVal, slice('abc', None))
            del foo()[:'abc']
            self.assertEqual(setVal, slice(None, 'abc'))
            del foo()['abc':'def']
            self.assertEqual(setVal, slice('abc', 'def'))
            del foo()[2.0:]
            self.assertEqual(setVal, slice(2.0, None))
            del foo()[:2.0]
            self.assertEqual(setVal, slice(None, 2.0))
            del foo()[2.0:3.0]
            self.assertEqual(setVal, slice(2.0, 3.0))
            del foo()[1j:]
            self.assertEqual(setVal, slice(1j, None))
            del foo()[:1j]
            self.assertEqual(setVal, slice(None, 1j))
            del foo()[2j:3j]
            self.assertEqual(setVal, slice(2j, 3j))

        test_slice(foo)

        class foo:
            def __delitem__(self, index):
                global setVal
                setVal = index
            def __len__(self): return 42

        del foo()[:]
        self.assertEqual(setVal, slice(None))
        test_slice(foo)

    def test_oldclass_and_direct(self):
        """tests slicing OldInstance's and directly passing a slice object"""
        class OldStyle:
            def __getitem__(self, index):
                return index

        class OldStyleWithLen:
            def __getitem__(self, index):
                return index
            def __len__(self):
                return 10

        class NewStyle(object):
            def __getitem__(self, index):
                return index

        class OldStyleWithLenAndGetSlice:
            def __getitem__(self, index):
                return index
            def __len__(self):
                return 10
            def __getslice__(self, start, stop):
                return start, stop

        # slice object should pass through unmodified if constructed explicitly.
        self.assertEqual(NewStyle()[slice(None, -1, None)], slice(None, -1, None))
        self.assertEqual(OldStyleWithLen()[slice(None, -1, None)], slice(None, -1, None))
        self.assertEqual(OldStyle()[slice(None, -1, None)], slice(None, -1, None))
        self.assertEqual(OldStyleWithLenAndGetSlice()[slice(None, -1, None)], slice(None, -1, None))

        # using the slice syntax
        self.assertEqual(NewStyle()[:-1], slice(None, -1, None))
        self.assertEqual(OldStyleWithLen()[:-1], slice(None, -1, None))
        self.assertEqual(OldStyleWithLenAndGetSlice()[:-1], slice(None, -1))
        self.assertEqual(OldStyle()[:-1:1], slice(None, -1, 1))
        self.assertEqual(OldStyle()[:-1], slice(-1))
        self.assertEqual(OldStyle()[-1:], slice(-1, None))
        self.assertEqual(OldStyle()[:-1:None], slice(None, -1, None))
        self.assertEqual(OldStyle()[-1::None], slice(-1, None, None))
        self.assertEqual(OldStyle()[:-1:], slice(None, -1, None))
        self.assertEqual(OldStyle()[-1::], slice(-1, None, None))

    def test_oldclass_and_direct_set(self):
        """tests slicing OldInstance's and directly passing a slice object"""
        global setVal
        class OldStyle:
            def __setitem__(self, index, value):
                global setVal
                setVal = index, value

        class OldStyleWithLen:
            def __setitem__(self, index, value):
                global setVal
                setVal = index, value
            def __len__(self):
                return 10

        class NewStyle(object):
            def __setitem__(self, index, value):
                global setVal
                setVal = index, value

        class OldStyleWithLenAndGetSlice:
            def __setitem__(self, index, value):
                global setVal
                setVal = index, value
            def __len__(self):
                return 10
            def __setslice__(self, start, stop, value):
                global setVal
                setVal = start, stop, value

        # slice object should pass through unmodified if constructed explicitly.
        NewStyle()[slice(None, -1, None)] = 123
        self.assertEqual(setVal, (slice(None, -1, None), 123))
        OldStyleWithLen()[slice(None, -1, None)] = 123
        self.assertEqual(setVal, (slice(None, -1, None), 123))
        OldStyle()[slice(None, -1, None)] = 123
        self.assertEqual(setVal, (slice(None, -1, None), 123))
        OldStyleWithLenAndGetSlice()[slice(None, -1, None)] = 123
        self.assertEqual(setVal, (slice(None, -1, None), 123))

        # using the slice syntax
        NewStyle()[:-1] = 123
        self.assertEqual(setVal, (slice(None, -1, None), 123))
        OldStyleWithLen()[:-1] = 123
        self.assertEqual(setVal, (slice(None, -1, None), 123))
        OldStyleWithLenAndGetSlice()[:-1] = 123
        self.assertEqual(setVal, (slice(None, -1), 123))
        OldStyle()[:-1:1] = 123
        self.assertEqual(setVal, (slice(None, -1, 1), 123))
        OldStyle()[:-1] = 123
        self.assertEqual(setVal, (slice(-1), 123))
        OldStyle()[-1:] = 123
        self.assertEqual(setVal, (slice(-1, None), 123))
        OldStyle()[:-1:None] = 123
        self.assertEqual(setVal, (slice(None, -1, None), 123))
        OldStyle()[-1::None] = 123
        self.assertEqual(setVal, (slice(-1, None, None), 123))
        OldStyle()[:-1:] = 123
        self.assertEqual(setVal, (slice(None, -1, None), 123))
        OldStyle()[-1::] = 123
        self.assertEqual(setVal, (slice(-1, None, None), 123))

    def test_oldclass_and_direct_delete(self):
        """tests slicing OldInstance's and directly passing a slice object"""
        global setVal
        class OldStyle:
            def __delitem__(self, index):
                global setVal
                setVal = index

        class OldStyleWithLen:
            def __delitem__(self, index):
                global setVal
                setVal = index
            def __len__(self):
                return 10

        class NewStyle(object):
            def __delitem__(self, index):
                global setVal
                setVal = index

        class OldStyleWithLenAndGetSlice:
            def __delitem__(self, index):
                global setVal
                setVal = index
            def __len__(self):
                return 10
            def __delslice__(self, start, stop):
                global setVal
                setVal = start, stop

        # slice object should pass through unmodified if constructed explicitly.
        del NewStyle()[slice(None, -1, None)]
        self.assertEqual(setVal, (slice(None, -1, None)))
        del OldStyleWithLen()[slice(None, -1, None)]
        self.assertEqual(setVal, (slice(None, -1, None)))
        del OldStyle()[slice(None, -1, None)]
        self.assertEqual(setVal, (slice(None, -1, None)))
        del OldStyleWithLenAndGetSlice()[slice(None, -1, None)]
        self.assertEqual(setVal, (slice(None, -1, None)))

        # using the slice syntax
        del NewStyle()[:-1]
        self.assertEqual(setVal, (slice(None, -1, None)))
        del OldStyleWithLen()[:-1]
        self.assertEqual(setVal, (slice(None, -1, None)))
        del OldStyleWithLenAndGetSlice()[:-1]
        self.assertEqual(setVal, slice(None, -1))
        del OldStyle()[:-1:1]
        self.assertEqual(setVal, (slice(None, -1, 1)))
        del OldStyle()[:-1]
        self.assertEqual(setVal, (slice(-1)))
        del OldStyle()[-1:]
        self.assertEqual(setVal, (slice(-1, None)))
        del OldStyle()[:-1:None]
        self.assertEqual(setVal, (slice(None, -1, None)))
        del OldStyle()[-1::None]
        self.assertEqual(setVal, (slice(-1, None, None)))
        del OldStyle()[:-1:]
        self.assertEqual(setVal, (slice(None, -1, None)))
        del OldStyle()[-1::]
        self.assertEqual(setVal, (slice(-1, None, None)))

    def test_cp8297(self):
        #-1
        x = list(range(3))
        x[:-1] = x
        self.assertEqual(x, [0, 1, 2, 2])

        #-2
        x = list(range(3))
        x[:-2] = x
        self.assertEqual(x, [0, 1, 2, 1, 2])

        for i in [0, -3, -10, -1001, -2147483648, -2147483649, -9223372036854775807, -9223372036854775808, -9223372036854775809]:
            x = list(range(3))
            x[:i] = x
            self.assertEqual(x, [0, 1, 2, 0, 1, 2])

    def test_pickle(self):
        from pickle import dumps, loads
        vals = [None, 1]
        for start in vals:
            for stop in vals:
                for step in vals:
                    inp = slice(start, stop, step)
                    self.assertEqual(inp, loads(dumps(inp)))


run_test(__name__)
