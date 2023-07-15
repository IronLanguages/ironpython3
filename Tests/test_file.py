# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import os
import sys
import unittest
import _thread

CP16623_LOCK = _thread.allocate_lock()

from iptest import IronPythonTestCase, is_cli, is_cpython, is_netcoreapp, is_posix, run_test, skipUnlessIronPython

class FileTest(IronPythonTestCase):

    def setUp(self):
        super(FileTest, self).setUp()

        self.temp_file = os.path.join(self.temporary_dir, "temp_%d.dat" % os.getpid())
        self.rng_seed = 0

        # The set of read modes we wish to test. Each tuple consists of a human readable
        # name for the mode followed by the corresponding mode string that will be
        # passed to the file constructor.
        self.read_modes = (("binary", "rb"), ("text", "r"), ("universal", "rU"))

        # Same deal as above but for write modes. Note that writing doesn't support a
        # universal newline mode.
        self.write_modes = (("binary", "wb"), ("text", "w"))

    def tearDown(self):
        self.delete_files(self.temp_file)
        return super().tearDown()

    # This module tests operations on the builtin file object. It is not yet complete, the tests cover read(),
    # read(size), readline() and write() for binary, text and universal newline modes.
    def test_sanity(self):
        onlyread_tmp = os.path.join(self.temporary_dir, "onlyread_%d.tmp" % os.getpid())
        onlywrite_tmp = os.path.join(self.temporary_dir, "onlywrite_%d.tmp" % os.getpid())
        for i in range(5):
            ### general file robustness tests
            f = open(onlyread_tmp, "w")
            f.write("will only be read")
            f.flush()
            f.close()
            sin = open(onlyread_tmp, "r")
            sout = open(onlywrite_tmp, "w")

            # writer is null for sin
            self.assertRaises(IOError, sin.write, "abc")
            self.assertRaises(IOError, sin.writelines, ["abc","def"])

            # reader is null for sout
            self.assertRaises(IOError, sout.read)
            self.assertRaises(IOError, sout.read, 10)
            self.assertRaises(IOError, sout.readline)
            self.assertRaises(IOError, sout.readline, 10)
            self.assertRaises(IOError, sout.readlines)
            self.assertRaises(IOError, sout.readlines, 10)

            sin.close()
            sout.close()

            # now close a file and try to perform other I/O operations on it...
            # should throw ValueError according to docs
            f = open(onlywrite_tmp, "w")
            f.close()
            f.close()
            self.assertRaises(ValueError, f.__iter__)
            self.assertRaises(ValueError, f.flush)
            self.assertRaises(ValueError, f.fileno)
            self.assertRaises(ValueError, f.__next__)
            self.assertRaises(ValueError, f.read)
            self.assertRaises(ValueError, f.read, 10)
            self.assertRaises(ValueError, f.readline)
            self.assertRaises(ValueError, f.readline, 10)
            self.assertRaises(ValueError, f.readlines)
            self.assertRaises(ValueError, f.readlines, 10)
            self.assertRaises(ValueError, f.seek, 10)
            self.assertRaises(ValueError, f.seek, 10, 10)
            self.assertRaises(ValueError, f.write, "abc")
            self.assertRaises(ValueError, f.writelines, ["abc","def"])

        os.unlink(onlyread_tmp)
        os.unlink(onlywrite_tmp)

    # Test binary reading and writing fidelity using a round trip method. First
    # construct some pseudo random binary data in a string (making it long enough
    # that it's likely we'd show up any problems with the data being passed through
    # a character encoding/decoding scheme). Then write this data to disk (in binary
    # mode), read it back again (in binary) and check that no changes have occured.

    # Construct the binary data. We want the test to be repeatable so seed the
    # random number generator with a fixed value. Use a simple linear congruential
    # method to generate the random byte values.

    def test_read_write_fidelity(self):
        def randbyte():
            self.rng_seed = (1664525 * self.rng_seed) + 1013904223
            return (self.rng_seed >> 8) & 0xff

        data = ""
        for i in range(10 * 1024):
            data += chr(randbyte())
        data = data.encode("latin")

        # Keep a copy of the data safe.
        orig_data = data

        # Write the data to disk in binary mode.
        with open(self.temp_file, "wb") as f:
            f.write(data)

        # And read it back in again.
        with open(self.temp_file, "rb") as f:
            data = f.read()

        # Check nothing changed.
        self.assertTrue(data == orig_data)

    def test_cp10983(self):
        # writing non-unicode characters > 127 should be preserved
        with open(self.temp_file, 'w') as x:
            x.write('\xa33')

        with open(self.temp_file) as x:
            data = x.read()

        self.assertEqual(ord(data[0]), 163)
        self.assertEqual(ord(data[1]), 51)

        with open(self.temp_file, 'w', encoding="latin") as x:
            x.write("a2\xa33\\u0163\x0F\x0FF\t\\\x0FF\x0FE\x00\x01\x7F\x7E\x80")

        with open(self.temp_file, encoding="latin") as x:
            data = x.read()

        self.assertEqual(data, 'a2\xa33\\u0163\x0f\x0fF\t\\\x0fF\x0fE\x00\x01\x7F\x7E\x80')

    @skipUnlessIronPython()
    def test_cp27179(self):
        # write() accepting Array[Byte]
        from System import Array, Byte
        data_string = 'abcdef\nghijkl\n\n'
        data = Array[Byte](list(map(Byte, list(map(ord, data_string)))))

        with open(self.temp_file, 'wb+') as f:
            f.write(data)

        with open(self.temp_file, 'r') as f:
            data_read = f.read()

        self.assertEqual(data_string, data_read)

    # Helper used to format newline characters into a visible format.
    def format_newlines(self, string):
        out = ""
        for char in string:
            if char == '\r':
                out += "\\r"
            elif char == '\n':
                out += "\\n"
            else:
                out += char
        return out

    # The following is the setup for a set of pattern mode tests that will check
    # some tricky edge cases for newline translation for both reading and writing.
    # The entry point is the test_patterns() function.
    @unittest.skipIf(is_posix, "posix doesn't do any newline translation if b is specified, so this test is invalid")
    def test_newlines(self):

        # Read mode test cases. Each tuple has three values; the raw on-disk value we
        # start with (which also doubles as the value we should get back when we read in
        # binary mode) then the value we expect to get when reading in text mode and
        # finally the value we expect to get in universal newline mode.
        read_patterns = ((b"\r", "\n", "\n"),
                        (b"\n", "\n", "\n"),
                        (b"\r\n", "\n", "\n"),
                        (b"\n\r", "\n\n", "\n\n"),
                        (b"\r\r", "\n\n", "\n\n"),
                        (b"\n\n", "\n\n", "\n\n"),
                        (b"\r\n\r\n", "\n\n", "\n\n"),
                        (b"\n\r\n\r", "\n\n\n", "\n\n\n"),
                        (b"The quick brown fox", "The quick brown fox", "The quick brown fox"),
                        (b"The \rquick\n brown fox\r\n", "The \nquick\n brown fox\n", "The \nquick\n brown fox\n"),
                        (b"The \r\rquick\r\n\r\n brown fox", "The \n\nquick\n\n brown fox", "The \n\nquick\n\n brown fox"))

        # Write mode test cases. Same deal as above but with one less member in each
        # tuple due to the lack of a universal newline write mode. The first value
        # represents the in-memory value we start with (and expect to write in binary
        # write mode) and the next value indicates the value we expect to end up on disk
        # in text mode.
        write_patterns = ((b"\r", b"\r"),
                        (b"\n", b"\r\n"),
                        (b"\r\n", b"\r\r\n"),
                        (b"\n\r", b"\r\n\r"),
                        (b"\r\r", b"\r\r"),
                        (b"\n\n", b"\r\n\r\n"),
                        (b"\r\n\r\n", b"\r\r\n\r\r\n"),
                        (b"\n\r\n\r", b"\r\n\r\r\n\r"),
                        (b"The quick brown fox", b"The quick brown fox"),
                        (b"The \rquick\n brown fox\r\n", b"The \rquick\r\n brown fox\r\r\n"),
                        (b"The \r\rquick\r\n\r\n brown fox", b"The \r\rquick\r\r\n\r\r\n brown fox"))

        # Test a specific read mode pattern.
        def test_read_pattern(pattern):
            # Write the initial data to disk using binary mode (we test this
            # functionality earlier so we're satisfied it gets there unaltered).
            with open(self.temp_file, "wb") as f:
                f.write(pattern[0])

            # Read the data back in each read mode, checking that we get the correct
            # transform each time.
            for mode in range(3):
                test_read_mode(pattern, mode)

        # Test a specific read mode pattern for a given reading mode.
        def test_read_mode(pattern, mode):
            # Read the data back from disk using the given read mode.
            with open(self.temp_file, self.read_modes[mode][1]) as f:
                contents = f.read()

            # Check it equals what we expected for this mode.
            self.assertEqual(contents, pattern[mode])

        # Test a specific write mode pattern.
        def test_write_pattern(pattern):
            for mode in range(2):
                test_write_mode(pattern, mode)

        # Test a specific write mode pattern for a given write mode.
        def test_write_mode(pattern, mode):
            # Write the raw data using the given mode.
            with open(self.temp_file, self.write_modes[mode][1]) as f:
                if self.write_modes[mode][0] == "binary":
                    f.write(pattern[0])
                else:
                    f.write(pattern[0].decode("ascii"))

            # Read the data back in using binary mode (we tested this gets us back
            # unaltered data earlier).
            with open(self.temp_file, "rb") as f:
                contents = f.read()

            # Check it equals what we expected for this mode.
            self.assertEqual(contents, pattern[mode])

        # Run through the read and write mode tests for all patterns.
        def test_patterns():
            for pattern in read_patterns:
                test_read_pattern(pattern)
            for pattern in write_patterns:
                test_write_pattern(pattern)

        # Actually run the pattern mode tests.
        test_patterns()

    # Now some tests of read(size).
    # Test data is in the following format: ("raw data", read_size, (binary mode result strings) (binary mode result tell() result)
    #                                                               (text mode result strings) (text mode result tell() result)
    #                                                               (universal mode result strings) (universal mode result tell() results)

    def test_read_size(self):
        read_size_tests = [(b"Hello", 1, (b"H", b"e", b"l", b"l", b"o"), (1,2,3,4,5),
                                        ("H", "e", "l", "l", "o"), (1,2,3,4,5),
                                        ("H", "e", "l", "l", "o"), (1,2,3,4,5)),
                        (b"Hello", 2, (b"He", b"ll", b"o"), (2,4,5),
                                        ("He", "ll", "o"), (2,4,5),
                                        ("He", "ll", "o"), (2,4,5))]

        read_size_tests.append((b"H\re\n\r\nllo", 1, (b"H", b"\r", b"e", b"\n", b"\r", b"\n", b"l", b"l", b"o"), (1, 2, 3, 4, 5, 6, 7, 8, 9),
                                            ("H", "\n", "e", "\n", "\n", "l", "l", "o"), (1, 2, 3, 4, 6, 7, 8, 9),
                                            ("H", "\n", "e", "\n", "\n", "l", "l", "o"), (1, 2, 3, 4, 6, 7, 8, 9)))
        read_size_tests.append((b"H\re\n\r\nllo", 2, (b"H\r", b"e\n", b"\r\n", b"ll", b"o"), (2, 4, 6, 8, 9),
                                            ("H\n", "e\n", "\nl", "lo"), (2, 4, 7, 9),
                                            ("H\n", "e\n", "\nl", "lo"), (2, 4, 7, 9)))

        for test in read_size_tests:
            # Write the test pattern to disk in binary mode.
            with open(self.temp_file, "wb") as f:
                f.write(test[0])

            # Read the data back in each of the read modes we test.
            for mode in range(3):
                with open(self.temp_file, self.read_modes[mode][1]) as f:
                    self.assertFalse(f.closed)

                    # We read the data in the size specified by the test and expect to get
                    # the set of strings given for this specific mode.
                    size = test[1]
                    strings = test[2 + mode*2]
                    lengths = test[3 + mode*2]
                    count = 0
                    while True:
                        data = f.read(size)
                        if not data:
                            self.assertTrue(count == len(strings))
                            break
                        count = count + 1
                        self.assertTrue(count <= len(strings))
                        self.assertEqual(data, strings[count - 1])
                        t = f.tell()
                        # looks like a bug in CPython?
                        self.assertEqual(2 if is_cpython and t == 340282367000166625996085689099021713410 else t, lengths[count-1])
                    f.close()
                    self.assertTrue(f.closed)

    # And some readline tests.
    # Test data is in the following format: ("raw data", (binary mode result strings)
    #                                                    (text mode result strings)
    #                                                    (universal mode result strings))
    def test_readline(self):
        readline_tests = [(b"Mary had a little lamb", (b"Mary had a little lamb", ),
                                                    ("Mary had a little lamb", ),
                                                    ("Mary had a little lamb", )),
                        (b"Mary had a little lamb\r", (b"Mary had a little lamb\r", ),
                                                    ("Mary had a little lamb\n", ),
                                                    ("Mary had a little lamb\n", )),
                        (b"Mary had a \rlittle lamb\r", (b"Mary had a \rlittle lamb\r", ),
                                                        ("Mary had a \n", "little lamb\n", ),
                                                        ("Mary had a \n", "little lamb\n"))]

        # wb doesn't change the output to just \n like on Windows (binary mode means nothing on POSIX)
        readline_tests.append((b"Mary \r\nhad \na little lamb", (b"Mary \r\n", b"had \n", b"a little lamb"),
                                                    ("Mary \n", "had \n", "a little lamb"),
                                                    ("Mary \n", "had \n", "a little lamb")))

        for test in readline_tests:
            # Write the test pattern to disk in binary mode.
            with open(self.temp_file, "wb") as f:
                f.write(test[0])

            # Read the data back in each of the read modes we test.
            for mode in range(3):
                with open(self.temp_file, self.read_modes[mode][1]) as f:
                    # We read the data by line and expect to get a specific sets of lines back.
                    strings = test[1 + mode]
                    count = 0
                    while True:
                        data = f.readline()
                        if not data:
                            self.assertEqual(count, len(strings))
                            break
                        count = count + 1

                        self.assertTrue(count <= len(strings))
                        self.assertEqual(data, strings[count - 1])

    def format_tuple(self, tup):
        if tup == None:
            return "None"
        if (isinstance(tup, str)):
            return format_newlines(tup)
        out = "("
        for entry in tup:
            out += format_newlines(entry) + ", "
        out += ")"
        return out

    # Test the 'newlines' attribute.
    # Format of the test data is the raw data written to the test file followed by a tuple representing the values
    # of newlines expected after each line is read from the file in universal newline mode.
    def test_newlines_attribute(self):
        newlines_tests = ((b"123", (None, )),
                        (b"1\r\n2\r3\n", (('\r', '\n', '\r\n'), ('\r', '\n', '\r\n'), ('\r', '\n', '\r\n'))),
                        (b"1\r2\n3\r\n", (('\r', '\n', '\r\n'), ('\r', '\n', '\r\n'), ('\r', '\n', '\r\n'))),
                        (b"1\n2\r\n3\r", (('\n', '\r\n'), ('\n', '\r\n'), ('\r', '\n', '\r\n'))),
                        (b"1\r\n2\r\n3\r\n", ("\r\n", "\r\n", "\r\n")),
                        (b"1\r2\r3\r", ("\r", "\r", "\r")),
                        (b"1\n2\n3\n", ("\n", "\n", "\n")))

        for test in newlines_tests:
            # Write the test pattern to disk in binary mode.
            with open(self.temp_file, "wb") as f:
                f.write(test[0])
                # Verify newlines isn't set while writing.
                self.assertFalse(hasattr(f, "newlines"))

            # Verify that reading the file sets newlines.
            with open(self.temp_file, "r") as f:
                data = f.read()
                self.assertEqual(f.newlines, test[1][-1])

            # Read file in universal mode line by line and verify we see the expected output at each stage.
            expected = test[1]
            with open(self.temp_file, "rU") as f:
                self.assertTrue(f.newlines is None)
                count = 0
                while True:
                    data = f.readline()
                    if not data:
                        break
                    self.assertTrue(count < len(expected))
                    self.assertEqual(f.newlines, expected[count])
                    count = count + 1

    ## coverage: a sequence of file operation
    @unittest.skipIf(is_posix, 'file sequence specific to windows (because of newlines)')
    def test_coverage(self):
        with open(self.temp_file, 'w') as f:
            self.assertTrue(str(f).startswith(("<_io.TextIOWrapper name=%r mode='w'") % self.temp_file), str(f))
            self.assertFalse(f.closed)
            self.assertTrue(f.fileno() != -1)
            self.assertTrue(f.fileno() != 0)

            # write
            self.assertRaises(TypeError, f.writelines, [3])
            f.writelines(["firstline\n"])

            f.close()
            self.assertTrue(str(f).startswith(("<_io.TextIOWrapper name=%r mode='w'") % self.temp_file))
            self.assertTrue(f.closed)

        # append
        with open(self.temp_file, 'a+') as f:
            f.writelines(['\n', 'secondline\n'])

            pos = len('secondline\n') + 1
            f.seek(f.tell() - pos, 0)

            f.writelines(['thirdline\n'])

        # read
        with open(self.temp_file, 'r+', 512) as f:
            f.seek(0, 2)
            f.seek(f.tell() - pos - 2, 0)
            self.assertEqual(f.readline(), 'e\n')
            self.assertEqual(f.readline(5), 'third')
            self.assertEqual(f.read(-1), 'line\n')
            self.assertEqual(f.read(-1), '')

        # read
        with open(self.temp_file, 'rb', 512) as f:
            f.seek(0, 2)
            f.seek(f.tell() - pos - 2, 0)
            self.assertEqual(f.readline(), b'e\r\n')
            self.assertEqual(f.readline(5), b'third')
            self.assertEqual(f.read(-1), b'line\r\n')
            self.assertEqual(f.read(-1), b'')

        ## file op in os
        os.unlink(self.temp_file)

        fd = os.open(self.temp_file, os.O_CREAT | os.O_WRONLY)
        os.write(fd, b"hello ")
        os.close(fd)

        fd = os.open(self.temp_file, os.O_APPEND | os.O_WRONLY)
        os.write(fd, b"world")
        os.close(fd)

        fd = os.open(self.temp_file, 0)
        self.assertEqual(os.read(fd, 1024), b"hello world")
        os.close(fd)

        os.unlink(self.temp_file)

    def test_encoding(self):
        import locale
        with open(self.temp_file, 'w') as f:
            # succeeds or fails depending on the locale encoding
            try:
                '\u6211'.encode(locale.getpreferredencoding())
            except UnicodeEncodeError:
                with self.assertRaises(UnicodeEncodeError):
                    f.write('\u6211')
            else:
                f.write('\u6211')

    @skipUnlessIronPython()
    def test_net_stream(self):
        import System
        fs = System.IO.FileStream(self.temp_file, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.ReadWrite)
        with open(fs) as f:
            f.write(b'hello\rworld\ngoodbye\r\n')

        with open(self.temp_file, 'rb') as f:
            self.assertEqual(f.read(), b'hello\rworld\ngoodbye\r\n')

        with open(self.temp_file, 'rU') as f:
            self.assertEqual(f.read(), 'hello\nworld\ngoodbye\n')

    def test_file_manager(self):
        import gc

        def return_fd1():
            f = open(self.temp_file, 'w')
            return f.fileno()

        def return_fd2():
            return os.open(self.temp_file, 0)

        fd = return_fd1()
        gc.collect()
        self.assertRaises(OSError, os.fdopen, fd)

        fd = return_fd2()
        gc.collect()
        f = os.fdopen(fd)
        f.close()
        self.assertRaises(OSError, os.fdopen, fd)

    def test_file_manager_leak(self):
        # the number of iterations should be larger than Microsoft.Scripting.Utils.HybridMapping.SIZE (currently 4K)
        N = 5000
        for i in range(N):
            fd = os.open(self.temp_file, os.O_WRONLY | os.O_CREAT)
            f = os.fdopen(fd, 'w', closefd=True)
            f.close()

    def test_sharing(self):
        modes = ['w', 'w+', 'a+', 'r', 'w']
        fname = self.temp_file
        for xx in modes:
            for yy in modes:
                x = open(fname, xx)
                y = open(fname, yy)

                x.close()
                y.close()

        os.unlink(fname)

    def test_overwrite_readonly(self):
        filename = self.temp_file
        f = open(filename, "w+")
        f.write("I am read-only")
        f.close()
        os.chmod(filename, 256)
        try:
            try:
                f = open(filename, "w+") # FAIL
            finally:
                os.chmod(filename, 128)
                os.unlink(filename)
        except IOError as e:
            pass
        else:
            self.fail("Unreachable code reached") # should throw
        #any other exceptions fail

    # file newline handling test
    @unittest.skipIf(is_posix, "this test doesn't really make sense for posix since b doesn't change the behavior")
    def test_newline(self):
        fname = self.temp_file

        def test_newline(norm, mode):
            f = open(fname, mode)
            self.assertTrue(f.read() == norm)
            for x in range(len(norm)):
                f.seek(0)
                a = f.read(x)
                b = f.read(1)
                c = f.read()
                self.assertTrue(a+b+c == norm)
            f.close()

        self.assertRaises(TypeError, open, None) # arg must be string
        self.assertRaises(TypeError, open, [])

        norm   = "Hi\nHello\nHey\nBye\nAhoy\n"
        unnorm = b"Hi\r\nHello\r\nHey\r\nBye\r\nAhoy\r\n"
        f = open(fname, "wb")
        f.write(unnorm)
        f.close()

        test_newline(norm, "r")
        test_newline(unnorm, "rb")

        os.unlink(fname)

    def test_truncate(self):
        # truncate()
        fname = self.temp_file
        with open(fname, 'w') as a:
            a.write('hello world\n')
            a.truncate()
            a.truncate(None) # same as undefined

        with open(fname, 'r') as a:
            self.assertEqual(a.readlines(), ['hello world\n'])

        os.unlink(fname)

        # truncate(#)
        with open(fname, 'w') as a:
            a.write('hello\nworld\n')
            a.truncate(6)

        with open(fname, 'r') as a:
            self.assertEqual(a.readlines(), ['hello\n'])

        os.unlink(fname)

        # truncate(#) invalid args
        with open(fname, 'w') as a:
            self.assertRaises(ValueError if is_cli else OSError, a.truncate, -1)

        # read-only file
        with open(fname, 'r') as a:
            self.assertRaises(ValueError, a.truncate)
            self.assertRaises(ValueError, a.truncate, 0)
        os.unlink(fname)

        # std-out
        self.assertRaises(IOError, sys.stdout.truncate)

    def test_modes(self):
        """test various strange mode combinations and error reporting"""
        fname = self.temp_file
        with open(fname, 'w') as x:
            self.assertEqual(x.mode, 'w')
        # don't allow empty modes
        self.assertRaisesMessage(ValueError, "Must have exactly one of create/read/write/append mode and at most one plus", open, 'abc', '')

        # mode must start with valid value
        self.assertRaisesMessage(ValueError, "invalid mode: 'p'", open, 'abc', 'p')

        # allow anything w/ U but r and w
        err_msg = "mode U cannot be combined with 'x', 'w', 'a', or '+'" if is_cli or sys.version_info >= (3,7) else "mode U cannot be combined with x', 'w', 'a', or '+'" if sys.version_info >= (3,6) else "can't use U and writing mode at once"
        self.assertRaisesMessage(ValueError, err_msg, open, 'abc', 'Uw')
        self.assertRaisesMessage(ValueError, err_msg, open, 'abc', 'Ua')
        self.assertRaisesMessage(ValueError, err_msg, open, 'abc', 'Uw+')
        self.assertRaisesMessage(ValueError, err_msg, open, 'abc', 'Ua+')

        # check invalid modes
        self.assertRaises(ValueError, open, fname, 'pU')
        self.assertRaises(ValueError, open, fname, 'pU+')
        self.assertRaises(ValueError, open, fname, 'rFOOBAR')

    def test_cp16623(self):
        '''
        If this test ever fails randomly, there is a problem around file thread
        safety.  Do not wrap this test case with retry_on_failure!
        '''
        global FINISHED_COUNTER
        FINISHED_COUNTER = 0

        import time

        expected_lines = ["a", "bbb" * 100, "cc"]
        total_threads = 50
        file_name = self.temp_file
        with open(file_name, "w") as f:

            def write_stuff():
                global FINISHED_COUNTER
                global CP16623_LOCK
                for j in range(100):
                    for i in range(50):
                        print("a", file=f)
                    print("bbb" * 1000, file=f)
                    for i in range(10):
                        print("cc", file=f)
                with CP16623_LOCK:
                    FINISHED_COUNTER += 1

            for i in range(total_threads):
                _thread.start_new_thread(write_stuff, ())

            #Give all threads some time to finish
            for i in range(total_threads):
                if FINISHED_COUNTER!=total_threads:
                    print("*", end=' ')
                    time.sleep(1)
                else:
                    break
            self.assertEqual(FINISHED_COUNTER, total_threads)
        #Verifications - since print isn't threadsafe the following
        #is pointless...  Just make sure IP doesn't throw.
        #f = open(file_name, "r")
        #lines = f.readlines()
        #for line in lines:
        #    self.assertTrue(line in expected_lines, line)

    def test_write_memoryview(self):
        with open(self.temp_file, 'wb+') as foo:
            b = memoryview(b'hello world')[6:]
            foo.write(b)
        with open(self.temp_file, 'r') as foo:
            self.assertEqual(foo.readlines(), ['world'])

        with open(self.temp_file, 'w+b') as foo:
            b = memoryview(b'hello world')[6:]
            foo.write(b)

        with open(self.temp_file, 'r') as foo:
            self.assertEqual(foo.readlines(), ['world'])

    def test_errors(self):
        with self.assertRaises(OSError) as cm:
            open('some_file_that_really_does_not_exist')
        self.assertEqual(cm.exception.errno, 2)

        with self.assertRaises(OSError) as cm:
            open('path_too_long' * 100)
        self.assertEqual(cm.exception.errno, (36 if is_posix else 22) if is_netcoreapp and not is_posix or sys.version_info >= (3,6) else 2)

    def test_write_bytes(self):
        fname = self.temp_file
        f = open(fname, "wb+")
        try:
            f.write(b"Hello\n")
            f.close()
            f = open(fname)
            self.assertEqual(f.readlines(), ['Hello\n'])
            f.close()
        finally:
            f.close()
            os.unlink(fname)

    def test_kw_args(self):
        open(file=self.temp_file, mode ='w').close()

    def test_buffering_kwparam(self):
        #--Positive
        fname = self.temp_file
        for x in [-2147483648, -1, 1, 2, 1024, 2147483646, 2147483647]:
            f = open(file=fname, mode='w', buffering=x)
            f.close()
            os.unlink(fname)

        with self.assertRaises(ValueError): # can't have unbuffered text I/O
            open(file=fname, mode='w', buffering=0)

        self.assertRaisesMessage(TypeError, "expected Int32, got float" if is_cli else "integer argument expected, got float",
                                 open, fname, 'w', 3.14)

        #--Negative
        for x in [None, "abc", "", [], tuple()]:
            self.assertRaises(TypeError,  #"an integer is required",
                              lambda: open(file=fname, mode='w', buffering=x))

        for x in [2147483648, -2147483649]:
            self.assertRaises(OverflowError,  #"long int too large to convert to int",
                              lambda: open(file=fname, mode='w', buffering=x))

    def test_open_with_BOM(self):
        """https://github.com/IronLanguages/main/issues/1088"""
        fileName = os.path.join(self.test_dir, "file_without_BOM.txt")
        with open(fileName, "r", encoding="latin") as f:
            self.assertEqual(f.read(), "\x42\xc3\x93\x4d\x0a")
        with open(fileName, "rb") as f:
            self.assertEqual(f.read(), b"\x42\xc3\x93\x4d\x0d\x0a")

        fileName = os.path.join(self.test_dir, "file_with_BOM.txt")
        with open(fileName, "r", encoding="latin") as f:
            self.assertEqual(f.read(), "\xef\xbb\xbf\x42\xc3\x93\x4d\x0a")
        with open(fileName, "rb") as f:
            self.assertEqual(f.read(), b"\xef\xbb\xbf\x42\xc3\x93\x4d\x0d\x0a")

    def test_opener(self):
        data = "test message\n"
        with open(self.temp_file, "w", opener=os.open) as f:
            f.write(data)

        with open(self.temp_file, "r", opener=os.open) as f:
            self.assertEqual(f.read(), data)

        os.unlink(self.temp_file)

    def test_opener_negative_fd(self):
        def negative_opener(path, flags):
            return -1

        self.assertRaises(ValueError if is_cli or sys.version_info >= (3,5) else SystemError, open, "", "r", opener=negative_opener)

    def test_opener_none_fd(self):
        def none_opener(path, flags):
            return None

        self.assertRaises(TypeError, open, "", "r", opener=none_opener)

    def test_opener_uncallable(self):
        uncallable_opener = "uncallable_opener"

        self.assertRaises(TypeError, open, "", "r", opener=uncallable_opener)

    def test_open_abplus(self):
        with open(self.temp_file, "ab+") as f:
            f.write(b"abc")
            f.seek(0)
            self.assertEqual(f.read(), b"abc")

run_test(__name__)
