# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

# adding some negative test case coverage for the sys module; we currently don't implement
# some methods---there is a CodePlex work item 1042 to track the real implementation of
# these methods

import os
import sys
import unittest

from iptest import IronPythonTestCase, is_cli, path_modifier, run_test, skipUnlessIronPython

CP24381_MESSAGES = []

class SysTest(IronPythonTestCase):
    @skipUnlessIronPython()
    def test_dont_write_bytecode(self):
        self.assertEqual(sys.dont_write_bytecode, True)

    @skipUnlessIronPython()
    def test_api_version(self):
        # api_version
        self.assertEqual(sys.api_version, 0)

    def test_settrace(self):
        """TODO: now that sys.settrace has been implemented this test case needs to be fully revisited"""
        # settrace
        self.assertTrue(hasattr(sys, 'settrace'))

        global traces
        traces = []
        def f(frame, kind, info):
            traces.append(('f', kind, frame.f_code.co_name))
            return g

        def g(frame, kind, info):
            traces.append(('g', kind, frame.f_code.co_name))
            return g_ret

        g_ret = g
        def x():
            abc = 'hello'
            abc = 'next'

        sys.settrace(f)
        x()
        sys.settrace(None)
        self.assertEqual(traces, [('f', 'call', 'x'), ('g', 'line', 'x'), ('g', 'line', 'x'), ('g', 'return', 'x')])

        traces = []
        g_ret = f
        sys.settrace(f)
        x()
        sys.settrace(None)
        self.assertEqual(traces, [('f', 'call', 'x'), ('g', 'line', 'x'), ('f', 'line', 'x'), ('g', 'return', 'x')])

        # verify globals/locals are correct on the frame
        global frameobj
        def f(frame, event, payload):
            global frameobj
            frameobj = frame

        def g(a):
            b = 42

        sys.settrace(f)
        g(32)
        sys.settrace(None)
        self.assertEqual(frameobj.f_locals, {'a': 32, 'b':42})
        self.assertTrue('run_test' in frameobj.f_globals)

        if is_cli:
            # -X:Tracing should enable tracing of top-level code
            import os
            content = """a = "aaa"
import pdb; pdb.set_trace()
b = "bbb"
c = "ccc"
final = a + b + c
print final"""
            f = open('temp.py', 'w+')
            try:
                f.write(content)
                f.close()

                stdin, stdout = os.popen2(sys.executable +  ' -X:Tracing -X:Frames temp.py')
                stdin.write('n\nn\nn\nn\nn\nn\nn\nn\n')
                stdin.flush()
                out = [x for x in stdout]
                self.assertTrue('-> b = "bbb"\n' in out)
                self.assertTrue('-> c = "ccc"\n' in out)
                self.assertTrue('-> final = a + b + c\n' in out)
                self.assertTrue('-> print final\n' in out)
                self.assertTrue('(Pdb) aaabbbccc\n' in out)
                self.assertTrue('--Return--\n' in out)
                self.assertTrue('-> print final\n' in out)
                self.assertTrue('(Pdb) ' in out)
            finally:
                os.unlink('temp.py')

    def test_call_tracing(self):
        def f(i):
            return i * 2
        def g():
            pass

        # outside of a traceback
        self.assertEqual(10, sys.call_tracing(f, (5, )))

        # inside of a traceback
        log = []
        def thandler(frm, evt, pl):
            if evt == 'call':
                log.append(frm.f_code.co_name)
                if log[-1] == 'g':
                    sys.call_tracing(f, (5, ))
            return thandler

        sys.settrace(thandler)
        g()
        sys.settrace(None)

        self.assertEqual(log, ['g', 'f'])

    @skipUnlessIronPython()
    def test_version(self):
        import re
        # version DEBUG (FileVersion) [TargetFramework on FrameworkDescription (bitness-bit)]
        # for example:
        #     2.7.10a1 DEBUG (2.7.10.0001)
        #     [.NETFramework,Version=v4.5 on .NET Framework 4.8.3752.0 (64-bit)]
        regex = r'[\w.+]+\s*(?: DEBUG)?\s+\([^)]+\)\s+\[[^\]]+ \((?:32|64)-bit\)\]'
        self.assertTrue(re.match(regex, sys.version, re.IGNORECASE) != None)

    def test_winver(self):
        import re
        #E.g., "2.5"
        self.assertTrue(re.match("^\d\.\d$", sys.winver) != None)

    def test_ps1(self):
        self.assertTrue(not hasattr(sys, "ps1"))

    def test_ps2(self):
        self.assertTrue(not hasattr(sys, "ps2"))

    def test_getsizeof(self):
        '''TODO: revisit'''
        if is_cli:
            self.assertEqual(sys.getsizeof(1), sys.getsizeof(1.0))
        else:
            self.assertTrue(sys.getsizeof(1)<sys.getsizeof(1.0))

    def test_gettrace(self):
        '''TODO: revisit'''
        self.assertEqual(sys.gettrace(), None)

        def temp_func(*args, **kwargs):
            pass

        sys.settrace(temp_func)
        self.assertEqual(sys.gettrace(), temp_func)
        sys.settrace(None)
        self.assertEqual(sys.gettrace(), None)

    @unittest.skipIf(is_cli, 'https://github.com/IronLanguages/main/issues/740')
    def test_cp24381(self):
        import sys
        orig_sys_trace_func = sys.gettrace()
        def f(*args):
            global CP24381_MESSAGES
            CP24381_MESSAGES += args[1:]
            return f

        cp24381_file_name = "cp24381.py"
        cp24381_contents  = """
print 'a'
print 'b'
print 'c'

def f():
    print 'hi'

f()
"""

        try:
            self.write_to_file(cp24381_file_name, cp24381_contents)
            sys.settrace(f)
            with path_modifier('.'):
                import cp24381
        finally:
            sys.settrace(orig_sys_trace_func)
            os.unlink(cp24381_file_name)

        self.assertEqual(CP24381_MESSAGES,
                ['call', None, 'line', None, 'line', None, 'line', None, 'line',
                None, 'line', None, 'call', None, 'line', None, 'return', None,
                'return', None])

    def test_cp30130(self):
        def f(frame, event, arg):
            if event == 'exception':
                    global ex
                    ex = arg
            return f

        sys.settrace(f)

        def g():
            raise Exception()

        try:
            g()
        except:
            pass

        exc_type = ex[0]
        exc_value = ex[1]
        tb_value = ex[2]

        import traceback
        self.assertTrue(''.join(traceback.format_exception(exc_type, exc_value, tb_value)).find('line') != -1)

        sys.settrace(None)

    def test_getrefcount(self):
        import warnings
        self.assertTrue(hasattr(sys, 'getrefcount'))

        with warnings.catch_warnings(record = True) as w:
            count = sys.getrefcount(None)

        self.assertNotEqual(0, count)
        self.assertTrue(w)
        self.assertTrue('dummy result' in str(w[0].message))

    def test_builtin_module_names(self):
        ''' Validate properly sorted module names for issue #875 '''
        self.assertFalse(sys.builtin_module_names == tuple(sorted(sys.builtin_module_names)))


run_test(__name__)

