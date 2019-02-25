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

            private byte[] bytes;

            [SetUp]
            public void SetUp() {
                bytes = Enumerable.Range(0, 256).Select(c => (byte)c).ToArray();
            }

            [Test] public void Test256WithAscii() => TestRoundTrip(Encoding.ASCII, bytes);
            [Test] public void Test256WithUtf8() => TestRoundTrip(Encoding.UTF8, bytes);
            [Test] public void Test256WithDefault() => TestRoundTrip(Encoding.Default, bytes);
            [Test] public void Test256WithUnicode() => TestRoundTrip(Encoding.Unicode, bytes);
            [Test] public void Test256WithBigEndianUnicode() => TestRoundTrip(Encoding.BigEndianUnicode, bytes);
            [Test] public void Test256WithUtf32() => TestRoundTrip(Encoding.UTF32, bytes);
            [Test] public void Test256WithUtf32BE() => TestRoundTrip(new UTF32Encoding(bigEndian: true, byteOrderMark: false), bytes);
        }

        // Test decoding/encoding a valid UTF-8 sequence
        public class Utf8Test {

            private byte[] bytes;

            [SetUp]
            public void SetUp() {
                // 12 bytes, rounded to multiply of 4 for the sake of UTF-32 test
                bytes = "\xd0\x9f\xd0\xb8\xd1\x82\xd0\xbe\xd0\xbd!!".Select(c => (byte)c).ToArray();
            }

            [Test] public void TestValidUtf8WithAscii() => TestRoundTrip(Encoding.ASCII, bytes);
            [Test] public void TestValidUtf8WithUtf8() => TestRoundTrip(Encoding.UTF8, bytes);
            [Test] public void TestValidUtf8WithDefault() => TestRoundTrip(Encoding.Default, bytes);
            [Test] public void TestValidUtf8WithUnicode() => TestRoundTrip(Encoding.Unicode, bytes);
            [Test] public void TestValidUtf8WithBigEndianUnicode() => TestRoundTrip(Encoding.BigEndianUnicode, bytes);
            [Test] public void TestValidUtf8WithUtf32() => TestRoundTrip(Encoding.UTF32, bytes);
            [Test] public void TestValidUtf8WithUtf32BE() => TestRoundTrip(new UTF32Encoding(bigEndian: true, byteOrderMark: false), bytes);
        }

        // Test decoding/encoding an invalid UTF-8 sequence
        public class Utf8BrokenTest {

            private byte[] bytes;

            [SetUp]
            public void SetUp() {
                // 12 bytes: two valid UTF-8 2-byte chars, one non-decodable byte, 
                // one UTF-8 2-byte char with a non-decodable byte inserted in between the UTF-8 bytes
                // and final valid UTF-8 2-byte char
                bytes = "\xd0\x9f\xd0\xb8\x80\xd1\x20\x82\xd0\xbe\xd0\xbd".Select(c => (byte)c).ToArray();
            }

            [Test] public void TestBrokenUtf8WithAscii() => TestRoundTrip(Encoding.ASCII, bytes);
            [Test] public void TestBrokenUtf8WithUtf8() => TestRoundTrip(Encoding.UTF8, bytes);
            [Test] public void TestBrokenUtf8WithDefault() => TestRoundTrip(Encoding.Default, bytes);
            [Test] public void TestBrokenUtf8WithUnicode() => TestRoundTrip(Encoding.Unicode, bytes);
            [Test] public void TestBrokenUtf8WithBigEndianUnicode() => TestRoundTrip(Encoding.BigEndianUnicode, bytes);
            [Test] public void TestBrokenUtf8WithUtf32() => TestRoundTrip(Encoding.UTF32, bytes);
            [Test] public void TestBrokenUtf8WithUtf32BE() => TestRoundTrip(new UTF32Encoding(bigEndian: true, byteOrderMark: false), bytes);
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
        public class CPythonCompareTests {

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
        }

        #endregion
    }
}
