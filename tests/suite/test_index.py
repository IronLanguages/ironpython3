# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

from iptest import IronPythonTestCase, is_cli, run_test, skipUnlessIronPython

class IndexTest(IronPythonTestCase):

    def setUp(self):
        super(IndexTest, self).setUp()
        self.load_iron_python_test()


    @skipUnlessIronPython()
    def test_string(self):
        import clr
        import System

        x = System.Array.CreateInstance(System.String, 2)
        x[0]="Hello"
        x[1]="Python"
        self.assertTrue(x[0] == "Hello")
        self.assertTrue(x[1] == "Python")

    @skipUnlessIronPython()
    def test_hashtable(self):
        import clr
        import System
        hashtables = [System.Collections.Generic.Dictionary[object, object]()]
        hashtables.append(System.Collections.Hashtable())

        for x in hashtables:
            x["Hi"] = "Hello"
            x[1] = "Python"
            x[10,] = "Tuple Int"
            x["String",] = "Tuple String"
            x[2.4,] = "Tuple Double"

            self.assertTrue(x["Hi"] == "Hello")
            self.assertTrue(x[1] == "Python")
            self.assertTrue(x[(10,)] == "Tuple Int")
            self.assertTrue(x[("String",)] == "Tuple String")
            self.assertTrue(x[(2.4,)] == "Tuple Double")

            success=False
            try:
                x[1,2] = 10
            except TypeError as e:
                success=True
            self.assertTrue(success)

            x[(1,2)] = "Tuple key in hashtable"
            self.assertTrue(x[1,2,] == "Tuple key in hashtable")

    @skipUnlessIronPython()
    def test_multidim_array(self):
        import clr
        import System

        md = System.Array.CreateInstance(System.Int32, 2, 2, 2)

        for i in range(2):
            for j in range(2):
                for k in range(2):
                    md[i,j,k] = i+j+k

        for i in range(2):
            for j in range(2):
                for k in range(2):
                    self.assertTrue(md[i,j,k] == i+j+k)

    @skipUnlessIronPython()
    def test_array(self):
        import clr
        import System

        # verify that slicing an array returns an array of the proper type
        from System import Array
        data = Array[int]( (2,3,4,5,6) )

        self.assertEqual(type(data[:0]), Array[int])
        self.assertEqual(type(data[0:3:2]), Array[int])

    def test_dict(self):
        d = dict()
        d[1,2,3,4,5] = 12345
        self.assertTrue(d[1,2,3,4,5] == d[(1,2,3,4,5)])
        self.assertTrue(d[1,2,3,4,5] == 12345)
        self.assertTrue(d[(1,2,3,4,5)] == 12345)

        d = {None:23}
        del d[None]
        self.assertEqual(d, {})

    @skipUnlessIronPython()
    def test_custom_indexable(self):
        from IronPythonTest import Indexable
        i = Indexable()

        i[10] = "Hello Integer"
        i["String"] = "Hello String"
        i[2.4] = "Hello Double"

        self.assertTrue(i[10] == "Hello Integer")
        self.assertTrue(i["String"] == "Hello String")
        self.assertTrue(i[2.4] == "Hello Double")

        indexes = (10, "String", 2.4)
        for a in indexes:
            for b in indexes:
                complicated = "Complicated " + str(a) + " " + str(b)
                i[a,b] = complicated
                self.assertTrue(i[a,b] == complicated)

    @skipUnlessIronPython()
    def test_property_access(self):
        from IronPythonTest import PropertyAccessClass
        x = PropertyAccessClass()
        for i in range(3):
            self.assertTrue(x[i] == i)
            for j in range(3):
                x[i, j] = i + j
                self.assertTrue(x[i, j] == i + j)
                for k in range(3):
                    x[i, j, k] = i + j + k
                    self.assertTrue(x[i, j, k] == i + j + k)

    @skipUnlessIronPython()
    def test_multiple_indexes(self):
        from IronPythonTest import MultipleIndexes
        x = MultipleIndexes()

        def get_value(*i):
            value = ""
            append = False
            for v in i:
                if append:
                    value = value + " : "
                value = value + str(v)
                append = True
            return value

        def get_tuple_value(*i):
            return get_value("Indexing as tuple", *i)

        def get_none(*i):
            return None

        def verify_values(mi, gv, gtv):
            for i in i_idx:
                self.assertTrue(x[i] == gv(i))
                self.assertTrue(x[i,] == gtv(i))
                for j in j_idx:
                    self.assertTrue(x[i,j] == gv(i,j))
                    self.assertTrue(x[i,j,] == gtv(i,j))
                    for k in k_idx:
                        self.assertTrue(x[i,j,k] == gv(i,j,k))
                        self.assertTrue(x[i,j,k,] == gtv(i,j,k))
                        for l in l_idx:
                            self.assertTrue(x[i,j,k,l] == gv(i,j,k,l))
                            self.assertTrue(x[i,j,k,l,] == gtv(i,j,k,l))
                            for m in m_idx:
                                self.assertTrue(x[i,j,k,l,m] == gv(i,j,k,l,m))
                                self.assertTrue(x[i,j,k,l,m,] == gtv(i,j,k,l,m))

        i_idx = ("Hi", 2.5, 34)
        j_idx = (0, "*", "@")
        k_idx = list(range(3))
        l_idx = ("Sun", "Moon", "Star")
        m_idx = ((9,8,7), (6,5,4,3,2), (4,))

        for i in i_idx:
            x[i] = get_value(i)
            for j in j_idx:
                x[i,j] = get_value(i,j)
                for k in k_idx:
                    x[i,j,k] = get_value(i,j,k)
                    for l in l_idx:
                        x[i,j,k,l] = get_value(i,j,k,l)
                        for m in m_idx:
                            x[i,j,k,l,m] = get_value(i,j,k,l,m)

        verify_values(x, get_value, get_none)

        for i in i_idx:
            x[i,] = get_tuple_value(i)
            for j in j_idx:
                x[i,j,] = get_tuple_value(i,j)
                for k in k_idx:
                    x[i,j,k,] = get_tuple_value(i,j,k)
                    for l in l_idx:
                        x[i,j,k,l,] = get_tuple_value(i,j,k,l)
                        for m in m_idx:
                            x[i,j,k,l,m,] = get_tuple_value(i,j,k,l,m)

        verify_values(x, get_value, get_tuple_value)

    @skipUnlessIronPython()
    def test_indexable_list(self):
        from IronPythonTest import IndexableList
        a = IndexableList()
        for i in range(5):
            result = a.Add(i)

        for i in range(5):
            self.assertEqual(a[str(i)], i)

    @skipUnlessIronPython()
    def test_generic_function(self):
        from IronPythonTest import GenMeth
        # all should succeed at indexing
        x = GenMeth.StaticMeth[int, int]
        x = GenMeth.StaticMeth[int]
        x = GenMeth.StaticMeth[(int, int)]
        x = GenMeth.StaticMeth[(int,)]

    def test_getorsetitem_override(self):
        class old_base: pass

        for base in [object, list, dict, int, str, tuple, float, complex, old_base]:
            class foo(base):
                def __getitem__(self, index):
                    return index
                def __setitem__(self, index, value):
                    self.res = (index, value)

            a = foo()
            self.assertEqual(a[1], 1)
            self.assertEqual(a[1,2], (1,2))
            self.assertEqual(a[1,2,3], (1,2,3))
            self.assertEqual(a[(1, 2)], (1, 2))
            self.assertEqual(a[(5,)], (5,))
            self.assertEqual(a[6,], (6,))

            a[1] = 23
            self.assertEqual(a.res, (1,23))
            a[1,2] = 23
            self.assertEqual(a.res, ((1,2),23))
            a[1,2,3] = 23
            self.assertEqual(a.res, ((1,2,3),23))

            a[(1, 2)] = "B"; self.assertEqual(a.res, ((1, 2), "B"))
            a[(5,)] = "D"; self.assertEqual(a.res, ((5,), "D"))
            a[6,] = "E"; self.assertEqual(a.res, ((6,), "E"))

    def test_getorsetitem_super(self):
        tests = [  # base type, constructor arg, result of index 0
        (list,(1,2,3,4,5), 1),
            (dict,{0:2, 3:4, 5:6, 7:8}, 2),
            (str,'abcde', 'a'),
            (tuple, (1,2,3,4,5), 1),]

        for testInfo in tests:
            base = testInfo[0]
            arg  = testInfo[1]
            zero = testInfo[2]

            class foo(base):
                def __getitem__(self, index):
                    if isinstance(index, tuple):
                        return base.__getitem__(self, index[0])
                    return base.__getitem__(self, index)
                def __setitem__(self, index, value):
                    if isinstance(index, tuple):
                        base.__setitem__(self, index[0], value)
                    else:
                        base.__setitem__(self, index, value)

            a = foo(arg)
            self.assertEqual(a[0], zero)
            a = foo(arg)
            self.assertEqual(a[0,1], zero)
            a = foo(arg)
            self.assertEqual(a[0,1,2], zero)
            a = foo(arg)
            self.assertEqual(a[(0,)], zero)
            a = foo(arg)
            self.assertEqual(a[(0,1)], zero)
            a = foo(arg)
            self.assertEqual(a[(0,1,2)], zero)

            if hasattr(base, '__setitem__'):
                a[0] = 'x'
                self.assertEqual(a[0], 'x')
                a[0,1] = 'y'
                self.assertEqual(a[0,1], 'y')
                a[0,1,2] = 'z'
                self.assertEqual(a[0,1,2], 'z')
                a[(0,)] = 'x'
                self.assertEqual(a[(0,)], 'x')
                a[(0,1)] = 'y'
                self.assertEqual(a[(0,1)], 'y')
                a[(0,1,2)] = 'z'
                self.assertEqual(a[(0,1,2)], 'z')


    def test_getorsetitem_slice(self):
        tests = [  # base type, constructor arg, result of index 0
        (list,(1,2,3,4,5), 1, lambda x: [x]),
            (str,'abcde', 'a', lambda x: x),
            (tuple, (1,2,3,4,5), 1, lambda x: (x,)),]

        for testInfo in tests:
            base = testInfo[0]
            arg  = testInfo[1]
            zero = testInfo[2]
            resL = testInfo[3]

            class foo(base):
                def __getitem__(self, index):
                    if isinstance(index, tuple):
                        return base.__getitem__(self, index[0])
                    return base.__getitem__(self, index)
                def __setitem__(self, index, value):
                    if isinstance(index, tuple):
                        base.__setitem__(self, index[0], value)
                    else:
                        base.__setitem__(self, index, value)

            a = foo(arg)
            self.assertEqual(a[0:1], resL(zero))
            a = foo(arg)
            self.assertEqual(a[0:1, 1:2], resL(zero))
            a = foo(arg)
            self.assertEqual(a[0:1, 1:2, 2:3], resL(zero))
            a = foo(arg)
            self.assertEqual(a[(slice(0,1),)], resL(zero))
            a = foo(arg)
            self.assertEqual(a[(slice(0,1),slice(1,2))], resL(zero))
            a = foo(arg)
            self.assertEqual(a[(slice(0,1),slice(1,2),slice(2,3))], resL(zero))

            if hasattr(base, '__setitem__'):
                a[0:1] = 'x'
                self.assertEqual(a[0:1], ['x'])
                a[0:1,1:2] = 'y'
                self.assertEqual(a[0:1,1:2], ['y'])
                a[0:1,1:2,2:3] = 'z'
                self.assertEqual(a[0:1,1:2,2:3], ['z'])
                a[(slice(0,1),)] = 'x'
                self.assertEqual(a[(slice(0,1),)], ['x'])
                a[(slice(0,1),slice(1,2))] = 'y'
                self.assertEqual(a[(slice(0,1),slice(1,2))], ['y'])
                a[(slice(0,1),slice(1,2),slice(2,3))] = 'z'
                self.assertEqual(a[(slice(0,1),slice(1,2),slice(2,3))], ['z'])

    def test_index_by_tuple(self):
        class indexable:
            def __getitem__(self, index):
                return index
            def __setitem__(self, index, value):
                self.index = index
                self.value = value

        i = indexable()
        self.assertEqual(i["Hi"], "Hi")
        self.assertEqual(i[(1, 2)], (1, 2))
        self.assertEqual(i[3, 4], (3, 4))
        self.assertEqual(i[(5,)], (5,))
        self.assertEqual(i[6,], (6,))

        i["Hi"] = "A"; self.assertEqual(i.index, "Hi"); self.assertEqual(i.value, "A")
        i[(1, 2)] = "B"; self.assertEqual(i.index, (1, 2)); self.assertEqual(i.value, "B")
        i[3, 4] = "C"; self.assertEqual(i.index, (3, 4)); self.assertEqual(i.value, "C")
        i[(5,)] = "D"; self.assertEqual(i.index, (5,)); self.assertEqual(i.value, "D")
        i[6,] = "E"; self.assertEqual(i.index, (6,)); self.assertEqual(i.value, "E")


    def test_assignment_order(self):
        # declare types to log the execution ordering
        class Q:
            def __init__(self):
                self.log = ""
            # we're just doing assignment, so don't define a __getitem__
            def __setitem__(self, idx, val):
                self.log += "(idx=%s, val=%s)" % ( idx, val)
        c=Q()
        def f():
            c[0]=1 # do a side effect to log execution order of f()
            return 'x'
        # Now execute the interesting statement. This has side-effects in c.log to log execution order.
        c[5]=c[2]=f()
        # now check that order is as expected
        # - assignments should occur from left to right
        # - rhs expression is evalled first, and should only be executed once,
        self.assertEqual(c.log, "(idx=0, val=1)(idx=5, val=x)(idx=2, val=x)")

    def test_custom_indexer(self):
        class cust_index(object):
            def __init__(self, index):
                self.index = index
            def __index__(self):
                return self.index

        for sliceable in [x(list(range(5))) for x in (list, tuple)]:
            self.assertEqual(sliceable[cust_index(0)], 0)
            self.assertEqual(sliceable[cust_index(0)], 0)
            self.assertEqual(list(sliceable[cust_index(0) : cust_index(3)]), [0, 1, 2])
            self.assertEqual(list(sliceable[cust_index(0) : cust_index(3)]), [0, 1, 2])

        # dictionary indexing shouldn't be affected
        x = cust_index(42)
        d = {x:3}
        self.assertEqual(d[x], 3)
        for key in list(d.keys()):
            self.assertEqual(key, x)

        if is_cli:
            from System.Collections.Generic import List
            cli_list = List[int](range(5))
            self.assertEqual(list(cli_list), list(range(5)))
            self.assertEqual(cli_list[cust_index(0)], 0)
            self.assertEqual(list(cli_list[cust_index(0) : cust_index(3)]), list(range(3)))
            self.assertEqual(type(cli_list[cust_index(0) : cust_index(3)]), List[int])

    @skipUnlessIronPython()
    def test_csharp_enumeration(self):
        from IronPythonTest import CSharpEnumerable
        a = CSharpEnumerable()
        for method in ('GetEnumerableOfInt', 'GetEnumerableOfObject', 'GetEnumerable',
                'GetEnumeratorOfInt', 'GetEnumeratorOfObject', 'GetEnumerator'):
            sum = 0
            for i in getattr(a, method)():
                sum = sum + i
            self.assertEqual(sum, 6)

    def test_error(self):
        l = []
        self.assertRaisesPartialMessage(TypeError, "'builtin_function_or_method' object is not subscriptable", lambda: l.append[float](1.0))
        self.assertRaisesPartialMessage(TypeError, "'int' object is not subscriptable", lambda: 1[2])

    def test_cp19350_index_restrictions(self):
        global keyValue
        class X(object):
            def __setitem__(self, key, value):
                global keyValue
                keyValue = key

        def f(a, b):
            X()[a, b] = object()

        f(1, 2)
        self.assertEqual(keyValue, (1, 2))
        f('one', 'two')
        self.assertEqual(keyValue, ('one', 'two'))

run_test(__name__)
