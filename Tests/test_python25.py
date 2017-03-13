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

#
# test python25
#

import sys
import exceptions

from iptest.assert_util import *

isPython25 = ((sys.version_info[0] == 2) and (sys.version_info[1] >= 5)) or (sys.version_info[0] > 2)
Assert(isPython25)


#test case with RAISE(exit consumes), YIELD, RETURN, BREAK and CONTINUE in WITH
m = 0
class A:
    def __enter__(self):
        globals()["m"] += 99
        return 300
    def __exit__(self,type,value,traceback):
        if(type == None and value == None and traceback == None):
            globals()["m"] += 55
        else:
            globals()["m"] *= 2
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
                    globals()["m"] += p
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
except StopIteration: AreEqual(m,427056988)
else :Fail("Expected StopIteration but found None")

# testing __enter__
def just_a_fun(arg): return 300
class B:
    def __enter__(self): return "Iron", "Python", just_a_fun
    def __exit__(self, a,b,c): pass

mydict = {1: [0,1,2], 2:None }
with B() as (mydict[1][0], mydict[2], B.myfun):
    AreEqual((mydict[1],mydict[2],B().myfun()),(["Iron",1,2],"Python",just_a_fun(None)) )

#ensure it is same outside with also
AreEqual((mydict[1],mydict[2],B().myfun()),(["Iron",1,2],"Python",just_a_fun(None)) )

# more args
class C:
    def __enter__(self,morearg): pass
    def __exit__(self, a,b,c): pass
try:
    with C() as something: pass
except TypeError: pass
else :Fail("Expected TypeError but found None")

#enter raises
class D:
    def __enter__(self):
        raise RuntimeError("we force an error")
    def __exit__(self, a,b,c): pass

try:
    with D() as something: pass
except RuntimeError: pass
else :Fail("Expected RuntimeError but found None")

#missing enter
class MissingEnter:
    def __exit__(self,a,b,c): pass
try:
    with MissingEnter(): pass
except AttributeError:pass
else: Fail("Expected AttributeError but found None")

# Testing __exit__
# more args
class E:
    def __enter__(self): pass
    def __exit__(self, a,b,c,d,e,f): pass
try:
    with E() as something: pass
except TypeError: pass
else :Fail("Expected TypeError but found None")

# less args
class F:
    def __enter__(self): pass
    def __exit__(self): pass
try:
    with F() as something: pass
except TypeError: pass
else :Fail("Expected TypeError but found None")

#exit raises
class H:
    def __enter__(self): H.var1 = 100
    def __exit__(self, a,b,c):
        H.var2 = 200
        raise RuntimeError("we force an error")

try:
    with H():
        H.var3 = 300
except RuntimeError: AreEqual((H.var1,H.var2,H.var3),(100,200,300))
else :Fail("Expected RuntimeError but found None")

#exit raises on successful / throwing WITH
class Myerr1(Exception):pass
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
else :Fail("Expected Myerr1 but found None")

try:
    with ExitRaise():
        raise Myerr3
except Myerr2: pass
else :Fail("Expected Myerr2 but found None")


#exit propagates exception on name deletion ( covers FLOW CHECK scenario)
class PropagateException:
    def __enter__(self): pass
    def __exit__(self, a,b,c): return False
try:
    with PropagateException() as PE:
        del PE
        print(PE)
except NameError:pass
else: Fail("Expected NameError but found None")

try:
    with PropagateException() as PE:
        PE.var1 = 100
        del PE
        print(PE)
except AttributeError:pass
else: Fail("Expected AttributeError but found None")

#exit consumes exception
class ConsumeException:
    def __enter__(self): pass
    def __exit__(self, a,b,c): return [1,2,3],{"dsad":"dsd"},"hello"
with ConsumeException():1/0

#missing exit
class MissingExit:
    def __enter__(self): pass
try:
    with MissingEnter(): pass
except AttributeError:pass
else: Fail("Expected AttributeError but found None")

#With Stmt under other compound statements (NO YIELD)

gblvar = 0


#inheritance
class cxtmgr:
    def __exit__(self, a, b, c):
        globals()["gblvar"] += 10
        return False


class inherited_cxtmgr(cxtmgr):
    def __enter__(self):
        globals()["gblvar"] += 10
        return False


# Building up most complex TRY-CATCH-FINALLY-RAISE-WITH-CLASS combination with inheritance.
#try->(try->(except->(with ->fun ->(try->(with->raise)->Finally(With)))))
try: #Try
    try: #try->try
        globals()["gblvar"] += 1
        1/0
    except ZeroDivisionError: #try->(try->except)
        globals()["gblvar"] += 2
        with inherited_cxtmgr() as ic: #try->(try->(except->with(inherited)))
            globals()["gblvar"] += 3
            def fun_in_with(): return "Python is smart"
            AreEqual(fun_in_with(),"Python is smart") #try->(try->(except->(with ->fun)))
            try:                                      #try->(try->(except->(with ->fun ->try)))
                globals()["gblvar"] += 4
                with inherited_cxtmgr() as inherited_cxtmgr.var: #try->(try->(except->(with ->fun ->(try->with))))
                    globals()["gblvar"] += 5
                    raise Myerr1()  #try->(try->(except->(with ->fun ->(try->with->raise))))
            finally:    #try->(try->(except->(with ->fun ->(try->(with->raise)->Finally))))
                if is_cpython: #http://ironpython.codeplex.com/workitem/27990/
                    AreEqual(sys.exc_info()[0], Myerr1)
                else:
                    AreEqual(sys.exc_info()[0], exceptions.ZeroDivisionError)
                globals()["gblvar"] += 6
                class ClassInFinally:
                    def __enter__(self):
                        globals()["gblvar"] +=  7
                        return 200
                    def __exit__(self,a,b,c):
                        globals()["gblvar"] += 8
                        return False # it raises
                with ClassInFinally(): #try->(try->(except->(with ->fun ->(try->(with->raise)->Finally(With)))))
                    globals()["gblvar"] += 9
except Myerr1: AreEqual(globals()["gblvar"],85)

# With in __enter__ and __exit__
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

AreEqual ( 1,1)
with WithInEnterExit() as wie:
    with wie as wie_wie:
        globals()["gblvar"] += 100
        
AreEqual(globals()["gblvar"],116)

#------------------------------------------------------------------------------
def test_thread_lock():
    import _thread

    temp_lock = _thread.allocate_lock()
    Assert(hasattr(temp_lock, "__enter__"))
    Assert(hasattr(temp_lock, "__exit__"))
    Assert(not temp_lock.locked())
    with temp_lock:
        Assert(temp_lock.locked())
    Assert(not temp_lock.locked())
    
    with _thread.allocate_lock(): pass

@skip("silverlight")
def test_with_file():    
    with file('abc.txt', 'w'):
        pass

def setvar() : globals()["gblvar"] += 1

def test_try_catch_finally():
    # test try-catch-finally syntax
    globals()["gblvar"] = 1

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
    AreEqual(globals()["gblvar"],7)

    globals()["gblvar"] = 1
    class MyErr1(Exception) :pass
    class MyErr2(Exception) :pass
    class MyErr3(Exception) :pass
    class MyErr4(Exception) :pass

                                     
    def TestUnifiedTry(myraise1,myraise2, myraise3,myraise4,myraise5,myraise6,myraise7,myraise8,myraise9):
        try:
            yield 1; setvar()
            yield 2; setvar()
            try:
                setvar()
                if myraise1 == "raiseInTry" :setvar(); raise MyErr1
                if myraise1 == "outerTry" :setvar(); raise MyErr2
                if myraise1 == "Unhandled" :setvar(); raise MyErr4
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
                                            k = TestUnifiedTry(a,b,c,d,e,f,g,h,i)
                                            while(True):
                                                try:
                                                    next(k)
                                                except MyErr4: setvar();break
                                                except StopIteration: setvar();break


    fun()
    AreEqual(globals()["gblvar"],141985)


def test_try_catch_finally_on_targets():
    #test try-catch-finally on targets
    globals()["gblvar"]  = 1
    def TestTargets(ret):
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
        TestTargets(r)
    AreEqual(globals()["gblvar"],403)
    
def test_yield_in_finally():
    #test yield in finally
    globals()["gblvar"]  = 1
    def test_yiled_finally():
        setvar()
        try: setvar();1/0
        except:setvar()
        else: setvar()
        finally:
            setvar();yield 100
            setvar();yield 100
            setvar()
        setvar();
    
    try:
        k = test_yiled_finally()
        while(1):
            next(k)
    except StopIteration: pass
    
    AreEqual(globals()["gblvar"],8)


def test_string_partition():
    AreEqual('http://www.codeplex.com/WorkItem/List.aspx?ProjectName=IronPython'.partition('://'), ('http','://','www.codeplex.com/WorkItem/List.aspx?ProjectName=IronPython'))
    AreEqual('http://www.codeplex.com/WorkItem/List.aspx?ProjectName=IronPython'.partition('stringnotpresent'), ('http://www.codeplex.com/WorkItem/List.aspx?ProjectName=IronPython','',''))
    AreEqual('stringisnotpresent'.partition('presentofcoursenot'), ('stringisnotpresent','',''))
    AreEqual(''.partition('stringnotpresent'), ('','',''))
    AreEqual('onlymatchingtext'.partition('onlymatchingtext'), ('','onlymatchingtext',''))
    AreEqual('alotoftextherethatisapartofprefixonlyprefix_nosuffix'.partition('_nosuffix'), ('alotoftextherethatisapartofprefixonlyprefix','_nosuffix',''))
    AreEqual('noprefix_alotoftextherethatisapartofsuffixonlysuffix'.partition('noprefix_'), ('','noprefix_','alotoftextherethatisapartofsuffixonlysuffix'))
    AreEqual('\0'.partition('\0'), ('','\0',''))
    AreEqual('\00\ff\67\56\d8\89\33\09\99\ee\20\00\56\78\45\77\e9'.partition('\00\56\78'), ('\00\ff\67\56\d8\89\33\09\99\ee\20','\00\56\78','\45\77\e9'))
    AreEqual('\00\ff\67\56\d8\89\33\09\99\ee\20\00\56\78\45\77\e9'.partition('\78\45\77\e9'), ('\00\ff\67\56\d8\89\33\09\99\ee\20\00\56','\78\45\77\e9',''))
    AreEqual('\ff\67\56\d8\89\33\09\99\ee\20\00\56\78\45\77\e9'.partition('\ff\67\56\d8\89\33\09\99'), ('','\ff\67\56\d8\89\33\09\99','\ee\20\00\56\78\45\77\e9'))
    AreEqual('\ff\67\56\d8\89\33\09\99some random 8-bit text here \ee\20\00\56\78\45\77\e9'.partition('random'), ('\ff\67\56\d8\89\33\09\99some ','random',' 8-bit text here \ee\20\00\56\78\45\77\e9'))
    AreEqual('\ff\67\56\d8\89\33\09\99some random 8-bit text here \ee\20\00\56\78\45\77\e9'.partition('\33\09\99some r'), ('\ff\67\56\d8\89','\33\09\99some r','andom 8-bit text here \ee\20\00\56\78\45\77\e9'))
    AssertError(ValueError,'sometextheretocauseanexeption'.partition,'')
    AssertError(ValueError,''.partition,'')
    AssertError(TypeError,'some\90text\ffhere\78to\88causeanexeption'.partition,None)
    AssertError(TypeError,''.partition,None)

    prefix = """ this is some random text
    and it has lots of text
    """

    sep = """
            that is multilined
            and includes unicode \00 \56
            \01 \02 \06 \12\33\67\33\ff \ee also"""
    suffix = """
            \78\ff\43\12\23ok"""
    
    str = prefix + sep + suffix

    AreEqual(str.partition(sep),(prefix,sep,suffix))
    AreEqual(str.partition('nomatch'),(str,'',''))
    AssertError(TypeError,str.partition,None)
    AssertError(ValueError,str.partition,'')

def test_string_rpartition():
    AreEqual('http://www.codeplex.com/WorkItem/List.aspx?Project://Name=IronPython'.rpartition('://'), ('http://www.codeplex.com/WorkItem/List.aspx?Project','://','Name=IronPython'))
    AreEqual('http://www.codeplex.com/WorkItem/List.aspx?ProjectName=IronPython'.rpartition('stringnotpresent'), ('', '', 'http://www.codeplex.com/WorkItem/List.aspx?ProjectName=IronPython'))
    AreEqual('stringisnotpresent'.rpartition('presentofcoursenot'), ('','', 'stringisnotpresent'))
    AreEqual(''.rpartition('stringnotpresent'), ('','',''))
    AreEqual('onlymatchingtext'.rpartition('onlymatchingtext'), ('','onlymatchingtext',''))
    AreEqual('alotoftextherethatisapartofprefixonlyprefix_nosuffix'.rpartition('_nosuffix'), ('alotoftextherethatisapartofprefixonlyprefix','_nosuffix',''))
    AreEqual('noprefix_alotoftextherethatisapartofsuffixonlysuffix'.rpartition('noprefix_'), ('','noprefix_','alotoftextherethatisapartofsuffixonlysuffix'))
    AreEqual('\0'.partition('\0'), ('','\0',''))
    AreEqual('\00\ff\67\56\d8\89\33\09\99\ee\20\00\56\78\00\56\78\45\77\e9'.rpartition('\00\56\78'), ('\00\ff\67\56\d8\89\33\09\99\ee\20\00\56\78','\00\56\78','\45\77\e9'))
    AreEqual('\00\ff\67\56\d8\89\33\09\99\ee\20\00\56\78\45\77\e9\78\45\77\e9'.rpartition('\78\45\77\e9'), ('\00\ff\67\56\d8\89\33\09\99\ee\20\00\56\78\45\77\e9','\78\45\77\e9',''))
    AreEqual('\ff\67\56\d8\89\33\09\99\ee\20\00\56\78\45\77\e9'.rpartition('\ff\67\56\d8\89\33\09\99'), ('','\ff\67\56\d8\89\33\09\99','\ee\20\00\56\78\45\77\e9'))
    AreEqual('\ff\67\56\d8\89\33\09\99some random 8-bit text here \ee\20\00\56\78\45\77\e9'.rpartition('random'), ('\ff\67\56\d8\89\33\09\99some ','random',' 8-bit text here \ee\20\00\56\78\45\77\e9'))
    AreEqual('\ff\67\56\d8\89\33\09\99some random 8-bit text here \ee\20\00\56\78\45\77\e9'.rpartition('\33\09\99some r'), ('\ff\67\56\d8\89','\33\09\99some r','andom 8-bit text here \ee\20\00\56\78\45\77\e9'))
    AssertError(ValueError,'sometextheretocauseanexeption'.rpartition,'')
    AssertError(ValueError,''.rpartition,'')
    AssertError(TypeError,'some\90text\ffhere\78to\88causeanexeption'.rpartition,None)
    AssertError(TypeError,''.rpartition,None)

    prefix = """ this is some random text
    and it has lots of text
    """

    sep = """
            that is multilined
            and includes unicode \00 \56
            \01 \02 \06 \12\33\67\33\ff \ee also"""
    suffix = """
            \78\ff\43\12\23ok"""
    
    str = prefix + sep + suffix

    AreEqual(str.rpartition(sep),(prefix,sep,suffix))
    AreEqual(str.rpartition('nomatch'),('','', str))
    AssertError(TypeError,str.rpartition,None)
    AssertError(ValueError,str.rpartition,'')

                            
def test_string_startswith():
    class A:pass
    # failure scenarios
    AssertError(TypeError,'string'.startswith,None)
    AssertError(TypeError,'string'.startswith,(None,"strin","str"))
    AssertError(TypeError,'string'.startswith,(None,))
    AssertError(TypeError,'string'.startswith,(["this","is","invalid"],"str","stri"))
    AssertError(TypeError,'string'.startswith,(("string","this is invalid","this is also invalid",),))
    AssertError(TypeError,''.startswith,None)
    AssertError(TypeError,''.startswith,(None,"strin","str"))
    AssertError(TypeError,''.startswith,(None,))
    AssertError(TypeError,''.startswith,(["this","is","invalid"],"str","stri"))
    AssertError(TypeError,''.startswith,(("string","this is invalid","this is also invalid",),))

    # success scenarios
    AreEqual('no matching string'.startswith(("matching","string","here")),False)
    AreEqual('here matching string'.startswith(("matching","string","here")), True)
    AreEqual('here matching string'.startswith(("here", "matching","string","here")), True)
    AreEqual('here matching string'.startswith(("matching","here","string",)), True)
    AreEqual('here matching string'.startswith(("here matching string","here matching string","here matching string",)), True)

    s = 'here \12 \34 \ff \e5 \45 matching string'
    m = "here \12 \34 \ff \e5 \45 "
    m1 = " \12 \34 \ff \e5 \45 "
    n = "here \12 \34 \ff \e5 \46 "
    n1 = " \12 \34 \ff \e5 \46 "
    
    AreEqual(s.startswith((m,None)), True)
    AreEqual(s.startswith((m,123, ["here","good"])), True)
    AreEqual(s.startswith(("nomatch",m,123, ["here","good"])), True)


    # with start parameter  = 0
    AreEqual(s.startswith((m,None),0), True)
    AreEqual(s.startswith((n,"nomatch"),0), False)
    AreEqual(s.startswith((s,"nomatch"),0), True)
    AreEqual(s.startswith((s + "a","nomatch"),0), False)
    AssertError(TypeError, s.startswith,(n,None),0)
    AssertError(TypeError, s.startswith,(None, n),0)
    AssertError(TypeError, s.startswith,(A, None, m),0)

    # with start parameter  > 0
    AreEqual(s.startswith((m1,None),4), True)
    AreEqual(s.startswith((m,"nomatch"),4), False)
    AreEqual(s.startswith((n1,"nomatch"),4), False)
    AreEqual(s.startswith((" \12 \34 \fd \e5 \45 ","nomatch"),4), False)
    AreEqual(s.startswith((s," \12 \34 \ff \e5 \45 matching string"),4), True)
    AreEqual(s.startswith((" \12 \34 \ff \e5 \45 matching string" + "a","nomatch"),4), False)
    AssertError(TypeError, s.startswith,(n1,None),4)
    AssertError(TypeError, s.startswith,(None, n1),4)
    AssertError(TypeError, s.startswith,(A, None, m1),4)

    AreEqual(s.startswith(("g",None),len(s) - 1), True)
    AreEqual(s.startswith(("g","nomatch"),len(s)), False)
    AreEqual(s.startswith(("g","nomatch"),len(s) + 400), False)

    # with start parameter  < 0
    AreEqual(s.startswith(("string",None),-6), True)
    AreEqual(s.startswith(("stro","nomatch"),-6), False)
    AreEqual(s.startswith(("strong","nomatch"),-6), False)
    AreEqual(s.startswith(("stringandmore","nomatch"),-6), False)
    AreEqual(s.startswith(("prefixandstring","nomatch"),-6), False)
    AssertError(TypeError, s.startswith,("string000",None),-6)
    AssertError(TypeError, s.startswith,(None, "string"),-6)
    AssertError(TypeError, s.startswith,(A, None, "string"),-6)

    AreEqual(s.startswith(("here",None),-len(s)), True)
    AreEqual(s.startswith((s,None),-len(s) - 1 ), True)
    AreEqual(s.startswith(("here",None),-len(s) - 400), True)

    # with start and end parameters
      # with +ve start , +ve end
        # end > start
    AreEqual(s.startswith((m1,None),4,len(s)), True)
    AreEqual(s.startswith((m1,None),4,len(s) + 100), True)
    AreEqual(s.startswith((n1,"nomatch"),len(s)), False)
    AssertError(TypeError, s.startswith,(n1,None),4, len(s))
    AssertError(TypeError, s.startswith,(None, n1),4 , len(s) + 100)
    AssertError(TypeError, s.startswith,(A, None, m1),4, len(s))

        # end < start
    AssertError(TypeError, s.startswith, (m1,None),4,3)
    AssertError(TypeError, s.startswith, (m1,None),4,2)
    AssertError(TypeError, s.startswith, (n1,None),4, 3)
    AssertError(TypeError, s.startswith, (None, n1),4 , 3)
    AssertError(TypeError, s.startswith, (A, None, m1),4, 0)
    
        # end == start
    AreEqual(s.startswith(("",None),4,4), True)
    AreEqual(s.startswith((m1,),4,4), False)
    AssertError(TypeError, s.startswith,(n1,None),4, 4)
    AssertError(TypeError, s.startswith,(None, n1),4 , 4)
    AssertError(TypeError, s.startswith,(A, None, m1),4, 4)

     # with -ve start , +ve end
       # end > start
    AreEqual(s.startswith(("string",None),-6, len(s)), True)
    AreEqual(s.startswith(("string",None),-6, len(s) + 100), True)
    AreEqual(s.startswith(("string","nomatch"),-6, len(s) -2), False)

    AreEqual(s.startswith(("stro","nomatch"),-6, len(s)-1), False)
    AreEqual(s.startswith(("strong","nomatch"),-6,len(s)), False)
    AssertError(TypeError, s.startswith,("string000",None),-6,len(s) + 3)
    AssertError(TypeError, s.startswith,(None, "string"),-6, len(s))
    AssertError(TypeError, s.startswith,(A, None, "string"),-6,len(s))

    AreEqual(s.startswith(("here",None),-len(s), 5), True)
    AreEqual(s.startswith(("here","nomatch"),-len(s), 2), False)
    AreEqual(s.startswith(("here",None),-len(s) - 1, 4 ), True)
    AreEqual(s.startswith(("here","nomatch"),-len(s) - 1, 2 ), False)

        # end < start
    AssertError(TypeError, s.startswith, ("string",None),-6, 10)
    AssertError(TypeError, s.startswith, ("string000",None),-6,10)
    AssertError(TypeError, s.startswith, (None, "string"),-6, 10)
    AssertError(TypeError, s.startswith, (A, None, "string"),-6,10)
    AreEqual(s.startswith(("stro","nomatch"),-6, 10), False)
    AreEqual(s.startswith(("strong","nomatch"),-6,10), False)
    
    
        # end == start
    AssertError(TypeError,s.startswith, ("string",None),-6, len(s) -6)
    AreEqual(s.startswith(("",None),-6, len(s) -6), True)


      # with +ve start , -ve end
        # end > start
    AreEqual(s.startswith((m1,None),4,-5 ), True)
    AreEqual(s.startswith((m1,"nomatch"),4,-(4  + len(m) +1) ), False)
    AssertError(TypeError, s.startswith,(n1,None),4, -5)
    AssertError(TypeError, s.startswith,(None, n1),4 , -5)
    AssertError(TypeError, s.startswith,(A, None, m1),4, -5)

        # end < start
    AssertError(TypeError, s.startswith, (m1,None),4,-len(s) + 1)
    AssertError(TypeError, s.startswith, (n1,None),4, -len(s))
    AssertError(TypeError, s.startswith, (None, n1),4 , -len(s))
    AssertError(TypeError, s.startswith, (A, None, m1),4, -len(s))
    
    AreEqual(s.startswith((m1,),4,-len(s) + 1), False)
    AreEqual(s.startswith((m1,),4,-500), False)
    
    
        # end == start
    AreEqual(s.startswith(("",None),4,-len(s)  + 4), True)
    AreEqual(s.startswith((m1,"nomatch"),4,-len(s)  + 4), False)
    AssertError(TypeError, s.startswith,(n1,None),4, -len(s)  + 4)
    AssertError(TypeError, s.startswith,(None, n1),4 , -len(s)  + 4)
    AssertError(TypeError, s.startswith,(A, None, m1),4, -len(s)  + 4)


      # with -ve start , -ve end
        # end > start
    AreEqual(s.startswith(("stri",None),-6, -2), True)
    AreEqual(s.startswith(("string","nomatch"),-6, -1), False)

    AreEqual(s.startswith(("stro","nomatch"),-6, -1), False)
    AreEqual(s.startswith(("strong","nomatch"),-6,-1), False)
    AreEqual(s.startswith(("stringand","nomatch"),-6,-1), False)

    AssertError(TypeError, s.startswith,("string000",None),-6, -1)
    AssertError(TypeError, s.startswith,(None, "string"),-6, -1)
    AssertError(TypeError, s.startswith,(A, None, "string"),-6,-1)

    AreEqual(s.startswith(("here","nomatch"),-len(s), -5), True)
    AreEqual(s.startswith(("here","nomatch"),-len(s), -len(s) + 2), False)
    AreEqual(s.startswith(("here","nomatch"),-len(s) - 1, -5 ), True)
    AreEqual(s.startswith(("here","nomatch"),-len(s) - 1,  -len(s) + 2), False)

        # end < start
    AssertError(TypeError, s.startswith, ("string",None),-6, -7)
    AssertError(TypeError, s.startswith, ("string000",None),-6,-8)
    AssertError(TypeError, s.startswith, (None, "string"),-6, -8)
    AssertError(TypeError, s.startswith, (A, None, "string"),-6,-8)
  
    AreEqual(s.startswith(("stro","nomatch"),-6, -8), False)
    AreEqual(s.startswith(("strong","nomatch"),-6,-8), False)

        # end == start
    AreEqual(s.startswith(("string","nomatch"),-6, -6), False)
    AreEqual(s.startswith(("",None),-6, -6), True)



def test_string_endswith():
    #failue scenarios
    class A:pass
    AssertError(TypeError,'string'.endswith,None)
    AssertError(TypeError,'string'.endswith,(None,"tring","ing"))
    AssertError(TypeError,'string'.endswith,(None,))
    AssertError(TypeError,'string'.endswith,(["this","is","invalid"],"ring","ing"))
    AssertError(TypeError,'string'.endswith,(("string","this is invalid","this is also invalid",),))
    AssertError(TypeError,''.endswith,None)
    AssertError(TypeError,''.endswith,(None,"tring","ring"))
    AssertError(TypeError,''.endswith,(None,))
    AssertError(TypeError,''.endswith,(["this","is","invalid"],"tring","ring"))
    AssertError(TypeError,''.endswith,(("string","this is invalid","this is also invalid",),))
    
    #Positive scenarios
    AreEqual('no matching string'.endswith(("matching","no","here")),False)
    AreEqual('here matching string'.endswith(("string", "matching","nomatch")), True)
    AreEqual('here matching string'.endswith(("string", "matching","here","string")), True)
    AreEqual('here matching string'.endswith(("matching","here","string",)), True)
    AreEqual('here matching string'.endswith(("here matching string","here matching string","here matching string",)), True)

    s = 'here \12 \34 \ff \e5 \45 matching string'
    m = "\e5 \45 matching string"
    m1 = "\e5 \45 matching "
    n = "\e5 \45 matching strinh"
    n1 = "\e5 \45 matching_"
    
    AreEqual(s.endswith((m,None)), True)
    AreEqual(s.endswith((m,123, ["string","good"])), True)
    AreEqual(s.endswith(("nomatch",m,123, ["here","string"])), True)

    #With starts parameter = 0
    AreEqual(s.endswith((m,None),0), True)
    AreEqual(s.endswith((n,"nomatch"),0), False)
    AreEqual(s.endswith((s,"nomatch"),0), True)
    AreEqual(s.endswith((s + "a","nomatch"),0), False)
    AssertError(TypeError, s.endswith,(n,None),0)
    AssertError(TypeError, s.endswith,(None, n),0)
    AssertError(TypeError, s.endswith,(A, None, m),0)

    #With starts parameter > 0
    AreEqual(s.endswith((m,None),4), True)
    AreEqual(s.endswith((m,"nomatch"),4), True)
    AreEqual(s.endswith((n1,"nomatch"),4), False)
    AreEqual(s.endswith((" \12 \34 \fd \e5 \45 ","nomatch"),4), False)
    AreEqual(s.endswith((s," \12 \34 \ff \e5 \45 matching string"),4), True)
    AreEqual(s.endswith((" \12 \34 \ff \e5 \45 matching string" + "a","nomatch"),4), False)
    AssertError(TypeError, s.endswith,(n1,None),4)
    AssertError(TypeError, s.endswith,(None, n1),4)
    AssertError(TypeError, s.endswith,(A, None, m1),4)

    AreEqual(s.endswith(("g",None),len(s) - 1), True)
    AreEqual(s.endswith(("g","nomatch"),len(s)), False)
    AreEqual(s.endswith(("g","nomatch"),len(s) + 400), False)

    #With starts parameter < 0
    AreEqual(s.endswith(("string",None),-6), True)
    AreEqual(s.endswith(("ring",None),-6), True)
    AreEqual(s.endswith(("rong","nomatch"),-6), False)
    AreEqual(s.endswith(("strong","nomatch"),-6), False)
    AreEqual(s.endswith(("stringandmore","nomatch"),-6), False)
    AreEqual(s.endswith(("prefixandstring","nomatch"),-6), False)
    AssertError(TypeError, s.endswith,("string000",None),-6)
    AssertError(TypeError, s.endswith,(None, "string"),-6)
    AssertError(TypeError, s.endswith,(A, None, "string"),-6)

    AreEqual(s.endswith(("string",None),-len(s)), True)
    AreEqual(s.endswith((s,None),-len(s) - 1 ), True)
    AreEqual(s.endswith(("string",None),-len(s) - 400), True)

    #With starts , end parameter
      # with +ve start , +ve end
        # end > start
    AreEqual(s.endswith((m1,"nomatch"),4,len(s)), False)
    AreEqual(s.endswith((m1,"nomatch"),4,len(s) - 6), True)
    AreEqual(s.endswith((m1,"nomatch"),4,len(s) - 8), False)
    AreEqual(s.endswith((n1,"nomatch"),4,len(s) - 6), False)
    AssertError(TypeError, s.endswith,(n1,None),4, len(s)-6)
    AssertError(TypeError, s.endswith,(None, n1),4 , len(s)-6)
    AssertError(TypeError, s.endswith,(A, None, m1),4, len(s)-6)

        # end < start
    AssertError(TypeError, s.endswith, (m1,None),4,3)
    AssertError(TypeError, s.endswith, (n1,None),4, 3)
    AssertError(TypeError, s.endswith, (None, n1),4 , 3)
    AssertError(TypeError, s.endswith, (A, None, m1),4, 0)
    
        # end == start
    AreEqual(s.endswith(("",None),4,4), True)
    AreEqual(s.endswith((m1,),4,4), False)
    AssertError(TypeError, s.endswith,(n1,None),4, 4)
    AssertError(TypeError, s.endswith,(None, n1),4 , 4)
    AssertError(TypeError, s.endswith,(A, None, m1),4, 4)

      # with -ve start , +ve end
        # end > start
    AreEqual(s.endswith((m1,None),-30, len(s) -6), True)
    AreEqual(s.endswith((m1,None),-300, len(s) -6 ), True)
    AreEqual(s.endswith((m1,"nomatch"),-5, len(s) -6), False)

    AreEqual(s.endswith(("string",None),-30, len(s) + 6), True)
    AreEqual(s.endswith(("string",None),-300, len(s) + 6 ), True)

    AreEqual(s.endswith(("here",None),-len(s), 4), True)
    AreEqual(s.endswith(("here",None),-300, 4 ), True)
    AreEqual(s.endswith(("hera","nomatch"),-len(s), 4), False)
    AreEqual(s.endswith(("hera","nomatch"),-300, 4 ), False)


    AssertError(TypeError, s.endswith,("here000",None),-len(s),4)
    AssertError(TypeError, s.endswith,(None, "here"),-len(s),4)
    AssertError(TypeError, s.endswith,(A, None, "here"),-len(s),4)

        # end < start
    AssertError(TypeError, s.endswith, ("here",None),-len(s) + 4, 2)
    AssertError(TypeError, s.endswith, ("here000",None),-len(s) + 4, 2)
    AssertError(TypeError, s.endswith, (None, "he"),-len(s) + 4, 2)
    AssertError(TypeError, s.endswith, (A, None, "string"),-len(s) + 4, 2)
  
    AreEqual(s.endswith(("hera","nomatch"),-len(s) + 4, 2), False)

        # end == start
    AssertError(TypeError,s.endswith, ("here",None),-6, len(s) -6)
    AreEqual(s.endswith(("",None),-6, len(s) -6), True)


      # with +ve start , -ve end
        # end > start
    AreEqual(s.endswith((m1,None),4,-6 ), True)
    AreEqual(s.endswith((m1,"nomatch"),4,-7), False)
    AssertError(TypeError, s.endswith,(n1,None),4, -6)
    AssertError(TypeError, s.endswith,(None, n1),4 , -6)
    AssertError(TypeError, s.endswith,(A, None, m1),4, -6)

        # end < start
    AssertError(TypeError, s.endswith, (m1,None),4,-len(s) + 1)
    AssertError(TypeError, s.endswith, (n1,None),4, -len(s))
    AssertError(TypeError, s.endswith, (None, n1),4 , -len(s))
    AssertError(TypeError, s.endswith, (A, None, m1),4, -len(s))
    
    AreEqual(s.endswith((m1,),4,-len(s) + 1), False)
    AreEqual(s.endswith((m1,),4,-500), False)
    
        # end == start
    AreEqual(s.endswith(("",None),4,-len(s)  + 4), True)
    AreEqual(s.endswith((m1,"nomatch"),4,-len(s)  + 4), False)
    AssertError(TypeError, s.endswith,(n1,None),4, -len(s)  + 4)
    AssertError(TypeError, s.endswith,(None, n1),4 , -len(s)  + 4)
    AssertError(TypeError, s.endswith,(A, None, m1),4, -len(s)  + 4)


      # with -ve start , -ve end
        # end > start
    AreEqual(s.endswith(("stri",None),-6, -2), True)
    AreEqual(s.endswith(("string","nomatch"),-6, -1), False)

    AreEqual(s.endswith(("stro","nomatch"),-6, -2), False)
    AreEqual(s.endswith(("stron","nomatch"),-6,-1), False)
    AreEqual(s.endswith(("stringand","nomatch"),-6,-1), False)

    AssertError(TypeError, s.endswith,("string000",None),-6, -1)
    AssertError(TypeError, s.endswith,(None, "string"),-6, -1)
    AssertError(TypeError, s.endswith,(A, None, "string"),-6,-1)

    AreEqual(s.endswith(("here","nomatch"),-len(s), -len(s)+4), True)
    AreEqual(s.endswith(("here","nomatch"),-len(s), -len(s) + 2), False)
    AreEqual(s.endswith(("here","nomatch"),-len(s) - 1, -len(s)+4 ), True)
    AreEqual(s.endswith(("here","nomatch"),-len(s) - 1,  -len(s) + 2), False)

        # end < start
    AssertError(TypeError, s.endswith, ("here",None),-len(s) + 5, -len(s) + 4)
    AssertError(TypeError, s.endswith, ("here000",None),-len(s) + 5, -len(s) + 4)
    AssertError(TypeError, s.endswith, (None, "here"),-len(s) + 5, -len(s) + 4)
    AssertError(TypeError, s.endswith, (A, None, "here"),-len(s) + 5, -len(s) + 4)
    
    AreEqual(s.endswith(("hera","nomatch"),-len(s) + 5, -len(s) + 4), False)

        # end == start
    AreEqual(s.endswith(("here","nomatch"),-6, -6), False)
    AreEqual(s.endswith(("",None),-6, -6), True)
    
def test_any():
    class A: pass
    a = A()
    
    class enum:
        def __iter__(self):
            return [1,2,3].__iter__()
        
    AreEqual(any(enum()),True) # enumerable class
    AssertError(TypeError,any,a)# non - enumerable class
    AssertError(TypeError,any,0.000000) # non - enumerable object
    AreEqual(any([0.0000000,0,False]),False)# all False
    AreEqual(any((0,False,a)),True) # True class
    AreEqual(any((0,False,None,"")),False) # None and ""
    AreEqual(any([]),False) # no items in array
    AreEqual(any([None]),False) # only None in array
    AreEqual(any([None,a]),True) # only None and an Object in array
    AreEqual(any({0:0,False:"hi"}),False) # Dict with All False
    AreEqual(any({True:0,False:"hi"}),True) # Dict with onely 1 True
    AreEqual(any({a:"hello",False:"bye"}),True) # Dict with Class

    class mylist(list):
        def __iter__(self):
            return [1,2,0].__iter__()
    AreEqual(any(mylist()),True)

    class raiser:
        def __bool__(self):
            raise RuntimeError

    AreEqual(any([None,False,0,1,raiser()]),True) # True before the raiser()
    AssertError(RuntimeError,any,[None,False,0,0,raiser(),1,2]) # True after the raiser()
    AssertError(RuntimeError,any,{None:"",0:1000,raiser():True}) # raiser in dict
    AssertError(TypeError,any) # any without any params
    AssertError(TypeError,any,(20,30,40),(50,60,70))# any with more params


def test_all():
    class A: pass
    a = A()
    
    class enum:
        def __iter__(self):
            return [1,2,3].__iter__()
        
    AreEqual(all(enum()),True) # enumerable class
    AssertError(TypeError,all,a) # non - enumerable class
    AssertError(TypeError,all,0.000000) # non - enumerable object
    AreEqual(all([0.0000000,0,False]),False) # all False
    AreEqual(all([True,1.89,"hello",a]),True) # all true array ( bool, double, str, class)
    AreEqual(all((True,1.89,"hello",a)),True) # all true tuple ( bool, double, str, class)
    AreEqual(all((True,"hello",a,None)),False) # one None in Tuple
    AreEqual(all((0,False,None,"")),False) # Tuple with None and ""
    AreEqual(all([]),True) # with empty array
    AreEqual(all([None]),False) # arry with onle None
    AreEqual(all([a,None]),False) # array with None and Class
    AreEqual(all({"hello":"hi",True:0,False:"hi",0:0}),False) # dict with some True, False
    AreEqual(all({True:0,100:"hi","hello":200,a:100}),True) # dict with all True

    class mylist(list):
        def __iter__(self):
            return [1,2,0].__iter__()
    AreEqual(all(mylist()),False)
    
    class raiser:
        def __bool__(self):
            raise RuntimeError

    AreEqual(all([None,False,0,1,raiser()]),False) # array With raiser() after false
    AreEqual(all({None:"",0:1000,raiser():True}),False) # Dict with raiser after falls
    AssertError(RuntimeError,all,[raiser(),200,None,False,0,1,2])# Array  with raiser before False
    AssertError(RuntimeError,all,{raiser():True,200:"",300:1000})# Dict  with raiser before False
    AssertError(TypeError,all) # no params
    AssertError(TypeError,all,(20,30,40),(50,60,70)) # extra params
    
def test_max_with_kwarg():
    class A(int):
        def __len__(self):
            return 10
    a=A()
    AreEqual(max(a,"aaaaaaa",key=len),a) # 2 args + buitin method
    def userfunc(arg):
        if(arg == None):return -1
        if(type(arg) == bool):return 0
        if(type(arg) == int):return 10
        if(type(arg) == str):return len(arg)
        if(type(arg) == list):return len(arg)
        return 40
        
    AreEqual(max(["b","aaaaaaaaaaaaaaaaa",["this",True,"is","Python"],0, a, None],key=userfunc),a)# array  + user method
    AreEqual(max(("b","aaa",["this",True,"is","Python"],0, 1.8, True,a, None),key=userfunc),1.8)# Tuple  + user method
    AreEqual(max("b","aaa",["this",None,"is","Python"], True,None,key=userfunc),["this",None,"is","Python"])# param list  + user method
    # error scenarios
    #apply invalid key k
    try: max("aaaaa","b",k=len)
    except TypeError:pass
    else: Fail("Expected TypeError, but found None")

    #apply non-existing Name
    try: max([1,2,3,4],key=method)
    except NameError:pass
    else: Fail("Expected TypeError, but found None")

    #apply non-callable Method
    method = 100
    try: max([1,2,3,4],key=method)
    except TypeError:pass
    else: Fail("Expected TypeError, but found None")

    #apply callable on empty list
    try: max([],key=len)
    except ValueError:pass
    else: Fail("Expected ValueError, but found None")

    #apply callable on non-enumerable type
    try: max(None,key=len)
    except TypeError:pass
    else: Fail("Expected TypeError, but found None")

    #apply Method on non callable class
    class B:pass
    try: max((B(),"hi"),key=len)
    except AttributeError:pass
    else: Fail("Expected AttributeError, but found None")


def test_min_with_kwarg():
    class A(int):
        def __len__(self):
            return 0
    a=A()
    AreEqual(min(a,"aaaaaaa",key=len),a) # 2 args + buitin method
    def userfunc(arg):
        if(arg == None):return 100
        if(type(arg) == bool):return 90
        if(type(arg) == int):return 80
        if(type(arg) == str):return len(arg)
        if(type(arg) == list):return len(arg)
        return 5
        
    AreEqual(min(["aaaaaaaaaaaaaaaaa",["this",True,"is","Python","Iron","Python"],0, a, None],key=userfunc),a)# array  + user method
    AreEqual(min(("aaaaaaaaaaaaa",["this",True,"is","Python","Iron","Python"],0, 1.8, True,a, None),key=userfunc),1.8)# Tuple  + user method
    AreEqual(min("aaaaaaaaaaaaaa",["this",None,"is","Python"], True,None,key=userfunc),["this",None,"is","Python"])# param list  + user method
    # error scenarios
    #apply invalid key k
    try: min("aaaaa","b",k=len)
    except TypeError:pass
    else: Fail("Expected TypeError, but found None")

    #apply non-existing Name
    try: min([1,2,3,4],key=method)
    except NameError:pass
    else: Fail("Expected TypeError, but found None")

    #apply non-callable Method
    method = 100;
    try: min([1,2,3,4],key=method)
    except TypeError:pass
    else: Fail("Expected TypeError, but found None")

    #apply callable on empty list
    try: min([],key=len)
    except ValueError:pass
    else: Fail("Expected ValueError, but found None")

    #apply callable on non-enumerable type
    try: min(None,key=len)
    except TypeError:pass
    else: Fail("Expected TypeError, but found None")

    #apply Method on non callable class
    class B:pass
    try: min((B(),"hi"),key=len)
    except AttributeError:pass
    else: Fail("Expected AttributeError, but found None")

def test_missing():
    # dict base class should not have __missing__
    AreEqual(hasattr(dict, "__missing__"), False)
    # positive cases
    class A(dict):
        def __missing__(self,key):
            return 100
    a = A({1:1, "hi":"bye"})
    def fun():pass
    AreEqual(hasattr(A, "__missing__"), True)
    AreEqual( (a["hi"],a[23112],a["das"], a[None],a[fun],getattr(a,"__missing__")("IP")),("bye",100,100,100,100,100))
    
    # negative case
    try: a[not_a_name]
    except NameError: pass
    except: Fail("Expected NameError, but found", sys.exc_info())
    else: Fail("Expected NameError, but found None")
    # extra paramaters
    AssertError(TypeError,a.__missing__,300,400)
    # less paramaters
    AssertError(TypeError,a.__missing__)
    AssertError(TypeError,a.__getitem__,A())
    
    #invalid __missing__ methods
    
    A.__missing__ = "dont call me!"
    AssertError(TypeError,a.__getitem__,300)
    
    # set missing to with new function
    def newmissing(self,key): return key/2
    A.__missing__ = newmissing
    AreEqual( a[999], 999/2);
    AssertError(TypeError,a.__getitem__,"sometext")
    
    del A.__missing__
    AreEqual( a[1], 1);
    AssertError(KeyError,a.__getitem__,"sometext")
    
    # inheritance scenarios
        #basic inheritance
    class M(dict):
        def __missing__(self,key): return 99
    class N(M):pass
    AreEqual(N({"hi":"bye"})["none"], 99)
        
    class C:
        def __missing__(self,key): return 100
    class D(C,dict):pass
    
    AreEqual(D({"hi":"bye"})["none"], 100)

        # inheritance -> override __missing__
    class E(dict):
        def __missing__(self,key): return 100
    class F(E,dict):
        def __missing__(self,key): return 50

    AreEqual(F({"hi":"bye"})["none"], 50)

def test_with():
    # nested with can not have a yield
    class J:
        def __enter__(self):pass
        def __exit__(self,a,b,c):pass
    try:
        c = compile(
"""def nest():
with J():
    with J():
        yield 100
""","","exec")
    except SyntaxError as e: pass


def test_importwarning():
    exc_list = []
    exc_list.append(ImportWarning())
    exc_list.append(ImportWarning("some message"))

    import exceptions
    exc_list.append(exceptions.ImportWarning())
    exc_list.append(exceptions.ImportWarning("some message"))
    
    for exc in exc_list:
        try:
            raise exc
        except exceptions.ImportWarning as e:
            pass

def test_overflowwarning():
    AssertError(AttributeError, lambda: exceptions.OverflowWarning)

@skip("silverlight")
def test_cp5609():
    from os import remove
    temp_name = "test_cp5609.txt"

    with open(temp_name, "w") as f:
        Assert(not f.closed)
        f.write("xyz")
        Assert(hasattr(f, "__enter__"))
        Assert(hasattr(f, "__exit__"))
    Assert(f.closed)
    
    with open(temp_name, "r") as f:
        Assert(not f.closed)
        AreEqual(f.readlines(), ["xyz"])
        Assert(hasattr(f, "__enter__"))
        Assert(hasattr(f, "__exit__"))
    Assert(f.closed)
    
    remove(temp_name)
    
    


run_test(__name__)
