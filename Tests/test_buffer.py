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
## Test builtin-type buffer
##

from iptest.assert_util import *
import array

def test_negative():
    AssertError(TypeError, buffer, None)
    AssertError(TypeError, buffer, None, 0)
    AssertError(TypeError, buffer, None, 0, 0)
    AssertError(ValueError, buffer, "abc", -1) #offset < 0
    AssertError(ValueError, buffer, "abc", -1, 0) #offset < 0
    #size < -1; -1 is allowed since that is the way to ask for the default value
    AssertError(ValueError, buffer, "abc", 0, -2)

def test_len():
    testData = ('hello world', array.array('b', 'hello world'), buffer('hello world'), buffer('abchello world', 3))
    if is_cli:
        import System
        testData += (System.Array[System.Char]('hello world'), )
    
    for x in testData:        
        b = buffer(x, 6)
        AreEqual(len(b), 5)
        b = buffer(x, 6, 2)
        AreEqual(len(b), 2)

    AreEqual(len(buffer("abc", 5)), 0)
    AreEqual(len(buffer("abc", 5, 50)), 0)

def test_pass_in_string():
    b = buffer("abc", 0, -1)
    AreEqual(str(b), "abc")
    AreEqual(len(b), 3)

    b1 = buffer("abc")
    AreEqual(str(b1), "abc")
    b2 = buffer("def", 0)
    AreEqual(str(b2), "def")
    
    b3 = b1 + b2
    AreEqual(str(b3), "abcdef")
    b4 = 2 * (b2 * 2)
    AreEqual(str(b4), "defdefdefdef")
    b5 = 2 * b2
    AreEqual(str(b5), 'defdef')

def test_pass_in_buffer():
    a = buffer("abc")
    
    b = buffer(a, 0, 2)
    AreEqual("ab", str(b))
    
    c = buffer(b, 0, 1)
    AreEqual("a", str(c))
    
    d = buffer(b, 0, 100)
    AreEqual("ab", str(d))
    
    e = buffer(a, 1, 2)
    AreEqual(str(e), "bc")
    
    e = buffer(a, 1, 5)
    AreEqual(str(e), "bc")
    
    e = buffer(a, 1, -1)
    AreEqual(str(e), "bc")
    
    e = buffer(a, 1, 0)
    AreEqual(str(e), "")

    e = buffer(a, 1, 1)
    AreEqual(str(e), "b")


@skip('win32')
def test_pass_in_clrarray():
    import System
    a1 = System.Array[int]([1,2])
    arrbuff1 = buffer(a1, 0, 5)
    AreEqual(1, arrbuff1[0])
    AreEqual(2, arrbuff1[1])

    a2 = System.Array[System.String](["a","b"])
    arrbuff2 = buffer(a2, 0, 2)
    AreEqual("a", arrbuff2[0])
    AreEqual("b", arrbuff2[1])

    AreEqual(len(arrbuff1), len(arrbuff2))

    arrbuff1 = buffer(a1, 1, 1)
    AreEqual(2, arrbuff1[0])
    AreEqual(len(arrbuff1), 1)
    
    arrbuff1 = buffer(a1, 0, -1)
    AreEqual(1, arrbuff1[0])
    AreEqual(2, arrbuff1[1])
    AreEqual(len(arrbuff1), 2)

    a3 = System.Array[System.Guid]([])
    AssertError(TypeError, buffer, a3)
        
def test_equality():
    x = buffer('abc')
    AreEqual(x == None, False)
    AreEqual(None == x, False)
    AreEqual(x == x, True)
    

def test_buffer_add():
    AreEqual(buffer('abc') + 'def', 'abcdef')
    import array
    arr = array.array('b', [1,2,3,4,5])
    AreEqual(buffer(arr) + 'abc', '\x01\x02\x03\x04\x05abc')
    
def test_buffer_tostr():
    import array
    AreEqual(str(buffer('abc')), 'abc')
    AreEqual(str(buffer(array.array('b', [1,2,3,4,5]))), '\x01\x02\x03\x04\x05')


def test_buffer_bytes():
    for x in (b'abc', bytearray(b'abc')):
        AreEqual(str(buffer(x)), 'abc')
        AreEqual(buffer(x)[0:1], b'a')
        
@skip("silverlight")
def test_write_file():
    inputs = [buffer('abcdef'), buffer(b'abcdef'), buffer(bytearray(b'abcdef')), buffer(array.array('b', 'abcdef'))]
    text_inputs = [array.array('b', 'abcdef'), array.array('c', 'abcdef')]
    #if is_cli:
    #    inputs.append(System.Array[System.Char]('abcdef'))

    for inp in inputs + text_inputs:
        f = file('foo', 'wb')
        f.write(inp)
        f.close()
        f = file('foo')
        AreEqual(f.readlines(), ['abcdef'])
        f.close()
        
    
    # TODO: Arrays not allowed in non-binary mode
    # buffer(array(...)) is currently allowed because disallowing it
    # would require some hacks.
    for inp in inputs:
        f = file('foo', 'w')
        f.write(inp)
        f.close()
        f = file('foo')
        AreEqual(f.readlines(), ['abcdef'])
        f.close()

    for inp in text_inputs:
        f = file('foo', 'w')
        
        AssertError(TypeError, f.write, inp)
        f.close()
        
run_test(__name__)
