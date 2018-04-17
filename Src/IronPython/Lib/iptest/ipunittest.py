import io
import os
import sys
import unittest

from .test_env import *
from .file_util import FileUtil
from .process_util import ProcessUtil

if is_cli:
    import clr

class _AssertRaisesContext(object):
    """A context manager used to implement TestCase.assertRaises* methods."""

    def __init__(self, expected, test_case, expected_regexp=None, expected_number=None, expected_message=None, expected_messages=[], partial=False):
        self.expected = expected
        self.failureException = test_case.failureException
        self.expected_regexp = expected_regexp
        self.expected_message = expected_message
        self.expected_number = expected_number

    def __enter__(self):
        return self

    def __exit__(self, exc_type, exc_value, tb):
        if exc_type is None:
            try:
                exc_name = self.expected.__name__
            except AttributeError:
                exc_name = str(self.expected)
            raise self.failureException(
                "{0} not raised".format(exc_name))
        if not issubclass(exc_type, self.expected):
            # let unexpected exceptions pass through
            return False
        self.exception = exc_value # store for later retrieval
        if self.expected_regexp is None:
            return True

        if expected_messages:
            if is_cli:
                self.expected_message = expected_messages[0]
            else:
                self.expected_message = expected_messages[1]

        if self.expected_regexp:
            expected_regexp = self.expected_regexp
            if not expected_regexp.search(str(exc_value)):
                raise self.failureException('"%s" does not match "%s"' %
                        (expected_regexp.pattern, str(exc_value)))
        elif self.expected_message:
            if partial:
                if self.expected_message in str(exc_value):
                    raise self.failureException("'%s' does not match '%s'" %
                            (self.expected_message, str(exc_value)))
            else:
                if self.expected_message != str(exc_value):
                    raise self.failureException("'%s' does not match '%s'" %
                            (self.expected_message, str(exc_value)))
        elif not (self.expected_number is None):
            if self.expected_number != exc_value.errno:
                raise self.failureException("'%d' does not match '%d'" % 
                            (self.expected_number, exc_value.errno))
        return True

class stderr_trapper(object):
    def __init__(self):
        self.stderr = io.StringIO()
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
        self.stdout = io.StringIO()
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

class path_modifier(object):
    def __init__(self, *directories):
        self._directories = directories
        self._old_path = [x for x in sys.path]
    def __enter__(self):
        sys.path = [x for x in self._directories]
    def __exit__(self, *args):
        sys.path = [x for x in self._old_path]

def skipUnlessIronPython():
    """Skips the test unless currently running on IronPython"""
    return unittest.skipUnless(is_cli, 'IronPython specific test')

MAX_FAILURE_RETRY = 3
def retryOnFailure(f, times=MAX_FAILURE_RETRY, *args, **kwargs):
    '''
    Utility function which:
    1. Wraps execution of the input function, f
    2. If f() fails, it retries invoking it MAX_FAILURE_RETRY times
    '''
    def t(*args, **kwargs):
        for i in range(times):
            try:
                ret_val = f(*args, **kwargs)
                return ret_val
            except Exception as e:
                print(("retryOnFailure(%s): failed on attempt '%d':" % (f.__name__, i+1)))
                print(e)
                excp_info = sys.exc_info()
                continue
        # raise w/ excep info to preverve the original stack trace
        raise excp_info[0](excp_info[1]).with_traceback(excp_info[2])

    return t

def _find_root():
    test_dirs = ['Src', 'Build', 'Package', 'Tests', 'Util']
    root = os.getcwd()
    test = all([os.path.exists(os.path.join(root, x)) for x in test_dirs])
    while not test:
        root = os.path.dirname(root)
        test = all([os.path.exists(os.path.join(root, x)) for x in test_dirs])
    return root

_root = _find_root()

def source_root():
    return _root

def _add_reference_to_dlr_core():
    if is_cli:
        clr.AddReference("System.Core")

_iron_python_test_dll = os.path.join(sys.prefix, 'IronPythonTest.dll')

def load_ironpython_test(*args):
    _add_reference_to_dlr_core()
    clr.AddReference("Microsoft.Scripting")
    clr.AddReference("Microsoft.Dynamic")
    clr.AddReference("IronPython")

    if args: 
        return clr.LoadAssemblyFromFileWithPath(_iron_python_test_dll)
    else: 
        clr.AddReferenceToFileAndPath(_iron_python_test_dll)

class IronPythonTestCase(unittest.TestCase, FileUtil, ProcessUtil):

    def setUp(self):
        self._temporary_dir = os.path.join(self.temp_dir, "IronPython")
        self.ensure_directory_present(self._temporary_dir)
        
        self._iron_python_test_dll = _iron_python_test_dll
        self._test_dir = os.path.join(_root, 'Tests')
        self._test_inputs_dir = os.path.join(_root, 'Tests', 'Inputs')

    def add_reference_to_dlr_core(self):
        _add_reference_to_dlr_core()
        
    def load_iron_python_test(self, *args):
        if args:
            return load_ironpython_test(*args)
        else:
            load_ironpython_test()

    def load_iron_python_dll(self):
        #When assemblies are installed into the GAC, we should not expect
        #IronPython.dll to exist alongside IronPython.dll
        if os.path.exists(os.path.join(sys.prefix, "IronPython.dll")):
            clr.AddReferenceToFileAndPath(os.path.join(sys.prefix, "IronPython.dll"))
        else:
            clr.AddReference("IronPython")

    def add_clr_assemblies(self, *dlls):
        """Adds test assemblies as references"""
        import clr
        prefix = "rowantest."
        for x in dlls:
            if x.startswith(prefix):
                clr.AddReference(x)
            else:
                clr.AddReference(prefix + x)

    def force_gc(self):
        if is_cpython:
            import gc
            gc.collect()
        else:
            import System
            for i in range(100):
                System.GC.Collect()
            System.GC.WaitForPendingFinalizers()

    # assertion helpers

    def assertRaisesMessage(self, expected_exception, expected_message,
                           callable_obj=None, *args, **kwargs):
        """Asserts that the message in a raised exception matches the expected message.

        Args:
            expected_exception: Exception class expected to be raised.
            expected_message: Expected error message
            callable_obj: Function to be called.
            args: Extra args.
            kwargs: Extra kwargs.
        """
        context = _AssertRaisesContext(expected_exception, self, expected_message=expected_message)
        if callable_obj is None:
            return context
        with context:
            callable_obj(*args, **kwargs)

    def assertRaisesMessages(self, expected_exception, ipy_expected_message, cpy_expected_message,
                           callable_obj=None, *args, **kwargs):
        """Asserts that the message in a raised exception matches the expected messages for IPy and CPy.

        Args:
            expected_exception: Exception class expected to be raised.
            expected_message: Expected error message
            callable_obj: Function to be called.
            args: Extra args.
            kwargs: Extra kwargs.
        """
        context = _AssertRaisesContext(expected_exception, self, expected_messages=[ipy_expected_message, cpy_expected_message])
        if callable_obj is None:
            return context
        with context:
            callable_obj(*args, **kwargs)

    def assertRaisesPartialMessage(self, expected_exception, expected_message,
                           callable_obj=None, *args, **kwargs):
        """Asserts that the message in a raised exception is contained in the expected message.

        Args:
            expected_exception: Exception class expected to be raised.
            expected_message: Expected error message
            callable_obj: Function to be called.
            args: Extra args.
            kwargs: Extra kwargs.
        """
        context = _AssertRaisesContext(expected_exception, self, expected_message=expected_message, partial=True)
        if callable_obj is None:
            return context
        with context:
            callable_obj(*args, **kwargs)

    def assertRaisesNumber(self, expected_exception, expected_number, 
                        callable_obj=None, *args, **kwargs):
        """Asserts that the message in a raised exception is contained in the expected message.

        Args:
            expected_exception: Exception class expected to be raised.
            expected_message: Expected error message
            callable_obj: Function to be called.
            args: Extra args.
            kwargs: Extra kwargs.
        """
        context = _AssertRaisesContext(expected_exception, self, expected_number=expected_number)
        if callable_obj is None:
            return context
        with context:
            callable_obj(*args, **kwargs)

    def assertArrayEqual(self,a,b):
        self.assertEqual(a.Length, b.Length)
        for x in range(a.Length):
            self.assertEqual(a[x], b[x])

    def assertUnreachable(self, msg=None):
        if msg: self.fail("Unreachable code reached: " + msg)
        else: self.fail("Unreachable code reached")

    def assertWarns(self, warning, callable, *args, **kwds):
        import warnings
        with warnings.catch_warnings(record=True) as warning_list:
            warnings.simplefilter('always')
            result = callable(*args, **kwds)
            self.assertTrue(any(item.category == warning for item in warning_list))

    def assertWarnsPartialMessage(self, warning, msg, callable, *args, **kwds):
        import warnings
        with warnings.catch_warnings(record=True) as warning_list:
            warnings.simplefilter('always')
            result = callable(*args, **kwds)
            self.assertEqual(len(warning_list), 1)
            self.assertTrue(any(item.category == warning for item in warning_list))
            self.assertIn(msg, str(warning_list[0].message))

    def assertNotWarns(self, warning, callable, *args, **kwds):
        import warnings
        with warnings.catch_warnings(record=True) as warning_list:
            warnings.simplefilter('always')
            result = callable(*args, **kwds)
            self.assertFalse(any(item.category == warning for item in warning_list))

    def assertDocEqual(self, received, expected):
        expected = expected.split(newline)
        received = received.split(newline)
        for x in received:
            if not x in expected:
                self.fail('Extra doc string: ' + x)
            index = expected.index(x)
            del expected[index]
        
        if expected: self.fail('Missing doc strings: ' + expected.join(', '))

    def assertInAndNot(self, test_list, in_list, not_in_list):
        for x in in_list:
            self.assertIn(x, test_list)
        for x in not_in_list:
            self.assertNotIn(x, test_list)

    # environment variables
    def get_environ_variable(self, key):
        l = [y for x, y in os.environ.items() if x.lower() == key.lower()]
        if l: return l[0]
        return None

    # file paths
    def get_temporary_dir(self):
        return self._temporary_dir
    temporary_dir = property(get_temporary_dir)

    def get_iron_python_test_dll(self):
        return self._iron_python_test_dll
    iron_python_test_dll = property(get_iron_python_test_dll)

    def get_test_dir(self):
        return self._test_dir
    test_dir = property(get_test_dir)

    def get_test_inputs_dir(self):
        return self._test_inputs_dir
    test_inputs_dir = property(get_test_inputs_dir)

def run_test(name):
    if name == '__main__':
        from test import support
        support.run_unittest(name)

# class testpath:
#     # find the ironpython root directory
#     rowan_root          = get_environ_variable("dlr_root")

#     basePyDir = path_combine('Languages', 'IronPython')
#     if not rowan_root:
#         rowan_root = sys.prefix
#         if is_cli:
#             if System.IO.Directory.Exists(path_combine(rowan_root, '../../Src')):
#                 basePyDir = '../../Src'

#     # get some directories and files
#     ip_root             = path_combine(rowan_root, basePyDir)
#     external_dir        = path_combine(rowan_root, 'External.LCA_RESTRICTED/Languages/IronPython')
#     clean_external_dir  = path_combine(rowan_root, 'External.LCA_RESTRICTED/Languages/CPython/27')
#     public_testdir      = path_combine(ip_root, 'Tests')
#     compat_testdir      = path_combine(ip_root, 'Tests/compat')
#     test_inputs_dir     = path_combine(ip_root, 'Tests/Inputs')
#     script_testdir      = path_combine(ip_root, 'Scripts')

#     math_testdir        = path_combine(external_dir, 'Math')
#     parrot_testdir      = path_combine(external_dir, 'parrotbench')
#     lib_testdir         = path_combine(external_dir, '27/Lib')
#     private_testdir     = path_combine(external_dir, '27/Lib/test')

#     if is_cli: 
#         ipython_executable  = sys.executable
#         if is_posix:
#             cpython_executable  = '/usr/bin/python2.7'
#         else:
#             cpython_executable  = path_combine(external_dir, '27/python.exe')
#     else: 
#         ipython_executable  = path_combine(sys.prefix, 'ipy.exe')
#         cpython_executable  = sys.executable
    
#     #team_dir            = path_combine(ip_root, r'Team')
#     #team_profile        = path_combine(team_dir, r'settings.py')
#     #
#     #my_name             = os.environ.get(r'USERNAME', None)
#     #my_dir              = my_name and path_combine(team_dir, my_name) or None
#     #my_profile          = my_dir and path_combine(my_dir, r'settings.py') or None


