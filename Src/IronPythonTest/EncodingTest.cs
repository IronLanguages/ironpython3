// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using IronPython.Runtime;
using NUnit.Framework;
using System.Linq;
using System.Text;

namespace IronPythonTest {

    // Unit testing class PythonSurrogateEscapeEncoding
    [TestFixture(Category = "IronPython")]
    public class SurrogateEscapeTest {

        #region Round-trip tests

        // Test 256 bytes sequence
        public class Bytes256Test {

            private byte[] _bytes;

            [SetUp]
            public void SetUp() {
                _bytes = Enumerable.Range(0, 256).Select(c => (byte)c).ToArray();
            }

            [Test] public void Test256WithAscii() => TestRoundTrip(Encoding.ASCII, _bytes);
            [Test] public void Test256WithUtf8() => TestRoundTrip(Encoding.UTF8, _bytes);
            [Test] public void Test256WithDefault() => TestRoundTrip(Encoding.Default, _bytes);
            [Test] public void Test256WithUnicode() => TestRoundTrip(Encoding.Unicode, _bytes);
            [Test] public void Test256WithBigEndianUnicode() => TestRoundTrip(Encoding.BigEndianUnicode, _bytes);
            [Test] public void Test256WithUtf32() => TestRoundTrip(Encoding.UTF32, _bytes);
            [Test] public void Test256WithUtf32BE() => TestRoundTrip(new UTF32Encoding(bigEndian: true, byteOrderMark: false), _bytes);
        }

        // Test decoding/encoding a valid UTF-8 sequence
        public class Utf8Test {

            private byte[] _bytes;

            [SetUp]
            public void SetUp() {
                // 12 bytes, rounded to multiply of 4 for the sake of UTF-32 test
                _bytes = "\xd0\x9f\xd0\xb8\xd1\x82\xd0\xbe\xd0\xbd!!".AsBytes();
            }

            [Test] public void TestValidUtf8WithAscii() => TestRoundTrip(Encoding.ASCII, _bytes);
            [Test] public void TestValidUtf8WithUtf8() => TestRoundTrip(Encoding.UTF8, _bytes);
            [Test] public void TestValidUtf8WithDefault() => TestRoundTrip(Encoding.Default, _bytes);
            [Test] public void TestValidUtf8WithUnicode() => TestRoundTrip(Encoding.Unicode, _bytes);
            [Test] public void TestValidUtf8WithBigEndianUnicode() => TestRoundTrip(Encoding.BigEndianUnicode, _bytes);
            [Test] public void TestValidUtf8WithUtf32() => TestRoundTrip(Encoding.UTF32, _bytes);
            [Test] public void TestValidUtf8WithUtf32BE() => TestRoundTrip(new UTF32Encoding(bigEndian: true, byteOrderMark: false), _bytes);
        }

        // Test decoding/encoding an invalid UTF-8 sequence
        public class Utf8BrokenTest {

            private byte[] _bytes;

            [SetUp]
            public void SetUp() {
                // 12 bytes: two valid UTF-8 2-byte chars, one non-decodable byte, 
                // one UTF-8 2-byte char with a non-decodable byte inserted in between the UTF-8 bytes
                // and final valid UTF-8 2-byte char
                _bytes = "\xd0\x9f\xd0\xb8\x80\xd1\x20\x82\xd0\xbe\xd0\xbd".AsBytes();
            }

            [Test] public void TestBrokenUtf8WithAscii() => TestRoundTrip(Encoding.ASCII, _bytes);
            [Test] public void TestBrokenUtf8WithUtf8() => TestRoundTrip(Encoding.UTF8, _bytes);
            [Test] public void TestBrokenUtf8WithDefault() => TestRoundTrip(Encoding.Default, _bytes);
            [Test] public void TestBrokenUtf8WithUnicode() => TestRoundTrip(Encoding.Unicode, _bytes);
            [Test] public void TestBrokenUtf8WithBigEndianUnicode() => TestRoundTrip(Encoding.BigEndianUnicode, _bytes);
            [Test] public void TestBrokenUtf8WithUtf32() => TestRoundTrip(Encoding.UTF32, _bytes);
            [Test] public void TestBrokenUtf8WithUtf32BE() => TestRoundTrip(new UTF32Encoding(bigEndian: true, byteOrderMark: false), _bytes);
        }

        // Note: UTF-7 is not round-trip safe in general
        private static void TestRoundTrip(Encoding enc, byte[] bytes) {

            Encoding penc = new PythonSurrogateEscapeEncoding(enc);
            char[] chars1 = new char[penc.GetCharCount(bytes)];
            penc.GetChars(bytes, 0, bytes.Length, chars1, 0);
            char[] chars2 = penc.GetChars(bytes);
            Assert.AreEqual(chars1, chars2);

            byte[] bytes1 = penc.GetBytes(chars1);
            byte[] bytes2 = new byte[penc.GetByteCount(chars1, 0, chars1.Length)];
            penc.GetBytes(chars1, 0, chars1.Length, bytes2, 0);
            Assert.AreEqual(bytes1, bytes2);
            Assert.AreEqual(bytes, bytes1);
        }

        #endregion

        #region Tests comparing with CPython results

        // Test 256 bytes sequence
        public class CPythonCompare256Tests {

            private byte[] bytes;

            [SetUp]
            public void SetUp() {
                bytes = Enumerable.Range(0, 256).Select(c => (byte)c).ToArray();
            }

            // Compare ASCII handling with CPython results
            [Test]
            public void TestCompare256WithAscii() {
                Encoding penc = new PythonSurrogateEscapeEncoding(Encoding.ASCII);
                char[] chars = penc.GetChars(bytes);
                string python_chars = "\x00\x01\x02\x03\x04\x05\x06\x07\x08\t\n\x0b\x0c\r\x0e\x0f\x10\x11\x12\x13\x14\x15\x16\x17\x18\x19\x1a\x1b\x1c\x1d\x1e\x1f !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~\x7f\udc80\udc81\udc82\udc83\udc84\udc85\udc86\udc87\udc88\udc89\udc8a\udc8b\udc8c\udc8d\udc8e\udc8f\udc90\udc91\udc92\udc93\udc94\udc95\udc96\udc97\udc98\udc99\udc9a\udc9b\udc9c\udc9d\udc9e\udc9f\udca0\udca1\udca2\udca3\udca4\udca5\udca6\udca7\udca8\udca9\udcaa\udcab\udcac\udcad\udcae\udcaf\udcb0\udcb1\udcb2\udcb3\udcb4\udcb5\udcb6\udcb7\udcb8\udcb9\udcba\udcbb\udcbc\udcbd\udcbe\udcbf\udcc0\udcc1\udcc2\udcc3\udcc4\udcc5\udcc6\udcc7\udcc8\udcc9\udcca\udccb\udccc\udccd\udcce\udccf\udcd0\udcd1\udcd2\udcd3\udcd4\udcd5\udcd6\udcd7\udcd8\udcd9\udcda\udcdb\udcdc\udcdd\udcde\udcdf\udce0\udce1\udce2\udce3\udce4\udce5\udce6\udce7\udce8\udce9\udcea\udceb\udcec\udced\udcee\udcef\udcf0\udcf1\udcf2\udcf3\udcf4\udcf5\udcf6\udcf7\udcf8\udcf9\udcfa\udcfb\udcfc\udcfd\udcfe\udcff";
                Assert.AreEqual(python_chars, chars);
            }


            // Compare UTF-8 handling with CPython results
            [Test]
            public void TestCompare256WithUtf8() {
                Encoding penc = new PythonSurrogateEscapeEncoding(Encoding.UTF8);
                char[] chars = penc.GetChars(bytes);
                string python_chars = "\x00\x01\x02\x03\x04\x05\x06\x07\x08\t\n\x0b\x0c\r\x0e\x0f\x10\x11\x12\x13\x14\x15\x16\x17\x18\x19\x1a\x1b\x1c\x1d\x1e\x1f !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~\x7f\udc80\udc81\udc82\udc83\udc84\udc85\udc86\udc87\udc88\udc89\udc8a\udc8b\udc8c\udc8d\udc8e\udc8f\udc90\udc91\udc92\udc93\udc94\udc95\udc96\udc97\udc98\udc99\udc9a\udc9b\udc9c\udc9d\udc9e\udc9f\udca0\udca1\udca2\udca3\udca4\udca5\udca6\udca7\udca8\udca9\udcaa\udcab\udcac\udcad\udcae\udcaf\udcb0\udcb1\udcb2\udcb3\udcb4\udcb5\udcb6\udcb7\udcb8\udcb9\udcba\udcbb\udcbc\udcbd\udcbe\udcbf\udcc0\udcc1\udcc2\udcc3\udcc4\udcc5\udcc6\udcc7\udcc8\udcc9\udcca\udccb\udccc\udccd\udcce\udccf\udcd0\udcd1\udcd2\udcd3\udcd4\udcd5\udcd6\udcd7\udcd8\udcd9\udcda\udcdb\udcdc\udcdd\udcde\udcdf\udce0\udce1\udce2\udce3\udce4\udce5\udce6\udce7\udce8\udce9\udcea\udceb\udcec\udced\udcee\udcef\udcf0\udcf1\udcf2\udcf3\udcf4\udcf5\udcf6\udcf7\udcf8\udcf9\udcfa\udcfb\udcfc\udcfd\udcfe\udcff";
                Assert.AreEqual(python_chars, chars);
            }


            // Compare Windows-1252 (Western European Windows, variant of ISO-8859-1) handling with CPython results
#if !NETCOREAPP2_1
            // Windows-1252 is not available on .NET Core
            [Test]
            public void TestCompare256WithWindows1252() {
                Encoding penc = new PythonSurrogateEscapeEncoding(Encoding.GetEncoding(1252));
                Assert.AreEqual("iso-8859-1-surrogateescape", penc.WebName);

                char[] chars = penc.GetChars(bytes);
                string python_chars = "\x00\x01\x02\x03\x04\x05\x06\x07\x08\t\n\x0b\x0c\r\x0e\x0f\x10\x11\x12\x13\x14\x15\x16\x17\x18\x19\x1a\x1b\x1c\x1d\x1e\x1f !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~\x7f€\udc81‚ƒ„…†‡ˆ‰Š‹Œ\udc8dŽ\udc8f\udc90‘’“”•–—˜™š›œ\udc9džŸ\xa0¡¢£¤¥¦§¨©ª«¬\xad®¯°±²³´µ¶·¸¹º»¼½¾¿ÀÁÂÃÄÅÆÇÈÉÊËÌÍÎÏÐÑÒÓÔÕÖ×ØÙÚÛÜÝÞßàáâãäåæçèéêëìíîïðñòóôõö÷øùúûüýþÿ";
                string encoded = new string(chars);
                Assert.AreEqual(python_chars.Length, encoded.Length);
                for (int i = 0; i < encoded.Length; i++) {
                    if (encoded[i] != python_chars[i]) {
                        // Known differences between Windows and Python (Unicode) implementation of Windows-1252
                        // https://en.wikipedia.org/wiki/Windows-1252
                        CollectionAssert.Contains(new[] { 0x81, 0x8d, 0x8f, 0x90, 0x9d }, i);
                    }
                }
            }
#endif

            // Compare  ISO-8859-1 (Western European) handling with CPython results
            [Test]
            public void TestCompare256WithIso8859_1() {
                Encoding penc = new PythonSurrogateEscapeEncoding(Encoding.GetEncoding(28591));
                Assert.AreEqual("iso-8859-1-surrogateescape", penc.WebName);

                char[] chars = penc.GetChars(bytes);
                string python_chars = "\x00\x01\x02\x03\x04\x05\x06\x07\x08\t\n\x0b\x0c\r\x0e\x0f\x10\x11\x12\x13\x14\x15\x16\x17\x18\x19\x1a\x1b\x1c\x1d\x1e\x1f !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~\x7f\x80\x81\x82\x83\x84\x85\x86\x87\x88\x89\x8a\x8b\x8c\x8d\x8e\x8f\x90\x91\x92\x93\x94\x95\x96\x97\x98\x99\x9a\x9b\x9c\x9d\x9e\x9f\xa0¡¢£¤¥¦§¨©ª«¬\xad®¯°±²³´µ¶·¸¹º»¼½¾¿ÀÁÂÃÄÅÆÇÈÉÊËÌÍÎÏÐÑÒÓÔÕÖ×ØÙÚÛÜÝÞßàáâãäåæçèéêëìíîïðñòóôõö÷øùúûüýþÿ";
                string encoded = new string(chars);
                Assert.AreEqual(python_chars, encoded);
            }

            // Compare UTF-7 handling with CPython results
            [Test]
            public void TestCompare256WithUtf7() {
                Encoding penc = new PythonSurrogateEscapeEncoding(Encoding.UTF7);
                // The following Python output is produced with python 3.4 but is not correct: it is missing the '+' character
                string python_chars = "\x00\x01\x02\x03\x04\x05\x06\x07\x08\t\n\x0b\x0c\r\x0e\x0f\x10\x11\x12\x13\x14\x15\x16\x17\x18\x19\x1a\x1b\x1c\x1d\x1e\x1f !\"#$%&'()*,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~\x7f\udc80\udc81\udc82\udc83\udc84\udc85\udc86\udc87\udc88\udc89\udc8a\udc8b\udc8c\udc8d\udc8e\udc8f\udc90\udc91\udc92\udc93\udc94\udc95\udc96\udc97\udc98\udc99\udc9a\udc9b\udc9c\udc9d\udc9e\udc9f\udca0\udca1\udca2\udca3\udca4\udca5\udca6\udca7\udca8\udca9\udcaa\udcab\udcac\udcad\udcae\udcaf\udcb0\udcb1\udcb2\udcb3\udcb4\udcb5\udcb6\udcb7\udcb8\udcb9\udcba\udcbb\udcbc\udcbd\udcbe\udcbf\udcc0\udcc1\udcc2\udcc3\udcc4\udcc5\udcc6\udcc7\udcc8\udcc9\udcca\udccb\udccc\udccd\udcce\udccf\udcd0\udcd1\udcd2\udcd3\udcd4\udcd5\udcd6\udcd7\udcd8\udcd9\udcda\udcdb\udcdc\udcdd\udcde\udcdf\udce0\udce1\udce2\udce3\udce4\udce5\udce6\udce7\udce8\udce9\udcea\udceb\udcec\udced\udcee\udcef\udcf0\udcf1\udcf2\udcf3\udcf4\udcf5\udcf6\udcf7\udcf8\udcf9\udcfa\udcfb\udcfc\udcfd\udcfe\udcff";
                // Our implementation will refuse to decode (correctly) because the ',' after '+' is not valid thus requires escaping,
                // but escaping of chars under 128 is not allowed.
                Assert.Throws<DecoderFallbackException>(() => penc.GetChars(bytes));

                // Let's try again without the '+'
                bytes = bytes.Where(i => i != (byte)'+').ToArray();
                char[] chars = penc.GetChars(bytes);
                Assert.AreEqual(python_chars, chars);

                // Now the encoding part
                byte[] encoded_bytes = penc.GetBytes(chars);
                byte[] expected_bytes = "+AAAAAQACAAMABAAFAAYABwAI-\t\n+AAsADA-\r+AA4ADwAQABEAEgATABQAFQAWABcAGAAZABoAGwAcAB0AHgAf- +ACEAIgAjACQAJQAm-'()+ACo-,-./0123456789:+ADsAPAA9AD4-?+AEA-ABCDEFGHIJKLMNOPQRSTUVWXYZ+AFsAXABdAF4AXwBg-abcdefghijklmnopqrstuvwxyz+AHsAfAB9AH4Af9yA3IHcgtyD3ITchdyG3IfciNyJ3Irci9yM3I3cjtyP3JDckdyS3JPclNyV3Jbcl9yY3Jncmtyb3Jzcndye3J/coNyh3KLco9yk3KXcptyn3Kjcqdyq3KvcrNyt3K7cr9yw3LHcstyz3LTctdy23LfcuNy53Lrcu9y83L3cvty/3MDcwdzC3MPcxNzF3Mbcx9zI3MncytzL3MzczdzO3M/c0NzR3NLc09zU3NXc1tzX3Njc2dza3Nvc3Nzd3N7c39zg3OHc4tzj3OTc5dzm3Ofc6Nzp3Orc69zs3O3c7tzv3PDc8dzy3PPc9Nz13Pbc99z43Pnc+tz73Pzc/dz+3P8-"
                    .Select(c => (byte)c).ToArray();
                Assert.AreEqual(expected_bytes, encoded_bytes);

                // Encoding the given chars with CPython produces the following byte string
                byte[] python_bytes =   "+AAAAAQACAAMABAAFAAYABwAI\t\n+AAsADA\r+AA4ADwAQABEAEgATABQAFQAWABcAGAAZABoAGwAcAB0AHgAf !\"#$%&'()*,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[+AFw]^_`abcdefghijklmnopqrstuvwxyz{|}+AH4Af9yA3IHcgtyD3ITchdyG3IfciNyJ3Irci9yM3I3cjtyP3JDckdyS3JPclNyV3Jbcl9yY3Jncmtyb3Jzcndye3J/coNyh3KLco9yk3KXcptyn3Kjcqdyq3KvcrNyt3K7cr9yw3LHcstyz3LTctdy23LfcuNy53Lrcu9y83L3cvty/3MDcwdzC3MPcxNzF3Mbcx9zI3MncytzL3MzczdzO3M/c0NzR3NLc09zU3NXc1tzX3Njc2dza3Nvc3Nzd3N7c39zg3OHc4tzj3OTc5dzm3Ofc6Nzp3Orc69zs3O3c7tzv3PDc8dzy3PPc9Nz13Pbc99z43Pnc+tz73Pzc/dz+3P8-"
                    .Select(c => (byte)c).ToArray();
                // The sequences expected_bytes and python_bytes are NOT equal: .NET ends encoded blocks (starting with '+') with '-'
                // and encodes some additional characters, like !"#$%&*;<=>@{|}
                // Encoding those characters is optional, and terminating the encoded blocks with '-' is also optional.
                // CPython does not do it, resulting in a more compact encoding.
                // However, they both decode to the same text, although, again, CPython's version cannot be decoded using surrogateescape
                char[] dotnet_decoded = penc.GetChars(encoded_bytes);
                char[] python_decoded = Encoding.UTF7.GetChars(python_bytes);
                Assert.AreEqual(chars, python_decoded);
                Assert.AreEqual(chars, dotnet_decoded);
                dotnet_decoded = Encoding.UTF7.GetChars(encoded_bytes);
                Assert.AreEqual(chars, dotnet_decoded);
            }

            // Compare UTF-16 handling with CPython results
            [Test]
            public void TestCompare256Utf16() {
                Encoding penc = new PythonSurrogateEscapeEncoding(Encoding.Unicode);
                char[] chars = penc.GetChars(bytes);
                char[] python_chars = (new[] { 0x0100, 0x0302, 0x0504, 0x0706, 0x0908, 0x0b0a, 0x0d0c, 0x0f0e, 0x1110, 0x1312, 0x1514, 0x1716, 0x1918, 0x1b1a, 0x1d1c, 0x1f1e, 0x2120, 0x2322, 0x2524, 0x2726, 0x2928, 0x2b2a, 0x2d2c, 0x2f2e, 0x3130, 0x3332, 0x3534, 0x3736, 0x3938, 0x3b3a, 0x3d3c, 0x3f3e, 0x4140, 0x4342, 0x4544, 0x4746, 0x4948, 0x4b4a, 0x4d4c, 0x4f4e, 0x5150, 0x5352, 0x5554, 0x5756, 0x5958, 0x5b5a, 0x5d5c, 0x5f5e, 0x6160, 0x6362, 0x6564, 0x6766, 0x6968, 0x6b6a, 0x6d6c, 0x6f6e, 0x7170, 0x7372, 0x7574, 0x7776, 0x7978, 0x7b7a, 0x7d7c, 0x7f7e, 0x8180, 0x8382, 0x8584, 0x8786, 0x8988, 0x8b8a, 0x8d8c, 0x8f8e, 0x9190, 0x9392, 0x9594, 0x9796, 0x9998, 0x9b9a, 0x9d9c, 0x9f9e, 0xa1a0, 0xa3a2, 0xa5a4, 0xa7a6, 0xa9a8, 0xabaa, 0xadac, 0xafae, 0xb1b0, 0xb3b2, 0xb5b4, 0xb7b6, 0xb9b8, 0xbbba, 0xbdbc, 0xbfbe, 0xc1c0, 0xc3c2, 0xc5c4, 0xc7c6, 0xc9c8, 0xcbca, 0xcdcc, 0xcfce, 0xd1d0, 0xd3d2, 0xd5d4, 0xd7d6, 0xdcd8, 0xdcd9, 0x1069dc, 0xdcde, 0xdcdf, 0xe1e0, 0xe3e2, 0xe5e4, 0xe7e6, 0xe9e8, 0xebea, 0xedec, 0xefee, 0xf1f0, 0xf3f2, 0xf5f4, 0xf7f6, 0xf9f8, 0xfbfa, 0xfdfc, 0xfffe })
                        .SelectMany(i => i <= 0xffff ? ((char)i).ToString() : char.ConvertFromUtf32(i)).ToArray();
                Assert.AreEqual(python_chars, chars);

                // byte[] python_bytes = ??? - CPython fails to encode the string it decoded itself; a bug in CPython?
                byte[] bytes1 = penc.GetBytes(chars);
                Assert.AreEqual(bytes, bytes1);
            }
        }

        // Test sequence with surrogates
        public class CPythonCompareSurrogateTests {

            private byte[] bytes;

            [SetUp]
            public void SetUp() {
                // In UTF-16LE: Lone high surrogate (invalid), surrogate pair: high-low (valid), lone low surrogate (invalid)
                bytes = new byte[] { 0xd8, 0xd9, 0xda, 0xdb, 0xdc, 0xdd, 0xde, 0xdf };
            }

            [Test]
            public void TesWithtUtf16() {
                Encoding penc = new PythonSurrogateEscapeEncoding(Encoding.Unicode);
                char[] chars = penc.GetChars(bytes);
                char[] python_chars = (new[] { 0x0000dcd8, 0x0000dcd9, 0x001069dc, 0x0000dcde, 0x0000dcdf })
                        .SelectMany(i => i <= 0xffff ? ((char)i).ToString() : char.ConvertFromUtf32(i)).ToArray();
                Assert.AreEqual(python_chars, chars);

                // byte[] python_bytes = ??? - CPython fails on encoding the string it decoded itself; a bug in CPython?
                byte[] bytes1 = penc.GetBytes(chars);
                Assert.AreEqual(bytes, bytes1);
            }

            [Test]
            public void TestWithUtf32() {
                Encoding penc = new PythonSurrogateEscapeEncoding(Encoding.UTF32);
                char[] chars = penc.GetChars(bytes);
                char[] python_chars = (new[] { 0x0000dcd8, 0x0000dcd9, 0x0000dcda, 0x0000dcdb, 0x0000dcdc, 0x0000dcdd, 0x0000dcde, 0x0000dcdf })
                        .SelectMany(i => i <= 0xffff ? ((char)i).ToString() : char.ConvertFromUtf32(i)).ToArray();
                Assert.AreEqual(python_chars, chars);

                // byte[] python_bytes = ??? - CPython fails on encoding the string it decoded itself; a bug in CPython?
                byte[] bytes1 = penc.GetBytes(chars);
                Assert.AreEqual(bytes, bytes1);
            }
        }

        #endregion

        // Test block-wise decoding/encoding
        public class BlockWiseTests {

            private byte[] _bytes;

            [SetUp]
            public void SetUp() {
                // In UTF-16LE: Lone high surrogate (invalid), surrogate pair: high-low (valid), lone low surrogate (invalid)
                _bytes = new byte[] { 0xd8, 0xd9, 0xda, 0xdb, 0xdc, 0xdd, 0xde, 0xdf };
            }

            [Test]
            public void TestBlockWiseWithtUtf16() {
                Encoding penc = new PythonSurrogateEscapeEncoding(Encoding.Unicode);
                BlockWiseTest(penc);
            }

            [Test]
            public void TestBlockWiseWithUtf32() {
                Encoding penc = new PythonSurrogateEscapeEncoding(Encoding.UTF32);
                BlockWiseTest(penc);
            }

            [Test]
            public void TestBlockWiseWithUtf8() {
                // translate bytes in UTF-8 form
                Encoding pencUtf16 = new PythonSurrogateEscapeEncoding(Encoding.Unicode);
                Encoding pencUtf8 = new PythonSurrogateEscapeEncoding(Encoding.UTF8);
                _bytes = Encoding.Convert(pencUtf16, pencUtf8, _bytes);
                BlockWiseTest(pencUtf8);
            }

            private void BlockWiseTest(Encoding penc) { 
                // Reference for comparisons: chars encoded in one step
                char[] chars = penc.GetChars(_bytes);

                for (int splitBytesAt = 0; splitBytesAt <= _bytes.Length; splitBytesAt += 1) {
                    // From https://docs.microsoft.com/en-us/dotnet/api/system.text.decoder.getchars?view=netframework-4.5:
                    // The application should call GetCharCount on a block of data immediately before calling GetChars on the same block,
                    // so that any trailing bytes from the previous block are included in the calculation. 
                    var dec = penc.GetDecoder();
                    char[] chars1 = new char[dec.GetCharCount(_bytes, 0, splitBytesAt, flush: false)];
                    dec.GetChars(_bytes, 0, splitBytesAt, chars1, 0, flush: false);
                    char[] chars2 = new char[dec.GetCharCount(_bytes, splitBytesAt, _bytes.Length - splitBytesAt, flush: true)];
                    dec.GetChars(_bytes, splitBytesAt, _bytes.Length - splitBytesAt, chars2, 0, flush: true);

                    char[] total_chars = chars1.Concat(chars2).ToArray();
                    Assert.AreEqual(chars, total_chars);

                    for (int splitCharsAt = 1; splitCharsAt <= total_chars.Length; splitCharsAt += 1) {
                        // From https://docs.microsoft.com/en-us/dotnet/api/system.text.encoder.getbytecount?view=netframework-4.5:
                        // The application should call GetByteCount on a block of data immediately before calling GetBytes on the same block,
                        // so that any trailing characters from the previous block are included in the calculation.

                        var enc = penc.GetEncoder();
                        byte[] bytes1 = new byte[enc.GetByteCount(total_chars, 0, splitCharsAt, flush: false)];
                        enc.GetBytes(total_chars, 0, splitCharsAt, bytes1, 0, flush: false);
                        byte[] bytes2 = new byte[enc.GetByteCount(total_chars, splitCharsAt, total_chars.Length - splitCharsAt, flush: true)];
                        enc.GetBytes(total_chars, splitCharsAt, total_chars.Length - splitCharsAt, bytes2, 0, flush: true);

                        byte[] total_bytes = bytes1.Concat(bytes2).ToArray();
                        Assert.AreEqual(_bytes, total_bytes);
                    }
                }
            }
        }

        public class EndiannessTests {

            private byte[] _bytes1, _bytes2;

            // U+0A00 is an unassigned character, U+000A is LF
            [SetUp]
            public void SetUp() {
                _bytes1 = new byte[] { 0x0a, 0x00, 0x00, 0x00 };
                _bytes2 = _bytes1.Reverse().ToArray();
            }

            [Test]
            public void TestEndiannessWithtUtf16LE() {
                Encoding penc = new PythonSurrogateEscapeEncoding(Encoding.Unicode);
                Assert.AreEqual("\u000a\u0000", penc.GetChars(_bytes1));
                Assert.AreEqual("\u0000\u0a00", penc.GetChars(_bytes2));
            }

            [Test]
            public void TestEndiannessWithtUtf16BE() {
                Encoding penc = new PythonSurrogateEscapeEncoding(Encoding.BigEndianUnicode);
                Assert.AreEqual("\u0a00\u0000", penc.GetChars(_bytes1));
                Assert.AreEqual("\u0000\u000a", penc.GetChars(_bytes2));
            }

            [Test]
            public void TestEndiannessWithtUtf32LE() {
                Encoding penc = new PythonSurrogateEscapeEncoding(new UTF32Encoding(bigEndian: false, byteOrderMark: false));
                Assert.AreEqual("\u000a", penc.GetChars(_bytes1));
                Assert.AreEqual("\udc00\udc00\udc00\udc0a", penc.GetChars(_bytes2));
            }

            [Test]
            public void TestEndiannessWithtUtf32BE() {
                Encoding penc = new PythonSurrogateEscapeEncoding(new UTF32Encoding(bigEndian: true, byteOrderMark: false));
                Assert.AreEqual("\udc0a\udc00\udc00\udc00", penc.GetChars(_bytes1));
                Assert.AreEqual("\u000a", penc.GetChars(_bytes2));
            }
        }

        // Tests equivalent to selected CPython tests from the standard library
        // These tests can be deleted here once the corresponding CPython tests are included in the test coverage
        public class CPythonStdLib {

            // Tests from test_codecs module
            public class test_codecs {

                // Tests from SurrogateEscapeTest test case
                public class SurrogateEscapeTest {
                    /*
                    def test_utf8(self):
                        # Bad byte
                        self.assertEqual(b"foo\x80bar".decode("utf-8", "surrogateescape"),
                                         "foo\udc80bar")
                        self.assertEqual("foo\udc80bar".encode("utf-8", "surrogateescape"),
                                         b"foo\x80bar")
                        # bad-utf-8 encoded surrogate
                        self.assertEqual(b"\xed\xb0\x80".decode("utf-8", "surrogateescape"),
                                         "\udced\udcb0\udc80")
                        self.assertEqual("\udced\udcb0\udc80".encode("utf-8", "surrogateescape"),
                                         b"\xed\xb0\x80")
                    */
                    [Test]
                    public void test_utf8() {
                        Encoding penc = new PythonSurrogateEscapeEncoding(Encoding.UTF8);
                        // Bad byte
                        Assert.AreEqual("foo\udc80bar",
                            penc.GetChars("foo\u0080bar".AsBytes()));
                        Assert.AreEqual("foo\u0080bar".AsBytes(),
                            penc.GetBytes("foo\udc80bar"));
                        // bad-utf-8 encoded surogate
                        Assert.AreEqual("\udced\udcb0\udc80",
                            penc.GetChars("\xed\xb0\x80".AsBytes()));
                        Assert.AreEqual("\xed\xb0\x80".AsBytes(),
                            penc.GetBytes("\udced\udcb0\udc80"));
                    }

                    /*
                    def test_ascii(self):
                        # bad byte
                        self.assertEqual(b"foo\x80bar".decode("ascii", "surrogateescape"),
                                         "foo\udc80bar")
                        self.assertEqual("foo\udc80bar".encode("ascii", "surrogateescape"),
                                         b"foo\x80bar")
                    */
                    [Test]
                    public void test_ascii() {
                        Encoding penc = new PythonSurrogateEscapeEncoding(Encoding.ASCII);
                        // Bad byte
                        Assert.AreEqual("foo\udc80bar",
                            penc.GetChars("foo\u0080bar".AsBytes()));
                        Assert.AreEqual("foo\u0080bar".AsBytes(),
                            penc.GetBytes("foo\udc80bar"));
                    }

                    /*
                    def test_charmap(self):
                        # bad byte: \xa5 is unmapped in iso-8859-3
                        self.assertEqual(b"foo\xa5bar".decode("iso-8859-3", "surrogateescape"),
                                         "foo\udca5bar")
                        self.assertEqual("foo\udca5bar".encode("iso-8859-3", "surrogateescape"),
                                         b"foo\xa5bar")
                    */
                    /* 
                    [Test]
                    public void test_charmap() {
                        // bad byte: \xa5 is unmapped in iso-8859-3
                        // However, .NET maps this byte to U+F7F5 (private use character)
                        // so no escaping is trigered
                        Encoding penc = new PythonSurrogateEscapeEncoding(Encoding.GetEncoding("iso-8859-3"));
                        Assert.AreEqual("foo\udca5bar",
                            penc.GetChars("foo\u00a5bar".AsBytes()));
                    }
                    */

                    /*
                    def test_latin1(self):
                        # Issue6373
                        self.assertEqual("\udce4\udceb\udcef\udcf6\udcfc".encode("latin-1", "surrogateescape"),
                                         b"\xe4\xeb\xef\xf6\xfc")

                     */
                    [Test]
                     public void test_latin1() {
                        // Issue6373
                        Encoding penc = new PythonSurrogateEscapeEncoding(Encoding.GetEncoding("iso-8859-1"));
                        Assert.AreEqual("\xe4\xeb\xef\xf6\xfc".AsBytes(),
                            penc.GetBytes("\udce4\udceb\udcef\udcf6\udcfc"));
                    }
                }

            }
        }

    }


    #region Helper methods

    public static class Helpers {
        public static byte[] AsBytes(this string s) => s.Select(c => (byte)c).ToArray();
    }

    #endregion
}
