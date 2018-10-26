# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import sys
import unittest

from iptest import is_cpython, run_test

class MissingEnter:
    def __exit__(self,a,b,c): pass

class H:
    def __enter__(self): H.var1 = 100
    def __exit__(self, a,b,c):
        H.var2 = 200
        raise RuntimeError("we force an error")

class Myerr1(Exception):pass

class WithTest(unittest.TestCase):

    def test_raise(self):
        events = []
        expectedEvents = [
        'A.__enter__', 'A.__exit__(None)', 'A.__enter__', 'A.__exit__(None)', 'A.__enter__', 'A.__exit__(exception)',
        'A.__enter__', 'A.__exit__(None)', 'A.__enter__', 'A.__exit__(None)', 'A.__enter__', 'A.__exit__(exception)',
        'A.__enter__', 'A.__exit__(None)', 'A.__enter__', 'A.__exit__(None)', 'A.__enter__', 'A.__exit__(exception)',
        'A.__enter__', 'A.__exit__(None)', 'A.__enter__', 'A.__exit__(None)', 'A.__enter__', 'A.__exit__(exception)',
        'A.__enter__', 'A.__exit__(None)', 'A.__enter__', 'A.__exit__(None)', 'A.__enter__', 'A.__exit__(exception)',
        'A.__enter__', 'A.__exit__(None)', 'A.__enter__', 'A.__exit__(None)', 'A.__enter__', 'A.__exit__(exception)',
        'A.__enter__', 'A.__exit__(None)', 'A.__enter__', 'A.__exit__(None)', 'A.__enter__', 'A.__exit__(exception)',
        'A.__enter__', 'A.__exit__(None)', 'A.__enter__', 'A.__exit__(None)', 'A.__enter__', 'A.__exit__(exception)',
        'A.__enter__', 'A.__exit__(None)', 'A.__enter__', 'A.__exit__(None)', 'A.__enter__', 'A.__exit__(exception)',
        'A.__enter__', 'A.__exit__(None)', 'A.__enter__', 'A.__exit__(None)', 'A.__enter__', 'A.__exit__(exception)',
        'A.__enter__', 'A.__exit__(None)', 'A.__enter__', 'A.__exit__(None)', 'A.__enter__', 'A.__exit__(exception)',
        'A.__enter__', 'A.__exit__(None)', 'A.__enter__', 'A.__exit__(None)', 'A.__enter__', 'A.__exit__(exception)',
        'A.__enter__', 'A.__exit__(None)', 'A.__enter__', 'A.__exit__(None)', 'A.__enter__', 'A.__exit__(exception)',
        'A.__enter__', 'A.__exit__(None)', 'A.__enter__', 'A.__exit__(None)',
        'A.__enter__', 'A.__exit__(None)', 'A.__enter__', 'A.__exit__(None)', 'A.__enter__', 'A.__exit__(exception)',
        'A.__enter__', 'A.__exit__(None)', 'A.__enter__', 'A.__exit__(None)', 'A.__enter__', 'A.__exit__(exception)',
        'A.__enter__', 'A.__exit__(None)', 'A.__enter__', 'A.__exit__(None)', 'A.__enter__', 'A.__exit__(exception)',
        'A.__enter__', 'A.__exit__(None)', 'A.__enter__', 'A.__exit__(None)', 'A.__enter__', 'A.__exit__(exception)',
        'A.__enter__', 'A.__exit__(None)', 'A.__enter__', 'A.__exit__(None)', 'A.__enter__', 'A.__exit__(exception)',
        'A.__enter__', 'A.__exit__(None)', 'A.__enter__', 'A.__exit__(None)', 'A.__enter__', 'A.__exit__(exception)',
        'A.__enter__', 267504, 'A.__exit__(None)']

        #test case with RAISE(exit consumes), YIELD, RETURN, BREAK and CONTINUE in WITH
        class A:
            def __enter__(self):
                events.append('A.__enter__')
                return 300
            def __exit__(self,type,value,traceback):
                if(type == None and value == None and traceback == None):
                    events.append('A.__exit__(None)')
                else:
                    events.append('A.__exit__(exception)')
                return 1

        a = A()

        def foo():
            p = 100
            for y in [1,2,3,4,5,6,7,8,9]:
                for x in [10,20,30,40,50,60,70,80,90]:
                    with a as b:
                        p = p + 1
                        if ( x == 20 ): continue
                        if ( x == 50 and y == 5 ):    break
                        if( x != 40 and y != 4) : yield p
                        p = p + 5
                        p = p +  x * 100
                        p = p + 1
                        if(x  % 3 == 0):
                            raise RuntimeError("we force exception")
                        if(y == 8):
                            events.append(p)
                            #globals()["m"] += p
                            return
                        if(x  % 3  == 0 and y %3 == 0):
                            raise RuntimeError("we force exception")
                        if ( x == 90 ): continue
                        if ( x == 60 and y == 6 ): break
                        yield b + p
                        p = p + 1
        try:
            k = foo()
            while(next(k)):pass
        except StopIteration: self.assertEqual(events, expectedEvents)
        else :self.fail("Expected StopIteration but found None")

    def test_enter(self):
        # testing __enter__
        def just_a_fun(arg): return 300
        class B:
            def __enter__(self): return "Iron", "Python", just_a_fun
            def __exit__(self, a,b,c): pass

        mydict = {1: [0,1,2], 2:None }
        with B() as (mydict[1][0], mydict[2], B.myfun):
            self.assertEqual((mydict[1],mydict[2],B().myfun()),(["Iron",1,2],"Python",just_a_fun(None)) )

        #ensure it is same outside with also
        self.assertEqual((mydict[1],mydict[2],B().myfun()),(["Iron",1,2],"Python",just_a_fun(None)) )

    def test_more_args(self):
        # more args
        class C:
            def __enter__(self,morearg): pass
            def __exit__(self, a,b,c): pass
        try:
            with C() as something: pass
        except TypeError: pass
        else :self.fail("Expected TypeError but found None")

    def test_enter_raises(self):
        #enter raises
        class D:
            def __enter__(self):
                raise RuntimeError("we force an error")
            def __exit__(self, a,b,c): pass

        try:
            with D() as something: pass
        except RuntimeError: pass
        else :self.fail("Expected RuntimeError but found None")

    def test_missing_enter(self):
        try:
            with MissingEnter(): pass
        except AttributeError:pass
        else: self.fail("Expected AttributeError but found None")

    def test_exit_more_args(self):
        # Testing __exit__
        # more args
        class E:
            def __enter__(self): pass
            def __exit__(self, a,b,c,d,e,f): pass
        try:
            with E() as something: pass
        except TypeError: pass
        else :self.fail("Expected TypeError but found None")

    def test_less_args(self):
        # less args
        class F:
            def __enter__(self): pass
            def __exit__(self): pass
        try:
            with F() as something: pass
        except TypeError: pass
        else :self.fail("Expected TypeError but found None")

    def test_exit_raises(self):
        #exit raises
        try:
            with H():
                H.var3 = 300
        except RuntimeError: self.assertEqual((H.var1,H.var2,H.var3),(100,200,300))
        else :self.fail("Expected RuntimeError but found None")

    def test_exit_raises_on_successful(self):
        #exit raises on successful / throwing WITH

        class Myerr2(Exception):pass
        class Myerr3(Exception):pass
        class ExitRaise:
            def __enter__(self): H.var1 = 100
            def __exit__(self, a,b,c):
                if(a == None and b == None and c == None):
                    raise Myerr1
                raise Myerr2

        try:
            with ExitRaise():
                1+2+3
        except Myerr1: pass
        else :self.fail("Expected Myerr1 but found None")

        try:
            with ExitRaise():
                raise Myerr3
        except Myerr2: pass
        else :self.fail("Expected Myerr2 but found None")

    def test_exit_propagates_exception_on_name_deletion(self):
        #exit propagates exception on name deletion ( covers FLOW CHECK scenario)
        class PropagateException:
            def __enter__(self): pass
            def __exit__(self, a,b,c): return False
        try:
            with PropagateException() as PE:
                del PE
                print(PE)
        except NameError:pass
        else: self.fail("Expected NameError but found None")

        try:
            with PropagateException() as PE:
                PE.var1 = 100
                del PE
                print(PE)
        except AttributeError:pass
        else: self.fail("Expected AttributeError but found None")

    def test_exit_consumes_exception(self):
        #exit consumes exception
        class ConsumeException:
            def __enter__(self): pass
            def __exit__(self, a,b,c): return [1,2,3],{"dsad":"dsd"},"hello"
        with ConsumeException():1/0

    def test_missing_exit(self):
        #missing exit
        class MissingExit:
            def __enter__(self): pass
        try:
            with MissingEnter(): pass
        except AttributeError:pass
        else: self.fail("Expected AttributeError but found None")

    def test_with_stmt_under_compound_stmts_no_yield(self):
        events = []
        expectedEvents = [
            'body', 'ZeroDivisionError',
            'inherited_cxtmgr.__enter__',
            'inner_with', 'inner_body',
            'inherited_cxtmgr.__enter__', 'deep_inner_with', 'cxtmgr.__exit__',
            'finally',
            'ClassInFinally.__enter__', 'last_with', 'ClassInFinally.__exit__',
            'cxtmgr.__exit__']

        #inheritance
        class cxtmgr:
            def __exit__(self, a, b, c):
                events.append('cxtmgr.__exit__')
                return False

        class inherited_cxtmgr(cxtmgr):
            def __enter__(self):
                events.append('inherited_cxtmgr.__enter__')
                return False


        # Building up most complex TRY-CATCH-FINALLY-RAISE-WITH-CLASS combination with inheritance.
        #try->(try->(except->(with ->fun ->(try->(with->raise)->Finally(With)))))
        try: #Try
            try: #try->try
                events.append('body')
                1/0
            except ZeroDivisionError: #try->(try->except)
                events.append('ZeroDivisionError')
                with inherited_cxtmgr() as ic: #try->(try->(except->with(inherited)))
                    events.append('inner_with')
                    def fun_in_with(): return "Python is smart"
                    self.assertEqual(fun_in_with(),"Python is smart") #try->(try->(except->(with ->fun)))
                    try:                                      #try->(try->(except->(with ->fun ->try)))
                        events.append('inner_body')
                        with inherited_cxtmgr() as inherited_cxtmgr.var: #try->(try->(except->(with ->fun ->(try->with))))
                            events.append('deep_inner_with')
                            raise Myerr1  #try->(try->(except->(with ->fun ->(try->with->raise))))
                    finally:    #try->(try->(except->(with ->fun ->(try->(with->raise)->Finally))))
                        if is_cpython: #http://ironpython.codeplex.com/workitem/27990/
                            self.assertEqual(sys.exc_info()[0], Myerr1)
                        else:
                            self.assertEqual(sys.exc_info()[0], ZeroDivisionError)
                        events.append('finally')
                        class ClassInFinally:
                            def __enter__(self):
                                events.append('ClassInFinally.__enter__')
                                return 200
                            def __exit__(self,a,b,c):
                                events.append('ClassInFinally.__exit__')
                                return False # it raises
                        with ClassInFinally(): #try->(try->(except->(with ->fun ->(try->(with->raise)->Finally(With)))))
                            events.append('last_with')
        except Myerr1: self.assertEqual(events, expectedEvents)
        else: self.fail("Expected Myerr1")

    def test_with_enter_and_exit(self):
        global gblvar
        gblvar = 0
        class A:
            def __enter__(self):  globals()["gblvar"] += 1 ; return 100
            def __exit__(self,a,b,c):  globals()["gblvar"] += 2; return 200

        class WithInEnterExit:
            def __enter__(self):
                with A() as b:
                    globals()["gblvar"] += 3;return A()
            def __exit__(self,a,b,c):
                with A() as c:
                    globals()["gblvar"] += 4; return A()

        self.assertEqual( 1,1)
        with WithInEnterExit() as wie:
            with wie as wie_wie:
                globals()["gblvar"] += 100

        self.assertEqual(globals()["gblvar"],116)

    def test_void_return_value(self):
        class A:
            def __enter__(self): pass
            def __exit__(self, a,b,c): pass

        try:
            with A() as a:
                raise Exception
        except Exception: pass
        else: self.fail("Should have raised exception")

run_test(__name__)