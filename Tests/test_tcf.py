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

import unittest

from iptest import run_test

globals()["gblvar"] = 1
def setvar() : globals()["gblvar"] += 1

class MyErr1(Exception) :pass
class MyErr2(Exception) :pass
class MyErr3(Exception) :pass
class MyErr4(Exception) :pass

class TryCatchFinallyTest(unittest.TestCase):
    def setUp(self):
        super(TryCatchFinallyTest, self).setUp()
        globals()["gblvar"] = 1

    def test_syntax(self):
        """test try-catch-finally syntax"""
        
        #missing except,else
        try:
            setvar()
        
            # missing else, finally
            try:1 / 0
            except ZeroDivisionError: setvar()
        
            # missing else
            try:
                setvar()
                a =[]
                a[10]
            except ZeroDivisionError: assert(False)
            except IndexError: setvar()
            finally: setvar()
        finally:
            setvar()
        self.assertEqual(globals()["gblvar"],7)
    
    def test_unified_try_tests(self):
        def test_unified_try(myraise1,myraise2,myraise3,myraise4,myraise5,myraise6,myraise7,myraise8,myraise9):
            try:
                yield 1; setvar()
                yield 2; setvar()
                try:
                    setvar()
                    if myraise1 == "raiseInTry": setvar(); raise MyErr1
                    if myraise1 == "outerTry": setvar(); raise MyErr2
                    if myraise1 == "Unhandled": setvar(); raise MyErr4
                    setvar()
                except MyErr1:
                    setvar()
                    if myraise2 == "raiseInExcept": setvar(); raise MyErr2
                    if myraise2 == "Unhandled": setvar(); raise MyErr4
                    setvar()
                except :setvar() # should never be executed
                else :
                    setvar()
                    if myraise2 == "raiseInElse": setvar(); raise MyErr2
                    if myraise2 == "Unhandled": setvar(); raise MyErr4
                    setvar()
                finally :
                    setvar()
                    if myraise3 == "raiseInFinally":  setvar(); raise MyErr3
                    if myraise3 == "Unhandled":  setvar(); raise MyErr4
                    setvar()
                yield 1; setvar()
                yield 2; setvar()
            except MyErr2:
                yield 1; setvar()
                yield 2; setvar()
                try:
                    setvar()
                    if myraise4 == "raiseInTry" :setvar(); raise MyErr1
                    if myraise4 == "Unhandled" :setvar(); raise MyErr4
                    setvar()
                except MyErr1:
                    setvar()
                    if myraise5 == "Unhandled": setvar(); raise MyErr4
                    setvar()
                except :setvar() # should never be executed
                else :
                    setvar()
                    if myraise5 == "Unhandled": setvar(); raise MyErr4
                    setvar()
                finally :
                    setvar()
                    if myraise6 == "Unhandled":  setvar(); raise MyErr4
                    setvar()
                yield 1; setvar()
                yield 2; setvar()
            except MyErr3:
                yield 1; setvar()
                yield 2; setvar()
                try:
                    setvar()
                    if myraise4 == "raiseInTry" :setvar(); raise MyErr1
                    if myraise4 == "Unhandled" :setvar(); raise MyErr4
                    setvar()
                except MyErr1:
                    setvar()
                    if myraise5 == "Unhandled": setvar(); raise MyErr4
                    setvar()
                except :setvar() # should never be executed
                else :
                    setvar()
                    if myraise5 == "Unhandled": setvar(); raise MyErr4
                    setvar()
                finally :
                    setvar()
                    if myraise6 == "Unhandled":  setvar(); raise MyErr4
                    setvar()
                yield 1; setvar()
                yield 2; setvar()
            else :
                yield 1; setvar()
                yield 2; setvar()
                try:
                    setvar()
                    if myraise4 == "raiseInTry" :setvar(); raise MyErr1
                    if myraise4 == "Unhandled" :setvar(); raise MyErr4
                    setvar()
                except MyErr1:
                    setvar()
                    if myraise5 == "Unhandled": setvar(); raise MyErr4
                    setvar()
                except :setvar() # should never be executed
                else :
                    setvar()
                    if myraise5 == "Unhandled": setvar(); raise MyErr4
                    setvar()
                finally :
                    setvar()
                    if myraise6 == "Unhandled":  setvar(); raise MyErr4
                    setvar()
                yield 1; setvar()
                yield 2; setvar()
            finally :
                #uncomment the following 2 lines once we have the fix for PS:1752
                #and accordingly adjust the final expected result value
                #yield 1; setvar()
                #yield 2; setvar()
                try:
                    setvar()
                    if myraise7 == "raiseInTry" :setvar(); raise MyErr1
                    setvar()
                    if myraise7 == "Unhandled" :setvar(); raise MyErr4
                    setvar()
                except MyErr1:
                    setvar()
                    if myraise8 == "Unhandled": setvar(); raise MyErr4
                    setvar()
                except :setvar() # should never be executed
                else :
                    setvar()
                    if myraise8 == "Unhandled": setvar(); raise MyErr4
                    setvar()
                finally :
                    setvar()
                    if myraise9 == "Unhandled":  setvar(); raise MyErr4
                    setvar()
                #uncomment the following 2 lines once we have the fix for PS:1752
                #and accordingly adjust the final expected result value
                #yield 1; setvar()
                #yield 2; setvar()
        
        
        myraise1 = ["raiseInTry","outerTry","Unhandled","None"]
        myraise2 = ["raiseInExcept", "raiseInElse","Unhandled","None"]
        myraise3 = ["raiseInFinally","Unhandled","None"]
        myraise4 = ["raiseInTry","Unhandled","None"]
        myraise5 = ["Unhandled","None"]
        myraise6 = ["Unhandled","None"]
        myraise7 = ["raiseInTry","Unhandled","None"]
        myraise8 = ["Unhandled","None"]
        myraise9 = ["Unhandled","None"]
        
        def fun():
            for a in myraise1:
                for b in myraise2:
                    for c in myraise3:
                        for d in myraise4:
                            for e in myraise5:
                                for f in myraise6:
                                    for g in myraise7:
                                        for h in myraise8:
                                            for i in myraise9:
                                                k = test_unified_try(a,b,c,d,e,f,g,h,i)
                                                while(True):
                                                    try:
                                                        k.next()
                                                    except MyErr4: setvar();break
                                                    except StopIteration: setvar();break
        
        
        fun()
        self.assertEqual(globals()["gblvar"],141985)
    
    def test_try_catch_finally_on_targets(self):
        """test try-catch-finally on targets"""
        globals()["gblvar"]  = 1
        def test_targets(ret):
            x = 0
            y = 0
            z = 0
            setvar()
            while( z < 6 ) :
                z += 1
                while( y <  8 ) :
                    y += 1
                    while( x < 20 ) :
                        x += 1
                        setvar()
                        try:
                            setvar()
                            if not x % 3 : setvar();continue
                            if not x % 4 : setvar();break
                            if not x % 5 : setvar();1 / 0
                            if not x % 7 and ret == "try" : setvar();return
                            setvar()
                        except:
                            setvar()
                            if not y % 3 : setvar();continue
                            if not y % 4 : setvar();break
                            if not y % 7 and ret == "except" : setvar();return
                            setvar()
                        else:
                            setvar()
                            if not x % 11 : setvar();continue
                            if not x % 13 : setvar();break
                            if not x % 19 and ret == "else" : setvar();return
                            setvar()
                        finally:
                            setvar()
                            #IPy does support continue under finally, just for CPy compatibility we do not test it here
                            #if z % 2 : setvar();continue
                            if not z % 2 : setvar();break
                            if not z % 5 and ret == "finally" : setvar();return
                            setvar()
            setvar()
            return
        
        ret = ["try","except","else","finally"]
        for r in ret:
            test_targets(r)
        self.assertEqual(globals()["gblvar"],403)        

    def test_yield_in_finally(self):
        #test yield in finally
        globals()["gblvar"]  = 1
        def test_yield_finally():
            setvar()
            try: setvar();1/0
            except:setvar()
            else: setvar()
            finally:
                setvar();yield 100
                setvar();yield 100
                setvar()
            setvar()

        try:
            k = test_yield_finally()
            while(1):
                k.next()
        except StopIteration: pass
        
        self.assertEqual(globals()["gblvar"],8)

run_test(__name__)