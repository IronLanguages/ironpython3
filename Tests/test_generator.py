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

def ifilter(iterable):
    def predicate(x):
        return x % 3
    for x in iterable:
        if predicate(x):
            yield x

def ifilterfalse(iterable):
    def predicate(x):
        return x % 3
    for x in iterable:
        if not predicate(x):
            yield x

def test_simple_generators():
    ll = [1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20]
    x = ifilter(ll)
    l = []
    for i in x: l.append(i)
    x = ifilterfalse(ll)
    Assert(l == [1,2,4,5,7,8,10,11,13,14,16,17,19,20])
    l = []
    for i in x: l.append(i)
    Assert(l == [3,6,9,12,15,18])

#  Generator expressions

def test_generator_expressions():
    AreEqual(sum(i+i for i in range(100) if i < 50), 2450)
    AreEqual(list((i,j) for i in xrange(2) for j in xrange(3)), [(0, 0), (0, 1), (0, 2), (1, 0), (1, 1), (1, 2)])
    AreEqual(list((i,j) for i in xrange(2) for j in xrange(i+1)), [(0, 0), (1, 0), (1, 1)])
    AreEqual([x for x, in [(1,)]], [1])
    
    i = 10
    AreEqual(sum(i+i for i in range(1000) if i < 50), 2450)
    AreEqual(i, 10)
    
    g = (i+i for i in range(10))
    AreEqual(list(g), [0, 2, 4, 6, 8, 10, 12, 14, 16, 18])
    
    g = (i+i for i in range(3))
    AreEqual(g.next(), 0)
    AreEqual(g.next(), 2)
    AreEqual(g.next(), 4)
    AssertError(StopIteration, g.next)
    AssertError(StopIteration, g.next)
    AssertError(StopIteration, g.next)
    AreEqual(list(g), [])
    
    def f(n):
        return (i+i for i in range(n) if i < 50)
    
    AreEqual(sum(f(100)), 2450)
    AreEqual(list(f(10)), [0, 2, 4, 6, 8, 10, 12, 14, 16, 18])
    AreEqual(sum(f(10)), 90)
    
    def f(n):
        return ((i,j) for i in xrange(n) for j in xrange(i))
    
    AreEqual(list(f(3)), [(1, 0), (2, 0), (2, 1)])


# Nested generators
def test_nested_generators():
    def outergen():
        def innergen():
            yield i
            for j in range(i):
                yield j
        for i in range(10):
            yield (i, innergen())
    
    for a,b in outergen():
        AreEqual(a, b.next())
        AreEqual(range(a), list(b))
    
    
    def f():
        yield "Import inside generator"
    
    AreEqual(f().next(), "Import inside generator")
    
    
    def xgen():
        try:
            yield 1
        except:
            pass
        else:
            yield 2
    
    AreEqual([ i for i in xgen()], [1,2])
    
    
    def xgen2(x):
        yield "first"
        try:
            yield "try"
            if x > 3:
                raise AssertionError("x > 10")
            100 / x
            yield "try 2"
        except AssertionError:
            yield "error"
            yield "error 2"
        except:
            yield "exc"
            yield "exc 2"
        else:
            yield "else"
            yield "else 2"
        yield "last"
    
    def testxgen2(x, r):
        AreEqual(list(xgen2(x)), r)
    
    testxgen2(0, ['first', 'try', 'exc', 'exc 2', 'last'])
    testxgen2(1, ['first', 'try', 'try 2', 'else', 'else 2', 'last'])
    testxgen2(2, ['first', 'try', 'try 2', 'else', 'else 2', 'last'])
    testxgen2(3, ['first', 'try', 'try 2', 'else', 'else 2', 'last'])
    testxgen2(4, ['first', 'try', 'error', 'error 2', 'last'])
    
    
    def xgen3():
        yield "first"
        try:
            pass
        finally:
            yield "fin"
            yield "fin 2"
        yield "last"
    
    AreEqual(list(xgen3()), ['first', 'fin', 'fin 2', 'last'])
    
    AreEqual(type(xgen), type(xgen2))
    AreEqual(type(ifilter), type(xgen3))

def test_more_nested_generators():
    def f():
        def g():
            def xx():
                return x
    
            def yy():
                return y
    
            def zz():
                return z
    
            def ii():
                return i
    
    
            yield xx()
            yield yy()
            yield zz()
            for i in [11, 12, 13]:
                yield ii()
        x = 1
        y = 2
        z = 3
    
        return g()
    
    AreEqual(list(f()), [1, 2, 3, 11, 12, 13])

def test_generator_finally():
    def yield_in_finally_w_exception():
        try:
            1/0
        finally:
            yield 1
            yield 2
            yield 3
    
    n = yield_in_finally_w_exception()
    AreEqual(n.next(), 1)
    AreEqual(n.next(), 2)
    AreEqual(n.next(), 3)
    AssertError(ZeroDivisionError, n.next)
    
    def yield_in_finally_w_exception_2():
        try:
            1/0
        finally:
            yield 1
            yield 2
            raise AssertionError()
            yield 3
    
    n = yield_in_finally_w_exception_2()
    AreEqual(n.next(), 1)
    AreEqual(n.next(), 2)
    AssertError(AssertionError, n.next)
    
    def test_generator_exp():
        l = ((1,2),(3, 4))
        return (x for x,y in l)
    
    AreEqual(list(test_generator_exp()), [1, 3])


def test_generator_exceptions():
    def nested_yield_1():
        try:
            yield 1
            try:
                yield 2
                yield 3
            except:
                raise AssertionError()
            else:
                yield 4
                yield 5
            finally:
                yield 6
                yield 7
            1/0
        except:
            yield 8
            yield 9
            try:
                yield 10
                yield 11
            except:
                raise AssertionError()
            else:
                yield 12
                yield 13
            finally:
                yield 14
                yield 15
            yield 32
        else:
            raise AssertionError()
        finally:
            yield 30
            try:
                yield 23
                yield 24
            except:
                raise AssertionError()
            else:
                yield 25
                yield 26
            finally:
                yield 27
                yield 28
            yield 29
        yield 33
    
    AreEqual(list(nested_yield_1()), [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 32, 30, 23, 24, 25, 26, 27, 28, 29, 33])
    
    def nested_yield_2():
        try:
            pass
        except:
            raise AssertionError()
        else:
            yield 1
            try:
                yield 2
                yield 3
            except:
                raise AssertionError()
            else:
                yield 4
                yield 5
            finally:
                yield 6
                yield 7
        finally:
            yield 8
            yield 9
        yield 10
        
    AreEqual(list(nested_yield_2()), [1, 2, 3, 4, 5, 6, 7, 8, 9, 10])
    
    def nested_yield_3():
        yield 1
        try:
            yield 2
            try:
                yield 3
                try:
                    yield 4
                    try:
                        yield 5
                    except:
                        pass
                    yield 6
                except:
                    pass
                yield 7
            except:
                pass
            yield 8
        except:
            pass
        yield 9
    
    AreEqual(list(nested_yield_3()), [1, 2, 3, 4, 5, 6, 7, 8, 9])
    
    def nested_yield_4():
        yield 1
        try:
            1/0
        except:
            yield 2
            try:
                yield 3
                try:
                    yield 4
                except:
                    pass
            except:
                pass
        else:
            raise AssertionError()
        yield 5
            
    AreEqual(list(nested_yield_4()), [1, 2, 3, 4, 5])


def test_generator_arg_counts():
    # Generator methods with varying amounts of local state
    def lstate(size):
        args = ''
        for i in xrange(size-1):
            args = args+('a%i, ' % i)
        args = args+('a%i' % (size-1))
    
        func = """
def fetest(%s):        
    ret = 0
    for i in xrange(%i):
        exec('a%%i = a%%i*a%%i' %% (i,i,i))
        exec('ret = a%%i' %% i)
        yield ret
""" % (args, size)
        #print func
        d = {'AreEqual':AreEqual}
        exec func in d, d
    
        args = range(size)
        exec "AreEqual(list(fetest(%s)),%s)" % (str(args)[1:-1], str([x*x for x in args])) in d, d
    
    lstate(1)
    lstate(2)
    lstate(4)
    lstate(8)
    lstate(16)
    lstate(32)
    lstate(64)
    lstate(122)
    #lstate(123) # CLR bug, can't handle 127 arguments in DynamicMethod
    #lstate(124)
    #lstate(125) # CLR bug, can't handle 127 arguments in DynamicMethod
    lstate(128)
    if sys.platform != "win32":
        # CPython doesn't support more than 255 arguments
        lstate(256)
        #
        lstate(512)

def test_iterate_closed():
    #
    # Test that calling on a closed generator throws a StopIteration exception and does not
    # do any further execution of the generator. (See codeplex workitem 1402)
    #
    #
    
    
    # 1) Test exiting by normal return
    l=[0, 0]
    def f(l):
      l[0] += 1 # side effect
      yield 'a'
      l[1] += 1  # side effect statement
    
    g=f(l)
    Assert(g.next() == 'a')
    Assert(l == [1,0]) # should not have executed past yield
    AssertError(StopIteration, g.next)
    Assert(l == [1,1]) # now should have executed
    
    # Generator is now closed, future calls should just keep throwing
    AssertError(StopIteration, g.next)
    Assert(l == [1,1]) # verify that we didn't execute any more statements
    
    
    
    # 2) Now test with exiting via StopIteration exception
    l = [0,0]
    def f(l):
      yield 'c'
      l[0] += 1
      raise StopIteration
      l[1] += 1
    
    g=f(l)
    Assert(g.next() == 'c')
    Assert(l == [0,0])
    AssertError(StopIteration, g.next)
    Assert(l == [1,0])
    
    # generator is now closed from unhandled exception. Future calls should throw StopIteration
    AssertError(StopIteration, g.next)
    Assert(l == [1,0]) # verify that we didn't execute any more statements
    
    # repeat enumeration in a comprehension.
    # This tests that StopIteration is properly caught and gracefully terminates the generator.
    l=[0,0]
    AreEqual([x for x in f(l)], ['c'])
    AreEqual(l,[1,0])
    
    
    
    # 3) Now test with exiting via throwing an unhandled exception
    class MyError:
      pass
    
    l=[0, 0]
    def f(l):
      l[0] += 1 # side effect
      yield 'b'
      l[1] += 1  # side effect statement
      raise MyError
    
    g=f(l)
    Assert(g.next() == 'b')
    Assert(l == [1,0])
    AssertError(MyError, g.next)
    Assert(l == [1,1])
    
    # generator is now closed from unhandled exception. Future calls should throw StopIteration
    AssertError(StopIteration, g.next)
    Assert(l == [1,1]) # verify that we didn't execute any more statements
    
    
    # repeat enumeration in a comprehension. Unlike case 2, this now fails since the exception
    # is MyError instead of StopIteration
    l=[0,0]
    def g():
      return [x for x in f(l)]
    AssertError(MyError, g)
    AreEqual(l,[1,1])

def test_generator_empty_tuple():
    def f():
        yield ()
    AreEqual(list(f()), [()])
    
def test_generator_reentrancy():
    # Test that generator can't be called re-entrantly. This is explicitly called out in Pep 255.
    # Any operation should throw a ValueError if called.
    def f():
      try:
        i = me.next() # error: reentrant call! Should throw ValueError, which we can catch.
      except ValueError:
        yield 7
      yield 10
      # try again, should still throw
      me.send(None)
      Assert(False) # unreachable!
    
    me = f()
    AreEqual(me.next(), 7)
    # Generator should still be alive
    AreEqual(me.next(), 10)
    AssertError(ValueError, me.next)
    # since the last call went unhandled, the generator is now closed.
    AssertError(StopIteration, me.next)

def test_generator_expr_in():
    AreEqual('abc' in (x for x in ('abc', )), True)
    
    def f(): yield 2

    AreEqual(2 in f(), True)

def test_generator_attrs():
    expectedAttributes = ['gi_running', 'send', 'next', '__iter__', '__name__', 'close', 'throw', 'gi_frame', 'gi_code']
    expectedAttributes.sort()
    def f(): yield 2
    
    got = set(dir(f())) - set(dir(object))
    got = list(got)
    got.sort()
    AreEqual(got, expectedAttributes)
    
    temp_gen = f()
    AreEqual(f.func_code, temp_gen.gi_code)
    
def test_cp24031():
    def f(*args):
        return args
    
    AreEqual(f(*(x for x in xrange(2))),
             (0, 1))
    
    class KNew(object):
        pass
    
    AssertErrorWithMessage(TypeError, "object.__new__() takes no parameters",
                           lambda: KNew(*(i for i in xrange(10))) != None)

    
#--MAIN------------------------------------------------------------------------
run_test(__name__)
