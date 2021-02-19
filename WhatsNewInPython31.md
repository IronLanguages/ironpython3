# What's New In Python 3.1

https://docs.python.org/3/whatsnew/3.1.html

PEPs
====
- [x] [PEP 372][]: Ordered Dictionaries
- [x] [PEP 378][]: Format Specifier for Thousands Separator

Other Language Changes
=======================
- [ ] Directories and zip archives containing a `__main__.py` file can now be executed directly by passing their name to the interpreter. The directory/zipfile is automatically inserted as the first entry in `sys.path`.
- [x] The `int()` type gained a `bit_length` method that returns the number of bits necessary to represent its argument in binary.
- [x] The fields in `format()` strings can now be automatically numbered.
- [x] The `string.maketrans()` function is deprecated and is replaced by new static methods, `bytes.maketrans()` and `bytearray.maketrans()`. This change solves the confusion around which types were supported by the string module. Now, `str`, `bytes`, and `bytearray` each have their own `maketrans` and `translate` methods with intermediate translation tables of the appropriate type.
- [x] The syntax of the `with` statement now allows multiple context managers in a single statement.
- [x] `round(x, n)` now returns an integer if `x` is an integer. Previously it returned a `float`.
- [ ] Python now uses David Gay's algorithm for finding the shortest floating point representation that doesn't change its value.

New, Improved, and Deprecated Modules
==============
- [x] Added a `collections.Counter` class to support convenient counting of unique items in a sequence or iterable.
- [ ] Added a new module, `tkinter.ttk` for access to the Tk themed widget set. The basic idea of `ttk` is to separate, to the extent possible, the code implementing a widget's behavior from the code implementing its appearance.
- [ ] The `gzip.GzipFile` and `bz2.BZ2File` classes now support the context management protocol.
- [x] The `decimal` module now supports methods for creating a decimal object from a binary float.
- [x] The `itertools` module grew two new functions. The `itertools.combinations_with_replacement()` function is one of four for generating combinatorics including permutations and Cartesian products. The `itertools.compress()` function mimics its namesake from APL. Also, the existing `itertools.count()` function now has an optional step argument and can accept any type of counting sequence including `fractions.Fraction` and `decimal.Decimal`.
- [x] `collections.namedtuple()` now supports a keyword argument rename which lets invalid fieldnames be automatically converted to positional names in the form `_0`, `_1`, etc. This is useful when the field names are being created by an external source such as a CSV header, SQL field list, or user input.
- [x] The `re.sub()`, `re.subn()` and `re.split()` functions now accept a `flags` parameter.
- [ ] The `logging` module now implements a simple `logging.NullHandler` class for applications that are not using logging but are calling library code that does. Setting-up a null handler will suppress spurious warnings such as "No handlers could be found for logger foo".
- [ ] The `runpy` module which supports the `-m` command line switch now supports the execution of packages by looking for and executing a `__main__` submodule when a package name is supplied.
- [ ] The `pdb` module can now access and display source code loaded via `zipimport` (or any other conformant PEP 302 loader).
- [ ] `functools.partial` objects can now be pickled.
- [ ] Add `pydoc` help topics for symbols so that `help('@')` works as expected in the interactive environment.
- [ ] The `unittest` module now supports skipping individual tests or classes of tests. And it supports marking a test as an expected failure, a test that is known to be broken, but shouldn't be counted as a failure on a `TestResult`. Also, tests for exceptions have been built out to work with context managers using the `with` statement. In addition, several new assertion methods were added including `assertSetEqual()`, `assertDictEqual()`, `assertDictContainsSubset()`, `assertListEqual()`, `assertTupleEqual()`, `assertSequenceEqual()`, `assertRaisesRegexp()`, `assertIsNone()`, and `assertIsNotNone()`.
- [x] The `io` module has three new constants for the `seek()` method: `SEEK_SET`, `SEEK_CUR`, and `SEEK_END`.
- [x] `sys.version_info` tuple is now a named tuple.
- [ ] The `nntplib` and `imaplib` modules now support IPv6.
- [ ] The `pickle` module has been adapted for better interoperability with Python 2.x when used with protocol 2 or lower. The reorganization of the standard library changed the formal reference for many objects. For example, `__builtin__.set` in Python 2 is called `builtins.set` in Python 3. This change confounded efforts to share data between different versions of Python. But now when protocol 2 or lower is selected, the pickler will automatically use the old Python 2 names for both loading and dumping. This remapping is turned-on by default but can be disabled with the `fix_imports` option. An unfortunate but unavoidable side-effect of this change is that protocol 2 pickles produced by Python 3.1 won't be readable with Python 3.0. The latest pickle protocol, protocol 3, should be used when migrating data between Python 3.x implementations, as it doesn't attempt to remain compatible with Python 2.x.
- [ ] A new module, `importlib` was added. It provides a complete, portable, pure Python reference implementation of the `import` statement and its counterpart, the `__import__()` function. It represents a substantial step forward in documenting and defining the actions that take place during imports.

[PEP 372]: https://python.org/dev/peps/pep-0372
[PEP 378]: https://python.org/dev/peps/pep-0378
