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

class It:
    x = 0
    a = ()
    def __init__(self, a):
        self.x = 0
        self.a = a
    def next(self):
        if self.x <= 9:
            self.x = self.x+1
            return self.a[self.x-1]
        else:
            raise StopIteration
    def __iter__(self):
        return self


class Iterator:
    x = 0
    a = (1,2,3,4,5,6,7,8,9,0)
    def __iter__(self):
        return It(self.a)

class Indexer:
    a = (1,2,3,4,5,6,7,8,9,0)
    def __getitem__(self, i):
        if i < len(self.a):
            return self.a[i]
        else:
            raise IndexError

i = Iterator()
for j in i:
    Assert(j in i)

Assert(1 in i)
Assert(2 in i)
Assert(not (10 in i))

i = Indexer()
for j in i:
    Assert(j in i)

Assert(1 in i)
Assert(2 in i)
Assert(not (10 in i))


# Testing the iter(o,s) function

class Iter:
    x = [1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20]
    index = -1

it = Iter()

def f():
    it.index += 1
    return it.x[it.index]


y = []

for i in iter(f, 14):
    y.append(i)

Assert(y == [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13])

y = ['1']
y += Iterator()
Assert(y == ['1', 1, 2, 3, 4, 5, 6, 7, 8, 9, 0])
y = ['1']
y += Indexer()
Assert(y == ['1', 1, 2, 3, 4, 5, 6, 7, 8, 9, 0])

AssertErrorWithMessages(TypeError, "iter() takes at least 1 argument (0 given)",
                                   "iter expected at least 1 arguments, got 0", iter)


def test_itertools_same_value():
    from itertools import izip
    x = iter(range(4))
    AreEqual([(i,j) for i,j in izip(x,x)], [(0, 1), (2, 3)])

def test_itertools_islice_end():
    """islice shouldn't consume values after the limit specified by step"""
    from itertools import izip, islice
    
    # create a zipped iterator w/ odd number of values...
    it = izip([2,3,4], [4,5,6])
    
    # slice in 2, turn that into a list...
    list(islice(it, 2))
    
    # we should still have the last value still present
    for x in it:
        AreEqual(x, (4,6))

@skip("silverlight")
def test_iterator_for():
    """test various iterable objects with multiple incomplete iterations"""
    def generator():
        yield 0
        yield 1
    
    from cStringIO import StringIO
    strO = StringIO()
    strO.write('abc\n')
    strO.write('def')
    strI = StringIO('abc\ndef')
    
    import sys
    fi = sys.float_info
    
    d = {2:3, 3:4}        
    l = [2, 3]
    s = set([2, 3, 4])
    
    if not is_silverlight:
        f = file('test_file.txt', 'w+')
        f.write('abc\n')
        f.write('def')
        f.close()
    
        f = file('test_file.txt')
        
        import nt    
        stat = nt.stat(__file__)

    class x(object):
        abc = 2
        bcd = 3
        
    dictproxy = x.__dict__
    dictlist = list(x.__dict__)
    
    ba = bytearray(b'abc')
    
    try:
                      # iterator,       first Value,    second Value
        iterators = [
                     # objects which when enumerated multiple times continue
                     (generator(),      0,              1),
                     (strI,             'abc\n',        'def'),
                     (strO,             'abc\n',        'def'),
                     
                     # objects which when enumerated multiple times reset
                     (xrange(10),       0,              0), 
                     ([0, 1],           0,              0),
                     ((0, 1),           0,              0),
                     (fi,               fi[0],          fi[0]),
                     (b'abc',           b'a',           b'a'),
                     (ba,               ord(b'a'),      ord(b'a')),
                     (u'abc',           u'a',           u'a'),
                     (d,                list(d)[0],    list(d)[0]),
                     (l,                l[0],          l[0]),
                     (s,                list(s)[0],    list(s)[0]),
                     (dictproxy,        dictlist[0],   dictlist[0]),
                    ]
        
        if not is_silverlight:
            iterators.append((f,                'abc\n',        'def'))
            iterators.append((stat,             stat[0],        stat[0]))

        for iterator, res0, res1 in iterators:
            for x in iterator: 
                AreEqual(x, res0)
                break
            for x in iterator:
                AreEqual(x, res1)
                break
    finally:
        f.close()
        nt.unlink('test_file.txt')


def test_iterator_closed_file():
    cf = file(__file__)
    cf.close()
    
    def f():
        for x in cf: pass
        
    AssertError(ValueError, f)


def test_no_return_self_in_iter():
    class A(object):
        def __iter__(cls):
            return 1

        def next(cls):
            return 2
    
    a = A()
    AreEqual(next(a), 2)

def test_no_iter():
    class A(object):
        def next(cls):
            return 2    
    a = A()
    AreEqual(next(a), 2)

def test_with_iter():
    class A(object):
        def __iter__(cls):
            return cls
        def next(self):
            return 2
             
    a = A()
    AreEqual(next(a), 2)

def test_with_iter_next_in_init():
    class A(object):
        def __init__(cls):
            AreEqual(next(cls), 2)
            AreEqual(cls.next(), 2)
        def __iter__(cls):
            return cls
        def next(cls):
            return 2

    a = A()
    AreEqual(next(a), 2)

def test_interacting_iterators():
    """This test is similar to how Jinga2 fails."""
    class A(object):
        def __iter__(cls):
            return cls
        def next(self):
            return 3
            
    class B(object):
        def __iter__(cls):
            return A()
        def next(self):
            return 2
            
    b = B()
    AreEqual(next(b), 2)

def test_call_to_iter_or_next():
    class A(object):
        def __iter__(cls):
            Assert(False, "__iter__ should not be called.")
            return cls
        def next(self):
            return 2

    a = A()
    AreEqual(next(a), 2)


run_test(__name__)
