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

# COM interop utility module

import sys
import nt

from iptest.assert_util  import *
from iptest.file_util    import *
from iptest.process_util import *

if is_cli:
    import clr

    from System import Type
    from System import Activator
    from System import Exception as System_dot_Exception

    remove_ironpython_dlls(testpath.public_testdir)
    load_iron_python_dll()
    import IronPython

    load_iron_python_test()
    import IronPythonTest
    
    #--For asserts in IP/DLR assemblies----------------------------------------
    from System.Diagnostics import Debug, DefaultTraceListener
    
    class MyTraceListener(DefaultTraceListener):
        def Fail(self, msg, detailMsg=''):
            print "ASSERT FAILED:", msg
            if detailMsg!='':
                print "              ", detailMsg
            sys.exit(1)
            
    if is_snap:
        Debug.Listeners.Clear()
        Debug.Listeners.Add(MyTraceListener())
    
    
is_pywin32 = False
if sys.platform=="win32":
    try:
        import win32com.client
        is_pywin32 = True
        if sys.prefix not in nt.environ["Path"]:
            nt.environ["Path"] += ";" + sys.prefix
    except:
        pass
    
    

#------------------------------------------------------------------------------
#--GLOBALS
    
windir = get_environ_variable("windir")
agentsvr_path = path_combine(windir, r"msagent\agentsvr.exe")
scriptpw_path = path_combine(windir, r"system32\scriptpw.dll")

STRING_VALUES = [   "", "a", "ab", "abc", "aa",
                    "a" * 100000,
                    "1", "1.0", "1L", "object", "str", "object()",
                    " ", "_", "abc ", " abc", " abc ", "ab c", "ab  c",
                    "\ta", "a\t", "\n", "\t", "\na", "a\n"]
STRING_VALUES = [unicode(x) for x in STRING_VALUES] + STRING_VALUES

def aFunc(): pass

class KNew(object): pass

class KOld: pass

NON_NUMBER_VALUES = [   object, 
                        KNew, KOld, 
                        Exception,
                        object(), KNew(), KOld(),
                        aFunc, str, eval, type,
                        [], [3.14], ["abc"],
                        (), (3,), (u"xyz",),
                        xrange(5), 
                        {}, {'a':1},
                        __builtins__,
                     ]

FPN_VALUES = [   -1.23, -1.0, -0.123, -0.0, 0.123, 1.0, 1.23, 
                0.0000001, 3.14159265, 1E10, 1.0E10 ]
UINT_VALUES = [ 0, 1, 2, 7, 10, 32]
INT_VALUES = [ -x for x in UINT_VALUES ] + UINT_VALUES
LONG_VALUES = [long(x) for x in INT_VALUES]
COMPLEX_VALUES = [ 3j]

#--Subclasses of Python/.NET types
class Py_Str(str): pass  

if is_cli:
    class Py_System_String(System.String): pass

class Py_Float(float): pass  

class Py_Double(float): pass  

if is_cli:
    class Py_System_Double(System.Double): pass

class Py_UShort(int): pass

class Py_ULong(long): pass

class Py_ULongLong(long): pass

class Py_Short(int): pass

class Py_Long(int): pass

if is_cli:
    class Py_System_Int32(System.Int32): pass

class Py_LongLong(long): pass
    
#-------Helpers----------------
    
def shallow_copy(in_list):
    '''
    We do not necessarily have access to the copy module.
    '''
    return [x for x in in_list]

def pos_num_helper(clr_type):
    return [
            clr_type.MinValue,
            clr_type.MinValue + 1,
            clr_type.MinValue + 2,
            clr_type.MinValue + 10,
            clr_type.MaxValue/2,
            clr_type.MaxValue - 10,
            clr_type.MaxValue - 2,
            clr_type.MaxValue - 1,
            clr_type.MaxValue,
            ]
            
def overflow_num_helper(clr_type):
    return [
            clr_type.MinValue - 1,
            clr_type.MinValue - 2,
            clr_type.MinValue - 3,
            clr_type.MinValue - 10,
            clr_type.MaxValue + 10,
            clr_type.MaxValue + 3,
            clr_type.MaxValue + 2,
            clr_type.MaxValue + 1,
            ]   
    
def valueErrorTrigger(in_type):
    ret_val = {}
    
    ############################################################
    #Is there anything in Python not being able to evaluate to a bool?
    ret_val["VARIANT_BOOL"] =  [ ]
                     
    ############################################################              
    ret_val["BYTE"] = shallow_copy(NON_NUMBER_VALUES)
    ret_val["BYTE"] += COMPLEX_VALUES
      
    if sys.platform=="win32":
        ret_val["BYTE"] += FPN_VALUES  #Merlin 323751
        ret_val["BYTE"] = [x for x in ret_val["BYTE"] if type(x) not in [unicode, str]] #INCOMPAT BUG - should be ValueError
        ret_val["BYTE"] = [x for x in ret_val["BYTE"] if not isinstance(x, KOld)] #INCOMPAT BUG - should be AttributeError
      
        
    ############################################################
    ret_val["BSTR"] = shallow_copy(NON_NUMBER_VALUES)
    ret_val["BSTR"] += COMPLEX_VALUES
    
    if sys.platform=="win32":
        ret_val["BSTR"] = [] #INCOMPAT BUG
    
    #strip out string values
    ret_val["BSTR"] = [x for x in ret_val["BSTR"] if type(x) is not str and type(x) is not KNew and type(x) is not KOld and type(x) is not object]
  
    ############################################################  
    ret_val["CHAR"] =  shallow_copy(NON_NUMBER_VALUES)
    ret_val["CHAR"] += COMPLEX_VALUES
    if sys.platform=="win32":
        ret_val["CHAR"] += FPN_VALUES #Merlin 323751
    
    ############################################################
    ret_val["FLOAT"] = shallow_copy(NON_NUMBER_VALUES)
    ret_val["FLOAT"] += COMPLEX_VALUES
    
    if sys.platform=="win32":
            ret_val["FLOAT"] += UINT_VALUES + INT_VALUES #COMPAT BUG
    
    ############################################################
    ret_val["DOUBLE"] = shallow_copy(ret_val["FLOAT"])
    
    ############################################################            
    ret_val["USHORT"] =  shallow_copy(NON_NUMBER_VALUES)
    ret_val["USHORT"] += COMPLEX_VALUES
    
    if sys.platform=="win32":
            ret_val["USHORT"] += FPN_VALUES #Merlin 323751
    
    ############################################################  
    ret_val["ULONG"] = shallow_copy(ret_val["USHORT"])
    
    ############################################################           
    ret_val["ULONGLONG"] =  shallow_copy(ret_val["ULONG"])
    
    ############################################################  
    ret_val["SHORT"] =  shallow_copy(NON_NUMBER_VALUES)
    ret_val["SHORT"] += COMPLEX_VALUES
      
    if sys.platform=="win32":
            ret_val["SHORT"] += FPN_VALUES  #Merlin 323751
    
    ############################################################  
    ret_val["LONG"] =  shallow_copy(ret_val["SHORT"])
    
    ############################################################             
    ret_val["LONGLONG"] =  shallow_copy(ret_val["LONG"])
    
    ############################################################
    return ret_val[in_type]
    

def typeErrorTrigger(in_type):
    ret_val = {}
    
    ############################################################
    #Is there anything in Python not being able to evaluate to a bool?
    ret_val["VARIANT_BOOL"] =  [ ]
                     
    ############################################################              
    ret_val["BYTE"] = []
    
    ############################################################
    ret_val["BSTR"] = []
    #strip out string values
    ret_val["BSTR"] = [x for x in ret_val["BSTR"] if type(x) is not str]
  
    ############################################################  
    ret_val["CHAR"] =  []
    
    ############################################################
    ret_val["FLOAT"] = []
    
    ############################################################
    ret_val["DOUBLE"] = []
    
    ############################################################            
    ret_val["USHORT"] =  []
    
    
    ############################################################  
    ret_val["ULONG"] = []
    
    ############################################################           
    ret_val["ULONGLONG"] =  []
    
    ############################################################  
    ret_val["SHORT"] =  []
    
    ############################################################  
    ret_val["LONG"] =  []
    
    ############################################################             
    ret_val["LONGLONG"] =  []
    
    ############################################################
    return ret_val[in_type]
    

def overflowErrorTrigger(in_type):
    ret_val = {}
    
    ############################################################
    ret_val["VARIANT_BOOL"] =  []
                     
    ############################################################              
    ret_val["BYTE"] = []
    ret_val["BYTE"] += overflow_num_helper(System.Byte)
        
    ############################################################
    #Doesn't seem possible to create a value (w/o 1st overflowing
    #in Python) to pass to the COM method which will overflow.
    ret_val["BSTR"] = [] #["0123456789" * 1234567890]
    
    ############################################################ 
    ret_val["CHAR"] = []
    ret_val["CHAR"] +=  overflow_num_helper(System.SByte)
    
    ############################################################
    ret_val["FLOAT"] = []  
    ret_val["FLOAT"] += overflow_num_helper(System.Double)
    
    #Shouldn't be possible to overflow a double.
    ret_val["DOUBLE"] =  []
    
    
    ############################################################            
    ret_val["USHORT"] =  []
    ret_val["USHORT"] += overflow_num_helper(System.UInt16)
      
    ret_val["ULONG"] =  []
    ret_val["ULONG"] +=  overflow_num_helper(System.UInt32)
               
    ret_val["ULONGLONG"] =  []
    # Dev10 475426
    #ret_val["ULONGLONG"] +=  overflow_num_helper(System.UInt64)
      
    ret_val["SHORT"] =  []
    ret_val["SHORT"] += overflow_num_helper(System.Int16)
      
    ret_val["LONG"] =  []
    # Dev10 475426
    #ret_val["LONG"] += overflow_num_helper(System.Int32)
                
    ret_val["LONGLONG"] =  []
    # Dev10 475426
    #ret_val["LONGLONG"] += overflow_num_helper(System.Int64)
    
    ############################################################
    return ret_val[in_type]    
    

def pythonToCOM(in_type):
    '''
    Given a COM type (in string format), this helper function returns a list of
    lists where each sublists contains 1-N elements.  Each of these elements in
    turn are of different types (compatible with in_type), but equivalent to 
    one another.
    '''
    ret_val = {}
    
    ############################################################
    temp_funcs = [int, bool, System.Boolean]   # long, Dev10 475426
    temp_values = [ 0, 1, True, False]
    
    ret_val["VARIANT_BOOL"] =  [ [y(x) for y in temp_funcs] for x in temp_values]
                     
    ############################################################              
    temp_funcs = [System.Byte]
    temp_values = pos_num_helper(System.Byte)
    
    ret_val["BYTE"] =  [ [y(x) for y in temp_funcs] for x in temp_values]

    ############################################################
    temp_funcs = [  str, unicode, # Py_Str, Py_System_String,  
                    System.String ]
    temp_values = shallow_copy(STRING_VALUES)
    
    ret_val["BSTR"] = [ [y(x) for y in temp_funcs] for x in temp_values]
  
    ############################################################  
    temp_funcs = [System.SByte]
    temp_values = pos_num_helper(System.SByte)            
    
    ret_val["CHAR"] =  [ [y(x) for y in temp_funcs] for x in temp_values]

    ############################################################
    temp_funcs = [  float, # Py_Float, 
                    System.Single]
    ret_val["FLOAT"] = [ [y(x) for y in temp_funcs] for x in FPN_VALUES]
    
    ############################################################
    temp_funcs = [  float, System.Double]  # Py_Double, Py_System_Double, 
    temp_values = [-1.0e+308,  1.0e308] + FPN_VALUES

    ret_val["DOUBLE"] = [ [y(x) for y in temp_funcs] for x in temp_values]
    ret_val["DOUBLE"] += ret_val["FLOAT"]
    
    ############################################################  
    temp_funcs = [int, System.UInt16]  # Py_UShort, 
    temp_values = pos_num_helper(System.UInt16)
    
    ret_val["USHORT"] =  [ [y(x) for y in temp_funcs] for x in temp_values]
    
    ############################################################  
    temp_funcs = [int, System.UInt32]  # Py_ULong, 
    temp_values = pos_num_helper(System.UInt32) + pos_num_helper(System.UInt16)
        
    ret_val["ULONG"] =  [ [y(x) for y in temp_funcs] for x in temp_values]
    ret_val["ULONG"] += ret_val["USHORT"]
    
    ############################################################  
    temp_funcs = [int, long, System.UInt64]  # Py_ULongLong, 
    temp_values = pos_num_helper(System.UInt64) + pos_num_helper(System.UInt32) + pos_num_helper(System.UInt16)
                
    ret_val["ULONGLONG"] =  [ [y(x) for y in temp_funcs] for x in temp_values]
    ret_val["ULONGLONG"] += ret_val["ULONG"]
    
    ############################################################  
    temp_funcs = [int, System.Int16]  # Py_Short, 
    temp_values = pos_num_helper(System.Int16)
                
    ret_val["SHORT"] =  [ [y(x) for y in temp_funcs] for x in temp_values]
    
    ############################################################  
    temp_funcs = [int, System.Int32] # Py_Long, Dev10 475426
    temp_values = pos_num_helper(System.Int32) + pos_num_helper(System.Int16)
    
    ret_val["LONG"] =  [ [y(x) for y in temp_funcs] for x in temp_values]
    ret_val["LONG"] += ret_val["SHORT"]
    
    
    ############################################################  
    temp_funcs = [int, long, System.Int64] # Py_LongLong, Dev10 475426
    temp_values = pos_num_helper(System.Int64) + pos_num_helper(System.Int32) + pos_num_helper(System.Int16)
                
    ret_val["LONGLONG"] =  [ [y(x) for y in temp_funcs] for x in temp_values]
    ret_val["LONGLONG"] += ret_val["LONG"]
    
    ############################################################
    return ret_val[in_type]
    
#------------------------------------------------------------------------------
#--Override a couple of definitions from assert_util
from iptest import assert_util
DEBUG = 1

def assert_helper(in_dict):    
    #add the keys if they're not there
    if not in_dict.has_key("runonly"): in_dict["runonly"] = True
    if not in_dict.has_key("skip"): in_dict["skip"] = False
    
    #determine whether this test will be run or not
    run = in_dict["runonly"] and not in_dict["skip"]
    
    #strip out the keys
    for x in ["runonly", "skip"]: in_dict.pop(x)
    
    if not run:
        if in_dict.has_key("bugid"):
            print "...skipped an assert due to bug", str(in_dict["bugid"])
            
        elif DEBUG:
            print "...skipped an assert on", sys.platform
    
    if in_dict.has_key("bugid"): in_dict.pop("bugid")
    return run

def Assert(*args, **kwargs):
    if assert_helper(kwargs): assert_util.Assert(*args, **kwargs)
    
def AreEqual(*args, **kwargs):
    if assert_helper(kwargs): assert_util.AreEqual(*args, **kwargs)

def AssertError(*args, **kwargs):
    try:
        if assert_helper(kwargs): assert_util.AssertError(*args, **kwargs)
    except Exception, e:
        print "AssertError(" + str(args) + ", " + str(kwargs) + ") failed!"
        raise e

def AssertErrorWithMessage(*args, **kwargs):
    try:
        if assert_helper(kwargs): assert_util.AssertErrorWithMessage(*args, **kwargs)
    except Exception, e:
        print "AssertErrorWithMessage(" + str(args) + ", " + str(kwargs) + ") failed!"
        raise e

def AssertErrorWithPartialMessage(*args, **kwargs):
    try:
        if assert_helper(kwargs): assert_util.AssertErrorWithPartialMessage(*args, **kwargs)
    except Exception, e:
        print "AssertErrorWithPartialMessage(" + str(args) + ", " + str(kwargs) + ") failed!"
        raise e

def AlmostEqual(*args, **kwargs):
    if assert_helper(kwargs): assert_util.AlmostEqual(*args, **kwargs)


#------------------------------------------------------------------------------
#--HELPERS

def TryLoadExcelInteropAssembly():
    try:
        clr.AddReferenceByName('Microsoft.Office.Interop.Excel, Version=12.0.0.0, Culture=neutral, PublicKeyToken=71e9bce111e9429c')
    except:
        try:
            clr.AddReferenceByName('Microsoft.Office.Interop.Excel, Version=11.0.0.0, Culture=neutral, PublicKeyToken=71e9bce111e9429c')
        except:
            pass

#------------------------------------------------------------------------------
def TryLoadWordInteropAssembly():
    try:
        clr.AddReferenceByName('Microsoft.Office.Interop.Word, Version=12.0.0.0, Culture=neutral, PublicKeyToken=71e9bce111e9429c')
    except:
        try:
            clr.AddReferenceByName('Microsoft.Office.Interop.Word, Version=11.0.0.0, Culture=neutral, PublicKeyToken=71e9bce111e9429c')
        except:
            pass

#------------------------------------------------------------------------------
def IsExcelInstalled():
    from Microsoft.Win32 import Registry
    from System.IO import File

    excel = None
    
    #Office 11 or 12 are both OK for this test. Office 12 is preferred.
    excel = Registry.LocalMachine.OpenSubKey("Software\\Microsoft\\Office\\12.0\\Excel\\InstallRoot")
    if excel==None:
        excel = Registry.LocalMachine.OpenSubKey("Software\\Microsoft\\Office\\11.0\\Excel\\InstallRoot")
    
    #sanity check
    if excel==None:
        return False
    
    #make sure it's really installed on disk
    excel_path = excel.GetValue("Path") + "excel.exe"
    return File.Exists(excel_path)

#------------------------------------------------------------------------------
def IsWordInstalled():
    from Microsoft.Win32 import Registry
    from System.IO import File

    word  = None
    
    #Office 11 or 12 are both OK for this test. Office 12 is preferred.
    word = Registry.LocalMachine.OpenSubKey("Software\\Microsoft\\Office\\12.0\\Word\\InstallRoot")
    if word==None:
        word= Registry.LocalMachine.OpenSubKey("Software\\Microsoft\\Office\\11.0\\Word\\InstallRoot")
    
    #sanity check
    if word==None:
        return False
    
    #make sure it's really installed on disk
    word_path = word.GetValue("Path") + "winword.exe"
    return File.Exists(word_path)

#------------------------------------------------------------------------------
def CreateExcelApplication():
    #TODO: why is there use of the GUID here?
    #import clr
    #typelib = clr.LoadTypeLibrary(System.Guid("00020813-0000-0000-C000-000000000046"))
    #return typelib.Excel.Application()
    import System
    type = System.Type.GetTypeFromProgID("Excel.Application")
    return System.Activator.CreateInstance(type)

#------------------------------------------------------------------------------
def CreateWordApplication():    
    import System
    #import clr
    #typelib = clr.LoadTypeLibrary(System.Guid("00020905-0000-0000-C000-000000000046"))
    #return typelib.Word.Application()
    type = System.Type.GetTypeFromProgID("Word.Application")
    return System.Activator.CreateInstance(type)

#------------------------------------------------------------------------------
def CreateAgentServer():
    import clr
    from System import Guid
    typelib = clr.LoadTypeLibrary(Guid("A7B93C73-7B81-11D0-AC5F-00C04FD97575"))
    return typelib.AgentServerObjects.AgentServer()
    
#------------------------------------------------------------------------------
def CreateDlrComServer():
    com_type_name = "DlrComLibrary.DlrComServer"
    
    if is_cli:
        com_obj = getRCWFromProgID(com_type_name)
    else:
        com_obj = win32com.client.Dispatch(com_type_name)
        
    return com_obj    

#------------------------------------------------------------------------------    
def getTypeFromProgID(prog_id):
    '''
    Returns the Type object for prog_id.
    '''    
    return Type.GetTypeFromProgID(prog_id)

#------------------------------------------------------------------------------    
def getRCWFromProgID(prog_id):
    '''
    Returns an instance of prog_id.
    '''
    if is_cli:
        return Activator.CreateInstance(getTypeFromProgID(prog_id))
    else:
        return win32com.client.Dispatch(prog_id)

#------------------------------------------------------------------------------
def genPeverifyInteropAsm(file):
    #if this isn't a test run that will invoke peverify there's no point in
    #continuing
    if not is_peverify_run: 
        return
    else:
        mod_name = file.rsplit("\\", 1)[1].split(".py")[0]
        print "Generating interop assemblies for the", mod_name, "test module which are needed in %TEMP% by peverify..."
        from System.IO import Path
        tempDir = Path.GetTempPath()
        cwd = nt.getcwd()
    
    #maps COM interop test module names to a list of DLLs
    module_dll_dict = {
        "excel" :          [],
        "msagent" :        [agentsvr_path],
        "scriptpw" :       [scriptpw_path],
        "word" :           [],
    }
    
    dlrcomlib_list = [  "dlrcomserver", "paramsinretval", "method", "obj", "prop",  ]
    if is_cli32:
        temp_name = testpath.rowan_root + "\\Test\\DlrComLibrary\\Debug\\DlrComLibrary.dll" 
    else:
        temp_name = testpath.rowan_root + "\\Test\\DlrComLibrary\\x64\\Release\\DlrComLibrary.dll" 
    for mod_name in dlrcomlib_list: module_dll_dict[mod_name] = [ temp_name ]
    
    
    if not file_exists_in_path("tlbimp.exe"):
        print "ERROR: tlbimp.exe is not in the path!"
        sys.exit(1)
    
    try:
        if not module_dll_dict.has_key(mod_name):
            print "WARNING: cannot determine which interop assemblies to install!"
            print "         This may affect peverify runs adversely."
            print
            return
            
        else:
            nt.chdir(tempDir)
    
            for com_dll in module_dll_dict[mod_name]:
                if not file_exists(com_dll):
                    print "\tERROR: %s does not exist!" % (com_dll)
                    continue
    
                print "\trunning tlbimp on", com_dll
                run_tlbimp(com_dll)
        
    finally:
        nt.chdir(cwd)   
        
#------------------------------------------------------------------------------
#--Fake parts of System for compat tests
if sys.platform=="win32":
    class System:
        class Byte(int):
            MinValue = 0
            MaxValue = 255
        class SByte(int):
            MinValue = -128
            MaxValue = 127
        class Int16(int):
            MinValue = -32768
            MaxValue = 32767
        class UInt16(int):
            MinValue = 0
            MaxValue = 65535
        class Int32(int):
            MinValue = -2147483648
            MaxValue =  2147483647
        class UInt32(long):
            MinValue = 0
            MaxValue = 4294967295
        class Int64(long):
            MinValue = -9223372036854775808L
            MaxValue =  9223372036854775807L
        class UInt64(long):
            MinValue = 0L 
            MaxValue = 18446744073709551615
        class Single(float):
            MinValue = -3.40282e+038
            MaxValue =  3.40282e+038
        class Double(float):
            MinValue = -1.79769313486e+308
            MaxValue =  1.79769313486e+308
        class String(str):
            pass
        class Boolean(int):
            pass
            

#------------------------------------------------------------------------------
def run_com_test(name, file):
    run_test(name)
    genPeverifyInteropAsm(file)
    
