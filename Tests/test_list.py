# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import os
import unittest

from iptest import IronPythonTestCase, long, run_test, skipUnlessIronPython

hitCount = 0

class ListTest(IronPythonTestCase):
    def test_extend_self(self):
        l=['a','b','c']
        l.extend(l)
        self.assertTrue(l==['a','b','c','a','b','c'])

    def test_append_self(self):
        """verify repr and print have the same result for a recursive list"""
        a = list('abc')
        a.append(a)
        self.assertEqual(str(a), "['a', 'b', 'c', [...]]")

        ## file
        fn = os.path.join(self.temporary_dir, "testfile.txt")

        with open(fn, "w") as fo:
            a = list('abc')
            a.append(a)
            print(a, end='', file=fo)

        with open(fn, "r") as fo:
            self.assertTrue(fo.read() == repr(a))

    @skipUnlessIronPython()
    def test_cli_enumerator(self):
        import clr
        x = [1,2,3]
        y = []
        xenum = iter(x)
        while xenum.MoveNext():
            y.append(xenum.Current)
        self.assertEqual(x, y)

    @skipUnlessIronPython()
    def test_generic_list(self):
        """https://github.com/IronLanguages/ironpython2/issues/109"""
        from System.Collections.Generic import List
        lst = List[str]()
        lst.Add('Hello')
        lst.Add('World')
        vals = []
        for v in lst[1:]:
            vals.append(v)
        self.assertEqual(vals, ['World'])
        lst.Add('Universe')
        self.assertEqual(list(lst[0::2]), ['Hello', 'Universe'])

    def test_assign_to_empty(self):
        # should all succeed
        y = []
        [] = y
        [], t = y, 0
        [[[]]] = [[y]]
        del y

    def test_unpack(self):
        listOfSize2 = [1, 2]

        # Disallow unequal unpacking assignment
        def f1(): [a, b, c] = listOfSize2
        def f2(): del a
        def f3(): [a] = listOfSize2

        self.assertRaises(ValueError, f1)
        self.assertRaises(NameError, f2)
        self.assertRaises(ValueError, f3)
        self.assertRaises(NameError, f2)

        [a, [b, c]] = [listOfSize2, listOfSize2]
        self.assertEqual(a, listOfSize2)
        self.assertEqual(b, 1)
        self.assertEqual(c, 2)
        del a, b, c

        [[a, b], c] = (listOfSize2, listOfSize2)
        self.assertEqual(a, 1)
        self.assertEqual(b, 2)
        self.assertEqual(c, listOfSize2)
        del a, b, c

    def test_sort(self):
        """named params passed to sort"""
        LExpected = ['A', 'b', 'c', 'D']
        L = ['D', 'c', 'b', 'A']
        L.sort(key=lambda x: x.lower())
        self.assertTrue(L == LExpected)

        l = [1, 2, 3]
        l2 = l[:]
        l.sort(key=lambda x: x)
        self.assertEqual(l, l2)
        l.sort(key=lambda x: x)
        self.assertEqual(l, l2)

    def test_list_in_list(self):
        aList = [['a']]
        anItem = ['a']
        self.assertEqual( aList.index(anItem), 0 )
        self.assertTrue(anItem in aList)

    def test_pop(self):
        x = [1,2,3,4,5,6,7,8,9,0]
        self.assertTrue(x.pop() == 0)
        self.assertTrue(x.pop(3) == 4)
        self.assertTrue(x.pop(-5) == 5)
        self.assertTrue(x.pop(0) == 1)
        self.assertTrue(x.pop() == 9)
        self.assertTrue(x.pop(2) == 6)
        self.assertTrue(x.pop(3) == 8)
        self.assertTrue(x.pop(-1) == 7)
        self.assertTrue(x.pop(-2) == 2)
        self.assertTrue(x.pop() == 3)

    def test_add_mul(self):
        x = [1,2,3]
        x += [4,5,6]
        self.assertTrue(x == [1,2,3,4,5,6])

        x = [1,2,3]
        self.assertEqual(x * 2, [1,2,3,1,2,3])
        self.assertEqual(2 * x, [1,2,3,1,2,3])

        class mylong(long): pass
        self.assertEqual([1, 2] * mylong(2), [1, 2, 1, 2])
        self.assertEqual([3, 4].__mul__(mylong(2)), [3, 4, 3, 4])
        self.assertEqual([5, 6].__rmul__(mylong(2)), [5, 6, 5, 6])
        self.assertEqual(mylong(2) * [7,8] , [7, 8, 7, 8])
        self.assertRaises(TypeError, lambda: [1,2] * [3,4])
        self.assertRaises(OverflowError, lambda: [1,2] * mylong(203958720984752098475023957209))

    def test_reverse(self):
        x = ["begin",1,2,3,4,5,6,7,8,9,0,"end"]
        del x[6:]
        x.reverse()
        self.assertTrue(x == [5, 4, 3, 2, 1, "begin"])

        x = list("iron python")
        x.reverse()
        self.assertTrue(x == ['n','o','h','t','y','p',' ','n','o','r','i'])

        # should return listreverseenumerator, not reversed
        self.assertTrue(type(reversed([2,3,4])) != reversed)

    def test_equal(self):
        self.assertEqual([2,3] == '', False)
        self.assertEqual(list.__eq__([], None), NotImplemented)

        class MyEquality(object):
            def __eq__(self, other):
                return 'abc'

        class MyOldEquality(object):
            def __eq__(self, other):
                return 'def'

        self.assertEqual([] == MyEquality(), 'abc')
        self.assertEqual([] == MyOldEquality(), 'def')

        self.assertEqual([2,3] == (2,3), False)

        class MyIterable(object):
            def __iter__(self): return MyIterable()
            def __next__(self):
                yield 'a'
                yield 'b'

        self.assertEqual(['a', 'b'] == MyIterable(), False)

    def test_self_init(self):
        a = [1, 2, 3]
        list.__init__(a, a)
        self.assertEqual(a, [])

    def test_index_removed(self):
        global hitCount
        class clears(object):
            def __eq__(self, other):
                global hitCount
                hitCount = hitCount + 1
                del a[:]
                return False

        class appends(object):
            def __eq__(self, other):
                global hitCount
                hitCount = hitCount + 1
                a.append(self)
                return False

        a = [clears(), clears(),clears(),clears(),clears()]
        hitCount = 0
        self.assertRaises(ValueError, a.index, 23)
        self.assertEqual(hitCount, 1)       # should stop after the first equality check

        a = [appends(), appends(), appends()]
        hitCount = 0
        self.assertRaises(ValueError, a.index, 2)
        self.assertEqual(hitCount, 3)       # should have only checked existing items

    @skipUnlessIronPython()
    def test_pass_pythonlist_to_clr(self):
        ##
        ## test passing pythonlist to clr where IList or ArrayList is requested
        ## also borrow this place to test passing python dict to clr where
        ##      IDictionary or Hashtable is requested
        ##

        def contains_all_1s(x):
            '''check the return value are 11111 or similar'''
            if type(x) == tuple:
                x = x[0]
            s = str(x)
            self.assertEqual(s.count("1"), len(s))

        def do_something(thetype, pl, cl, check_func):
            pt = thetype(pl)
            pt.AddRemove()

            ct = thetype(cl)
            ct.AddRemove()

            check_func()

            x = pt.Inspect()
            y = ct.Inspect()
            contains_all_1s(x)
            contains_all_1s(y)
            self.assertEqual(x, y)

            self.assertEqual(pt.Loop(), ct.Loop())
            check_func()

        self.load_iron_python_test()
        import System
        import IronPythonTest

        # test ListWrapperForIList
        pl = list(range(40))
        cl = System.Collections.Generic.List[int]()
        for x in pl: cl.Add(x)

        def check_content():
            for x, y in zip(cl, pl): self.assertEqual(x, y)

        do_something(IronPythonTest.UsePythonListAsList, pl, cl, check_content)

        # test DictWrapperForIDict
        pl = {"redmond" : 10, "seattle" : 20}
        cl = System.Collections.Generic.Dictionary[str, int]()
        for x, y in pl.items(): cl.Add(x, y)

        pll = list(pl.items())
        cll = list(cl)
        pll.sort(key=lambda x: x[0])
        cll.sort(key=lambda x: x.Key)

        def check_content():
            for x, y in zip(cll, pll):
                self.assertEqual(x.Key, y[0])
                self.assertEqual(x.Value, y[1])

        do_something(IronPythonTest.UsePythonDictAsDictionary, pl, cl, check_content)

    def test_inplace_addition(self):
        x = [2,3,4]
        x += x
        self.assertEqual(x, [2,3,4,2,3,4])

        test_cases = [ ([],     [],     []),
                    ([1],    [],     [1]),
                    ([],     [1],    [1]),
                    ([1],    [1],    [1, 1]),
                    ([1],    [2],    [1, 2]),
                    ([2],    [1],    [2, 1]),
                    ([1, 2], [],     [1, 2]),
                    ([],     [1, 2], [1, 2]),
                    ([1, 2], [3],    [1, 2, 3]),
                    ([3],    [1, 2], [3, 1, 2]),
                    ([1, 2], [3, 4], [1, 2, 3, 4]),
                    ([3, 4], [1, 2], [3, 4, 1, 2]),
                    ([None], [],     [None]),
                    ([None], [2],    [None, 2]),
                    ([""],   [],     [""]),
                    ]

        for left_operand, right_operand, result in test_cases:

            #(No access to copy.deepcopy in IP)
            #  Create new list to verify no side effects to the RHS list
            orig_right = [x for x in right_operand]

            left_operand += right_operand

            self.assertEqual(left_operand, result)

            #Side effects...
            self.assertEqual(orig_right, right_operand)

        #interesting cases
        x = [None]
        x += range(3)
        self.assertEqual(x, [None, 0, 1, 2])

        x = [None]
        x += (0, 1, 2)
        self.assertEqual(x, [None, 0, 1, 2])

        x = [None]
        x += "012"
        self.assertEqual(x, [None, "0", "1", "2"])

        x = [None]
        with self.assertRaises(TypeError):
            x += Exception() # raises an exception since Python 3.0

        #negative cases
        neg_cases = [   ([],    None),
                        ([],    1),
                        ([],    long(1)),
                        ([],    3.14),
                        ([],    object),
                        ([],    object()),
                    ]
        for left_operand, right_operand in neg_cases:
            try:
                left_operand += right_operand
                self.assertUnreachable()
            except TypeError:
                pass

    def test_indexing(self):
        l = [2,3,4]
        def set(x, i, v): x[i] = v
        self.assertRaises(TypeError, lambda : l[2.0])
        self.assertRaises(TypeError, lambda : set(l, 2.0, 1))

        class mylist(list):
            def __getitem__(self, index):
                return list.__getitem__(self, int(index))
            def __setitem__(self, index, value):
                return list.__setitem__(self, int(index), value)

        l = mylist(l)
        self.assertEqual(l[2.0], 4)
        l[2.0] = 1
        self.assertEqual(l[2], 1)

    def test_repr(self):
        class mylist(list):
            def __repr__(self): return 'abc'

        self.assertEqual(repr(mylist()), 'abc')

    def test_index_multiply(self):
        for data in ([1,2], (1,2), 'ab'):

            class M:
                def __rmul__(self, other):
                    return 1

            class Index(object):
                def __index__(self): return 2

            class OldIndex:
                def __index__(self): return 2

            self.assertEqual(data * M(), 1)
            self.assertRaises(TypeError, lambda : data.__mul__(M()))

            self.assertEqual(data * Index(), data * 2)
            self.assertEqual(data * OldIndex(), data * 2)
            self.assertEqual(data.__mul__(Index()), data * 2)
            self.assertEqual(data.__mul__(OldIndex()), data * 2)

            self.assertRaisesMessage(TypeError, "'NoneType' object cannot be interpreted as an integer", lambda : data.__mul__(None))
            self.assertRaises(TypeError, lambda : data * None)
            self.assertRaises(TypeError, lambda : None * data)

    def test_sequence_assign(self):
        tokens = [(chr(ord('a') + val), val) for val in range(0,10)]
        (first,pos),tokens = tokens[0], tokens[1:]

        self.assertEqual(first, 'a')
        self.assertEqual(pos, 0)
        self.assertEqual(tokens, [('b', 1), ('c', 2), ('d', 3), ('e', 4), ('f', 5), ('g', 6), ('h', 7), ('i', 8), ('j', 9)])

    def test_inheritance(self):
        listIter = type(iter([2,3,4]))
        reverseListIter = type(reversed([2,3,4]))

        for base in (listIter, reverseListIter):
            def subclass():
                class x(base): pass

            self.assertRaises(TypeError, subclass)


    def test_backwards_slicing_no_step(self):
        class mylist(object):
            def __getitem__(self, index):
                return 'stuff'[index]

        a = list('stuff')
        for val in (a, 'stuff', tuple('stuff'), mylist()):
            a[1:0] = val
            self.assertEqual(a, list("stuff"[:1] + "stuff" + "stuff"[1:]))
            a = list('stuff')

        for val in (a, 'stuff', tuple('stuff'), mylist()):
            a[1:0:1] = a
            self.assertEqual(a, list("stuff"[:1] + "stuff" + "stuff"[1:]))
            a = list('stuff')

    def test_cp20125(self):
        class Temp(list):
            def __init__(self, value):
                self.value = value
            def __mul__(self, other):
                return self.value * other

        t1 = Temp(3.0)
        self.assertEqual(t1 * 3.0, 9.0)


run_test(__name__)
