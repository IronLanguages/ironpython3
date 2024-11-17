# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.
'''
NOTES:
- seems not a good test?
'''

import unittest

from iptest import IronPythonTestCase, run_test, skipUnlessIronPython

@skipUnlessIronPython()
class ReturnValueTest(IronPythonTestCase):
    def setUp(self):
        super(ReturnValueTest, self).setUp()
        self.add_clr_assemblies("returnvalues", "typesamples")

        from Merlin.Testing.Call import C
        self.c = C()

    def test_return_null(self):
        for f in [ 
            self.c.ReturnVoid, 
            self.c.ReturnNull, 
            self.c.ReturnNullableInt2, 
            self.c.ReturnNullableStruct2,
        ]:
            self.assertEqual(f(), None)
    
    def test_return_numbers(self):
        import clr
        import System
        from Merlin.Testing.Call import Int32RInt32EventHandler
        from Merlin.Testing.TypeSample import EnumInt16, SimpleStruct
        for (f, t, val) in [
            (self.c.ReturnByte, System.Byte, 0), 
            (self.c.ReturnSByte, System.SByte, 1), 
            (self.c.ReturnUInt16, System.UInt16, 2), 
            (self.c.ReturnInt16, System.Int16, 3), 
            (self.c.ReturnUInt32, System.UInt32, 4), 
            (self.c.ReturnInt32, System.Int32, 5), 
            (self.c.ReturnUInt64, System.UInt64, 6), 
            (self.c.ReturnInt64, System.Int64, 7), 
            (self.c.ReturnDouble, System.Double, 8), 
            (self.c.ReturnSingle, System.Single, 9), 
            (self.c.ReturnDecimal, System.Decimal, 10), 
            
            (self.c.ReturnChar, System.Char, 'A'), 
            (self.c.ReturnBoolean, System.Boolean, True), 
            (self.c.ReturnString, System.String, "CLR"), 
            
            (self.c.ReturnEnum, EnumInt16, EnumInt16.C),
        ]:
            x = f()
            self.assertEqual(x.GetType(), clr.GetClrType(t))
            self.assertEqual(x, val)
            
        # return value type
        x= self.c.ReturnStruct()
        self.assertEqual(x.Flag, 100)
        
        x= self.c.ReturnClass()
        self.assertEqual(x.Flag, 200)
        
        x= self.c.ReturnNullableInt1()
        self.assertEqual(x.GetType(), clr.GetClrType(System.Int32))
        self.assertEqual(x, 300)
        
        x= self.c.ReturnNullableStruct1()
        self.assertEqual(x.GetType(), clr.GetClrType(SimpleStruct))
        self.assertEqual(x.Flag, 400)
        
        x= self.c.ReturnInterface()
        self.assertEqual(x.Flag, 500)
        
        # return delegate
        x= self.c.ReturnDelegate()
        self.assertEqual(x.GetType(), clr.GetClrType(Int32RInt32EventHandler))
        self.assertEqual(x(3), 6)
        
        # array
        x= self.c.ReturnInt32Array()
        self.assertEqual(x[0], 1)
        self.assertEqual(x[1], 2)
        
        x= self.c.ReturnStructArray()
        self.assertEqual(x[0].Flag, 1)
        self.assertEqual(x[1].Flag, 2)

    def test_return_from_generic(self):
        from Merlin.Testing.Call import G
        for (t, v) in [
            (int, 2), 
            (str, "python"),
        ]:
            g = G[t](v)
            
            self.assertEqual(g.ReturnT(), v)
            self.assertEqual(g.ReturnStructT().Flag, v)
            self.assertEqual(g.ReturnClassT().Flag, v)

            x = g.ReturnArrayT()
            self.assertEqual(len(x), 3)
            self.assertEqual(x[2], v)
    
run_test(__name__)

