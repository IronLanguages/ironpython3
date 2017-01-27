# What's New In Python 3.2

https://docs.python.org/3/whatsnew/3.2.html

PEPs
====
- [ ] PEP 384: Defining a Stable ABI
- [ ] PEP 389: Argparse Command Line Parsing Module
- [ ] PEP 391: Dictionary Based Configuration for Logging
- [ ] PEP 3148: The concurrent.futures module
- [ ] PEP 3147: PYC Repository Directories
- [ ] PEP 3149: ABI Version Tagged .so Files
- [ ] PEP 3333: Python Web Server Gateway Interface v1.0.1

Other Language Changes
==============
- [ ] String formatting for format() and str.format() gained new capabilities for the format character #. Previously, for integers in binary, octal, or hexadecimal, it caused the output to be prefixed with '0b', '0o', or '0x' respectively. Now it can also handle floats, complex, and Decimal, causing the output to always have a decimal point even when no digits follow it.
- [ ] There is also a new str.format_map() method that extends the capabilities of the existing str.format() method by accepting arbitrary mapping objects. This new method makes it possible to use string formatting with any of Python's many dictionary-like objects such as defaultdict, Shelf, ConfigParser, or dbm. It is also useful with custom dict subclasses that normalize keys before look-up or that supply a \_\_missing\_\_() method for unknown keys.
- [ ] The interpreter can now be started with a quiet option, -q, to prevent the copyright and version information from being displayed in the interactive mode. The option can be introspected using the sys.flags attribute
- [ ] The hasattr() function works by calling getattr() and detecting whether an exception is raised. This technique allows it to detect methods created dynamically by \_\_getattr\_\_() or \_\_getattribute\_\_() which would otherwise be absent from the class dictionary. Formerly, hasattr would catch any exception, possibly masking genuine errors. Now, hasattr has been tightened to only catch AttributeError and let other exceptions pass through.
- [ ] The str() of a float or complex number is now the same as its repr(). Previously, the str() form was shorter but that just caused confusion and is no longer needed now that the shortest possible repr() is displayed by default.
- [ ] memoryview objects now have a release() method and they also now support the context management protocol. This allows timely release of any resources that were acquired when requesting a buffer from the original object.
- [ ] Previously it was illegal to delete a name from the local namespace if it occurs as a free variable in a nested block. This is now allowed.
- [ ] The internal structsequence tool now creates subclasses of tuple. This means that C structures like those returned by os.stat(), time.gmtime(), and sys.version_info now work like a named tuple and now work with functions and methods that expect a tuple as an argument. This is a big step forward in making the C structures as flexible as their pure Python counterparts.
- [ ] Warnings are now easier to control using the PYTHONWARNINGS environment variable as an alternative to using -W at the command line.
- [ ] A new warning category, ResourceWarning, has been added. It is emitted when potential issues with resource consumption or cleanup are detected. It is silenced by default in normal release builds but can be enabled through the means provided by the warnings module, or on the command line. A ResourceWarning is issued at interpreter shutdown if the gc.garbage list isn't empty, and if gc.DEBUG_UNCOLLECTABLE is set, all uncollectable objects are printed. This is meant to make the programmer aware that their code contains object finalization issues. A ResourceWarning is also issued when a file object is destroyed without having been explicitly closed. While the deallocator for such object ensures it closes the underlying operating system resource (usually, a file descriptor), the delay in deallocating the object could produce various issues, especially under Windows. Here is an example of enabling the warning from the command line.
- [ ] range objects now support index and count methods. This is part of an effort to make more objects fully implement the collections.Sequence abstract base class. As a result, the language will have a more uniform API. In addition, range objects now support slicing and negative indices, even with values larger than sys.maxsize. This makes range more interoperable with lists.
- [ ] The callable() builtin function from Py2.x was resurrected. It provides a concise, readable alternative to using an abstract base class in an expression like isinstance(x, collections.Callable).
- [ ] Python's import mechanism can now load modules installed in directories with non-ASCII characters in the path name. This solved an aggravating problem with home directories for users with non-ASCII characters in their usernames.

New, Improved, and Deprecated Modules
==============
- [ ] email
  - [ ] New functions message_from_bytes() and message_from_binary_file(), and new classes BytesFeedParser and BytesParser allow binary message data to be parsed into model objects.
  - [ ] Given bytes input to the model, get_payload() will by default decode a message body that has a Content-Transfer-Encoding of 8bit using the charset specified in the MIME headers and return the resulting string.
  - [ ] Given bytes input to the model, Generator will convert message bodies that have a Content-Transfer-Encoding of 8bit to instead have a 7bit Content-Transfer-Encoding. Headers with unencoded non-ASCII bytes are deemed to be RFC 2047-encoded using the unknown-8bit character set.
  - [ ] A new class BytesGenerator produces bytes as output, preserving any unchanged non-ASCII data that was present in the input used to build the model, including message bodies with a Content-Transfer-Encoding of 8bit.
  - [ ] The smtplib SMTP class now accepts a byte string for the msg argument to the sendmail() method, and a new method, send_message() accepts a Message object and can optionally obtain the from_addr and to_addrs addresses directly from the object.
- [ ] The xml.etree.ElementTree package and its xml.etree.cElementTree counterpart have been updated to version 1.3.
- [ ] functools
  - [ ] The functools module includes a new decorator for caching function calls. functools.lru_cache() can save repeated queries to an external resource whenever the results are expected to be the same.
  - [ ] The functools.wraps() decorator now adds a \_\_wrapped\_\_ attribute pointing to the original callable function. This allows wrapped functions to be introspected. It also copies \_\_annotations\_\_ if defined. And now it also gracefully skips over missing attributes such as \_\_doc\_\_ which might not be defined for the wrapped callable.
  - [ ] To help write classes with rich comparison methods, a new decorator functools.total_ordering() will use existing equality and inequality methods to fill in the remaining methods.
  - [ ] To aid in porting programs from Python 2, the functools.cmp_to_key() function converts an old-style comparison function to modern key function.
- [ ] The itertools module has a new accumulate() function modeled on APL's scan operator and Numpy's accumulate function.
- [ ] collections
  - [ ] The collections.Counter class now has two forms of in-place subtraction, the existing -= operator for saturating subtraction and the new subtract() method for regular subtraction. The former is suitable for multisets which only have positive counts, and the latter is more suitable for use cases that allow negative counts.
  - [ ] The collections.OrderedDict class has a new method move_to_end() which takes an existing key and moves it to either the first or last position in the ordered sequence. The default is to move an item to the last position. This is equivalent of renewing an entry with od[k] = od.pop(k). A fast move-to-end operation is useful for resequencing entries. For example, an ordered dictionary can be used to track order of access by aging entries from the oldest to the most recently accessed.
  - [ ] The collections.deque class grew two new methods count() and reverse() that make them more substitutable for list objects.
- [ ] threading
- [ ] datetime and time
  - [ ] The datetime module has a new type timezone that implements the tzinfo interface by returning a fixed UTC offset and timezone name. This makes it easier to create timezone-aware datetime objects.
  - [ ] Also, timedelta objects can now be multiplied by float and divided by float and int objects. And timedelta objects can now divide one another.
  - [ ] The datetime.date.strftime() method is no longer restricted to years after 1900. The new supported year range is from 1000 to 9999 inclusive.
  - [ ] Whenever a two-digit year is used in a time tuple, the interpretation has been governed by time.accept2dyear. The default is True which means that for a two-digit year, the century is guessed according to the POSIX rules governing the %y strptime format. Starting with Py3.2, use of the century guessing heuristic will emit a DeprecationWarning. Instead, it is recommended that time.accept2dyear be set to False so that large date ranges can be used without guesswork. Several functions now have significantly expanded date ranges. When time.accept2dyear is false, the time.asctime() function will accept any year that fits in a C int, while the time.mktime() and time.strftime() functions will accept the full range supported by the corresponding operating system functions.
- [ ] math
  - [ ] The isfinite() function provides a reliable and fast way to detect special values. It returns True for regular numbers and False for Nan or Infinity:
  - [ ] The expm1() function computes e**x-1 for small values of x without incurring the loss of precision that usually accompanies the subtraction of nearly equal quantities:
  - [ ] The erf() function computes a probability integral or Gaussian error function. The complementary error function, erfc(), is 1 - erf(x):
  - [ ] The gamma() function is a continuous extension of the factorial function. See https://en.wikipedia.org/wiki/Gamma_function for details. Because the function is related to factorials, it grows large even for small values of x, so there is also a lgamma() function for computing the natural logarithm of the gamma function:
- [ ] abc
- [ ] io
- [ ] reprlib
- [ ] logging
- [ ] csv
- [ ] contextlib
- [ ] decimal and fractions
- [ ] ftp
- [ ] popen
- [ ] select
- [ ] gzip and zipfile
- [ ] tarfile
- [ ] hashlib
- [ ] ast
- [ ] os
- [ ] shutil
- [ ] sqlite3
- [ ] html
- [ ] socket
- [ ] ssl
- [ ] nntp
- [ ] certificates
- [ ] imaplib
- [ ] http.client
- [ ] unittest
- [ ] random
- [ ] poplib
- [ ] asyncore
- [ ] tempfile
- [ ] inspect
- [ ] pydoc
- [ ] dis
- [ ] dbm
- [ ] ctypes
  - [ ] A new type, ctypes.c_ssize_t represents the C ssize_t datatype.
- [ ] site
- [ ] sysconfig
- [ ] pdb
- [ ] configparser
- [ ] urllib.parse
- [ ] mailbox
- [ ] turtledemo

Porting to Python 3.2
============
- [ ] The configparser module has a number of clean-ups.
- [ ] The nntplib module was reworked extensively, meaning that its APIs are often incompatible with the 3.1 APIs.
- [ ] bytearray objects can no longer be used as filenames; instead, they should be converted to bytes.
- [ ] The array.tostring() and array.fromstring() have been renamed to array.tobytes() and array.frombytes() for clarity. The old names have been deprecated. (See issue 8990.)
- [ ] The sys.setfilesystemencoding() function was removed because it had a flawed design.
- [ ] he random.seed() function and method now salt string seeds with an sha512 hash function. To access the previous version of seed in order to reproduce Python 3.1 sequences, set the version argument to 1, random.seed(s, version=1).
- [ ] The previously deprecated string.maketrans() function has been removed in favor of the static methods bytes.maketrans() and bytearray.maketrans(). This change solves the confusion around which types were supported by the string module. Now, str, bytes, and bytearray each have their own maketrans and translate methods with intermediate translation tables of the appropriate type.
- [ ] The previously deprecated contextlib.nested() function has been removed in favor of a plain with statement which can accept multiple context managers. The latter technique is faster (because it is built-in), and it does a better job finalizing multiple context managers when one of them raises an exception:
- [ ] struct.pack() now only allows bytes for the s string pack code. Formerly, it would accept text arguments and implicitly encode them to bytes using UTF-8. This was problematic because it made assumptions about the correct encoding and because a variable-length encoding can fail when writing to fixed length segment of a structure. Code such as struct.pack('<6sHHBBB', 'GIF87a', x, y) should be rewritten with to use bytes instead of text, struct.pack('<6sHHBBB', b'GIF87a', x, y).
- [ ] The xml.etree.ElementTree class now raises an xml.etree.ElementTree.ParseError when a parse fails. Previously it raised an xml.parsers.expat.ExpatError.
- [ ] The new, longer str() value on floats may break doctests which rely on the old output format.
- [ ] In subprocess.Popen, the default value for close_fds is now True under Unix; under Windows, it is True if the three standard streams are set to None, False otherwise. Previously, close_fds was always False by default, which produced difficult to solve bugs or race conditions when open file descriptors would leak into the child process.
- [ ] Support for legacy HTTP 0.9 has been removed from urllib.request and http.client. Such support is still present on the server side (in http.server).
- [ ] SSL sockets in timeout mode now raise socket.timeout when a timeout occurs, rather than a generic SSLError.
- [ ] Due to security risks, asyncore.handle_accept() has been deprecated, and a new function, asyncore.handle_accepted(), was added to replace it.
