# What's New In Python 3.4

https://docs.python.org/3/whatsnew/3.4.html

PEPs
====
- [ ] [PEP 453][]: Pip should always be available
- [ ] [PEP 446][]: Newly created file descriptors are non-inheritable
- [ ] [PEP 451][]:  A `ModuleSpec` Type for the Import System

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
- [ ] `xml-etree`

Deprecated Python modules, functions and methods
==============

- [ ] The `PyThreadState.tick_counter` field has been removed; its value has been meaningless since Python 3.2, when the “new GIL” was introduced
- [ ] `PyLoader` and `PyPycLoader` have been removed from `importlib`.
- [ ] The `strict` argument to `HTTPConnection` and `HTTPSConnection` has been removed. HTTP 0.9-style “Simple Responses” are no longer supported.
- [ ] The deprecated `urllib.request.Request` getter and setter methods `add_data`, `has_data`, `get_data`, `get_type`, `get_host`, `get_selector`, `set_proxy`, `get_origin_req_host`, and `is_unverifiable` have been removed (use direct attribute access instead).
- [ ] Support for loading the deprecated `TYPE_INT64` has been removed from marshal.
- [ ] `inspect.Signature`: positional-only parameters are now required to have a valid name.
- [ ] `object.__format__()` no longer accepts non-empty format strings, it now raises a `TypeError` instead. Using a non-empty string has been deprecated since Python 3.2
- [ ] `difflib.SequenceMatcher.isbjunk()` and `difflib.SequenceMatcher.isbpopular()` were deprecated in 3.2, and have now been removed: use `x in sm.bjunk` and `x in sm.bpopular`, where `sm` is a `SequenceMatcher` object

[PEP 453]: https://www.python.org/dev/peps/pep-0453
[PEP 446]: https://www.python.org/dev/peps/pep-0446
[PEP 451]: https://www.python.org/dev/peps/pep-0451
