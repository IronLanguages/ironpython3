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
'''
This test module tests all new features available in .NET 4.0 which are relevant
for Python.
'''

#--IMPORTS---------------------------------------------------------------------
from iptest.assert_util import *
skiptest("silverlight")
skiptest("win32")

import System

if System.Environment.Version.Major<4:
    import sys
    print("Will not run this test against versions of the CLR earlier than 4.0")
    sys.exit(0)

import System.Threading.Tasks.Parallel as Parallel
import System.Diagnostics.Contracts.Contract as Contract

#TODO
#add_clr_assemblies(...)
#from Merlin.Testing import *
#

#--GLOBALS---------------------------------------------------------------------

#--TEST CASES------------------------------------------------------------------
def test_system_threading_tasks():
    '''
    http://msdn.microsoft.com/en-us/library/system.threading.tasks(VS.100).aspx
    http://msdn.microsoft.com/en-us/library/dd460713(VS.100).aspx
    
    Shouldn't be necessary to test this (i.e., not directly supported by IP, 
    but will work), but as it's a significant .NET 4.0 feature minimally a 
    sanity test is in order here.
    '''
    #0..10000
    temp_list  = list(range(10000))
    temp_list_output = []
    
    #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=25859
    Parallel.For.Overloads[int, int, System.Action[int]](0,
                                                         len(temp_list),
                                                         lambda x: temp_list_output.append(x))
    AreEqual(len(temp_list), len(temp_list_output))
    temp_list_output.sort()
    AreEqual(temp_list, temp_list_output)
    
    #0..10000 (foreach)
    temp_list  = list(range(10000))
    temp_list_output = []
    
    Parallel.ForEach(range(10000),
                     lambda x: temp_list_output.append(x))
    AreEqual(len(temp_list), len(temp_list_output))
    temp_list_output.sort()
    AreEqual(temp_list, temp_list_output)
    
@disabled("http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=25860")    
def test_system_diagnostics_contracts():
    '''
    http://msdn.microsoft.com/en-us/library/dd264808(VS.100).aspx
    
    There's no reason this should cause problems for IronPython - make sure a 
    simple scenario works.  Use of class attributes might cause problems 
    though...
    '''
    class KNew(object):
        def m1(self, p0):
            Contract.Requires(True)
    k = KNew()
    k.m1(0)

def test_system_dynamic():
    '''
    http://msdn.microsoft.com/en-us/library/system.dynamic(VS.100).aspx
    
    Only a sanity check here.  Exhaustive testing of 'dynamic' will exist
    elsewhere.  Piggyback off of CSharp/VB's work.
    '''
    print("TODO")
    
def test_covariance():
    '''
    http://msdn.microsoft.com/en-us/library/dd799517(VS.100).aspx
    
    - generic interface
    - generic delegate
    
    Only reference types should work.
    '''
    print("TODO")
    
def test_contravariance():
    '''
    http://msdn.microsoft.com/en-us/library/dd799517(VS.100).aspx
    
    - generic interface
    - generic delegate
    
    Only reference types should work.
    '''
    print("TODO")

def test_system_numerics_biginteger():
    '''
    http://msdn.microsoft.com/en-us/library/system.numerics.biginteger(VS.100).aspx
    
    This should be tested minimally here, and hit comprehensively from number
    tests. Basically any "long" test should pass against a BigInteger.
    '''
    print("TODO")

def test_system_numerics_complex():
    '''
    http://msdn.microsoft.com/en-us/library/system.numerics.complex(VS.100).aspx
    
    This should be tested minimally here, and hit comprehensively from number
    tests. Basically any "complex" test should pass against a Complex.
    '''
    print("TODO")

def test_system_tuple():
    '''
    http://msdn.microsoft.com/en-us/library/system.tuple(VS.100).aspx
    
    - Python tuple interchangeable? If so, quite a bit to do here
    '''
    print("TODO")

def test_file_system_enumerations():
    '''
    http://msdn.microsoft.com/en-us/library/dd997370(VS.100).aspx
    
    Only minimal sanity tests should be needed. We already have 
    "for x in IEnumerable:..."-like tests elsewhere.
    '''
    import os
    os_dir   = os.listdir(".")
    os_dir.sort()
    
    enum_dir = [x[2:] for x in System.IO.Directory.EnumerateFileSystemEntries(".")]
    enum_dir.sort()
    AreEqual(os_dir, enum_dir)

def test_memory_mapped_files():
    '''
    http://msdn.microsoft.com/en-us/library/dd997372(VS.100).aspx
    
    Do these play nice with Python's existing memory-mapped file
    support? Should they?
    '''
    print("TODO")

def test_named_args():
    '''
    http://msdn.microsoft.com/en-us/library/dd264739(VS.100).aspx
    
    Existing use of (VB) named args tests look woefully insufficient. Lots of
    work needed.
    '''
    print("TODO")

def test_optional_args():
    '''
    http://msdn.microsoft.com/en-us/library/dd264739(VS.100).aspx
    
    Existing use of (VB) optional args tests look woefully insufficient. Lots
    of work needed.
    '''
    print("TODO")
    
def test_misc():
    #http://msdn.microsoft.com/en-us/library/dd409610(VS.100).aspx
    #Can IronPython make use of COM object references from CSharp that have the
    #PIA embedded? This should fall under normal .NET interop.  I.e., we'll 
    #automatically pickup whatever's in the CSharp assembly.

    #http://msdn.microsoft.com/en-us/library/system.string.isnullorwhitespace(VS.100).aspx
    #Does System.String.IsNullOrWhiteSpace work with Python unicode/str types?
    
    #http://msdn.microsoft.com/en-us/library/dd991828(VS.100).aspx
    #Does System.String.Concat<T>() work with Python iterables?

    #http://msdn.microsoft.com/en-us/library/system.enum.hasflag(VS.100).aspx
    #Does enum.HasFlag work from IronPython?  I can't imagine this would fail;
    #maybe if the '|' operator was broken...
    
    #http://msdn.microsoft.com/en-us/library/system.io.stream.copyto(VS.100).aspx
    #Does System.IO.Stream.CopyTo(...) work with Python stream types which 
    #derive from System.IO.Stream?
    
    #http://msdn.microsoft.com/en-us/library/dd990377(VS.100).aspx
    #IObservable<T> can be used for Covariance tests. Otherwise, these 
    #interfaces aren't too interesting.

    #http://msdn.microsoft.com/en-us/library/dd642331(VS.100).aspx
    #Does lazy initialization work correctly from Python? If we're using reflection
    #to access properties...

    #http://msdn.microsoft.com/en-us/library/dd412070(VS.100).aspx
    #Can a SortedSet be passed in as the param to a Python 'set'? Should it?
    
    #http://msdn.microsoft.com/en-us/library/system.threading.thread.yield(VS.100).aspx
    #Does Thread.Yield affect IronPython at all? A sanity check should suffice
    
    #http://msdn.microsoft.com/en-us/library/system.windows.controls.datagrid(VS.100).aspx
    #We had problems with System.Windows.Forms.DataGrideView.  Let's make sure
    #there's nothing similar with System.Windows.Controls.DataGrid

    #http://msdn.microsoft.com/en-us/library/bb613588(VS.100).aspx#binding
    #Binding to (Python) dynamic objects in XAML?  Falls into our 'dynamic'
    #testing
    
    #http://msdn.microsoft.com/en-us/library/system.componentmodel.composition(VS.100).aspx
    #Is MEF relevant to IP at all?
    
    print("TODO")

#--MAIN------------------------------------------------------------------------
run_test(__name__)