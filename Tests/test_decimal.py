from iptest.assert_util import *

from decimal import *

def test_explicit_from_System_Decimal():
    import System

    #int
    AreEqual(str(Decimal(System.Decimal.Parse('45'))), '45')

    #float
    AreEqual(str(Decimal(System.Decimal.Parse('45.34'))), '45.34')        

run_test(__name__)
