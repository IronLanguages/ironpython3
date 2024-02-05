# What's New In Python 3.6

https://docs.python.org/3/whatsnew/3.6.html

New Features
============
- [x] [PEP 498][]: Formatted string literals
- [ ] [PEP 526][]: Syntax for variable annotations
- [ ] [PEP 515][]: Underscores in Numeric Literals
- [ ] [PEP 525][]: Asynchronous Generators
- [ ] [PEP 530][]: Asynchronous Comprehensions
- [ ] [PEP 487][]: Simpler customization of class creation
- [ ] [PEP 487][]: Descriptor Protocol Enhancements
- [ ] [PEP 519][]: Adding a file system path protocol
- [ ] [PEP 495][]: Local Time Disambiguation
- [ ] [PEP 529][]: Change Windows filesystem encoding to UTF-8
- [ ] [PEP 528][]: Change Windows console encoding to UTF-8
- [ ] [PEP 520][]: Preserving Class Attribute Definition Order
- [ ] [PEP 468][]: Preserving Keyword Argument Order
- [ ] [PEP 523][]: Adding a frame evaluation API to CPython

Other Language Changes
======================
- [ ] A `global` or `nonlocal` statement must now textually appear before the first use of the affected name in the same scope. Previously this was a `SyntaxWarning`.
- [ ] It is now possible to set a special method to `None` to indicate that the corresponding operation is not available. For example, if a class sets `__iter__()` to `None`, the class is not iterable.
- [ ] Long sequences of repeated traceback lines are now abbreviated as `"[Previous line repeated {count} more times]"` (see traceback for an example).
- [ ] Import now raises the new exception `ModuleNotFoundError` (subclass of `ImportError`) when it cannot find a module. Code that currently checks for `ImportError` (in try-except) will still work.
- [ ] Class methods relying on zero-argument `super()` will now work correctly when called from metaclass methods during class creation.

New Modules
===========
- [ ] `secrets`

Improved Modules
================
- [ ] `array`
- [ ] `ast`
- [ ] `asyncio`
- [ ] `binascii`
- [ ] `cmath`
- [ ] `collections`
- [ ] `concurrent.futures`
- [ ] `contextlib`
- [ ] `datetime`
- [ ] `decimal`
- [ ] `distutils`
- [ ] `email`
- [ ] `encodings`
- [ ] `enum`
- [ ] `faulthandler`
- [ ] `fileinput`
- [ ] `hashlib`
- [ ] `http.client`
- [ ] `idlelib and IDLE`
- [ ] `importlib`
- [ ] `inspect`
- [ ] `json`
- [ ] `logging`
- [ ] `math`
- [ ] `multiprocessing`
- [ ] `operator`
- [ ] `os`
- [ ] `pathlib`
- [ ] `pdb`
- [ ] `pickle`
- [ ] `pickletools`
- [ ] `pydoc`
- [ ] `random`
- [ ] `re`
- [ ] `readline`
- [ ] `rlcompleter`
- [ ] `shlex`
- [ ] `site`
- [ ] `sqlite3`
- [ ] `socket`
- [ ] `socketserver`
- [ ] `ssl`
- [ ] `statistics`
- [ ] `struct`
- [ ] `subprocess`
- [ ] `sys`
- [ ] `telnetlib`
- [ ] `time`
- [ ] `timeit`
- [ ] `tkinter`
- [ ] `traceback`
- [ ] `tracemalloc`
- [ ] `typing`
- [ ] `unicodedata`
- [ ] `unittest.mock`
- [ ] `urllib.requests`
- [ ] `urllib.robotparser`
- [ ] `venv`
- [ ] `warnings`
- [ ] `winreg`
- [ ] `winsound`
- [ ] `xmlrpc.client`
- [ ] `zipfile`
- [ ] `zlib`

Other Improvements
==================
- [x] When `--version` (short form: `-V`) is supplied twice, Python prints `sys.version` for detailed information.

Deprecated
==========

New Keywords
------------
- [ ] `async` and `await` are not recommended to be used as variable, class, function or module names. Introduced by PEP 492 in Python 3.5, they will become proper keywords in Python 3.7. Starting in Python 3.6, the use of `async` or `await` as names will generate a `DeprecationWarning`.

Deprecated Python Behavior
--------------------------
- [ ] Raising the `StopIteration` exception inside a generator will now generate a `DeprecationWarning` and will trigger a `RuntimeError` in Python 3.7. See PEP 479: Change StopIteration handling inside generators for details.
- [ ] The `__aiter__()` method is now expected to return an asynchronous iterator directly instead of returning an awaitable as previously. Doing the former will trigger a `DeprecationWarning`. Backward compatibility will be removed in Python 3.7.
- [ ] A backslash-character pair that is not a valid escape sequence now generates a `DeprecationWarning`. Although this will eventually become a `SyntaxError`, that will not be for several Python releases.
- [ ] When performing a relative import, falling back on `__name__` and `__path__` from the calling module when `__spec__` or `__package__` are not defined now raises an `ImportWarning`.

Deprecated Python modules, functions and methods
------------------------------------------------
- [ ] The `asynchat` has been deprecated in favor of `asyncio`.
- [ ] The `asyncore` has been deprecated in favor of `asyncio`.
- [ ] Unlike other `dbm` implementations, the `dbm.dumb` module creates databases with the `'rw'` mode and allows modifying the database opened with the `'r'` mode. This behavior is now deprecated and will be removed in 3.8.
- [ ] The undocumented `extra_path` argument to the `Distribution` constructor is now considered deprecated and will raise a warning if set. Support for this parameter will be removed in a future Python release. See bpo-27919 for details.
- [ ] The support of non-integer arguments in `getgrgid()` has been deprecated.
- [ ] The `importlib.machinery.SourceFileLoader.load_module()` and `importlib.machinery.SourcelessFileLoader.load_module()` methods are now deprecated. They were the only remaining implementations of `importlib.abc.Loader.load_module()` in `importlib` that had not been deprecated in previous versions of Python in favour of `importlib.abc.Loader.exec_module()`.
- [ ] The `importlib.machinery.WindowsRegistryFinder` class is now deprecated. As of 3.6.0, it is still added to `sys.meta_path` by default (on Windows), but this may change in future releases.
- [ ] Undocumented support of general bytes-like objects as paths in `os` functions, `compile()` and similar functions is now deprecated.
- [ ] Support for inline flags `(?letters)` in the middle of the regular expression has been deprecated and will be removed in a future Python version. Flags at the start of a regular expression are still allowed.
- [ ] OpenSSL 0.9.8, 1.0.0 and 1.0.1 are deprecated and no longer supported. In the future the `ssl` module will require at least OpenSSL 1.0.2 or 1.1.0..
- [ ] SSL-related arguments like `certfile`, `keyfile` and `check_hostname` in `ftplib`, `http.client`, `imaplib`, `poplib`, and `smtplib` have been deprecated in favor of `context`.
- [ ] A couple of protocols and functions of the `ssl` module are now deprecated. Some features will no longer be available in future versions of OpenSSL. Other features are deprecated in favor of a different API.
- [ ] The `tkinter.tix` module is now deprecated. `tkinter` users should use `tkinter.ttk` instead.
- [ ] The `pyvenv` script has been deprecated in favour of `python3 -m venv`. This prevents confusion as to what Python interpreter `pyvenv` is connected to and thus what Python interpreter will be used by the virtual environment.

Removed
=======
- [ ] Unknown escapes consisting of `'\'` and an ASCII letter in regular expressions will now cause an error. In replacement templates for `re.sub()` they are still allowed, but deprecated. The `re.LOCALE` flag can now only be used with binary patterns.
- [ ] `inspect.getmoduleinfo()` was removed (was deprecated since CPython 3.3). `inspect.getmodulename()` should be used for obtaining the module name for a given path.
- [ ] `traceback.Ignore` class and `traceback.usage`, `traceback.modname`, `traceback.fullmodname`, `traceback.find_lines_from_code`, `traceback.find_lines`, `traceback.find_strings`, `traceback.find_executable_lines` methods were removed from the `traceback` module. They were undocumented methods deprecated since Python 3.2 and equivalent functionality is available from private methods.
- [ ] The `tk_menuBar()` and `tk_bindForTraversal()` dummy methods in `tkinter` widget classes were removed (corresponding Tk commands were obsolete since Tk 4.0).
- [ ] The `open()` method of the `zipfile.ZipFile` class no longer supports the `'U'` mode (was deprecated since Python 3.4). Use `io.TextIOWrapper` for reading compressed text files in universal newlines mode.
- [ ] The undocumented `IN`, `CDROM`, `DLFCN`, `TYPES`, `CDIO`, and `STROPTS` modules have been removed. They had been available in the platform specific `Lib/plat-*/` directories, but were chronically out of date, inconsistently available across platforms, and unmaintained. The script that created these modules is still available in the source distribution at Tools/scripts/h2py.py.
- [ ] The deprecated `asynchat.fifo` class has been removed.

[PEP 498]: https://www.python.org/dev/peps/pep-0498
[PEP 526]: https://www.python.org/dev/peps/pep-0526
[PEP 515]: https://www.python.org/dev/peps/pep-0515
[PEP 525]: https://www.python.org/dev/peps/pep-0525
[PEP 530]: https://www.python.org/dev/peps/pep-0530
[PEP 487]: https://www.python.org/dev/peps/pep-0487
[PEP 487]: https://www.python.org/dev/peps/pep-0487
[PEP 519]: https://www.python.org/dev/peps/pep-0519
[PEP 495]: https://www.python.org/dev/peps/pep-0495
[PEP 529]: https://www.python.org/dev/peps/pep-0529
[PEP 528]: https://www.python.org/dev/peps/pep-0528
[PEP 520]: https://www.python.org/dev/peps/pep-0520
[PEP 468]: https://www.python.org/dev/peps/pep-0468
[PEP 523]: https://www.python.org/dev/peps/pep-0523
