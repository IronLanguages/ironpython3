# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

'''
This test module tests all new features available in .NET 4.0 which are relevant
for Python.
'''

import unittest

from iptest import IronPythonTestCase, is_netcoreapp, run_test, skipUnlessIronPython

@skipUnlessIronPython()
class FourDotZeroTest(IronPythonTestCase):
    def setUp(self):
        super(FourDotZeroTest, self).setUp()
        if is_netcoreapp:
            import clr
            clr.AddReference("System.Threading.Tasks.Parallel")
            clr.AddReference("System.IO.FileSystem")

    def test_system_threading_tasks(self):
        '''
        http://msdn.microsoft.com/en-us/library/system.threading.tasks(VS.100).aspx
        http://msdn.microsoft.com/en-us/library/dd460713(VS.100).aspx

        Shouldn't be necessary to test this (i.e., not directly supported by IP,
        but will work), but as it's a significant .NET 4.0 feature minimally a
        sanity test is in order here.
        '''
        import System
        import System.Threading.Tasks.Parallel as Parallel
        #0..10000
        temp_list  = list(range(10000))
        temp_list_output = []

        #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=25859
        Parallel.For.Overloads[int, int, System.Action[int]](0,
                                                            len(temp_list),
                                                            lambda x: temp_list_output.append(x))
        self.assertEqual(len(temp_list), len(temp_list_output))
        temp_list_output.sort()
        self.assertEqual(temp_list, temp_list_output)

        #0..10000 (foreach)
        temp_list  = list(range(10000))
        temp_list_output = []

        Parallel.ForEach(range(10000),
                        lambda x: temp_list_output.append(x))
        self.assertEqual(len(temp_list), len(temp_list_output))
        temp_list_output.sort()
        self.assertEqual(temp_list, temp_list_output)

    @unittest.skip('https://github.com/IronLanguages/main/issues/786')
    def test_system_diagnostics_contracts(self):
        '''
        http://msdn.microsoft.com/en-us/library/dd264808(VS.100).aspx

        There's no reason this should cause problems for IronPython - make sure a
        simple scenario works.  Use of class attributes might cause problems
        though...
        '''
        import System.Diagnostics.Contracts.Contract as Contract
        class KNew(object):
            def m1(self, p0):
                Contract.Requires(True)
        k = KNew()
        k.m1(0)

    def test_system_dynamic(self):
        '''
        http://msdn.microsoft.com/en-us/library/system.dynamic(VS.100).aspx

        Only a sanity check here.  Exhaustive testing of 'dynamic' will exist
        elsewhere.  Piggyback off of CSharp/VB's work.
        '''
        print("TODO")

    def test_covariance(self):
        '''
        http://msdn.microsoft.com/en-us/library/dd799517(VS.100).aspx

        - generic interface
        - generic delegate

        Only reference types should work.
        '''
        print("TODO")

    def test_contravariance(self):
        '''
        http://msdn.microsoft.com/en-us/library/dd799517(VS.100).aspx

        - generic interface
        - generic delegate

        Only reference types should work.
        '''
        print("TODO")

    def test_system_numerics_biginteger(self):
        '''
        http://msdn.microsoft.com/en-us/library/system.numerics.biginteger(VS.100).aspx

        This should be tested minimally here, and hit comprehensively from number
        tests. Basically any "long" test should pass against a BigInteger.
        '''
        print("TODO")

    def test_system_numerics_complex(self):
        '''
        http://msdn.microsoft.com/en-us/library/system.numerics.complex(VS.100).aspx

        This should be tested minimally here, and hit comprehensively from number
        tests. Basically any "complex" test should pass against a Complex.
        '''
        print("TODO")

    def test_system_tuple(self):
        '''
        http://msdn.microsoft.com/en-us/library/system.tuple(VS.100).aspx

        - Python tuple interchangeable? If so, quite a bit to do here
        '''
        print("TODO")

    def test_file_system_enumerations(self):
        '''
        http://msdn.microsoft.com/en-us/library/dd997370(VS.100).aspx

        Only minimal sanity tests should be needed. We already have
        "for x in IEnumerable:..."-like tests elsewhere.
        '''
        import System
        import os
        os_dir   = os.listdir(".")
        os_dir.sort()

        enum_dir = [x[2:] for x in System.IO.Directory.EnumerateFileSystemEntries(".")]
        enum_dir.sort()
        self.assertEqual(os_dir, enum_dir)

    def test_memory_mapped_files(self):
        '''
        http://msdn.microsoft.com/en-us/library/dd997372(VS.100).aspx

        Do these play nice with Python's existing memory-mapped file
        support? Should they?
        '''
        print("TODO")

    def test_named_args(self):
        '''
        http://msdn.microsoft.com/en-us/library/dd264739(VS.100).aspx

        Existing use of (VB) named args tests look woefully insufficient. Lots of
        work needed.
        '''
        print("TODO")

    def test_optional_args(self):
        '''
        http://msdn.microsoft.com/en-us/library/dd264739(VS.100).aspx

        Existing use of (VB) optional args tests look woefully insufficient. Lots
        of work needed.
        '''
        print("TODO")

    def test_misc(self):
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

run_test(__name__)