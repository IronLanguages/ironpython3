# What's New In Python 3.5

https://docs.python.org/3/whatsnew/3.5.html

New Features
============
- [ ] [PEP 492][]: Coroutines with async and await syntax
- [x] [PEP 465][]: A dedicated infix operator for matrix multiplication
- [x] [PEP 448][]: Additional Unpacking Generalizations
- [ ] [PEP 461][]: percent formatting support for bytes and bytearray
- [ ] [PEP 484][]: Type Hints
- [ ] [PEP 471][]: os.scandir() function – a better and faster directory iterator
- [ ] [PEP 475][]: Retry system calls failing with EINTR
- [x] [PEP 479][]: Change StopIteration handling inside generators
- [ ] [PEP 485][]: A function for testing approximate equality
- [ ] [PEP 486][]: Make the Python Launcher aware of virtual environments
- [ ] [PEP 488][]: Elimination of PYO files
- [ ] [PEP 489][]: Multi-phase extension module initialization

Other Language Changes
======================
- [ ] Added the `"namereplace"` error handlers. The `"backslashreplace"` error handlers now work with decoding and translating.
- [ ] The `-b` option now affects comparisons of `bytes` with `int`.
- [ ] New Kazakh `kz1048` and Tajik `koi8_t` codecs.
- [ ] Property docstrings are now writable. This is especially useful for `collections.namedtuple()` docstrings.
- [ ] Circular imports involving relative imports are now supported.

New Modules
===========
- [ ] `typing`
- [ ] `zipapp`

Improved Modules
================
- [ ] `argparse`
- [ ] `asyncio`
- [ ] `bz2`
- [ ] `cgi`
- [ ] `cmath`
- [ ] `code`
- [ ] `collections`
- [ ] `collections.abc`
- [ ] `compileall`
- [ ] `concurrent.futures`
- [ ] `configparser`
- [ ] `contextlib`
- [ ] `csv`
- [ ] `curses`
- [ ] `dbm`
- [ ] `difflib`
- [ ] `distutils`
- [ ] `doctest`
- [ ] `email`
- [ ] `enum`
- [ ] `faulthandler`
- [ ] `functools`
- [ ] `glob`
- [ ] `gzip`
- [ ] `heapq`
- [ ] `http`
- [ ] `http.client`
- [ ] `idlelib and IDLE`
- [ ] `imaplib`
- [ ] `imghdr`
- [ ] `importlib`
- [ ] `inspect`
- [ ] `io`
- [ ] `ipaddress`
- [ ] `json`
- [ ] `linecache`
- [ ] `locale`
- [ ] `logging`
- [ ] `lzma`
- [ ] `math`
- [ ] `multiprocessing`
- [ ] `operator`
- [ ] `os`
- [ ] `pathlib`
- [ ] `pickle`
- [ ] `poplib`
- [ ] `re`
- [ ] `readline`
- [ ] `selectors`
- [ ] `shutil`
- [ ] `signal`
- [ ] `smtpd`
- [ ] `smtplib`
- [ ] `sndhdr`
- [ ] `socket`
- [ ] `ssl`
- [ ] `sqlite3`
- [ ] `subprocess`
- [ ] `sys`
- [ ] `sysconfig`
- [ ] `tarfile`
- [ ] `threading`
- [ ] `time`
- [ ] `timeit`
- [ ] `tkinter`
- [ ] `traceback`
- [ ] `types`
- [ ] `unicodedata`
- [ ] `unittest`
- [ ] `unittest.mock`
- [ ] `urllib`
- [ ] `wsgiref`
- [ ] `xmlrpc`
- [ ] `xml.sax`
- [ ] `zipfile`

Other module-level changes
==========================
- [ ] Many functions in the `mmap`, `ossaudiodev`, `socket`, `ssl`, and `codecs` modules now accept writable bytes-like objects.

Deprecated
==========
- [ ] New Keywords: `async` and `await` are not recommended to be used as variable, class, function or module names. Introduced by PEP 492 in Python 3.5, they will become proper keywords in Python 3.7.
- [ ] Deprecated Python Behavior: Raising the `StopIteration` exception inside a generator will now generate a silent `PendingDeprecationWarning`, which will become a non-silent deprecation warning in Python 3.6 and will trigger a `RuntimeError` in Python 3.7. See PEP 479: Change StopIteration handling inside generators for details.

Deprecated Python modules, functions and methods
================================================
- [ ] The `formatter` module has now graduated to full deprecation and is still slated for removal in Python 3.6.
- [ ] The `asyncio.async()` function is deprecated in favor of `ensure_future()`.
- [ ] The `smtpd` module has in the past always decoded the DATA portion of email messages using the `utf-8` codec. This can now be controlled by the new _decode__data_ keyword to `SMTPServer`. The default value is `True`, but this default is deprecated. Specify the _decode__data_ keyword with an appropriate value to avoid the deprecation warning.
- [ ] Directly assigning values to the `key`, `value` and `coded_value` of `http.cookies.Morsel` objects is deprecated. Use the `set()` method instead. In addition, the undocumented LegalChars parameter of `set()` is deprecated, and is now ignored.
- [ ] Passing a format string as keyword argument _format__string_ to the `format()` method of the `string.Formatter` class has been deprecated.
- [ ] The `platform.dist()` and `platform.linux_distribution()` functions are now deprecated. Linux distributions use too many different ways of describing themselves, so the functionality is left to a package. 
- [ ] The previously undocumented `from_function` and `from_builtin methods` of inspect.Signature are deprecated. Use the new `Signature.from_callable()` method instead.
- [ ] The `inspect.getargspec()` function is deprecated and scheduled to be removed in Python 3.6.
- [ ] The inspect `getfullargspec()`, `getcallargs()`, and `formatargspec()` functions are deprecated in favor of the `inspect.signature()` API.
- [ ] `getargvalues()` and `formatargvalues()` functions were inadvertently marked as deprecated with the release of Python 3.5.0.
- [ ] Use of `re.LOCALE` flag with str patterns or `re.ASCII` is now deprecated.
- [ ] Use of unrecognized special sequences consisting of `'\'` and an ASCII letter in regular expression patterns and replacement patterns now raises a deprecation warning and will be forbidden in Python 3.6.
- [ ] The undocumented and unofficial _use__load__tests_ default argument of the `unittest.TestLoader.loadTestsFromModule()` method now is deprecated and ignored.

Removed
=======
- [ ] The `__version__` attribute has been dropped from the email package. The email code hasn’t been shipped separately from the stdlib for a long time, and the `__version__` string was not updated in the last few releases.
- [ ] The internal `Netrc` class in the ftplib module was deprecated in 3.4, and has now been removed.
- [ ] The concept of .pyo files has been removed.
- [ ] The `JoinableQueue` class in the provisional `asyncio` module was deprecated in 3.4.4 and is now removed. 

[PEP 492]: https://www.python.org/dev/peps/pep-0492
[PEP 465]: https://www.python.org/dev/peps/pep-0465
[PEP 448]: https://www.python.org/dev/peps/pep-0448
[PEP 461]: https://www.python.org/dev/peps/pep-0461
[PEP 484]: https://www.python.org/dev/peps/pep-0484
[PEP 471]: https://www.python.org/dev/peps/pep-0471
[PEP 475]: https://www.python.org/dev/peps/pep-0475
[PEP 479]: https://www.python.org/dev/peps/pep-0479
[PEP 485]: https://www.python.org/dev/peps/pep-0485
[PEP 486]: https://www.python.org/dev/peps/pep-0486
[PEP 488]: https://www.python.org/dev/peps/pep-0488
[PEP 489]: https://www.python.org/dev/peps/pep-0489
