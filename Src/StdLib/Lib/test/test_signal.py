import unittest
from test import support
from contextlib import closing
import enum
import gc
import pickle
import select
import signal
import socket
import struct
import subprocess
import traceback
import sys, os, time, errno
from test.support.script_helper import assert_python_ok, spawn_python
try:
    import threading
except ImportError:
    threading = None
try:
    import _testcapi
except ImportError:
    _testcapi = None


class HandlerBCalled(Exception):
    pass


def exit_subprocess():
    """Use os._exit(0) to exit the current subprocess.

    Otherwise, the test catches the SystemExit and continues executing
    in parallel with the original test, so you wind up with an
    exponential number of tests running concurrently.
    """
    os._exit(0)


def ignoring_eintr(__func, *args, **kwargs):
    try:
        return __func(*args, **kwargs)
    except OSError as e:
        if e.errno != errno.EINTR:
            raise
        return None


class GenericTests(unittest.TestCase):

    @unittest.skipIf(threading is None, "test needs threading module")
    def test_enums(self):
        for name in dir(signal):
            sig = getattr(signal, name)
            if name in {'SIG_DFL', 'SIG_IGN'}:
                self.assertIsInstance(sig, signal.Handlers)
            elif name in {'SIG_BLOCK', 'SIG_UNBLOCK', 'SIG_SETMASK'}:
                self.assertIsInstance(sig, signal.Sigmasks)
            elif name.startswith('SIG') and not name.startswith('SIG_'):
                self.assertIsInstance(sig, signal.Signals)
            elif name.startswith('CTRL_'):
                self.assertIsInstance(sig, signal.Signals)
                self.assertEqual(sys.platform, "win32")


@unittest.skipIf(sys.platform == "win32", "Not valid on Windows")
class InterProcessSignalTests(unittest.TestCase):
    MAX_DURATION = 20   # Entire test should last at most 20 sec.

    def setUp(self):
        self.using_gc = gc.isenabled()
        gc.disable()

    def tearDown(self):
        if self.using_gc:
            gc.enable()

    def format_frame(self, frame, limit=None):
        return ''.join(traceback.format_stack(frame, limit=limit))

    def handlerA(self, signum, frame):
        self.a_called = True

    def handlerB(self, signum, frame):
        self.b_called = True
        raise HandlerBCalled(signum, self.format_frame(frame))

    def wait(self, child):
        """Wait for child to finish, ignoring EINTR."""
        while True:
            try:
                child.wait()
                return
            except OSError as e:
                if e.errno != errno.EINTR:
                    raise

    def run_test(self):
        # Install handlers. This function runs in a sub-process, so we
        # don't worry about re-setting the default handlers.
        signal.signal(signal.SIGHUP, self.handlerA)
        signal.signal(signal.SIGUSR1, self.handlerB)
        signal.signal(signal.SIGUSR2, signal.SIG_IGN)
        signal.signal(signal.SIGALRM, signal.default_int_handler)

        # Variables the signals will modify:
        self.a_called = False
        self.b_called = False

        # Let the sub-processes know who to send signals to.
        pid = os.getpid()

        child = ignoring_eintr(subprocess.Popen, ['kill', '-HUP', str(pid)])
        if child:
            self.wait(child)
            if not self.a_called:
                time.sleep(1)  # Give the signal time to be delivered.
        self.assertTrue(self.a_called)
        self.assertFalse(self.b_called)
        self.a_called = False

        # Make sure the signal isn't delivered while the previous
        # Popen object is being destroyed, because __del__ swallows
        # exceptions.
        del child
        try:
            child = subprocess.Popen(['kill', '-USR1', str(pid)])
            # This wait should be interrupted by the signal's exception.
            self.wait(child)
            time.sleep(1)  # Give the signal time to be delivered.
            self.fail('HandlerBCalled exception not raised')
        except HandlerBCalled:
            self.assertTrue(self.b_called)
            self.assertFalse(self.a_called)

        child = ignoring_eintr(subprocess.Popen, ['kill', '-USR2', str(pid)])
        if child:
            self.wait(child)  # Nothing should happen.

        try:
            signal.alarm(1)
            # The race condition in pause doesn't matter in this case,
            # since alarm is going to raise a KeyboardException, which
            # will skip the call.
            signal.pause()
            # But if another signal arrives before the alarm, pause
            # may return early.
            time.sleep(1)
        except KeyboardInterrupt:
            pass
        except:
            self.fail("Some other exception woke us from pause: %s" %
                      traceback.format_exc())
        else:
            self.fail("pause returned of its own accord, and the signal"
                      " didn't arrive after another second.")

    # Issue 3864, unknown if this affects earlier versions of freebsd also
    @unittest.skipIf(sys.platform=='freebsd6',
        'inter process signals not reliable (do not mix well with threading) '
        'on freebsd6')
    def test_main(self):
        # This function spawns a child process to insulate the main
        # test-running process from all the signals. It then
        # communicates with that child process over a pipe and
        # re-raises information about any exceptions the child
        # raises. The real work happens in self.run_test().
        os_done_r, os_done_w = os.pipe()
        with closing(os.fdopen(os_done_r, 'rb')) as done_r, \
             closing(os.fdopen(os_done_w, 'wb')) as done_w:
            child = os.fork()
            if child == 0:
                # In the child process; run the test and report results
                # through the pipe.
                try:
                    done_r.close()
                    # Have to close done_w again here because
                    # exit_subprocess() will skip the enclosing with block.
                    with closing(done_w):
                        try:
                            self.run_test()
                        except:
                            pickle.dump(traceback.format_exc(), done_w)
                        else:
                            pickle.dump(None, done_w)
                except:
                    print('Uh oh, raised from pickle.')
                    traceback.print_exc()
                finally:
                    exit_subprocess()

            done_w.close()
            # Block for up to MAX_DURATION seconds for the test to finish.
            r, w, x = select.select([done_r], [], [], self.MAX_DURATION)
            if done_r in r:
                tb = pickle.load(done_r)
                if tb:
                    self.fail(tb)
            else:
                os.kill(child, signal.SIGKILL)
                self.fail('Test deadlocked after %d seconds.' %
                          self.MAX_DURATION)


@unittest.skipIf(sys.platform == "win32", "Not valid on Windows")
class PosixTests(unittest.TestCase):
    def trivial_signal_handler(self, *args):
        pass

    def test_out_of_range_signal_number_raises_error(self):
        self.assertRaises(ValueError, signal.getsignal, 4242)

        self.assertRaises(ValueError, signal.signal, 4242,
                          self.trivial_signal_handler)

    def test_setting_signal_handler_to_none_raises_error(self):
        self.assertRaises(TypeError, signal.signal,
                          signal.SIGUSR1, None)

    def test_getsignal(self):
        hup = signal.signal(signal.SIGHUP, self.trivial_signal_handler)
        self.assertIsInstance(hup, signal.Handlers)
        self.assertEqual(signal.getsignal(signal.SIGHUP),
                         self.trivial_signal_handler)
        signal.signal(signal.SIGHUP, hup)
        self.assertEqual(signal.getsignal(signal.SIGHUP), hup)


@unittest.skipUnless(sys.platform == "win32", "Windows specific")
class WindowsSignalTests(unittest.TestCase):
    def test_issue9324(self):
        # Updated for issue #10003, adding SIGBREAK
        handler = lambda x, y: None
        checked = set()
        for sig in (signal.SIGABRT, signal.SIGBREAK, signal.SIGFPE,
                    signal.SIGILL, signal.SIGINT, signal.SIGSEGV,
                    signal.SIGTERM):
            # Set and then reset a handler for signals that work on windows.
            # Issue #18396, only for signals without a C-level handler.
            if signal.getsignal(sig) is not None:
                signal.signal(sig, signal.signal(sig, handler))
                checked.add(sig)
        # Issue #18396: Ensure the above loop at least tested *something*
        self.assertTrue(checked)

        with self.assertRaises(ValueError):
            signal.signal(-1, handler)

        with self.assertRaises(ValueError):
            signal.signal(7, handler)


class WakeupFDTests(unittest.TestCase):

    def test_invalid_fd(self):
        fd = support.make_bad_fd()
        self.assertRaises((ValueError, OSError),
                          signal.set_wakeup_fd, fd)

    def test_invalid_socket(self):
        sock = socket.socket()
        fd = sock.fileno()
        sock.close()
        self.assertRaises((ValueError, OSError),
                          signal.set_wakeup_fd, fd)

    def test_set_wakeup_fd_result(self):
        r1, w1 = os.pipe()
        self.addCleanup(os.close, r1)
        self.addCleanup(os.close, w1)
        r2, w2 = os.pipe()
        self.addCleanup(os.close, r2)
        self.addCleanup(os.close, w2)

        if hasattr(os, 'set_blocking'):
            os.set_blocking(w1, False)
            os.set_blocking(w2, False)

        signal.set_wakeup_fd(w1)
        self.assertEqual(signal.set_wakeup_fd(w2), w1)
        self.assertEqual(signal.set_wakeup_fd(-1), w2)
        self.assertEqual(signal.set_wakeup_fd(-1), -1)

    def test_set_wakeup_fd_socket_result(self):
        sock1 = socket.socket()
        self.addCleanup(sock1.close)
        sock1.setblocking(False)
        fd1 = sock1.fileno()

        sock2 = socket.socket()
        self.addCleanup(sock2.close)
        sock2.setblocking(False)
        fd2 = sock2.fileno()

        signal.set_wakeup_fd(fd1)
        self.assertEqual(signal.set_wakeup_fd(fd2), fd1)
        self.assertEqual(signal.set_wakeup_fd(-1), fd2)
        self.assertEqual(signal.set_wakeup_fd(-1), -1)

    # On Windows, files are always blocking and Windows does not provide a
    # function to test if a socket is in non-blocking mode.
    @unittest.skipIf(sys.platform == "win32", "tests specific to POSIX")
    def test_set_wakeup_fd_blocking(self):
        rfd, wfd = os.pipe()
        self.addCleanup(os.close, rfd)
        self.addCleanup(os.close, wfd)

        # fd must be non-blocking
        os.set_blocking(wfd, True)
        with self.assertRaises(ValueError) as cm:
            signal.set_wakeup_fd(wfd)
        self.assertEqual(str(cm.exception),
                         "the fd %s must be in non-blocking mode" % wfd)

        # non-blocking is ok
        os.set_blocking(wfd, False)
        signal.set_wakeup_fd(wfd)
        signal.set_wakeup_fd(-1)


@unittest.skipIf(sys.platform == "win32", "Not valid on Windows")
class WakeupSignalTests(unittest.TestCase):
    @unittest.skipIf(_testcapi is None, 'need _testcapi')
    def check_wakeup(self, test_body, *signals, ordered=True):
        # use a subprocess to have only one thread
        code = """if 1:
        import _testcapi
        import os
        import signal
        import struct

        signals = {!r}

        def handler(signum, frame):
            pass

        def check_signum(signals):
            data = os.read(read, len(signals)+1)
            raised = struct.unpack('%uB' % len(data), data)
            if not {!r}:
                raised = set(raised)
                signals = set(signals)
            if raised != signals:
                raise Exception("%r != %r" % (raised, signals))

        {}

        signal.signal(signal.SIGALRM, handler)
        read, write = os.pipe()
        os.set_blocking(write, False)
        signal.set_wakeup_fd(write)

        test()
        check_signum(signals)

        os.close(read)
        os.close(write)
        """.format(tuple(map(int, signals)), ordered, test_body)

        assert_python_ok('-c', code)

    @unittest.skipIf(_testcapi is None, 'need _testcapi')
    def test_wakeup_write_error(self):
        # Issue #16105: write() errors in the C signal handler should not
        # pass silently.
        # Use a subprocess to have only one thread.
        code = """if 1:
        import _testcapi
        import errno
        import os
        import signal
        import sys
        from test.support import captured_stderr

        def handler(signum, frame):
            1/0

        signal.signal(signal.SIGALRM, handler)
        r, w = os.pipe()
        os.set_blocking(r, False)

        # Set wakeup_fd a read-only file descriptor to trigger the error
        signal.set_wakeup_fd(r)
        try:
            with captured_stderr() as err:
                _testcapi.raise_signal(signal.SIGALRM)
        except ZeroDivisionError:
            # An ignored exception should have been printed out on stderr
            err = err.getvalue()
            if ('Exception ignored when trying to write to the signal wakeup fd'
                not in err):
                raise AssertionError(err)
            if ('OSError: [Errno %d]' % errno.EBADF) not in err:
                raise AssertionError(err)
        else:
            raise AssertionError("ZeroDivisionError not raised")

        os.close(r)
        os.close(w)
        """
        r, w = os.pipe()
        try:
            os.write(r, b'x')
        except OSError:
            pass
        else:
            self.skipTest("OS doesn't report write() error on the read end of a pipe")
        finally:
            os.close(r)
            os.close(w)

        assert_python_ok('-c', code)

    def test_wakeup_fd_early(self):
        self.check_wakeup("""def test():
            import select
            import time

            TIMEOUT_FULL = 10
            TIMEOUT_HALF = 5

            class InterruptSelect(Exception):
                pass

            def handler(signum, frame):
                raise InterruptSelect
            signal.signal(signal.SIGALRM, handler)

            signal.alarm(1)

            # We attempt to get a signal during the sleep,
            # before select is called
            try:
                select.select([], [], [], TIMEOUT_FULL)
            except InterruptSelect:
                pass
            else:
                raise Exception("select() was not interrupted")

            before_time = time.monotonic()
            select.select([read], [], [], TIMEOUT_FULL)
            after_time = time.monotonic()
            dt = after_time - before_time
            if dt >= TIMEOUT_HALF:
                raise Exception("%s >= %s" % (dt, TIMEOUT_HALF))
        """, signal.SIGALRM)

    def test_wakeup_fd_during(self):
        self.check_wakeup("""def test():
            import select
            import time

            TIMEOUT_FULL = 10
            TIMEOUT_HALF = 5

            class InterruptSelect(Exception):
                pass

            def handler(signum, frame):
                raise InterruptSelect
            signal.signal(signal.SIGALRM, handler)

            signal.alarm(1)
            before_time = time.monotonic()
            # We attempt to get a signal during the select call
            try:
                select.select([read], [], [], TIMEOUT_FULL)
            except InterruptSelect:
                pass
            else:
                raise Exception("select() was not interrupted")
            after_time = time.monotonic()
            dt = after_time - before_time
            if dt >= TIMEOUT_HALF:
                raise Exception("%s >= %s" % (dt, TIMEOUT_HALF))
        """, signal.SIGALRM)

    def test_signum(self):
        self.check_wakeup("""def test():
            import _testcapi
            signal.signal(signal.SIGUSR1, handler)
            _testcapi.raise_signal(signal.SIGUSR1)
            _testcapi.raise_signal(signal.SIGALRM)
        """, signal.SIGUSR1, signal.SIGALRM)

    @unittest.skipUnless(hasattr(signal, 'pthread_sigmask'),
                         'need signal.pthread_sigmask()')
    def test_pending(self):
        self.check_wakeup("""def test():
            signum1 = signal.SIGUSR1
            signum2 = signal.SIGUSR2

            signal.signal(signum1, handler)
            signal.signal(signum2, handler)

            signal.pthread_sigmask(signal.SIG_BLOCK, (signum1, signum2))
            _testcapi.raise_signal(signum1)
            _testcapi.raise_signal(signum2)
            # Unblocking the 2 signals calls the C signal handler twice
            signal.pthread_sigmask(signal.SIG_UNBLOCK, (signum1, signum2))
        """,  signal.SIGUSR1, signal.SIGUSR2, ordered=False)


@unittest.skipUnless(hasattr(socket, 'socketpair'), 'need socket.socketpair')
class WakeupSocketSignalTests(unittest.TestCase):

    @unittest.skipIf(_testcapi is None, 'need _testcapi')
    def test_socket(self):
        # use a subprocess to have only one thread
        code = """if 1:
        import signal
        import socket
        import struct
        import _testcapi

        signum = signal.SIGINT
        signals = (signum,)

        def handler(signum, frame):
            pass

        signal.signal(signum, handler)

        read, write = socket.socketpair()
        read.setblocking(False)
        write.setblocking(False)
        signal.set_wakeup_fd(write.fileno())

        _testcapi.raise_signal(signum)

        data = read.recv(1)
        if not data:
            raise Exception("no signum written")
        raised = struct.unpack('B', data)
        if raised != signals:
            raise Exception("%r != %r" % (raised, signals))

        read.close()
        write.close()
        """

        assert_python_ok('-c', code)

    @unittest.skipIf(_testcapi is None, 'need _testcapi')
    def test_send_error(self):
        # Use a subprocess to have only one thread.
        if os.name == 'nt':
            action = 'send'
        else:
            action = 'write'
        code = """if 1:
        import errno
        import signal
        import socket
        import sys
        import time
        import _testcapi
        from test.support import captured_stderr

        signum = signal.SIGINT

        def handler(signum, frame):
            pass

        signal.signal(signum, handler)

        read, write = socket.socketpair()
        read.setblocking(False)
        write.setblocking(False)

        signal.set_wakeup_fd(write.fileno())

        # Close sockets: send() will fail
        read.close()
        write.close()

        with captured_stderr() as err:
            _testcapi.raise_signal(signum)

        err = err.getvalue()
        if ('Exception ignored when trying to {action} to the signal wakeup fd'
            not in err):
            raise AssertionError(err)
        """.format(action=action)
        assert_python_ok('-c', code)


@unittest.skipIf(sys.platform == "win32", "Not valid on Windows")
class SiginterruptTest(unittest.TestCase):

    def readpipe_interrupted(self, interrupt):
        """Perform a read during which a signal will arrive.  Return True if the
        read is interrupted by the signal and raises an exception.  Return False
        if it returns normally.
        """
        # use a subprocess to have only one thread, to have a timeout on the
        # blocking read and to not touch signal handling in this process
        code = """if 1:
            import errno
            import os
            import signal
            import sys

            interrupt = %r
            r, w = os.pipe()

            def handler(signum, frame):
                1 / 0

            signal.signal(signal.SIGALRM, handler)
            if interrupt is not None:
                signal.siginterrupt(signal.SIGALRM, interrupt)

            print("ready")
            sys.stdout.flush()

            # run the test twice
            try:
                for loop in range(2):
                    # send a SIGALRM in a second (during the read)
                    signal.alarm(1)
                    try:
                        # blocking call: read from a pipe without data
                        os.read(r, 1)
                    except ZeroDivisionError:
                        pass
                    else:
                        sys.exit(2)
                sys.exit(3)
            finally:
                os.close(r)
                os.close(w)
        """ % (interrupt,)
        with spawn_python('-c', code) as process:
            try:
                # wait until the child process is loaded and has started
                first_line = process.stdout.readline()

                stdout, stderr = process.communicate(timeout=5.0)
            except subprocess.TimeoutExpired:
                process.kill()
                return False
            else:
                stdout = first_line + stdout
                exitcode = process.wait()
                if exitcode not in (2, 3):
                    raise Exception("Child error (exit code %s): %r"
                                    % (exitcode, stdout))
                return (exitcode == 3)

    def test_without_siginterrupt(self):
        # If a signal handler is installed and siginterrupt is not called
        # at all, when that signal arrives, it interrupts a syscall that's in
        # progress.
        interrupted = self.readpipe_interrupted(None)
        self.assertTrue(interrupted)

    def test_siginterrupt_on(self):
        # If a signal handler is installed and siginterrupt is called with
        # a true value for the second argument, when that signal arrives, it
        # interrupts a syscall that's in progress.
        interrupted = self.readpipe_interrupted(True)
        self.assertTrue(interrupted)

    def test_siginterrupt_off(self):
        # If a signal handler is installed and siginterrupt is called with
        # a false value for the second argument, when that signal arrives, it
        # does not interrupt a syscall that's in progress.
        interrupted = self.readpipe_interrupted(False)
        self.assertFalse(interrupted)


@unittest.skipIf(sys.platform == "win32", "Not valid on Windows")
class ItimerTest(unittest.TestCase):
    def setUp(self):
        self.hndl_called = False
        self.hndl_count = 0
        self.itimer = None
        self.old_alarm = signal.signal(signal.SIGALRM, self.sig_alrm)

    def tearDown(self):
        signal.signal(signal.SIGALRM, self.old_alarm)
        if self.itimer is not None: # test_itimer_exc doesn't change this attr
            # just ensure that itimer is stopped
            signal.setitimer(self.itimer, 0)

    def sig_alrm(self, *args):
        self.hndl_called = True

    def sig_vtalrm(self, *args):
        self.hndl_called = True

        if self.hndl_count > 3:
            # it shouldn't be here, because it should have been disabled.
            raise signal.ItimerError("setitimer didn't disable ITIMER_VIRTUAL "
                "timer.")
        elif self.hndl_count == 3:
            # disable ITIMER_VIRTUAL, this function shouldn't be called anymore
            signal.setitimer(signal.ITIMER_VIRTUAL, 0)

        self.hndl_count += 1

    def sig_prof(self, *args):
        self.hndl_called = True
        signal.setitimer(signal.ITIMER_PROF, 0)

    def test_itimer_exc(self):
        # XXX I'm assuming -1 is an invalid itimer, but maybe some platform
        # defines it ?
        self.assertRaises(signal.ItimerError, signal.setitimer, -1, 0)
        # Negative times are treated as zero on some platforms.
        if 0:
            self.assertRaises(signal.ItimerError,
                              signal.setitimer, signal.ITIMER_REAL, -1)

    def test_itimer_real(self):
        self.itimer = signal.ITIMER_REAL
        signal.setitimer(self.itimer, 1.0)
        signal.pause()
        self.assertEqual(self.hndl_called, True)

    # Issue 3864, unknown if this affects earlier versions of freebsd also
    @unittest.skipIf(sys.platform in ('freebsd6', 'netbsd5'),
        'itimer not reliable (does not mix well with threading) on some BSDs.')
    def test_itimer_virtual(self):
        self.itimer = signal.ITIMER_VIRTUAL
        signal.signal(signal.SIGVTALRM, self.sig_vtalrm)
        signal.setitimer(self.itimer, 0.3, 0.2)

        start_time = time.monotonic()
        while time.monotonic() - start_time < 60.0:
            # use up some virtual time by doing real work
            _ = pow(12345, 67890, 10000019)
            if signal.getitimer(self.itimer) == (0.0, 0.0):
                break # sig_vtalrm handler stopped this itimer
        else: # Issue 8424
            self.skipTest("timeout: likely cause: machine too slow or load too "
                          "high")

        # virtual itimer should be (0.0, 0.0) now
        self.assertEqual(signal.getitimer(self.itimer), (0.0, 0.0))
        # and the handler should have been called
        self.assertEqual(self.hndl_called, True)

    # Issue 3864, unknown if this affects earlier versions of freebsd also
    @unittest.skipIf(sys.platform=='freebsd6',
        'itimer not reliable (does not mix well with threading) on freebsd6')
    def test_itimer_prof(self):
        self.itimer = signal.ITIMER_PROF
        signal.signal(signal.SIGPROF, self.sig_prof)
        signal.setitimer(self.itimer, 0.2, 0.2)

        start_time = time.monotonic()
        while time.monotonic() - start_time < 60.0:
            # do some work
            _ = pow(12345, 67890, 10000019)
            if signal.getitimer(self.itimer) == (0.0, 0.0):
                break # sig_prof handler stopped this itimer
        else: # Issue 8424
            self.skipTest("timeout: likely cause: machine too slow or load too "
                          "high")

        # profiling itimer should be (0.0, 0.0) now
        self.assertEqual(signal.getitimer(self.itimer), (0.0, 0.0))
        # and the handler should have been called
        self.assertEqual(self.hndl_called, True)


class PendingSignalsTests(unittest.TestCase):
    """
    Test pthread_sigmask(), pthread_kill(), sigpending() and sigwait()
    functions.
    """
    @unittest.skipUnless(hasattr(signal, 'sigpending'),
                         'need signal.sigpending()')
    def test_sigpending_empty(self):
        self.assertEqual(signal.sigpending(), set())

    @unittest.skipUnless(hasattr(signal, 'pthread_sigmask'),
                         'need signal.pthread_sigmask()')
    @unittest.skipUnless(hasattr(signal, 'sigpending'),
                         'need signal.sigpending()')
    def test_sigpending(self):
        code = """if 1:
            import os
            import signal

            def handler(signum, frame):
                1/0

            signum = signal.SIGUSR1
            signal.signal(signum, handler)

            signal.pthread_sigmask(signal.SIG_BLOCK, [signum])
            os.kill(os.getpid(), signum)
            pending = signal.sigpending()
            for sig in pending:
                assert isinstance(sig, signal.Signals), repr(pending)
            if pending != {signum}:
                raise Exception('%s != {%s}' % (pending, signum))
            try:
                signal.pthread_sigmask(signal.SIG_UNBLOCK, [signum])
            except ZeroDivisionError:
                pass
            else:
                raise Exception("ZeroDivisionError not raised")
        """
        assert_python_ok('-c', code)

    @unittest.skipUnless(hasattr(signal, 'pthread_kill'),
                         'need signal.pthread_kill()')
    def test_pthread_kill(self):
        code = """if 1:
            import signal
            import threading
            import sys

            signum = signal.SIGUSR1

            def handler(signum, frame):
                1/0

            signal.signal(signum, handler)

            if sys.platform == 'freebsd6':
                # Issue #12392 and #12469: send a signal to the main thread
                # doesn't work before the creation of the first thread on
                # FreeBSD 6
                def noop():
                    pass
                thread = threading.Thread(target=noop)
                thread.start()
                thread.join()

            tid = threading.get_ident()
            try:
                signal.pthread_kill(tid, signum)
            except ZeroDivisionError:
                pass
            else:
                raise Exception("ZeroDivisionError not raised")
        """
        assert_python_ok('-c', code)

    @unittest.skipUnless(hasattr(signal, 'pthread_sigmask'),
                         'need signal.pthread_sigmask()')
    def wait_helper(self, blocked, test):
        """
        test: body of the "def test(signum):" function.
        blocked: number of the blocked signal
        """
        code = '''if 1:
        import signal
        import sys
        from signal import Signals

        def handler(signum, frame):
            1/0

        %s

        blocked = %s
        signum = signal.SIGALRM

        # child: block and wait the signal
        try:
            signal.signal(signum, handler)
            signal.pthread_sigmask(signal.SIG_BLOCK, [blocked])

            # Do the tests
            test(signum)

            # The handler must not be called on unblock
            try:
                signal.pthread_sigmask(signal.SIG_UNBLOCK, [blocked])
            except ZeroDivisionError:
                print("the signal handler has been called",
                      file=sys.stderr)
                sys.exit(1)
        except BaseException as err:
            print("error: {}".format(err), file=sys.stderr)
            sys.stderr.flush()
            sys.exit(1)
        ''' % (test.strip(), blocked)

        # sig*wait* must be called with the signal blocked: since the current
        # process might have several threads running, use a subprocess to have
        # a single thread.
        assert_python_ok('-c', code)

    @unittest.skipUnless(hasattr(signal, 'sigwait'),
                         'need signal.sigwait()')
    def test_sigwait(self):
        self.wait_helper(signal.SIGALRM, '''
        def test(signum):
            signal.alarm(1)
            received = signal.sigwait([signum])
            assert isinstance(received, signal.Signals), received
            if received != signum:
                raise Exception('received %s, not %s' % (received, signum))
        ''')

    @unittest.skipUnless(hasattr(signal, 'sigwaitinfo'),
                         'need signal.sigwaitinfo()')
    def test_sigwaitinfo(self):
        self.wait_helper(signal.SIGALRM, '''
        def test(signum):
            signal.alarm(1)
            info = signal.sigwaitinfo([signum])
            if info.si_signo != signum:
                raise Exception("info.si_signo != %s" % signum)
        ''')

    @unittest.skipUnless(hasattr(signal, 'sigtimedwait'),
                         'need signal.sigtimedwait()')
    def test_sigtimedwait(self):
        self.wait_helper(signal.SIGALRM, '''
        def test(signum):
            signal.alarm(1)
            info = signal.sigtimedwait([signum], 10.1000)
            if info.si_signo != signum:
                raise Exception('info.si_signo != %s' % signum)
        ''')

    @unittest.skipUnless(hasattr(signal, 'sigtimedwait'),
                         'need signal.sigtimedwait()')
    def test_sigtimedwait_poll(self):
        # check that polling with sigtimedwait works
        self.wait_helper(signal.SIGALRM, '''
        def test(signum):
            import os
            os.kill(os.getpid(), signum)
            info = signal.sigtimedwait([signum], 0)
            if info.si_signo != signum:
                raise Exception('info.si_signo != %s' % signum)
        ''')

    @unittest.skipUnless(hasattr(signal, 'sigtimedwait'),
                         'need signal.sigtimedwait()')
    def test_sigtimedwait_timeout(self):
        self.wait_helper(signal.SIGALRM, '''
        def test(signum):
            received = signal.sigtimedwait([signum], 1.0)
            if received is not None:
                raise Exception("received=%r" % (received,))
        ''')

    @unittest.skipUnless(hasattr(signal, 'sigtimedwait'),
                         'need signal.sigtimedwait()')
    def test_sigtimedwait_negative_timeout(self):
        signum = signal.SIGALRM
        self.assertRaises(ValueError, signal.sigtimedwait, [signum], -1.0)

    @unittest.skipUnless(hasattr(signal, 'sigwait'),
                         'need signal.sigwait()')
    @unittest.skipUnless(hasattr(signal, 'pthread_sigmask'),
                         'need signal.pthread_sigmask()')
    @unittest.skipIf(threading is None, "test needs threading module")
    def test_sigwait_thread(self):
        # Check that calling sigwait() from a thread doesn't suspend the whole
        # process. A new interpreter is spawned to avoid problems when mixing
        # threads and fork(): only async-safe functions are allowed between
        # fork() and exec().
        assert_python_ok("-c", """if True:
            import os, threading, sys, time, signal

            # the default handler terminates the process
            signum = signal.SIGUSR1

            def kill_later():
                # wait until the main thread is waiting in sigwait()
                time.sleep(1)
                os.kill(os.getpid(), signum)

            # the signal must be blocked by all the threads
            signal.pthread_sigmask(signal.SIG_BLOCK, [signum])
            killer = threading.Thread(target=kill_later)
            killer.start()
            received = signal.sigwait([signum])
            if received != signum:
                print("sigwait() received %s, not %s" % (received, signum),
                      file=sys.stderr)
                sys.exit(1)
            killer.join()
            # unblock the signal, which should have been cleared by sigwait()
            signal.pthread_sigmask(signal.SIG_UNBLOCK, [signum])
        """)

    @unittest.skipUnless(hasattr(signal, 'pthread_sigmask'),
                         'need signal.pthread_sigmask()')
    def test_pthread_sigmask_arguments(self):
        self.assertRaises(TypeError, signal.pthread_sigmask)
        self.assertRaises(TypeError, signal.pthread_sigmask, 1)
        self.assertRaises(TypeError, signal.pthread_sigmask, 1, 2, 3)
        self.assertRaises(OSError, signal.pthread_sigmask, 1700, [])

    @unittest.skipUnless(hasattr(signal, 'pthread_sigmask'),
                         'need signal.pthread_sigmask()')
    def test_pthread_sigmask(self):
        code = """if 1:
        import signal
        import os; import threading

        def handler(signum, frame):
            1/0

        def kill(signum):
            os.kill(os.getpid(), signum)

        def check_mask(mask):
            for sig in mask:
                assert isinstance(sig, signal.Signals), repr(sig)

        def read_sigmask():
            sigmask = signal.pthread_sigmask(signal.SIG_BLOCK, [])
            check_mask(sigmask)
            return sigmask

        signum = signal.SIGUSR1

        # Install our signal handler
        old_handler = signal.signal(signum, handler)

        # Unblock SIGUSR1 (and copy the old mask) to test our signal handler
        old_mask = signal.pthread_sigmask(signal.SIG_UNBLOCK, [signum])
        check_mask(old_mask)
        try:
            kill(signum)
        except ZeroDivisionError:
            pass
        else:
            raise Exception("ZeroDivisionError not raised")

        # Block and then raise SIGUSR1. The signal is blocked: the signal
        # handler is not called, and the signal is now pending
        mask = signal.pthread_sigmask(signal.SIG_BLOCK, [signum])
        check_mask(mask)
        kill(signum)

        # Check the new mask
        blocked = read_sigmask()
        check_mask(blocked)
        if signum not in blocked:
            raise Exception("%s not in %s" % (signum, blocked))
        if old_mask ^ blocked != {signum}:
            raise Exception("%s ^ %s != {%s}" % (old_mask, blocked, signum))

        # Unblock SIGUSR1
        try:
            # unblock the pending signal calls immediately the signal handler
            signal.pthread_sigmask(signal.SIG_UNBLOCK, [signum])
        except ZeroDivisionError:
            pass
        else:
            raise Exception("ZeroDivisionError not raised")
        try:
            kill(signum)
        except ZeroDivisionError:
            pass
        else:
            raise Exception("ZeroDivisionError not raised")

        # Check the new mask
        unblocked = read_sigmask()
        if signum in unblocked:
            raise Exception("%s in %s" % (signum, unblocked))
        if blocked ^ unblocked != {signum}:
            raise Exception("%s ^ %s != {%s}" % (blocked, unblocked, signum))
        if old_mask != unblocked:
            raise Exception("%s != %s" % (old_mask, unblocked))
        """
        assert_python_ok('-c', code)

    @unittest.skipIf(sys.platform == 'freebsd6',
        "issue #12392: send a signal to the main thread doesn't work "
        "before the creation of the first thread on FreeBSD 6")
    @unittest.skipUnless(hasattr(signal, 'pthread_kill'),
                         'need signal.pthread_kill()')
    def test_pthread_kill_main_thread(self):
        # Test that a signal can be sent to the main thread with pthread_kill()
        # before any other thread has been created (see issue #12392).
        code = """if True:
            import threading
            import signal
            import sys

            def handler(signum, frame):
                sys.exit(3)

            signal.signal(signal.SIGUSR1, handler)
            signal.pthread_kill(threading.get_ident(), signal.SIGUSR1)
            sys.exit(2)
        """

        with spawn_python('-c', code) as process:
            stdout, stderr = process.communicate()
            exitcode = process.wait()
            if exitcode != 3:
                raise Exception("Child error (exit code %s): %s" %
                                (exitcode, stdout))


def tearDownModule():
    support.reap_children()

if __name__ == "__main__":
    unittest.main()
