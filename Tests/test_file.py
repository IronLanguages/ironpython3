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

from iptest.assert_util import *
skiptest("silverlight")
import sys
import nt

# This module tests operations on the builtin file object. It is not yet complete, the tests cover read(),
# read(size), readline() and write() for binary, text and universal newline modes.
def test_sanity():
    for i in range(5):
        ### general file robustness tests
        f = file("onlyread.tmp", "w")
        f.write("will only be read")
        f.flush()
        f.close()
        sin = file("onlyread.tmp", "r")
        sout = file("onlywrite.tmp", "w")
        
        # writer is null for sin
        AssertError(IOError, sin.write, "abc")
        AssertError(IOError, sin.writelines, ["abc","def"])

        # reader is null for sout
        if is_cli:
            AssertError(IOError, sout.read)
            AssertError(IOError, sout.read, 10)
            AssertError(IOError, sout.readline)
            AssertError(IOError, sout.readline, 10)
            AssertError(IOError, sout.readlines)
            AssertError(IOError, sout.readlines, 10)
        
        sin.close()
        sout.close()

        # now close a file and try to perform other I/O operations on it...
        # should throw ValueError according to docs
        f = file("onlywrite.tmp", "w")
        f.close()
        f.close()
        AssertError(ValueError, f.__iter__)
        AssertError(ValueError, f.flush)
        AssertError(ValueError, f.fileno)
        AssertError(ValueError, f.next)
        AssertError(ValueError, f.read)
        AssertError(ValueError, f.read, 10)
        AssertError(ValueError, f.readline)
        AssertError(ValueError, f.readline, 10)
        AssertError(ValueError, f.readlines)
        AssertError(ValueError, f.readlines, 10)
        AssertError(ValueError, f.seek, 10)
        AssertError(ValueError, f.seek, 10, 10)
        AssertError(ValueError, f.write, "abc")
        AssertError(ValueError, f.writelines, ["abc","def"])

    ###

# The name of a temporary test data file that will be used for the following
# file tests.
temp_file = path_combine(testpath.temporary_dir, "temp.dat")

# Test binary reading and writing fidelity using a round trip method. First
# construct some pseudo random binary data in a string (making it long enough
# that it's likely we'd show up any problems with the data being passed through
# a character encoding/decoding scheme). Then write this data to disk (in binary
# mode), read it back again (in binary) and check that no changes have occured.

# Construct the binary data. We want the test to be repeatable so seed the
# random number generator with a fixed value. Use a simple linear congruential
# method to generate the random byte values.

rng_seed = 0

def test_read_write_fidelity():
    def randbyte():
        global rng_seed
        rng_seed = (1664525 * rng_seed) + 1013904223
        return (rng_seed >> 8) & 0xff

    data = ""
    for i in range(10 * 1024):
        data += chr(randbyte())

    # Keep a copy of the data safe.
    orig_data = data;

    # Write the data to disk in binary mode.
    f = file(temp_file, "wb")
    f.write(data)
    f.close()

    # And read it back in again.
    f = file(temp_file, "rb")
    data = f.read()
    f.close()

    # Check nothing changed.
    Assert(data == orig_data)
    
def test_cp10983():
    # writing non-unicode characters > 127 should be preserved
    x = open(temp_file, 'w')
    x.write('\xa33')
    x.close()
    
    x = open(temp_file)
    data = x.read()
    x.close()
    
    AreEqual(ord(data[0]), 163)
    AreEqual(ord(data[1]), 51)
    
    x = open(temp_file, 'w')
    x.write("a2\xa33\u0163\x0F\x0FF\t\\\x0FF\x0FE\x00\x01\x7F\x7E\x80")
    x.close()
    
    x = open(temp_file)
    data = x.read()
    x.close()
    
    AreEqual(data, 'a2\xa33\\u0163\x0f\x0fF\t\\\x0fF\x0fE\x00\x01\x7F\x7E\x80')

@skip('win32')
def test_cp27179():
    # file.write() accepting Array[Byte]
    from System import Array, Byte
    data_string = 'abcdef\nghijkl\n\n'
    data = Array[Byte](map(Byte, map(ord, data_string)))
    
    f = open(temp_file, 'w+')
    f.write(data)
    f.close()
    
    f = open(temp_file, 'r')
    data_read = f.read()
    f.close()
    
    AreEqual(data_string, data_read)

# Helper used to format newline characters into a visible format.
def format_newlines(string):
    out = ""
    for char in string:
        if char == '\r':
            out += "\\r"
        elif char == '\n':
            out += "\\n"
        else:
            out += char
    return out

# The set of read modes we wish to test. Each tuple consists of a human readable
# name for the mode followed by the corresponding mode string that will be
# passed to the file constructor.
read_modes = (("binary", "rb"), ("text", "r"), ("universal", "rU"))

# Same deal as above but for write modes. Note that writing doesn't support a
# universal newline mode.
write_modes = (("binary", "wb"), ("text", "w"))

# The following is the setup for a set of pattern mode tests that will check
# some tricky edge cases for newline translation for both reading and writing.
# The entry point is the test_patterns() function.
def test_newlines():

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
        f = file(temp_file, "wb")
        f.write(pattern[0])
        f.close()

        # Read the data back in each read mode, checking that we get the correct
        # transform each time.
        for mode in range(3):
            test_read_mode(pattern, mode);

    # Test a specific read mode pattern for a given reading mode.
    def test_read_mode(pattern, mode):
        # Read the data back from disk using the given read mode.
        f = file(temp_file, read_modes[mode][1])
        contents = f.read()
        f.close()

        # Check it equals what we expected for this mode.
        Assert(contents == pattern[mode])

    # Test a specific write mode pattern.
    def test_write_pattern(pattern):
        for mode in range(2):
            test_write_mode(pattern, mode);

    # Test a specific write mode pattern for a given write mode.
    def test_write_mode(pattern, mode):
        # Write the raw data using the given mode.
        f = file(temp_file, write_modes[mode][1])
        f.write(pattern[0])
        f.close()

        # Read the data back in using binary mode (we tested this gets us back
        # unaltered data earlier).
        f = file(temp_file, "rb")
        contents = f.read()
        f.close()

        # Check it equals what we expected for this mode.
        Assert(contents == pattern[mode])

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
#                                                               (universal mode result strings) (univermose mode result tell() results)

def test_read_size():
    read_size_tests = (("Hello", 1, ("H", "e", "l", "l", "o"), (1,2,3,4,5),
                                    ("H", "e", "l", "l", "o"), (1,2,3,4,5),
                                    ("H", "e", "l", "l", "o"), (1,2,3,4,5)),
                       ("Hello", 2, ("He", "ll", "o"), (2,4,5),
                                    ("He", "ll", "o"), (2,4,5),
                                    ("He", "ll", "o"), (2,4,5)),
                       ("H\re\n\r\nllo", 1, ("H", "\r", "e", "\n", "\r", "\n", "l", "l", "o"), (1,2,3,4,5,6,7, 8, 9),
                                            ("H", "\r", "e", "\n", "\n", "l", "l", "o"), (1,2,3,4,6,7,8,9),
                                            ("H", "\n", "e", "\n", "\n", "l", "l", "o"), (1,2,3,4,6,7,8,9)),
                       ("H\re\n\r\nllo", 2, ("H\r", "e\n", "\r\n", "ll", "o"), (2, 4, 6, 8, 9),
                                            ("H\r", "e\n", "\nl", "lo"), (2,4,7, 9),
                                            ("H\n", "e\n", "\nl", "lo"), (2,4,7, 9)))

    if not is_cli: return
    
    for test in read_size_tests:
        # Write the test pattern to disk in binary mode.
        f = file(temp_file, "wb")
        f.write(test[0])
        f.close()

        # Read the data back in each of the read modes we test.
        for mode in range(3):
            f = file(temp_file, read_modes[mode][1])
            AreEqual(f.closed, False)

            # We read the data in the size specified by the test and expect to get
            # the set of strings given for this specific mode.
            size = test[1]
            strings = test[2 + mode*2]
            lengths = test[3 + mode*2]
            count = 0
            while True:
                data = f.read(size)
                if data == "":
                    Assert(count == len(strings))
                    break
                count = count + 1
                Assert(count <= len(strings))
                Assert(data == strings[count - 1])
                AreEqual(f.tell(), lengths[count-1])

            f.close()
            AreEqual(f.closed, True)

# And some readline tests.
# Test data is in the following format: ("raw data", (binary mode result strings)
#                                                    (text mode result strings)
#                                                    (universal mode result strings))
def test_readline():
    readline_tests = (("Mary had a little lamb", ("Mary had a little lamb", ),
                                                 ("Mary had a little lamb", ),
                                                 ("Mary had a little lamb", )),
                      ("Mary had a little lamb\r", ("Mary had a little lamb\r", ),
                                                   ("Mary had a little lamb\r", ),
                                                   ("Mary had a little lamb\n", )),
                      ("Mary had a \rlittle lamb\r", ("Mary had a \rlittle lamb\r", ),
                                                     ("Mary had a \rlittle lamb\r", ),
                                                     ("Mary had a \n", "little lamb\n")),
                      ("Mary \r\nhad \na little lamb", ("Mary \r\n", "had \n", "a little lamb"),
                                                       ("Mary \n", "had \n", "a little lamb"),
                                                       ("Mary \n", "had \n", "a little lamb")))
    for test in readline_tests:
        # Write the test pattern to disk in binary mode.
        f = file(temp_file, "wb")
        f.write(test[0])
        f.close()

        # Read the data back in each of the read modes we test.
        for mode in range(3):
            f = file(temp_file, read_modes[mode][1])

            # We read the data by line and expect to get a specific sets of lines back.
            strings = test[1 + mode]
            count = 0
            while True:
                data = f.readline()
                if data == "":
                    AreEqual(count, len(strings))
                    break
                count = count + 1
                Assert(count <= len(strings))
                AreEqual(data, strings[count - 1])

            f.close()

def format_tuple(tup):
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
def test_newlines_attribute():
    newlines_tests = (("123", (None, )),
                      ("1\r\n2\r3\n", ("\r\n", ("\r\n", "\r"), ("\r\n", "\r", "\n"))),
                      ("1\r2\n3\r\n", ("\r", ("\r", "\n"), ("\r\n", "\r", "\n"))),
                      ("1\n2\r\n3\r", ("\n", ("\r\n", "\n"), ("\r\n", "\r", "\n"))),
                      ("1\r\n2\r\n3\r\n", ("\r\n", "\r\n", "\r\n")),
                      ("1\r2\r3\r", ("\r", "\r", "\r")),
                      ("1\n2\n3\n", ("\n", "\n", "\n")))

    if not is_cli: return False
    
    for test in newlines_tests:
        # Write the test pattern to disk in binary mode.
        f = file(temp_file, "wb")
        f.write(test[0])
        # Verify newlines isn't set while writing.
        Assert(f.newlines == None)
        f.close()

        # Verify that reading the file in binary or text mode won't set newlines.
        f = file(temp_file, "rb")
        data = f.read()
        Assert(f.newlines == None)
        f.close()

        f = file(temp_file, "r")
        data = f.read()
        Assert(f.newlines == None)
        f.close()

        # Read file in universal mode line by line and verify we see the expected output at each stage.
        expected = test[1]
        f = file(temp_file, "rU")
        Assert(f.newlines == None)
        count = 0
        while True:
            data = f.readline()
            if data == "":
                break
            Assert(count < len(expected))
            Assert(f.newlines == expected[count])
            count = count + 1
        f.close()
    
## coverage: a sequence of file operation
def test_coverage():
    f = file(temp_file, 'w')
    Assert(str(f).startswith("<open file '%s', mode 'w'" % temp_file))
    Assert(f.fileno() <> -1)
    Assert(f.fileno() <> 0)

    # write
    AssertError(TypeError, f.writelines, [3])
    f.writelines(["firstline\n"])

    f.close()
    Assert(str(f).startswith("<closed file '%s', mode 'w'" % temp_file))

    # append
    f = file(temp_file, 'a+')
    f.writelines(['\n', 'secondline\n'])

    pos = len('secondline\n') + 1
    f.seek(-1 * pos, 1)

    f.writelines(['thirdline\n'])
    f.close()

    # read
    f = file(temp_file, 'r+', 512)
    f.seek(-1 * pos - 2, 2)

    AreEqual(f.readline(), 'e\n')
    AreEqual(f.readline(5), 'third')
    AreEqual(f.read(-1), 'line\n')
    AreEqual(f.read(-1), '')
    f.close()

    # read
    f = file(temp_file, 'rb', 512)
    f.seek(-1 * pos - 2, 2)

    AreEqual(f.readline(), 'e\r\n')
    AreEqual(f.readline(5), 'third')
    AreEqual(f.read(-1), 'line\r\n')
    AreEqual(f.read(-1), '')
    f.close()

    ## file op in nt    
    nt.unlink(temp_file)

    fd = nt.open(temp_file, nt.O_CREAT | nt.O_WRONLY)
    nt.write(fd, "hello ")
    nt.close(fd)

    fd = nt.open(temp_file, nt.O_APPEND | nt.O_WRONLY)
    nt.write(fd, "world")
    nt.close(fd)

    fd = nt.open(temp_file, 0)
    AreEqual(nt.read(fd, 1024), "hello world")
    nt.close(fd)

    nt.unlink(temp_file)

def test_encoding():
    #verify we start w/ ASCII
    import sys

    f = file(temp_file, 'w')
    # we throw on flush, CPython throws on write, so both write & close need to catch
    try:
        f.write(u'\u6211')
        f.close()
        AssertUnreachable()
    except UnicodeEncodeError:
        pass
    
    if hasattr(sys, "setdefaultencoding"):
        #and verify UTF8 round trips correctly
        setenc = sys.setdefaultencoding
        saved = sys.getdefaultencoding()
        try:
            setenc('utf8')
            f = file(temp_file, 'w')
            f.write(u'\u6211')
            f.close()

            f = file(temp_file, 'r')
            txt = f.read()
            f.close()
            AreEqual(txt, u'\u6211')
        finally:
            setenc(saved)

if is_cli:
    def test_net_stream():
        import System
        fs = System.IO.FileStream(temp_file, System.IO.FileMode.Create, System.IO.FileAccess.Write)
        f = file(fs, "wb")
        f.write('hello\rworld\ngoodbye\r\n')
        f.close()
        
        f = file(temp_file, 'rb')
        AreEqual(f.read(), 'hello\rworld\ngoodbye\r\n')
        f.close()
        
        f = file(temp_file, 'rU')
        AreEqual(f.read(), 'hello\nworld\ngoodbye\n')
        f.close()
    
    def test_file_manager():
        def return_fd1():
            f = file(temp_file, 'w')
            return f.fileno()
            
        def return_fd2():
            return nt.open(temp_file, 0)
        
        import System

        fd = return_fd1()
        System.GC.Collect()
        System.GC.WaitForPendingFinalizers()
        AssertError(OSError, nt.fdopen, fd)

        fd = return_fd2()
        System.GC.Collect()
        System.GC.WaitForPendingFinalizers()
        f = nt.fdopen(fd)
        f.close()
        AssertError(OSError, nt.fdopen, fd)

def test_sharing():
    modes = ['w', 'w+', 'a+', 'r', 'w']
    for xx in modes:
        for yy in modes:
            x = file('tempfile.txt', xx)
            y = file('tempfile.txt', yy)
            
            x.close()
            y.close()
            
    nt.unlink('tempfile.txt')

def test_overwrite_readonly():
    filename = "tmp.txt"
    f = file(filename, "w+")
    f.write("I am read-only")
    f.close()
    nt.chmod(filename, 256)
    try:
        try:
            f = file(filename, "w+") # FAIL
        finally:
            nt.chmod(filename, 128)
            nt.unlink(filename)
    except IOError, e:
        pass
    else:
        AssertUnreachable() # should throw
    #any other exceptions fail

def test_inheritance_kwarg_override():
    class TEST(file):
        def __init__(self,fname,VERBOSITY=0):
            file.__init__(self,fname,"w",1)
            self.VERBOSITY = VERBOSITY
    
    f=TEST(r'sometext.txt',VERBOSITY=1)
    AreEqual(f.VERBOSITY, 1)
    f.close()
    nt.unlink('sometext.txt')

# file newline handling test
def test_newline():
    def test_newline(norm, mode):
        f = file("testfile.tmp", mode)
        Assert(f.read() == norm)
        for x in xrange(len(norm)):
            f.seek(0)
            a = f.read(x)
            b = f.read(1)
            c = f.read()
            Assert(a+b+c == norm)
        f.close()
    
    AssertError(TypeError, file, None) # arg must be string
    AssertError(TypeError, file, [])
    AssertError(TypeError, file, 1)
    
    norm   = "Hi\nHello\nHey\nBye\nAhoy\n"
    unnorm = "Hi\r\nHello\r\nHey\r\nBye\r\nAhoy\r\n"
    f = file("testfile.tmp", "wb")
    f.write(unnorm)
    f.close()
    
    test_newline(norm, "r")
    test_newline(unnorm, "rb")

def test_creation():
    f = file.__new__(file, None)
    Assert(repr(f).startswith("<closed file '<uninitialized file>', mode '<uninitialized file>' at"))
    
    AssertError(TypeError, file, None)


def test_repr():
    class x(file):
        def __repr__(self): return 'abc'
        
    f = x('repr_does_not_exist', 'w')
    AreEqual(repr(f), 'abc')
    f.close()
    nt.unlink('repr_does_not_exist')

def test_truncate():
    
    # truncate()
    a = file('abc.txt', 'w')
    a.write('hello world\n')
    a.truncate()
    a.close()
    
    a = file('abc.txt', 'r')
    AreEqual(a.readlines(), ['hello world\n'])
    a.close()
    nt.unlink('abc.txt')

    # truncate(#)
    a = file('abc.txt', 'w')
    a.write('hello\nworld\n')
    a.truncate(6)
    a.close()
    
    a = file('abc.txt', 'r')
    AreEqual(a.readlines(), ['hello\r'])
    a.close()

    nt.unlink('abc.txt')
    
    # truncate(#) invalid args
    a = file('abc.txt', 'w')
    AssertError(IOError, a.truncate, -1)
    AssertError(TypeError, a.truncate, None)
    a.close()
    
    # read-only file
    a = file('abc.txt', 'r')
    AssertError(IOError, a.truncate)
    AssertError(IOError, a.truncate, 0)
    a.close()
    
    nt.unlink('abc.txt')
    
    # std-out
    AssertError(IOError, sys.stdout.truncate)

def test_modes():
    """test various strange mode combinations and error reporting"""
    try:
        x = file('test_file', 'w')
        AreEqual(x.mode, 'w')
        x.close()
        # don't allow empty modes
        AssertErrorWithMessage(ValueError, 'empty mode string', file, 'abc', '')
        
        # mode must start with valid value
        AssertErrorWithMessage(ValueError, "mode string must begin with one of 'r', 'w', 'a' or 'U', not 'p'", file, 'abc', 'p')
        
        # allow anything w/ U but r and w
        AssertErrorWithMessage(ValueError, "universal newline mode can only be used with modes starting with 'r'", file, 'abc', 'Uw')
        AssertErrorWithMessage(ValueError, "universal newline mode can only be used with modes starting with 'r'", file, 'abc', 'Ua')
        AssertErrorWithMessage(ValueError, "universal newline mode can only be used with modes starting with 'r'", file, 'abc', 'Uw+')
        AssertErrorWithMessage(ValueError, "universal newline mode can only be used with modes starting with 'r'", file, 'abc', 'Ua+')
    
        if is_cli:
            #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=21910
            x = file('test_file', 'pU')
            AreEqual(x.mode, 'pU')
            x.close()
            
            #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=21910
            x = file('test_file', 'pU+')
            AreEqual(x.mode, 'pU+')
            x.close()
            
            #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=21911
            # extra info can be passed and is retained
            x = file('test_file', 'rFOOBAR')
            AreEqual(x.mode, 'rFOOBAR')
            x.close()
        else:
            AssertError(ValueError, file, 'test_file', 'pU')
            AssertError(ValueError, file, 'test_file', 'pU+')
            AssertError(ValueError, file, 'test_file', 'rFOOBAR')
    finally:
        nt.unlink('test_file')

import thread
CP16623_LOCK = thread.allocate_lock()

@skip("win32")  #This test is unstable under RunAgainstCpy.py
def test_cp16623():
    '''
    If this test ever fails randomly, there is a problem around file thread
    safety.  Do not wrap this test case with retry_on_failure!
    '''
    global FINISHED_COUNTER
    FINISHED_COUNTER = 0
    
    import time
    
    expected_lines = ["a", "bbb" * 100, "cc"]
    total_threads = 50
    file_name = path_combine(testpath.temporary_dir, "cp16623.txt")
    f = open(file_name, "w")
    
    def write_stuff():
        global FINISHED_COUNTER
        global CP16623_LOCK
        for j in xrange(100):
            for i in xrange(50):
                print >> f, "a"
            print >> f, "bbb" * 1000
            for i in xrange(10):
                print >> f, "cc"
        
        with CP16623_LOCK:
            FINISHED_COUNTER += 1

    for i in xrange(total_threads):
        thread.start_new_thread(write_stuff, ())

    #Give all threads some time to finish
    for i in xrange(total_threads):
        if FINISHED_COUNTER!=total_threads:
            print "*",
            time.sleep(1)
        else:
            break
    AreEqual(FINISHED_COUNTER, total_threads)    
    f.close()
    
    #Verifications - since print isn't threadsafe the following
    #is pointless...  Just make sure IP doesn't throw.
    #f = open(file_name, "r")
    #lines = f.readlines()
    #for line in lines:
    #    Assert(line in expected_lines, line)


def test_write_buffer():
    from iptest.file_util import delete_files
    
    try:
        for mode in ('b', ''):
            foo = open('foo', 'w+' + mode)
            b = buffer(b'hello world', 6)
            foo.write(b)
            foo.close()
        
            foo = open('foo', 'r')
            AreEqual(foo.readlines(), ['world'])
            foo.close()
        
        foo = open('foo', 'w+')
        b = buffer(u'hello world', 6)
        foo.write(b)
        foo.close()
        
        foo = open('foo', 'r')
        AreEqual(foo.readlines(), ['world'])
        foo.close()
        
        foo = open('foo', 'w+b')
        b = buffer(u'hello world', 6)
        foo.write(b)
        foo.close()
        
        foo = open('foo', 'r')
        if is_cpython:
            AreEqual(foo.readlines(), ['l\x00o\x00 \x00w\x00o\x00r\x00l\x00d\x00'])
        else:
            AreEqual(foo.readlines(), ['world'])
        foo.close()

    finally:
        delete_files("foo")

def test_errors():
    try:
        file('some_file_that_really_does_not_exist')        
    except Exception, e:
        AreEqual(e.errno, 2)
    else:
        AssertUnreachable()

    try:
        file('path_too_long' * 100) 
    except Exception, e:
        AreEqual(e.errno, 2)
    else:
        AssertUnreachable()

def test_write_bytes():
    f = open("temp_ip", "w+")
    try:
        f.write(b"Hello\n")
        f.close()
        f = file('temp_ip')
        AreEqual(f.readlines(), ['Hello\n'])
        f.close()
    finally:
        nt.unlink('temp_ip')

def test_kw_args():
    file(name = 'some_test_file.txt', mode = 'w').close()
    nt.unlink('some_test_file.txt')

def test_buffering_kwparam():
    #--Positive
    for x in [-2147483648, -1, 0, 1, 2, 1024, 2147483646, 2147483647]:
        f = file(name = 'some_test_file.txt', mode = 'w', buffering=x)
        f.close()
        nt.unlink('some_test_file.txt')
    
    if is_cpython: #http://ironpython.codeplex.com/workitem/28214
        AssertErrorWithMessage(TypeError, "integer argument expected, got float",
                               file, 'some_test_file.txt', 'w', 3.14)
    else:
        f = file(name = 'some_test_file.txt', mode = 'w', buffering=3.14)
        f.close()
        nt.unlink('some_test_file.txt') 

    #--Negative
    for x in [None, "abc", u"", [], tuple()]:
        AssertError(TypeError, #"an integer is required",
                   lambda: file(name = 'some_test_file.txt', mode = 'w', buffering=x))
    
    for x in [2147483648, -2147483649]:
        AssertError(OverflowError, #"long int too large to convert to int",
                    lambda: file(name = 'some_test_file.txt', mode = 'w', buffering=x))

#------------------------------------------------------------------------------    
run_test(__name__)
