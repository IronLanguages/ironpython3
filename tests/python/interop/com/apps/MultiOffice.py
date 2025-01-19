# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

# Office Multi-application COM Interop tests

from iptest.assert_util import *
skiptest("win32", "cli64", "posix")
from iptest.cominterop_util import *

if not IsExcelInstalled():
    from sys import exit
    print("Excel is not installed.  Cannot run this test!")
    exit(1)
else:
    TryLoadExcelInteropAssembly()
    from Microsoft.Office.Interop import Excel

if not IsWordInstalled():
    from sys import exit
    print("Word is not installed.  Cannot run this test!")
    exit(1)
else:
    TryLoadWordInteropAssembly()
    from Microsoft.Office.Interop import Word

#------------------------------------------------------------------------------
def test_multioffice():  # Bug 393974
    import System

    try:
        ex = CreateExcelApplication()
        wd = CreateWordApplication()
    
        try:
            ws = ex.Workbooks.Add()
        except TypeError: Fail("Failure to create Excel workbook")
        
        try:
            doc = wd.Documents.Add()
        except TypeError: Fail("Failure to create Word document")
    
    finally:
        ex.Quit()
        wd.Quit()

#------------------------------------------------------------------------------
run_com_test(__name__, __file__)