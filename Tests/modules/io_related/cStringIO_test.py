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

##
## Test the cStringIO module
##

from iptest.assert_util import *
import cStringIO

text = "Line 1\nLine 2\nLine 3\nLine 4\nLine 5"

# close
def call_close(i):
    AreEqual(i.closed, False)
    i.close()
    AreEqual(i.closed, True)
    i.close()
    AreEqual(i.closed, True)
    i.close()
    AreEqual(i.closed, True)
    

def call_isatty(i):
    AreEqual(i.isatty(), False)


# read
def call_read(i):
    AreEqual(i.read(), text)
    AreEqual(i.read(), "")
    AreEqual(i.read(), "")
    i.close()
    i.close()
    AssertError(ValueError, i.read)
    
   

# readline
def call_readline(i):
    AreEqual(i.readline(), "Line 1\n")
    AreEqual(i.readline(), "Line 2\n")
    AreEqual(i.readline(), "Line 3\n")
    AreEqual(i.readline(), "Line 4\n")
    AreEqual(i.readline(), "Line 5")
    AreEqual(i.readline(), "")
    i.close()
    AssertError(ValueError, i.readline)

def call_readline_n(i):
    AreEqual(i.readline(50), "Line 1\n")
    AreEqual(i.readline(0), "")
    AreEqual(i.readline(1), "L")
    AreEqual(i.readline(9), "ine 2\n")
    AreEqual(i.readline(50), "Line 3\n")
    AreEqual(i.readline(6), "Line 4")
    AreEqual(i.readline(50), "\n")
    AreEqual(i.readline(50), "Line 5")
    i.close()
    AssertError(ValueError, i.readline)    

# readlines
def call_readlines(i):
    AreEqual(i.readlines(), ["Line 1\n", "Line 2\n", "Line 3\n", "Line 4\n", "Line 5"])
    AreEqual(i.readlines(), [])
    i.close()
    AssertError(ValueError, i.readlines)

def call_readlines_n(i):
    AreEqual(i.readlines(10), ["Line 1\n", "Line 2\n"])
    AreEqual(i.readlines(50), ["Line 3\n", "Line 4\n", "Line 5"])
    AreEqual(i.readlines(50), [])
    i.close()
    AssertError(ValueError, i.readlines)
    

# getvalue
def call_getvalue(i):
    AreEqual(i.getvalue(), text)
    AreEqual(i.read(6), "Line 1")
    AreEqual(i.getvalue(True), "Line 1")
    AreEqual(i.getvalue(), text)
    i.close()
    AssertError(ValueError, i.getvalue)
    
    

# __iter__, next
def call_next(i):
    AreEqual(i.__iter__(), i)
    AreEqual(i.next(), "Line 1\n")
    AreEqual(i.next(), "Line 2\n")
    AreEqual([l for l in i], ["Line 3\n", "Line 4\n", "Line 5"])
    i.close()
    AssertError(ValueError, i.readlines)
    
    

# read, readline, reset
def call_reset(i):
    AreEqual(i.read(0), "")
    AreEqual(i.read(4), "Line")
    AreEqual(i.readline(), " 1\n")
    i.reset()
    AreEqual(i.read(4), "Line")
    AreEqual(i.readline(), " 1\n")
    i.reset()
    AreEqual(i.read(37),text)
    i.reset()
    AreEqual(i.read(38),text)
    i.close()
    AssertError(ValueError, i.read, 5)
    AssertError(ValueError, i.readline)
    
    
    
    

# seek, tell, read
def call_seek_tell(i):
    AreEqual(i.read(4), "Line")
    AreEqual(i.tell(), 4)
    i.seek(10)
    AreEqual(i.tell(), 10)
    AreEqual(i.read(3), "e 2")
    i.seek(15, 0)
    AreEqual(i.tell(), 15)
    AreEqual(i.read(5), "ine 3")
    i.seek(3, 1)
    AreEqual(i.read(4), "ne 4")
    i.seek(-5, 2)
    AreEqual(i.tell(), len(text) - 5)
    AreEqual(i.read(), "ine 5")
    i.seek(1000)
    AreEqual(i.tell(), 1000)
    AreEqual(i.read(), "")
    i.seek(2000, 0)
    AreEqual(i.tell(), 2000)
    AreEqual(i.read(), "")
    i.seek(400, 1)
    AreEqual(i.tell(), 2400)
    AreEqual(i.read(), "")
    i.seek(100, 2)
    AreEqual(i.tell(), len(text) + 100)
    AreEqual(i.read(), "")
    i.close()
    AssertError(ValueError, i.tell)
    AssertError(ValueError, i.seek, 0)
    AssertError(ValueError, i.seek, 0, 2)
    
# truncate
def call_truncate(i):
    AreEqual(i.read(6), "Line 1")
    i.truncate(20)
    AreEqual(i.tell(), 20)
    AreEqual(i.getvalue(), "Line 1\nLine 2\nLine 3")
    i.truncate(30)
    AreEqual(i.tell(), 20)
    AreEqual(i.getvalue(), "Line 1\nLine 2\nLine 3")
    i.reset()
    AreEqual(i.tell(), 0)
    AreEqual(i.read(6), "Line 1")
    i.truncate()
    AreEqual(i.getvalue(), "Line 1")
    i.close()
    AssertError(ValueError, i.truncate)
    AssertError(ValueError, i.truncate, 10)
    
    
    
   

# write
def call_write(o):
    AreEqual(o.getvalue(), text)
    o.write("Data")
    o.write(buffer(' 1'))
    AssertError(TypeError, o.write, None)
    AreEqual(o.read(7), "\nLine 2")
    AreEqual(o.getvalue(), "Data 1\nLine 2\nLine 3\nLine 4\nLine 5")
    o.close()
    AssertError(ValueError, o.write, "Hello")

# writelines
def call_writelines(o):
    AreEqual(o.getvalue(), text)
    o.writelines(["Data 1", "Data 2"])
    AreEqual(o.read(8), "2\nLine 3")
    AreEqual(o.getvalue(), "Data 1Data 22\nLine 3\nLine 4\nLine 5")
    AssertError(TypeError, o.writelines, [buffer('foo')])
    AssertError(TypeError, o.writelines, [None])
    o.close()
    AssertError(ValueError, o.writelines, "Hello")
    AssertError(ValueError, o.writelines, ['foo', buffer('foo')])
    AssertError(TypeError, o.writelines, [buffer('foo')])

# softspace
def call_softspace(o):
    o.write("Hello")
    o.write("Hi")
    o.softspace = 1
    AreEqual(o.softspace, 1)
    AreEqual(o.getvalue(), "HelloHiLine 2\nLine 3\nLine 4\nLine 5")

# flush
def call_flush(i):
    i.flush()
    AreEqual(i,i)

def init_StringI():
    return cStringIO.StringIO(text)

def init_StringO():
    o = cStringIO.StringIO()
    o.write(text)
    o.reset()
    return o

def init_emptyStringI():
    return cStringIO.StringIO("")
    
def test_empty():
    i = init_emptyStringI()
    
    # test closed
    AreEqual(i.closed,False)
    i.close()
    AreEqual(i.closed,True)
    
    
    #test read
    i = init_emptyStringI()
    AreEqual(i.read(),"")
    i.close()
    AssertError(ValueError, i.read)
    i.close()
    AssertError(ValueError, i.read, 2)
    
    #test readline
    i = init_emptyStringI()
    AreEqual(i.readline(),"")
    i.close()
    AssertError(ValueError, i.readline)
    
    i = init_emptyStringI()
    AreEqual(i.readline(0),"")
    i.close()
    AssertError(ValueError, i.readline)
    
    #test readlines
    i = init_emptyStringI()
    AreEqual(i.readlines(),[])
    
    i = init_emptyStringI()
    AreEqual(i.readlines(0),[])
    
    #test getvalue
    i = init_emptyStringI()
    AreEqual(i.getvalue(),"")
    AreEqual(i.getvalue(True),"")
    i.close()
    AssertError(ValueError, i.getvalue)
    
    #test iter
    i = init_emptyStringI()
    AreEqual(i.__iter__(), i)
    
    #test reset
    i = init_emptyStringI()
    AreEqual(i.read(0), "")
    i.reset()
    AreEqual(i.read(1), "")
    i.reset()
    AreEqual(i.readline(), "")
    i.close()
    AssertError(ValueError, i.read, 2)
    AssertError(ValueError, i.readline)
    
    #test seek,tell,read
    i = init_emptyStringI()
    AreEqual(i.read(0), "")
    AreEqual(i.tell(), 0)
    AreEqual(i.read(1), "")
    AreEqual(i.tell(), 0)
    i.seek(2)
    AreEqual(i.tell(), 2)
    AreEqual(i.read(),"")
    i.close()
    AssertError(ValueError, i.tell)
    AssertError(ValueError, i.seek, 0)
    AssertError(ValueError, i.seek, 0, 2)
    
    #test truncate
    i = init_emptyStringI()
    i.truncate(0)
    AreEqual(i.tell(), 0)
    i.truncate(1)
    AreEqual(i.tell(), 0)
    i.close()
    AssertError(ValueError, i.truncate)
    
def test_cp8567():
    for x in ["", "1", "12", "12345", 
                #u"123", #CodePlex 19220
                ]:
        for i in [5, 6, 7, 2**8, 100, 2**16-1, 2**16, 2**16, 2**31-2, 2**31-1]:
            cio = cStringIO.StringIO(x)
            cio.truncate(i)
            AreEqual(cio.tell(), len(x))
            cio.close()
    
    
    
def test_i_o():
    for t in [  call_close,
                call_isatty,
                call_read,
                call_readline,
                call_readline_n,
                call_readlines,
                call_readlines_n,
                call_getvalue,
                call_next,
                call_reset,
                call_seek_tell,
                call_truncate,
                call_flush ]:
        i = init_StringI()
        t(i)
        
        o= init_StringO()
        t(o)

def test_o():
    for t in [  call_write,
                call_writelines,
                call_softspace ]:
        o = init_StringO()
        t(o)

def test_cp22017():
    m = cStringIO.StringIO()
    m.seek(2)
    m.write("hello!")
    AreEqual(m.getvalue(), '\x00\x00hello!')
    m.seek(2)
    AreEqual(m.getvalue(), '\x00\x00hello!')

run_test(__name__)
