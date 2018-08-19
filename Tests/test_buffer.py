# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import array
import unittest

from iptest import IronPythonTestCase, is_cli, run_test

class BufferTest(IronPythonTestCase):
    def test_negative(self):
        self.assertRaises(TypeError, buffer, None)
        self.assertRaises(TypeError, buffer, None, 0)
        self.assertRaises(TypeError, buffer, None, 0, 0)
        self.assertRaises(ValueError, buffer, "abc", -1) #offset < 0
        self.assertRaises(ValueError, buffer, "abc", -1, 0) #offset < 0
        #size < -1; -1 is allowed since that is the way to ask for the default value
        self.assertRaises(ValueError, buffer, "abc", 0, -2)

    def test_len(self):
        testData = ('hello world', array.array('b', 'hello world'), buffer('hello world'), buffer('abchello world', 3))
        if is_cli:
            import System
            testData += (System.Array[System.Char]('hello world'), )
        
        for x in testData:        
            b = buffer(x, 6)
            self.assertEqual(len(b), 5)
            b = buffer(x, 6, 2)
            self.assertEqual(len(b), 2)

        self.assertEqual(len(buffer("abc", 5)), 0)
        self.assertEqual(len(buffer("abc", 5, 50)), 0)

    def test_pass_in_string(self):
        b = buffer("abc", 0, -1)
        self.assertEqual(str(b), "abc")
        self.assertEqual(len(b), 3)

        b1 = buffer("abc")
        self.assertEqual(str(b1), "abc")
        b2 = buffer("def", 0)
        self.assertEqual(str(b2), "def")
        
        b3 = b1 + b2
        self.assertEqual(str(b3), "abcdef")
        b4 = 2 * (b2 * 2)
        self.assertEqual(str(b4), "defdefdefdef")
        b5 = 2 * b2
        self.assertEqual(str(b5), 'defdef')

    def test_pass_in_buffer(self):
        a = buffer("abc")
        
        b = buffer(a, 0, 2)
        self.assertEqual("ab", str(b))
        
        c = buffer(b, 0, 1)
        self.assertEqual("a", str(c))
        
        d = buffer(b, 0, 100)
        self.assertEqual("ab", str(d))
        
        e = buffer(a, 1, 2)
        self.assertEqual(str(e), "bc")
        
        e = buffer(a, 1, 5)
        self.assertEqual(str(e), "bc")
        
        e = buffer(a, 1, -1)
        self.assertEqual(str(e), "bc")
        
        e = buffer(a, 1, 0)
        self.assertEqual(str(e), "")

        e = buffer(a, 1, 1)
        self.assertEqual(str(e), "b")


    @unittest.skipUnless(is_cli, 'IronPython specific test')
    def test_pass_in_clrarray(self):
        import System
        a1 = System.Array[int]([1,2])
        arrbuff1 = buffer(a1, 0, 5)
        self.assertEqual(1, arrbuff1[0])
        self.assertEqual(2, arrbuff1[1])

        a2 = System.Array[System.String](["a","b"])
        arrbuff2 = buffer(a2, 0, 2)
        self.assertEqual("a", arrbuff2[0])
        self.assertEqual("b", arrbuff2[1])

        self.assertEqual(len(arrbuff1), len(arrbuff2))

        arrbuff1 = buffer(a1, 1, 1)
        self.assertEqual(2, arrbuff1[0])
        self.assertEqual(len(arrbuff1), 1)
        
        arrbuff1 = buffer(a1, 0, -1)
        self.assertEqual(1, arrbuff1[0])
        self.assertEqual(2, arrbuff1[1])
        self.assertEqual(len(arrbuff1), 2)

        a3 = System.Array[System.Guid]([])
        self.assertRaises(TypeError, buffer, a3)
            
    def test_equality(self):
        x = buffer('abc')
        self.assertEqual(x == None, False)
        self.assertEqual(None == x, False)
        self.assertEqual(x == x, True)
        

    def test_buffer_add(self):
        self.assertEqual(buffer('abc') + 'def', 'abcdef')
        arr = array.array('b', [1,2,3,4,5])
        self.assertEqual(buffer(arr) + 'abc', '\x01\x02\x03\x04\x05abc')
        
    def test_buffer_tostr(self):
        self.assertEqual(str(buffer('abc')), 'abc')
        self.assertEqual(str(buffer(array.array('b', [1,2,3,4,5]))), '\x01\x02\x03\x04\x05')


    def test_buffer_bytes(self):
        for x in (b'abc', bytearray(b'abc')):
            self.assertEqual(str(buffer(x)), 'abc')
            self.assertEqual(buffer(x)[0:1], b'a')
            

    def test_write_file(self):
        inputs = [buffer('abcdef'), buffer(b'abcdef'), buffer(bytearray(b'abcdef')), buffer(array.array('b', 'abcdef'))]
        text_inputs = [array.array('b', 'abcdef'), array.array('c', 'abcdef')]
        #if is_cli:
        #    inputs.append(System.Array[System.Char]('abcdef'))

        for inp in inputs + text_inputs:
            with open('foo', 'wb') as f:
                f.write(inp)
            
            with open('foo') as f:
                self.assertEqual(f.readlines(), ['abcdef'])
        
        # TODO: Arrays not allowed in non-binary mode
        # buffer(array(...)) is currently allowed because disallowing it
        # would require some hacks.
        for inp in inputs:
            with open('foo', 'w') as f:
                f.write(inp)
            
            with open('foo') as f:
                self.assertEqual(f.readlines(), ['abcdef'])
            
        for inp in text_inputs:
            with open('foo', 'w') as f:
                self.assertRaises(TypeError, f.write, inp)

        self.delete_files('foo')

run_test(__name__)
