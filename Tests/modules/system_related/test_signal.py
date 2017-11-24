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

'''
TODO: until we can send signals to other processes consistently (e.g., os.kill),
      signal is fairly difficult to test in an automated fashion. For now this
      will have to do.
'''

import signal
import unittest

from iptest import IronPythonTestCase, is_cli, run_test

SUPPORTED_SIGNALS = [   signal.SIGBREAK,
                        signal.SIGABRT,
                        signal.SIGINT, 
                        signal.SIGTERM,
                        signal.SIGFPE, 
                        signal.SIGILL, 
                        signal.SIGSEGV,
                        ]

class SignalTest(IronPythonTestCase):
    
    def test_000_run_me_first(self):
        for x in [x for x in SUPPORTED_SIGNALS if x!=signal.SIGINT]:
            self.assertEqual(signal.getsignal(x), 0)
        self.assertEqual(signal.getsignal(signal.SIGINT), signal.default_int_handler)

        for x in xrange(1, 23):
            if x in SUPPORTED_SIGNALS: continue
            self.assertEqual(signal.getsignal(x), None)

    def test_signal_getsignal_negative(self):
        for x in [-2, -1, 0, 23, 24, 25]:
            self.assertRaisesMessage(ValueError, "signal number out of range",
                                signal.getsignal, x)
        for x in [None, "abc", "14"]:
            self.assertRaises(TypeError, signal.getsignal, x)

    def test_module_constants(self):
        self.assertEqual(signal.NSIG, 23)
        self.assertEqual(signal.SIGABRT, 22)
        self.assertEqual(signal.SIGBREAK, 21)
        self.assertEqual(signal.SIGFPE, 8)
        self.assertEqual(signal.SIGILL, 4)
        self.assertEqual(signal.SIGINT, 2)
        self.assertEqual(signal.SIGSEGV, 11)
        self.assertEqual(signal.SIGTERM, 15)
        self.assertEqual(signal.SIG_DFL, 0)
        self.assertEqual(signal.SIG_IGN, 1)

    def test_doc(self):
        self.assertTrue("get the signal action for a given signal" in signal.__doc__)
        self.assertTrue("The default handler for SIGINT installed by Python" in signal.default_int_handler.__doc__)
        self.assertTrue("Return the current action for the given signal" in signal.getsignal.__doc__)
        self.assertTrue("Set the action for the given signal" in signal.signal.__doc__)

    @unittest.skipUnless(is_cli, 'http://bugs.python.org/issue9324')
    def test_signal_signal_neg(self):
        def a(b, c):
            pass
        
        WEIRD_WORKING_CASES = [6]
        NO_SUCH_DIR = [21]
        
        for x in xrange(1, 23):
            if x in SUPPORTED_SIGNALS: continue
            if x in WEIRD_WORKING_CASES: continue
            self.assertRaises(RuntimeError,
                        signal.signal, x, a)

        for x in [-2, -1, 0, 23, 24, 25]:
            self.assertRaisesMessage(ValueError, "signal number out of range",
                                signal.signal, x, a)

        def bad_sig0(): pass
        def bad_sig1(a): pass
        def bad_sig3(a,b,c): pass

        for y in SUPPORTED_SIGNALS + WEIRD_WORKING_CASES:
            bad_handlers = [-2, -1, 2, 3, 4, 10, 22, 23, 24, None] 
            for x in bad_handlers:
                self.assertRaisesMessage(TypeError, "signal handler must be signal.SIG_IGN, signal.SIG_DFL, or a callable object",
                                    signal.signal, y, x)

    @unittest.skipUnless(is_cli, 'http://bugs.python.org/issue9324')
    def test_signal_signal(self):
        WORKING_CASES = SUPPORTED_SIGNALS + [6]
        WEIRD_CASES = {
                    6: None,
                    2: signal.default_int_handler}
        for x in WORKING_CASES:
            #Ideal handler signature
            def a(signum, frame):
                return x
            
            ret_val = signal.signal(x, a)
            if x not in WEIRD_CASES.keys():
                self.assertEqual(ret_val, signal.SIG_DFL)
            else:
                self.assertEqual(ret_val, WEIRD_CASES[x])
            self.assertEqual(a, signal.getsignal(x))
            
        #Strange handler signatures
        class KNew(object):
            def __call__(self, *args, **kwargs):
                pass
        
        a = KNew()
        ret_val = signal.signal(signal.SIGBREAK, a)
        self.assertEqual(a, signal.getsignal(signal.SIGBREAK))
                 

run_test(__name__)
