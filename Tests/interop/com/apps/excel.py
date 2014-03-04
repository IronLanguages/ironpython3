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

# Excel COM Interop tests

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

#------------------------------------------------------------------------------
#--HELPERS
selection_counter = 0

def selection_change_eventhandler(range):
    global selection_counter
    selection_counter = selection_counter + 1
    #print "selected range - " + range.Address[0]
        
def add_worksheet_event(ws):
    ws.SelectionChange += selection_change_eventhandler

def remove_worksheet_event(ws):
    ws.SelectionChange -= selection_change_eventhandler

#-----------------------------------------------------------------------------
#--TESTS
def test_excel():
    ex = None
    
    try: 
        ex = CreateExcelApplication() 
        Assert(not callable(ex))
        ex.DisplayAlerts = False
        AreEqual(ex.DisplayAlerts, False)
        
        for x in dir(ex):
            AreEqual(dir(ex).count(x), 1)
        
        #ex.Visible = True
        nb = ex.Workbooks.Add()
        ws = nb.Worksheets[1]

        AreEqual('Sheet1', ws.Name)

        #Dev10 409961
        # COM has 1-based arrays
        #AssertError(EnvironmentError, lambda: ws.Rows[0])

        for i in range(1, 10):
            for j in range(1, 10):
                ws.Cells[i, j] = i * j

        rng = ws.Range['A1', 'B3']
        AreEqual(6, rng.Count)

        co = ws.ChartObjects()
        graph = co.Add(100, 100, 200, 200)
        graph.Chart.ChartWizard(rng, Excel.XlChartType.xl3DColumn)                        
    
    finally:            
        # clean up outstanding RCWs 
        ws = None
        nb = None
        rng = None
        System.GC.Collect()
        System.GC.WaitForPendingFinalizers()

        if ex: ex.Quit()
        else: print "ex is %s" % ex

def test_excel_typelibsupport():
    ex = None

    try: 
        ex = CreateExcelApplication() 

        typelib = clr.LoadTypeLibrary(ex)
        AreEqual(typelib.Name, 'Excel')
        typelib.Excel.__class__
        Assert('Application' in dir(typelib.Excel))
        Assert('XlSaveAction' in dir(typelib.Excel))
        Assert('xlSaveChanges' in dir(typelib.Excel.XlSaveAction))
        AreEqual(typelib.Excel.XlSaveAction.xlSaveChanges, 1)

        # verify namespace Excel is not yet available
        try:
            Excel.__class__
        except NameError: pass
        else: Fail("namespace Excel has not been imported yet")

        typelib = clr.AddReferenceToTypeLibrary(ex)
        try:
            Excel.__class__
        except NameError: pass
        else: Fail("namespace Excel has not been imported yet")
        
        import Excel
        Assert('Application' in dir(Excel))
        Assert('XlSaveAction' in dir(Excel))
        Assert('xlSaveChanges' in dir(Excel.XlSaveAction))
        AreEqual(Excel.XlSaveAction.xlSaveChanges, 1)

    finally:            
        System.GC.Collect()
        System.GC.WaitForPendingFinalizers()

        if ex: ex.Quit()
        else: print "ex is %s" % ex

def excel_events_helper(ex):
    ex.Workbooks.Add()
    ws = ex.ActiveSheet
    
    # test single event is firing
    add_worksheet_event(ws)
    ex.ActiveCell.Offset[1, 0].Activate()
    AreEqual(selection_counter, 1)

    # test events chaining is working
    add_worksheet_event(ws)
    ex.ActiveCell.Offset[1, 0].Activate()
    AreEqual(selection_counter, 3)

    # test removing event from a chain
    remove_worksheet_event(ws)
    ex.ActiveCell.Offset[1, 0].Activate()
    AreEqual(selection_counter, 4)

    # test removing event alltogether
    remove_worksheet_event(ws)
    ex.ActiveCell.Offset[1, 0].Activate()
    AreEqual(selection_counter, 4)

    add_worksheet_event(ws)
    ex.ActiveCell.Offset[1, 0].Activate()
    AreEqual(selection_counter, 5)

def test_excelevents():
    import gc
    ex = None
    try: 
        ex = CreateExcelApplication() 
        ex.DisplayAlerts = False 
        #Regression for CodePlex 18614
        temp_list = dir(ex)
        for x in temp_list:
            if temp_list.count(x)!=1:
                Fail("There should be exactly one '%s' in dir(excel)" % str(x))
        
        #ex.Visible = True
                
        global selection_counter
        selection_counter = 0

        # we need all temps/locals allocated for worksheets to be in a separate function
        # in order to be collected by GC
        excel_events_helper(ex)

        gc.collect()
        System.GC.WaitForPendingFinalizers()

        ex.ActiveCell.Offset[1, 0].Activate()
        AreEqual(selection_counter, 5)

    finally:
        # clean up outstanding RCWs 
        gc.collect()
        System.GC.WaitForPendingFinalizers()
                
        if ex: ex.Quit()
        else: print "ex is %s" % ex

def test_cp148579():
    AssertErrorWithMessage(TypeError, 
                           "Cannot create instances of Range because it is abstract", 
                           Excel.Range, 1,2)

def test_cp14539():
    try: 
        ex = CreateExcelApplication() 
        for i in xrange(3):
            AreEqual(ex.Visible, False)
            ex.Visible = True
            if not is_stress:
                AreEqual(ex.Visible, True)
            ex.Visible = False
        
    finally:
        ex.Quit()
    
def test_cp24654():
    app = CreateExcelApplication() 
    try:
        app.workbooks.Add()
        workbook = app.ActiveWorkbook
        worksheet = workbook.ActiveSheet
        chart = worksheet.ChartObjects
        type(chart)
        AreEqual(type(chart.__doc__), str)
    finally:
        app.Quit()
    
#------------------------------------------------------------------------------
run_com_test(__name__, __file__)
