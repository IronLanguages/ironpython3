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

# Disallow assignment to empty tuple
def test_assign_to_empty():
    y = ()
    AssertError(SyntaxError, compile, "() = y", "Error", "exec")
    AssertError(SyntaxError, compile, "(), t = y, 0", "Error", "exec")
    AssertError(SyntaxError, compile, "((()))=((y))", "Error", "exec")
    del y

# Disallow unequal unpacking assignment
def test_unpack():
    tupleOfSize2 = (1, 2)

    def f1(): (a, b, c) = tupleOfSize2
    def f2(): del a

    AssertError(ValueError, f1)
    AssertError(NameError, f2)

    (a) = tupleOfSize2
    AreEqual(a, tupleOfSize2)
    del a

    (a, (b, c)) = (tupleOfSize2, tupleOfSize2)
    AreEqual(a, tupleOfSize2)
    AreEqual(b, 1)
    AreEqual(c, 2)
    del a, b, c

    ((a, b), c) = (tupleOfSize2, tupleOfSize2)
    AreEqual(a, 1)
    AreEqual(b, 2)
    AreEqual(c, tupleOfSize2)
    del a, b, c

def test_add_mul():
    AreEqual((1,2,3) + (4,5,6),  (1,2,3,4,5,6))
    AreEqual((1,2,3) * 2, (1,2,3,1,2,3))
    AreEqual(2 * (1,2,3), (1,2,3,1,2,3))

    class mylong(long): pass
    AreEqual((1,2) * mylong(2), (1, 2, 1, 2))
    AreEqual((3, 4).__mul__(mylong(2)), (3, 4, 3, 4))
    AreEqual((5, 6).__rmul__(mylong(2)), (5, 6, 5, 6))
    AreEqual(mylong(2) * (7,8) , (7, 8, 7, 8))
    
    class mylong2(long):
        def __rmul__(self, other):
            return 42
            
    AreEqual((1,2) * mylong2(2), 42) # this one uses __rmul__
    #TODO AreEqual((3, 4).__mul__(mylong2(2L)), (3, 4, 3, 4))
    AreEqual((5, 6).__rmul__(mylong2(2)), (5, 6, 5, 6))
    AreEqual(mylong2(2) * (7,8) , (7, 8, 7, 8))

    

def test_tuple_hash():
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
    
    AreEqual(test[0].hashcalls, 1)
    AreEqual(test[1].hashcalls, 1)
    AreEqual(test[2].hashcalls, 1)
    
    hashes = set()
    for i in range(1000):
        for j in range(1000):
            hashes.add(hash((i, j)))

    AreEqual(len(hashes), 1000000)

    
@skip('win32')
def test_tuple_cli_interactions():
    # verify you can call ToString on a tuple after importing clr
    import clr
    a = (0,)
    
    AreEqual(str(a), a.ToString())
    

def test_sequence_assign():
    try:
        a, b = None
        AssertUnreachable()
    except TypeError as e:
            AreEqual(e.message, "'NoneType' object is not iterable")

def test_sort():
    # very simple test for sorting lists of tuples
    s=[(3,0),(1,2),(1,1)]
    t=s[:]
    t.sort()
    AreEqual(sorted(s),t)
    AreEqual(t,[(1,1),(1,2),(3,0)])

def test_indexing():
    t = (2,3,4)
    AssertError(TypeError, lambda : t[2.0])
    
    class mytuple(tuple):
        def __getitem__(self, index):
            return tuple.__getitem__(self, int(index))

    t = mytuple(t)
    AreEqual(t[2.0], 4)

def test_tuple_slicing():
    l = [0, 1, 2, 3, 4]
    u = tuple(l)
    AreEqual(u[-100:100:-1], ())

def test_tuple_iteration():
    class T(tuple):
        def __getitem__(self):
            return None

    for x in T((1,)):
        AreEqual(x, 1)
        
def test_mul_subclass():
    class subclass(tuple):
        pass
        
    u = subclass([0,1])
    Assert(u is not u*1)


def test_compare_to_none():
    Assert((None,) > None)
    Assert(not None >= (None,))
    Assert((None,)==(None,))

    AreEqual(tuple() > None, True)
    AreEqual(tuple() < None, False)
    AreEqual(tuple() >= None, True)
    AreEqual(tuple() <= None, False)
    Assert(    tuple() != None)
    Assert(not tuple() == None)

    AreEqual(None < tuple(), True)
    AreEqual(None > tuple(), False)
    AreEqual(None <= tuple(), True)
    AreEqual(None >= tuple(), False)
    Assert(    None != tuple())
    Assert(not None == tuple())
    

def test_wacky_contains():
    for retval in [None, 0, [], (), 0.0, 0, {}]:
        class x(tuple):
            def  __contains__(self, other):
                return retval
            
        AreEqual('abc' in x(), False)

    class x2(tuple):
        def __contans__(self, other):
            pass
    AreEqual('abc' in x2(), False)

def test_tuple_equality():
    class x(object):
        def __eq__(self, other): return False
        def __ne__(self, other): return True
    
    a = x()
    AreEqual((a, ), (a, ))
    
def test_tuple_reuse():
    t = (2,4,6)
    AreEqual(id(t), id(tuple(t)))
    AreEqual(id(t), id(t[:]))
    AreEqual(id(t), id(t[0:]))
    AreEqual(id(t), id(t[0::1]))

def test_index_error():
    x = ()
    def delindex(): del x[42]
    def setindex(): x[42] = 42
    
    AssertErrorWithMessage(TypeError, "'tuple' object doesn't support item deletion", delindex)
    AssertErrorWithMessage(TypeError, "'tuple' object does not support item assignment", setindex)

    
run_test(__name__)

