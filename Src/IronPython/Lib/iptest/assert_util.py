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

### make this file platform neutral as much as possible

import sys
import time
import cStringIO

from iptest.test_env import *
from iptest import options, l

if not is_silverlight:
    import nt
    from file_util import *
 
from type_util import types

#------------------------------------------------------------------------------
def usage(code, msg=''):
    print sys.modules['__main__'].__doc__ or 'No doc provided'
    if msg: print 'Error message: "%s"' % msg
    sys.exit(code)

if not is_silverlight:
    def get_environ_variable(key):
        l = [nt.environ[x] for x in nt.environ.keys() if x.lower() == key.lower()]
        if l: return l[0]
        else: return None

    def get_temp_dir():
        temp = get_environ_variable("TMP")
        if temp == None: temp = get_environ_variable("TEMP")
        if (temp == None) or (' ' in temp) : 
            temp = r"C:\temp"
        return temp
    
    ironpython_dlls = [
        "Microsoft.Scripting.Core.dll",
        "Microsoft.Scripting.dll",
        "Microsoft.Dynamic.dll",
        "Microsoft.Scripting.Internal.dll",
        "IronPython.Modules.dll",
        "IronPython.dll",
    ]

    def copy_ironpython_dlls(targetdir):
        import System
        for dll in ironpython_dlls:
            src = System.IO.Path.Combine(sys.prefix, dll)
            dst = System.IO.Path.Combine(targetdir, dll)
            try: System.IO.File.Copy(src, dst, True)
            except: pass

    def remove_ironpython_dlls(targetdir):
        import System
        for dll in ironpython_dlls:
            dst = System.IO.Path.Combine(targetdir, dll)
            try: System.IO.File.Delete(dst)
            except: pass

if is_silverlight:
    class testpath:
        rowan_root      = 'E:\\IP\\Main\\' #hack: should be set somewhere else
        ip_root             = rowan_root + r"Languages\IronPython"
        public_testdir      = ip_root + r'Tests'
        compat_testdir      = ip_root + r'Tests\compat'
        test_inputs_dir     = (ip_root + r'Tests\Inputs')
        script_testdir      = (ip_root + r'Scripts')
        
        sys.prefix = ip_root
        sys.path.append(public_testdir)

else:
    class testpath:
        # find the ironpython root directory
        rowan_root          = get_environ_variable("dlr_root")

        basePyDir = 'Languages\\IronPython'
        if not rowan_root:
            rowan_root = sys.prefix
            if is_cli:
                if System.IO.Directory.Exists(path_combine(rowan_root, r'..\..\Src')):
                    basePyDir = r'..\..\Src'

        # get some directories and files
        ip_root             = path_combine(rowan_root, basePyDir)
        external_dir        = path_combine(rowan_root, r'External.LCA_RESTRICTED\Languages\IronPython')
        clean_external_dir  = path_combine(rowan_root, r'External.LCA_RESTRICTED\Languages\CPython\27')
        public_testdir      = path_combine(ip_root, r'Tests')
        compat_testdir      = path_combine(ip_root, r'Tests\compat')
        test_inputs_dir     = path_combine(ip_root, r'Tests\Inputs')
        script_testdir      = path_combine(ip_root, r'Scripts')

        math_testdir        = path_combine(external_dir, r'Math')
        parrot_testdir      = path_combine(external_dir, r'parrotbench')
        lib_testdir         = path_combine(external_dir, r'27\Lib')
        private_testdir     = path_combine(external_dir, r'27\Lib\test')

        temporary_dir   = path_combine(get_temp_dir(), "IronPython")
        ensure_directory_present(temporary_dir)
        
        iron_python_test_dll        = path_combine(sys.prefix, 'IronPythonTest.dll')

        if is_cli: 
            ipython_executable  = sys.executable
            cpython_executable  = path_combine(external_dir, r'27\python.exe')
        else: 
            ipython_executable  = path_combine(sys.prefix, r'ipy.exe')
            cpython_executable  = sys.executable
        
        #team_dir            = path_combine(ip_root, r'Team')
        #team_profile        = path_combine(team_dir, r'settings.py')
        #
        #my_name             = nt.environ.get(r'USERNAME', None)
        #my_dir              = my_name and path_combine(team_dir, my_name) or None
        #my_profile          = my_dir and path_combine(my_dir, r'settings.py') or None
    

class formatter:
    Number         = 60
    TestNameLen    = 40
    SeparatorEqual = '=' * Number
    Separator1     = '#' * Number
    SeparatorMinus = '-' * Number
    SeparatorStar  = '*' * Number
    SeparatorPlus  = '+' * Number
    Space4         = ' ' * 4
    Greater4       = '>' * 4

# helper functions for sys.path
_saved_syspath = []
def preserve_syspath(): 
    _saved_syspath[:] = list(set(sys.path))
    
def restore_syspath():  
    sys.path = _saved_syspath[:]

if is_cli or is_silverlight:
    import clr
    clr.AddReference("IronPython")

def is_interactive():
    if not is_silverlight:
        isInteractive = get_environ_variable("ISINTERACTIVE")
        if isInteractive != None:
            return True
    else:
	    return False

def is_stdlib():
    if is_cli:
        clean_lib = System.IO.Path.GetFullPath(testpath.clean_external_dir + r"\lib").lower()
        for x in sys.path:
            if clean_lib==System.IO.Path.GetFullPath(x).lower():
                return True
                
        dirty_lib = clean_lib.replace("cpython", "ironpython")
        for x in sys.path:
            if dirty_lib==System.IO.Path.GetFullPath(x).lower():
                return True
                
        return False
        
    elif is_silverlight:
        return False
        
    else:
        #CPython should always have access to the standard library
        return True
    

# test support 
def Fail(m):  raise AssertionError(m)

def Assert(c, m = "Assertion failed"):
    if not c: raise AssertionError(m)

def AssertFalse(c, m = "Assertion for False failed"):
    if c: raise AssertionError(m)

def AssertUnreachable(m = None):
    if m: Assert(False, "Unreachable code reached: "+m)
    else: Assert(False, "Unreachable code reached")

def AreEqual(a, b):
    Assert(a == b, "expected %r, but found %r" % (b, a))

def AreNotEqual(a, b):
    Assert(a <> b, "expected only one of the values to be %r" % a)

def AssertContains(containing_string, substring):
    Assert(substring in containing_string, "%s should be in %s" % (substring, containing_string))

def AssertDoesNotContain(containing_string, substring):
    Assert(not substring in containing_string, "%s should not be in %s" % (substring, containing_string))

def SequencesAreEqual(a, b, m=None):
    Assert(len(a) == len(b), m or 'sequence lengths differ: expected %d, but found %d' % (len(b), len(a)))
    for i in xrange(len(a)):
        Assert(a[i] == b[i], m or 'sequences differ at index %d: expected %r, but found %r' % (i, b[i], a[i]))

def AlmostEqual(a, b, tolerance=6):
    Assert(round(a-b, tolerance) == 0, "expected %r and %r almost same" % (a, b))
    
def AssertError(exc, func, *args, **kwargs):
    try:        func(*args, **kwargs)
    except exc: return
    else :      Fail("Expected %r but got no exception" % exc)

def AssertDocEqual(received, expected):
    expected = expected.split(newline)
    received = received.split(newline)
    for x in received:
        if not x in expected:
            raise AssertionError('Extra doc string: ' + x)
        index = expected.index(x)
        del expected[index]
    
    if expected: raise AssertionError('Missing doc strings: ' + expected.join(', '))
    
def AssertInOrNot(l, in_list, not_in_list):
    for x in in_list:
        Assert(x in l, "%s should be in %s" % (x, l))
    for x in not_in_list:
        Assert(x not in l, "%s should not be in %s" % (x, l))
        
# Check that the exception is raised with the provided message

def AssertErrorWithMessage(exc, expectedMessage, func, *args, **kwargs):
    Assert(expectedMessage, "expectedMessage cannot be null")
    try:   func(*args, **kwargs)
    except exc, inst:
        Assert(expectedMessage == inst.__str__(), \
               "Exception %r message (%r) does not match %r" % (type(inst), inst.__str__(), expectedMessage))
    else:  Assert(False, "Expected %r but got no exception" % exc)

def AssertErrorWithPartialMessage(exc, expectedMessage, func, *args, **kwargs):
    Assert(expectedMessage, "expectedMessage cannot be null")
    try:   func(*args, **kwargs)
    except exc, inst:
        Assert(expectedMessage in inst.__str__(), \
               "Exception %r message (%r) does not contain %r" % (type(inst), inst.__str__(), expectedMessage))
    else:  Assert(False, "Expected %r but got no exception" % exc)

def AssertErrorWithNumber(exc, expectedErrorNo, func, *args, **kwargs):
    try:        func(*args, **kwargs)
    except exc, e: 
        AreEqual(e.errno, expectedErrorNo)
    else :      Fail("Expected %r but got no exception" % exc)
    
# Check that the exception is raised with the provided message, where the message
# differs on IronPython and CPython

def AssertErrorWithMessages(exc, ironPythonMessage, cpythonMessage, func, *args, **kwargs):
    if is_cli or is_silverlight:
        expectedMessage = ironPythonMessage
    else:
        expectedMessage = cpythonMessage

    Assert(expectedMessage, "expectedMessage cannot be null")
    try:   func(*args, **kwargs)
    except exc, inst:
        Assert(expectedMessage == inst.__str__(), \
               "Exception %r message (%r) does not contain %r" % (type(inst), inst.__str__(), expectedMessage))
    else:  Assert(False, "Expected %r but got no exception" % exc)

# Check that the exception is raised with the provided message, where the message
# is matches using a regular-expression match
if is_silverlight:
    def load_iron_python_test(*args):
        import clr

        AddReferenceToDlrCore()
        clr.AddReference("Microsoft.Scripting")
        clr.AddReference("Microsoft.Dynamic, Version=2.0.5.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35, processorArchitecture=MSIL")
        clr.AddReference("IronPython")

        ipt_fullname = "IronPythonTest, Version=1.0.0.0, PublicKeyToken=31bf3856ad364e35"
        if args: 
            return clr.LoadAssembly(ipt_fullname)
        else: 
            clr.AddReference(ipt_fullname)
else:
    def AssertErrorWithMatch(exc, expectedMessage, func, *args, **kwargs):
        import re
        Assert(expectedMessage, "expectedMessage cannot be null")
        try:   func(*args, **kwargs)
        except exc, inst:
            Assert(re.compile(expectedMessage).match(inst.__str__()), \
                   "Exception %r message (%r) does not contain %r" % (type(inst), inst.__str__(), expectedMessage))
        else:  Assert(False, "Expected %r but got no exception" % exc)

    def load_iron_python_test(*args):
        import clr

        AddReferenceToDlrCore()
        clr.AddReference("Microsoft.Scripting")
        clr.AddReference("Microsoft.Dynamic")
        clr.AddReference("IronPython")

        if args: 
            return clr.LoadAssemblyFromFileWithPath(testpath.iron_python_test_dll)
        else: 
            clr.AddReferenceToFileAndPath(testpath.iron_python_test_dll)

    def load_iron_python_dll():
        import clr
        from System.IO import File
        #When assemblies are installed into the GAC, we should not expect
        #IronPython.dll to exist alongside IronPython.dll
        if File.Exists(path_combine(sys.prefix, "IronPython.dll")):
            clr.AddReferenceToFileAndPath(path_combine(sys.prefix, "IronPython.dll"))
        else:
            clr.AddReference("IronPython")
        
        
    def GetTotalMemory():
        import System
        # 3 collect calls to ensure collection
        for x in range(3):
            System.GC.Collect()
            System.GC.WaitForPendingFinalizers()
        return System.GC.GetTotalMemory(True)

def _do_nothing(*args): 
    for arg in args:
        print arg
    pass

def get_num_iterations():
    default = 1
    if not is_silverlight:
        value = get_environ_variable('NUM_TEST_ITERATIONS')
    else:
        value = None

    if value:
        num_of_iterations = int(value)
    else:
        num_of_iterations = default

    if num_of_iterations < default :
        num_of_iterations = default

    return num_of_iterations

class disabled:
    def __init__(self, reason):
        self.reason = reason
    def __call__(self, f):
        return _do_nothing("Skipping disabled test %s. (Reason: %s)" % (f.func_name, self.reason))
    
class skip:
    def __init__(self, *platforms):
        if len(platforms) == 1 and isinstance(platforms[0], str): 
            self.platforms = platforms[0].split()
        else: 
            self.platforms = platforms

    def silverlight_test(self):
        return is_silverlight
    def cli64_test(self):
        return is_cli64
    def orcas_test(self):
        return is_orcas
    def interactive_test(self):
	    return is_interactive()
    def multiple_execute_test(self):
		return get_num_iterations() > 1
    def stdlib_test(self):
        return is_stdlib()
    
    def __call__(self, f):
        #skip questionable tests
        if is_silverlight and 'silverlightbug?' in self.platforms:
            msg = '... TODO, investigate Silverlight failure @ %s' % f.func_name
            return _do_nothing(msg)
        elif sys.platform in self.platforms:
            msg = '... Decorated with @skip(%s), skipping %s ...' % (
                self.platforms, f.func_name)
            return _do_nothing(msg)
		
        
        platforms = 'silverlight', 'cli64', 'orcas', 'interactive', 'multiple_execute', 'stdlib'
        for to_skip in platforms:
            platform_test = getattr(self, to_skip + '_test')
            if to_skip in self.platforms and platform_test():
                msg = '... Decorated with @skip(%s), skipping %s ...' % (
                    self.platforms, f.func_name)
                return _do_nothing(msg)
        return f
   
class runonly: 
    def __init__(self, *platforms):
        if len(platforms) == 1 and isinstance(platforms[0], str): 
            self.platforms = platforms[0].split()
        else: 
            self.platforms = platforms
    def __call__(self, f):
        if "orcas" in self.platforms and is_orcas:
            return f
        elif "silverlight" in self.platforms and is_silverlight:
            return f
        elif "stdlib" in self.platforms and is_stdlib():
            return f
        elif sys.platform in self.platforms:
            return f
        else: 
            return _do_nothing('... Decorated with @runonly(%s), Skipping %s ...' % (self.platforms, f.func_name))

@runonly('win32 silverlight cli')
def _func(): pass

# method could be used to skip rest of test
def skiptest(*args):
    #hack: skip  questionable tests:
    if is_silverlight and 'silverlightbug?' in args:
        print '... TODO, whole test module is skipped for Silverlight failure. Need to investigate...' 
        exit_module()
    elif is_silverlight and 'silverlight' in args:
        print '... %s, skipping whole test module...' % sys.platform
        exit_module()
    elif is_interactive() and 'interactive' in args:
        print '... %s, skipping whole test module under "interactive" mode...' % sys.platform
        exit_module()
    elif is_stdlib() and 'stdlib' in args:
        print '... %s, skipping whole test module under "stdlib" mode...' % sys.platform
        exit_module()     
    
    elif is_cli64 and 'cli64' in args:
        print '... %s, skipping whole test module on 64-bit CLI...' % sys.platform
        exit_module()
    
    elif get_num_iterations() > 1 and 'multiple_execute' in args:
        print '... %d invocations, skipping whole test module under "multiple_execute" mode...' % get_num_iterations()
        exit_module()
    
    if sys.platform in args: 
        print '... %s, skipping whole test module...' % sys.platform
        exit_module()

def exit_module():
    #Have to catch exception for below call. Any better way to exit?
    sys.exit(0)

def print_failures(total, failures):
    print
    for failure in failures:
        name, (extype, ex, tb) = failure
        print '------------------------------------'
        print "Test %s failed throwing %s (%s)" % (name, str(extype), str(ex))            
        while tb:
            print ' ... %s in %s line %d' % (tb.tb_frame.f_code.co_name, tb.tb_frame.f_code.co_filename, tb.tb_lineno)
            tb = tb.tb_next	
        print
    
        if is_cli:
            if '-X:ExceptionDetail' in System.Environment.GetCommandLineArgs():
                load_iron_python_test()
                from IronPythonTest import TestHelpers
                print 'CLR Exception: ',
                print TestHelpers.GetContext().FormatException(ex.clsException)

    print
    failcount = len(failures)
    print '%d total, %d passed, %d failed' % (total, total - failcount, failcount)
		
def run_test(mod_name, noOutputPlease=False):
    if not options.RUN_TESTS:
        l.debug("Will not invoke any test cases from '%s'." % mod_name)
        return
        
    import sys
    module = sys.modules[mod_name]
    stdout = sys.stdout
    stderr = sys.stderr
    failures = []
    total = 0
    
    includedTests = [arg[4:] for arg in sys.argv if arg.startswith('run:test_') and not arg.endswith('.py')]
    for name in dir(module): 
        obj = getattr(module, name)
        if isinstance(obj, types.functionType):
            if name.endswith("_clionly") and not is_cli: continue
            if name.startswith("test_"): 
                if not includedTests or name in includedTests:
                    for i in xrange( get_num_iterations()):
                        if not noOutputPlease: 
                            if hasattr(time, 'clock'):
                                print ">>> %6.2fs testing %-40s" % (round(time.clock(), 2), name, ), 
                            else:
                                print ">>> testing %-40s" % name, 
						#obj()
						#catches the error and exit at the end of each test
                        total += 1
                        try:
                            try:
                                obj()
                            finally:
                                # restore std-in / std-err incase the test corrupted it                
                                sys.stdout = stdout
                                sys.stderr = stderr
                            print
                                
                        except:
                            failures.append( (name, sys.exc_info()) )
                            print "FAIL (%s)" % str(sys.exc_info()[0])
					
                elif not noOutputPlease:
                    print ">>> skipping %-40s" % name
    if failures:
        print_failures(total, failures)
        if is_cli:
            cmd_line = System.Environment.CurrentDirectory + "> " + System.Environment.CommandLine
            print "Please run the following command to repro:"
            print "\t" + cmd_line
        
        sys.exit(len(failures))
    else:
        print
        print '%d tests passed' % total

def run_class(mod_name, verbose=False): 
    pass
    

def add_clr_assemblies(*dlls):
    import clr
    prefix = "rowantest."
    for x in dlls:
        if x.startswith(prefix):
            clr.AddReference(x)
        else:
            clr.AddReference(prefix + x)

def AddReferenceToDlrCore():
    import clr
    import System
    if System.Environment.Version.Major >=4:
        clr.AddReference("System.Core")
    else:
        clr.AddReference("Microsoft.Scripting.Core")


class stderr_trapper(object):
    def __init__(self):
        self.stderr = cStringIO.StringIO()
    def __enter__(self):
        self.oldstderr = sys.stderr
        sys.stderr = self.stderr
        return self
    def __exit__(self, *args):
        self.stderr.flush()
        self.stderr.reset()
        self.messages = self.stderr.readlines()
        self.messages = [x.rstrip() for x in self.messages]
        self.stderr.close()
        sys.stderr = self.oldstderr

class stdout_trapper(object):
    def __init__(self):
        self.stdout = cStringIO.StringIO()
    def __enter__(self):
        self.oldstdout, sys.stdout = sys.stdout, self.stdout
        return self
    def __exit__(self, *args):
        self.stdout.flush()
        self.stdout.reset()
        self.messages = self.stdout.readlines()
        self.messages = [x.rstrip() for x in self.messages]
        self.stdout.close()
        sys.stdout = self.oldstdout


#------------------------------------------------------------------------------
MAX_FAILURE_RETRY = 3

def retry_on_failure(f, *args, **kwargs):
    '''
    Utility function which:
    1. Wraps execution of the input function, f
    2. If f() fails, it retries invoking it MAX_FAILURE_RETRY times
    '''
    def t(*args, **kwargs):        
        for i in xrange(MAX_FAILURE_RETRY):
            try:
                ret_val = f(*args, **kwargs)
                return ret_val
            except Exception, e:
                print "retry_on_failure(%s): failed on attempt '%d':" % (f.__name__, i+1)
                print e
                excp_info = sys.exc_info()
                continue
        # raise w/ excep info to preverve the original stack trace
        raise excp_info[0], excp_info[1], excp_info[2]
                
    return t
    
def force_gc():
    if is_silverlight:
        return
    elif is_cpython:
        import gc
        gc.collect()
    else:
        import System
        for i in xrange(100):
            System.GC.Collect()
        System.GC.WaitForPendingFinalizers()