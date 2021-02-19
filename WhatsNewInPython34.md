# What's New In Python 3.4

https://docs.python.org/3/whatsnew/3.4.html

PEPs
====
- [ ] [PEP 453][]: Explicit Bootstrapping of PIP in Python Installations
- [ ] [PEP 446][]: Newly created file descriptors are non-inheritable
- [ ] [PEP 451][]:  A `ModuleSpec` Type for the Import System

Improvements to Codec Handling
==============================
- [ ] See https://docs.python.org/3/whatsnew/3.4.html#improvements-to-codec-handling

Other Language Changes
======================
- [ ] Unicode database updated to UCD version 6.3.
- [x] `min()` and `max()` now accept a default keyword-only argument that can be used to specify the value they return if the iterable they are evaluating has no elements.
- [ ] Module objects are now `weakref`’able.
- [ ] Module `__file__` attributes (and related values) should now always contain absolute paths by default, with the sole exception of `__main__.__file__` when a script has been executed directly using a relative path.
- [ ] All the UTF-* codecs (except UTF-7) now reject surrogates during both encoding and decoding unless the surrogatepass error handler is used, with the exception of the UTF-16 decoder and the UTF-16 encoder
- [ ] New German EBCDIC codec `cp273`.
- [ ] New Ukrainian codec `cp1125`.
- [ ] `bytes.join()` and `bytearray.join()` now accept arbitrary buffer objects as arguments.
- [ ] The `int` constructor now accepts any object that has an `__index__` method for its base argument.
- [ ] Frame objects now have a `clear()` method that clears all references to local variables from the frame.
- [ ] `memoryview` is now registered as a `Sequence`, and supports the `reversed()` builtin.
- [ ] Signatures reported by `help()` have been modified and improved in several cases as a result of the introduction of Argument Clinic and other changes to the `inspect` and `pydoc` modules.
- [ ] `__length_hint__()` is now part of the formal language specification

New Modules
===========
- [ ] `asyncio`
- [ ] `ensurepip`
- [ ] `enum`
- [ ] `pathlib`
- [ ] `selectors`
- [ ] `statistics`
- [ ] `tracemalloc`

Improved Modules
================
- [ ] `abc`
- [ ] `aifc`
- [ ] `argparse`
- [ ] `audioop`
- [ ] `base64`
- [ ] `collections`
- [ ] `colorsys`
- [ ] `contextlib`
- [ ] `dbm`
- [ ] `dis`
- [ ] `doctest`
- [ ] `email`
- [ ] `filecmp`
- [ ] `functools`
- [ ] `gc`
- [ ] `glob`
- [ ] `hashlib`
- [ ] `hmac`
- [ ] `html`
- [ ] `http`
- [ ] `idlelib-and-idle`
- [ ] `importlib`
- [ ] `inspect`
- [ ] `ipaddress`
- [ ] `logging`
- [ ] `marshal`
- [ ] `mmap`
- [ ] `multiprocessing`
- [ ] `operator`
- [ ] `os`
- [ ] `pdb`
- [ ] `pickle`
- [ ] `plistlib`
- [ ] `poplib`
- [ ] `pprint`
- [ ] `pty`
- [ ] `pydoc`
- [ ] `re`
- [ ] `resource`
- [ ] `select`
- [ ] `shelve`
- [ ] `shutil`
- [ ] `smtpd`
- [ ] `smtplib`
- [ ] `socket`
- [ ] `sqlite3`
- [ ] `ssl`
- [ ] `stat`
- [ ] `struct`
- [ ] `subprocess`
- [ ] `sunau`
- [ ] `sys`
- [ ] `tarfile`
- [ ] `textwrap`
- [ ] `threading`
- [ ] `traceback`
- [ ] `types`
- [ ] `urllib`
- [ ] `unittest`
- [ ] `venv`
- [ ] `wave`
- [ ] `weakref`
- [ ] `xml.etree`
- [ ] `zipfile`


Deprecations in the Python API
==============================
- [ ] As mentioned in PEP 451: A ModuleSpec Type for the Import System, a number of importlib methods and functions are deprecated: importlib.find_loader() is replaced by importlib.util.find_spec(); importlib.machinery.PathFinder.find_module() is replaced by importlib.machinery.PathFinder.find_spec(); importlib.abc.MetaPathFinder.find_module() is replaced by importlib.abc.MetaPathFinder.find_spec(); importlib.abc.PathEntryFinder.find_loader() and find_module() are replaced by importlib.abc.PathEntryFinder.find_spec(); all of the xxxLoader ABC load_module methods (importlib.abc.Loader.load_module(), importlib.abc.InspectLoader.load_module(), importlib.abc.FileLoader.load_module(), importlib.abc.SourceLoader.load_module()) should no longer be implemented, instead loaders should implement an exec_module method (importlib.abc.Loader.exec_module(), importlib.abc.InspectLoader.exec_module() importlib.abc.SourceLoader.exec_module()) and let the import system take care of the rest; and importlib.abc.Loader.module_repr(), importlib.util.module_for_loader(), importlib.util.set_loader(), and importlib.util.set_package() are no longer needed because their functions are now handled automatically by the import system.
- [ ] The imp module is pending deprecation. To keep compatibility with Python 2/3 code bases, the module's removal is currently not scheduled.
- [ ] The formatter module is pending deprecation and is slated for removal in Python 3.6.
- [ ] MD5 as the default digestmod for the hmac.new() function is deprecated. Python 3.6 will require an explicit digest name or constructor as digestmod argument.
- [ ] The internal Netrc class in the ftplib module has been documented as deprecated in its docstring for quite some time. It now emits a DeprecationWarning and will be removed completely in Python 3.5.
- [ ] The undocumented endtime argument to subprocess.Popen.wait() should not have been exposed and is hopefully not in use; it is deprecated and will mostly likely be removed in Python 3.5.
- [ ] The strict argument of HTMLParser is deprecated.
- [ ] The plistlib readPlist(), writePlist(), readPlistFromBytes(), and writePlistToBytes() functions are deprecated in favor of the corresponding new functions load(), dump(), loads(), and dumps(). Data() is deprecated in favor of just using the bytes constructor.
- [ ] The sysconfig key SO is deprecated, it has been replaced by EXT_SUFFIX.
- [ ] The U mode accepted by various open functions is deprecated. In Python3 it does not do anything useful, and should be replaced by appropriate uses of io.TextIOWrapper (if needed) and its newline argument.
- [ ] The parser argument of xml.etree.ElementTree.iterparse() has been deprecated, as has the html argument of XMLParser(). To prepare for the removal of the latter, all arguments to XMLParser should be passed by keyword.

Deprecated Features
===================
- [ ] Running IDLE with the -n flag (no subprocess) is deprecated. However, the feature will not be removed until bpo-18823 is resolved.
- [ ] The site module adding a “site-python” directory to sys.path, if it exists, is deprecated (bpo-19375).

API and Feature Removals
========================
- [ ] The unmaintained Misc/TextMate and Misc/vim directories have been removed (see the devguide for suggestions on what to use instead).
- [ ] The SO makefile macro is removed (it was replaced by the SHLIB_SUFFIX and EXT_SUFFIX macros) (bpo-16754).
- [ ] The `PyThreadState.tick_counter` field has been removed; its value has been meaningless since Python 3.2, when the “new GIL” was introduced (bpo-19199).
- [ ] `PyLoader` and `PyPycLoader` have been removed from `importlib`. (Contributed by Taras Lyapun in bpo-15641.)
- [ ] The `strict` argument to `HTTPConnection` and `HTTPSConnection` has been removed. HTTP 0.9-style “Simple Responses” are no longer supported.
- [ ] The deprecated `urllib.request.Request` getter and setter methods `add_data`, `has_data`, `get_data`, `get_type`, `get_host`, `get_selector`, `set_proxy`, `get_origin_req_host`, and `is_unverifiable` have been removed (use direct attribute access instead).
- [ ] Support for loading the deprecated `TYPE_INT64` has been removed from marshal. (Contributed by Dan Riti in bpo-15480.)
- [ ] `inspect.Signature`: positional-only parameters are now required to have a valid name.
- [ ] `object.__format__()` no longer accepts non-empty format strings, it now raises a `TypeError` instead. Using a non-empty string has been deprecated since Python 3.2. This change has been made to prevent a situation where previously working (but incorrect) code would start failing if an object gained a `__format__` method, which means that your code may now raise a TypeError if you are using an 's' format code with objects that do not have a `__format__` method that handles it. See bpo-7994 for background.
- [ ] `difflib.SequenceMatcher.isbjunk()` and `difflib.SequenceMatcher.isbpopular()` were deprecated in 3.2, and have now been removed: use `x in sm.bjunk` and `x in sm.bpopular`, where `sm` is a `SequenceMatcher` object (bpo-13248).

[PEP 453]: https://www.python.org/dev/peps/pep-0453
[PEP 446]: https://www.python.org/dev/peps/pep-0446
[PEP 451]: https://www.python.org/dev/peps/pep-0451
