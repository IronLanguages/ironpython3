# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

# Test that sys.exc_info() is properly set.

import sys
import traceback
import unittest

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
    return t.args[0]

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
        self.A(None)  # no longer valid after exception block, but still in function


    # test setting in except params
    def test_excep_params(self):
        def t():  # called as argument in except clause
            self.A(63)
            return TypeError

        def f():
            try:
                raise ValueError(63)
            except t():  # not matching
                self.fail()
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
                # raise a new exception type. exc_info gets updated
                # immediately
                raise ValueError(43)
            finally:
                # Using the new exception; the old one is no longer active
                self.A(43)
        try:
            f()
        except:
            self.A(43)
        self.A(None)

    # finally, same function as active except, exception path


    def test_fin_except(self):
        self.A(None)
        try:
            raise ValueError(20)
        except:
            self.A(20)
        finally:
            self.A(None)  # Caught by except block already
        self.A(None)  # Not active


    # finally doesnt see exc_info when there's no catcher.
    def test_fin_except2(self):
        def f1():
            self.A(None)
            raise ValueError(20)

        def f2():
            self.A(None)
            try:
                f1()  # throw from a different function
                self.fail()
            finally:
                # we should be here via the exceptional path.
                # but since there's no except block in here, exc_info not set.
                self.A(20)
            self.fail()
        try:
            f2()
        except:
            self.A(20)
        self.A(None)


    # Finally w/o an except block does not see the exception.
    # compare to test_fin_except()
    def helper_fin_no_except(self):
        self.A(None)
        try:
            raise ValueError(15)
        finally:
            self.A(15)  # no except block, exception still thrown.


    def test_fin_no_except(self):
        try:
            self.helper_fin_no_except()
        except:
            self.A(15)
        self.A(None)

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
                self.A(None)
            except:
                self.fail()
            self.A(None)
            try:
                self.A(None)
                # Now raise a new exception. This becomes the current exc_info()
                # value.
                raise ValueError(20)
            except:
                self.A(20)
            self.A(None)
        except:
            self.fail()
        self.A(None)


    # Child function inherits exc_info() from parent, but can't change parents.
    # only changed by a function having an except block.
    def test_call(self):
        def f():
            self.A(7)  # parent is already in a except block.
            try:
                raise ValueError(20)
            except:
                self.A(20)
            self.A(7)
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
            self.A(55)

        def f3b():
            self.A(55)
            try:
                raise ValueError(22)
            except:
                self.A(22)
                return  # return from Except, swallows Ex
            self.fail()

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
            self.A(None)

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
                self.A(27)  # exc_info not reset because exception is still being thrown
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
                yield 1
                self.A(3)
                yield 2
                self.A(3) 
                try:
                    yield 3  # generator will call next when exc_info=Val(6) here.
                finally:
                    # We're in the non-exception path of a finally, but still have exc_info set since
                    # generator was called from a catch block.
                    self.A(3)
                yield 4
                self.A(3)  # still set from generator's caller
            self.A(None)
            yield 5
        # call the generator
        g = f()
        self.assertEqual(next(g), 1)
        self.A(None)  # generator's exc value shouldn't taint the caller
        self.assertEqual(next(g), 2)
        self.A(None)  # clear after returning from yield
        try:
            raise ValueError(5)  # New exception!
        except:
            self.A(5)
            # Now call back into the generator with a new exc_info!
            self.assertEqual(next(g), 3)
            self.A(5)
        self.A(None)
        try:
            self.A(None)
            raise ValueError(6)  # New exception
        except:
            self.A(6)
            # this will execute a finally in the generator.
            self.assertEqual(next(g), 4)
            self.A(6)
        self.A(None)
        self.assertEqual(next(g), 5)


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
        self.assertEqual(next(g), 1)
        self.A(None)
        try:
            try:
                g.throw(ValueError(87))
                self.fail()
            finally:
                # exceptional path.
                # The generator throws an exception, causing us
                # to receive it
                self.A(22)
        except:
            self.A(22)
        self.A(None)
   
    #========================================================
    # With's Pep (http://www.python.org/dev/peps/pep-0343/) says the
    # __exit__ can be invoked by an except block,
    # but unlike a normal except, that shouldn't set sys.exc_info().

    # https://github.com/IronLanguages/ironpython3/issues/451
    @unittest.skip('unbound variable: $localContext')
    def test_with_simple(self):
        """Simple case, no exception set."""
        class M1(ManBase):
            def __init__(self, s):
                super(M1, self).__init__(s)
            
        with M1(self):
            pass

    # https://github.com/IronLanguages/ironpython3/issues/451
    @unittest.skip('unbound variable: $localContext') 
    def test_with_fail(self):
        """with.__exit__ doesn't see exception in exception case."""
        class M2(ManBase):
            def __init__(self, s):
                super(M2, self).__init__(s)

            # exit is invoked when 'with' body exits (either via exception, branch)
            def __exit__(self, t, v, tb):
                self.s.assertEqual(v.args[0], 15)  # exception passed in as local
                self.s.A(15)
                return True  # swallow exception
        #
        # With.__exit__ does not see current exception
        with M2(self):
            raise ValueError(15)

    # https://github.com/IronLanguages/ironpython3/issues/451
    @unittest.skip('unbound variable: $localContext') 
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

    # https://github.com/IronLanguages/ironpython3/issues/451
    @unittest.skip('unbound variable: $localContext')
    # call 'with' from an except block, do failure case
    def test_with_except_fail(self):
        class M2(ManBase):
            def __init__(self,s):
                super(M2,self).__init__(s)
            def __enter__(self):
                self.s.A(15)
            # exit is invoked when 'with' body exits (either via exception, branch)

            def __exit__(self, t, v, tb):
                self.s.assertEqual(v.args[0], 34)  # gets failure from With block
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
            self.A(15) # TODO: is this a bug?

class ExcInfoGeneratorTest(unittest.TestCase):
    
    def test_generator_has_own_exception_exception_context(self):
        def gen():
            try:
                raise Exception
            except:
                exc_info = sys.exc_info()
                yield exc_info
                yield sys.exc_info()
            yield sys.exc_info()
        x = gen()
        exc_info = next(x)
        self.assertEqual(sys.exc_info(), (None, None, None))
        self.assertEqual(next(x), exc_info)
        self.assertEqual(next(x), (None, None, None))

    def test_exception_context_cleared(self):
        try:
            try:
                raise Exception(1)
            except:
                pass    
            raise Exception(2)
        except Exception as e:
            self.assertIs(e.__context__, None)

    def test_generator_inherits_exception_context(self):
        def gen():
            try:
                raise Exception
            except:
                pass
            yield sys.exc_info()
        x = gen()
        try:
            raise Exception
        except:
            exc_info = sys.exc_info()
            self.assertEqual(next(x), exc_info)

    def test_generator_inherits_changing_exception_context_1(self):
        def gen():
            yield sys.exc_info()
            try:
                raise Exception
            except:
                pass
            yield sys.exc_info()
        x = gen()
        self.assertEqual(next(x), (None, None, None))
        try:
            raise Exception
        except:
            exc_info = sys.exc_info()
            self.assertEqual(next(x), exc_info)

    def test_generator_inherits_changing_exception_context_2(self):
        def gen():
            yield sys.exc_info()
            try:
                raise Exception
            except:
                pass
            yield sys.exc_info()
        x = gen()
        try:
            raise Exception
        except:
            exc_info_1 = sys.exc_info()
            self.assertEqual(next(x), exc_info_1)
        try:
            raise Exception
        except:
            exc_info_2 = sys.exc_info()
            self.assertEqual(next(x), exc_info_2)
        self.assertNotEqual(exc_info_1, exc_info_2)

    def test_exception_traceback_unchanged_by_inherited_exception(self):
        def gen():
            try:
                raise Exception
            except:
                yield traceback.format_exc()
                yield traceback.format_exc()
            
        x = gen()
        try:
            raise Exception('@Exception 1')
        except:
            self.assertTrue('@Exception 1' in next(x))
        try:
            raise Exception('@Exception 2')
        except:
            self.assertTrue('@Exception 1' in next(x))

    def test_new_exceptions_inherit_new_exception_contexts_1(self):
        def gen():
            try:
                raise Exception
            except:
                yield traceback.format_exc()
            try:
                raise Exception
            except:
                yield traceback.format_exc()
            
        x = gen()
        try:
            raise Exception('@Exception 1')
        except:
            self.assertIn('@Exception 1', next(x))
        try:
            raise Exception('@Exception 2')
        except:
            res = next(x)
            self.assertIn('@Exception 2', res)
            self.assertNotIn('@Exception 1', res)

    def test_new_exceptions_inherit_new_exception_contexts_2(self):
        def gen():
            try:
                raise Exception
            except:
                yield traceback.format_exc()
            yield "Hello"
            try:
                raise Exception
            except:
                yield traceback.format_exc()
            
        x = gen()
        try:
            raise Exception('@Exception 1')
        except:
            self.assertIn('@Exception 1', next(x))
        next(x)
        try:
            raise Exception('@Exception 2')
        except:
            res = next(x)
            self.assertIn('@Exception 2', res)
            self.assertNotIn('@Exception 1', res)

if __name__ == "__main__":
    unittest.main()