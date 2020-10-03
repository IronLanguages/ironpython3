# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Test codecs, in addition to test_codecs from StdLib and from modules/io_related
##

import unittest
import codecs

from iptest import run_test, is_cli

if is_cli:
    import clr
    import System
    import System.Text
    clr.AddReference("System.Memory")

class CodecsTest(unittest.TestCase):

    @unittest.skipUnless(is_cli, "Interop with CLI")
    def test_interop_ascii(self):
        self.assertEqual("abc".encode(System.Text.Encoding.ASCII), b"abc")
        self.assertEqual(b"abc".decode(System.Text.Encoding.ASCII), "abc")

        us_ascii = System.Text.ASCIIEncoding()
        self.assertEqual("abc".encode(us_ascii), b"abc")
        self.assertEqual(b"abc".decode(us_ascii), "abc")

    def test_interop_utf8(self):
        if is_cli:
            utf_8 = System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier=False, throwOnInvalidBytes=True)
            utf_8_sig = System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier=True, throwOnInvalidBytes=True)

            self.assertEqual("abć".encode(utf_8), b"ab\xc4\x87")
            self.assertEqual(b"ab\xc4\x87".decode(utf_8), "abć")
            self.assertEqual(b"ab\xc4\x87".decode(utf_8_sig), "abć")

            self.assertEqual("abć".encode(utf_8_sig), b"\xef\xbb\xbfab\xc4\x87")
            self.assertEqual(b"\xef\xbb\xbfab\xc4\x87".decode(utf_8), "\ufeffabć")
            self.assertEqual(b"\xef\xbb\xbfab\xc4\x87".decode(utf_8_sig), "abć")

        # now for comparison the same but with codec names
        self.assertEqual("abć".encode('utf_8'), b"ab\xc4\x87")
        self.assertEqual(b"ab\xc4\x87".decode('utf_8'), "abć")
        self.assertEqual(b"ab\xc4\x87".decode('utf_8_sig'), "abć")

        self.assertEqual("abć".encode('utf_8_sig'), b"\xef\xbb\xbfab\xc4\x87")
        self.assertEqual(b"\xef\xbb\xbfab\xc4\x87".decode('utf_8'), "\ufeffabć")
        self.assertEqual(b"\xef\xbb\xbfab\xc4\x87".decode('utf_8_sig'), "abć")

    def test_interop_utf16(self):
        if is_cli:
            utf_16 = System.Text.UnicodeEncoding(bigEndian=False, byteOrderMark=True, throwOnInvalidBytes=True)
            utf_16_le = System.Text.UnicodeEncoding(bigEndian=False, byteOrderMark=False, throwOnInvalidBytes=True)
            utf_16_be = System.Text.UnicodeEncoding(bigEndian=True, byteOrderMark=False, throwOnInvalidBytes=True)

            self.assertEqual("abć".encode(utf_16), b"\xff\xfea\x00b\x00\x07\x01")
            self.assertEqual(b"\xff\xfea\x00b\x00\x07\x01".decode(utf_16), "abć")
            self.assertEqual(b"\xff\xfea\x00b\x00\x07\x01".decode(utf_16_le), "\ufeffabć")

            self.assertEqual("abć".encode(utf_16_le), b"a\x00b\x00\x07\x01")
            self.assertEqual(b"a\x00b\x00\x07\x01".decode(utf_16), "abć")
            self.assertEqual(b"a\x00b\x00\x07\x01".decode(utf_16_le), "abć")

            self.assertEqual("abć".encode(utf_16_be), b"\x00a\x00b\x01\x07")
            self.assertEqual(b"\x00a\x00b\x01\x07".decode(utf_16_be), "abć")

        # now for comparison the same but with codec names
        self.assertEqual("abć".encode('utf_16'), b"\xff\xfea\x00b\x00\x07\x01")
        self.assertEqual(b"\xff\xfea\x00b\x00\x07\x01".decode('utf_16'), "abć")
        self.assertEqual(b"\xff\xfea\x00b\x00\x07\x01".decode('utf_16_le'), "\ufeffabć")

        self.assertEqual("abć".encode('utf_16_le'), b"a\x00b\x00\x07\x01")
        self.assertEqual(b"a\x00b\x00\x07\x01".decode('utf_16'), "abć")
        self.assertEqual(b"a\x00b\x00\x07\x01".decode('utf_16_le'), "abć")

        self.assertEqual("abć".encode('utf_16_be'), b"\x00a\x00b\x01\x07")
        self.assertEqual(b"\x00a\x00b\x01\x07".decode('utf_16_be'), "abć")

    def test_interop_utf32(self):
        if is_cli:
            utf_32 = System.Text.UTF32Encoding(bigEndian=False, byteOrderMark=True, throwOnInvalidCharacters=True)
            utf_32_le = System.Text.UTF32Encoding(bigEndian=False, byteOrderMark=False, throwOnInvalidCharacters=True)
            utf_32_be = System.Text.UTF32Encoding(bigEndian=True, byteOrderMark=False, throwOnInvalidCharacters=True)

            self.assertEqual("abć".encode(utf_32), b"\xff\xfe\x00\x00a\x00\x00\x00b\x00\x00\x00\x07\x01\x00\x00")
            self.assertEqual(b"\xff\xfe\x00\x00a\x00\x00\x00b\x00\x00\x00\x07\x01\x00\x00".decode(utf_32), "abć")
            self.assertEqual(b"\xff\xfe\x00\x00a\x00\x00\x00b\x00\x00\x00\x07\x01\x00\x00".decode(utf_32_le), "\ufeffabć")

            self.assertEqual("abć".encode(utf_32_le), b"a\x00\x00\x00b\x00\x00\x00\x07\x01\x00\x00")
            self.assertEqual(b"a\x00\x00\x00b\x00\x00\x00\x07\x01\x00\x00".decode(utf_32), "abć")
            self.assertEqual(b"a\x00\x00\x00b\x00\x00\x00\x07\x01\x00\x00".decode(utf_32_le), "abć")

            self.assertEqual("abć".encode(utf_32_be), b"\x00\x00\x00a\x00\x00\x00b\x00\x00\x01\x07")
            self.assertEqual(b"\x00\x00\x00a\x00\x00\x00b\x00\x00\x01\x07".decode(utf_32_be), "abć")

        # now for comparison the same but with codec names
        self.assertEqual("abć".encode('utf_32'), b"\xff\xfe\x00\x00a\x00\x00\x00b\x00\x00\x00\x07\x01\x00\x00")
        self.assertEqual(b"\xff\xfe\x00\x00a\x00\x00\x00b\x00\x00\x00\x07\x01\x00\x00".decode('utf_32'), "abć")
        self.assertEqual(b"\xff\xfe\x00\x00a\x00\x00\x00b\x00\x00\x00\x07\x01\x00\x00".decode('utf_32_le'), "\ufeffabć")

        self.assertEqual("abć".encode('utf_32_le'), b"a\x00\x00\x00b\x00\x00\x00\x07\x01\x00\x00")
        self.assertEqual(b"a\x00\x00\x00b\x00\x00\x00\x07\x01\x00\x00".decode('utf_32'), "abć")
        self.assertEqual(b"a\x00\x00\x00b\x00\x00\x00\x07\x01\x00\x00".decode('utf_32_le'), "abć")

        self.assertEqual("abć".encode('utf_32_be'), b"\x00\x00\x00a\x00\x00\x00b\x00\x00\x01\x07")
        self.assertEqual(b"\x00\x00\x00a\x00\x00\x00b\x00\x00\x01\x07".decode('utf_32_be'), "abć")

    @unittest.skipUnless(is_cli, "Interop with CLI")
    def test_interop_array(self):
        arr = System.Array[System.Byte](b"abc")
        ars = System.ArraySegment[System.Byte](arr)
        mem = System.Memory[System.Byte](arr)
        rom = System.ReadOnlyMemory[System.Byte](arr)

        self.assertEqual(codecs.latin_1_decode(arr), ("abc", 3))
        self.assertEqual(codecs.latin_1_decode(ars), ("abc", 3))
        self.assertEqual(codecs.latin_1_decode(mem), ("abc", 3))
        self.assertEqual(codecs.latin_1_decode(rom), ("abc", 3))

    def test_interop_ascii_encode_exception(self):
        def check_error1(encoding, name):
            # exception on a single character
            with self.assertRaises(UnicodeEncodeError) as uee:
                "abćẋyz".encode(encoding)
            self.assertEqual(uee.exception.encoding, name)
            self.assertEqual(uee.exception.object, "abćẋyz")
            self.assertEqual(uee.exception.start, 2)
            self.assertGreaterEqual(uee.exception.end, 3) # CPython 4 (all bad characers), IronPython 3 (first bad character)
            self.assertLessEqual(uee.exception.end, 4)

        def check_error2(encoding, name):
            # exception on a surrogate pair
            with self.assertRaises(UnicodeEncodeError) as uee:
                "ab🜋yz".encode(encoding)
            self.assertEqual(uee.exception.encoding, name)
            self.assertEqual(uee.exception.object, "ab🜋yz")
            self.assertEqual(uee.exception.start, 2)
            self.assertGreaterEqual(uee.exception.end, 3) # CPython 3 (single character), IronPython 4 (surrogate pair)
            self.assertLessEqual(uee.exception.end, 4)

        if is_cli:
            check_error1(System.Text.ASCIIEncoding(), 'us-ascii')
            check_error2(System.Text.ASCIIEncoding(), 'us-ascii')
            check_error1(System.Text.Encoding.ASCII, 'us-ascii')
            check_error2(System.Text.Encoding.ASCII, 'us-ascii')

        check_error1('ascii', 'ascii')
        if not is_cli: # TODO: Replace PythonAsciiEncoding with ASCIIEncoding
            check_error2('ascii', 'ascii')

    def test_interop_utf8_encode_exception(self):
        def check_error(encoding, name):
            # exception on a lone surrogate
            with self.assertRaises(UnicodeEncodeError) as uee:
                "abć\uddddẋyz".encode(encoding)
            self.assertEqual(uee.exception.encoding, name)
            self.assertEqual(uee.exception.object, "abć\uddddẋyz")
            self.assertEqual(uee.exception.start, 3)
            self.assertEqual(uee.exception.end, 4)

        if is_cli:
            check_error(System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier=False, throwOnInvalidBytes=True), 'utf-8')
            check_error(System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier=True, throwOnInvalidBytes=True), 'utf-8')
            check_error(System.Text.Encoding.UTF8, 'utf-8')

        check_error('utf-8', 'utf-8')
        if is_cli:
            check_error('utf-8-sig', 'utf-8-sig')
        else:
            check_error('utf-8-sig', 'utf-8')

    def test_interop_utf16_encode_exception(self):
        def check_error(encoding, name):
            # exception on a lone surrogate
            with self.assertRaises(UnicodeEncodeError) as uee:
                "abć\uddddẋyz".encode(encoding)
            self.assertEqual(uee.exception.encoding, name)
            self.assertEqual(uee.exception.object, "abć\uddddẋyz")
            self.assertEqual(uee.exception.start, 3)
            self.assertEqual(uee.exception.end, 4)

        if is_cli:
            check_error(System.Text.UnicodeEncoding(bigEndian=False, byteOrderMark=True, throwOnInvalidBytes=True), 'utf-16LE')
            check_error(System.Text.UnicodeEncoding(bigEndian=True, byteOrderMark=True, throwOnInvalidBytes=True), 'utf-16BE')

        check_error('utf-16', 'utf-16') # TODO: should be 'utf-16LE' (CPython: 'utf-16-le')

    def test_interop_ascii_decode_exception(self):
        def check_error(encoding, name):
            with self.assertRaises(UnicodeDecodeError) as ude:
                b"abc\xffxyz".decode(encoding)
            self.assertEqual(ude.exception.encoding, name)
            self.assertEqual(ude.exception.object, b"abc\xffxyz")
            self.assertEqual(ude.exception.start, 3)
            self.assertEqual(ude.exception.end, 4)

        if is_cli:
            check_error(System.Text.ASCIIEncoding(), 'us-ascii')
            check_error(System.Text.Encoding.ASCII, 'us-ascii')

        check_error('ascii', 'ascii')

    def test_interop_utf8_decode_exception(self):
        def check_error(encoding, name):
            with self.assertRaises(UnicodeDecodeError) as ude:
                # broken input (� is 0xff): "abć�ẋyz"
                # does not work with 'strict' due to UTF-8 bug https://github.com/dotnet/corefx/issues/29898
                b"ab\xc4\x87\xff\xe1\xba\x8byz".decode(encoding,'surrogatepass')
            self.assertEqual(ude.exception.encoding, name)
            self.assertEqual(ude.exception.object, b"ab\xc4\x87\xff\xe1\xba\x8byz")
            self.assertEqual(ude.exception.start, 4)
            self.assertEqual(ude.exception.end, 5)

        if is_cli:
            check_error(System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier=False, throwOnInvalidBytes=True), 'utf-8')
            check_error(System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier=True, throwOnInvalidBytes=True), 'utf-8')
            check_error(System.Text.Encoding.UTF8, 'utf-8')

        check_error('utf-8', 'utf-8')

    def test_interop_utf8bom_decode_exception(self):
        def check_error(encoding, name):
            with self.assertRaises(UnicodeDecodeError) as ude:
                # broken input (� is 0xff): BOM + "abć�ẋyz"
                # does not work with 'strict' due to UTF-8 bug https://github.com/dotnet/corefx/issues/29898
                b"\xef\xbb\xbfab\xc4\x87\xff\xe1\xba\x8byz".decode(encoding,'surrogatepass')
            self.assertEqual(ude.exception.encoding, name)
            # regular utf-8 retains BOM if present
            self.assertEqual(ude.exception.object, b"\xef\xbb\xbfab\xc4\x87\xff\xe1\xba\x8byz")
            self.assertEqual(ude.exception.start, 7)
            self.assertEqual(ude.exception.end, 8)

        if is_cli:
            check_error(System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier=False, throwOnInvalidBytes=True), 'utf-8')

        check_error('utf-8', 'utf-8')

    def test_interop_utf8sigbom_decode_exception(self):
        def check_error(encoding, name):
            with self.assertRaises(UnicodeDecodeError) as ude:
                # broken input (� is 0xff): BOM_UTF8 + "abć�ẋyz"
                # does not work with 'strict' due to UTF-8 bug https://github.com/dotnet/corefx/issues/29898
                b"\xef\xbb\xbfab\xc4\x87\xff\xe1\xba\x8byz".decode(encoding,'surrogatepass')
            self.assertEqual(ude.exception.encoding, name)
            # utf-8 with signature skips BOM
            self.assertEqual(ude.exception.object, b"ab\xc4\x87\xff\xe1\xba\x8byz")
            self.assertEqual(ude.exception.start, 4)
            self.assertEqual(ude.exception.end, 5)

        if is_cli:
            check_error(System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier=True, throwOnInvalidBytes=True), 'utf-8')
            check_error(System.Text.Encoding.UTF8, 'utf-8')

        if is_cli:
            check_error('utf-8-sig', 'utf-8-sig')
        else:
            check_error('utf-8-sig', 'utf-8')

    def test_interop_utf16_decode_exception(self):
        def check_error(encoding, name):
            with self.assertRaises(UnicodeDecodeError) as ude:
                # broken input (� is lone surrogate 0xdddd): BOM_UTF16_LE + "abć�ẋyz"
                b'\xff\xfea\x00b\x00\x07\x01\xdd\xdd\x8b\x1ey\x00z\x00'.decode(encoding,'strict')
            self.assertEqual(ude.exception.encoding, name)
            # regular utf-16 skips BOM
            # NOTE: CPython is not consistent in this behavior, possibly a CPython bug (utf-8-sig behaves correctly)
            if is_cli:
                self.assertEqual(ude.exception.object, b'a\x00b\x00\x07\x01\xdd\xdd\x8b\x1ey\x00z\x00')
                self.assertEqual(ude.exception.start, 6)
                self.assertEqual(ude.exception.end, 8)
            else:
                self.assertEqual(ude.exception.object, codecs.BOM_UTF16_LE + b'a\x00b\x00\x07\x01\xdd\xdd\x8b\x1ey\x00z\x00')
                self.assertEqual(ude.exception.start, 8)
                self.assertEqual(ude.exception.end, 10)

        if is_cli:
            check_error(System.Text.UnicodeEncoding(bigEndian=False, byteOrderMark=True, throwOnInvalidBytes=True), 'utf-16LE')

        if is_cli:
            check_error('utf-16', 'utf-16') # TODO: should be 'utf-16LE'
        else:
            check_error('utf-16', 'utf-16-le')

    def test_interop_utf16le_decode_exception(self):
        def check_error(encoding, name):
            with self.assertRaises(UnicodeDecodeError) as ude:
                # broken input (� is lone surrogate 0xdddd): BOM_UTF16_LE + "abć�ẋyz"
                b'\xff\xfea\x00b\x00\x07\x01\xdd\xdd\x8b\x1ey\x00z\x00'.decode(encoding,'strict')
            self.assertEqual(ude.exception.encoding, name)
            # utf-16LE treats BOM as a regular character
            self.assertEqual(ude.exception.object, b'\xff\xfea\x00b\x00\x07\x01\xdd\xdd\x8b\x1ey\x00z\x00')
            self.assertEqual(ude.exception.start, 8)
            self.assertEqual(ude.exception.end, 10)

        if is_cli:
            check_error(System.Text.UnicodeEncoding(bigEndian=False, byteOrderMark=False, throwOnInvalidBytes=True), 'utf-16LE')

        if is_cli:
            check_error('utf-16LE', 'utf-16LE')
        else:
            check_error('utf-16LE', 'utf-16-le')

run_test(__name__)
