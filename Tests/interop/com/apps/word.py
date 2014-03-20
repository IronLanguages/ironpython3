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
# Word Interop tests for IronPython

from iptest.assert_util import skiptest
skiptest("win32", "silverlight", "cli64")
from iptest.cominterop_util import *
from iptest.file_util import file_exists, delete_files
import nt

#------------------------------------------------------------------------------
#--SANITY CHECK
if not IsWordInstalled():
    from sys import exit
    print "Word is not installed.  Cannot run this test!"
    exit(1)
else:
    TryLoadWordInteropAssembly()
    from Microsoft.Office.Interop import Word

#------------------------------------------------------------------------------
#--HELPERS
def IsWordPIAInstalled():
    from Microsoft.Win32 import Registry
    word_pia_registry  = None
    wordapp_pia_registry = Registry.ClassesRoot.OpenSubKey("CLSID\\{000209FF-0000-0000-C000-000000000046}\\InprocServer32")
    #worddoc_pia_registry = Registry.ClassesRoot.OpenSubKey("CLSID\\{00020906-0000-0000-C000-000000000046}\\InprocServer32")
    return wordapp_pia_registry != None

def wd_selection_change_eventhandler(range):
    global SELECTION_COUNTER
    SELECTION_COUNTER = SELECTION_COUNTER + 1
    #print "selected range - ", range.Start, range.End

def add_wordapp_event(wdapp):
    wdapp.WindowSelectionChange += wd_selection_change_eventhandler

def remove_wordapp_event(wdapp):
    wdapp.WindowSelectionChange -= wd_selection_change_eventhandler
    
def get_range(doc, start, end):
    return doc.Range(start, end)
    
def quit_word(wd):
    if IS_PIA_INSTALLED : 
        wd.Quit(clr.Reference[System.Object](0))
    else: 
        wd.Quit(0)

#------------------------------------------------------------------------------
#--GLOBALS
IS_PIA_INSTALLED = IsWordPIAInstalled()
SELECTION_COUNTER = 0
word = CreateWordApplication()
doc = word.Documents.Add()

#------------------------------------------------------------------------------
#--TEST CASES
def test_word_typelibsupport():
    # load Word namespace directly from the TypeLib
    typeLib = clr.LoadTypeLibrary(System.Guid("00020905-0000-0000-C000-000000000046"))

    # we can get some information about he typelib
    Assert( typeLib.Name == 'Word')
    Assert( System.String.ToUpper(typeLib.Guid.ToString()) == '00020905-0000-0000-C000-000000000046')
    # check version information is available and does not throw
    typeLib.VersionMajor
    typeLib.VersionMinor
    # check typeLib exposes only those discoverable methods
    Assert( dir(typeLib).__len__() == 5 )
    Assert( 'Word' in dir(typeLib) );
    Assert( 'Name' in dir(typeLib) );
    Assert( 'Guid' in dir(typeLib) );
    Assert( 'VersionMajor' in dir(typeLib) );
    Assert( 'VersionMinor' in dir(typeLib) );

    # check some coclasses are present in Word's namespace
    Assert('Application' in dir(typeLib.Word))
    Assert('Document' in dir(typeLib.Word))

    # check some enums are present in Word's namespace
    Assert('WdCountry' in dir(typeLib.Word))
    Assert('WdSaveFormat' in dir(typeLib.Word))
    Assert('WdXMLNodeType' in dir(typeLib.Word))

    # check we can explore the content of enums
    Assert('wdFormatXML' in dir(typeLib.Word.WdSaveFormat))
    Assert('wdUS' in dir(typeLib.Word.WdCountry))

    #check we can access enums' values
    Assert(typeLib.Word.WdCountry.wdUS == 1)


    # verify namespace Word is not yet available
    try:
        Word.__class__
    except NameError: pass
    else: Fail("namespace Word has not been imported yet")

    # Now let's do above tests but with imported namespace
    clr.AddReferenceToTypeLibrary(typeLib.Guid)

    # verify namespace Word is not yet available
    try:
        Word.__class__
    except NameError: pass
    else: Fail("namespace Word has not been imported yet")

    import Word

    # check __class__ extension is available
    Word.__class__ 

    # check some coclasses are present in Word's namespace
    Assert('Application' in dir(Word))
    Assert('Document' in dir(Word))

    # check some expected enums are present in Word's namespace
    Assert('WdCountry' in dir(Word))
    Assert('WdSaveFormat' in dir(Word))
    Assert('WdXMLNodeType' in dir(Word))

    # check we can explore the content of enums
    Assert('wdFormatXML' in dir(Word.WdSaveFormat))
    Assert('wdUS' in dir(Word.WdCountry))

    #check we can access enums' values
    Assert(Word.WdCountry.wdUS == 1)


def test_wordevents():
    global SELECTION_COUNTER
    SELECTION_COUNTER = 0
    
    if IS_PIA_INSTALLED:
        print "Found PIAs for Word"
    else:
        print "No PIAs for Word were Found!!!!" 

    doc.Range().Text = "test"
    
    add_wordapp_event(word)
    get_range(doc, 1, 1).Select()
    AreEqual(SELECTION_COUNTER, 1)

    add_wordapp_event(word)
    get_range(doc, 1, 2).Select()
    AreEqual(SELECTION_COUNTER, 3)

    remove_wordapp_event(word)
    get_range(doc, 2, 2).Select()
    AreEqual(SELECTION_COUNTER, 4)

    remove_wordapp_event(word)
    get_range(doc, 2, 3).Select()
    AreEqual(SELECTION_COUNTER, 4)


def test_spellChecker():
    suggestions = word.GetSpellingSuggestions("waht")
    Assert(suggestions.Count > 5)
    # This tests for enumeration support over COM objects
    suggestions = [s.Name for s in suggestions.GetEnumerator()] 
    # Check to see that some expected suggestions actually exist
    Assert("what" in suggestions.GetEnumerator())
    Assert("with" in suggestions.GetEnumerator())


def test_word_basic():
    '''
    http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=14166
    '''
    temp_file_name = nt.tempnam() + ".word_basic.doc"
    
    word_basic = getRCWFromProgID("Word.Basic")
    if is_snap:
        word_basic.AppShow()
    word_basic.FileNewDefault()
    word_basic.Insert("some stuff...")
    word_basic.FileSaveAs(temp_file_name)
    if is_snap:
        word_basic.AppHide()
    del word_basic
    
    Assert(file_exists(temp_file_name))
    delete_files(temp_file_name)
    

#------------------------------------------------------------------------------
try:
    run_com_test(__name__, __file__)
finally:
    quit_word(word)

