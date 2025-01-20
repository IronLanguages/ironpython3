# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.
'''
Covers VB default indexer.
'''

import unittest

from iptest import IronPythonTestCase, run_test, skipUnlessIronPython

@skipUnlessIronPython()
class DefaultMemberTest(IronPythonTestCase):
    def setUp(self):
        super(DefaultMemberTest, self).setUp()
        self.add_clr_assemblies("defaultmembersvb", "defaultmemberscs", "typesamples")

    def test_vb_scenarios(self):
        '''vb supported scenarios'''
        from Merlin.Testing.DefaultMemberSample import ClassImplementsIDefaultIndexer, ClassWithNotExistingMember, ClassWithOverloadDefaultIndexer, DerivedClass, \
            StructImplementsIDefaultIndexer, StructWithDefaultIndexer
        x = ClassWithOverloadDefaultIndexer()
    
        for i in range(3):
            #x[i] = 2 * i
            #self.assertEqual(x[i], 2 * i)
        
            x.MyProperty[i] = 3 * i
            self.assertEqual(x.MyProperty[i], 3 * i)
        
        for i in range(2, 4):
            for j in range(6, 9):
                a = i + j

                #x[i, j] = a
                #self.assertEqual(a, x[i, j])
                
                #x.MyProperty[i, j] = a * 2
                #self.assertEqual(x.MyProperty[i, j], a * 2)
        
        # negative scenarios: incorrect argument count, type
        #x[()]
        #x[1, 2, 3]
        #x[x]

        # value type
        x = StructWithDefaultIndexer()
        x.Init()
        
        #x[1] = 1
        #print x.MyProperty[0]
        #x.MyProperty[1] = 1
        
        x = ClassWithNotExistingMember()
        # x[1] 
        # x[2] = 2
        self.assertEqual(x.MyProperty[1] , 0)
        x.MyProperty[1] = 10
        self.assertEqual(x.MyProperty[1] , 10)
        
        # interface declared with default indexer
        for t in [StructImplementsIDefaultIndexer, ClassImplementsIDefaultIndexer]:
            x = t()
            #self.assertEqual(x[5], 5)
            #x[5] = 6
            #Flag.Check(11)
            
            #IDefaultIndexer.MyProperty.GetValue(x, 5) 

        # try to leverage default member from the derived class: no
        x = DerivedClass()
        # x[2]
        # x[2] = 3
        x.MyProperty[2] = 4
        self.assertEqual(x.MyProperty[2], 4)
    
    def test_cs_scenarios(self):
        from Merlin.Testing import Flag
        from Merlin.Testing.DefaultMemberSample import ClassWithItem, ClassWithget_Item, ClassWithset_Item, ClassWithDefaultMemberCtor
        x = ClassWithItem()
        self.assertRaises(TypeError, lambda: x[1])
        x.Item = 2
        self.assertEqual(x.Item, 2)
        
        x = ClassWithset_Item()
        def f(): x[10] = 20
        self.assertRaises(TypeError, f)
        x.set_Item(3)
        Flag.Check(3)
        
        x = ClassWithget_Item()
        def f(): return x[10]
        self.assertRaises(TypeError, f)
        self.assertEqual(x[10, 20], 30)
        self.assertRaises(TypeError, x.get_Item, 10)
        self.assertEqual(x.get_Item(10, 20), 30)
        
        # try other types
        x = ClassWithDefaultMemberCtor(1)

    def test_cp_19510(self):
        """Test indexing on .NET classes with default members"""
        import clr
        clr.AddReference("System.Xml")
        import System.Xml
        
        doc = System.Xml.XmlDocument()
        doc.LoadXml('<tag attr="value">Data</tag>')
        root = doc.SelectSingleNode("tag")
        
        self.assertEqual(root.Attributes["attr"].Name, "attr")

run_test(__name__)

