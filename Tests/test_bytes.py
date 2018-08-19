# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import sys
import unittest

from iptest import IronPythonTestCase, ip_supported_encodings, is_cli, is_mono, is_osx, run_test

types = [bytearray, bytes]
class IndexableOC:
    def __init__(self, value):
        self.value = value
    def __index__(self):
        return self.value

class Indexable(object):
    def __init__(self, value):
        self.value = value
    def __index__(self):
        return self.value

class BytesTest(IronPythonTestCase):

    def test_capitalize(self):
        tests = [(b'foo', b'Foo'), 
                (b' foo', b' foo'),
                (b'fOO', b'Foo'),
                (b' fOO BAR', b' foo bar'),
                (b'fOO BAR', b'Foo bar'),
                ]
        
        for testType in types:
            for data, result in tests:
                self.assertEqual(testType(data).capitalize(), result)
                
        y = b''
        x = y.capitalize()
        self.assertEqual(id(x), id(y))
        
        y = bytearray(b'')
        x = y.capitalize()
        self.assertTrue(id(x) != id(y), "bytearray.capitalize returned self")

    def test_center(self):
        for testType in types:
            self.assertEqual(testType(b'aa').center(4), b' aa ')
            self.assertEqual(testType(b'aa').center(4, b'*'), b'*aa*')
            self.assertEqual(testType(b'aa').center(4, '*'), b'*aa*')
            self.assertEqual(testType(b'aa').center(2), b'aa')
            self.assertEqual(testType(b'aa').center(2, '*'), b'aa')
            self.assertEqual(testType(b'aa').center(2, b'*'), b'aa')
            self.assertRaises(TypeError, testType(b'abc').center, 3, [2, ])
        
        x = b'aa'
        self.assertEqual(id(x.center(2, '*')), id(x))
        self.assertEqual(id(x.center(2, b'*')), id(x))
        
        x = bytearray(b'aa')
        self.assertTrue(id(x.center(2, '*')) != id(x))
        self.assertTrue(id(x.center(2, b'*')) != id(x))
        
        

    def test_count(self):
        for testType in types:
            self.assertEqual(testType(b"adadad").count(b"d"), 3)
            self.assertEqual(testType(b"adbaddads").count(b"ad"), 3)
            self.assertEqual(testType(b"adbaddads").count(b"ad", 1, 8), 2)
            self.assertEqual(testType(b"adbaddads").count(b"ad", -1, -1), 0)
            self.assertEqual(testType(b"adbaddads").count(b"ad", 0, -1), 3)
            self.assertEqual(testType(b"adbaddads").count(b"", 0, -1), 9)
            self.assertEqual(testType(b"adbaddads").count(b"", 27), 0)
            
            self.assertRaises(TypeError, testType(b"adbaddads").count, [2,])
            self.assertRaises(TypeError, testType(b"adbaddads").count, [2,], 0)
            self.assertRaises(TypeError, testType(b"adbaddads").count, [2,], 0, 1)

    def test_decode(self):
        for testType in types:
            self.assertEqual(testType(b'\xff\xfea\x00b\x00c\x00').decode('utf-16'), 'abc')

    def test_endswith(self):
        for testType in types:
            self.assertRaises(TypeError, testType(b'abcdef').endswith, ([], ))
            self.assertRaises(TypeError, testType(b'abcdef').endswith, [])
            self.assertRaises(TypeError, testType(b'abcdef').endswith, [], 0)
            self.assertRaises(TypeError, testType(b'abcdef').endswith, [], 0, 1)
            self.assertEqual(testType(b'abcdef').endswith(b'def'), True)
            self.assertEqual(testType(b'abcdef').endswith(b'def', -1, -2), False)
            self.assertEqual(testType(b'abcdef').endswith(b'def', 0, 42), True)
            self.assertEqual(testType(b'abcdef').endswith(b'def', 0, -7), False)
            self.assertEqual(testType(b'abcdef').endswith(b'def', 42, -7), False)
            self.assertEqual(testType(b'abcdef').endswith(b'def', 42), False)
            self.assertEqual(testType(b'abcdef').endswith(b'bar'), False)
            self.assertEqual(testType(b'abcdef').endswith((b'def', )), True)
            self.assertEqual(testType(b'abcdef').endswith((b'baz', )), False)
            self.assertEqual(testType(b'abcdef').endswith((b'baz', ), 0, 42), False)
            self.assertEqual(testType(b'abcdef').endswith((b'baz', ), 0, -42), False)

                    
            for x in (0, 1, 2, 3, -10, -3, -4):
                self.assertEqual(testType(b"abcdef").endswith(b"def", x), True)
                self.assertEqual(testType(b"abcdef").endswith(b"de", x, 5), True)
                self.assertEqual(testType(b"abcdef").endswith(b"de", x, -1), True)
                self.assertEqual(testType(b"abcdef").endswith((b"def", ), x), True)
                self.assertEqual(testType(b"abcdef").endswith((b"de", ), x, 5), True)
                self.assertEqual(testType(b"abcdef").endswith((b"de", ), x, -1), True)
        
            for x in (4, 5, 6, 10, -1, -2):
                self.assertEqual(testType(b"abcdef").endswith((b"def", ), x), False)
                self.assertEqual(testType(b"abcdef").endswith((b"de", ), x, 5), False)
                self.assertEqual(testType(b"abcdef").endswith((b"de", ), x, -1), False)

    def test_expandtabs(self):
        for testType in types:
            self.assertTrue(testType(b"\ttext\t").expandtabs(0) == b"text")
            self.assertTrue(testType(b"\ttext\t").expandtabs(-10) == b"text")
            self.assertEqual(testType(b"\r\ntext\t").expandtabs(-10), b"\r\ntext")
            
            self.assertEqual(len(testType(b"aaa\taaa\taaa").expandtabs()), 19)
            self.assertEqual(testType(b"aaa\taaa\taaa").expandtabs(), b"aaa     aaa     aaa")
            self.assertRaises(OverflowError, bytearray(b'\t\t').expandtabs, sys.maxsize)

    def test_extend(self):
        b = bytearray(b'abc')
        b.extend(b'def')
        self.assertEqual(b, b'abcdef')
        b.extend(bytearray(b'ghi'))
        self.assertEqual(b, b'abcdefghi')
        b = bytearray(b'abc')
        b.extend([2,3,4])
        self.assertEqual(b, b'abc' + b'\x02\x03\x04')
        
    def test_find(self):
        for testType in types:
            self.assertEqual(testType(b"abcdbcda").find(b"cd", 1), 2)
            self.assertEqual(testType(b"abcdbcda").find(b"cd", 3), 5)
            self.assertEqual(testType(b"abcdbcda").find(b"cd", 7), -1)
            self.assertEqual(testType(b'abc').find(b'abc', -1, 1), -1)
            self.assertEqual(testType(b'abc').find(b'abc', 25), -1)
            self.assertEqual(testType(b'abc').find(b'add', 0, 3), -1)
            if testType == bytes:
                self.assertEqual(testType(b'abc').find(b'add', 0, None), -1)
                self.assertEqual(testType(b'abc').find(b'add', None, None), -1)
                self.assertEqual(testType(b'abc').find(b'', None, 0), 0)
                self.assertEqual(testType(b'x').find(b'x', None, 0), -1)
            
            self.assertEqual(testType(b'abc').find(b'', 0, 0), 0)
            self.assertEqual(testType(b'abc').find(b'', 0, 1), 0)
            self.assertEqual(testType(b'abc').find(b'', 0, 2), 0)
            self.assertEqual(testType(b'abc').find(b'', 0, 3), 0)
            self.assertEqual(testType(b'abc').find(b'', 0, 4), 0)
            self.assertEqual(testType(b'').find(b'', 0, 4), 0)
            
            self.assertEqual(testType(b'x').find(b'x', 0, 0), -1)
                    
            self.assertEqual(testType(b'x').find(b'x', 3, 0), -1)
            self.assertEqual(testType(b'x').find(b'', 3, 0), -1)
            
            self.assertRaises(TypeError, testType(b'x').find, [1])
            self.assertRaises(TypeError, testType(b'x').find, [1], 0)
            self.assertRaises(TypeError, testType(b'x').find, [1], 0, 1)
            
    def test_fromhex(self):
        for testType in types:
            if testType != str:
                self.assertRaises(ValueError, testType.fromhex, '0')
                self.assertRaises(ValueError, testType.fromhex, 'A')
                self.assertRaises(ValueError, testType.fromhex, 'a')
                self.assertRaises(ValueError, testType.fromhex, 'aG')
                self.assertRaises(ValueError, testType.fromhex, 'Ga')
                
                self.assertEqual(testType.fromhex('00'), b'\x00')
                self.assertEqual(testType.fromhex('00 '), b'\x00')
                self.assertEqual(testType.fromhex('00  '), b'\x00')
                self.assertEqual(testType.fromhex('00  01'), b'\x00\x01')
                self.assertEqual(testType.fromhex('00  01 0a'), b'\x00\x01\x0a')
                self.assertEqual(testType.fromhex('00  01 0a 0B'), b'\x00\x01\x0a\x0B')
                self.assertEqual(testType.fromhex('00  a1 Aa 0B'), b'\x00\xA1\xAa\x0B')

    def test_index(self):
        for testType in types:
            self.assertRaises(TypeError, testType(b'abc').index, 257)
            self.assertEqual(testType(b'abc').index(b'a'), 0)
            self.assertEqual(testType(b'abc').index(b'a', 0, -1), 0)
            
            self.assertRaises(ValueError, testType(b'abc').index, b'c', 0, -1)
            self.assertRaises(ValueError, testType(b'abc').index, b'a', -1)
            
            self.assertEqual(testType(b'abc').index(b'ab'), 0)
            self.assertEqual(testType(b'abc').index(b'bc'), 1)
            self.assertRaises(ValueError, testType(b'abc').index, b'abcd')
            self.assertRaises(ValueError, testType(b'abc').index, b'e')

            self.assertRaises(TypeError, testType(b'x').index, [1])
            self.assertRaises(TypeError, testType(b'x').index, [1], 0)
            self.assertRaises(TypeError, testType(b'x').index, [1], 0, 1)

    def test_insert(self):
        b = bytearray(b'abc')
        b.insert(0, ord('d'))
        self.assertEqual(b, b'dabc')

        b.insert(1000, ord('d'))
        self.assertEqual(b, b'dabcd')

        b.insert(-1, ord('d'))
        self.assertEqual(b, b'dabcdd')
        
        self.assertRaises(ValueError, b.insert, 0, 256)

    def check_is_method(self, methodName, result):
        for testType in types:
            self.assertEqual(getattr(testType(b''), methodName)(), False)
            for i in range(256):
                data = bytearray()
                data.append(i)
                
                self.assertTrue(getattr(testType(data), methodName)() == result(i), chr(i) + " (" + str(i) + ") should be " + str(result(i)))
        
    def test_isalnum(self):
        self.check_is_method('isalnum', lambda i : i >= ord('a') and i <= ord('z') or i >= ord('A') and i <= ord('Z') or i >= ord('0') and i <= ord('9'))
        
    def test_isalpha(self):
        self.check_is_method('isalpha', lambda i : i >= ord('a') and i <= ord('z') or i >= ord('A') and i <= ord('Z'))

    def test_isdigit(self):
        self.check_is_method('isdigit', lambda i : (i >= ord('0') and i <= ord('9')))

    def test_islower(self):
        self.check_is_method('islower', lambda i : i >= ord('a') and i <= ord('z'))
        for testType in types:
            for i in range(256):
                if not chr(i).isupper():
                    self.assertEqual((testType(b'a') + testType([i])).islower(), True)
        
    def test_isspace(self):
        self.check_is_method('isspace', lambda i : i in [ord(' '), ord('\t'), ord('\f'), ord('\n'), ord('\r'), 11])
        for testType in types:
            for i in range(256):
                if not chr(i).islower():
                    self.assertEqual((testType(b'A') + testType([i])).isupper(), True)

    def test_istitle(self):
        for testType in types:
            self.assertEqual(testType(b'').istitle(), False)
            self.assertEqual(testType(b'Foo').istitle(), True)
            self.assertEqual(testType(b'Foo Bar').istitle(), True)
            self.assertEqual(testType(b'FooBar').istitle(), False)
            self.assertEqual(testType(b'foo').istitle(), False)

    def test_isupper(self):
        self.check_is_method('isupper', lambda i : i >= ord('A') and i <= ord('Z'))

    def test_join(self):
        x = b''
        self.assertEqual(id(x.join(b'')), id(x))

        x = bytearray(x)
        self.assertTrue(id(x.join(b'')) != id(x))

        x = b'abc'
        self.assertEqual(id(b'foo'.join([x])), id(x))

        self.assertRaises(TypeError, b'foo'.join, [42])
        
        x = bytearray(b'foo')
        self.assertTrue(id(bytearray(b'foo').join([x])) != id(x), "got back same object on single arg join w/ bytearray")
        
        for testType in types:
            self.assertEqual(testType(b'x').join([b'd', b'e', b'f']), b'dxexf')
            self.assertEqual(testType(b'x').join([b'd', b'e', b'f']), b'dxexf')
            self.assertEqual(type(testType(b'x').join([b'd', b'e', b'f'])), testType)
            if str != bytes:
                # works in Py3k/Ipy, not in Py2.6
                self.assertEqual(b'x'.join([testType(b'd'), testType(b'e'), testType(b'f')]), b'dxexf')
            self.assertEqual(bytearray(b'x').join([testType(b'd'), testType(b'e'), testType(b'f')]), b'dxexf')
            self.assertEqual(testType(b'').join([]), b'')
            self.assertEqual(testType(b'').join((b'abc', )), b'abc')
            self.assertEqual(testType(b'').join((b'abc', b'def')), b'abcdef')
            self.assertRaises(TypeError, testType(b'').join, (42, ))

    def test_ljust(self):
        for testType in types:
            self.assertRaises(TypeError, testType(b'').ljust, 42, '  ')
            self.assertRaises(TypeError, testType(b'').ljust, 42, b'  ')
            self.assertRaises(TypeError, testType(b'').ljust, 42, '\u0100')
            self.assertEqual(testType(b'abc').ljust(4), b'abc ')
            self.assertEqual(testType(b'abc').ljust(4, b'x'), b'abcx')
            self.assertEqual(testType(b'abc').ljust(4, 'x'), b'abcx')
        
        x = b'abc'
        self.assertEqual(id(x.ljust(2)), id(x))
        
        x = bytearray(x)
        self.assertTrue(id(x.ljust(2)) != id(x))

    def test_lower(self):
        expected = b'\x00\x01\x02\x03\x04\x05\x06\x07\x08\t\n\x0b\x0c\r\x0e\x0f'  \
        b'\x10\x11\x12\x13\x14\x15\x16\x17\x18\x19\x1a\x1b\x1c\x1d\x1e\x1f !"#$%' \
        b'&\'()*+,-./0123456789:;<=>?@abcdefghijklmnopqrstuvwxyz[\\]^_`'          \
        b'abcdefghijklmnopqrstuvwxyz{|}~\x7f\x80\x81\x82\x83\x84\x85\x86\x87\x88' \
        b'\x89\x8a\x8b\x8c\x8d\x8e\x8f\x90\x91\x92\x93\x94\x95\x96\x97\x98\x99'   \
        b'\x9a\x9b\x9c\x9d\x9e\x9f\xa0\xa1\xa2\xa3\xa4\xa5\xa6\xa7\xa8\xa9\xaa'   \
        b'\xab\xac\xad\xae\xaf\xb0\xb1\xb2\xb3\xb4\xb5\xb6\xb7\xb8\xb9\xba\xbb'   \
        b'\xbc\xbd\xbe\xbf\xc0\xc1\xc2\xc3\xc4\xc5\xc6\xc7\xc8\xc9\xca\xcb\xcc'   \
        b'\xcd\xce\xcf\xd0\xd1\xd2\xd3\xd4\xd5\xd6\xd7\xd8\xd9\xda\xdb\xdc\xdd'   \
        b'\xde\xdf\xe0\xe1\xe2\xe3\xe4\xe5\xe6\xe7\xe8\xe9\xea\xeb\xec\xed\xee'   \
        b'\xef\xf0\xf1\xf2\xf3\xf4\xf5\xf6\xf7\xf8\xf9\xfa\xfb\xfc\xfd\xfe\xff'
        
        data = bytearray()
        for i in range(256):
            data.append(i)
        
        for testType in types:
            self.assertEqual(testType(data).lower(), expected)
        
    def test_lstrip(self):
        for testType in types:
            self.assertEqual(testType(b' abc').lstrip(), b'abc')
            self.assertEqual(testType(b' abc ').lstrip(), b'abc ')
            self.assertEqual(testType(b' ').lstrip(), b'')

        x = b'abc'
        self.assertEqual(id(x.lstrip()), id(x))

        x = bytearray(x)
        self.assertTrue(id(x.lstrip()) != id(x))

    def test_partition(self):
        for testType in types:
            self.assertRaises(TypeError, testType(b'').partition, None)        
            self.assertRaises(ValueError, testType(b'').partition, b'')
            self.assertRaises(ValueError, testType(b'').partition, b'')
            
            if testType == bytearray:
                self.assertEqual(testType(b'a\x01c').partition([1]), (b'a', b'\x01', b'c'))
            else:
                self.assertRaises(TypeError, testType(b'a\x01c').partition, [1])
            
            self.assertEqual(testType(b'abc').partition(b'b'), (b'a', b'b', b'c'))
            self.assertEqual(testType(b'abc').partition(b'd'), (b'abc', b'', b''))
            
            x = testType(b'abc')
            one, two, three = x.partition(b'd')
            if testType == bytearray:
                self.assertTrue(id(one) != id(x))
            else:
                self.assertEqual(id(one), id(x))
        
        one, two, three = b''.partition(b'abc')
        self.assertEqual(id(one), id(two))
        self.assertEqual(id(two), id(three))

        one, two, three = bytearray().partition(b'abc')
        self.assertTrue(id(one) != id(two))
        self.assertTrue(id(two) != id(three))
        self.assertTrue(id(three) != id(one))

    def test_pop(self):
        b = bytearray()
        self.assertRaises(IndexError, b.pop)
        self.assertRaises(IndexError, b.pop, 0)
        
        b = bytearray(b'abc')
        self.assertEqual(b.pop(), ord('c'))
        self.assertEqual(b, b'ab')
        
        b = bytearray(b'abc')
        b.pop(1)
        self.assertEqual(b, b'ac')

        b = bytearray(b'abc')
        b.pop(-1)
        self.assertEqual(b, b'ab')

    def test_replace(self):
        for testType in types:
            self.assertRaises(TypeError, testType(b'abc').replace, None, b'abc')
            self.assertRaises(TypeError, testType(b'abc').replace, b'abc', None)
            self.assertRaises(TypeError, testType(b'abc').replace, None, b'abc', 1)
            self.assertRaises(TypeError, testType(b'abc').replace, b'abc', None, 1)
            self.assertRaises(TypeError, testType(b'abc').replace, [1], b'abc')
            self.assertRaises(TypeError, testType(b'abc').replace, b'abc', [1])
            self.assertRaises(TypeError, testType(b'abc').replace, [1], b'abc', 1)
            self.assertRaises(TypeError, testType(b'abc').replace, b'abc', [1], 1)
                    
            self.assertEqual(testType(b'abc').replace(b'b', b'foo'), b'afooc')
            self.assertEqual(testType(b'abc').replace(b'b', b''), b'ac')
            self.assertEqual(testType(b'abcb').replace(b'b', b'foo', 1), b'afoocb')
            self.assertEqual(testType(b'abcb').replace(b'b', b'foo', 2), b'afoocfoo')
            self.assertEqual(testType(b'abcb').replace(b'b', b'foo', 3), b'afoocfoo')
            self.assertEqual(testType(b'abcb').replace(b'b', b'foo', -1), b'afoocfoo')
            self.assertEqual(testType(b'abcb').replace(b'', b'foo', 100), b'fooafoobfoocfoobfoo')
            self.assertEqual(testType(b'abcb').replace(b'', b'foo', 0), b'abcb')
            self.assertEqual(testType(b'abcb').replace(b'', b'foo', 1), b'fooabcb')
            
            self.assertEqual(testType(b'ooooooo').replace(b'o', b'u'), b'uuuuuuu')
        
        x = b'abc'
        self.assertEqual(id(x.replace(b'foo', b'bar', 0)), id(x))
        
        if is_cli:
            # CPython bug in 2.6 - http://bugs.python.org/issue4348
            x = bytearray(b'abc')
            self.assertTrue(id(x.replace(b'foo', b'bar', 0)) != id(x))

    def test_remove(self):
        for toremove in (ord('a'), b'a', Indexable(ord('a')), IndexableOC(ord('a'))):    
            b = bytearray(b'abc')
            b.remove(ord('a'))
            self.assertEqual(b, b'bc')
        
        self.assertRaises(ValueError, b.remove, ord('x'))

        b = bytearray(b'abc')
        self.assertRaises(TypeError, b.remove, bytearray(b'a'))

    def test_reverse(self):
        b = bytearray(b'abc')
        b.reverse()
        self.assertEqual(b, b'cba')    
        
    # CoreCLR bug xxxx found in build 30324 from silverlight_w2
    def test_rfind(self):
        for testType in types:
            self.assertEqual(testType(b"abcdbcda").rfind(b"cd", 1), 5)
            self.assertEqual(testType(b"abcdbcda").rfind(b"cd", 3), 5)
            self.assertEqual(testType(b"abcdbcda").rfind(b"cd", 7), -1)
            self.assertEqual(testType(b"abcdbcda").rfind(b"cd", -1, -2), -1)
            self.assertEqual(testType(b"abc").rfind(b"add", 3, 0), -1)
            self.assertEqual(testType(b'abc').rfind(b'bd'), -1)
            self.assertRaises(TypeError, testType(b'abc').rfind, [1])
            self.assertRaises(TypeError, testType(b'abc').rfind, [1], 1)
            self.assertRaises(TypeError, testType(b'abc').rfind, [1], 1, 2)

            if testType == bytes:
                self.assertEqual(testType(b"abc").rfind(b"add", None, 0), -1)
                self.assertEqual(testType(b"abc").rfind(b"add", 3, None), -1)
                self.assertEqual(testType(b"abc").rfind(b"add", None, None), -1)

            self.assertEqual(testType(b'abc').rfind(b'', 0, 0), 0)
            self.assertEqual(testType(b'abc').rfind(b'', 0, 1), 1)
            self.assertEqual(testType(b'abc').rfind(b'', 0, 2), 2)
            self.assertEqual(testType(b'abc').rfind(b'', 0, 3), 3)
            self.assertEqual(testType(b'abc').rfind(b'', 0, 4), 3)
            
            self.assertEqual(testType(b'x').rfind(b'x', 0, 0), -1)
            
            self.assertEqual(testType(b'x').rfind(b'x', 3, 0), -1)
            self.assertEqual(testType(b'x').rfind(b'', 3, 0), -1)    

    def test_rindex(self):
        for testType in types:
            self.assertRaises(TypeError, testType(b'abc').rindex, 257)
            self.assertEqual(testType(b'abc').rindex(b'a'), 0)
            self.assertEqual(testType(b'abc').rindex(b'a', 0, -1), 0)
            self.assertRaises(TypeError, testType(b'abc').rindex, [1])
            self.assertRaises(TypeError, testType(b'abc').rindex, [1], 1)
            self.assertRaises(TypeError, testType(b'abc').rindex, [1], 1, 2)

            self.assertRaises(ValueError, testType(b'abc').rindex, b'c', 0, -1)
            self.assertRaises(ValueError, testType(b'abc').rindex, b'a', -1)

    def test_rjust(self):
        for testType in types:
            self.assertRaises(TypeError, testType(b'').rjust, 42, '  ')
            self.assertRaises(TypeError, testType(b'').rjust, 42, b'  ')
            self.assertRaises(TypeError, testType(b'').rjust, 42, '\u0100')
            self.assertRaises(TypeError, testType(b'').rjust, 42, [2])
            self.assertEqual(testType(b'abc').rjust(4), b' abc')
            self.assertEqual(testType(b'abc').rjust(4, b'x'), b'xabc')
            self.assertEqual(testType(b'abc').rjust(4, 'x'), b'xabc')
        
        x = b'abc'
        self.assertEqual(id(x.rjust(2)), id(x))
        
        x = bytearray(x)
        self.assertTrue(id(x.rjust(2)) != id(x))

    def test_rpartition(self):
        for testType in types:
            self.assertRaises(TypeError, testType(b'').rpartition, None)
            self.assertRaises(ValueError, testType(b'').rpartition, b'')
            
            if testType == bytearray:
                self.assertEqual(testType(b'a\x01c').rpartition([1]), (b'a', b'\x01', b'c'))
            else:
                self.assertRaises(TypeError, testType(b'a\x01c').rpartition, [1])
            
            self.assertEqual(testType(b'abc').rpartition(b'b'), (b'a', b'b', b'c'))
            self.assertEqual(testType(b'abc').rpartition(b'd'), (b'', b'', b'abc'))
            
            x = testType(b'abc')
            one, two, three = x.rpartition(b'd')        
            if testType == bytearray:
                self.assertTrue(id(three) != id(x))
            else:
                self.assertEqual(id(three), id(x))
            
            b = testType(b'mississippi')
            self.assertEqual(b.rpartition(b'i'), (b'mississipp', b'i', b''))
            self.assertEqual(type(b.rpartition(b'i')[0]), testType)
            self.assertEqual(type(b.rpartition(b'i')[1]), testType)
            self.assertEqual(type(b.rpartition(b'i')[2]), testType)
            
            b = testType(b'abcdefgh')
            self.assertEqual(b.rpartition(b'a'), (b'', b'a', b'bcdefgh'))
        
        one, two, three = b''.rpartition(b'abc')
        self.assertEqual(id(one), id(two))
        self.assertEqual(id(two), id(three))

        one, two, three = bytearray().rpartition(b'abc')
        self.assertTrue(id(one) != id(two))
        self.assertTrue(id(two) != id(three))
        self.assertTrue(id(three) != id(one))

    def test_rsplit(self):
        for testType in types:
            x=testType(b"Hello Worllds")
            self.assertEqual(x.rsplit(), [b'Hello', b'Worllds'])
            s = x.rsplit(b"ll")
            self.assertTrue(s[0] == b"He")
            self.assertTrue(s[1] == b"o Wor")
            self.assertTrue(s[2] == b"ds")
        
            self.assertTrue(testType(b"1--2--3--4--5--6--7--8--9--0").rsplit(b"--", 2) == [b'1--2--3--4--5--6--7--8', b'9', b'0'])
        
            for temp_string in [b"", b"  ", b"   ", b"\t", b" \t", b"\t ", b"\t\t", b"\n", b"\n\n", b"\n \n"]:
                self.assertEqual(temp_string.rsplit(None), [])
            
            self.assertEqual(testType(b"ab").rsplit(None), [b"ab"])
            self.assertEqual(testType(b"a b").rsplit(None), [b"a", b"b"])
            
            self.assertRaises(TypeError, testType(b'').rsplit, [2])
            self.assertRaises(TypeError, testType(b'').rsplit, [2], 2)

    def test_rstrip(self):
        for testType in types:
            self.assertEqual(testType(b'abc ').rstrip(), b'abc')
            self.assertEqual(testType(b' abc ').rstrip(), b' abc')
            self.assertEqual(testType(b' ').rstrip(), b'')

            self.assertEqual(testType(b'abcx').rstrip(b'x'), b'abc')
            self.assertEqual(testType(b'xabc').rstrip(b'x'), b'xabc')
            self.assertEqual(testType(b'x').rstrip(b'x'), b'')
            
            self.assertRaises(TypeError, testType(b'').rstrip, [2])

        x = b'abc'
        self.assertEqual(id(x.rstrip()), id(x))

        x = bytearray(x)
        self.assertTrue(id(x.rstrip()) != id(x))

    def test_split(self):
        for testType in types:
            
            x=testType(b"Hello Worllds")
            self.assertRaises(ValueError, x.split, b'')
            self.assertEqual(x.split(None, 0), [b'Hello Worllds'])
            self.assertEqual(x.split(None, -1), [b'Hello', b'Worllds'])
            self.assertEqual(x.split(None, 2), [b'Hello', b'Worllds'])
            self.assertEqual(x.split(), [b'Hello', b'Worllds'])
            self.assertEqual(testType(b'abc').split(b'c'), [b'ab', b''])
            self.assertEqual(testType(b'abcd').split(b'c'), [b'ab', b'd'])
            self.assertEqual(testType(b'abccdef').split(b'c'), [b'ab', b'', b'def'])
            s = x.split(b"ll")
            self.assertTrue(s[0] == b"He")
            self.assertTrue(s[1] == b"o Wor")
            self.assertTrue(s[2] == b"ds")
        
            self.assertTrue(testType(b"1,2,3,4,5,6,7,8,9,0").split(b",") == [b'1',b'2',b'3',b'4',b'5',b'6',b'7',b'8',b'9',b'0'])
            self.assertTrue(testType(b"1,2,3,4,5,6,7,8,9,0").split(b",", -1) == [b'1',b'2',b'3',b'4',b'5',b'6',b'7',b'8',b'9',b'0'])
            self.assertTrue(testType(b"1,2,3,4,5,6,7,8,9,0").split(b",", 2) == [b'1',b'2',b'3,4,5,6,7,8,9,0'])
            self.assertTrue(testType(b"1--2--3--4--5--6--7--8--9--0").split(b"--") == [b'1',b'2',b'3',b'4',b'5',b'6',b'7',b'8',b'9',b'0'])
            self.assertTrue(testType(b"1--2--3--4--5--6--7--8--9--0").split(b"--", -1) == [b'1',b'2',b'3',b'4',b'5',b'6',b'7',b'8',b'9',b'0'])
            self.assertTrue(testType(b"1--2--3--4--5--6--7--8--9--0").split(b"--", 2) == [b'1', b'2', b'3--4--5--6--7--8--9--0'])
        
            self.assertEqual(testType(b"").split(None), [])
            self.assertEqual(testType(b"ab").split(None), [b"ab"])
            self.assertEqual(testType(b"a b").split(None), [b"a", b"b"])
            self.assertEqual(bytearray(b' a bb c ').split(None, 1), [bytearray(b'a'), bytearray(b'bb c ')])
            
            self.assertEqual(testType(b'    ').split(), [])
            
            self.assertRaises(TypeError, testType(b'').split, [2])
            self.assertRaises(TypeError, testType(b'').split, [2], 2)

    def test_splitlines(self):
        for testType in types:
            self.assertEqual(testType(b'foo\nbar\n').splitlines(), [b'foo', b'bar'])
            self.assertEqual(testType(b'foo\nbar\n').splitlines(True), [b'foo\n', b'bar\n'])
            self.assertEqual(testType(b'foo\r\nbar\r\n').splitlines(True), [b'foo\r\n', b'bar\r\n'])
            self.assertEqual(testType(b'foo\r\nbar\r\n').splitlines(), [b'foo', b'bar'])
            self.assertEqual(testType(b'foo\rbar\r').splitlines(True), [b'foo\r', b'bar\r'])
            self.assertEqual(testType(b'foo\nbar\nbaz').splitlines(), [b'foo', b'bar', b'baz'])
            self.assertEqual(testType(b'foo\nbar\nbaz').splitlines(True), [b'foo\n', b'bar\n', b'baz'])
            self.assertEqual(testType(b'foo\r\nbar\r\nbaz').splitlines(True), [b'foo\r\n', b'bar\r\n', b'baz'])
            self.assertEqual(testType(b'foo\rbar\rbaz').splitlines(True), [b'foo\r', b'bar\r', b'baz'])
        
    def test_startswith(self):
        for testType in types:
            self.assertRaises(TypeError, testType(b'abcdef').startswith, [])
            self.assertRaises(TypeError, testType(b'abcdef').startswith, [], 0)
            self.assertRaises(TypeError, testType(b'abcdef').startswith, [], 0, 1)

            self.assertEqual(testType(b"abcde").startswith(b'c', 2, 6), True)
            self.assertEqual(testType(b"abc").startswith(b'c', 4, 6), False)
            self.assertEqual(testType(b"abcde").startswith(b'cde', 2, 9), True)
            self.assertEqual(testType(b'abc').startswith(b'abcd', 4), False)
            self.assertEqual(testType(b'abc').startswith(b'abc', -3), True)
            self.assertEqual(testType(b'abc').startswith(b'abc', -10), True)
            self.assertEqual(testType(b'abc').startswith(b'abc', -3, 0), False)
            self.assertEqual(testType(b'abc').startswith(b'abc', -10, 0), False)
            self.assertEqual(testType(b'abc').startswith(b'abc', -10, -10), False)
            self.assertEqual(testType(b'abc').startswith(b'ab', 0, -1), True)
            self.assertEqual(testType(b'abc').startswith((b'abc', ), -10), True)
            self.assertEqual(testType(b'abc').startswith((b'abc', ), 10), False)
            self.assertEqual(testType(b'abc').startswith((b'abc', ), -10, 0), False)
            self.assertEqual(testType(b'abc').startswith((b'abc', ), 10, 0), False)
            self.assertEqual(testType(b'abc').startswith((b'abc', ), 1, -10), False)
            self.assertEqual(testType(b'abc').startswith((b'abc', ), 1, -1), False)
            self.assertEqual(testType(b'abc').startswith((b'abc', ), -1, -2), False)

            self.assertEqual(testType(b'abc').startswith((b'abc', b'def')), True)
            self.assertEqual(testType(b'abc').startswith((b'qrt', b'def')), False)
            self.assertEqual(testType(b'abc').startswith((b'abc', b'def'), -3), True)
            self.assertEqual(testType(b'abc').startswith((b'qrt', b'def'), -3), False)
            self.assertEqual(testType(b'abc').startswith((b'abc', b'def'), 0), True)
            self.assertEqual(testType(b'abc').startswith((b'qrt', b'def'), 0), False)
            self.assertEqual(testType(b'abc').startswith((b'abc', b'def'), -3, 3), True)
            self.assertEqual(testType(b'abc').startswith((b'qrt', b'def'), -3, 3), False)
            self.assertEqual(testType(b'abc').startswith((b'abc', b'def'), 0, 3), True)
            self.assertEqual(testType(b'abc').startswith((b'qrt', b'def'), 0, 3), False)
            
            hw = testType(b"hello world")
            self.assertTrue(hw.startswith(b"hello"))
            self.assertTrue(not hw.startswith(b"heloo"))
            self.assertTrue(hw.startswith(b"llo", 2))
            self.assertTrue(not hw.startswith(b"lno", 2))
            self.assertTrue(hw.startswith(b"wor", 6, 9))
            self.assertTrue(not hw.startswith(b"wor", 6, 7))
            self.assertTrue(not hw.startswith(b"wox", 6, 10))
            self.assertTrue(not hw.startswith(b"wor", 6, 2))

    def test_strip(self):
        for testType in types:
            self.assertEqual(testType(b'abc ').strip(), b'abc')
            self.assertEqual(testType(b' abc').strip(), b'abc')
            self.assertEqual(testType(b' abc ').strip(), b'abc')
            self.assertEqual(testType(b' ').strip(), b'')

            self.assertEqual(testType(b'abcx').strip(b'x'), b'abc')
            self.assertEqual(testType(b'xabc').strip(b'x'), b'abc')
            self.assertEqual(testType(b'xabcx').strip(b'x'), b'abc')
            self.assertEqual(testType(b'x').strip(b'x'), b'')

        x = b'abc'
        self.assertEqual(id(x.strip()), id(x))

        x = bytearray(x)
        self.assertTrue(id(x.strip()) != id(x))

    def test_swapcase(self):
        expected = b'\x00\x01\x02\x03\x04\x05\x06\x07\x08\t\n\x0b\x0c\r\x0e\x0f'  \
        b'\x10\x11\x12\x13\x14\x15\x16\x17\x18\x19\x1a\x1b\x1c\x1d\x1e\x1f !"#$%' \
        b'&\'()*+,-./0123456789:;<=>?@abcdefghijklmnopqrstuvwxyz[\\]^_`'          \
        b'ABCDEFGHIJKLMNOPQRSTUVWXYZ{|}~\x7f\x80\x81\x82\x83\x84\x85\x86\x87\x88' \
        b'\x89\x8a\x8b\x8c\x8d\x8e\x8f\x90\x91\x92\x93\x94\x95\x96\x97\x98\x99'   \
        b'\x9a\x9b\x9c\x9d\x9e\x9f\xa0\xa1\xa2\xa3\xa4\xa5\xa6\xa7\xa8\xa9\xaa'   \
        b'\xab\xac\xad\xae\xaf\xb0\xb1\xb2\xb3\xb4\xb5\xb6\xb7\xb8\xb9\xba\xbb'   \
        b'\xbc\xbd\xbe\xbf\xc0\xc1\xc2\xc3\xc4\xc5\xc6\xc7\xc8\xc9\xca\xcb\xcc'   \
        b'\xcd\xce\xcf\xd0\xd1\xd2\xd3\xd4\xd5\xd6\xd7\xd8\xd9\xda\xdb\xdc\xdd'   \
        b'\xde\xdf\xe0\xe1\xe2\xe3\xe4\xe5\xe6\xe7\xe8\xe9\xea\xeb\xec\xed\xee'   \
        b'\xef\xf0\xf1\xf2\xf3\xf4\xf5\xf6\xf7\xf8\xf9\xfa\xfb\xfc\xfd\xfe\xff'
        
        data = bytearray()
        for i in range(256):
            data.append(i)

        for testType in types:
            self.assertEqual(testType(b'123').swapcase(), b'123')       
            b = testType(b'123')
            self.assertTrue(id(b.swapcase()) != id(b))
            
            self.assertEqual(testType(b'abc').swapcase(), b'ABC')
            self.assertEqual(testType(b'ABC').swapcase(), b'abc')
            self.assertEqual(testType(b'ABc').swapcase(), b'abC')
            
            x = testType(data).swapcase()
            self.assertEqual(testType(data).swapcase(), expected)
        
    def test_title(self):
        for testType in types:
            self.assertEqual(testType(b'').title(), b'')
            self.assertEqual(testType(b'foo').title(), b'Foo')
            self.assertEqual(testType(b'Foo').title(), b'Foo')
            self.assertEqual(testType(b'foo bar baz').title(), b'Foo Bar Baz')
            
            for i in range(256):
                b = bytearray()
                b.append(i)
                
                if (b >= b'a' and b <= b'z') or (b >= b'A' and b <= 'Z'):
                    continue
                
                inp = testType(b.join([b'foo', b'bar', b'baz']))
                exp = b.join([b'Foo', b'Bar', b'Baz'])
                self.assertEqual(inp.title(), exp)
                
        x = b''
        self.assertEqual(id(x.title()), id(x))
        
        x = bytearray(b'')
        self.assertTrue(id(x.title()) != id(x))

    def test_translate(self):
        identTable = bytearray()
        for i in range(256):
            identTable.append(i)

        repAtable = bytearray(identTable)
        repAtable[ord('A')] = ord('B')
        
        for testType in types:
            self.assertRaises(TypeError, testType(b'').translate, {})
            self.assertRaises(ValueError, testType(b'foo').translate, b'')
            self.assertRaises(ValueError, testType(b'').translate, b'')        
            self.assertEqual(testType(b'AAA').translate(repAtable), b'BBB')
            self.assertEqual(testType(b'AAA').translate(repAtable, b'A'), b'')
            self.assertRaises(TypeError, b''.translate, identTable, None)
        
        self.assertEqual(b'AAA'.translate(None, b'A'), b'')
        self.assertEqual(b'AAABBB'.translate(None, b'A'), b'BBB')
        self.assertEqual(b'AAA'.translate(None), b'AAA')
        self.assertEqual(bytearray(b'AAA').translate(None, b'A'),
                b'')
        self.assertEqual(bytearray(b'AAA').translate(None),
                b'AAA')

        b = b'abc'    
        self.assertEqual(id(b.translate(None)), id(b))    
        
        b = b''
        self.assertEqual(id(b.translate(identTable)), id(b))

        b = b''
        self.assertEqual(id(b.translate(identTable, b'')), id(b))

        b = b''
        self.assertEqual(id(b.translate(identTable, b'')), id(b))
        
        if is_cli:
            # CPython bug 4348 - http://bugs.python.org/issue4348
            b = bytearray(b'')
            self.assertTrue(id(b.translate(identTable)) != id(b))
            
        self.assertRaises(TypeError, testType(b'').translate, [])
        self.assertRaises(TypeError, testType(b'').translate, [], [])

    def test_upper(self):
        expected = b'\x00\x01\x02\x03\x04\x05\x06\x07\x08\t\n\x0b\x0c\r\x0e\x0f'  \
        b'\x10\x11\x12\x13\x14\x15\x16\x17\x18\x19\x1a\x1b\x1c\x1d\x1e\x1f !"#$%' \
        b'&\'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`'          \
        b'ABCDEFGHIJKLMNOPQRSTUVWXYZ{|}~\x7f\x80\x81\x82\x83\x84\x85\x86\x87\x88' \
        b'\x89\x8a\x8b\x8c\x8d\x8e\x8f\x90\x91\x92\x93\x94\x95\x96\x97\x98\x99'   \
        b'\x9a\x9b\x9c\x9d\x9e\x9f\xa0\xa1\xa2\xa3\xa4\xa5\xa6\xa7\xa8\xa9\xaa'   \
        b'\xab\xac\xad\xae\xaf\xb0\xb1\xb2\xb3\xb4\xb5\xb6\xb7\xb8\xb9\xba\xbb'   \
        b'\xbc\xbd\xbe\xbf\xc0\xc1\xc2\xc3\xc4\xc5\xc6\xc7\xc8\xc9\xca\xcb\xcc'   \
        b'\xcd\xce\xcf\xd0\xd1\xd2\xd3\xd4\xd5\xd6\xd7\xd8\xd9\xda\xdb\xdc\xdd'   \
        b'\xde\xdf\xe0\xe1\xe2\xe3\xe4\xe5\xe6\xe7\xe8\xe9\xea\xeb\xec\xed\xee'   \
        b'\xef\xf0\xf1\xf2\xf3\xf4\xf5\xf6\xf7\xf8\xf9\xfa\xfb\xfc\xfd\xfe\xff'
        
        data = bytearray()
        for i in range(256):
            data.append(i)

        for testType in types:
            self.assertEqual(testType(data).upper(), expected)

    def test_zfill(self):
        for testType in types:
            self.assertEqual(testType(b'abc').zfill(0), b'abc')
            self.assertEqual(testType(b'abc').zfill(4), b'0abc')
            self.assertEqual(testType(b'+abc').zfill(5), b'+0abc')
            self.assertEqual(testType(b'-abc').zfill(5), b'-0abc')
            self.assertEqual(testType(b'').zfill(2), b'00')
            self.assertEqual(testType(b'+').zfill(2), b'+0')
            self.assertEqual(testType(b'-').zfill(2), b'-0')

        b = b'abc'
        self.assertEqual(id(b.zfill(0)), id(b))
        
        b = bytearray(b)
        self.assertTrue(id(b.zfill(0)) != id(b))

    def test_none(self):
        for testType in types:        
            self.assertRaises(TypeError, testType(b'abc').replace, b"new")
            self.assertRaises(TypeError, testType(b'abc').replace, b"new", 2)
            self.assertRaises(TypeError, testType(b'abc').center, 0, None)
            if str != bytes:
                self.assertRaises(TypeError, testType(b'abc').fromhex, None)
            self.assertRaises(TypeError, testType(b'abc').decode, 'ascii', None)
        
            for fn in ['find', 'index', 'rfind', 'count', 'startswith', 'endswith']:
                f = getattr(testType(b'abc'), fn)
                self.assertRaises(TypeError, f, None)
                self.assertRaises(TypeError, f, None, 0)
                self.assertRaises(TypeError, f, None, 0, 2)
        
            self.assertRaises(TypeError, testType(b'abc').replace, None, b'ef')
            self.assertRaises(TypeError, testType(b'abc').replace, None, b'ef', 1)
            self.assertRaises(TypeError, testType(b'abc').replace, b'abc', None)
            self.assertRaises(TypeError, testType(b'abc').replace, b'abc', None, 1)

    def test_add_mul(self):
        for testType in types:
            self.assertRaises(TypeError, lambda: testType(b"a") + 3)
            self.assertRaises(TypeError, lambda: 3 + testType(b"a"))
        
            self.assertRaises(TypeError, lambda: "a" * "3")
            self.assertRaises(OverflowError, lambda: "a" * (sys.maxsize + 1))
            self.assertRaises(OverflowError, lambda: (sys.maxsize + 1) * "a")
        
            class mylong(long): pass
            
            # multiply
            self.assertEqual("aaaa", "a" * 4)
            self.assertEqual("aaaa", "a" * mylong(4))
            self.assertEqual("aaa", "a" * 3)
            self.assertEqual("a", "a" * True)
            self.assertEqual("", "a" * False)
        
            self.assertEqual("aaaa", 4 * "a")
            self.assertEqual("aaaa", mylong(4) * "a")
            self.assertEqual("aaa", 3 * "a")
            self.assertEqual("a", True * "a")
            self.assertEqual("", False * "a" )

    # zero-length string
    def test_empty_bytes(self):
        for testType in types:
            self.assertEqual(testType(b'').title(), b'')
            self.assertEqual(testType(b'').capitalize(), b'')
            self.assertEqual(testType(b'').count(b'a'), 0)
            table = testType(b'10') * 128
            self.assertEqual(testType(b'').translate(table), b'')
            self.assertEqual(testType(b'').replace(b'a', b'ef'), b'')
            self.assertEqual(testType(b'').replace(b'bc', b'ef'), b'')
            self.assertEqual(testType(b'').split(), [])
            self.assertEqual(testType(b'').split(b' '), [b''])
            self.assertEqual(testType(b'').split(b'a'), [b''])

    def test_encode_decode(self):
        for testType in types:
            self.assertEqual(testType(b'abc').decode(), 'abc')

    def test_encode_decode_error(self):
        for testType in types:
            self.assertRaises(TypeError, testType(b'abc').decode, None)
            
    def test_bytes_subclass(self):
        for testType in types:
            class customstring(testType):
                def __str__(self):  return 'xyz'
                def __repr__(self): return 'foo'
                def __hash__(self): return 42
                def __mul__(self, count): return b'multiplied'
                def __add__(self, other): return 23
                def __len__(self): return 2300
                def __contains__(self, value): return False
            
            o = customstring(b'abc')
            self.assertEqual(str(o), "xyz")
            self.assertEqual(repr(o), "foo")
            self.assertEqual(hash(o), 42)
            self.assertEqual(o * 3, b'multiplied')
            self.assertEqual(o + b'abc', 23)
            self.assertEqual(len(o), 2300)
            self.assertEqual(b'a' in o, False)
        
        class custombytearray(bytearray):
            def __init__(self, value):
                bytearray.__init__(self)
                
        self.assertEqual(custombytearray(42), bytearray())

        class custombytearray(bytearray):
            def __init__(self, value, **args):
                bytearray.__init__(self)
                
        self.assertEqual(custombytearray(42, x=42), bytearray())

    def test_bytes_equals(self):
        for testType in types:
            x = testType(b'abc') == testType(b'abc')
            y = testType(b'def') == testType(b'def')
            self.assertEqual(id(x), id(y))
            self.assertEqual(id(x), id(True))
            
            x = testType(b'abc') != testType(b'abc')
            y = testType(b'def') != testType(b'def')
            self.assertEqual(id(x), id(y))
            self.assertEqual(id(x), id(False))
            
            x = testType(b'abcx') == testType(b'abc')
            y = testType(b'defx') == testType(b'def')
            self.assertEqual(id(x), id(y))
            self.assertEqual(id(x), id(False))
            
            x = testType(b'abcx') != testType(b'abc')
            y = testType(b'defx') != testType(b'def')
            self.assertEqual(id(x), id(y))
            self.assertEqual(id(x), id(True))

    def test_bytes_dict(self):
        self.assertTrue('__init__' not in list(bytes.__dict__.keys()))
        self.assertTrue('__init__' in list(bytearray.__dict__.keys()))

        for testType in types:
            extra_str_dict_keys = [ "__cmp__", "isdecimal", "isnumeric", "isunicode"]  # "__radd__", 
            
            #It's OK that __getattribute__ does not show up in the __dict__.  It is
            #implemented.
            self.assertTrue(hasattr(testType, "__getattribute__"), str(testType) + " has no __getattribute__ method")
            
            for temp_key in extra_str_dict_keys:
                self.assertTrue(not temp_key in list(testType.__dict__.keys()))

    def test_bytes_to_numeric(self):
        for testType in types:
            class substring(testType):
                def __int__(self): return 1
                def __complex__(self): return 1j
                def __float__(self): return 1.0
                def __long__(self): return 1
            
            class myfloat(float): pass
            class mylong(long): pass
            class myint(int): pass
            class mycomplex(complex): pass
            
            v = substring(b"123")        
            
            self.assertEqual(float(v), 1.0)
            self.assertEqual(myfloat(v), 1.0)
            self.assertEqual(type(myfloat(v)), myfloat)

            self.assertEqual(int(v), 1)
            self.assertEqual(mylong(v), 1)
            self.assertEqual(type(mylong(v)), mylong)
            
            self.assertEqual(int(v), 1)
            self.assertEqual(myint(v), 1)
            self.assertEqual(type(myint(v)), myint)
            
            # str in 2.6 still supports this, but not in 3.0, we have the 3.0 behavior.
            if not is_cli and testType == bytes:
                self.assertEqual(complex(v), 123 + 0j)
                self.assertEqual(mycomplex(v), 123 + 0j)
            else:
                self.assertEqual(complex(v), 1j)
                self.assertEqual(mycomplex(v), 1j)
            
            class substring(testType): pass
            
            v = substring(b"123")
            
            self.assertEqual(int(v), 123)
            self.assertEqual(int(v), 123)
            self.assertEqual(float(v), 123.0)
            
            self.assertEqual(mylong(v), 123)
            self.assertEqual(type(mylong(v)), mylong)
            self.assertEqual(myint(v), 123)
            self.assertEqual(type(myint(v)), myint)

            if testType == str:
                # 2.6 allows this, 3.0 disallows this.
                self.assertEqual(complex(v), 123+0j)
                self.assertEqual(mycomplex(v), 123+0j)
            else:
                self.assertRaises(TypeError, complex, v)
                self.assertRaises(TypeError, mycomplex, v)

    def test_compares(self):
        a = b'A'
        b = b'B'
        bb = b'BB'
        aa = b'AA'
        ab = b'AB'
        ba = b'BA'
        
        for testType in types:
            for otherType in types:
                self.assertEqual(testType(a) > otherType(b), False)
                self.assertEqual(testType(a) < otherType(b), True)
                self.assertEqual(testType(a) <= otherType(b), True)
                self.assertEqual(testType(a) >= otherType(b), False)
                self.assertEqual(testType(a) == otherType(b), False)
                self.assertEqual(testType(a) != otherType(b), True)
                
                self.assertEqual(testType(b) > otherType(a), True)
                self.assertEqual(testType(b) < otherType(a), False)
                self.assertEqual(testType(b) <= otherType(a), False)
                self.assertEqual(testType(b) >= otherType(a), True)
                self.assertEqual(testType(b) == otherType(a), False)
                self.assertEqual(testType(b) != otherType(a), True)

                self.assertEqual(testType(a) > otherType(a), False)
                self.assertEqual(testType(a) < otherType(a), False)
                self.assertEqual(testType(a) <= otherType(a), True)
                self.assertEqual(testType(a) >= otherType(a), True)
                self.assertEqual(testType(a) == otherType(a), True)
                self.assertEqual(testType(a) != otherType(a), False)
                
                self.assertEqual(testType(aa) > otherType(b), False)
                self.assertEqual(testType(aa) < otherType(b), True)
                self.assertEqual(testType(aa) <= otherType(b), True)
                self.assertEqual(testType(aa) >= otherType(b), False)
                self.assertEqual(testType(aa) == otherType(b), False)
                self.assertEqual(testType(aa) != otherType(b), True)
                
                self.assertEqual(testType(bb) > otherType(a), True)
                self.assertEqual(testType(bb) < otherType(a), False)
                self.assertEqual(testType(bb) <= otherType(a), False)
                self.assertEqual(testType(bb) >= otherType(a), True)
                self.assertEqual(testType(bb) == otherType(a), False)
                self.assertEqual(testType(bb) != otherType(a), True)

                self.assertEqual(testType(ba) > otherType(b), True)
                self.assertEqual(testType(ba) < otherType(b), False)
                self.assertEqual(testType(ba) <= otherType(b), False)
                self.assertEqual(testType(ba) >= otherType(b), True)
                self.assertEqual(testType(ba) == otherType(b), False)
                self.assertEqual(testType(ba) != otherType(b), True)
                
                self.assertEqual(testType(ab) > otherType(a), True)
                self.assertEqual(testType(ab) < otherType(a), False)
                self.assertEqual(testType(ab) <= otherType(a), False)
                self.assertEqual(testType(ab) >= otherType(a), True)
                self.assertEqual(testType(ab) == otherType(a), False)
                self.assertEqual(testType(ab) != otherType(a), True)
                
                self.assertEqual(testType(ab) == [], False)
                
                self.assertEqual(testType(a) > None, True)
                self.assertEqual(testType(a) < None, False)
                self.assertEqual(testType(a) <= None, False)
                self.assertEqual(testType(a) >= None, True)
                self.assertEqual(None > testType(a), False)
                self.assertEqual(None < testType(a), True)
                self.assertEqual(None <= testType(a), True)
                self.assertEqual(None >= testType(a), False)

                
    def test_bytearray(self):
        self.assertRaises(TypeError, hash, bytearray(b'abc'))
        self.assertRaises(TypeError, bytearray(b'').__setitem__, None, b'abc')
        self.assertRaises(TypeError, bytearray(b'').__delitem__, None)
        x = bytearray(b'abc')
        del x[-1]
        self.assertEqual(x, b'ab')
        
        def f():
            x = bytearray(b'abc')
            x[0:2] = [1j]
        self.assertRaises(TypeError, f)
        
        x = bytearray(b'abc')
        x[0:1] = [ord('d')]
        self.assertEqual(x, b'dbc')
        
        x = bytearray(b'abc')
        x[0:3] = x
        self.assertEqual(x, b'abc')
        
        x = bytearray(b'abc')
        
        del x[0]
        self.assertEqual(x, b'bc')
        
        x = bytearray(b'abc')
        x += b'foo'
        self.assertEqual(x, b'abcfoo')
        
        b = bytearray(b"abc")
        b1 = b
        b += b"def"    
        self.assertEqual(b1, b)
        
        x = bytearray(b'abc')
        x += bytearray(b'foo')
        self.assertEqual(x, b'abcfoo')

        x = bytearray(b'abc')
        x *= 2
        self.assertEqual(x, b'abcabc')
        
        x = bytearray(b'abcdefghijklmnopqrstuvwxyz')
        x[25:1] = b'x' * 24
        self.assertEqual(x, b'abcdefghijklmnopqrstuvwxyxxxxxxxxxxxxxxxxxxxxxxxxz')
        
        x = bytearray(b'abcdefghijklmnopqrstuvwxyz')
        x[25:0] = b'x' * 25
        self.assertEqual(x, b'abcdefghijklmnopqrstuvwxyxxxxxxxxxxxxxxxxxxxxxxxxxz')
        
        tests = ( ((0, 3, None), b'abc', b''), 
                ((0, 2, None), b'abc', b'c'), 
                ((4, 0, 2),    b'abc', b'abc'), 
                ((3, 0, 2),    b'abc', b'abc'), 
                ((3, 0, -2),   b'abc', b'ab'), 
                ((0, 3, 1),    b'abc', b''), 
                ((0, 2, 1),    b'abc', b'c'), 
                ((0, 3, 2),    b'abc', b'b'), 
                ((0, 2, 2),    b'abc', b'bc'), 
                ((0, 3, -1),   b'abc', b'abc'), 
                ((0, 2, -1),   b'abc', b'abc'), 
                ((3, 0, -1),   b'abc', b'a'), 
                ((2, 0, -1),   b'abc', b'a'), 
                ((4, 2, -1),   b'abcdef', b'abcf'),
                )

        for indexes, input, result in tests:
            x = bytearray(input)
            if indexes[2] == None:
                del x[indexes[0] : indexes[1]]
                self.assertEqual(x, result)
            else:
                del x[indexes[0] : indexes[1] : indexes[2]]
                self.assertEqual(x, result)     
        
        class myint(int): pass
        class intobj(object):
            def __int__(self):
                return 42
        
        x = bytearray(b'abe')
        x[-1] = ord('a')
        self.assertEqual(x, b'aba')
        
        x[-1] = IndexableOC(ord('r'))
        self.assertEqual(x, b'abr')
        
        x[-1] = Indexable(ord('s'))
        self.assertEqual(x, b'abs')

        def f(): x[-1] = IndexableOC(256)
        self.assertRaises(ValueError, f)
        
        def f(): x[-1] = Indexable(256)
        self.assertRaises(ValueError, f)

        x[-1] = b'b'
        self.assertEqual(x, b'abb')
        x[-1] = myint(ord('c'))
        self.assertEqual(x, b'abc')

        x[0:1] = 2
        self.assertEqual(x, b'\x00\x00bc')
        x = bytearray(b'abc')
        x[0:1] = 2
        self.assertEqual(x, b'\x00\x00bc')
        x[0:2] = b'a'
        self.assertEqual(x, b'abc')
        x[0:1] = b'd'
        self.assertEqual(x, b'dbc')
        x[0:1] = myint(3)
        self.assertEqual(x, b'\x00\x00\x00bc')
        x[0:3] = [ord('a'), ord('b'), ord('c')]
        self.assertEqual(x, b'abcbc')

        def f(): x[0:1] = intobj()
        self.assertRaises(TypeError, f)

        def f(): x[0:1] = sys.maxsize
        # mono doesn't throw an OutOfMemoryException on Linux when the size is too large,
        # it does get a value error for trying to set capacity to a negative number
        if is_mono and not is_osx:
            self.assertRaises(ValueError, f)
        else:
            self.assertRaises(MemoryError, f)
        
        def f(): x[0:1] = sys.maxsize+1
        self.assertRaises(TypeError, f)
            
        for setval in [b'bar', bytearray(b'bar'), [b'b', b'a', b'r'], (b'b', b'a', b'r'), (98, b'a', b'r'), (Indexable(98), b'a', b'r'), (IndexableOC(98), b'a', b'r')]:
            x = bytearray(b'abc')
            x[0:3] = setval
            self.assertEqual(x, b'bar')
            
            x = bytearray(b'abc')
            x[1:4] = setval
            self.assertEqual(x, b'abar')

            x = bytearray(b'abc')
            x[0:2] = setval
            self.assertEqual(x, b'barc')
            
            x = bytearray(b'abc')
            x[4:0:2] = setval[-1:-1]
            self.assertEqual(x, b'abc')
            
            x = bytearray(b'abc')
            x[3:0:2] = setval[-1:-1]
            self.assertEqual(x, b'abc')
            
            x = bytearray(b'abc')
            x[3:0:-2] = setval[-1:-1]
            self.assertEqual(x, b'ab')
            
            x = bytearray(b'abc')
            x[3:0:-2] = setval[0:-2]
            self.assertEqual(x, b'abb')
            
            x = bytearray(b'abc')
            x[0:3:1] = setval
            self.assertEqual(x, b'bar')
            
            x = bytearray(b'abc')
            x[0:2:1] = setval
            self.assertEqual(x, b'barc')
            
            x = bytearray(b'abc')
            x[0:3:2] = setval[0:-1]
            self.assertEqual(x, b'bba')
            
            x = bytearray(b'abc')
            x[0:2:2] = setval[0:-2]
            self.assertEqual(x, b'bbc')
            
            x = bytearray(b'abc')
            x[0:3:-1] = setval[-1:-1]
            self.assertEqual(x, b'abc')
            
            x = bytearray(b'abc')
            x[0:2:-1] = setval[-1:-1]
            self.assertEqual(x, b'abc')
            
            x = bytearray(b'abc')
            x[3:0:-1] = setval[0:-1]
            self.assertEqual(x, b'aab')
            
            x = bytearray(b'abc')
            x[2:0:-1] = setval[0:-1]
            self.assertEqual(x, b'aab')
            
            x = bytearray(b'abcdef')
            def f():x[0:6:2] = b'a'
            self.assertRaises(ValueError, f)

        self.assertEqual(bytearray(source=b'abc'), bytearray(b'abc'))
        self.assertEqual(bytearray(source=2), bytearray(b'\x00\x00'))
        
        self.assertEqual(bytearray(b'abc').__alloc__(), 4)
        self.assertEqual(bytearray().__alloc__(), 0)
        
    def test_bytes(self):
        self.assertEqual(hash(b'abc'), hash(b'abc'))
        self.assertEqual(b'abc', B'abc')

    def test_operators(self):
        for testType in types:
            self.assertRaises(TypeError, lambda : testType(b'abc') * None)
            self.assertRaises(TypeError, lambda : testType(b'abc') + None)
            self.assertRaises(TypeError, lambda : None * testType(b'abc'))
            self.assertRaises(TypeError, lambda : None + testType(b'abc'))
            self.assertEqual(testType(b'abc') * 2, b'abcabc')
            
            if testType == bytearray:
                self.assertEqual(testType(b'abc')[0], ord('a'))        
                self.assertEqual(testType(b'abc')[-1], ord('c'))
            else:
                self.assertEqual(testType(b'abc')[0], b'a')        
                self.assertEqual(testType(b'abc')[-1], b'c')
            
            for otherType in types:
                
                self.assertEqual(testType(b'abc') + otherType(b'def'), b'abcdef')
                resType = type(testType(b'abc') + otherType(b'def'))
                if testType == bytearray or otherType == bytearray:
                    self.assertEqual(resType, bytearray)
                else:
                    self.assertEqual(resType, bytes)
                    
            self.assertEqual(b'ab' in testType(b'abcd'), True)
            
            # 2.6 doesn't allow this for testType=bytes, so test for 3.0 in this case
            if testType is not bytes or hasattr(bytes, '__iter__'):
                self.assertEqual(ord(b'a') in testType(b'abcd'), True)
            
                self.assertRaises(ValueError, lambda : 256 in testType(b'abcd'))
        
        x = b'abc'
        self.assertEqual(x * 1, x)
        self.assertEqual(1 * x, x)
        self.assertEqual(id(x), id(x * 1))    
        self.assertEqual(id(x), id(1 * x))    

        x = bytearray(b'abc')
        self.assertEqual(x * 1, x)
        self.assertEqual(1 * x, x)
        self.assertTrue(id(x) != id(x * 1))    
        self.assertTrue(id(x) != id(1 * x))    

    def test_init(self):
        for testType in types:
            if testType != str:  # skip on Cpy 2.6 for str type
                self.assertRaises(TypeError, testType, None, 'ascii')
                self.assertRaises(TypeError, testType, 'abc', None)
                self.assertRaises(TypeError, testType, [None])
                self.assertEqual(testType('abc', 'ascii'), b'abc')
                self.assertEqual(testType(0), b'')
                self.assertEqual(testType(5), b'\x00\x00\x00\x00\x00')
                self.assertRaises(ValueError, testType, [256])
                self.assertRaises(ValueError, testType, [257])
                
            testType(list(range(256)))
            
        def f():
            yield 42

        self.assertEqual(bytearray(f()), b'*')

    def test_slicing(self):
        for testType in types:
            self.assertEqual(testType(b'abc')[0:3], b'abc')
            self.assertEqual(testType(b'abc')[0:2], b'ab')
            self.assertEqual(testType(b'abc')[3:0:2], b'')
            self.assertEqual(testType(b'abc')[3:0:2], b'')
            self.assertEqual(testType(b'abc')[3:0:-2], b'c')
            self.assertEqual(testType(b'abc')[3:0:-2], b'c')
            self.assertEqual(testType(b'abc')[0:3:1], b'abc')
            self.assertEqual(testType(b'abc')[0:2:1], b'ab')
            self.assertEqual(testType(b'abc')[0:3:2], b'ac')
            self.assertEqual(testType(b'abc')[0:2:2], b'a')
            self.assertEqual(testType(b'abc')[0:3:-1], b'')
            self.assertEqual(testType(b'abc')[0:2:-1], b'')
            self.assertEqual(testType(b'abc')[3:0:-1], b'cb')
            self.assertEqual(testType(b'abc')[2:0:-1], b'cb')
            
            self.assertRaises(TypeError, testType(b'abc').__getitem__, None)

    def test_ord(self):
        for testType in types:
            self.assertEqual(ord(testType(b'a')), 97)
            self.assertRaisesPartialMessage(TypeError, "expected a character, but string of length 2 found", ord, testType(b'aa'))

    def test_pickle(self):
        import pickle
        
        for testType in types:
            self.assertEqual(pickle.loads(pickle.dumps(testType(list(range(256))))), testType(list(range(256))))

    @unittest.skipUnless(is_cli, 'IronPython specific test')
    def test_zzz_cli_features(self):
        import System
        import clr
        clr.AddReference('Microsoft.Dynamic')
        import Microsoft
        
        for testType in types:
            self.assertEqual(testType(b'abc').Count, 3)
            self.assertEqual(bytearray(b'abc').Contains(ord('a')), True)
            self.assertEqual(list(System.Collections.IEnumerable.GetEnumerator(bytearray(b'abc'))), [ord('a'), ord('b'), ord('c')])
            self.assertEqual(testType(b'abc').IndexOf(ord('a')), 0)
            self.assertEqual(testType(b'abc').IndexOf(ord('d')), -1)
            
            myList = System.Collections.Generic.List[System.Byte]()
            myList.Add(ord('a'))
            myList.Add(ord('b'))
            myList.Add(ord('c'))
            
            self.assertEqual(testType(b'').join([myList]), b'abc')

        # bytearray
        '''
        self.assertEqual(bytearray(b'abc') == 'abc', False)
        if not is_net40:
            self.assertEqual(Microsoft.Scripting.IValueEquality.ValueEquals(bytearray(b'abc'), 'abc'), False)
        '''
        self.assertEqual(bytearray(b'abc') == 'abc', True)
        self.assertEqual(b'abc'.IsReadOnly, True)
        self.assertEqual(bytearray(b'abc').IsReadOnly, False)
            
        self.assertEqual(bytearray(b'abc').Remove(ord('a')), True)
        self.assertEqual(bytearray(b'abc').Remove(ord('d')), False)
        
        x = bytearray(b'abc')
        x.Clear()
        self.assertEqual(x, b'')
        
        x.Add(ord('a'))
        self.assertEqual(x, b'a')
        
        self.assertEqual(x.IndexOf(ord('a')), 0)
        self.assertEqual(x.IndexOf(ord('b')), -1)
        
        x.Insert(0, ord('b'))
        self.assertEqual(x, b'ba')
        
        x.RemoveAt(0)
        self.assertEqual(x, b'a')
        
        System.Collections.Generic.IList[System.Byte].__setitem__(x, 0, ord('b'))
        self.assertEqual(x, b'b')

        # bytes    
        self.assertRaises(System.InvalidOperationException, b'abc'.Remove, ord('a'))
        self.assertRaises(System.InvalidOperationException, b'abc'.Remove, ord('d'))    
        self.assertRaises(System.InvalidOperationException, b'abc'.Clear)    
        self.assertRaises(System.InvalidOperationException, b'abc'.Add, ord('a'))    
        self.assertRaises(System.InvalidOperationException, b'abc'.Insert, 0, ord('b'))    
        self.assertRaises(System.InvalidOperationException, b'abc'.RemoveAt, 0)    
        self.assertRaises(System.InvalidOperationException, System.Collections.Generic.IList[System.Byte].__setitem__, b'abc', 0, ord('b'))
        
        lst = System.Collections.Generic.List[System.Byte]()
        lst.Add(42)
        self.assertEqual(ord(lst), 42)
        lst.Add(42)
        self.assertRaisesMessage(TypeError, "expected a character, but string of length 2 found", ord, lst)

    def test_bytes_hashing(self):
        """test interaction of bytes w/ hashing modules"""
        import _sha, _sha256, _sha512, _md5
        
        for hashLib in (_sha.new, _sha256.sha256, _sha512.sha512, _sha512.sha384, _md5.new):
            x = hashLib(b'abc')
            x.update(b'abc')
            
            #For now just make sure this doesn't throw
            temp = hashLib(bytearray(b'abc'))
            x.update(bytearray(b'abc'))

    def test_cp35493(self):
        self.assertEqual(bytearray('\xde\xad\xbe\xef\x80'), bytearray(b'\xde\xad\xbe\xef\x80'))

run_test(__name__)
