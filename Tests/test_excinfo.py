# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

# Test that sys.exc_info() is properly set.

import sys
import unittest

from iptest import is_ironpython, run_test

# Rules:
# 1) thread has a current exception
# 2) except block sets current exception
# 3) when method returns (either normally, via exception, or via generator),
#    it restores the current exception to what it was on function entry.
#
# exc_info() is pretty useless in a finally block. It may be non-null, but it's value
# depends on how exactly we got to execute the finally. In fact, in a finally, exc_info
# may be non-null, but not for the current exception.
# Or it may be null, even when there's an outstanding exception. $$$ is
# that true?

# return the current exception arg or None
# raise ValueError(15)  ; E() returns 15


def E():
    t = sys.exc_info()[1]
    if t == None:
        return None
    return t[0]

# a manager class to use 'with' statement
class ManBase(object):
    def __init__(self,s=None):
        self.s = s
    def __enter__(self):
        self.s.A(None)
        pass
    # exit is invoked when 'with' body exits (either via exception, branch)

    def __exit__(self, t, v, tb):
        self.s.A(None)
        return True  # swallow exception

# helper for easy asserts on E
class ExcInfoTest(unittest.TestCase):

    def A(self,v):
        self.assertEqual(E(), v)

    def test_simple(self):
        self.A(None)
        try:
            raise ValueError(15)
        except:
            self.A(15)
        self.A(15)  # still valid after exception block, but still in function


    # test setting in except params
    def test_excep_params(self):
        def t():  # called as argument in except clause
            self.A(63)
            return TypeError

        def f():
            try:
                raise ValueError(63)
            except t():  # not matching
                Assert(False)
        try:
            f()
        except:
            self.A(63)


    # raise new ex out of catch; check in finally
    def test_except_rethrow(self):
        def f():
            try:
                raise ValueError(81)
            except:
                self.A(81)
                # raise a new exception type. exc_info doesn't get updated
                # until we hit a new except block
                raise ValueError(43)
            finally:
                # still using the original exc since we haven't hit a new exc block
                # yet.
                self.A(81)
        try:
            f()
        except:
            self.A(43)
        self.A(43)

    # finally, same function as active except, exception path


    def test_fin_except(self):
        self.A(None)
        try:
            raise ValueError(20)
        except:
            self.A(20)
        finally:
            self.A(20)  # active from except block
        self.A(20)  # still active


    # finally doesnt see exc_info when there's no catcher.
    def test_fin_except2(self):
        def f1():
            self.A(None)
            raise ValueError(20)

        def f2():
            self.A(None)
            try:
                f1()  # throw from a different function
                Assert(False)
            finally:
                # we should be here via the exceptional path.
                # but since there's no except block in here, exc_info not set.
                self.A(None)
            Assert(False)
        try:
            f2()
        except:
            self.A(20)
        self.A(20)


    # Finally w/o an except block does not see the exception.
    # compare to test_fin_except()
    def helper_fin_no_except(self):
        self.A(None)
        try:
            raise ValueError(15)
        finally:
            self.A(None)  # no except block, so not set.


    def test_fin_no_except(self):
        try:
            self.helper_fin_no_except()
        except:
            self.A(15)
        self.A(15)

    #
    # inactive except block.
    # The mere presence of an except block is enough to set exc_info(). We don't
    # need to actually execute the handlers.


    def helper_fin_inactive(self):
        self.A(None)
        try:
            raise ValueError(20)
        except TypeError:  # 
            self.fail('mismatched, still causes exc_info() to be set')
        finally:
            self.A(20)  # still set even from inactive block
        self.A(20)  # still active


    def test_fin_inactive(self):
        try:
            self.helper_fin_inactive()
        except:  # prevent from going unhandled
            self.A(20)


    # Non exception path
    def test_fin_normal(self):
        self.A(None)
        try:
            pass
        finally:
            self.A(None)
        self.A(None)


    # Nested
    def test_nested(self):
        try:
            try:
                try:
                    raise ValueError(15)
                except:
                    self.A(15)
                self.A(15)
            except:
                Assert(False)
            self.A(15)
            try:
                self.A(15)
                # Now raise a new exception. This becomes the current exc_info()
                # value.
                raise ValueError(20)
            except:
                self.A(20)
            self.A(20)
        except:
            Assert(False)
        self.A(20)


    # Child function inherits exc_info() from parent, but can't change parents.
    # only changed by a function having an except block.
    def test_call(self):
        def f():
            self.A(7)  # parent is already in a except block.
            try:
                raise ValueError(20)
            except:
                self.A(20)
            self.A(20)
            # will be restored to 7 on function return
        #
        try:
            raise ValueError(7)
        except:
            self.A(7)
            f()
            self.A(7)

    # Test with multiple calls and ensure value is restored


    def test_call2(self):
        def f3a():
            self.A(55)
            try:
                raise ValueError(11)
            except:
                self.A(11)
            self.A(11)

        def f3b():
            self.A(55)
            try:
                raise ValueError(22)
            except:
                self.A(22)
                return  # return from Except, swallows Ex
            Assert(False)

        def f2():
            self.A(55)
            f3a()
            self.A(55)
            f3b()
            self.A(55)
        #
        try:
            self.A(None)
            raise ValueError(55)
        except:
            self.A(55)
            f2()
            self.A(55)


    # Still set in finally on return.
    def test_ex_fin(self):
        try:
            try:
                raise ValueError(25)
            except:
                self.A(25)
                return 7
        finally:
            # still set from the except block
            self.A(25)

    # like test_ex_fin, but when we split into an inner function, it gets reset


    def test_funcs(self):
        def f():
            try:
                try:
                    raise ValueError(27)
                except:
                    self.A(27)
                    raise  # rethrow
            finally:
                # on exceptional path. Since this function had a except clause
                # in the function, exc_info() is still set.
                self.A(27)
        try:
            try:
                f()
            finally:
                self.A(None)  # exc_info reset since thrown from different function
        except:
            self.A(27)
            pass


    # ???
    # Tests splitting across multiple functions to show reset
    def f():
        pass


    # Test with exc_info and generators.
    # The first yield in the except block is a return from the function and clears
    # the current exception status.


    def test_generator(self):
        def f():
            try:
                raise ValueError(3)
            except:
                self.A(3)
                yield 1  # this will reset exc_info
                self.A(None)
                yield 2
                self.A(5)  # pick up from caller
                try:
                    yield 3  # generator will call next when exc_info=Val(6) here.
                finally:
                    # We're in the non-exception path of a finally, but still have exc_info set since
                    # generator was called from a catch block.
                    self.A(6)
                yield 4
                self.A(6)  # still set from generator's caller
            self.A(6)
            yield 5
        # call the generator
        g = f()
        self.assertEqual(g.next(), 1)
        self.A(None)  # generator's exc value shouldn't taint the caller
        self.assertEqual(g.next(), 2)
        self.A(None)  # clear after returning from yield
        try:
            raise ValueError(5)  # New exception!
        except:
            self.A(5)
            # Now call back into the generator with a new exc_info!
            self.assertEqual(g.next(), 3)
            self.A(5)
        self.A(5)
        try:
            self.A(5)
            raise ValueError(6)  # New exception
        except:
            self.A(6)
            # this will execute a finally in the generator.
            self.assertEqual(g.next(), 4)
            self.A(6)
        self.A(6)
        self.assertEqual(g.next(), 5)


    # throw out of generator
    # ensure that exc_info() is cleared.
    def test_gen_throw(self):
        def f():
            try:
                yield 1  # caller will g.Throw() from here
            except:
                self.A(87)
                raise ValueError(22)  # throw new error
        
        g = f()
        self.A(None)
        self.assertEqual(g.next(), 1)
        self.A(None)
        try:
            try:
                g.throw(ValueError(87))
                Assert(False)
            finally:
                # exceptional path.
                # exc_info should have been cleared on exiting generator.
                self.A(None)
        except:
            self.A(22)
        self.A(22)


    #---------------------------------------------------------------------
    #
    # Test sys.exc_clear(), which was added in Python 2.3
    # This clears the last exception status.
    #
    #---------------------------------------------------------------------

    def test_clear_simple(self):
        """simple case of clear in an except block."""
        try:
            raise ValueError(12)
        except:
            self.A(12)
            sys.exc_clear()
            self.A(None)
        self.A(None)

    def test_clear_nested(self):
        try:
            raise ValueError(13)
        except:
            try:
                self.A(13)
                raise ValueError(54)
            except:
                self.A(54)
                sys.exc_clear()
                self.A(None)
            self.A(None)
        self.A(None)

    def test_clear_nested_func(self):
        def f():
            try:
                self.A(13)
                raise ValueError(54)
            except:
                self.A(54)
                sys.exc_clear()
                self.A(None)
            self.A(None)  # will be restored after func returns
        #
        try:
            raise ValueError(13)
        except:
            self.A(13)
            f()  # calls sys.exc_clear()
            self.A(13)  # still restored even after clear
        self.A(13)


    # Test clearing when there isn't an active exception (outside except block)
    def test_clear_no_active_ex(self):
        self.A(None)
        sys.exc_clear()
        self.A(None)
        try:
            sys.exc_clear()
            self.A(None)
        except:
            pass
        try:
            pass
        finally:
            sys.exc_clear()
            self.A(None)
        self.A(None)

    #========================================================
    # With's Pep (http://www.python.org/dev/peps/pep-0343/) says the
    # __exit__ can be invoked by an except block,
    # but unlike a normal except, that shouldn't set sys.exc_info().

    def test_with_simple(self):
        """Simple case, no exception set."""
        class M1(ManBase):
            def __init__(self, s):
                super(M1, self).__init__(s)
            
        with M1(self):
            pass

    def test_with_fail(self):
        """with.__exit__ doesn't see exception in exception case."""
        class M2(ManBase):
            def __init__(self, s):
                super(M2, self).__init__(s)

            # exit is invoked when 'with' body exits (either via exception, branch)
            def __exit__(self, t, v, tb):
                self.s.assertEqual(v[0], 15)  # exception passed in as local
                if is_ironpython:  # http://ironpython.codeplex.com/workitem/27990
                    self.s.A(None)  # but sys.exc_info() should not be set!!
                else:
                    self.s.A(15)
                return True  # swallow exception
        #
        # With.__exit__ does not see current exception
        with M2(self):
            raise ValueError(15)


    # call 'with' from an except block
    def test_with_except_pass(self):
        class M2(ManBase):
            def __init__(self,s):
                super(M2, self).__init__(s)
            def __enter__(self):
                self.s.A(15)
            # exit is invoked when 'with' body exits (either via exception, branch)

            def __exit__(self, t, v, tb):
                self.s.assertEqual(v, None)
                self.s.A(15)
                return True  # swallow exception
        #
        # With.__exit__ does not see current exception
        try:
            raise ValueError(15)
        except:
            self.A(15)
            with M2(self):
                self.A(15)
                pass
            self.A(15)


    # call 'with' from an except block, do failure case
    def test_with_except_fail(self):
        class M2(ManBase):
            def __init__(self,s):
                super(M2,self).__init__(s)
            def __enter__(self):
                self.s.A(15)
            # exit is invoked when 'with' body exits (either via exception, branch)

            def __exit__(self, t, v, tb):
                self.s.assertEqual(v[0], 34)  # gets failure from With block
                if is_ironpython:  # http://ironpython.codeplex.com/workitem/27990
                    self.s.A(15)  # gets failure from sys.exc_info() which is from outer except block
                else:
                    self.s.A(34)
                return True  # swallow exception
        #
        # With.__exit__ does not see current exception
        try:
            raise ValueError(15)
        except:
            self.A(15)
            with M2(self):
                self.A(15)
                raise ValueError(34)
            if is_ironpython:  # http://ironpython.codeplex.com/workitem/27990
                self.A(15)
            else:
                self.A(34)


run_test(__name__)