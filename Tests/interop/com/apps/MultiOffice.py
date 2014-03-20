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

# Office Multi-application COM Interop tests

from iptest.assert_util import *
skiptest("win32", "silverlight", "cli64")
from iptest.cominterop_util import *

if not IsExcelInstalled():
    from sys import exit
    print "Excel is not installed.  Cannot run this test!"
    exit(1)
else:
    TryLoadExcelInteropAssembly()
    from Microsoft.Office.Interop import Excel

if not IsWordInstalled():
    from sys import exit
    print "Word is not installed.  Cannot run this test!"
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