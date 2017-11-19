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

import sys
import unittest

from iptest import IronPythonTestCase, is_cli, is_netcoreapp, run_test, skipUnlessIronPython

class CP35300_Derived(EnvironmentError):
    def __init__(self, *args, **kwargs):
        pass

class ExceptionTest(IronPythonTestCase):
    def setUp(self):
        super(ExceptionTest, self).setUp()
        self.assertEqual(sys.exc_info(), (None, None, None))

    def test_exception_line_no_with_finally(self):
        def f():
            try:
                raise Exception()   # this line should correspond w/ the number below
            finally:
                pass
        
        try:
            f()
        except Exception as e:
            tb = sys.exc_info()[2]
            expected = [34, 39]
            while tb:
                self.assertEqual(tb.tb_lineno, expected.pop()) # adding lines will require an update here
                tb = tb.tb_next
            
    @skipUnlessIronPython()
    def test_system_exception(self):
        import System
        
        def RaiseSystemException():
            raise System.SystemException()

        self.assertRaises(SystemError, RaiseSystemException)

    @skipUnlessIronPython()
    def test_raise(self):
        try:
             self.fail("Message")
        except AssertionError as e:
             self.assertEqual(e.__str__(), e.args[0])
        else:
            self.fail("Expected exception")

    def test_finally_continue_fails(self):
        t = '''
try:
    pass
finally:
    continue
'''
        try:
            compile(t, '<test>', 'exec')
            self.fail("Should raise SyntaxError")
        except SyntaxError:
            pass

    def test_finally_continue_in_loop_allowed(self):
        t = '''
try:
    pass
finally:
    for i in range(1):
        continue
'''
        try:
            compile(t, '<test>', 'exec')
        except SyntaxError:
            self.fail("Should not raise SyntaxError")

    def test_finally_continue_nested_finally_fails(self):
        t = '''
try:
    pass
finally:
    for i in range(1):
        try:
            pass
        finally:
            continue
'''
        try:
            compile(t, '<test>', 'exec')
            self.fail("Should raise SyntaxError")
        except SyntaxError:
            pass

    def test_bigint_division(self):
        def divide(a, b):
            try:
                c = a / b
                self.fail("Expected ZeroDivisionError for %r / %r == %r" % (a, b, c))
            except ZeroDivisionError:
                pass

            try:
                c = a % b
                self.fail("Expected ZeroDivisionError for %r %% %r == %r" % (a, b, c))
            except ZeroDivisionError:
                pass

            try:
                c = a // b
                self.fail("Expected ZeroDivisionError for %r // %r == %r" % (a, b, c))
            except ZeroDivisionError:
                pass

        big0 = 9999999999999999999999999999999999999999999999999999999999999999999999
        big0 = big0-big0

        pats = [0, 0, 0.0, big0, (0+0j)]
        nums = [42, 987654321, 7698736985726395723649587263984756239847562983745692837465928374569283746592837465923, 2352345324523532523, 5223523.3453, (10+25j)]

        for divisor in pats:
            for number in nums:
                divide(number, divisor)


    def test_handlers(self):
        handlers = []

        def a():
            try:
                b()
            finally:
                handlers.append("finally a")

        def b():
            try:
                c()
            finally:
                handlers.append("finally b")

        def c():
            try:
                d()
            finally:
                handlers.append("finally c")

        def d():
            sys.exit("abnormal termination")

        try:
            a()
        except SystemExit as e:
            handlers.append(e.args[0])

        self.assertTrue(handlers == ["finally c", "finally b", "finally a", "abnormal termination"])

    def test_sys_exit1(self):
        try:
            sys.exit()
            self.assertTrue(False)
        except SystemExit as e:
            self.assertEqual(len(e.args), 0)

    def test_sys_exit2(self):
        try:
            sys.exit(None)
            self.assertTrue(False)
        except SystemExit as e:
            self.assertEqual(e.args, ())

        self.assertEqual(SystemExit(None).args, (None,))

    def test_sys_exit3(self):
        try:
            sys.exit(-10)
        except SystemExit as e:
            self.assertEqual(e.code, -10)
            self.assertEqual(e.args, (-10,))
        else:
            self.assertTrue(False)

    @skipUnlessIronPython()
    def test_interop(self):
        self.load_iron_python_test()
        
        from IronPythonTest import ExceptionsTest
        import System
        import sys
        
        a = ExceptionsTest()
        
        try:
            a.ThrowException()  # throws index out of range
        except IndexError as e:
            self.assertTrue(e.__class__ == IndexError)
        
        class MyTest(ExceptionsTest):
            def VirtualFunc(self):
                raise ex("hello world")
        
        
        ex = ValueError
        
        
        a = MyTest()
        
        # raise in python, translate into .NET, catch in Python
        try:
            a.CallVirtual()
        except ex as e:
            self.assertTrue(e.__class__ == ValueError)
            self.assertTrue(e.args[0] == "hello world")
        
        # raise in python, catch in .NET, verify .NET got an ArgumentException
        
        try:
            x = a.CallVirtCatch()
        except ex as e:
            self.assertTrue(False)
        
        
        self.assertTrue(isinstance(x, System.ArgumentException))
        
        # call through the slow paths...
        
        try:
            a.CallVirtualOverloaded('abc')
        except ex as e:
            self.assertTrue(e.__class__ == ex)
            self.assertTrue(e.args[0] == "hello world")
        # Note that sys.exc_info() is still set
       
        try:
            a.CallVirtualOverloaded(5)
        except ex as e:
            self.assertTrue(e.__class__ == ex)
            self.assertTrue(e.args[0] == "hello world")
        
        
        
        try:
            a.CallVirtualOverloaded(a)
        except ex as e:
            self.assertTrue(e.__class__ == ex)
            self.assertTrue(e.args[0] == "hello world")
        
        
        # catch and re-throw (both throw again and rethrow)
        
        try:
            a.CatchAndRethrow()
        except ex as e:
            self.assertTrue(e.__class__ == ex)
            self.assertTrue(e.args[0] == "hello world")
        
        try:
            a.CatchAndRethrow2()
        except ex as e:
            self.assertTrue(e.__class__ == ex)
            self.assertTrue(e.args[0] == "hello world")
        
        
        
        class MyTest(ExceptionsTest):
            def VirtualFunc(self):
                self.ThrowException()
        
        a = MyTest()
        
        # start in python, call CLS which calls Python which calls CLS which raises the exception
        try:
            a.CallVirtual()  # throws index out of range
        except IndexError as e:
            self.assertTrue(e.__class__ == IndexError)
        
        
        # verify we can throw arbitrary classes
        class MyClass: pass
        
        try:
            raise MyClass
            self.assertTrue(False)
        except MyClass as mc:
            self.assertTrue(mc.__class__ == MyClass)
        
        # BUG 430 intern(None) should throw TypeError
        try:
            intern(None)
            self.assertTrue(False)
        except TypeError:
            pass
        # /BUG
        
        # BUG 393 exceptions throw when bad value passed to except
        try:
            try:
                raise SyntaxError("foo")
            except 12:
                self.assertTrue(False)
                pass
        except SyntaxError:
            pass
        # /BUG
        
        # BUG 319 IOError not raised.
        try:
            fp = file('thisfiledoesnotexistatall.txt')
        except IOError:
            pass
        # /BUG
        
        # verify we can raise & catch CLR exceptions
        try:
            raise System.Exception('Hello World')
        except System.Exception as e:
            self.assertTrue(type(e) == System.Exception)
        
        
        
        # BUG 481 Trying to pass raise in Traceback should cause an error until it is implemented
        try:
            raise StopIteration("BadTraceback")("somedata").with_traceback("a string is not a traceback")
            self.assertTrue (False, "fell through raise for some reason")
        except StopIteration:
            self.assertTrue(False)
        except TypeError:
            pass
        
        try:
            raise TypeError
        except:
            import sys
            if (sys.exc_info()[2] != None):
                x = dir(sys.exc_info()[2])
                for name in ['tb_frame', 'tb_lasti', 'tb_lineno', 'tb_next']:
                    self.assertTrue(name in x, name)
                try:
                    raise Exception("foo")("Msg").with_traceback(sys.exc_info()[2])
                except Exception as X:
                    pass
        
                          
        
        try:
            raise Exception(3,4,5)
        except Exception as X:
            self.assertEqual(X[0], 3)
            self.assertEqual(X[1], 4)
            self.assertEqual(X[2], 5)
        
        
        try:
            raise Exception
        except:
            import exceptions
            self.assertEqual(sys.exc_info()[0], exceptions.Exception)
            self.assertEqual(sys.exc_info()[1].__class__, exceptions.Exception)
            
        try:
            self.fail("message")
        except AssertionError as e:
            import exceptions
            
            self.assertEqual(e.__class__, exceptions.AssertionError)
            self.assertEqual(len(e.args), 1)
            self.assertEqual(e.args[0], "message")
        else:
            self.fail("Expected exception")

#####################################################################################
# __str__ behaves differently for exceptions because of implementation (ExceptionConverter.ExceptionToString)

# TODO: doesn't work in IronPython
#def test_str1():
#    AssertErrorWithMessage(TypeError, "descriptor '__str__' of 'exceptions.BaseException' object needs an argument", Exception.__str__)
#    AssertErrorWithMessage(TypeError, "descriptor '__str__' requires a 'exceptions.BaseException' object but received a 'list'", Exception.__str__, list())
#    AssertErrorWithMessage(TypeError, "descriptor '__str__' requires a 'exceptions.BaseException' object but received a 'list'", Exception.__str__, list(), 1)
#    AssertErrorWithMessage(TypeError, "expected 0 arguments, got 1", Exception.__str__, Exception(), 1)

    def test_str2(self):
        # verify we can assign to sys.exc_*
        sys.exc_info()[2] = None
        sys.exc_info()[1] = None
        sys.exc_info()[0] = None

        self.assertEqual(str(Exception()), '')

        @skipUnlessIronPython()
        def test_array(self):
            import System
            try:
                a = System.Array()
            except Exception as e:
                self.assertEqual(e.__class__, TypeError)
            else:
                self.assertTrue(False, "FAILED!")

    def test_assert_error(self):
        self.assertRaises(ValueError, chr, -1)
        self.assertRaises(TypeError, None)

    def test_dir(self):
        testingdir = 10
        self.assertTrue('testingdir' in dir())
        del testingdir
        self.assertTrue(not 'testingdir' in dir())

    def test_assert(self):
        try:
            self.assertTrue(False, "Failed message")
        except AssertionError as e:
            self.assertTrue(e.args[0] == "Failed message")
        else:
            self.fail("should have thrown")

        try:
            self.assertTrue(False, "Failed message 2")
        except AssertionError as e:
            self.assertTrue(e.args[0] == "Failed message 2")
        else:
            self.fail("should have thrown")


    def test_syntax_error_exception(self):
        try: 
            compile('a = """\n\n', 'foo', 'single', 0x200)
        except SyntaxError as se: 
            self.assertEqual(se.offset, 9)
        
        try: 
            compile('a = """\n\nxxxx\nxxx\n', 'foo', 'single', 0x200)
        except SyntaxError as se: 
            self.assertEqual(se.offset, 18)

        try: 
            compile('abc\na = """\n\n', 'foo', 'exec', 0x200)
        except SyntaxError as se: 
            self.assertEqual(se.offset, 9)

        try:
            compile("if 2==2: x=2\nelse:y=", "Error", "exec")
        except SyntaxError as se:
            l1 = dir(se)
            self.assertTrue('lineno' in l1)
            self.assertTrue('offset' in l1)
            self.assertTrue('filename' in l1)
            self.assertTrue('text' in l1)
            if is_cli:
                import clr
                l2 = dir(se.clsException)
                self.assertTrue('Line' in l2)
                self.assertTrue('Column' in l2)
                self.assertTrue('GetSymbolDocumentName' in l2)
                self.assertTrue('GetCodeLine' in l2)
            self.assertEqual(se.lineno, 2)
            # Bug 1132
            #self.assertEqual(se.offset, 7)
            self.assertEqual(se.filename, "Error")
            if is_cli: # https://github.com/IronLanguages/main/issues/843
                self.assertEqual(se.text, "else:y=")
            else:
                self.assertEqual(se.text, "else:y=\n")
            if is_cli:
                self.assertEqual(se.clsException.Line, 2)
                # Bug 1132
                #self.assertEqual(se.clsException.Column, 7)
                self.assertEqual(se.clsException.GetSymbolDocumentName(), "Error")
                self.assertEqual(se.clsException.GetCodeLine(), "else:y=")
            self.assertEqual(se.__dict__, {})
            self.assertEqual(type(se.__dict__), dict)
    
    def test_syntax_error_exception_exec(self):
        try:
            compile("if 2==2: x=", "Error", "exec")
        except SyntaxError as se:
            self.assertEqual(se.lineno, 1)
            # Bug 1132
            #self.assertEqual(se.offset, 11)
            self.assertEqual(se.filename, "Error")
            if is_cli: # https://github.com/IronLanguages/main/issues/843
                self.assertEqual(se.text, "if 2==2: x=")
            else:
                self.assertEqual(se.text, "if 2==2: x=\n")
            self.assertEqual(se.__dict__, {})
            self.assertEqual(type(se.__dict__), dict)
        
    def test_syntax_error_exception_eval(self):
        try:
            compile("if 2==2: x=", "Error", "eval")
        except SyntaxError as se:
            self.assertEqual(se.lineno, 1)
            # Bug 1132
            #self.assertEqual(se.offset, 2)
            self.assertEqual(se.filename, "Error")
            self.assertEqual(se.text, "if 2==2: x=")
            self.assertEqual(se.__dict__, {})
            self.assertEqual(type(se.__dict__), dict)
        

    def test_user_syntax_error_exception(self):
        x = SyntaxError()
        self.assertEqual(x.lineno, None)
        self.assertEqual(x.filename, None)
        self.assertEqual(x.msg, None)
        self.assertEqual(x.message, '')
        self.assertEqual(x.offset, None)
        self.assertEqual(x.print_file_and_line, None)
        self.assertEqual(x.text, None)
        #Run a few minimal tests to ensure the __dict__ member works OK
        self.assertEqual(x.__dict__, {})
        self.assertEqual(type(x.__dict__), dict)
        x.arbitrary = 3.14
        self.assertEqual(x.__dict__["arbitrary"], 3.14)
        del x.__dict__["arbitrary"]
        self.assertEqual(x.__dict__, {})
        
        x = SyntaxError('hello')
        self.assertEqual(x.lineno, None)
        self.assertEqual(x.filename, None)
        self.assertEqual(x.msg, 'hello')
        self.assertEqual(x.message, 'hello')
        self.assertEqual(x.offset, None)
        self.assertEqual(x.print_file_and_line, None)
        self.assertEqual(x.text, None)
        
        x = SyntaxError('hello', (1,2,3,4))
        self.assertEqual(x.lineno, 2)
        self.assertEqual(x.filename, 1)
        self.assertEqual(x.msg, 'hello')
        self.assertEqual(x.message, '')
        self.assertEqual(x.offset, 3)
        self.assertEqual(x.print_file_and_line, None)
        self.assertEqual(x.text, 4)
        
        self.assertRaises(IndexError, SyntaxError, 'abc', ())
        self.assertRaises(IndexError, SyntaxError, 'abc', (1,))
        self.assertRaises(IndexError, SyntaxError, 'abc', (1,2))
        self.assertRaises(IndexError, SyntaxError, 'abc', (1,2,3))
    
    def test_return(self):
        def test_func():
            try: pass
            finally:
                try: raise 'foo'
                except:
                    return 42
                    
        self.assertEqual(test_func(), 42)
                
        def test_func():
            try: pass
            finally:
                try: raise 'foo'
                except:
                    try: raise 'foo'
                    except:
                        return 42

        self.assertEqual(test_func(), 42)
        
        def test_func():
            try: pass
            finally:
                try: pass
                finally:
                    try: raise 'foo'
                    except:
                        try: raise 'foo'
                        except:
                            return 42

        self.assertEqual(test_func(), 42)

        def test_func():
            try: raise 'foo'
            except:
                try: pass
                finally:
                    try: raise 'foo'
                    except:
                        try: raise 'foo'
                        except:
                            return 42

        self.assertEqual(test_func(), 42)

    def test_break_and_continue(self):
        class stateobj(object):
            __slots__ = ['loops', 'finallyCalled']
            def __init__(self):
                self.loops = 0
                self.finallyCalled = False
                
        def test_break(state):
            try:
                try:
                    raise Exception()
                except:
                    for n in range(10):
                        state.loops += 1
                        break
                return 42
            except: pass
        
        
        def test_continue(state):
            try:
                try:
                    raise Exception()
                except:
                    for n in range(10):
                        state.loops += 1
                        continue
                return 42
            except: pass
            
        
        
        def test_multi_break(state):
            try:
                try:
                    raise Exception()
                except:
                    for n in range(10):
                        state.loops += 1
                        if False: break
                        
                        break
        
                return 42
            except: pass
            
            
        def test_multi_continue(state):
            try:
                try:
                    raise Exception()
                except:
                    for n in range(10):
                        state.loops += 1
                        if False: continue
                        
                        continue
        
                return 42
            except: pass
                
        state = stateobj()
        self.assertEqual(test_break(state), 42)
        self.assertEqual(state.loops, 1)
        
        state = stateobj()
        self.assertEqual(test_continue(state), 42)
        self.assertEqual(state.loops, 10)
        
        state = stateobj()
        self.assertEqual(test_multi_break(state), 42)
        self.assertEqual(state.loops, 1)
        
        state = stateobj()
        self.assertEqual(test_multi_continue(state), 42)
        self.assertEqual(state.loops, 10)
        
        def test_break_in_finally_raise(state):
            for x in range(10):
                try:
                    raise 'foo'
                finally:
                    state.finallyCalled = True
                    break
            return 42

        def test_break_in_finally(state):
            for x in range(10):
                try: pass
                finally:
                    state.finallyCalled = True
                    break
            return 42

        state = stateobj()
        self.assertEqual(test_break_in_finally_raise(state), 42)
        self.assertEqual(state.finallyCalled, True)
        
        state = stateobj()
        self.assertEqual(test_break_in_finally(state), 42)
        self.assertEqual(state.finallyCalled, True)

        def test_outer_for_with_finally(state, shouldRaise):
            for x in range(10):
                try:
                    try:
                        if shouldRaise:
                            raise 'hello world'
                    finally:
                        state.finallyCalled = True
                        break
                except:
                    pass
                raise 'bad!!!'
            return 42
            
        state = stateobj()
        self.assertEqual(test_outer_for_with_finally(state, False), 42)
        self.assertEqual(state.finallyCalled, True)
            
        state = stateobj()
        self.assertEqual(test_outer_for_with_finally(state, True), 42)
        self.assertEqual(state.finallyCalled, True)
        
        def test_outer_for_with_finally(state, shouldRaise):
            for x in range(10):
                try:
                    try:
                        if shouldRaise:
                            raise 'hello world'
                    finally:
                        state.finallyCalled = True
                        break
                except:
                    pass
                raise 'bad!!!'
            return 42
        
        state = stateobj()
        self.assertEqual(test_outer_for_with_finally(state, False), 42)
        self.assertEqual(state.finallyCalled, True)
            
        state = stateobj()
        self.assertEqual(test_outer_for_with_finally(state, True), 42)
        self.assertEqual(state.finallyCalled, True)

    @unittest.skipIf(is_netcoreapp, 'no System.AppDomain')
    def test_serializable_clionly(self):
        import clr
        import System
        from IronPythonTest import ExceptionsTest
        path = clr.GetClrType(ExceptionsTest).Assembly.Location
        mbro = System.AppDomain.CurrentDomain.CreateInstanceFromAndUnwrap(path, "IronPythonTest.EngineTest")
        self.assertRaises(AssertionError, mbro.Run, 'raise AssertionError')
        import exceptions
        
        for eh in dir(exceptions):
            eh = getattr(exceptions, eh)
            if isinstance(eh, type) and issubclass(eh, BaseException):
                # unicode exceptions require more args...
                if (eh.__name__ != 'UnicodeDecodeError' and 
                    eh.__name__ != 'UnicodeEncodeError' and 
                    eh.__name__ != 'UnicodeTranslateError'):
                    self.assertRaises(eh, mbro.Run, 'raise ' + eh.__name__)

    def test_sanity(self):
        '''
        Sanity checks to ensure all exceptions implemented can be created/thrown/etc
        in the standard ways.
        '''
        #build up a list of all valid exceptions
        import exceptions
        #special cases - do not test these like everything else
        special_types = [ "UnicodeTranslateError", "UnicodeEncodeError", "UnicodeDecodeError"]
        exception_types = [ x for x in list(exceptions.__dict__.keys()) if x.startswith("__")==False and special_types.count(x)==0]
        exception_types = [ eval("exceptions." + x) for x in exception_types]
        
        #run a few sanity checks
        for exception_type in exception_types:
            except_list = [exception_type(), exception_type("a single param")]
            
            for t_except in except_list:
                try:
                    raise t_except
                except exception_type as e:
                    pass
                
                str_except = str(t_except)
                
                #there is no __getstate__ method of exceptions...
                self.assertTrue(not hasattr(t_except, '__getstate__'))
        
        #special cases
        encode_except = exceptions.UnicodeEncodeError("1", "2", 3, 4, "5")
        self.assertEqual(encode_except.encoding, "1")
        self.assertEqual(encode_except.object, "2")
        self.assertEqual(encode_except.start, 3)
        self.assertEqual(encode_except.end, 4)
        self.assertEqual(encode_except.reason, "5")
        self.assertEqual(encode_except.message, "")
        
        #CodePlex Work Item 356
        #self.assertRaises(TypeError, exceptions.UnicodeDecodeError, "1", u"2", 3, 4, "e")
        exceptions.UnicodeDecodeError("1", "2", 3, 4, "e")
        
        decode_except = exceptions.UnicodeDecodeError("1", "2", 3, 4, "5")
        self.assertEqual(decode_except.encoding, "1")
        self.assertEqual(decode_except.object, "2")
        self.assertEqual(decode_except.start, 3)
        self.assertEqual(decode_except.end, 4)
        self.assertEqual(decode_except.reason, "5")
        self.assertEqual(decode_except.message, "")
        
        translate_except = exceptions.UnicodeTranslateError("1", 2, 3, "4")
        self.assertEqual(translate_except.object, "1")
        self.assertEqual(translate_except.start, 2)
        self.assertEqual(translate_except.end, 3)
        self.assertEqual(translate_except.reason, "4")
        self.assertEqual(translate_except.message, "")
        self.assertEqual(translate_except.encoding, None)

    def test_nested_exceptions(self):
        try:
            raise Exception()
        except Exception as e:
            # PushException
            try:
                raise TypeError
            except TypeError as te:
                # PushException
                ei = sys.exc_info()
                # PopException
            ei2 = sys.exc_info()
            self.assertEqual(ei, ei2)
        ei3 = sys.exc_info()
        self.assertEqual(ei, ei3)

    def test_swallow_from_else(self):
        def f():
            try:
                pass
            except:
                pass
            else:
                raise AttributeError
            finally:
                return 4
                
        self.assertEqual(f(), 4)

    def test_newstyle_raise(self):
        # raise a new style exception via raise type, value that returns an arbitrary object
        class MyException(Exception):
            def __new__(cls, *args): return 42
            
        try:
            raise MyException('abc')
            AssertUnreachable()
        except Exception as e:
            self.assertEqual(e, 42)

    def test_enverror_init(self):
        x = EnvironmentError()
        self.assertEqual(x.message, '')
        self.assertEqual(x.errno, None)
        self.assertEqual(x.filename, None)
        self.assertEqual(x.strerror, None)
        self.assertEqual(x.args, ())
        
        x.__init__('abc')
        self.assertEqual(x.message, 'abc')
        self.assertEqual(x.args, ('abc', ))
        
        x.__init__('123', '456')
        self.assertEqual(x.message, 'abc')
        self.assertEqual(x.errno, '123')
        self.assertEqual(x.strerror, '456')
        self.assertEqual(x.args, ('123', '456'))
        
        x.__init__('def', 'qrt', 'foo')
        self.assertEqual(x.message, 'abc')
        self.assertEqual(x.errno, 'def')
        self.assertEqual(x.strerror, 'qrt')
        self.assertEqual(x.filename, 'foo')
        self.assertEqual(x.args, ('def', 'qrt')) # filename not included in args
        
        x.__init__()
        self.assertEqual(x.message, 'abc')
        self.assertEqual(x.errno, 'def')
        self.assertEqual(x.strerror, 'qrt')
        self.assertEqual(x.filename, 'foo')
        self.assertEqual(x.args, ())

        x.__init__('1', '2', '3', '4')
        self.assertEqual(x.message, 'abc')
        self.assertEqual(x.errno, 'def')
        self.assertEqual(x.strerror, 'qrt')
        self.assertEqual(x.filename, 'foo')
        self.assertEqual(x.args, ('1', '2', '3', '4'))
        
        x = EnvironmentError('a', 'b', 'c', 'd')
        self.assertEqual(x.message, '')
        self.assertEqual(x.errno, None)
        self.assertEqual(x.filename, None)
        self.assertEqual(x.strerror, None)
        self.assertEqual(x.args, ('a', 'b', 'c', 'd'))

    def test_raise_None(self):
        lineno1, lineno2 = 0, 0
        try:
            raise None
        except:
            lineno1 = sys.exc_info()[2].tb_lineno
        
        try:
            # dummy line
            raise None
        except:
            lineno2 = sys.exc_info()[2].tb_lineno
        
        self.assertTrue(lineno1 != lineno2, "FAILED! Should not have reused exception")

    def test_exception_setstate(self):
        x = BaseException()
        self.assertEqual(x.__dict__, {})
        x.__setstate__({'a' : 1, 'b' : 2})
        self.assertEqual(x.__dict__, {'a' : 1, 'b' : 2})
        x.__setstate__({'a' : 3, 'c' : 4})
        self.assertEqual(x.__dict__, {'a' : 3, 'b' : 2, 'c' : 4})

    def test_deprecated_string_exception(self):
        try:
            raise 'foo'
        except TypeError as e:
            print(e.message)
            # TODO: re-enable this when https://github.com/IronLanguages/ironpython2/issues/10 is fixed
            # self.assertEqual(e.message, 'exceptions must be old-style classes or derived from BaseException, not str')

        class SomeClass(object):
            pass

        try:
            raise SomeClass()
        except TypeError as e:
            print(e.message)
            # TODO: re-enable this when https://github.com/IronLanguages/ironpython2/issues/10 is fixed
            # self.assertEqual(e.message, 'exceptions must be old-style classes or derived from BaseException, not SomeClass')


    def test_nested_try(self):
        global l
        l = []
        def foo():
            try:
                try:
                    l.append(1)
                except:
                    pass
            except:
                l.append(2)
            else:
                l.append(3)
        
        foo()
        self.assertEqual(l, [1, 3])
        l = []
        
        def bar():
            try:
                l.append(1)
            except:
                l.append(2)
            else:
                l.append(3)
                
        bar()
        self.assertEqual(l, [1, 3])

    def test_module_exceptions(self):
        """verify exceptions in modules are like user defined exception objects, not built-in types."""
        
        # these modules have normal types...
        normal_types = ['sys', 'clr', 'exceptions', '__builtin__', '_winreg', 'mmap', 'nt', 'posix']       
        builtins = [x for x in sys.builtin_module_names if x not in normal_types ]
        for module in builtins:
            mod = __import__(module)
            
            for attrName in dir(mod):
                val = getattr(mod, attrName)
                if isinstance(val, type) and issubclass(val, Exception):
                    if "BlockingIOError" not in repr(val): 
                        self.assertTrue(repr(val).startswith("<class "))
                        val.x = 2
                        self.assertEqual(val.x, 2)
                    else:
                        self.assertTrue(repr(val).startswith("<type "))

    def test_raise_inside_str(self):
        #raising an error inside the __str__ used to cause an unhandled exception.
        class error(Exception):
            def __str__(self):
                raise TypeError("inside __str__")

        def f():
            raise error
        self.assertRaises(error, f)

    def test_exception_doc(self):
        # should be accessible, CPython and IronPython have different strings though.
        Exception().__doc__
        Exception("abc").__doc__

    def test_repr_not_called(self):
        """__repr__ shouldn't be called when message is a tuple w/ multiple args"""
        class x(object):
            def __repr__(self):
                raise StopIteration('repr should not be called')
        
        try:
            sys.exit((x(), x()))
        except SystemExit:
            pass

    def test_windows_error(self):
        # int is required for 2/3 params
        self.assertRaises(TypeError, WindowsError, 'foo', 'bar')
        self.assertRaises(TypeError, WindowsError, 'foo', 'bar', 'baz')
        
        err = WindowsError('foo', 'bar', 'baz', 'quox')
        self.assertEqual(err.errno, None)
        self.assertEqual(err.winerror, None)
        self.assertEqual(err.filename, None)
        self.assertEqual(err.strerror, None)
        self.assertEqual(err.args, ('foo', 'bar', 'baz', 'quox'))
        
        err = WindowsError(42, 'bar', 'baz')
        self.assertEqual(err.filename, 'baz')
        self.assertEqual(err.winerror, 42)
        self.assertEqual(err.strerror, 'bar')
        self.assertEqual(err.args, (42, 'bar'))

        # winerror code is passed through unmodified
        for i in range(256):
            x = WindowsError(i, 'foo')
            self.assertEqual(x.winerror, i)
        
        # winerror code is mapped to Python error code
        self.assertEqual(WindowsError(10, 'foo').errno, 7)

    def test_derived_keyword_args(self):
        class ED(Exception):
            def __init__(self, args=''):
                pass
        
        self.assertEqual(type(ED(args='')), ED)

    def test_cp35300(self):
        # make sure that classes derived from builtin exceptions which have
        # generated implementation do not restrict parameters of __init__
        self.assertNotEqual(None, CP35300_Derived("a", x="b"))

    def test_issue1164(self):
        class error(Exception):
            pass

        def f():
            raise error(0)

        self.assertRaises(error, f)

run_test(__name__)
