# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

# COM Interop tests for IronPython
from iptest.assert_util import skiptest
skiptest("win32")
from iptest.cominterop_util import *
from System import Type, Activator, Guid
import clr

dlrcomlib_guid = Guid("a50d2773-4b1b-428a-b5b4-9300e1b50484")    

def test_load_typelib():    
    for x in [dlrcomlib_guid, Activator.CreateInstance(Type.GetTypeFromProgID("DlrComLibrary.ParamsInRetval"))]:
        lib = clr.LoadTypeLibrary(x)
        
        #ComTypeLibInfo Members
        AreEqual(lib.Guid, dlrcomlib_guid)
        AreEqual(lib.Name, "DlrComLibraryLib")
        AreEqual(lib.VersionMajor, 1)
        AreEqual(lib.VersionMinor, 0)    
        Assert("DlrComLibraryLib" in dir(lib))
        
        #ComTypeLibDesc Members
        dlrComLib = lib.DlrComLibraryLib
        Assert("DlrComServer" in dir(lib.DlrComLibraryLib))   
        Assert("IDlrComServer" not in dir(lib.DlrComLibraryLib))
        
        #ComTypeClassDesc Members 
        dlrComServer = lib.DlrComLibraryLib.DlrComServer    
        AreEqual(dlrComServer.TypeLib, lib.DlrComLibraryLib)    
        AreEqual(dlrComServer.TypeName, "DlrComServer")
        AreEqual(str(dlrComServer.Kind), "Class")
        
        #Create an instance of the class and access members.
        obj = dlrComServer.CreateInstance()
        Assert("__ComObject" in str(obj.__class__))
        AreEqual(12345, obj.SumArgs(1,2,3,4,5))
        
        #Complete the circle back to the lib
        AreEqual(clr.LoadTypeLibrary(obj).Guid, lib.Guid)
        
def test_import_typelib():
    for x in [dlrcomlib_guid, Activator.CreateInstance(Type.GetTypeFromProgID("DlrComLibrary.ParamsInRetval"))]:
        clr.AddReferenceToTypeLibrary(x)
                
        try:
            DlrComLibrary.__class__
        except NameError: pass            
        else: Fail("Namespace already exists")
            
        import DlrComLibraryLib        
        from DlrComLibraryLib import DlrComServer
        
        Assert("DlrComServer" in dir(DlrComLibraryLib))
        
        #Create an instance of the class and access members.
        obj = DlrComServer.CreateInstance()
        Assert("__ComObject" in str(obj.__class__))
        AreEqual(12345, obj.SumArgs(1,2,3,4,5))
        
        del DlrComServer
        del DlrComLibraryLib
        
def test_negative():
    AssertError(ValueError, clr.LoadTypeLibrary, "DlrComLibrary.DlrComServer")
    AssertError(ValueError, clr.LoadTypeLibrary, 42)
    
    AssertError(EnvironmentError, clr.LoadTypeLibrary, Guid.NewGuid())

run_test(__name__)