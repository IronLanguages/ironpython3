# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import unittest
from decimal import *

from iptest import run_test, skipUnlessIronPython

def SystemDecimalParse(s):
    import System
    import System.Globalization
    return System.Decimal.Parse(s, System.Globalization.CultureInfo.InvariantCulture)

@skipUnlessIronPython()
class DecimalTest(unittest.TestCase):

    def test_explicit_from_System_Decimal(self):
        #int
        self.assertEqual(str(Decimal(SystemDecimalParse('45'))), '45')

        #float
        self.assertEqual(str(Decimal(SystemDecimalParse('45.34'))), '45.34')

    def test_formatting(self):
        d = SystemDecimalParse('1.4274243253253245432543254545')
        self.assertEqual('{}'.format(d), '1.4274243253253245432543254545')
        self.assertEqual('{:,.2f}'.format(d), '1.43')
        self.assertEqual('{:e}'.format(d), '1.427424325325e+00')

        d = SystemDecimalParse('4000000000.40000000')
        self.assertEqual('{}'.format(d), '4000000000.40000000')
        self.assertEqual('{:e}'.format(d), '4.000000000400e+09')

run_test(__name__)
