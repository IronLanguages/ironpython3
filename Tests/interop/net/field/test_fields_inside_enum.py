# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import unittest

from iptest import IronPythonTestCase, run_test, skipUnlessIronPython

@skipUnlessIronPython()
class FieldsInsideEnumTest(IronPythonTestCase):
    def setUp(self):
        super(FieldsInsideEnumTest, self).setUp()
        self.add_clr_assemblies("fieldtests", "typesamples", "baseclasscs")

    def test_get_set(self):
        from Merlin.Testing.TypeSample import EnumInt32
        o = EnumInt32()
        self.assertEqual(o.A, EnumInt32.A)
    
        desc = EnumInt32.__dict__['B']
        self.assertEqual(EnumInt32.B, desc)
        
        def f(): o.A = 10
        self.assertRaisesRegex(AttributeError, "attribute 'A' of 'EnumInt32' object is read-only", f)
        
        def f(): EnumInt32.B = 10
        self.assertRaisesRegex(AttributeError, "attribute 'B' of 'EnumInt32' object is read-only", f)

        def f(): EnumInt32.B = EnumInt32.A
        self.assertRaisesRegex(AttributeError, "attribute 'B' of 'EnumInt32' object is read-only", f)

    def test_enum_bool(self):
        from Merlin.Testing.BaseClass import EmptyEnum
        from Merlin.Testing.TypeSample import EnumByte, EnumSByte, EnumUInt16, EnumInt16, EnumUInt32, EnumInt32, EnumUInt64, EnumInt64
        
        #An empty enumeration
        self.assertTrue(not bool(EmptyEnum())) 

        #__nonzero__
        o = EnumInt32()
        self.assertTrue(not o.A.__nonzero__())
        self.assertTrue(o.B.__nonzero__())

        for enum_type in [
                            EnumByte,
                            EnumSByte,
                            EnumUInt16, 
                            EnumInt16,
                            EnumUInt32, 
                            EnumInt32,
                            EnumUInt64, 
                            EnumInt64,
                            ]:
            self.assertTrue(not bool(enum_type().A))
            self.assertTrue(not bool(enum_type.A))
            self.assertTrue(bool(enum_type().B))
            self.assertTrue(bool(enum_type.B))
            self.assertTrue(bool(enum_type().C))
            self.assertTrue(bool(enum_type.C))
            self.assertTrue(enum_type)
            self.assertTrue(not bool(enum_type()))
    

run_test(__name__)