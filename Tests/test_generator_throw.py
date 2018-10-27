# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

#
# Test Pep 342 enhancements to generator, including Throw(), Send(), Close() and yield expressions.
#

import gc
import sys
import unittest

from iptest import is_cli, run_test

# Declare some dummy exceptions to throw


class MyError(Exception):
    pass


class MyError2(Exception):
    pass

# this is straight from the sample in Pep342
# Useful to skip the first call to generator.next() for generators that
# are consumers.
def consumer(func):
    def wrapper(*args, **kw):
        gen = func(*args, **kw)
        next(gen)
        return gen
    wrapper.__name__ = func.__name__
    wrapper.__dict__ = func.__dict__
    wrapper.__doc__ = func.__doc__
    return wrapper

# simple generator with finally
# set l[0]=1 to indicate that finally block was executed.
def f1(l):
    yield 1
    try:
        yield 2
        pass  # '  Non exception case'
    finally:
        pass  # '  inside finally'
        l[0] = 1
    yield 3

#
# In print redirection slot.
#
class MyWriter:
    data = ""

    def write(self, l):
        self.data += l

def gen_compare():
    f = ((yield 1) < (yield 2) < (yield 3))
    yield f

class GeneratorThrowTest(unittest.TestCase):

    def EnsureClosed(self, g):
        with self.assertRaises(StopIteration):
            next(g)

    def test_del(self):
        """Test that generator.__del__ is invoked and that it calls Close()"""
        # Note that .NET's GC:
        # 1) runs on another thread,
        # 2) runs at a random time (but can be forcibly invoked from gc.collect)
        # So other generators that go out of scope will get closed() called at random times from wherever
        # the generator was left. This can introduce some nondeterminism in the tests.


        global l
        l = [0]

        def nested():
            def ff3(l):
                try:
                    yield 10
                finally:
                    l[0] += 1
            g = ff3(l)
            self.assertEqual(next(g), 10)  # move to inside the finally
            del g

        nested()
        gc.collect()
        # in controlled environment like this, this is ok to expect finalizer to run
        # however, when gc happens at random, and finalizer tries to continue execution
        # of generator, the state of generator and generator frame is non
        # deterministic

        # self.assertEqual(l,[1]) # finally should have execute now.


    def test_yield_lambda(self):
        """
        Yield can appear in lambda expressions (or any function body).
        A generator lambda expression yields its final result, instead of just
        returning it.
        """

        f = lambda x: (3 + (yield x), (yield x * 2))

        g = f(10)
        self.assertEqual(next(g), 10)
        self.assertEqual(g.send(9), 10 * 2)
        if is_cli:  # https://github.com/IronLanguages/main/issues/864
            self.assertEqual(g.send(5), (3 + 9, 5))
        else:
            self.assertRaises(StopIteration, g.send, 5)


    def test_yield_old_lambda(self):
        """
        This usage of lambda expression tests a different parsing path in IPY.
        (old lambda expressions)
        """
        l = [x for x in (lambda: (yield 3), 8)]
        self.assertEqual(l[1], 8)
        f = l[0]
        g = f()
        self.assertEqual(next(g), 3)


    def test_type_generator(self):
        def g(): yield 10

        def f(): x += yield
        self.assertEqual(type(f()), type(g()))


    def test_yield_default_param(self):
        """
        CPython 2.5 allows yield as a default parameter for lambda expressions
        (though not for regular def functions)
        """
        # This will return a generator that
        # defines a lambda expression, with default param initialized by send()
        # returns that lambda expression
        def f():
            yield lambda x=(yield 25): x * 2

        g = f()
        self.assertEqual(next(g), 25)
        # this sends in the default parameter, yields the lambda expression
        l = g.send(15)
        self.assertEqual(l(), 15 * 2)  # this now uses the default param
        self.assertEqual(l(3), 3 * 2)  # use a non-default param.
        self.assertEqual(l(), 15 * 2)


    def test_yield_genexp(self):
        """
        Test yield in a genexp body. This is a little bizare, but the CPython
        tests have this.
        """

        # def f():
        #   for i in range(5):
        #     yield (yield i)
        #
        # Since list() ctor only calls next, (yield i) returns None
        g = ((yield i) for i in range(5))
        x = list(g)
        self.assertEqual(x, [0, None, 1, None, 2, None, 3, None, 4, None])


    def test_yield_index(self):
        """test using yield expressions in indexing"""
        def f():
            # eval order is 1[2]=3
            (yield)[(yield)] = 'x'
            yield
        g = f()
        self.assertEqual(next(g), None)
        l = [10, 20, 30]
        g.send(l)
        g.send(1)
        self.assertEqual(l[1], 'x')


    def test_yield_exp(self):
        """test send with yield expression"""
        def side_effect(l, i, res):
            l[i] += 1
            return res

        def f(l):
            # first test simple yield expression
            self.assertEqual((yield 3), 100)
            # test an empty yield. Equivalent to 'yield None'
            yield
            # now test yield embedded in a complex expression with side-effects and
            # evaluation order.
            x = side_effect(l, 0, 5) + (yield 10) + side_effect(l, 1, 2)
            yield x
        l = [0, 0]
        g = f(l)
        self.assertEqual(next(g), 3)
        self.assertEqual(g.send(100), None)
        self.assertEqual(next(g), 10)
        self.assertEqual(l, [1, 0])
        self.assertEqual(g.send(30), 37)
        self.assertEqual(l, [1, 1])

    def test_yield_exp_parse(self):
        """test different parsing configurations of yield expression"""
        # - Top-level assignment (=, +=) does not require parenthesis.
        # - else yield used as expression does require parenthesis.
        # - argument to yield is optional

        def f():
            # yield as statement, yielding tuple
            yield 1, 2
            # top-level assignment. Doesn't need parenthesis
            x = yield
            self.assertEqual(x, 15)
            x = yield 10
            self.assertEqual(x, None)
            y = 5
            y += yield 99
            self.assertEqual(y, 105)
            y += yield
            self.assertEqual(y, 145)
            # test precedence. This is w = (yield (1,2)). Not w=(yield 1), 2
            w = yield 1, 2
            self.assertEqual(w, 39)
            # yield in an expression, must be in parenthsis
            z = (yield) / (yield)
            self.assertEqual(z, 100 / 25)
            yield 123
        g = f()
        self.assertEqual(next(g), (1, 2))
        self.assertEqual(next(g), None)
        self.assertEqual(g.send(15), 10)
        self.assertEqual(next(g), 99)
        self.assertEqual(g.send(100), None)
        self.assertEqual(g.send(40), (1, 2))
        self.assertEqual(g.send(39), None)
        self.assertEqual(g.send(100), None)
        self.assertEqual(g.send(25), 123)


    def test_yy(self):
        """Test some goofier places to put a yield expression"""
        def f():
            yield (yield 5)
        g = f()
        self.assertEqual(next(g), 5)
        self.assertEqual(g.send(15), 15)


    def test_send_after_closed(self):
        """Test Send after Close(), should throw StopException, just like Next()"""
        l = [0]

        def f():
            x = yield 10
            l[0] += 1
            self.assertEqual(x, 15)
        g = f()
        self.assertEqual(next(g), 10)

        def t():
            g.send(15)
        self.assertEqual(l, [0])
        self.assertRaises(StopIteration, t)
        self.assertEqual(l, [1])
        self.EnsureClosed(g)
        self.assertRaises(StopIteration, t)
        self.assertEqual(l, [1])  # no more change


    def test_send_unstarted(self):
        """send(non-none) fails on newly created generator"""
        def f():
            x = yield 10
            self.assertEqual(x, None)  # next() is like send(None)
            yield 5
        g = f()

        def t():
            g.send(1)
        self.assertRaises(TypeError, t)  # can't send non-null on unstarted
        # should not change generator status
        self.assertEqual(next(g), 10)
        self.assertEqual(next(g), 5)


    def test_send_exception(self):
        """Ensure that sending an exception doesn't become a throw"""
        def f():
            y = yield
            self.assertEqual(y, MyError)
            yield
        g = f()
        next(g)
        g.send(MyError)


    def test_throw_unhandled(self):
        """Throw not handled in iterator"""
        # Simple iterator
        def f():
            # Caller will throw an exception after getting this value
            yield 5
            self.fail('Iterator should not get here')

        g = f()

        i = next(g)
        self.assertEqual(i, 5)

        # This should go uncaught from the iterator
        try:
            g.throw(MyError)
            self.assertTrue(False)  # expected exception
        except MyError:
            pass  # 'Good: Exception passed through generator and caught by caller'


    def test_throw_handled(self):
        """Throw handled in iterator"""
        def f2():
            yield 1
            try:
                yield 2  # caller throws from here
                self.assertTrue(False)  # unreachable
            except MyError:
                pass  # 'Good: Generator caught exception from throw'
                yield 3
            yield 4

        g = f2()
        self.assertEqual(next(g), 1)
        self.assertEqual(next(g), 2)

        # generator will catch this.
        # this throws from the last yield point, resumes executing the generator
        # and returns the result of the next yield point.
        i = g.throw(MyError)
        self.assertEqual(i, 3)

        # Test that we can call next() after throw.
        self.assertEqual(next(g), 4)


    def test_throw_value(self):
        """Test another throw overload passing (type,value)."""
        class MyClass2(Exception):
            def __init__(self, val):
                self.val = val

        def f():
            try:
                yield 5
                self.assertTrue(false)
            except MyClass2 as x:
                self.assertEqual(x.val, 10)
                yield 15

        g = f()
        self.assertEqual(next(g), 5)
        self.assertEqual(g.throw(MyClass2, 10), 15)


    def test_catch_rethrow(self):
        """Test catch and rethrow"""
        def f4():
            try:
                yield 1
                self.assertTrue(False)
            except MyError:
                raise MyError2

        g = f4()
        next(g)  # move into try block
        try:
            g.throw(MyError)  # will get caught and rethrow MyError 2
            self.assertTrue(False)
        except MyError2:  # catch different error than thrown
            pass


    def test_throw_unstarted(self):
        """Throw as first call  on the iterator."""
        # In this case, throw does not get to the first yield point.
        def f3():
            # haven't called next yet, so throw shouldn't execute anything
            # it should also be before (outside) the try block on the first line.
            try:
                self.assertTrue(False)
                yield 5
            except:
                self.assertTrue(False)

        # 'Test: throw before first yield'
        g = f3()
        try:
            g.throw(MyError)
            self.assertTrue(False)
        except MyError:
            pass
        self.EnsureClosed(g)  # generator should now be closed.

    def test_throw_closed(self):
        """Throw after closed"""
        # Throw after closed, should raise its exception,
        # not another StopIteration / other exception
        # Note this is a little inconsistent with Next(), which raises a StopIteration exception
        # on closed generators.

        def f5():
            yield 1

        g = f5()
        self.assertEqual(next(g), 1)

        # Loop this to ensure that we're in steady state.
        for i in range(0, 3):
            try:
                # G is now closed.
                g.throw(MyError)
                self.assertTrue(False)
            except MyError:
                pass  # 'Good: caught our own exception'


    def test_throw_from_finally(self):
        """test that a generator.Throw() works when stopped in a finally"""
        def f(l):
            try:
                pass
            finally:
                pass  # ' good: inside finally'
                l[0] = 1
                yield 1
                try:
                    yield 2  # throw here
                    self.assertTrue(False)
                except MyError:
                    l[0] = 2

        l = [0]
        g = f(l)
        self.assertEqual(next(g), 1)
        self.assertEqual(l[0], 1)

        self.assertEqual(next(g), 2)  # move into finally block
        try:
            # throw, it will catch and run to completion
            g.throw(MyError)
            self.assertTrue(False)
        except StopIteration:
            self.assertEqual(l[0], 2)
            pass  # ' good: threw and generator ran to completion'
            pass


    def test_throw_run_finally_nonexception(self):
        # Test that finallys properly execute when Gen.Throw is called.
        # This verifies that the exception is really being raised from the right spot
        # within the generator body.

            # Sanity check
            # 'Test: simple finally, no exception'
        l = [0]
        g = f1(l)
        self.assertEqual(next(g), 1)
        self.assertEqual(next(g), 2)
        self.assertEqual(l[0], 0)
        self.assertEqual(next(g), 3)
        self.assertEqual(l[0], 1)
        self.EnsureClosed(g)


    def test_throw_before_finally(self):
        """Now try throwing before finally"""
        l = [0]
        g = f1(l)
        self.assertEqual(next(g), 1)
        try:
            g.throw(MyError)
            self.assertTrue(False)
        except MyError:
            pass
        self.assertEqual(l[0], 0)  # finally should not have been executed

        # since we terminated with an exception, generator should be closed
        self.EnsureClosed(g)


    def test_throw_run_finally_exception(self):
        """Now try throwing in range of finally, so that finally is executed"""
        l = [0]
        g = f1(l)
        self.assertEqual(next(g), 1)
        self.assertEqual(next(g), 2)
        try:
            g.throw(MyError)
            self.assertTrue(False)
        except MyError:
            pass

        # since we terminated with an exception, generator should be closed
        self.EnsureClosed(g)
        self.assertEqual(l[0], 1)  # finally should have run


#
# Test that code/exceptions are being invoked from the right callstack,
# either
#   a) inside the generator body, or
#   b) at the call to Generator.Throw(), but outside the generator body.
# This is important so that the right set of catch blocks get applied.
#

    def test_ctor_throws(self):
        """Creating the exception occurs inside the generator."""
        # Simple class to raise an error in __init__
        class MyErrorClass(Exception):
            def __init__(self):
                raise MyError

        def f():
            try:
                yield 5
                yield 7
            except MyError:
                yield 12

        g = f()
        self.assertEqual(next(g), 5)

        # MyError's ctor will raise an exception. It should be invoked in the generator's body,
        # and so the generator can catch it and continue running to yield a value.
        self.assertEqual(g.throw(MyErrorClass), 12)

        g.close()

    def test_throw_none(self):
        """Test corner case with Throw(None)"""
        def f():
            try:
                yield 5  # we'll be stopped here and do g.throw(none)
                yield 10
            except TypeError:
                # error shouldn't be raised inside of generator, so can't be caught
                # here
                self.assertTrue(false)

        g = f()
        self.assertEqual(next(g), 5)

        # g.throw(None) should:
        # - throw a TypeError immediately, not from generator body (So generator can't catch it)
        # - does not update generator
        def t():
            g.throw(None)
        self.assertRaises(TypeError, t)

        # verify that generator is still valid and can be resumed
        self.assertEqual(next(g), 10)

    def test_close_throw(self):
        """Test close(), which builds on throw()"""
        global l

        def f(l):
            try:
                yield 1
            finally:
                l[0] += 1

        l = [0]
        g = f(l)


    def test_close_ends(self):
        """Test close() on unstarted and closed generators"""
        def f():
            self.assertTrue(False)  # we won't execute the generator
            yield 10
        g = f()
        g.close()  # close on unstarted
        self.EnsureClosed(g)
        f().close()  # close already closed, should be nop.


    def test_close_catch_exit(self):
        def f():
            try:
                yield 1  # caller will close() from here
                self.assertTrue(False)
            except GeneratorExit:
                pass  # catch but exit, that's ok.
        g = f()
        self.assertEqual(next(g), 1)
        g.close()


    def test_close_rethrow(self):
        def f():
            try:
                yield 1  # caller will close() from here
                self.assertTrue(False)
            except GeneratorExit:
                # print 'caught and rethrow'
                raise MyError
        g = f()
        self.assertEqual(next(g), 1)
        # close(), which will raise a GeneratorExit, which gets caught and
        # rethrown as MyError

        def t():
            g.close()
        self.assertRaises(MyError, t)


    def test_close_illegal_swallow(self):
        def f():
            try:
                yield 1  # caller will close() from here
                self.assertTrue(False)
            except GeneratorExit:
                yield 2  # illegal, don't swallow GeneratorExit
        g = f()
        self.assertEqual(next(g), 1)
        # close(), which will raise a GeneratorExit, which gets caught and
        # rethrown as MyError

        def t():
            g.close()
        self.assertRaises(RuntimeError, t)


    # A (yield) expressions can appear in practically any spot as a normal expression.
    # Test a smattering of interesting spots for a variety of coverage.


    def test_exp_tuple(self):
        """Yield in the middle of a tuple"""
        def f():
            yield (1, (yield), 3)
        g = f()
        next(g)
        self.assertEqual(g.send(5), (1, 5, 3))


    def test_exp_base_class(self):
        """Yield as a base class"""
        class MyBase(object):
            def b(self):
                return 5

        # generator to make a base class.
        @consumer
        def M():
            # yield expression as a base class.
            class Foo((yield)):
                def m(self):
                    print('x')
            yield Foo

        g = M()
        F = g.send(MyBase)
        c = F()
        self.assertEqual(c.b(), 5)  # invokes base method


    def test_exp_print_redirect(self):
        @consumer
        def f(text):
            print(text, end='', file=(yield))
            yield  # extra spot to stop on so send() won't immediately throw

        c = MyWriter()
        f("abc").send(c)
        self.assertEqual(c.data, "abc")

    def test_exp_dict_literals(self):
        def f():
            # Note eval order is: {2:1, 4:3}
            d = {(yield 2): (yield 1), (yield): (yield)}
            yield d
        g = f()
        self.assertEqual(next(g), 1)
        self.assertEqual(g.send('a'), 2)
        g.send(10)
        g.send('b')
        d2 = g.send(20)  # {10: 'a', 20: 'b'}
        self.assertEqual(d2, {10: 'a', 20: 'b'})


    # Test yield expressions in compound comparisons

    def test_exp_compare1(self):
        """Compare expecting true."""
        g = gen_compare()
        self.assertEqual(next(g), 1)
        self.assertEqual(g.send(5), 2)
        self.assertEqual(g.send(10), 3)
        self.assertEqual(g.send(15), True)
        self.EnsureClosed(g)

    def test_exp_compare2(self):
        """compare expecting false. This will short-circuit"""
        g = gen_compare()
        self.assertEqual(next(g), 1)
        self.assertEqual(g.send(5), 2)
        self.assertEqual(g.send(2), False)
        self.EnsureClosed(g)

    def test_exp_raise(self):
        """Use as the argument to Raise"""
        @consumer
        def f():
            raise (yield)((yield))
            self.assertTrue(False)
        g = f()
        g.send(ValueError)
        try:
            g.send(15)
        except ValueError as x:
            self.assertEqual(x.args[0], 15)
        # Generator is now closed
        self.EnsureClosed(g)


    def test_exp_slice(self):
        """Slicing. Nothing fancy here, just another place to try yield"""
        @consumer
        def f():
            l = list(range(0, 10))
            yield l[(yield):(yield)]

        g = f()
        g.send(4)
        self.assertEqual(g.send(7), [4, 5, 6])


    def test_layering(self):
        """Layering. Have multiple coroutines calling each other."""
        # manually implement the @consumer pattern
        def append_dict(d):
            def f2():
                while True:
                    (a, b) = ((yield), (yield))
                    d[a] = (b)
            g = f2()
            next(g)
            return g
        # Wrapper around a generator.

        @consumer
        def splitter(g):
            # take in a tuple, split it apart
            try:
                while True:
                    for x in (yield):
                        g.send(x)
            finally:
                g.close()
        d = {}
        g = splitter(append_dict(d))
        #
        g.send(('a', 10))
        self.assertEqual(d, {'a': 10})
        #
        g.send(('b', 20))
        self.assertEqual(d, {'a': 10, 'b': 20})
        #
        g.send(('c', 30))
        self.assertEqual(d, {'a': 10, 'c': 30, 'b': 20})


    def test_layering_2(self):
        """watered down example from Pep342"""
        #
        @consumer
        def Pager(dest):
            # group in threes
            while True:
                try:
                    s = ""
                    s += '[%s,' % ((yield))
                    s += str((yield))
                    s += ',%d]' % ((yield))
                except GeneratorExit:
                    dest.send(s + "...incomplete")
                    dest.close()
                    return
                else:
                    dest.send(s)
        #

        @consumer
        def Writer(outstream):
            while True:
                try:
                    print('Page=' + (yield), file=outstream)
                except GeneratorExit:
                    print('done', file=outstream)
                    raise
        #

        def DoIt(l, outstream):
            pipeline = Pager(Writer(outstream))
            for i in l:
                pipeline.send(i)
            pipeline.close()
        #
        o = MyWriter()
        DoIt(range(8), o)
        self.assertEqual(
            o.data, 'Page=[0,1,2]\nPage=[3,4,5]\nPage=[6,7...incomplete\ndone\n')



    # Test Yield in expressions in an except block
    # even crazier example, (yield) in both Type + Value spots in Except clause


    def getCatch(self):
        """generator to use with test_yield_except_crazy*"""
        yield 1
        l = [0, 1, 2]
        try:
            raise MyError('a')
        except (yield 'a') as e:
            # doesn't work - cp35682
            # self.assertEqual(sys.exc_info(), (None,None,None)) # will print None from the
            # yields
            l[(yield 'b')] = e
            self.assertTrue(l[1] != 1)  # validate that the catch properly assigned to it.
            yield 'c'
        except (yield 'c'):  # especially interesting here
            yield 'd'
        except:
            print('Not caught')
        print(4)


    def test_yield_except_crazy1(self):
        """executes the generators 1st except clause"""
        g = self.getCatch()
        self.assertEqual(next(g), 1)
        self.assertEqual(next(g), 'a')
        # doesn't work - cp35682
        #self.assertEqual(sys.exc_info(), (None, None, None))
        self.assertEqual(g.send(MyError), 'b')
        # doesn't work - cp35682
        # self.assertEqual(sys.exc_info(), (None, None, None))
        self.assertEqual(g.send(1), 'c')
        g.close()

    def test_yield_except_crazy2(self):
        """executes the generators 2nd except clause"""
        # try the 2nd clause
        g = self.getCatch()
        self.assertEqual(next(g), 1)
        self.assertEqual(next(g), 'a')
        # Cause us to skip the first except handler
        self.assertEqual(g.send(ValueError), 'c')
        self.assertEqual(g.send(MyError), 'd')
        g.close()


    def test_yield_empty(self):
        """Yield statements without any return values."""
        def f():
            yield

        g = f()
        self.assertEqual(next(g), None)

        def f():
            if True:
                yield
            yield
        g = f()
        self.assertEqual(next(g), None)
        self.assertEqual(next(g), None)


    def test_throw_stop_iteration(self):
        def f():
            raise StopIteration('foo')
            yield 3

        x = f()
        try:
            next(x)
        except StopIteration as e:
            self.assertEqual(e.args[0], 'foo')


run_test(__name__)