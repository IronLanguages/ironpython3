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
# Test Pep 342 enhancements to generator, including Throw(), Send(), Close() and yield expressions.
#

from iptest.assert_util import *

# Declare some dummy exceptions to throw
class MyError(Exception):
  pass

class MyError2:
  pass

# ensure the generator is finished.
def EnsureClosed(g):
  try:
    g.next()
    Assert(False)
  except StopIteration:
    pass


# test __del__ on generators.
import sys
import gc

# Test that generator.__del__ is invoked and that it calls Close()
# Note that .NET's GC:
# 1) runs on another thread,
# 2) runs at a random time (but can be forcibly invoked from gc.collect)
# So other generators that go out of scope will get closed() called at random times from wherever
# the generator was left. This can introduce some nondeterminism in the tests.

# Note that silverlight doesn't support finalizers, so we don't test Generator.__del__ on that platform.
skiptest("silverlight")
def test_del():
  global l
  l=[0]
  def nested():
    def ff3(l):
      try:
        yield 10
      finally:
        l[0] += 1
    g=ff3(l)
    AreEqual(g.next(), 10) # move to inside the finally
    del g
    
  nested()
  gc.collect()
  AreEqual(l,[1]) # finally should have execute now.



# Yield can appear in lambda expressions (or any function body).
# A generator lambda expression yields its final result, instead of just returning it.
def test_yield_lambda():
  f=lambda x: (3+(yield x), (yield x*2))
  g=f(10)
  AreEqual(g.next(), 10)
  AreEqual(g.send(9), 10*2)
  if is_cpython: #http://ironpython.codeplex.com/workitem/28219
    AssertError(StopIteration, g.send, 5)
  else:
    AreEqual(g.send(5), (3+9, 5))

  
# This usage of lambda expression tests a different parsing path in IPY. (old lambda expressions)
def test_yield_old_lambda():
    l=[x for x in lambda : (yield 3),8]
    AreEqual(l[1], 8)
    f=l[0]
    g=f()
    AreEqual(g.next(), 3)


def test_type_generator():
  def g(): yield 10
  def f(): x += yield
  AreEqual(type(f()), type(g()))


# CPython 2.5 allows yield as a default parameter for lambda expressions
# (though not for regular def functions)
def test_yield_default_param():
    # This will return a generator that
    # defines a lambda expression, with default param initialized by send()
    # returns that lambda expression
    def f():
      yield lambda x=(yield 25): x * 2
      
    g=f()
    AreEqual(g.next(), 25)
    l = g.send(15) # this sends in the default parameter, yields the lambda expression
    AreEqual(l(), 15*2) # this now uses the default param
    AreEqual(l(3), 3*2) # use a non-default param.
    AreEqual(l(), 15*2)


#
# A yield expression can occur anywhere. Test various spots.
#

# Test yield in a genexp body. This is a little bizare, but the CPython tests have this.
def test_yield_genexp():
    # def f():
    #   for i in range(5):
    #     yield (yield i)
    #
    # Since list() ctor only calls next, (yield i) returns None
    g=((yield i) for i in range(5))
    x = list(g)
    AreEqual(x, [0, None, 1, None, 2, None, 3, None, 4, None])

# test using yield expressions in indexing
def test_yield_index():
  def f():
    # eval order is 1[2]=3
    (yield)[(yield)]='x'
    yield
  g=f()
  AreEqual(g.next(), None)
  l=[10,20,30]
  g.send(l)
  g.send(1)
  AreEqual(l[1], 'x')
 
  


#---------------------------
# test send with yield expression

def test_yield_exp():
    def side_effect(l, i, res):
      l[i] += 1
      return res
    def f(l):
      # first test simple yield expression
      AreEqual((yield 3), 100)
      # test an empty yield. Equivalent to 'yield None'
      yield
      # now test yield embedded in a complex expression with side-effects and evaluation order.
      x = side_effect(l, 0, 5) + (yield 10) + side_effect(l, 1, 2)
      yield x
    l=[0,0]
    g=f(l)
    AreEqual(g.next(), 3)
    AreEqual(g.send(100), None)
    AreEqual(g.next(), 10)
    AreEqual(l, [1,0])
    AreEqual(g.send(30), 37)
    AreEqual(l, [1,1])
    
# test different parsing configurations of yield expression
# - Top-level assignment (=, +=) does not require parenthesis.
# - else yield used as expression does require parenthesis.
# - argument to yield is optional
def test_yield_exp_parse():
    def f():
      # yield as statement, yielding tuple
      yield 1,2
      # top-level assignment. Doesn't need parenthesis
      x = yield
      AreEqual(x,15)
      x = yield 10
      AreEqual(x,None)
      y = 5
      y += yield 99
      AreEqual(y, 105)
      y += yield
      AreEqual(y, 145)
      # test precedence. This is w = (yield (1,2)). Not w=(yield 1), 2
      w = yield 1,2
      AreEqual(w,39)
      # yield in an expression, must be in parenthsis
      z = (yield) / (yield)
      AreEqual(z,100/25)
      yield 123
    g=f()
    AreEqual(g.next(), (1,2))
    AreEqual(g.next(), None)
    AreEqual(g.send(15), 10)
    AreEqual(g.next(), 99)
    AreEqual(g.send(100), None)
    AreEqual(g.send(40), (1,2))
    AreEqual(g.send(39), None)
    AreEqual(g.send(100), None)
    AreEqual(g.send(25), 123)
    
    

# Test some goofier places to put a yield expression
def test_yy():
  def f():
    yield (yield 5)
  g=f()
  AreEqual(g.next(), 5)
  AreEqual(g.send(15), 15)

# Test Send after Close(), should throw StopException, just like Next()
def test_send_after_closed():
  l = [0]
  def f():
    x = yield 10
    l[0] += 1
    AreEqual(x, 15)
  g = f()
  AreEqual(g.next(), 10)
  def t():
    g.send(15)
  AreEqual(l, [0])
  AssertError(StopIteration, t)
  AreEqual(l, [1])
  EnsureClosed(g)
  AssertError(StopIteration, t)
  AreEqual(l, [1]) # no more change
      

# Test: send(non-none) fails on newly created generator
def test_send_unstarted():
  def f():
    x = yield 10
    AreEqual(x,None) # next() is like send(None)
    yield 5
  g = f()
  def t():
    g.send(1)
  AssertError(TypeError, t) # can't send non-null on unstarted
  # should not change generator status
  AreEqual(g.next(), 10)
  AreEqual(g.next(), 5)

  
# Ensure that sending an exception doesn't become a throw
def test_send_exception():
  def f():
    y = yield
    AreEqual(y, MyError)
    yield
  g=f()
  g.next()
  g.send(MyError)
      
 


#-----------------------------


#
# Throw not handled in iterator
#
def test_throw_unhandled():
    # Simple iterator
    def f():
      # Caller will throw an exception after getting this value
      yield 5
      Assert(False) # Iterator should not get here

    g = f()

    i = g.next()
    AreEqual(i,5)

    # This should go uncaught from the iterator
    try:
      g.throw(MyError)
      Assert(False) # expected exception
    except MyError:
      pass # 'Good: Exception passed through generator and caught by caller'


#
# Throw handled in iterator
#
def test_throw_handled():
    def f2():
      yield 1
      try:
        yield 2  # caller throws from here
        Assert(False) # unreachable
      except MyError:
        pass # 'Good: Generator caught exception from throw'
        yield 3
      yield 4

    g = f2()
    AreEqual(g.next(),1)
    AreEqual(g.next(),2)

    # generator will catch this.
    # this throws from the last yield point, resumes executing the generator
    # and returns the result of the next yield point.
    i = g.throw(MyError)
    AreEqual(i,3)

    # Test that we can call next() after throw.
    AreEqual(g.next(),4)


#
# Test another throw overload passing (type,value).
#
def test_throw_value():
    class MyClass2:
      def __init__(self,val):
        self.val = val

    def f():
      try:
        yield 5
        Assert(false)
      except MyClass2, x:
        AreEqual(x.val,10)
        yield 15

    g=f()
    AreEqual(g.next(), 5)
    AreEqual(g.throw(MyClass2, 10), 15)




#
# Test catch and rethrow
#
def test_catch_rethrow():
    def f4():
      try:
        yield 1
        Assert(False)
      except MyError:
        raise MyError2
      
    g=f4()
    g.next() # move into try block
    try:
      g.throw(MyError) # will get caught and rethrow MyError 2
      Assert(False)
    except MyError2: # catch different error than thrown
      pass




#
# Throw as first call  on the iterator.
# In this case, throw does not get to the first yield point.
#
def test_throw_unstarted():
    def f3():
      # haven't called next yet, so throw shouldn't execute anything
      # it should also be before (outside) the try block on the first line.
      try:
        Assert(False)
        yield 5
      except:
        Assert(False)

    # 'Test: throw before first yield'
    g = f3()
    try:
      g.throw(MyError)
      Assert(False)
    except MyError:
      pass
    EnsureClosed(g) # generator should now be closed.

# Throw after closed, should raise its exception,
# not another StopIteration / other exception
# Note this is a little inconsistent with Next(), which raises a StopIteration exception
# on closed generators.
def test_throw_closed():
    def f5():
      yield 1

    g=f5()
    AreEqual(g.next(),1)

    # Loop this to ensure that we're in steady state.
    for i in range(0,3):
      try:
        # G is now closed.
        g.throw(MyError)
        Assert(False)
      except MyError:
        pass # 'Good: caught our own exception'

#
# test that a generator.Throw() works when stopped in a finally
#
def test_throw_from_finally():
    def f(l):
      try:
        pass
      finally:
        pass # ' good: inside finally'
        l[0] = 1
        yield 1
        try:
          yield 2 # throw here
          Assert(False) #
        except MyError:
          l[0] = 2


    l=[0]
    g=f(l)
    AreEqual(g.next(), 1)
    AreEqual(l[0], 1)

    AreEqual(g.next(), 2) # move into finally block
    try:
      # throw, it will catch and run to completion
      g.throw(MyError)
      Assert(False)
    except StopIteration:
      AreEqual(l[0], 2)
      pass # ' good: threw and generator ran to completion'
      pass


#
# Test that finallys properly execute when Gen.Throw is called.
# This verifies that the exception is really being raised from the right spot
# within the generator body.
#





# simple generator with finally
# set l[0]=1 to indicate that finally block was executed.
def f1(l):
  yield 1
  try:
    yield 2
    pass # '  Non exception case'
  finally:
    pass # '  inside finally'
    l[0] = 1
  yield 3

def test_throw_run_finally_nonexception():
    # Sanity check
    # 'Test: simple finally, no exception'
    l = [0]
    g=f1(l)
    AreEqual(g.next(), 1)
    AreEqual(g.next(), 2)
    AreEqual(l[0], 0)
    AreEqual(g.next(), 3)
    AreEqual(l[0], 1)
    EnsureClosed(g)


#
# Now try throwing before finally
#
def test_throw_before_finally():
    l = [0]
    g=f1(l)
    AreEqual(g.next(), 1)
    try:
      g.throw(MyError)
      Assert(False)
    except MyError:
      pass
    AreEqual(l[0], 0) # finally should not have been executed

    # since we terminated with an exception, generator should be closed
    EnsureClosed(g)

#
# Now try throwing in range of finally, so that finally is executed
#
def test_throw_run_finally_exception():
    # print 'Test: throw inside try-finally'
    l = [0]
    g=f1(l)
    AreEqual(g.next(), 1)
    AreEqual(g.next(), 2)
    try:
      g.throw(MyError)
      Assert(False)
    except MyError:
      pass

    # since we terminated with an exception, generator should be closed
    EnsureClosed(g)
    AreEqual(l[0], 1) # finally should have run



#
# Test that code/exceptions are being invoked from the right callstack,
# either
#   a) inside the generator body, or
#   b) at the call to Generator.Throw(), but outside the generator body.
# This is important so that the right set of catch blocks get applied.
#

# Creating the exception occurs inside the generator.
def test_ctor_throws():
    # Simple class to raise an error in __init__
    class MyErrorClass:
      def __init__(self):
        raise MyError

    def f():
      try:
        yield 5
        yield 7
      except MyError:
        yield 12

    g=f()
    AreEqual(g.next(), 5)

    # MyError's ctor will raise an exception. It should be invoked in the generator's body,
    # and so the generator can catch it and continue running to yield a value.
    AreEqual(g.throw(MyErrorClass), 12)
    
    g.close()

#
# Test corner case with Throw(None)
#

def test_throw_none():
  def f():
    try:
      yield 5 # we'll be stopped here and do g.throw(none)
      yield 10
    except TypeError:
      Assert(false) # error shouldn't be raised inside of generator, so can't be caught here
  
  g=f()
  AreEqual(g.next(), 5)
  
  # g.throw(None) should:
  # - throw a TypeError immediately, not from generator body (So generator can't catch it)
  # - does not update generator
  def t():
    g.throw(None)
  AssertError(TypeError, t)
  
  # verify that generator is still valid and can be resumed
  AreEqual(g.next(), 10)


#
# Test close(), which builds on throw()
#
def f(l):
  try:
    yield 1
  finally:
    l[0] += 1
  
l =[0]
g=f(l)

# Test close() on unstarted and closed generators
def test_close_ends():
  def f():
    Assert(False) # we won't execute the generator
    yield 10
  g = f()
  g.close() # close on unstarted
  EnsureClosed(g)
  f().close() # close already closed, should be nop.
 

def test_close_catch_exit():
  def f():
    try:
      yield 1 # caller will close() from here
      Assert(False)
    except GeneratorExit:
      pass # catch but exit, that's ok.
  g=f()
  AreEqual(g.next(), 1)
  g.close()

def test_close_rethrow():
  def f():
    try:
      yield 1 # caller will close() from here
      Assert(False)
    except GeneratorExit:
      # print 'caught and rethrow'
      raise MyError
  g=f()
  AreEqual(g.next(), 1)
  # close(), which will raise a GeneratorExit, which gets caught and rethrown as MyError
  def t():
    g.close()
  AssertError(MyError, t)

def test_close_illegal_swallow():
  def f():
    try:
      yield 1 # caller will close() from here
      Assert(False)
    except GeneratorExit:
      yield 2 # illegal, don't swallow GeneratorExit
  g=f()
  AreEqual(g.next(), 1)
  # close(), which will raise a GeneratorExit, which gets caught and rethrown as MyError
  def t():
    g.close()
  AssertError(RuntimeError, t)




#
# A (yield) expressions can appear in practically any spot as a normal expression.
# Test a smattering of interesting spots for a variety of coverage.
#

#
# this is straight from the sample in Pep342
# Useful to skip the first call to generator.next() for generators that are consumers.
def consumer(func):
            def wrapper(*args,**kw):
                gen = func(*args, **kw)
                gen.next()
                return gen
            wrapper.__name__ = func.__name__
            wrapper.__dict__ = func.__dict__
            wrapper.__doc__  = func.__doc__
            return wrapper

# Yield in the middle of a tuple
def test_exp_tuple():
  def f():
    yield (1,(yield),3)
  g=f()
  g.next()
  AreEqual(g.send(5), (1, 5, 3))


#
#  Yield as a base class
#

def test_exp_base_class():
    class MyBase(object):
      def b(self):
        return 5

    # generator to make a base class.
    @consumer
    def M():
      # yield expression as a base class.
      class Foo((yield)):
        def m(self):
          print 'x'
      yield Foo

    g=M()
    F = g.send(MyBase)
    c=F()
    AreEqual(c.b(),5) # invokes base method


#
# In print redirection slot.
#
class MyWriter:
  data=""
  def write(self,l):
    self.data += l

def test_exp_print_redirect():
    @consumer
    def f(text):
      print >> (yield), text,
      yield # extra spot to stop on so send() won't immediately throw

    c=MyWriter()
    f("abc").send(c)
    AreEqual(c.data, "abc")

#
# In dict literals
#
def test_exp_dict_literals():
    def f():
      # Note eval order is: {2:1, 4:3}
      d = { (yield 2): (yield 1), (yield): (yield) }
      yield d
    g=f()
    AreEqual(g.next(), 1)
    AreEqual(g.send('a'), 2)
    g.send(10)
    g.send('b')
    d2 = g.send(20) # {10: 'a', 20: 'b'}
    AreEqual(d2, {10: 'a', 20: 'b'})

#
# Test yield expressions in compound comparisons
#

def gen_compare():
  f = ((yield 1) < (yield 2) < (yield 3))
  yield f

# Compare expecting true.
def test_exp_compare1():
    g=gen_compare()
    AreEqual(g.next(), 1)
    AreEqual(g.send(5), 2)
    AreEqual(g.send(10), 3)
    AreEqual(g.send(15), True)
    EnsureClosed(g)

# compare expecting false. This will short-circuit
def test_exp_compare2():
    g=gen_compare()
    AreEqual(g.next(), 1)
    AreEqual(g.send(5), 2)
    AreEqual(g.send(2), False)
    EnsureClosed(g)

#
# Use as the argument to Raise
#
def test_exp_raise():
    @consumer
    def f():
      raise (yield), (yield)
      Assert(False)
    g=f()
    g.send(ValueError)
    try:
      g.send(15)
    except ValueError, x:
      AreEqual(x.args[0], 15)
    # Generator is now closed
    EnsureClosed(g)


#
# Slicing. Nothing fancy here, just another place to try yield
#
def test_exp_slice():
    @consumer
    def f():
      l=range(0,10)
      yield l[(yield):(yield)]

    g=f()
    g.send(4)
    AreEqual(g.send(7), [4, 5, 6])


#
# Layering. Have multiple coroutines calling each other.
#
def test_layering():
    # manually implement the @consumer pattern
    def append_dict(d):
      def f2():
        while True:
          (a,b) = ((yield), (yield))
          d[a]=(b)
      g=f2()
      g.next()
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
    d={}
    g=splitter(append_dict(d))
    #
    g.send(('a', 10))
    AreEqual(d, {'a': 10})
    #
    g.send(('b', 20))
    AreEqual(d, {'a': 10, 'b': 20})
    #
    g.send(('c', 30))
    AreEqual(d, {'a': 10, 'c': 30, 'b': 20})


#
# watered down example from Pep342
#
def test_layering_2():
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
           print >> outstream, 'Page=' + (yield)
         except GeneratorExit:
           print >> outstream, 'done'
           raise
    #
    def DoIt(l, outstream):
      pipeline = Pager(Writer(outstream))
      for i in l:
        pipeline.send(i)
      pipeline.close()
    #
    o=MyWriter()
    DoIt(range(8), o)
    AreEqual(o.data, 'Page=[0,1,2]\nPage=[3,4,5]\nPage=[6,7...incomplete\ndone\n')


#
# Test Yield in expressions in an except block
# even crazier example, (yield) in both Type + Value spots in Except clause
#

# generator to use with test_yield_except_crazy*
def getCatch():
  yield 1
  l=[0,1,2]
  try:
    raise MyError, 'a'
  except (yield 'a'), l[(yield 'b')]:
    AreEqual(sys.exc_info(), (None,None,None)) # will print None from the yields
    Assert(l[1] != 1) # validate that the catch properly assigned to it.
    yield 'c'
  except (yield 'c'): # especially interesting here
    yield 'd'
  except:
    print 'Not caught'
  print 4

# executes the generators 1st except clause
def test_yield_except_crazy1():
    g=getCatch()
    AreEqual(g.next(), 1)
    AreEqual(g.next(), 'a')
    AreEqual(sys.exc_info(), (None, None, None))
    AreEqual(g.send(MyError), 'b')
    AreEqual(sys.exc_info(), (None, None, None))
    AreEqual(g.send(1), 'c')
    g.close()

# executes the generators 2nd except clause
def test_yield_except_crazy2():
    # try the 2nd clause
    g=getCatch()
    AreEqual(g.next(), 1)
    AreEqual(g.next(), 'a')
    AreEqual(g.send(ValueError), 'c') # Cause us to skip the first except handler
    AreEqual(g.send(MyError), 'd')
    g.close()

# Yield statements without any return values.
def test_yield_empty():
    def f():
        yield
    
    g = f()
    AreEqual(g.next(), None)
    
    def f():
        if True:
            yield
        yield
    g = f()
    AreEqual(g.next(), None)
    AreEqual(g.next(), None)

def test_throw_stop_iteration():
    def f():
        raise StopIteration('foo')
        yield 3
    
    x = f()
    try:
        x.next()
    except StopIteration, e:
        AreEqual(e.message, 'foo')

run_test(__name__)
