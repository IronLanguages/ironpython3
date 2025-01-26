import io
import os
import re
import sys
import unittest

from .test_env import *
from .file_util import FileUtil
from .process_util import ProcessUtil

if is_cli:
    import clr

class stderr_trapper(object):
    def __init__(self):
        self.stderr = io.StringIO()
    def __enter__(self):
        self.oldstderr = sys.stderr
        sys.stderr = self.stderr
        return self
    def __exit__(self, *args):
        sys.stderr = self.oldstderr
        self.stderr.flush()
        self.stderr.seek(0)
        self.messages = self.stderr.readlines()
        self.messages = [x.rstrip() for x in self.messages]
        self.stderr.close()

class stdout_trapper(object):
    def __init__(self):
        self.stdout = io.StringIO()
    def __enter__(self):
        self.oldstdout, sys.stdout = sys.stdout, self.stdout
        return self
    def __exit__(self, *args):
        sys.stdout = self.oldstdout # do this first to avoid writes after seek(0) (e.g. test_regressions.test_cp23555)
        self.stdout.flush()
        self.stdout.seek(0)
        self.messages = self.stdout.readlines()
        self.messages = [x.rstrip() for x in self.messages]
        self.stdout.close()

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

def expectedFailureIf(condition):
    """The test is marked as an expectedFailure if the condition is satisfied."""
    def wrapper(func):
        if condition:
            return unittest.expectedFailure(func)
        else:
            return func
    return wrapper

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
        last_root = root
        root = os.path.dirname(root)
        if root == last_root: raise Exception("Root not found")
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
        temp_dir = self.get_temp_dir()
        if is_cli:
            self._temporary_dir = os.path.join(temp_dir, "IronPythonTest", clr.TargetFramework.translate(str.maketrans(" =,", "__-")))
        else:
            self._temporary_dir = os.path.join(temp_dir, "IronPythonTest", "CPython")
        self.ensure_directory_present(self._temporary_dir)

        self._iron_python_test_dll = _iron_python_test_dll
        self._test_dir = os.path.join(_root, 'Tests')
        self._test_inputs_dir = os.path.join(_root, 'Tests', 'Inputs')

    def add_reference_to_dlr_core(self):
        _add_reference_to_dlr_core()

    def load_iron_python_test(self, *args):
        if not is_cli: return
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

    # assertion helpers

    def assertRaisesMessage(self, expected_exception, expected_message, *args, **kwargs):
        return self.assertRaisesRegex(expected_exception, "^" + re.escape(expected_message) + "$", *args, **kwargs)

    def assertRaisesPartialMessage(self, expected_exception, expected_message, *args, **kwargs):
        return self.assertRaisesRegex(expected_exception, re.escape(expected_message), *args, **kwargs)

    # environment variables
    def get_environ_variable(self, key):
        l = [y for x, y in os.environ.items() if x.lower() == key.lower()]
        if l: return l[0]
        return None

    # file paths
    @property
    def temporary_dir(self):
        return self._temporary_dir

    @property
    def iron_python_test_dll(self):
        return self._iron_python_test_dll

    @property
    def test_dir(self):
        return self._test_dir

    @property
    def test_inputs_dir(self):
        return self._test_inputs_dir

def run_test(name):
    if name == '__main__':
        from test import support
        support.run_unittest(name)

def _flatten_suite(suite):
    tests = []
    for t in suite:
        if isinstance(t, unittest.BaseTestSuite):
            tests.extend(_flatten_suite(t))
        else:
            tests.append(t)
    return tests

def generate_suite(tests, failing_tests, skip_tests=[]):
    all_tests = _flatten_suite(tests)
    test_indices = {t: i for i, t in enumerate(all_tests)}
    unknown_tests = []

    for t in skip_tests:
        try:
            all_tests[test_indices.pop(t)] = None
        except ValueError:
            unknown_tests.append(t)

    for t in failing_tests:
        try:
            all_tests[test_indices.pop(t)] = unittest.expectedFailure(t)
        except ValueError:
            unknown_tests.append(t)

    if unknown_tests:
        raise ValueError("Unknown tests:\n - {}".format('\n - '.join(str(t) for t in unknown_tests)))

    return unittest.TestSuite(t for t in all_tests if t is not None)

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


