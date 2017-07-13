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
Named indexer
'''

import unittest

from iptest import IronPythonTestCase, is_mono, run_test, skipUnlessIronPython

@unittest.skipIf(is_mono, 'https://github.com/IronLanguages/main/issues/1438')
@skipUnlessIronPython()
class IndexerVbTest(IronPythonTestCase):
    def setUp(self):
        super(IndexerVbTest, self).setUp()
        self.add_clr_assemblies("indexerdefinitionsvb", "typesamples")
        import System
        self.array = System.Array[object]

    def test_basic(self):
        from Merlin.Testing.Indexer import ClassWithIndexer
        for t in [
                    ClassWithIndexer, 
                    #StructWithIndexer,
                ]:
            x = t()
            x.Init()
            
            for y,z in zip(x.PropertyName, range(10)):
                self.assertEqual(y,z)
                
            self.assertRaises(TypeError, lambda: x[2])
            
            x.PropertyName[2] = 9
            self.assertEqual(x.PropertyName[2], 9)
            
            i = x.PropertyName
            i.SetValue(self.array([3]), 10)
            self.assertEqual(i.GetValue(self.array([3])), 10)
            
            i = t.PropertyName                         # bug 363422
            #i.SetValue(x, self.array([4]), 11)
            #self.assertEqual(i.GetValue(x, self.array([4])), 11)
    
    def test_signature(self):
        from Merlin.Testing.Indexer import ClassWithSignature
        x = ClassWithSignature()
        
        for y,z in zip(x.PropertyName, range(2, 12)):
            self.assertEqual(y,z)
                
        i = x.PropertyName
        i[3] = 10
        self.assertEqual(10, i[3])
        self.assertEqual(10, i[1, 4])
    
    def test_only_optional(self):
        from Merlin.Testing.Indexer import ClassWithOnlyOptional
        x = ClassWithOnlyOptional()
        
        for y,z in zip(x.PropertyName, range(10)):
            self.assertEqual(y,z)
        
        i = x.PropertyName
        #i[()]  # bug 363440
    
    def test_only_paramarray(self):
        from Merlin.Testing.Indexer import ClassWithOnlyParamArray
        x = ClassWithOnlyParamArray()
        i = x.PropertyName
        
        for y,z in zip(x.PropertyName, range(10)):
            self.assertEqual(y,z)
        
        self.assertEqual(i[()], -99)
        i[()] = 10
        self.assertEqual(i[()], 10)
        
        i[1] = 4
        self.assertEqual(i[1], 4)
        self.assertEqual(i[1, 4, 5, 7], 4)
    
    def test_static_indexer(self):
        from Merlin.Testing.Indexer import ClassWithStaticIndexer
        t = ClassWithStaticIndexer
        x = t()
        
        i = ClassWithStaticIndexer.PropertyName
        i[1] = 10
        self.assertEqual(i[100], 111)
        
        i.SetValue(self.array([2]), 20)
        self.assertEqual(i.GetValue(self.array([200])), 222)

    def test_overloaded_indexer(self):
        from Merlin.Testing.Indexer import ClassWithOverloadedIndexers
        x = ClassWithOverloadedIndexers()
        x.Init()
        
        self.assertEqual(x.PropertyName[6], 6)
        #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=3740
        self.assertEqual([y for y in x.PropertyName],
                range(10)) #should be [2]?


run_test(__name__)
