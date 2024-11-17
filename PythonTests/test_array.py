# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Test array support by IronPython (System.Array)
##

from iptest import IronPythonTestCase, is_cli, run_test, skipUnlessIronPython

if is_cli:
    import System

@skipUnlessIronPython()
class ArrayTest(IronPythonTestCase):

    def test_sanity(self):
        # 1-dimension array
        array1 = System.Array.CreateInstance(int, 2)
        for i in range(2): array1[i] = i * 10

        self.assertRaises(IndexError, lambda: array1[2])

        array2 = System.Array.CreateInstance(int, 4)
        for i in range(2, 6): array2[i - 2] = i * 10

        array3 = System.Array.CreateInstance(float, 3)
        array3[0] = 2.1
        array3[1] = 3.14
        array3[2] = 0.11

        ## __setitem__/__getitem__
        System.Array.__setitem__(array3, 2, 0.14)
        self.assertEqual(System.Array.__getitem__(array3, 1), 3.14)
        self.assertEqual([x for x in System.Array.__getitem__(array3, slice(2))], [2.1, 3.14])

        ## __repr__

        # 2-dimension array
        array4 = System.Array.CreateInstance(int, 2, 2)
        array4[0, 1] = 1
        array4[0, 1] = 1 << 64 >> 64
        self.assertTrue(repr(array4).startswith("<2 dimensional Array[int] at"), "bad repr for 2-dimensional array")

        # 3-dimension array
        array5 = System.Array.CreateInstance(object, 2, 2, 2)
        array5[0, 1, 1] = int
        self.assertTrue(repr(array5).startswith("<3 dimensional Array[object] at "), "bad repr for 3-dimensional array")

        ## __add__/__mul__
        for f in (
            lambda a, b : System.Array.__add__(a, b),
            lambda a, b : a + b
            ) :

            temp = System.Array.__add__(array1, array2)
            result = f(array1, array2)

            for i in range(6): self.assertEqual(i * 10, result[i])
            self.assertEqual(repr(result), "Array[int]((0, 10, 20, 30, 40, 50))")

            result = f(array1, array3)
            self.assertEqual(len(result), 2 + 3)
            self.assertEqual([x for x in result], [0, 10, 2.1, 3.14, 0.14])

            self.assertRaises(NotImplementedError, f, array1, array4)

        for f in [
            lambda a, x: System.Array.__mul__(a, x),
            lambda a, x: array1 * x
            ]:

            self.assertEqual([x for x in f(array1, 4)], [0, 10, 0, 10, 0, 10, 0, 10])
            self.assertEqual([x for x in f(array1, 5)], [0, 10, 0, 10, 0, 10, 0, 10, 0, 10])
            self.assertEqual([x for x in f(array1, 0)], [])
            self.assertEqual([x for x in f(array1, -10)], [])

    def test_invalid_index(self):
        ## types and messages follow memoryview behaviour
        rank3array = System.Array.CreateInstance(object, 2, 2, 2)

        self.assertRaisesMessage(TypeError, "expected int, got str", lambda : rank3array['s'])
        self.assertRaisesMessage(TypeError, "expected int, got str", lambda : rank3array[0, 's'])
        self.assertRaisesMessage(TypeError, "expected int, got str", lambda : rank3array[0, 0, 's'])
        self.assertRaisesMessage(TypeError, "expected int, got str", lambda : rank3array[0, 0, 0, 's'])
        self.assertRaisesMessage(TypeError, "expected int, got str", lambda : rank3array[0, 0, 0, 0, 's'])
        self.assertRaisesMessage(TypeError, "bad dimensions for array, got 1 expected 3", lambda : rank3array[0])
        self.assertRaisesMessage(TypeError, "bad dimensions for array, got 2 expected 3", lambda : rank3array[0, 0])
        self.assertRaisesMessage(TypeError, "bad dimensions for array, got 4 expected 3", lambda : rank3array[0, 0, 0, 0])
        self.assertRaisesMessage(TypeError, "bad dimensions for array, got 5 expected 3", lambda : rank3array[0, 0, 0, 0, 0])

        def f1(): rank3array['s'] = 0
        self.assertRaisesMessage(TypeError, "expected int, got str", f1)
        def f1(): rank3array[0, 's'] = 0
        self.assertRaisesMessage(TypeError, "expected int, got str", f1)
        def f1(): rank3array[0, 0, 's'] = 0
        self.assertRaisesMessage(TypeError, "expected int, got str", f1)
        def f1(): rank3array[0, 0, 0, 's'] = 0
        self.assertRaisesMessage(TypeError, "expected int, got str", f1)
        def f1(): rank3array[0, 0, 0, 0, 's'] = 0
        self.assertRaisesMessage(TypeError, "expected int, got str", f1)

        def f2(): rank3array[0] = 0
        self.assertRaisesMessage(TypeError, "bad dimensions for array, got 1 expected 3", f2)
        def f2(): rank3array[0, 0] = 0
        self.assertRaisesMessage(TypeError, "bad dimensions for array, got 2 expected 3", f2)
        def f2(): rank3array[0, 0, 0, 0] = 0
        self.assertRaisesMessage(TypeError, "bad dimensions for array, got 4 expected 3", f2)
        def f2(): rank3array[0, 0, 0, 0, 0] = 0
        self.assertRaisesMessage(TypeError, "bad dimensions for array, got 5 expected 3", f2)

        self.assertRaisesMessage(IndexError, "index out of range: 2", lambda : rank3array[2, 0, 0])
        self.assertRaisesMessage(IndexError, "index out of range: 2", lambda : rank3array[0, 2, 0])
        self.assertRaisesMessage(IndexError, "index out of range: 2", lambda : rank3array[0, 0, 2])
        self.assertRaisesMessage(IndexError, "index out of range: -3", lambda : rank3array[-3, 0, 0])
        self.assertRaisesMessage(IndexError, "index out of range: -3", lambda : rank3array[0, -3, 0])
        self.assertRaisesMessage(IndexError, "index out of range: -3", lambda : rank3array[0, 0, -3])

        def f3(): rank3array[2, 0, 0] = 0
        self.assertRaisesMessage(IndexError, "index out of range: 2", f3)
        def f3(): rank3array[0, 2, 0] = 0
        self.assertRaisesMessage(IndexError, "index out of range: 2", f3)
        def f3(): rank3array[0, 0, 2] = 0
        self.assertRaisesMessage(IndexError, "index out of range: 2", f3)

        def f4(): rank3array[-3, 0, 0] = 0
        self.assertRaisesMessage(IndexError, "index out of range: -3", f4)
        def f4(): rank3array[0, -3, 0] = 0
        self.assertRaisesMessage(IndexError, "index out of range: -3", f4)
        def f4(): rank3array[0, 0, -3] = 0
        self.assertRaisesMessage(IndexError, "index out of range: -3", f4)

    def test_slice(self):
        array1 = System.Array.CreateInstance(int, 20)
        for i in range(20): array1[i] = i * i

        # positive
        array1[::2] = [x * 2 for x in range(10)]

        for i in range(0, 20, 2):
            self.assertEqual(array1[i], i)
        for i in range(1, 20, 2):
            self.assertEqual(array1[i], i * i)

        # negative: not-same-length
        def f(): array1[::2] = [x * 2 for x in range(11)]
        self.assertRaises(ValueError, f)

    def test_creation(self):
        t = System.Array
        ti = type(System.Array.CreateInstance(int, 1))

        self.assertRaises(TypeError, t, [1, 2])
        for x in (ti([1,2]), t[int]([1, 2]), ti([1.5, 2.3])):
            self.assertEqual([i for i in x], [1, 2])
            t.Reverse(x)
            self.assertEqual([i for i in x], [2, 1])

    def test_constructor(self):
        array1 = System.Array[int](10)
        array2 = System.Array.CreateInstance(System.Int32, 10)
        self.assertEqual(len(array1), len(array2))
        for i in range(len(array1)):
            self.assertEqual(array1[i], 0)
            self.assertEqual(array1[i], array2[i])

        # 2-dimensional
        array3 = System.Array[System.Byte](3, 4)
        self.assertEqual(array3.Rank, 2)
        for x in range(array3.GetLength(0)):
            for y in range(array3.GetLength(1)):
                self.assertEqual(array3[x, y], 0)

    def test_nonzero_lowerbound(self):
        a = System.Array.CreateInstance(int, (5,), (5,))
        for i in range(5): a[i] = i

        self.assertEqual(a[:2], System.Array[int]((0,1)))
        self.assertEqual(a[2:], System.Array[int]((2,3,4)))
        self.assertEqual(a[2:4], System.Array[int]((2,3)))
        self.assertEqual(a[-1], 4)

        self.assertEqual(repr(a), 'Array[int]((0, 1, 2, 3, 4))')

        a = System.Array.CreateInstance(int, (5,), (15,))
        b = System.Array.CreateInstance(int, (5,), (20,))
        self.assertEqual(a.Length, b.Length)
        for i in range(a.Length):
            self.assertEqual(a[i], b[i])

        ## 5-dimension
        a = System.Array.CreateInstance(int, (2,2,2,2,2), (1,2,3,4,5))
        self.assertEqual(a[0,0,0,0,0], 0)

        for i in range(5):
            index = [0,0,0,0,0]
            index[i] = 1

            a[index[0], index[1], index[2], index[3], index[4]] = i
            self.assertEqual(a[index[0], index[1], index[2], index[3], index[4]], i)

        for i in range(5):
            index = [0,0,0,0,0]
            index[i] = 0

            a[index[0], index[1], index[2], index[3], index[4]] = i
            self.assertEqual(a[index[0], index[1], index[2], index[3], index[4]], i)

        def sliceArray(arr, index):
            arr[:index]

        def sliceArrayAssign(arr, index, val):
            arr[:index] = val

        self.assertRaises(NotImplementedError, sliceArray, a, 1)
        self.assertRaises(NotImplementedError, sliceArray, a, 200)
        self.assertRaises(NotImplementedError, sliceArray, a, -200)
        self.assertRaises(NotImplementedError, sliceArrayAssign, a, -200, 1)
        self.assertRaises(NotImplementedError, sliceArrayAssign, a, 1, 1)

    def test_array_type(self):

        def type_helper(array_type, instance):
            #create the array type
            AT = System.Array[array_type]

            a0 = AT([])
            a1 = AT([instance])
            a2 = AT([instance, instance])

            a_normal = System.Array.CreateInstance(array_type, 3)
            self.assertTrue(str(AT)==str(type(a_normal)))
            for i in range(3):
                a_normal[i] = instance
                self.assertTrue(str(AT)==str(type(a_normal)))

            a_multi  = System.Array.CreateInstance(array_type, 2, 3)
            self.assertTrue(str(AT)==str(type(a_multi)))
            for i in range(2):
                for j in range(3):
                    self.assertTrue(str(AT)==str(type(a_multi)))
                    a_multi[i, j]=instance

            self.assertTrue(str(AT)==str(type(a0)))
            self.assertTrue(str(AT)==str(type(a0[0:])))
            self.assertTrue(str(AT)==str(type(a0[:0])))
            self.assertTrue(str(AT)==str(type(a1)))
            self.assertTrue(str(AT)==str(type(a1[1:])))
            self.assertTrue(str(AT)==str(type(a1[:0])))
            self.assertTrue(str(AT)==str(type(a_normal)))
            self.assertTrue(str(AT)==str(type(a_normal[:0])))
            self.assertTrue(str(AT)==str(type(a_normal[3:])))
            self.assertTrue(str(AT)==str(type(a_normal[4:])))
            self.assertTrue(str(AT)==str(type(a_normal[1:])))
            self.assertTrue(str(AT)==str(type(a_normal[1:1:50])))
            self.assertTrue(str(AT)==str(type(a_multi)))
            def silly(): a_multi[0:][1:0]
            self.assertRaises(NotImplementedError, silly)
            self.assertTrue(str(AT)==str(type((a0+a1)[:0])))

        type_helper(int, 0)
        type_helper(int, 1)
        type_helper(int, 100)
        type_helper(bool, False)
        type_helper(bool, True)
        #type_helper(bool, 1)
        type_helper(int, 0)
        type_helper(int, 1)
        type_helper(int, 100)
        type_helper(float, 0.0)
        type_helper(float, 1.0)
        type_helper(float, 3.14)
        type_helper(str, "")
        type_helper(str, " ")
        type_helper(str, "abc")

    def test_tuple_indexer(self):
        array1 = System.Array.CreateInstance(int, 20, 20)
        array1[0,0] = 5
        self.assertEqual(array1[0,0], array1[(0,0)])

run_test(__name__)
