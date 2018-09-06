# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

from iptest import IronPythonTestCase, run_test, skipUnlessIronPython

class Enum34Test(IronPythonTestCase):
    """check that CLR Enum have simular behavior to Python 3.4 enum"""
    def setUp(self):
        super(Enum34Test, self).setUp()
        self.load_iron_python_test()
    
    @skipUnlessIronPython()
    def test_constructor(self):
        """check that enum could be constructed from numeric values"""
        from IronPythonTest import DaysInt, DaysShort, DaysLong, DaysSByte, DaysByte, DaysUShort, DaysUInt, DaysULong
        
        enum_types = [DaysInt, DaysShort, DaysLong, DaysSByte, DaysByte, DaysUShort, DaysUInt, DaysULong]
        # days go from 0x01 to 0x40
        # we add 0x80 as a check for undefined enum member
        enum_values = list(range(0, 0x80))
        invalid_values = [None, 'invalid', self]

        for EnumType in enum_types:
            for value in enum_values:
                day = EnumType(value)
                self.assertEqual(int(day), value)
                
            self.assertTrue(EnumType.Mon < EnumType.Tue)
            
            self.assertEqual(EnumType(), EnumType.None)
            self.assertEqual(EnumType(0), EnumType.None)
            self.assertEqual(EnumType(1), EnumType.Mon)
            self.assertEqual(EnumType(6), EnumType.Tue | EnumType.Wed)
            self.assertEqual(EnumType(96), EnumType.Weekend)
            
            for value in invalid_values:
                self.assertRaises(ValueError, EnumType, value)

    @skipUnlessIronPython()
    def test_from_str(self):
        """check that enum could be constructed from str names"""
        from IronPythonTest import DaysInt, DaysShort, DaysLong, DaysSByte, DaysByte, DaysUShort, DaysUInt, DaysULong
        
        enum_types = [DaysInt, DaysShort, DaysLong, DaysSByte, DaysByte, DaysUShort, DaysUInt, DaysULong]

        for EnumType in enum_types:

            self.assertEqual(EnumType['None'], EnumType.None)
            self.assertEqual(EnumType['Mon'], EnumType.Mon)
            self.assertEqual(EnumType['Weekend'], EnumType.Weekend)
            
            self.assertRaises(SystemError, lambda: EnumType[DaysInt])
            self.assertRaises(TypeError, lambda: EnumType[None])
            self.assertRaises(TypeError, lambda: EnumType[self])
            self.assertRaises(KeyError, lambda: EnumType['invalid'])

run_test(__name__)
