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

import os
import sys
import unittest
import _thread

CP16623_LOCK = _thread.allocate_lock()

from iptest import IronPythonTestCase, is_cli, is_cpython, is_netstandard, is_posix, run_test

class FileTest(IronPythonTestCase):

    def setUp(self):
        super(FileTest, self).setUp()

        self.temp_file = os.path.join(self.temporary_dir, "temp.dat")
        self.rng_seed = 0

        # The set of read modes we wish to test. Each tuple consists of a human readable
        # name for the mode followed by the corresponding mode string that will be
        # passed to the file constructor.
        self.read_modes = (("binary", "rb"), ("text", "r"), ("universal", "rU"))

        # Same deal as above but for write modes. Note that writing doesn't support a
        # universal newline mode.
        self.write_modes = (("binary", "wb"), ("text", "w"))


    # This module tests operations on the builtin file object. It is not yet complete, the tests cover read(),
    # read(size), readline() and write() for binary, text and universal newline modes.
    def test_sanity(self):
        for i in range(5):
            ### general file robustness tests
            f = file("onlyread.tmp", "w")
            f.write("will only be read")
            f.flush()
            f.close()
            sin = file("onlyread.tmp", "r")
            sout = file("onlywrite.tmp", "w")

            # writer is null for sin
            self.assertRaises(IOError, sin.write, "abc")
            self.assertRaises(IOError, sin.writelines, ["abc","def"])

            # reader is null for sout
            if is_cli:
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
            f = file("onlywrite.tmp", "w")
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

        os.unlink("onlyread.tmp")
        os.unlink("onlywrite.tmp")

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

        # Keep a copy of the data safe.
        orig_data = data

        # Write the data to disk in binary mode.
        with file(self.temp_file, "wb") as f:
            f.write(data)

        # And read it back in again.
        with file(self.temp_file, "rb") as f:
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

        with open(self.temp_file, 'w') as x:
            x.write("a2\xa33\\u0163\x0F\x0FF\t\\\x0FF\x0FE\x00\x01\x7F\x7E\x80")

        with open(self.temp_file) as x:
            data = x.read()

        self.assertEqual(data, 'a2\xa33\\u0163\x0f\x0fF\t\\\x0fF\x0fE\x00\x01\x7F\x7E\x80')

    @unittest.skipUnless(is_cli, 'IronPython specific test')
    def test_cp27179(self):
        # file.write() accepting Array[Byte]
        from System import Array, Byte
        data_string = 'abcdef\nghijkl\n\n'
        data = Array[Byte](list(map(Byte, list(map(ord, data_string)))))

        with open(self.temp_file, 'w+') as f:
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
        read_patterns = (("\r", "\r", "\n"),
                        ("\n", "\n", "\n"),
                        ("\r\n", "\n", "\n"),
                        ("\n\r", "\n\r", "\n\n"),
                        ("\r\r", "\r\r", "\n\n"),
                        ("\n\n", "\n\n", "\n\n"),
                        ("\r\n\r\n", "\n\n", "\n\n"),
                        ("\n\r\n\r", "\n\n\r", "\n\n\n"),
                        ("The quick brown fox", "The quick brown fox", "The quick brown fox"),
                        ("The \rquick\n brown fox\r\n", "The \rquick\n brown fox\n", "The \nquick\n brown fox\n"),
                        ("The \r\rquick\r\n\r\n brown fox", "The \r\rquick\n\n brown fox", "The \n\nquick\n\n brown fox"))

        # Write mode test cases. Same deal as above but with one less member in each
        # tuple due to the lack of a universal newline write mode. The first value
        # represents the in-memory value we start with (and expect to write in binary
        # write mode) and the next value indicates the value we expect to end up on disk
        # in text mode.
        write_patterns = (("\r", "\r"),
                        ("\n", "\r\n"),
                        ("\r\n", "\r\r\n"),
                        ("\n\r", "\r\n\r"),
                        ("\r\r", "\r\r"),
                        ("\n\n", "\r\n\r\n"),
                        ("\r\n\r\n", "\r\r\n\r\r\n"),
                        ("\n\r\n\r", "\r\n\r\r\n\r"),
                        ("The quick brown fox", "The quick brown fox"),
                        ("The \rquick\n brown fox\r\n", "The \rquick\r\n brown fox\r\r\n"),
                        ("The \r\rquick\r\n\r\n brown fox", "The \r\rquick\r\r\n\r\r\n brown fox"))

        # Test a specific read mode pattern.
        def test_read_pattern(pattern):
            # Write the initial data to disk using binary mode (we test this
            # functionality earlier so we're satisfied it gets there unaltered).
            with file(self.temp_file, "wb") as f:
                f.write(pattern[0])

            # Read the data back in each read mode, checking that we get the correct
            # transform each time.
            for mode in range(3):
                test_read_mode(pattern, mode)

        # Test a specific read mode pattern for a given reading mode.
        def test_read_mode(pattern, mode):
            # Read the data back from disk using the given read mode.
            with file(self.temp_file, self.read_modes[mode][1]) as f:
                contents = f.read()

            # Check it equals what we expected for this mode.
            self.assertTrue(contents == pattern[mode])

        # Test a specific write mode pattern.
        def test_write_pattern(pattern):
            for mode in range(2):
                test_write_mode(pattern, mode)

        # Test a specific write mode pattern for a given write mode.
        def test_write_mode(pattern, mode):
            # Write the raw data using the given mode.
            with file(self.temp_file, self.write_modes[mode][1]) as f:
                f.write(pattern[0])

            # Read the data back in using binary mode (we tested this gets us back
            # unaltered data earlier).
            with file(self.temp_file, "rb") as f:
                contents = f.read()

            # Check it equals what we expected for this mode.
            self.assertTrue(contents == pattern[mode])

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

    @unittest.skipUnless(is_cli, 'IronPython specific test')
    def test_read_size(self):
        read_size_tests = [("Hello", 1, ("H", "e", "l", "l", "o"), (1,2,3,4,5),
                                        ("H", "e", "l", "l", "o"), (1,2,3,4,5),
                                        ("H", "e", "l", "l", "o"), (1,2,3,4,5)),
                        ("Hello", 2, ("He", "ll", "o"), (2,4,5),
                                        ("He", "ll", "o"), (2,4,5),
                                        ("He", "ll", "o"), (2,4,5))]

        if is_posix:
            read_size_tests.append(("H\re\n\r\nllo", 1, ("H", "\r", "e", "\n", "\r", "\n", "l", "l", "o"), (1,2,3,4,5,6,7, 8, 9),
                                                ("H", "\r", "e", "\n", "\r", "\n", "l", "l", "o"), (1, 2, 3, 4, 5, 6, 7, 8, 9),
                                                ("H", "\n", "e", "\n", "\n", "l", "l", "o"), (1, 2, 3, 4, 6, 7, 8, 9)))
            read_size_tests.append(("H\re\n\r\nllo", 2, ("H\r", "e\n", "\r\n", "ll", "o"), (2, 4, 6, 8, 9),
                                                ("H\r", "e\n", "\r\n", "ll", "o"), (2, 4, 6, 8, 9),
                                                ("H\n", "e\n", "\nl", "lo"), (2, 4, 7, 9)))
        else:
            read_size_tests.append(("H\re\n\r\nllo", 1, ("H", "\r", "e", "\n", "\r", "\n", "l", "l", "o"), (1,2,3,4,5,6,7, 8, 9),
                                                ("H", "\r", "e", "\n", "\n", "l", "l", "o"), (1,2,3,4,6,7,8,9),
                                                ("H", "\n", "e", "\n", "\n", "l", "l", "o"), (1,2,3,4,6,7,8,9)))
            read_size_tests.append(("H\re\n\r\nllo", 2, ("H\r", "e\n", "\r\n", "ll", "o"), (2, 4, 6, 8, 9),
                                                ("H\r", "e\n", "\nl", "lo"), (2,4,7, 9),
                                                ("H\n", "e\n", "\nl", "lo"), (2,4,7, 9)))

        for test in read_size_tests:
            # Write the test pattern to disk in binary mode.
            with file(self.temp_file, "wb") as f:
                f.write(test[0])

            # Read the data back in each of the read modes we test.
            for mode in range(3):
                with file(self.temp_file, self.read_modes[mode][1]) as f:
                    self.assertFalse(f.closed)

                    # We read the data in the size specified by the test and expect to get
                    # the set of strings given for this specific mode.
                    size = test[1]
                    strings = test[2 + mode*2]
                    lengths = test[3 + mode*2]
                    count = 0
                    while True:
                        data = f.read(size)
                        if data == "":
                            self.assertTrue(count == len(strings))
                            break
                        count = count + 1
                        self.assertTrue(count <= len(strings))
                        self.assertTrue(data == strings[count - 1])
                        self.assertEqual(f.tell(), lengths[count-1])
                    f.close()
                    self.assertTrue(f.closed)

    # And some readline tests.
    # Test data is in the following format: ("raw data", (binary mode result strings)
    #                                                    (text mode result strings)
    #                                                    (universal mode result strings))
    def test_readline(self):
        readline_tests = [("Mary had a little lamb", ("Mary had a little lamb", ),
                                                    ("Mary had a little lamb", ),
                                                    ("Mary had a little lamb", )),
                        ("Mary had a little lamb\r", ("Mary had a little lamb\r", ),
                                                    ("Mary had a little lamb\r", ),
                                                    ("Mary had a little lamb\n", )),
                        ("Mary had a \rlittle lamb\r", ("Mary had a \rlittle lamb\r", ),
                                                        ("Mary had a \rlittle lamb\r", ),
                                                        ("Mary had a \n", "little lamb\n"))]

        # wb doesn't change the output to just \n like on Windows (binary mode means nothing on POSIX)
        if is_posix:
            readline_tests.append(("Mary \r\nhad \na little lamb", ("Mary \r\n", "had \n", "a little lamb"),
                                                        ("Mary \r\n", "had \n", "a little lamb"),
                                                        ("Mary \n", "had \n", "a little lamb")))
        else:
            readline_tests.append(("Mary \r\nhad \na little lamb", ("Mary \r\n", "had \n", "a little lamb"),
                                                        ("Mary \n", "had \n", "a little lamb"),
                                                        ("Mary \n", "had \n", "a little lamb")))
        for test in readline_tests:
            # Write the test pattern to disk in binary mode.
            with file(self.temp_file, "wb") as f:
                f.write(test[0])

            # Read the data back in each of the read modes we test.
            for mode in range(3):
                with file(self.temp_file, self.read_modes[mode][1]) as f:
                    # We read the data by line and expect to get a specific sets of lines back.
                    strings = test[1 + mode]
                    count = 0
                    while True:
                        data = f.readline()
                        if data == "":
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
    @unittest.skipUnless(is_cli, 'IronPython specific test')
    def test_newlines_attribute(self):
        newlines_tests = (("123", (None, )),
                        ("1\r\n2\r3\n", ("\r\n", ("\r\n", "\r"), ("\r\n", "\r", "\n"))),
                        ("1\r2\n3\r\n", ("\r", ("\r", "\n"), ("\r\n", "\r", "\n"))),
                        ("1\n2\r\n3\r", ("\n", ("\r\n", "\n"), ("\r\n", "\r", "\n"))),
                        ("1\r\n2\r\n3\r\n", ("\r\n", "\r\n", "\r\n")),
                        ("1\r2\r3\r", ("\r", "\r", "\r")),
                        ("1\n2\n3\n", ("\n", "\n", "\n")))

        for test in newlines_tests:
            # Write the test pattern to disk in binary mode.
            with file(self.temp_file, "wb") as f:
                f.write(test[0])
                # Verify newlines isn't set while writing.
                self.assertTrue(f.newlines == None)

            # Verify that reading the file in binary or text mode won't set newlines.
            with file(self.temp_file, "rb") as f:
                data = f.read()
                self.assertTrue(f.newlines == None)

            with file(self.temp_file, "r") as f:
                data = f.read()
                self.assertTrue(f.newlines == None)

            # Read file in universal mode line by line and verify we see the expected output at each stage.
            expected = test[1]
            with file(self.temp_file, "rU") as f:
                self.assertTrue(f.newlines == None)
                count = 0
                while True:
                    data = f.readline()
                    if data == "":
                        break
                    self.assertTrue(count < len(expected))
                    self.assertTrue(f.newlines == expected[count])
                    count = count + 1


    ## coverage: a sequence of file operation
    @unittest.skipIf(is_posix, 'file sequence specific to windows')
    def test_coverage(self):
        with file(self.temp_file, 'w') as f:
            self.assertTrue(str(f).startswith("<open file '%s', mode 'w'" % self.temp_file))
            self.assertTrue(f.fileno() != -1)
            self.assertTrue(f.fileno() != 0)

            # write
            self.assertRaises(TypeError, f.writelines, [3])
            f.writelines(["firstline\n"])

            f.close()
            self.assertTrue(str(f).startswith("<closed file '%s', mode 'w'" % self.temp_file))

        # append
        with file(self.temp_file, 'a+') as f:
            f.writelines(['\n', 'secondline\n'])

            pos = len('secondline\n') + 1
            f.seek(-1 * pos, 1)

            f.writelines(['thirdline\n'])

        # read
        with file(self.temp_file, 'r+', 512) as f:
            f.seek(-1 * pos - 2, 2)
            self.assertEqual(f.readline(), 'e\n')
            self.assertEqual(f.readline(5), 'third')
            self.assertEqual(f.read(-1), 'line\n')
            self.assertEqual(f.read(-1), '')

        # read
        with file(self.temp_file, 'rb', 512) as f:
            f.seek(-1 * pos - 2, 2)
            self.assertEqual(f.readline(), 'e\r\n')
            self.assertEqual(f.readline(5), 'third')
            self.assertEqual(f.read(-1), 'line\r\n')
            self.assertEqual(f.read(-1), '')

        ## file op in os
        os.unlink(self.temp_file)

        fd = os.open(self.temp_file, os.O_CREAT | os.O_WRONLY)
        os.write(fd, "hello ")
        os.close(fd)

        fd = os.open(self.temp_file, os.O_APPEND | os.O_WRONLY)
        os.write(fd, "world")
        os.close(fd)

        fd = os.open(self.temp_file, 0)
        self.assertEqual(os.read(fd, 1024), "hello world")
        os.close(fd)

        os.unlink(self.temp_file)

    def test_encoding(self):
        f = file(self.temp_file, 'w')
        # we throw on flush, CPython throws on write, so both write & close need to catch
        try:
            f.write('\u6211')
            f.close()
            self.fail('UnicodeEncodeError should have been thrown')
        except UnicodeEncodeError:
            pass
        
        if hasattr(sys, "setdefaultencoding"):
            #and verify UTF8 round trips correctly
            setenc = sys.setdefaultencoding
            saved = sys.getdefaultencoding()
            try:
                setenc('utf8')
                with file(self.temp_file, 'w') as f:
                    f.write('\u6211')

                with file(self.temp_file, 'r') as f:
                    txt = f.read()
                self.assertEqual(txt, '\u6211')
            finally:
                setenc(saved)

    @unittest.skipUnless(is_cli, 'IronPython specific test')
    def test_net_stream(self):
        import System
        if is_netstandard:
            import clr
            clr.AddReference("System.IO.FileSystem")
            clr.AddReference("System.IO.FileSystem.Primitives")

        fs = System.IO.FileStream(self.temp_file, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.ReadWrite)
        with file(fs, "wb") as f:
            f.write('hello\rworld\ngoodbye\r\n')
        
        with file(self.temp_file, 'rb') as f:
            self.assertEqual(f.read(), 'hello\rworld\ngoodbye\r\n')

        with file(self.temp_file, 'rU') as f:
            self.assertEqual(f.read(), 'hello\nworld\ngoodbye\n')

    @unittest.skipUnless(is_cli, 'IronPython specific test')
    def test_file_manager(self):
        def return_fd1():
            f = file(self.temp_file, 'w')
            return f.fileno()
            
        def return_fd2():
            return os.open(self.temp_file, 0)
        
        import System

        fd = return_fd1()
        System.GC.Collect()
        System.GC.WaitForPendingFinalizers()
        self.assertRaises(OSError, os.fdopen, fd)

        fd = return_fd2()
        System.GC.Collect()
        System.GC.WaitForPendingFinalizers()
        f = os.fdopen(fd)
        f.close()
        self.assertRaises(OSError, os.fdopen, fd)

    def test_sharing(self):
        modes = ['w', 'w+', 'a+', 'r', 'w']
        for xx in modes:
            for yy in modes:
                x = file('tempfile.txt', xx)
                y = file('tempfile.txt', yy)
                
                x.close()
                y.close()
                
        os.unlink('tempfile.txt')

    def test_overwrite_readonly(self):
        filename = "tmp.txt"
        f = file(filename, "w+")
        f.write("I am read-only")
        f.close()
        os.chmod(filename, 256)
        try:
            try:
                f = file(filename, "w+") # FAIL
            finally:
                os.chmod(filename, 128)
                os.unlink(filename)
        except IOError as e:
            pass
        else:
            AssertUnreachable() # should throw
        #any other exceptions fail

    def test_inheritance_kwarg_override(self):
        class TEST(file):
            def __init__(self,fname,VERBOSITY=0):
                file.__init__(self,fname,"w",1)
                self.VERBOSITY = VERBOSITY
        
        f=TEST(r'sometext.txt',VERBOSITY=1)
        self.assertEqual(f.VERBOSITY, 1)
        f.close()
        os.unlink('sometext.txt')

    # file newline handling test
    @unittest.skipIf(is_posix, "this test doesn't really make sense for posix since b doesn't change the behavior")
    def test_newline(self):
        def test_newline(norm, mode):
            f = file("testfile.tmp", mode)
            self.assertTrue(f.read() == norm)
            for x in range(len(norm)):
                f.seek(0)
                a = f.read(x)
                b = f.read(1)
                c = f.read()
                self.assertTrue(a+b+c == norm)
            f.close()
        
        self.assertRaises(TypeError, file, None) # arg must be string
        self.assertRaises(TypeError, file, [])
        self.assertRaises(TypeError, file, 1)
        
        norm   = "Hi\nHello\nHey\nBye\nAhoy\n"
        unnorm = "Hi\r\nHello\r\nHey\r\nBye\r\nAhoy\r\n"
        f = file("testfile.tmp", "wb")
        f.write(unnorm)
        f.close()
        
        test_newline(norm, "r")
        test_newline(unnorm, "rb")

        os.unlink("testfile.tmp")

    def test_creation(self):
        f = file.__new__(file, None)
        self.assertTrue(repr(f).startswith("<closed file '<uninitialized file>', mode '<uninitialized file>' at"))
        
        self.assertRaises(TypeError, file, None)


    def test_repr(self):
        class x(file):
            def __repr__(self): return 'abc'
            
        f = x('repr_does_not_exist', 'w')
        self.assertEqual(repr(f), 'abc')
        f.close()
        os.unlink('repr_does_not_exist')

    def test_truncate(self):
        
        # truncate()
        with file('abc.txt', 'w') as a:
            a.write('hello world\n')
            a.truncate()

        with file('abc.txt', 'r') as a:
            self.assertEqual(a.readlines(), ['hello world\n'])
        
        os.unlink('abc.txt')

        # truncate(#)
        with file('abc.txt', 'w') as a:
            a.write('hello\nworld\n')
            a.truncate(6)
        
        with file('abc.txt', 'r') as a:
            if is_posix:
                self.assertEqual(a.readlines(), ['hello\n'])
            else:
                self.assertEqual(a.readlines(), ['hello\r'])

        os.unlink('abc.txt')
        
        # truncate(#) invalid args
        with file('abc.txt', 'w') as a:
            self.assertRaises(IOError, a.truncate, -1)
            self.assertRaises(TypeError, a.truncate, None)
        
        # read-only file
        with file('abc.txt', 'r') as a:
            self.assertRaises(IOError, a.truncate)
            self.assertRaises(IOError, a.truncate, 0)
        os.unlink('abc.txt')
        
        # std-out
        self.assertRaises(IOError, sys.stdout.truncate)

    def test_modes(self):
        """test various strange mode combinations and error reporting"""
        try:
            with file('test_file', 'w') as x:
                self.assertEqual(x.mode, 'w')
            # don't allow empty modes
            self.assertRaisesMessage(ValueError, 'empty mode string', file, 'abc', '')
            
            # mode must start with valid value
            self.assertRaisesMessage(ValueError, "mode string must begin with one of 'r', 'w', 'a' or 'U', not 'p'", file, 'abc', 'p')
            
            # allow anything w/ U but r and w
            self.assertRaisesMessage(ValueError, "universal newline mode can only be used with modes starting with 'r'", file, 'abc', 'Uw')
            self.assertRaisesMessage(ValueError, "universal newline mode can only be used with modes starting with 'r'", file, 'abc', 'Ua')
            self.assertRaisesMessage(ValueError, "universal newline mode can only be used with modes starting with 'r'", file, 'abc', 'Uw+')
            self.assertRaisesMessage(ValueError, "universal newline mode can only be used with modes starting with 'r'", file, 'abc', 'Ua+')
        
            # check invalid modes
            self.assertRaises(ValueError, file, 'test_file', 'pU')
            self.assertRaises(ValueError, file, 'test_file', 'pU+')
            self.assertRaises(ValueError, file, 'test_file', 'rFOOBAR')
        finally:
            os.unlink('test_file')

    

    @unittest.skipUnless(is_cli, 'Unstable with CPython')
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
        file_name = os.path.join(self.temporary_dir, "cp16623.txt")
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


    def test_write_buffer(self):
        try:
            for mode in ('b', ''):
                with open('foo', 'w+' + mode) as foo:
                    b = buffer(b'hello world', 6)
                    foo.write(b)
            
                with open('foo', 'r') as foo:
                    self.assertEqual(foo.readlines(), ['world'])
            
            with open('foo', 'w+') as foo:
                b = buffer('hello world', 6)
                foo.write(b)
            with open('foo', 'r') as foo:
                self.assertEqual(foo.readlines(), ['world'])
            
            with open('foo', 'w+b') as foo:
                b = buffer('hello world', 6)
                foo.write(b)
            
            
            with open('foo', 'r') as foo:
                if is_cpython:
                    self.assertEqual(foo.readlines(), ['l\x00o\x00 \x00w\x00o\x00r\x00l\x00d\x00'])
                else:
                    self.assertEqual(foo.readlines(), ['world'])

        finally:
            self.delete_files("foo")

    def test_errors(self):
        try:
            file('some_file_that_really_does_not_exist')
        except Exception as e:
            self.assertEqual(e.errno, 2)
        else:
            AssertUnreachable()

        try:
            file('path_too_long' * 100) 
        except Exception as e:
            self.assertEqual(e.errno, 2)
        else:
            AssertUnreachable()

    def test_write_bytes(self):
        f = open("temp_ip", "w+")
        try:
            f.write(b"Hello\n")
            f.close()
            f = file('temp_ip')
            self.assertEqual(f.readlines(), ['Hello\n'])
            f.close()
        finally:
            f.close()
            os.unlink('temp_ip')

    def test_kw_args(self):
        file(name = 'some_test_file.txt', mode = 'w').close()
        os.unlink('some_test_file.txt')

    def test_buffering_kwparam(self):
        #--Positive
        for x in [-2147483648, -1, 0, 1, 2, 1024, 2147483646, 2147483647]:
            f = file(name = 'some_test_file.txt', mode = 'w', buffering=x)
            f.close()
            os.unlink('some_test_file.txt')
        
        self.assertRaisesMessage(TypeError, "integer argument expected, got float",
                                file, 'some_test_file.txt', 'w', 3.14)

        #--Negative
        for x in [None, "abc", "", [], tuple()]:
            self.assertRaises(TypeError, #"an integer is required",
                    lambda: file(name = 'some_test_file.txt', mode = 'w', buffering=x))
        
        for x in [2147483648, -2147483649]:
            self.assertRaises(OverflowError, #"long int too large to convert to int",
                        lambda: file(name = 'some_test_file.txt', mode = 'w', buffering=x))

    def test_open_with_BOM(self):
        """https://github.com/IronLanguages/main/issues/1088"""
        fileName = os.path.join(self.test_dir, "file_without_BOM.txt") 
        with open(fileName, "r") as f:
            if is_posix: self.assertEqual(f.read(), "\x42\xc3\x93\x4d\x0d\x0a")
            else: self.assertEqual(f.read(), "\x42\xc3\x93\x4d\x0a")
        with open(fileName, "rb") as f:
            self.assertEqual(f.read(), "\x42\xc3\x93\x4d\x0d\x0a")

        fileName = os.path.join(self.test_dir, "file_with_BOM.txt") 
        with open(fileName, "r") as f:
            if is_posix: self.assertEqual(f.read(), "\xef\xbb\xbf\x42\xc3\x93\x4d\x0d\x0a")
            else: self.assertEqual(f.read(), "\xef\xbb\xbf\x42\xc3\x93\x4d\x0a")
        with open(fileName, "rb") as f:
            self.assertEqual(f.read(), "\xef\xbb\xbf\x42\xc3\x93\x4d\x0d\x0a")


run_test(__name__)