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

# Test that sys.exc_info() is properly set.

from iptest.assert_util import *


import sys

# Rules:
# 1) thread has a current exception
# 2) except block sets current exception
# 3) when method returns (either normally, via exception, or via generator),
#    it restores the current exception to what it was on function entry.
#
# exc_info() is pretty useless in a finally block. It may be non-null, but it's value
# depends on how exactly we got to execute the finally. In fact, in a finally, exc_info
# may be non-null, but not for the current exception.
# Or it may be null, even when there's an outstanding exception. $$$ is that true?

# return the current exception arg or None
# raise ValueError(15)  ; E() returns 15
def E():
  t = sys.exc_info()[1]
  if t == None: return None
  return t[0]

# helper for easy asserts on E
def A(v):
  AreEqual(E(), v)

#--------------------------------------
# Tests
def test_simple():
  A(None)
  try:
    raise ValueError(15)
  except:
    A(15)
  A(15) # still valid after exception block, but still in function


# test setting in except params
def test_excep_params():
  def t(): # called as argument in except clause
    A(63)
    return TypeError
  def f():
    try:
      raise ValueError(63)
    except t(): # not matching
      Assert(False)
  try:
    f()
  except:
    A(63)


# raise new ex out of catch; check in finally
def test_except_rethrow():
  def f():
    try:
      raise ValueError(81)
    except:
      A(81)
      # raise a new exception type. exc_info doesn't get updated
      # until we hit a new except block
      raise ValueError(43)
    finally:
      # still using the original exc since we haven't hit a new exc block yet.
      A(81)
  try:
    f()
  except:
    A(43)
  A(43)

# finally, same function as active except, exception path
def test_fin_except():
  A(None)
  try:
    raise ValueError(20)
  except:
    A(20)
  finally:
    A(20) # active from except block
  A(20) # still active


# finally doesnt see exc_info when there's no catcher.
def test_fin_except2():
  def f1():
    A(None)
    raise ValueError(20)
  def f2():
    A(None)
    try:
      f1() # throw from a different function
      Assert(False)
    finally:
      # we should be here via the exceptional path.
      # but since there's no except block in here, exc_info not set.
      A(None)
    Assert(False)
  try:
    f2()
  except:
    A(20)
  A(20)


# Finally w/o an except block does not see the exception.
# compare to test_fin_except()
def helper_fin_no_except():
  A(None)
  try:
    raise ValueError(15)
  finally:
    A(None) # no except block, so not set.

def test_fin_no_except():
  try:
    helper_fin_no_except()
  except:
    A(15)
  A(15)

#
# inactive except block.
# The mere presence of an except block is enough to set exc_info(). We don't
# need to actually execute the handlers.
def helper_fin_inactive():
  A(None)
  try:
    raise ValueError(20)
  except TypeError: # mismatched, still causes exc_info() to be set
    Assert(False)
  finally:
    A(20) # still set even from inactive block
  A(20) # still active


def test_fin_inactive():
  try:
    helper_fin_inactive()
  except: # prevent from going unhandled
    A(20)


# Non exception path
def test_fin_normal():
  A(None)
  try:
    pass
  finally:
    A(None)
  A(None)




# Nested
def test_nested():
  try:
    try:
      try:
        raise ValueError(15)
      except:
        A(15)
      A(15)
    except:
      Assert(False)
    A(15)
    try:
      A(15)
      # Now raise a new exception. This becomes the current exc_info() value.
      raise ValueError(20)
    except:
      A(20)
    A(20)
  except:
    Assert(False)
  A(20)



   
  
# Child function inherits exc_info() from parent, but can't change parents.
# only changed by a function having an except block.
def test_call():
  def f():
    A(7) # parent is already in a except block.
    try:
      raise ValueError(20)
    except:
      A(20)
    A(20)
    # will be restored to 7 on function return
  #
  try:
    raise ValueError(7)
  except:
    A(7)
    f()
    A(7)

# Test with multiple calls and ensure value is restored
def test_call2():
  def f3a():
    A(55)
    try:
      raise ValueError(11)
    except:
      A(11)
    A(11)
  def f3b():
    A(55)
    try:
      raise ValueError(22)
    except:
      A(22)
      return # return from Except, swallows Ex
    Assert(False)
  def f2():
    A(55)
    f3a()
    A(55)
    f3b()
    A(55)
  #
  try:
    A(None)
    raise ValueError(55)
  except:
    A(55)
    f2()
    A(55)
         

# Still set in finally on return.
def test_ex_fin():
  try:
    try:
      raise ValueError(25)
    except:
      A(25)
      return 7
  finally:
    # still set from the except block
    A(25)

# like test_ex_fin, but when we split into an inner function, it gets reset
def test_funcs():
  def f():
    try:
      try:
        raise ValueError(27)
      except:
        A(27)
        raise # rethrow
    finally:
      # on exceptional path. Since this function had a except clause
      # in the function, exc_info() is still set.
      A(27)
  try:
    try:
      f()
    finally:
      A(None) # exc_info reset since thrown from different function
  except:
    A(27)
    pass
  


# ???
# Tests splitting across multiple functions to show reset
def f():
  pass


# Test with exc_info and generators.
# The first yield in the except block is a return from the function and clears
# the current exception status.


def test_generator():
  def f():
    try:
      raise ValueError(3)
    except:
      A(3)
      yield 1 # this will reset exc_info
      A(None)
      yield 2
      A(5) # pick up from caller
      try:
        yield 3 # generator will call next when exc_info=Val(6) here.
      finally:
        # We're in the non-exception path of a finally, but still have exc_info set since
        # generator was called from a catch block.
        A(6)
      yield 4
      A(6) # still set from generator's caller
    A(6) #
    yield 5
  # call the generator
  g=f()
  AreEqual(next(g), 1)
  A(None) # generator's exc value shouldn't taint the caller
  AreEqual(next(g), 2)
  A(None) # clear after returning from yield
  try:
    raise ValueError(5) # New exception!
  except:
    A(5)
    # Now call back into the generator with a new exc_info!
    AreEqual(next(g), 3)
    A(5)
  A(5)
  try:
    A(5)
    raise ValueError(6) # New exception
  except:
    A(6)
    # this will execute a finally in the generator.
    AreEqual(next(g), 4)
    A(6)
  A(6)
  AreEqual(next(g), 5)


# throw out of generator
# ensure that exc_info() is cleared.
def test_gen_throw():
  def f():
    try:
      yield 1 # caller will g.Throw() from here
    except:
      A(87)
      raise ValueError(22) # throw new error
  #
  g=f()
  A(None)
  AreEqual(next(g), 1)
  A(None)
  try:
    try:
      g.throw(ValueError(87))
      Assert(False)
    finally:
      # exceptional path.
      # exc_info should have been cleared on exiting generator.
      A(None)
  except:
    A(22)
  A(22)
  

#---------------------------------------------------------------------
#
# Test sys.exc_clear(), which was added in Python 2.3
# This clears the last exception status.
#
#---------------------------------------------------------------------

# simple case of clear in an except block.
def test_clear_simple():
  try:
    raise ValueError(12)
  except:
    A(12)
    sys.exc_clear()
    A(None)
  A(None)

# cases with nesting.
def test_clear_nested():
  try:
    raise ValueError(13)
  except:
    try:
      A(13)
      raise ValueError(54)
    except:
      A(54)
      sys.exc_clear()
      A(None)
    A(None)
  A(None)

#
def test_clear_nested_func():
  def f():
    try:
      A(13)
      raise ValueError(54)
    except:
      A(54)
      sys.exc_clear()
      A(None)
    A(None) # will be restored after func returns
  #
  try:
    raise ValueError(13)
  except:
    A(13)
    f() # calls sys.exc_clear()
    A(13) # still restored even after clear
  A(13)
  

# Test clearing when there isn't an active exception (outside except block)
def test_clear_no_active_ex():
  A(None)
  sys.exc_clear()
  A(None)
  try:
    sys.exc_clear()
    A(None)
  except:
    pass
  try:
    pass
  finally:
    sys.exc_clear()
    A(None)
  A(None)

#---------------------------------------------------------------------
# With!
#---------------------------------------------------------------------


#========================================================
# With's Pep (http://www.python.org/dev/peps/pep-0343/) says the
# __exit__ can be invoked by an except block,
# but unlike a normal except, that shouldn't set sys.exc_info().


# a manager class to use 'with' statement
class ManBase():
  def __enter__(self):
    A(None)
    pass
  # exit is invoked when 'with' body exits (either via exception, branch)
  def __exit__(self, t,v, tb):
    A(None)
    return True # swallow exception


# Simple case, no exception set.
def test_with_simple():
  class M1(ManBase):
    pass
  with M1():
    pass

# with.__exit__ doesn't see exception in exception case.
def test_with_fail():
  class M2(ManBase):
    # exit is invoked when 'with' body exits (either via exception, branch)
    def __exit__(self, t,v, tb):
      AreEqual(v[0], 15) # exception passed in as local
      if is_ironpython: #http://ironpython.codeplex.com/workitem/27990
        A(None) # but sys.exc_info() should not be set!!
      else:
        A(15)
      return True # swallow exception
  #
  # With.__exit__ does not see current exception
  with M2():
    raise ValueError(15)


# call 'with' from an except block
def test_with_except_pass():
  class M2(ManBase):
    def __enter__(self):
      A(15)
    # exit is invoked when 'with' body exits (either via exception, branch)
    def __exit__(self, t,v, tb):
      AreEqual(v, None) #
      A(15) #
      return True # swallow exception
  #
  # With.__exit__ does not see current exception
  try:
    raise ValueError(15)
  except:
    A(15)
    with M2():
      A(15)
      pass
    A(15)


# call 'with' from an except block, do failure case
def test_with_except_fail():
  class M2(ManBase):
    def __enter__(self):
      A(15)
    # exit is invoked when 'with' body exits (either via exception, branch)
    def __exit__(self, t,v, tb):
      AreEqual(v[0], 34) # gets failure from With block
      if is_ironpython: #http://ironpython.codeplex.com/workitem/27990
        A(15) # gets failure from sys.exc_info() which is from outer except block
      else:
        A(34)
      return True # swallow exception
  #
  # With.__exit__ does not see current exception
  try:
    raise ValueError(15)
  except:
    A(15)
    with M2():
      A(15)
      raise ValueError(34)
    if is_ironpython: #http://ironpython.codeplex.com/workitem/27990
        A(15)
    else:
        A(34)



run_test(__name__)



