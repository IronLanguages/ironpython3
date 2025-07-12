# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

'''
TODO: until we can send signals to other processes consistently (e.g., os.kill),
      signal is fairly difficult to test in an automated fashion. For now this
      will have to do.
'''

import signal
import sys

from iptest import IronPythonTestCase, is_cpython, is_windows, is_posix, is_osx, is_linux, run_test

if is_linux:
    SIG_codes = {'SIGABRT': 6, 'SIGALRM': 14, 'SIGBUS': 7, 'SIGCHLD': 17, 'SIGCLD': 17, 'SIGCONT': 18, 'SIGFPE': 8, 'SIGHUP': 1, 'SIGILL': 4, 'SIGINT': 2, 'SIGIO': 29, 'SIGIOT': 6, 'SIGKILL': 9, 'SIGPIPE': 13, 'SIGPOLL': 29, 'SIGPROF': 27, 'SIGPWR': 30, 'SIGQUIT': 3, 'SIGRTMAX': 64, 'SIGRTMIN': 34, 'SIGSEGV': 11, 'SIGSTKFLT': 16, 'SIGSTOP': 19, 'SIGSYS': 31, 'SIGTERM': 15, 'SIGTRAP': 5, 'SIGTSTP': 20, 'SIGTTIN': 21, 'SIGTTOU': 22, 'SIGURG': 23, 'SIGUSR1': 10, 'SIGUSR2': 12, 'SIGVTALRM': 26, 'SIGWINCH': 28, 'SIGXCPU': 24, 'SIGXFSZ': 25}
elif is_osx:
    SIG_codes = {'SIGABRT': 6, 'SIGALRM': 14, 'SIGBUS': 10, 'SIGCHLD': 20, 'SIGCONT': 19, 'SIGEMT': 7, 'SIGFPE': 8, 'SIGHUP': 1, 'SIGILL': 4, 'SIGINFO': 29, 'SIGINT': 2, 'SIGIO': 23, 'SIGIOT': 6, 'SIGKILL': 9, 'SIGPIPE': 13, 'SIGPROF': 27, 'SIGQUIT': 3, 'SIGSEGV': 11, 'SIGSTOP': 17, 'SIGSYS': 12, 'SIGTERM': 15, 'SIGTRAP': 5, 'SIGTSTP': 18, 'SIGTTIN': 21, 'SIGTTOU': 22, 'SIGURG': 16, 'SIGUSR1': 30, 'SIGUSR2': 31, 'SIGVTALRM': 26, 'SIGWINCH': 28, 'SIGXCPU': 24, 'SIGXFSZ': 25}
elif is_windows:
    SIG_codes = {'SIGABRT': 22, 'SIGBREAK': 21, 'SIGFPE': 8, 'SIGILL': 4, 'SIGINT': 2, 'SIGSEGV': 11, 'SIGTERM': 15}

SUPPORTED_SIGNALS = set(SIG_codes.values())

class SignalTest(IronPythonTestCase):

    def test_000_run_me_first(self):
        WEIRD_CASES = { signal.SIGINT: signal.default_int_handler }
        if is_posix:
            WEIRD_CASES[signal.SIGPIPE] = signal.SIG_IGN
            WEIRD_CASES[signal.SIGXFSZ] = signal.SIG_IGN
        if is_osx:
            WEIRD_CASES[signal.SIGKILL] = None
            WEIRD_CASES[signal.SIGSTOP] = None

        for x in [x for x in SUPPORTED_SIGNALS]:
            with self.subTest(sig=x):
                self.assertEqual(signal.getsignal(x), WEIRD_CASES.get(x, signal.SIG_DFL))

        # test that unsupported signals have no handler
        for x in range(1, signal.NSIG):
            if x in SUPPORTED_SIGNALS: continue
            if is_linux and 35 <= x <= 64: continue # Real-time signals
            self.assertEqual(signal.getsignal(x), None)


    def test_signal_getsignal_negative(self):
        for x in [-2, -1, 0, signal.NSIG, signal.NSIG + 1, signal.NSIG + 2]:
            self.assertRaisesMessage(ValueError, "signal number out of range",
                                signal.getsignal, x)
        for x in [None, "abc", "14"]:
            self.assertRaises(TypeError, signal.getsignal, x)


    def test_module_constants(self):
        if is_linux:
            self.assertEqual(signal.NSIG, 65)
        elif is_osx:
            self.assertEqual(signal.NSIG, 32)
        elif is_windows:
            self.assertEqual(signal.NSIG, 23)

        # when run with CPython, this verifies that SIG_codes are correct and matching CPython
        for sig in SIG_codes:
            if sys.version_info < (3, 11) and is_cpython and sig == 'SIGSTKFLT':
                # SIGSTKFLT is not defined in CPython < 3.11
                continue
            self.assertEqual(getattr(signal, sig), SIG_codes[sig])


    def test_doc(self):
        signal_module = signal
        if sys.version_info >= (3,5):
            import _signal
            signal_module = _signal
        self.assertTrue("get the signal action for a given signal" in signal_module.__doc__)
        self.assertTrue("The default handler for SIGINT installed by Python" in signal.default_int_handler.__doc__)
        self.assertTrue("Return the current action for the given signal" in signal.getsignal.__doc__)
        self.assertTrue("Set the action for the given signal" in signal.signal.__doc__)


    def test_signal_signal_neg(self):
        def a(b, c):
            pass

        # test that unsupported signals raise OSError on trying to set a handler
        for x in range(1, signal.NSIG):
            if x in SUPPORTED_SIGNALS: continue
            if is_linux and signal.SIGRTMIN <= x <= signal.SIGRTMAX: continue # Real-time signals
            self.assertRaisesMessage(ValueError if is_windows else OSError, "invalid signal value" if is_windows else "[Errno 22] Invalid argument",
                                signal.signal, x, a)

        for x in [-2, -1, 0, signal.NSIG, signal.NSIG + 1, signal.NSIG + 2]:
            self.assertRaisesMessage(ValueError, "invalid signal value" if is_windows else "signal number out of range",
                                signal.signal, x, a)

        # TODO
        def bad_sig0(): pass
        def bad_sig1(a): pass
        def bad_sig3(a,b,c): pass

        # test that bad handler objects are caught with TypeError
        for y in SUPPORTED_SIGNALS:
            bad_handlers = [-2, -1, 2, 3, 4, 10, 22, 23, 24, None]
            for x in bad_handlers:
                self.assertRaisesMessage(TypeError, "signal handler must be signal.SIG_IGN, signal.SIG_DFL, or a callable object",
                                    signal.signal, y, x)


    def test_signal_signal(self):
        WEIRD_CASES = { signal.SIGINT: signal.default_int_handler}
        if is_posix:
            WEIRD_CASES[signal.SIGKILL] = None
            WEIRD_CASES[signal.SIGSTOP] = None
            WEIRD_CASES[signal.SIGPIPE] = signal.SIG_IGN
            WEIRD_CASES[signal.SIGXFSZ] = signal.SIG_IGN

        for x in SUPPORTED_SIGNALS:
            with self.subTest(sig=x):

                # correctness of expected handler tested by assertion in test_000_run_me_first
                expected_val = WEIRD_CASES.get(x, signal.SIG_DFL)

                if expected_val is None:
                    # signals that cannot be handled
                    with self.assertRaises(OSError) as cm:
                        signal.signal(x, signal.SIG_DFL)

                    self.assertEqual(cm.exception.errno, 22)
                    self.assertEqual(cm.exception.strerror, "Invalid argument")
                else:
                    # Ideal handler signature
                    def a(signum, frame):
                        return x

                    ret_val = signal.signal(x, a)
                    self.assertEqual(ret_val, expected_val)
                    self.assertEqual(a, signal.getsignal(x))
                    self.assertEqual(a, signal.signal(x, expected_val))  # restores old handler

        # Strange handler signatures
        class KNew(object):
            def __call__(self, *args, **kwargs):
                pass

        a = KNew()
        ret_val = signal.signal(signal.SIGINT, a)
        self.assertEqual(a, signal.getsignal(signal.SIGINT))


run_test(__name__)
