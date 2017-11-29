#####################################################################################
#
# Copyright (c) IronPython Team. All rights reserved.
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

##
## Test the io.StringIO
## based on cStringIO_test.py
##

import unittest

import io

from iptest import run_test

text = "Line 1\nLine 2\nLine 3\nLine 4\nLine 5"

class StringIOTest(unittest.TestCase):

    def call_close(self, i):
        self.assertEqual(i.closed, False)
        i.close()
        self.assertEqual(i.closed, True)
        i.close()
        self.assertEqual(i.closed, True)
        i.close()
        self.assertEqual(i.closed, True)

    def call_isatty(self, i):
        self.assertEqual(i.isatty(), False)

    # read
    def call_read(self, i):
        self.assertEqual(i.read(), text)
        self.assertEqual(i.read(), "")
        self.assertEqual(i.read(), "")
        i.close()
        i.close()
        self.assertRaises(ValueError, i.read)

    # readline
    def call_readline(self, i):
        self.assertEqual(i.readline(), "Line 1\n")
        self.assertEqual(i.readline(), "Line 2\n")
        self.assertEqual(i.readline(), "Line 3\n")
        self.assertEqual(i.readline(), "Line 4\n")
        self.assertEqual(i.readline(), "Line 5")
        self.assertEqual(i.readline(), "")
        i.close()
        self.assertRaises(ValueError, i.readline)

    def call_readline_n(self, i):
        self.assertEqual(i.readline(50), "Line 1\n")
        self.assertEqual(i.readline(0), "")
        self.assertEqual(i.readline(1), "L")
        self.assertEqual(i.readline(9), "ine 2\n")
        self.assertEqual(i.readline(50), "Line 3\n")
        self.assertEqual(i.readline(6), "Line 4")
        self.assertEqual(i.readline(50), "\n")
        self.assertEqual(i.readline(50), "Line 5")
        i.close()
        self.assertRaises(ValueError, i.readline)

    # readlines
    def call_readlines(self, i):
        self.assertEqual(i.readlines(), ["Line 1\n", "Line 2\n", "Line 3\n", "Line 4\n", "Line 5"])
        self.assertEqual(i.readlines(), [])
        i.close()
        self.assertRaises(ValueError, i.readlines)

    def call_readlines_n(self, i):
        self.assertEqual(i.readlines(10), ["Line 1\n", "Line 2\n"])
        self.assertEqual(i.readlines(50), ["Line 3\n", "Line 4\n", "Line 5"])
        self.assertEqual(i.readlines(50), [])
        i.close()
        self.assertRaises(ValueError, i.readlines)

    # getvalue
    def call_getvalue(self, i):
        self.assertEqual(i.getvalue(), text)
        self.assertEqual(i.read(6), "Line 1")
        self.assertEqual(i.getvalue(), text)
        i.close()
        self.assertRaises(ValueError, i.getvalue)

    # __iter__, next
    def call_next(self, i):
        self.assertEqual(i.__iter__(), i)
        self.assertEqual(i.next(), "Line 1\n")
        self.assertEqual(i.next(), "Line 2\n")
        self.assertEqual([l for l in i], ["Line 3\n", "Line 4\n", "Line 5"])
        i.close()
        self.assertRaises(ValueError, i.readlines)

    # read, readline, reset
    def call_reset(self, i):
        self.assertEqual(i.read(0), "")
        self.assertEqual(i.read(4), "Line")
        self.assertEqual(i.readline(), " 1\n")
        i.seek(0)
        self.assertEqual(i.read(4), "Line")
        self.assertEqual(i.readline(), " 1\n")
        i.seek(0)
        self.assertEqual(i.read(37),text)
        i.seek(0)
        self.assertEqual(i.read(38),text)
        i.seek(0)

    # seek, tell, read
    def call_seek_tell(self, i):
        self.assertEqual(i.read(4), "Line")
        self.assertEqual(i.tell(), 4)
        i.seek(10)
        self.assertEqual(i.tell(), 10)
        self.assertEqual(i.read(3), "e 2")
        i.seek(15, 0)
        self.assertEqual(i.tell(), 15)
        self.assertEqual(i.read(5), "ine 3")
        # seeking from current possition or from end is not supported unless offset is 0
        #i.seek(3, 1)
        #self.assertEqual(i.read(4), "ne 4")
        #i.seek(-5, 2)
        #self.assertEqual(i.tell(), len(text) - 5)
        #self.assertEqual(i.read(), "ine 5")
        i.seek(1000)
        self.assertEqual(i.tell(), 1000)
        self.assertEqual(i.read(), "")
        i.seek(2000, 0)
        self.assertEqual(i.tell(), 2000)
        self.assertEqual(i.read(), "")
        # seeking from current possition or from end is not supported unless offset is 0
        # i.seek(400, 1)
        #self.assertEqual(i.tell(), 2400)
        #self.assertEqual(i.read(), "")
        #i.seek(100, 2)
        #self.assertEqual(i.tell(), len(text) + 100)
        #self.assertEqual(i.read(), "")
        i.close()
        self.assertRaises(ValueError, i.tell)
        self.assertRaises(ValueError, i.seek, 0)
        self.assertRaises(ValueError, i.seek, 0, 2)

    # truncate
    def call_truncate(self, i):
        self.assertEqual(i.read(6), "Line 1")
        self.assertEqual(i.truncate(20), 20)
        # self.assertEqual(i.tell(), 20)
        self.assertEqual(i.getvalue(), "Line 1\nLine 2\nLine 3")
        i.truncate(30)
        self.assertEqual(i.tell(), 6)
        self.assertEqual(i.getvalue(), "Line 1\nLine 2\nLine 3")
        i.seek(0)
        self.assertEqual(i.tell(), 0)
        self.assertEqual(i.read(6), "Line 1")
        i.truncate()
        self.assertEqual(i.getvalue(), "Line 1")
        i.close()
        self.assertRaises(ValueError, i.truncate)
        self.assertRaises(ValueError, i.truncate, 10)

    # write
    def call_write(self, o):
        self.assertEqual(o.getvalue(), text)
        o.write("Data")
        self.assertRaises(TypeError, o.write, buffer(' 1'))
        self.assertRaises(TypeError, o.write, None)
        o.write(" 1")
        self.assertEqual(o.read(7), "\nLine 2")
        self.assertEqual(o.getvalue(), "Data 1\nLine 2\nLine 3\nLine 4\nLine 5")
        o.close()
        self.assertRaises(ValueError, o.write, "Hello")

    # writelines
    def call_writelines(self, o):
        self.assertEqual(o.getvalue(), text)
        o.writelines(["Data 1", "Data 2"])
        self.assertEqual(o.read(8), "2\nLine 3")
        self.assertEqual(o.getvalue(), "Data 1Data 22\nLine 3\nLine 4\nLine 5")
        self.assertRaises(TypeError, o.writelines, [buffer('foo')])
        self.assertRaises(TypeError, o.writelines, [None])
        o.close()
        self.assertRaises(ValueError, o.writelines, "Hello")
        self.assertRaises(ValueError, o.writelines, ['foo', buffer('foo')])
        self.assertRaises(ValueError, o.writelines, [buffer('foo')])

    # softspace
    def call_softspace(self, o):
        o.write("Hello")
        o.write("Hi")
        o.softspace = 1
        self.assertEqual(o.softspace, 1)
        self.assertEqual(o.getvalue(), "HelloHiLine 2\nLine 3\nLine 4\nLine 5")

    # flush
    def call_flush(self, i):
        i.flush()
        self.assertEqual(i,i)

    def init_StringI(self):
        return io.StringIO(text)

    def init_StringO(self):
        o = io.StringIO()
        o.write(text)
        o.seek(0)
        return o

    def init_emptyStringI(self):
        return io.StringIO("")

    def test_empty(self):
        i = self.init_emptyStringI()

        # test closed
        self.assertEqual(i.closed,False)
        i.close()
        self.assertEqual(i.closed,True)

        #test read
        i = self.init_emptyStringI()
        self.assertEqual(i.read(),"")
        i.close()
        self.assertRaises(ValueError, i.read)
        i.close()
        self.assertRaises(ValueError, i.read, 2)

        #test readline
        i = self.init_emptyStringI()
        self.assertEqual(i.readline(),"")
        i.close()
        self.assertRaises(ValueError, i.readline)

        i = self.init_emptyStringI()
        self.assertEqual(i.readline(0),"")
        i.close()
        self.assertRaises(ValueError, i.readline)

        #test readlines
        i = self.init_emptyStringI()
        self.assertEqual(i.readlines(),[])

        i = self.init_emptyStringI()
        self.assertEqual(i.readlines(0),[])

        #test getvalue
        i = self.init_emptyStringI()
        self.assertEqual(i.getvalue(),"")
        # getvalue does not accept argument
        # self.assertEqual(i.getvalue(True),"")
        i.close()
        self.assertRaises(ValueError, i.getvalue)

        #test iter
        i = self.init_emptyStringI()
        self.assertEqual(i.__iter__(), i)

        #test reset
        i = self.init_emptyStringI()
        self.assertEqual(i.read(0), "")
        i.seek(0)
        self.assertEqual(i.read(1), "")
        i.seek(0)
        self.assertEqual(i.readline(), "")
        i.close()
        self.assertRaises(ValueError, i.read, 2)
        self.assertRaises(ValueError, i.readline)

        #test seek,tell,read
        i = self.init_emptyStringI()
        self.assertEqual(i.read(0), "")
        self.assertEqual(i.tell(), 0)
        self.assertEqual(i.read(1), "")
        self.assertEqual(i.tell(), 0)
        i.seek(2)
        self.assertEqual(i.tell(), 2)
        self.assertEqual(i.read(),"")
        i.close()
        self.assertRaises(ValueError, i.tell)
        self.assertRaises(ValueError, i.seek, 0)
        self.assertRaises(ValueError, i.seek, 0, 2)

        #test truncate
        i = self.init_emptyStringI()
        i.truncate(0)
        self.assertEqual(i.tell(), 0)
        i.truncate(1)
        self.assertEqual(i.tell(), 0)
        i.close()
        self.assertRaises(ValueError, i.truncate)

    def test_cp8567(self):
        for x in ["", "1", "12", "12345"]:
            for i in [5, 6, 7, 2**8, 100, 2**16-1, 2**16, 2**16, 2**31-2, 2**31-1]:
                cio = io.StringIO(x)
                # make sure it doesn't thorow and it doesn't change seek position
                cio.truncate(i)
                self.assertEqual(cio.tell(), 0)
                cio.close()

    def test_i_o(self):
        for t in [  self.call_close,
                    self.call_isatty,
                    self.call_read,
                    self.call_readline,
                    self.call_readline_n,
                    self.call_readlines,
                    self.call_readlines_n,
                    self.call_getvalue,
                    self.call_next,
                    self.call_reset,
                    self.call_seek_tell,
                    self.call_truncate,
                    self.call_flush ]:
            i = self.init_StringI()
            t(i)

            o = self.init_StringO()
            t(o)

    def test_o(self):
        for t in [  self.call_write,
                    self.call_writelines,
                    self.call_softspace ]:
            o = self.init_StringO()
            t(o)

    def test_cp22017(self):
        m = io.StringIO()
        m.seek(2)
        m.write("hello!")
        self.assertEqual(m.getvalue(), '\x00\x00hello!')
        m.seek(2)
        self.assertEqual(m.getvalue(), '\x00\x00hello!')

    # tests from Jeffrey Bester, cp34683
    def test_read(self):
        # test stringio is readable
        with io.StringIO("hello world\r\n") as infile:
            self.assertSequenceEqual(infile.readline(), "hello world\r\n")

    def test_seekable(self):
        # test stringio is seekable
        with io.StringIO("hello") as infile:
            infile.seek(0, 2)
            infile.write(" world\r\n")
            self.assertSequenceEqual(infile.getvalue(), "hello world\r\n")

    def test_write(self):
        # test stringio is writable
        with io.StringIO() as output_file:
            output_file.write("hello")
            output_file.write(" world\n")
            self.assertSequenceEqual(output_file.getvalue(), "hello world\n")

    # test from cp26105
    def test_redirect(self):
        import sys
        stdout_save = sys.stdout
        capture = io.StringIO()
        sys.stdout = capture
        print "Testing"
        sys.stdout = stdout_save
        self.assertEqual(capture.getvalue(), "Testing\n")

run_test(__name__)
