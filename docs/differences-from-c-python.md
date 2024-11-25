This page documents various differences between IronPython and CPython. Since IronPython is under active development, any of the differences described here may change or disappear in the future:

- [Environment Variables](#environment-variables)
- [COM Interaction](#com-interaction)
- [Strings](#strings)
- [Interaction with the Operating System](#interaction-with-the-operating-system)
- [Codecs](#codecs)
- [Source File Encoding](#source-file-encoding)
- [Recursion](#recursion)

# Environment Variables

* `IRONPYTHONSTARTUP` is used instead of `PYTHONSTARTUP`

* `IRONPYTHONPATH` is used instead of `PYTHONPATH`

# COM Interaction

* Interaction with COM objects is handled by the CLR rather than a python library binding to the native COM dlls.

# Strings

* `str` objects are represented in UTF-16 (like all .NET strings) rather than UTF-32 used by CPython.

This has a few visible consequences if characters ouside of the Basic Multilingual Plane (BMP) are used (that is, characters with Unicode code points above `U+FFFF`). A few examples below illustrate the differences.

Let's take a Unicode character U+1F70B, '🜋'. In CPython, it is represented by a single character:

_CPython_
```
>>> len('\U0001f70b')
1
>>> str('\U0001f70b')
'🜋'
```

In IronPython, it is represented by a pair of surrogate characters U+D83D and U+DF0B:

_IronPython_
```
>>> len('\U0001f70b')
2
>>> str('\U0001f70b')
'\ud83d\udf0b'
```

In **both** cases, however, the string containing such character is printed out correctly, since `print` will transcode the string from its internal representation to whichever encoding is used by the console or file (usually UTF-8):

_CPython_ and _IronPython_
```
print('\U0001f70b')
'🜋'
```

Any surrogate pair in IronPython strings represents one logical character. CPython, however, sees a surrogate pair as two invalid characters. 

_IronPython_
```
>>> '\ud83d\udf0b'
'\ud83d\udf0b'
>>> print('\ud83d\udf0b')  
🜋
>>> '\ud83d\udf0b'.encode('utf-8')       
b'\xf0\x9f\x9c\x8b'
>>> '\U0001f70b'.encode('utf-8')
b'\xf0\x9f\x9c\x8b'
```

_CPython_
```
>>> '\ud83d\udf0b'
'\ud83d\udf0b'
>>> print('\ud83d\udf0b')
Traceback (most recent call last):
  File "<stdin>", line 1, in <module>
UnicodeEncodeError: 'utf-8' codec can't encode characters in position 0-1: surrogates not allowed
'\ud83d\udf0b'.encode('utf-8')
Traceback (most recent call last):
  File "<stdin>", line 1, in <module>
UnicodeEncodeError: 'utf-8' codec can't encode characters in position 0-1: surrogates not allowed
```

CPython requires use of `'surrogatepass'` error handler to let those pairs through. Note however, that they are still being treated as two separate characters. IronPython encodes the pair as if it were one character.

_CPython_
```
>>> '\ud83d\udf0b'.encode('utf-8','surrogatepass')
b'\xed\xa0\xbd\xed\xbc\x8b'
>>> '\U0001f70b'.encode('utf-8')
b'\xf0\x9f\x9c\x8b'
```

The `'surrogatepass'` error handler is still needed in IronPython to handle surrogate characters that do not form a valid surrogate pair:

_IronPython_
```
print('\ud83d\udf0b')          
🜋
>>> print('\ud83d\udf0b'[::-1])
Traceback (most recent call last):
  File "<stdin>", line 1, in <module>
UnicodeEncodeError: 'cp65001' codec can't encode character '\udf0b' in position 0: Unable to translate Unicode character \\uDF0B at index 0 to specified code page.
>>> print('\ud83d\udf0b'[::-1].encode('utf-8','surrogatepass'))
b'\xed\xbc\x8b\xed\xa0\xbd'
```

_CPython_
```
>>> print('\ud83d\udf0b')
Traceback (most recent call last):
  File "<stdin>", line 1, in <module>
UnicodeEncodeError: 'utf-8' codec can't encode characters in position 0-1: surrogates not allowed
>>> print('\ud83d\udf0b'[::-1])
Traceback (most recent call last):
  File "<stdin>", line 1, in <module>
UnicodeEncodeError: 'utf-8' codec can't encode characters in position 0-1: surrogates not allowed
>>> print('\ud83d\udf0b'[::-1].encode('utf-8','surrogatepass'))
b'\xed\xbc\x8b\xed\xa0\xbd'
```

# Interaction with the Operating System

* Environment variables are decoded using the `'replace'` error handler, rather than the `'surrogateescape'` error handler used by CPython.

This is how .NET libraries handle encoding errors in the system. The difference is only visible on Posix systems that have environment variables defined using a different encoding than the encoding used by the system (Windows environment variables are always in UTF-16, so no conversion takes place when accessed as Python `str` objects).

Assume that a Linux system is configured to use UTF-8. Under bash:

```
$ python -c 'f=open("test.sh","w",encoding="latin-1");print("NAME=\"André\"",file=f)'
$ source test.sh
$ export NAME
```

This creates an environment variable that is encoded using Latin-1 encoding, rather than the system encoding. CPython will escape the invalid byte 0xe9 (letter 'é' in Latin-1) in a lone surrogate 0xdce9, which is still an invalid Unicode character.

_CPython_
```
>>> import os
>>> os.environ["NAME"]
'Andr\udce9'
>>> print(os.environ["NAME"])
Traceback (most recent call last):
  File "<stdin>", line 1, in <module>
UnicodeEncodeError: 'utf-8' codec can't encode character '\udce9' in position 4: surrogates not allowed
```

IronPython will replace the invalid byte with U+FFFD, the Unicode replacement character, which is a valid and printable character.

_IronPython_
```
>>> import os
>>> os.environ["NAME"]
'Andr�'
>>> print(os.environ["NAME"])
Andr�
>>> hex(ord(os.environ["NAME"][-1]))
'0xfffd'
```

The CPython representation is not printable, but can be safely encoded back to the original form using `'surrogateescape'` (default when dealing with the OS environment):

_CPython_
```
>>> os.environ["PATH"] = os.environ["PATH"] + ":/home/" + os.environ["NAME"] + "/bin"
>>> import posix
>>> posix.environ[b"PATH"]
b'/bin:/usr/bin:/usr/local/bin:/home/Andr\xe9/bin'
>>> os.environ["NAME"].encode("utf-8","surrogateescape")
b'Andr\xe9'
```

The IronPython representation is printable, but the original byte value is lost:

_IronPython_
```
>>> os.environ["NAME"].encode("utf-8","surrogateescape")
b'Andr\xef\xbf\xbd'
```

# Codecs

* Some single-byte codecs may have unused positions in their codepage. There are differences between how CPython and IronPython (and .NET) handle such cases.

A simple example is encoding Windows-1252. According to the information on Microsoft's and the Unicode Consortium's websites, positions 81, 8D, 8F, 90, and 9D are unused; however, the Windows API `MultiByteToWideChar` maps these to the corresponding C1 control codes. The Unicode "best fit" mapping [documents this behavior](https://www.unicode.org/Public/MAPPINGS/VENDORS/MICSFT/WindowsBestFit/bestfit1252.txt). CPython will treat those bytes as invalid, while IronPython will map them to the "best fit" Unicode character:

_CPython_
```
>>> b'\x81'.decode('windows-1252')
Traceback (most recent call last):
  File "<stdin>", line 1, in <module>
  File "/opt/anaconda3/envs/py34/lib/python3.4/encodings/cp1252.py", line 15, in decode
    return codecs.charmap_decode(input,errors,decoding_table)
UnicodeDecodeError: 'charmap' codec can't decode byte 0x81 in position 0: character maps to <undefined>
>>> b'\x81'.decode('windows-1252','surrogateescape')
'\udc81'
```

_IronPython_
```
>>> b'\x81'.decode('windows-1252')
'\x81'
>>> b'\x81'.decode('windows-1252','surrogateescape')
'\x81'
```

The same difference in behavior can be observed during encoding:

_CPython_
```
>>> '\x81'.encode('windows-1252')
Traceback (most recent call last):
  File "<stdin>", line 1, in <module>
  File "/opt/anaconda3/envs/py34/lib/python3.4/encodings/cp1252.py", line 12, in encode
    return codecs.charmap_encode(input,errors,encoding_table)
UnicodeEncodeError: 'charmap' codec can't encode character '\x81' in position 0: character maps to <undefined>
```

_IronPython_
```
>>> '\x81'.encode('windows-1252')
b'\x81'
```

* When using the UTF-7 encoding, IronPython (and .NET) always terminates the modified Base64 encoded blocks with a '-' while CPython omits the '-' if allowed.

The UTF-7 standard allows encoders for some freedom of implementation. One optionality allowed in UTF-7 is how to end a sequence encoded in the modified Base64 code. In principle, `+` marks the start of the sequence, and `-` is the terminator. However, it is allowed to omit the terminating `-` if the next character unambiguously does not belong to the encoded Base64 block. CPython chooses to drop the terminating `-` in such cases, while IronPython will always terminate Base64-encoded blocks with a `-`:

_CPython_
```
>>> 'abc:~~:zyz'.encode('utf-7')
b'abc:+AH4Afg:zyz'
```

_IronPython_
```
>>> 'abc:~~:zyz'.encode('utf-7')
b'abc:+AH4Afg-:zyz'
```

Note that both forms are fully interchangeable; IronPython will correctly decode what CPython encoded and vice versa.

# Source File Encoding

* Widechar Unicode encodings are supported as source file encoding, in addition to standard Python encodings.

The default source file encoding is UTF-8. This also applies to bytestrings used within the program (processed by `compile`, `eval`, or `exec`). The source file encoding can be explicitly specified, and possibly changed, in one of the two ways:

  1. By declaring the encoding in a Python comment in one of the first two lines — in accordance with [PEP-263](https://www.python.org/dev/peps/pep-0263/).
  2. By a byte-order-mark (BOM) — only for Unicode encodings.

CPython recognizes only UTF-8 BOM. IronPython recognizes BOM in UTF-8, UTF-16LE, UTF-16BE, UTF-32LE, and UTF-32BE.

If both BOM and PEP-263 methods are used simultaneously in the same file, they should be specifying the same encoding. If the PEP-263 encoding does not match the BOM, then:

  * In case of UTF-8 BOM, an error will be reported (by both CPython and IronPython).
  * In case of other BOMs, the encoding specified in the PEP-263 comment is silently ignored.

# Recursion

By default, instead of raising a `RecursionError` when the maximum recursion depth is reached, IronPython will terminate with a `StackOverflowException`. You can enable the recursion limit in IronPython in a number of ways:

  1. From the command line: `ipy -X MaxRecursion=100`.
  2. In hosted scenarios: `Python.CreateEngine(new Dictionary<string, object>() { { "RecursionLimit", 100 } });`.
  3. From Python: `sys.setrecursionlimit(100)`.

*There is a significant performance cost when the recursion limit is enabled*.

Note that IronPython 3.4 adopts the CPython 3.5 behavior and throws a `RecursionError` instead of a `RuntimeError`.
