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

from iptest import IronPythonTestCase, run_test

# from Merlin.Testing import *
# from Merlin.Testing.Indexer import *
# from Merlin.Testing.TypeSample import *


class IndexerCSTest(IronPythonTestCase):
    def setUp(self):
        super(IndexerCSTest, self).setUp()
        self.add_clr_assemblies("indexerdefinitionscs", "typesamples")

    def test_this_in_interface(self):
        from Merlin.Testing.Indexer import ClassExplicitImplementInterface, ClassImplicitImplementInterface, StructExplicitImplementInterface, StructImplicitImplementInterface
        for t in [
                    StructExplicitImplementInterface,
                    ClassExplicitImplementInterface,
                    StructImplicitImplementInterface,
                    ClassImplicitImplementInterface,
                ]:
            x = t()
            
            #IReturnDouble.set_Item(x, 1, 10)  # bug 363289
            #self.assertEqual(IReturnDouble.get_Item(x, 1), 10)
    
    def test_params(self):
        from Merlin.Testing.Indexer import ClassWithParamsIndexer
        x = ClassWithParamsIndexer()
        x.Init()

        for y, z in [(y,z) for y,z in zip(x, range(100)) if z != 0]:
            self.assertEqual(y,z)
        
        x[1] = 2
        self.assertEqual(2, x[1])
            
        x[3, 4] = 5
        self.assertEqual(5, x[0, 7])
        
        self.assertEqual(x[()], -100)
        x[()] = 9
        self.assertEqual(x[()], 9)
    
    def test_overload1(self):
        from Merlin.Testing.Indexer import ClassWithIndexerOverloads1
        x = ClassWithIndexerOverloads1()
        x.Init()
        
        for y, z in [(y,z) for y,z in zip(x, range(100)) if z != 0]:
            self.assertEqual(y,z)
            
        self.assertEqual(x[()], -200)
        x[1, -1, 1, -1] = 3
        self.assertEqual(x[()], 3)
        
        x[6] = 0
        self.assertEqual(x[6], 0)
        
        x[2, 3] = 4
        self.assertEqual(x[1, 6], 4)
        self.assertEqual(x[7], 7)
        
        x[4, 5, 6] = 7
        self.assertEqual(7, x[4, 5, 6])
        
        self.assertTrue(not hasattr(x, 'get_Item'))
        self.assertTrue(not hasattr(x, 'set_Item'))
    
    def test_overload2(self):
        from Merlin.Testing.Indexer import ClassWithIndexerOverloads2
        x = ClassWithIndexerOverloads2()

        for y, z in zip(x, range(10)):
            self.assertEqual(y,z)
        
        x[1] = 2
        x['1'] = '3'
        self.assertEqual('3', x['1'])
        self.assertEqual(2, x[1])
    
    def test_basic(self):
        from Merlin.Testing.Indexer import ClassWithIndexer, StructWithIndexer
        from Merlin.Testing.TypeSample import SimpleClass, SimpleStruct
        for t in [  
                    ClassWithIndexer,
                    StructWithIndexer,   
                ]:
            x = t()
            x.Init()
            
            for y, z in zip(x, range(10)):
                self.assertEqual(y,z)
            
            a, b, c = 2, SimpleStruct(3), SimpleClass(4)
            
            x[1] = a
            self.assertEqual(x[1], a)
            
            x[2, "ab1"] = b
            self.assertEqual(x[12, "ab"].Flag, b.Flag)
            
            x['ab', 'c', 'd'] = c
            self.assertEqual(x['a', 'b', 'cd'], c)
            
            a, b, c = 5, SimpleStruct(6), SimpleClass(7)
            self.assertTrue(not hasattr(x, 'set_Item'))
            self.assertTrue(not hasattr(x, 'get_Item'))
                    
            # bad arg count
            self.assertRaisesRegexp(TypeError, "expected int, got tuple", lambda: x[()])
            self.assertRaisesRegexp(TypeError, "__getitem__\(\) takes at most 3 arguments \(4 given\)", lambda: x[1, 2, 3, 4])
            
            # bad arg type
            self.assertRaisesRegexp(TypeError, "expected str, got int", lambda: x[1, 2, 3])
            
            # bad value type
            def f(): x[1] = 'abc'
            self.assertRaisesRegexp(TypeError, "expected int, got str", f)
            

    def test_readonly(self):
        from Merlin.Testing.Indexer import ReadOnlyIndexer
        x = ReadOnlyIndexer()
        self.assertEqual(x[1], 10)
        
        def f(): x[2] = 20
        self.assertRaisesRegexp(TypeError, 
            "'ReadOnlyIndexer' object does not support item assignment",
            f)

    def test_writeonly(self):
        from Merlin.Testing.Indexer import WriteOnlyIndexer
        from Merlin.Testing import Flag
        x = WriteOnlyIndexer()
        Flag.Set(0)
        x[1] = 10
        Flag.Check(11)
        
        self.assertRaisesRegexp(TypeError, 
            "'WriteOnlyIndexer' object is not subscriptable",
            lambda: x[1])


    def test_access_from_derived_class(self):
        from Merlin.Testing.Indexer import DerivedClassWithoutIndexer, DerivedClassExplicitImplementInterface
        x = DerivedClassWithoutIndexer()
        x.Init()
        
        for y, z in zip(x, range(10)):
            self.assertEqual(y,z)
        
        x[2] = 4
        self.assertEqual(4, x[2])
        
        x = DerivedClassExplicitImplementInterface()
        x.Init()
        #IReturnDouble.set_Item(x, 3, 6)                      # bug 363289
        #self.assertEqual(IReturnDouble.get_Item(x, 3), 6)


    def test_new_indexer(self):
        from Merlin.Testing.Indexer import DerivedClassWithNewIndexer, DerivedClassWithNewWriteOnlyIndexer
        x = DerivedClassWithNewIndexer()
        
        for y, z in zip(x, range(0, -10, -1)):
            self.assertEqual(y,z)
            
        x[2] = 9
        self.assertEqual(-18, x[2])
        
        x = DerivedClassWithNewWriteOnlyIndexer()
        x[3] = 10
        #AssertError(TypeError, lambda: x[3])  # bug 362877
    
run_test(__name__)
