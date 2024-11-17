# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

'''
TODO

P2:
- check Message field
- check str(clrException)
- check repr(clrException)
- check miscellaneous HRESULT values via genUndefinedHresult
- other exception members?
- ISupportErrorInfo
'''

from iptest.assert_util import skiptest
from iptest.cominterop_util import *

if is_cli:
    from System.Runtime.InteropServices import COMException

###############################################################################
##GLOBALS######################################################################
com_type_name = "DlrComLibrary.SimpleErrors"
com_obj = getRCWFromProgID(com_type_name)


###############################################################################
##SANITY CHECKS################################################################

        
###############################################################################
##COR EXCEPTIONS###############################################################

from System import ApplicationException, ArgumentException, OverflowException,CannotUnloadAppDomainException
from System import ArgumentOutOfRangeException, ArithmeticException
from System import ArrayTypeMismatchException, BadImageFormatException 
from System import DivideByZeroException, DuplicateWaitObjectException
from System import EntryPointNotFoundException, ExecutionEngineException
from System import FieldAccessException, FormatException
from System import InvalidCastException, InvalidOperationException 
from System import MethodAccessException, MissingFieldException
from System import MissingMemberException, MissingMethodException
from System import MulticastNotSupportedException, NotFiniteNumberException
from System import NullReferenceException, OutOfMemoryException
from System import RankException, StackOverflowException, SystemException
from System import TypeInitializationException, MemberAccessException
from System import NotImplementedException, ContextMarshalException,NotSupportedException
from System import TypeLoadException, IndexOutOfRangeException
from System.IO import DirectoryNotFoundException, IOException
from System.IO import EndOfStreamException, FileNotFoundException
from System.IO import PathTooLongException
from System.Reflection import InvalidFilterCriteriaException, TargetException
from System.Reflection import ReflectionTypeLoadException
from System.Reflection import TargetInvocationException
from System.Reflection import TargetParameterCountException
from System.Resources import MissingManifestResourceException
from System.Runtime.InteropServices import InvalidComObjectException 
from System.Runtime.InteropServices import InvalidOleVariantTypeException
from System.Runtime.InteropServices import SafeArrayTypeMismatchException
from System.Runtime.Remoting import RemotingException 
from System.Runtime.Serialization import SerializationException
from System.Security import SecurityException, VerificationException
from System.Threading import SynchronizationLockException, ThreadAbortException
from System.Threading import ThreadInterruptedException, ThreadStateException

cor_mapper = {
                com_obj.genCorApplication : ApplicationException,
                com_obj.genCorArgument : ArgumentException,
                com_obj.genCorArgumentOutOfRange : ArgumentOutOfRangeException,
                com_obj.genCorArithmetic : ArithmeticException,
                com_obj.genCorArrayTypeMismatch : ArrayTypeMismatchException,
                com_obj.genCorBadImageFormat : BadImageFormatException,
                com_obj.genCorContextMarshal : ContextMarshalException, 
                com_obj.genCorDirectoryNotFound : DirectoryNotFoundException,
                com_obj.genCorDivideByZero : DivideByZeroException,
                com_obj.genCorDuplicateWaitObject : DuplicateWaitObjectException,
                com_obj.genCorEndOfStream : EndOfStreamException,
                com_obj.genCorException : Exception, #PY
                com_obj.genCorExecutionEngine : ExecutionEngineException,
                com_obj.genCorFieldAccess : FieldAccessException,
                com_obj.genCorFileNotFound : FileNotFoundException,
                com_obj.genCorFormat : FormatException,
                com_obj.genCorIndexOutOfRange : IndexOutOfRangeException,
                com_obj.genCorInvalidCast : InvalidCastException,
                com_obj.genCorInvalidCOMObject : InvalidComObjectException,
                com_obj.genCorInvalidFilterCriteria : InvalidFilterCriteriaException,
                com_obj.genCorInvalidOleVariantType : InvalidOleVariantTypeException,
                com_obj.genCorInvalidOperation : InvalidOperationException,
                com_obj.genCorIO : IOException,
                com_obj.genCorMemberAccess : MemberAccessException,
                com_obj.genCorMethodAccess : MethodAccessException,
                com_obj.genCorMissingField : MissingFieldException,
                com_obj.genCorMissingManifestResource : MissingManifestResourceException,
                com_obj.genCorMissingMember : MissingMemberException,
                com_obj.genCorMissingMethod : MissingMethodException,
                com_obj.genCorMulticastNotSupported : MulticastNotSupportedException,
                com_obj.genCorNotFiniteNumber : NotFiniteNumberException,
                com_obj.genCorNotSupported : NotSupportedException,  
                com_obj.genCorNullReference : NullReferenceException,
                com_obj.genCorOutOfMemory : OutOfMemoryException,
                com_obj.genCorOverflow : OverflowException,
                com_obj.genCorPathTooLong : PathTooLongException,
                com_obj.genCorRank : RankException,
                com_obj.genCorReflectionTypeLoad : ReflectionTypeLoadException,
                com_obj.genCorRemoting : RemotingException,
                com_obj.genCorSafeArrayTypeMismatch : SafeArrayTypeMismatchException,
                com_obj.genCorSecurity : SecurityException,
                com_obj.genCorSerialization : SerializationException,
                com_obj.genCorStackOverflow : StackOverflowException,
                com_obj.genCorSynchronizationLock : SynchronizationLockException,
                com_obj.genCorSystem : SystemException,
                com_obj.genCorTarget : TargetException,
                com_obj.genCorTargetInvocation : TargetInvocationException,
                com_obj.genCorTargetParamCount : TargetParameterCountException,
                com_obj.genCorThreadAborted : ThreadAbortException,
                com_obj.genCorThreadInterrupted : ThreadInterruptedException,
                com_obj.genCorThreadState : ThreadStateException,
                com_obj.genCorThreadStop : EnvironmentError,
                com_obj.genCorTypeLoad : TypeLoadException,
                com_obj.genCorTypeInitialization : TypeInitializationException,
                com_obj.genCorVerification : VerificationException,
                com_obj.genMseeAppDomainUnloaded : CannotUnloadAppDomainException,
                com_obj.genNTEFail : COMException,
                }

def test_cor_exceptions():
    for meth in list(cor_mapper.keys()):
        AssertError(cor_mapper[meth], meth)
    
###############################################################################
##DISP EXCEPTIONS##############################################################

disp_mapper = {
                com_obj.genDispArrayIsLocked :  EnvironmentError,
                com_obj.genDispBadCallee :      EnvironmentError, 
                com_obj.genDispBadIndex :       EnvironmentError, #Should be IndexError?
                com_obj.genDispBadParamCount :  Exception,    #Should be TypeError?
                com_obj.genDispBadVarType :     EnvironmentError, #Should be TypeError?
                com_obj.genDispBufferTooSmall : EnvironmentError, #Should be TypeError?
                com_obj.genDispDivByZero :      ZeroDivisionError,
                com_obj.genDispException :      Exception, 
                com_obj.genDispMemberNotFound : EnvironmentError, #Should be AttributeError?
                com_obj.genDispNoNamedArgs :    EnvironmentError, #Should be TypeError?
                com_obj.genDispNotACollection : EnvironmentError, #Should be TypeError?
                com_obj.genDispOverflow :       EnvironmentError, #Should be OverflowError?
                com_obj.genDispParamNotFound :  EnvironmentError, #Should be TypeError?
                com_obj.genDispParamNotOptional:EnvironmentError, #Should be TypeError?
                com_obj.genDispTypeMismatch :   EnvironmentError, #Should be TypeError?
                com_obj.genDispUnknownInterface:EnvironmentError, #???
                com_obj.genDispUnknownLCID :    EnvironmentError, #???
                com_obj.genDispUnknownName :    EnvironmentError, #???
                }

def test_disp_exceptions():
    for meth in list(disp_mapper.keys()):
        AssertError(disp_mapper[meth], meth)    
    
###############################################################################
##ERROR EXCEPTIONS#############################################################

from exceptions import ArithmeticError
from System import BadImageFormatException, StackOverflowException
from System.IO import DirectoryNotFoundException, FileNotFoundException
from System.IO import PathTooLongException
error_mapper = {
                com_obj.genErrorArithmeticOverflow :    ArithmeticError, #PY
                com_obj.genErrorBadFormat :             BadImageFormatException, #CLR
                com_obj.genErrorPathNotFound :          DirectoryNotFoundException, #CLR
                com_obj.genErrorFileNotFound :          FileNotFoundException, #CLR
                com_obj.genErrorFilenameExcedRange :    PathTooLongException, #CLR
                com_obj.genErrorStackOverflow :         StackOverflowException, #CLR
                }

def test_error_exceptions():
    for meth in list(error_mapper.keys()):
		#AssertError(error_mapper[meth], meth)
        #Dev10 409945 - none of these actually throw an exception..            
		meth()

###############################################################################
##MSEE EXCEPTIONS##############################################################

from System import AppDomainUnloadedException
msee_mapper = {
                com_obj.genMseeAppDomainUnloaded : SystemError,
                }

def test_msee_exceptions():
    for meth in list(msee_mapper.keys()):
        AssertError(msee_mapper[meth], meth)

###############################################################################
##NTE EXCEPTIONS###############################################################

from System.Security.Cryptography import CryptographicException
nte_mapper = {
                com_obj.genNTEFail : EnvironmentError,
                }
                
def test_nte_exceptions():
    for meth in list(nte_mapper.keys()):
        AssertError(COMException, meth)
    
###############################################################################
##GENERIC EXCEPTIONS###########################################################

from exceptions import NotImplementedError, MemoryError
from System import ArgumentException, InvalidCastException, NullReferenceException
generic_mapper = {  
                    com_obj.genInvalidArg :     ArgumentException, #CLR
                    com_obj.genNoInterface :    InvalidCastException, #CLR
                    com_obj.genNotImpl :        NotImplementedError, #PY
                    com_obj.genPointer :        NullReferenceException, #CLR
                    com_obj.genOutOfMemory :    MemoryError, #PY
                    }
                    
def test_generic_exceptions():
    for meth in list(generic_mapper.keys()):
        AssertError(generic_mapper[meth], meth)

###############################################################################
##MISC#########################################################################
'''
[id(73), helpstring("method genUndefinedHresult")] HRESULT genUndefinedHresult([in] ULONG hresult);
'''
    
###############################################################################
##MAIN#########################################################################
run_com_test(__name__, __file__)