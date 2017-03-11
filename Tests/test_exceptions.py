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
from iptest.warning_util import warning_trapper
import sys

AreEqual(sys.exc_info(), (None, None, None))

def test_exception_line_no_with_finally():
    def f():
        try:
            raise Exception()   # this line should correspond w/ the number below
        finally:
            pass
    
    try:
        f()
    except Exception as e:
        tb = sys.exc_info()[2]
        expected = [25, 30]
        while tb:
            AreEqual(tb.tb_lineno, expected.pop()) # adding lines will require an update here
            tb = tb.tb_next
            
if is_cli or is_silverlight:
    def test_system_exception():
        import System
        
        def RaiseSystemException():
            raise System.SystemException()

        AssertError(SystemError, RaiseSystemException)

    AreEqual(sys.exc_info(), (None, None, None))

if is_cli or is_silverlight:
    def test_raise():
        try:
             Fail("Message")
        except AssertionError as e:
             AreEqual(e.__str__(), e.args[0])
        else:
            Fail("Expected exception")

def test_finally_continue_fails():
    t = '''
try:
    pass
finally:
    continue
'''
    try:
        compile(t, '<test>', 'exec')
        Fail("Should raise SyntaxError")
    except SyntaxError:
        pass

def test_finally_continue_in_loop_allowed():
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
        Fail("Should not raise SyntaxError")

def test_finally_continue_nested_finally_fails():
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
        Fail("Should raise SyntaxError")
    except SyntaxError:
        pass

def test_bigint_division():
    def divide(a, b):
        try:
            c = a / b
            Fail("Expected ZeroDivisionError for %r / %r == %r" % (a, b, c))
        except ZeroDivisionError:
            pass

        try:
            c = a % b
            Fail("Expected ZeroDivisionError for %r %% %r == %r" % (a, b, c))
        except ZeroDivisionError:
            pass

        try:
            c = a // b
            Fail("Expected ZeroDivisionError for %r // %r == %r" % (a, b, c))
        except ZeroDivisionError:
            pass

    big0 = 9999999999999999999999999999999999999999999999999999999999999999999999
    big0 = big0-big0

    pats = [0, 0, 0.0, big0, (0+0j)]
    nums = [42, 987654321, 7698736985726395723649587263984756239847562983745692837465928374569283746592837465923, 2352345324523532523, 5223523.3453, (10+25j)]

    for divisor in pats:
        for number in nums:
            divide(number, divisor)


# sys.exit() test

def test_handlers():
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

    Assert(handlers == ["finally c", "finally b", "finally a", "abnormal termination"])

def test_sys_exit1():
    try:
        sys.exit()
        Assert(False)
    except SystemExit as e:
        AreEqual(len(e.args), 0)

def test_sys_exit2():
    try:
        sys.exit(None)
        Assert(False)
    except SystemExit as e:
        AreEqual(e.args, ())

    AreEqual(SystemExit(None).args, (None,))

def test_sys_exit3():
    try:
        sys.exit(-10)
    except SystemExit as e:
        AreEqual(e.code, -10)
        AreEqual(e.args, (-10,))
    else:
        Assert(False)

################################
# exception interop tests
if is_cli or is_silverlight:
    def test_interop():
        load_iron_python_test()
        
        from IronPythonTest import ExceptionsTest
        import System
        import sys
        
        a = ExceptionsTest()
        
        try:
            a.ThrowException()  # throws index out of range
        except IndexError as e:
            Assert(e.__class__ == IndexError)
        
        class MyTest(ExceptionsTest):
            def VirtualFunc(self):
                raise ex("hello world")
        
        
        ex = ValueError
        
        
        a = MyTest()
        
        # raise in python, translate into .NET, catch in Python
        try:
            a.CallVirtual()
        except ex as e:
            Assert(e.__class__ == ValueError)
            Assert(e.args[0] == "hello world")
        
        # raise in python, catch in .NET, verify .NET got an ArgumentException
        
        try:
            x = a.CallVirtCatch()
        except ex as e:
            Assert(False)
        
        
        Assert(isinstance(x, System.ArgumentException))
        
        # call through the slow paths...
        
        try:
            a.CallVirtualOverloaded('abc')
        except ex as e:
            Assert(e.__class__ == ex)
            Assert(e.args[0] == "hello world")
        # Note that sys.exc_info() is still set
       
        try:
            a.CallVirtualOverloaded(5)
        except ex as e:
            Assert(e.__class__ == ex)
            Assert(e.args[0] == "hello world")
        
        
        
        try:
            a.CallVirtualOverloaded(a)
        except ex as e:
            Assert(e.__class__ == ex)
            Assert(e.args[0] == "hello world")
        
        
        # catch and re-throw (both throw again and rethrow)
        
        try:
            a.CatchAndRethrow()
        except ex as e:
            Assert(e.__class__ == ex)
            Assert(e.args[0] == "hello world")
        
        try:
            a.CatchAndRethrow2()
        except ex as e:
            Assert(e.__class__ == ex)
            Assert(e.args[0] == "hello world")
        
        
        
        class MyTest(ExceptionsTest):
            def VirtualFunc(self):
                self.ThrowException()
        
        a = MyTest()
        
        # start in python, call CLS which calls Python which calls CLS which raises the exception
        try:
            a.CallVirtual()  # throws index out of range
        except IndexError as e:
            Assert(e.__class__ == IndexError)
        
        
        # verify we can throw arbitrary classes
        class MyClass: pass
        
        try:
            raise MyClass
            Assert(False)
        except MyClass as mc:
            Assert(mc.__class__ == MyClass)
        
        # BUG 430 intern(None) should throw TypeError
        try:
            sys.intern(None)
            Assert(False)
        except TypeError:
            pass
        # /BUG
        
        
        # BUG 393 exceptions throw when bad value passed to except
        try:
            try:
                raise SyntaxError("foo")
            except 12:
                Assert(False)
                pass
        except SyntaxError:
            pass
        # /BUG
        
        # BUG 319 IOError not raised.
        if is_silverlight==False:
            try:
                fp = file('thisfiledoesnotexistatall.txt')
            except IOError:
                pass
        # /BUG
        
        # verify we can raise & catch CLR exceptions
        try:
            raise System.Exception('Hello World')
        except System.Exception as e:
            Assert(type(e) == System.Exception)
        
        
        
        # BUG 481 Trying to pass raise in Traceback should cause an error until it is implemented
        try:
            raise StopIteration("BadTraceback")("somedata").with_traceback("a string is not a traceback")
            Assert (False, "fell through raise for some reason")
        except StopIteration:
            Assert(False)
        except TypeError:
            pass
        
        try:
            raise TypeError
        except:
            import sys
            if (sys.exc_info()[2] != None):
                x = dir(sys.exc_info()[2])
                for name in ['tb_frame', 'tb_lasti', 'tb_lineno', 'tb_next']:
                    Assert(name in x, name)
                try:
                    raise Exception("foo")("Msg").with_traceback(sys.exc_info()[2])
                except Exception as X:
                    pass
        
                          
        
        try:
            raise Exception(3,4,5)
        except Exception as X:
            AreEqual(X[0], 3)
            AreEqual(X[1], 4)
            AreEqual(X[2], 5)
        
        
        try:
            raise Exception
        except:
            import exceptions
            AreEqual(sys.exc_info()[0], exceptions.Exception)
            AreEqual(sys.exc_info()[1].__class__, exceptions.Exception)
            
        try:
            Fail("message")
        except AssertionError as e:
            import exceptions
            
            AreEqual(e.__class__, exceptions.AssertionError)
            AreEqual(len(e.args), 1)
            AreEqual(e.args[0], "message")
        else:
            Fail("Expected exception")

#####################################################################################
# __str__ behaves differently for exceptions because of implementation (ExceptionConverter.ExceptionToString)

# TODO: doesn't work in IronPython
#def test_str1():
#    AssertErrorWithMessage(TypeError, "descriptor '__str__' of 'exceptions.BaseException' object needs an argument", Exception.__str__)
#    AssertErrorWithMessage(TypeError, "descriptor '__str__' requires a 'exceptions.BaseException' object but received a 'list'", Exception.__str__, list())
#    AssertErrorWithMessage(TypeError, "descriptor '__str__' requires a 'exceptions.BaseException' object but received a 'list'", Exception.__str__, list(), 1)
#    AssertErrorWithMessage(TypeError, "expected 0 arguments, got 1", Exception.__str__, Exception(), 1)

def test_str2():
    # verify we can assign to sys.exc_*
    sys.exc_info()[2] = None
    sys.exc_info()[1] = None
    sys.exc_info()[0] = None

    AreEqual(str(Exception()), '')

#####################################################################

if is_cli or is_silverlight:
    def test_array():
        import System
        try:
            a = System.Array()
        except Exception as e:
            AreEqual(e.__class__, TypeError)
        else:
            Assert(False, "FAILED!")

def test_assert_error():
    AssertError(ValueError, chr, -1)
    AssertError(TypeError, None)

def test_dir():
    testingdir = 10
    Assert('testingdir' in dir())
    del testingdir
    Assert(not 'testingdir' in dir())

def test_assert():
    try:
        Assert(False, "Failed message")
    except AssertionError as e:
        Assert(e.args[0] == "Failed message")
    else:
        Fail("should have thrown")

    try:
        Assert(False, "Failed message 2")
    except AssertionError as e:
        Assert(e.args[0] == "Failed message 2")
    else:
        Fail("should have thrown")


def test_syntax_error_exception():
    try: 
        compile('a = """\n\n', 'foo', 'single', 0x200)
    except SyntaxError as se: 
        AreEqual(se.offset, 9)
    
    try: 
        compile('a = """\n\nxxxx\nxxx\n', 'foo', 'single', 0x200)
    except SyntaxError as se: 
        AreEqual(se.offset, 18)

    try: 
        compile('abc\na = """\n\n', 'foo', 'exec', 0x200)
    except SyntaxError as se: 
        AreEqual(se.offset, 9)

    try:
        compile("if 2==2: x=2\nelse:y=", "Error", "exec")
    except SyntaxError as se:
        l1 = dir(se)
        Assert('lineno' in l1)
        Assert('offset' in l1)
        Assert('filename' in l1)
        Assert('text' in l1)
        if is_cli or is_silverlight:
            import clr
            l2 = dir(se.clsException)
            Assert('Line' in l2)
            Assert('Column' in l2)
            Assert('GetSymbolDocumentName' in l2)
            Assert('GetCodeLine' in l2)
        AreEqual(se.lineno, 2)
        # Bug 1132
        #AreEqual(se.offset, 7)
        AreEqual(se.filename, "Error")
        if is_ironpython: #http://ironpython.codeplex.com/workitem/27989
            AreEqual(se.text, "else:y=")
        else:
            AreEqual(se.text, "else:y=\n")
        if is_cli or is_silverlight:
            AreEqual(se.clsException.Line, 2)
            # Bug 1132
            #AreEqual(se.clsException.Column, 7)
            AreEqual(se.clsException.GetSymbolDocumentName(), "Error")
            AreEqual(se.clsException.GetCodeLine(), "else:y=")
        AreEqual(se.__dict__, {})
        AreEqual(type(se.__dict__), dict)
    
def test_syntax_error_exception_exec():
    try:
        compile("if 2==2: x=", "Error", "exec")
    except SyntaxError as se:
        AreEqual(se.lineno, 1)
        # Bug 1132
        #AreEqual(se.offset, 11)
        AreEqual(se.filename, "Error")
        if is_ironpython: #http://ironpython.codeplex.com/workitem/27989
            AreEqual(se.text, "if 2==2: x=")
        else:
            AreEqual(se.text, "if 2==2: x=\n")
        AreEqual(se.__dict__, {})
        AreEqual(type(se.__dict__), dict)
        
def test_syntax_error_exception_eval():
    try:
        compile("if 2==2: x=", "Error", "eval")
    except SyntaxError as se:
        AreEqual(se.lineno, 1)
        # Bug 1132
        #AreEqual(se.offset, 2)
        AreEqual(se.filename, "Error")
        AreEqual(se.text, "if 2==2: x=")
        AreEqual(se.__dict__, {})
        AreEqual(type(se.__dict__), dict)
        

def test_user_syntax_error_exception():
    x = SyntaxError()
    AreEqual(x.lineno, None)
    AreEqual(x.filename, None)
    AreEqual(x.msg, None)
    AreEqual(x.message, '')
    AreEqual(x.offset, None)
    AreEqual(x.print_file_and_line, None)
    AreEqual(x.text, None)
    #Run a few minimal tests to ensure the __dict__ member works OK
    AreEqual(x.__dict__, {})
    AreEqual(type(x.__dict__), dict)
    x.arbitrary = 3.14
    AreEqual(x.__dict__["arbitrary"], 3.14)
    del x.__dict__["arbitrary"]
    AreEqual(x.__dict__, {})
    
    x = SyntaxError('hello')
    AreEqual(x.lineno, None)
    AreEqual(x.filename, None)
    AreEqual(x.msg, 'hello')
    AreEqual(x.message, 'hello')
    AreEqual(x.offset, None)
    AreEqual(x.print_file_and_line, None)
    AreEqual(x.text, None)
    
    x = SyntaxError('hello', (1,2,3,4))
    AreEqual(x.lineno, 2)
    AreEqual(x.filename, 1)
    AreEqual(x.msg, 'hello')
    AreEqual(x.message, '')
    AreEqual(x.offset, 3)
    AreEqual(x.print_file_and_line, None)
    AreEqual(x.text, 4)
    
    AssertError(IndexError, SyntaxError, 'abc', ())
    AssertError(IndexError, SyntaxError, 'abc', (1,))
    AssertError(IndexError, SyntaxError, 'abc', (1,2))
    AssertError(IndexError, SyntaxError, 'abc', (1,2,3))
    
def test_return():
    def test_func():
        try: pass
        finally:
            try: raise 'foo'
            except:
                return 42
                
    AreEqual(test_func(), 42)
            
    def test_func():
        try: pass
        finally:
            try: raise 'foo'
            except:
                try: raise 'foo'
                except:
                    return 42

    AreEqual(test_func(), 42)
    
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

    AreEqual(test_func(), 42)

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

    AreEqual(test_func(), 42)

def test_break_and_continue():
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
    AreEqual(test_break(state), 42)
    AreEqual(state.loops, 1)
    
    state = stateobj()
    AreEqual(test_continue(state), 42)
    AreEqual(state.loops, 10)
    
    state = stateobj()
    AreEqual(test_multi_break(state), 42)
    AreEqual(state.loops, 1)
    
    state = stateobj()
    AreEqual(test_multi_continue(state), 42)
    AreEqual(state.loops, 10)
    
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
    AreEqual(test_break_in_finally_raise(state), 42)
    AreEqual(state.finallyCalled, True)
    
    state = stateobj()
    AreEqual(test_break_in_finally(state), 42)
    AreEqual(state.finallyCalled, True)

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
    AreEqual(test_outer_for_with_finally(state, False), 42)
    AreEqual(state.finallyCalled, True)
        
    state = stateobj()
    AreEqual(test_outer_for_with_finally(state, True), 42)
    AreEqual(state.finallyCalled, True)
    
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
    AreEqual(test_outer_for_with_finally(state, False), 42)
    AreEqual(state.finallyCalled, True)
        
    state = stateobj()
    AreEqual(test_outer_for_with_finally(state, True), 42)
    AreEqual(state.finallyCalled, True)

def test_serializable_clionly():
    import clr
    import System
    from IronPythonTest import ExceptionsTest
    path = clr.GetClrType(ExceptionsTest).Assembly.Location
    mbro = System.AppDomain.CurrentDomain.CreateInstanceFromAndUnwrap(path, "IronPythonTest.EngineTest")
    AssertError(AssertionError, mbro.Run, 'raise AssertionError')
    import exceptions
    
    for eh in dir(exceptions):
        eh = getattr(exceptions, eh)
        if isinstance(eh, type) and issubclass(eh, BaseException):
            # unicode exceptions require more args...
            if (eh.__name__ != 'UnicodeDecodeError' and 
                eh.__name__ != 'UnicodeEncodeError' and 
                eh.__name__ != 'UnicodeTranslateError'):
                AssertError(eh, mbro.Run, 'raise ' + eh.__name__)

def test_sanity():
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
            Assert(not hasattr(t_except, '__getstate__'))
    
    if not is_silverlight:
        #special cases
        encode_except = exceptions.UnicodeEncodeError("1", "2", 3, 4, "5")
        AreEqual(encode_except.encoding, "1")
        AreEqual(encode_except.object, "2")
        AreEqual(encode_except.start, 3)
        AreEqual(encode_except.end, 4)
        AreEqual(encode_except.reason, "5")
        AreEqual(encode_except.message, "")
        
        #CodePlex Work Item 356
        #AssertError(TypeError, exceptions.UnicodeDecodeError, "1", u"2", 3, 4, "e")
        exceptions.UnicodeDecodeError("1", "2", 3, 4, "e")
        
        decode_except = exceptions.UnicodeDecodeError("1", "2", 3, 4, "5")
        AreEqual(decode_except.encoding, "1")
        AreEqual(decode_except.object, "2")
        AreEqual(decode_except.start, 3)
        AreEqual(decode_except.end, 4)
        AreEqual(decode_except.reason, "5")
        AreEqual(decode_except.message, "")
        
        translate_except = exceptions.UnicodeTranslateError("1", 2, 3, "4")
        AreEqual(translate_except.object, "1")
        AreEqual(translate_except.start, 2)
        AreEqual(translate_except.end, 3)
        AreEqual(translate_except.reason, "4")
        AreEqual(translate_except.message, "")
        AreEqual(translate_except.encoding, None)

def test_nested_exceptions():
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
        AreEqual(ei, ei2)
    ei3 = sys.exc_info()
    AreEqual(ei, ei3)

def test_swallow_from_else():
    def f():
        try:
            pass
        except:
            pass
        else:
            raise AttributeError
        finally:
            return 4
            
    AreEqual(f(), 4)

def test_newstyle_raise():
    # raise a new style exception via raise type, value that returns an arbitrary object
    class MyException(Exception):
        def __new__(cls, *args): return 42
        
    try:
        raise MyException('abc')
        AssertUnreachable()
    except Exception as e:
        AreEqual(e, 42)

def test_enverror_init():
    x = EnvironmentError()
    AreEqual(x.message, '')
    AreEqual(x.errno, None)
    AreEqual(x.filename, None)
    AreEqual(x.strerror, None)
    AreEqual(x.args, ())
    
    x.__init__('abc')
    AreEqual(x.message, 'abc')
    AreEqual(x.args, ('abc', ))
    
    x.__init__('123', '456')
    AreEqual(x.message, 'abc')
    AreEqual(x.errno, '123')
    AreEqual(x.strerror, '456')
    AreEqual(x.args, ('123', '456'))
    
    x.__init__('def', 'qrt', 'foo')
    AreEqual(x.message, 'abc')
    AreEqual(x.errno, 'def')
    AreEqual(x.strerror, 'qrt')
    AreEqual(x.filename, 'foo')
    AreEqual(x.args, ('def', 'qrt')) # filename not included in args
    
    x.__init__()
    AreEqual(x.message, 'abc')
    AreEqual(x.errno, 'def')
    AreEqual(x.strerror, 'qrt')
    AreEqual(x.filename, 'foo')
    AreEqual(x.args, ())

    x.__init__('1', '2', '3', '4')
    AreEqual(x.message, 'abc')
    AreEqual(x.errno, 'def')
    AreEqual(x.strerror, 'qrt')
    AreEqual(x.filename, 'foo')
    AreEqual(x.args, ('1', '2', '3', '4'))
    
    x = EnvironmentError('a', 'b', 'c', 'd')
    AreEqual(x.message, '')
    AreEqual(x.errno, None)
    AreEqual(x.filename, None)
    AreEqual(x.strerror, None)
    AreEqual(x.args, ('a', 'b', 'c', 'd'))

def test_raise_None():
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
    
    Assert(lineno1 != lineno2, "FAILED! Should not have reused exception")

def test_exception_setstate():
    x = BaseException()
    AreEqual(x.__dict__, {})
    x.__setstate__({'a' : 1, 'b' : 2})
    AreEqual(x.__dict__, {'a' : 1, 'b' : 2})
    x.__setstate__({'a' : 3, 'c' : 4})
    AreEqual(x.__dict__, {'a' : 3, 'b' : 2, 'c' : 4})

def test_deprecated_string_exception():
    w = warning_trapper()
    try:
        raise 'Error'
    except:
        pass
    m = w.finish()
    try:
        raise 'foo'
    except TypeError as e:
        print(e.message)
    

def test_nested_try():
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
    AreEqual(l, [1, 3])
    l = []
    
    def bar():
        try:
            l.append(1)
        except:
            l.append(2)
        else:
            l.append(3)
            
    bar()
    AreEqual(l, [1, 3])

def test_module_exceptions():    
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
                    Assert(repr(val).startswith("<class "))
                    val.x = 2
                    AreEqual(val.x, 2)
                else:
                    Assert(repr(val).startswith("<type "))

def test_raise_inside_str():
    #raising an error inside the __str__ used to cause an unhandled exception.
    class error(Exception):
	    def __str__(self):
		    raise TypeError("inside __str__")

    def f():
	    raise error
    AssertError(error, f)

def test_exception_doc():
    # should be accessible, CPython and IronPython have different strings though.
    Exception().__doc__
    Exception("abc").__doc__

def test_repr_not_called():
    """__repr__ shouldn't be called when message is a tuple w/ multiple args"""
    class x(object):
        def __repr__(self):
            raise StopIteration('repr should not be called')
    
    try:
        sys.exit((x(), x()))
    except SystemExit:
        pass

def test_windows_error():
    # int is required for 2/3 params
    AssertError(TypeError, WindowsError, 'foo', 'bar')
    AssertError(TypeError, WindowsError, 'foo', 'bar', 'baz')
    
    err = WindowsError('foo', 'bar', 'baz', 'quox')
    AreEqual(err.errno, None)
    AreEqual(err.winerror, None)
    AreEqual(err.filename, None)
    AreEqual(err.strerror, None)
    AreEqual(err.args, ('foo', 'bar', 'baz', 'quox'))
    
    err = WindowsError(42, 'bar', 'baz')
    AreEqual(err.filename, 'baz')
    AreEqual(err.winerror, 42)
    AreEqual(err.strerror, 'bar')
    AreEqual(err.args, (42, 'bar'))

    # winerror code is passed through unmodified
    for i in range(256):
        x = WindowsError(i, 'foo')
        AreEqual(x.winerror, i)
    
    # winerror code is mapped to Python error code
    AreEqual(WindowsError(10, 'foo').errno, 7)

def test_derived_keyword_args():
    class ED(Exception):
        def __init__(self, args=''):
            pass
    
    AreEqual(type(ED(args='')), ED)

class CP35300_Derived(EnvironmentError):
    def __init__(self, *args, **kwargs):
        pass

def test_cp35300():
    # make sure that classes derived from builtin exceptions which have
    # generated implementation do not restrict parameters of __init__
    AreNotEqual(None, CP35300_Derived("a", x="b"))

def test_issue1164():
    class error(Exception):
        pass

    def f():
	    raise error(0)

    AssertError(error, f)

run_test(__name__)
