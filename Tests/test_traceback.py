# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import os
import shutil
import sys
import tempfile
import unittest

from iptest import is_cli, is_mono, run_test

FILE = __file__

def getlineno(fn):
    return fn.__code__.co_firstlineno

def _raise_exception():
    raise Exception()

_retb = (getlineno(_raise_exception) + 1, 0, FILE, '_raise_exception')

def _raise_exception_with_finally():
    try:
        raise Exception()
    finally:
        pass

_rewftb = (getlineno(_raise_exception_with_finally) + 2, 0, FILE, '_raise_exception_with_finally')

class TracebackTest(unittest.TestCase):
    def assert_traceback(self, expected):
        tb = sys.exc_info()[2]

        if expected is None:
            self.assertEqual(None, expected)
        else:
            tb_list = []
            while tb is not None :
                f = tb.tb_frame
                co = f.f_code
                filename = co.co_filename
                name = co.co_name
                tb_list.append((tb.tb_lineno, tb.tb_lasti, filename, name))
                tb = tb.tb_next

            self.assertEqual(len(tb_list), len(expected))

            for x in range(len(expected)):
                self.assertEqual(tb_list[x][0], expected[x][0])
                self.assertEqual(tb_list[x][2:], expected[x][2:])

    def test_no_traceback(self):
        try:
            _raise_exception()
        except:
            pass
        self.assert_traceback(None)

    def test_catch_others_exception(self):
        lineno = getlineno(lambda _: None)
        try:
            _raise_exception()
        except:
            self.assert_traceback([(lineno + 2, 0, FILE, 'test_catch_others_exception'), _retb])

    def test_catch_its_own_exception(self):
        lineno = getlineno(lambda _: None)
        try:
            raise Exception()
        except:
            self.assert_traceback([(lineno + 2, 0, FILE, 'test_catch_its_own_exception')])

    def test_catch_others_exception_with_finally(self):
        lineno = getlineno(lambda _: None)
        try:
            _raise_exception_with_finally()
        except:
            self.assert_traceback([(lineno + 2, 0, FILE, 'test_catch_others_exception_with_finally'), _rewftb])

    def test_nested_caught_outside(self):
        lineno = getlineno(lambda _: None)
        try:
            x = 2
            try:
                _raise_exception()
            except NameError:
                self.fail("unhittable")
            y = 2
        except:
            self.assert_traceback([(lineno + 4, 0, FILE, 'test_nested_caught_outside'), _retb])

    def test_nested_caught_inside(self):
        lineno = getlineno(lambda _: None)
        try:
            x = 2
            try:
                _raise_exception()
            except:
                self.assert_traceback([(lineno + 3, 0, FILE, 'test_nested_caught_inside'), _retb])
            y = 2
        except:
            self.assert_traceback(None)

    def test_throw_in_except(self):
        lineno = getlineno(lambda _: None)
        try:
            _raise_exception()
        except:
            self.assert_traceback([(lineno + 2, 0, FILE, 'test_throw_in_except'), _retb])
            try:
                self.assert_traceback([(lineno + 2, 0, FILE, 'test_throw_in_except'), _retb])
                _raise_exception()
            except:
                self.assert_traceback([(lineno + 7, 0, FILE, 'test_throw_in_except'), _retb])
        self.assert_traceback(None)

    def test_throw_in_method(self):
        lineno = getlineno(lambda _: None)

        class C1:
            def M(self2):
                try:
                    _raise_exception()
                except:
                    self.assert_traceback([(lineno + 5, 0, FILE, 'M'), _retb])

        c = C1()
        c.M()

    def test_throw_when_defining_class(self):
        lineno = getlineno(lambda _: None)

        class C2(object):
            try:
                _raise_exception()
            except:
                self.assert_traceback([(lineno + 4, 0, FILE, 'C2'), _retb])

    def test_throw_when_defining_class_directly(self):
        lineno = getlineno(lambda _: None)

        def throw_when_defining_class_directly():
            class C1:
                def M(self2):
                    try:
                        _raise_exception()
                    except:
                        self.assert_traceback([(lineno + 5, 0, FILE, 'M'), _retb])

            class C3(C1):
                _raise_exception()

        try:
            throw_when_defining_class_directly()
        except:
            self.assert_traceback([(lineno + 14, 0, FILE, 'test_throw_when_defining_class_directly'),
                                (lineno + 10, 0, FILE, 'throw_when_defining_class_directly'),
                                (lineno + 11, 0, FILE, 'C3'), _retb])

    def test_compiled_code(self):
        lineno = getlineno(lambda _: None)
        try:
            codeobj = compile('\nraise Exception()', '<mycode>', 'exec')
            exec(codeobj, {})
        except:
            self.assert_traceback([(lineno + 3, 0, FILE, 'test_compiled_code'), (2, 0, '<mycode>', '<module>')])

    def test_throw_before_yield(self):
        lineno = getlineno(lambda _: None)

        def generator_throw_before_yield():
            _raise_exception()
            yield 1

        try:
            for x in generator_throw_before_yield():
                pass
        except:
            self.assert_traceback([(lineno + 7, 0, FILE, 'test_throw_before_yield'), (lineno + 3, 2, FILE, 'generator_throw_before_yield'), _retb])

    def test_throw_while_yield(self):
        lineno = getlineno(lambda _: None)

        def generator_throw_after_yield():
            yield 1
            _raise_exception()

        try:
            for x in generator_throw_while_yield():
                pass
        except:
            self.assert_traceback([(lineno + 7, 0, FILE, 'test_throw_while_yield')])

    def test_yield_inside_try(self):
        lineno = getlineno(lambda _: None)

        def generator_yield_inside_try():
            try:
                yield 1
                yield 2
                _raise_exception()
            except NameError:
                pass

        try:
            for x in generator_yield_inside_try():
                pass
        except:
            self.assert_traceback([(lineno + 11, 0, FILE, 'test_yield_inside_try'), (lineno + 6, 2, FILE, 'generator_yield_inside_try'), _retb])

    def test_throw_and_throw(self):
        lineno = getlineno(lambda _: None)
        try:
            _raise_exception()
        except:
            self.assert_traceback([(lineno + 2, 0, FILE, 'test_throw_and_throw'), _retb])
        try:
            _raise_exception()
        except:
            self.assert_traceback([(lineno + 6, 0, FILE, 'test_throw_and_throw'), _retb])

    def test_throw_in_another_file(self):
        lineno = getlineno(lambda _: None)
        _f_file = os.path.join(os.getcwd(), 'foo.py')
        with open(_f_file, "w") as f:
            f.write('''
def another_raise():
    raise Exception()
''');
        try:
            import foo
            foo.another_raise()
        except:
            self.assert_traceback([(lineno + 9, 0, FILE, 'test_throw_in_another_file'), (3, 0, _f_file, 'another_raise')])
        finally:
            os.remove(_f_file)

    def test_catch_MyException(self):
        lineno = getlineno(lambda _: None)
        class MyException(Exception): pass

        def catch_MyException():
            try:
                _raise_exception()
            except MyException:
                self.assert_traceback([])  # UNREACHABLE. THIS TRICK SIMPLIFIES THE CHECK

        try:
            catch_MyException()
        except:
            self.assert_traceback([(lineno + 10, 0, FILE, 'test_catch_MyException'), (lineno + 5, 0, FILE, 'catch_MyException'), _retb])

    def test_cp11923_first(self):
        line_num = getlineno(lambda _: None)
        try:
            _t_test = os.path.join(os.getcwd(), "cp11923.py")
            with open(_t_test, "w") as f:
                f.write("""
def f():
    x = 'something bad'
    raise Exception(x)""")

            import cp11923
            for i in range(3):
                try:
                    cp11923.f()
                except:
                    self.assert_traceback([(line_num + 12, 69, FILE, 'test_cp11923_first'), (4, 22, _t_test, 'f')])
                import importlib
                importlib.reload(cp11923)

        finally:
            os.remove(_t_test)

    def test_reraise(self):
        line_num = getlineno(lambda _: None)
        def g():
            f()

        def f():
            try:
                raise Exception
            except:
                raise

        try:
            g()
        except:
            self.assert_traceback([(line_num + 11, 0, FILE, 'test_reraise'), (line_num + 2, 0, FILE, 'g'), (line_num + 6, 0, FILE, 'f')])

    def test_reraise_finally(self):
        line_num = getlineno(lambda _: None)
        def g():
            f()

        def f():
            try:
                raise Exception
            finally:
                raise

        try:
            g()
        except:
            self.assert_traceback([(line_num + 11, 30, FILE, 'test_reraise_finally'), (line_num + 2, 3, FILE, 'g'), (line_num + 6,13, FILE, 'f')])

    def test_xafter_finally_raise(self):
        line_num = getlineno(lambda _: None)
        def g():
            raise Exception

        def nop(): pass

        def f():
            try:
                nop()
            finally:
                nop()

            try:
                g()
            except Exception as e:
                self.assert_traceback([(line_num + 13, 30, FILE, 'f'), (line_num + 2, 3, FILE, 'g')])

        f()

    def test_uncaught_exception_thru_try(self):
        line_num = getlineno(lambda _: None)
        def baz():
            raise StopIteration

        def f():
            try:
                baz()
            except TypeError:
                pass
        try:
            f()
        except:
            self.assert_traceback([(line_num + 10, 30, FILE, 'test_uncaught_exception_thru_try'), (line_num + 6, 3, FILE, 'f'), (line_num + 2, 3, FILE, 'baz')])

    def test_with_traceback(self):
        line_num=getlineno(lambda _: None)
        from _thread import allocate_lock
        def f():
            g()

        def g():
            h()

        def h():
            raise Exception('hello!!')

        try:
            with allocate_lock():
                f()
        except:
            self.assert_traceback([(line_num + 13, 30, FILE, 'test_with_traceback'),
                              (line_num + 3, 3, FILE, 'f'),
                              (line_num + 6, 3, FILE, 'g'),
                              (line_num + 9, 3, FILE, 'h')])

    @unittest.expectedFailure # https://github.com/IronLanguages/ironpython3/issues/738
    def test_xraise_again(self):
        line_num=getlineno(lambda _: None)
        def f():
            g()

        def g():
            h()

        def h():
            raise Exception('hello!!')

        try:
            try:
                f()
            except Exception as e:
                raise e
        except:
            self.assert_traceback([(line_num + 14, 30, FILE, 'test_xraise_again'),
                              (line_num + 12, 30, FILE, 'test_xraise_again'),
                              (line_num + 2, 3, FILE, 'f'),
                              (line_num + 5, 3, FILE, 'g'),
                              (line_num + 8, 3, FILE, 'h')])

    def test_with_traceback_enter_throws(self):
        line_num=getlineno(lambda _: None)
        class ctx_mgr(object):
            def __enter__(*args):
                raise Exception('hello')
            def __exit__(*args):
                pass

        def h():
            raise Exception('hello!!')


        try:
            with ctx_mgr():
                h()
        except:
            self.assert_traceback([(line_num + 12, 30, FILE, 'test_with_traceback_enter_throws'),
                              (line_num + 3, 3, FILE, '__enter__')])

    def test_with_traceback_exit_throws(self):
        line_num=getlineno(lambda _: None)
        class ctx_mgr(object):
            def __enter__(*args):
                pass
            def __exit__(*args):
                raise Exception('hello')

        def h():
            raise Exception('hello!!')

        try:
            with ctx_mgr():
                h()
        except:
            self.assert_traceback([(line_num + 12, 30, FILE, 'test_with_traceback_exit_throws'),
                              (line_num + 5, 3, FILE, '__exit__')])

    def test_with_traceback_ctor_throws(self):
        line_num=getlineno(lambda _: None)
        class ctx_mgr(object):
            def __init__(self):
                raise Exception('hello')
            def __enter__(*args):
                pass
            def __exit__(*args):
                pass

        def h():
            raise Exception('hello!!')

        try:
            with ctx_mgr():
                h()
        except:
            self.assert_traceback([(line_num + 13, 30, FILE, 'test_with_traceback_ctor_throws'),
                              (line_num + 3, 3, FILE, '__init__')])

    def test_with_mixed_stack(self):
        """tests a stack which is mixed w/ interpreted and non-interpreted frames
    because f() has a loop in it"""
        line_num=getlineno(lambda _: None)
        def a():
            with xxx() as abc:
                f()

        def f():
            for z in ():
                pass

            1/0

        class xxx(object):
            def __enter__(*args): pass
            def __exit__(*args): pass

        try:
            a()
        except:
            self.assert_traceback([(line_num + 16, 30, FILE, 'test_with_mixed_stack'),
                            (line_num + 3, 3, FILE, 'a'),
                            (line_num + 9, 3, FILE, 'f')])

    @unittest.skipIf(is_mono, "https://github.com/IronLanguages/ironpython3/issues/937")
    def test_cp11923_second(self):
        try:
            #Test setup
            _t_test = os.path.join(os.getcwd(), "cp11116_main.py")
            with open(_t_test, "w") as f:
                f.write("""import cp11116_a
try:
    cp11116_a.a()
except:
    pass

cp11116_a.a()
""")

            _t_test_a = os.path.join(os.getcwd(), "cp11116_a.py")
            with open(_t_test_a, "w") as f:
                f.write("""def a():
    raise None
""")

            #Actual test
            import subprocess
            p = subprocess.Popen([sys.executable, os.path.join(os.getcwd(), "cp11116_main.py")], shell=True, stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
            t_out, t_in, t_err = (p.stdin, p.stdout, p.stderr)
            lines = t_err.readlines()
            t_err.close()
            t_out.close()
            t_in.close()

            #Verification
            self.assertIn(b"cp11116_main.py\", line 7, in", lines[1])
            line_num = 3
            if is_cli:
                line_num -= 1
            self.assertTrue(lines[line_num].rstrip().endswith(b"cp11116_a.py\", line 2, in a"))

        finally:
            os.remove(_t_test)
            os.remove(_t_test_a)

run_test(__name__)
