# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import unittest

from iptest import IronPythonTestCase, long, run_test, skipUnlessIronPython

class TupleTest(IronPythonTestCase):

    def test_assign_to_empty(self):
        """Disallow assignment to empty tuple"""
        y = ()
        self.assertRaises(SyntaxError, compile, "() = y", "Error", "exec")
        self.assertRaises(SyntaxError, compile, "(), t = y, 0", "Error", "exec")
        self.assertRaises(SyntaxError, compile, "((()))=((y))", "Error", "exec")
        del y

    def test_unpack(self):
        """Disallow unequal unpacking assignment"""
        tupleOfSize2 = (1, 2)

        def f1(): (a, b, c) = tupleOfSize2
        def f2(): del a

        self.assertRaises(ValueError, f1)
        self.assertRaises(NameError, f2)

        (a) = tupleOfSize2
        self.assertEqual(a, tupleOfSize2)
        del a

        (a, (b, c)) = (tupleOfSize2, tupleOfSize2)
        self.assertEqual(a, tupleOfSize2)
        self.assertEqual(b, 1)
        self.assertEqual(c, 2)
        del a, b, c

        ((a, b), c) = (tupleOfSize2, tupleOfSize2)
        self.assertEqual(a, 1)
        self.assertEqual(b, 2)
        self.assertEqual(c, tupleOfSize2)
        del a, b, c

    def test_add_mul(self):
        self.assertEqual((1,2,3) + (4,5,6),  (1,2,3,4,5,6))
        self.assertEqual((1,2,3) * 2, (1,2,3,1,2,3))
        self.assertEqual(2 * (1,2,3), (1,2,3,1,2,3))

        class mylong(long): pass
        self.assertEqual((1,2) * mylong(2), (1, 2, 1, 2))
        self.assertEqual((3, 4).__mul__(mylong(2)), (3, 4, 3, 4))
        self.assertEqual((5, 6).__rmul__(mylong(2)), (5, 6, 5, 6))
        self.assertEqual(mylong(2) * (7,8) , (7, 8, 7, 8))
        
        class mylong2(long):
            def __rmul__(self, other):
                return 42
                
        self.assertEqual((1,2) * mylong2(2), 42) # this one uses __rmul__
        self.assertEqual((3, 4).__mul__(mylong2(2)), (3, 4, 3, 4))
        self.assertEqual((5, 6).__rmul__(mylong2(2)), (5, 6, 5, 6))
        self.assertEqual(mylong2(2) * (7,8) , (7, 8, 7, 8))

    def test_tuple_custom_hash(self):
        class myhashable(object):
            def __init__(self):
                self.hashcalls = 0
            def __hash__(self):
                self.hashcalls += 1
                return 42
            def __eq__(self, other):
                return type(self) == type(other)
        
        
        test = (myhashable(), myhashable(), myhashable())
        
        hash(test)
        
        self.assertEqual(test[0].hashcalls, 1)
        self.assertEqual(test[1].hashcalls, 1)
        self.assertEqual(test[2].hashcalls, 1)

    def test_tuple_hash_uniqueness(self):
        hashes = set()
        for i in range(1000):
            for j in range(1000):
                hashes.add(hash((i, j)))

        self.assertEqual(len(hashes), 1000000)

    @skipUnlessIronPython()
    def test_tuple_hash_none(self):
        import clr # Make sure .GetHashCode() is available
        example = (1, None)
        expected = 1625286227
        
        self.assertEqual(hash(example), expected)
        self.assertEqual(hash(example), example.GetHashCode())

    @skipUnlessIronPython()
    def test_tuple_cli_interactions(self):
        # verify you can call ToString on a tuple after importing clr
        import clr
        a = (0,)
        
        self.assertEqual(str(a), a.ToString())
    

    def test_sequence_assign(self):
        try:
            a, b = None
            self.assertUnreachable()
        except TypeError as e:
            self.assertEqual(e.args[0], "'NoneType' object is not iterable")

    def test_sort(self):
        # very simple test for sorting lists of tuples
        s=[(3,0),(1,2),(1,1)]
        t=s[:]
        t.sort()
        self.assertEqual(sorted(s),t)
        self.assertEqual(t,[(1,1),(1,2),(3,0)])

    def test_indexing(self):
        t = (2,3,4)
        self.assertRaises(TypeError, lambda : t[2.0])
        
        class mytuple(tuple):
            def __getitem__(self, index):
                return tuple.__getitem__(self, int(index))

        t = mytuple(t)
        self.assertEqual(t[2.0], 4)

    def test_tuple_slicing(self):
        l = [0, 1, 2, 3, 4]
        u = tuple(l)
        self.assertEqual(u[-100:100:-1], ())

    def test_tuple_iteration(self):
        class T(tuple):
            def __getitem__(self):
                return None

        for x in T((1,)):
            self.assertEqual(x, 1)
        
    def test_mul_subclass(self):
        class subclass(tuple):
            pass
            
        u = subclass([0,1])
        self.assertTrue(u is not u*1)


    def test_compare_to_none(self):
        self.assertRaises(TypeError, lambda: (None,) > None)
        self.assertRaises(TypeError, lambda: not None >= (None,))
        self.assertTrue((None,)==(None,))

        self.assertRaises(TypeError, lambda: tuple() > None)
        self.assertRaises(TypeError, lambda: tuple() < None)
        self.assertRaises(TypeError, lambda: tuple() >= None)
        self.assertRaises(TypeError, lambda: tuple() <= None)
        self.assertTrue(    tuple() != None)
        self.assertTrue(not tuple() == None)

        self.assertRaises(TypeError, lambda: None < tuple())
        self.assertRaises(TypeError, lambda: None > tuple())
        self.assertRaises(TypeError, lambda: None <= tuple())
        self.assertRaises(TypeError, lambda: None >= tuple())
        self.assertTrue(    None != tuple())
        self.assertTrue(not None == tuple())


    def test_wacky_contains(self):
        for retval in [None, 0, [], (), 0.0, long(0), {}]:
            class x(tuple):
                def  __contains__(self, other):
                    return retval
                
            self.assertEqual('abc' in x(), False)

        class x2(tuple):
            def __contans__(self, other):
                pass
        self.assertEqual('abc' in x2(), False)

    def test_tuple_equality(self):
        class x(object):
            def __eq__(self, other): return False
            def __ne__(self, other): return True
        
        a = x()
        self.assertEqual((a, ), (a, ))
    
    def test_tuple_reuse(self):
        t = (2,4,6)
        self.assertEqual(id(t), id(tuple(t)))
        self.assertEqual(id(t), id(t[:]))
        self.assertEqual(id(t), id(t[0:]))
        self.assertEqual(id(t), id(t[0::1]))

    def test_index_error(self):
        x = ()
        def delindex(): del x[42]
        def setindex(): x[42] = 42
        
        self.assertRaisesMessage(TypeError, "'tuple' object doesn't support item deletion", delindex)
        self.assertRaisesMessage(TypeError, "'tuple' object does not support item assignment", setindex)

    
run_test(__name__)