# What's New In Python 3.0

https://docs.python.org/3/whatsnew/3.0.html

Views And Iterators Instead Of Lists
=======================
- [x] dict methods dict.keys(), dict.items() and dict.values() return "views" instead of lists. For example, this no longer works: k = d.keys(); k.sort(). Use k = sorted(d) instead (this works in Python 2.5 too and is just as efficient).
- [x] Also, the dict.iterkeys(), dict.iteritems() and dict.itervalues() methods are no longer supported.
- [x] map() and filter() return iterators. If you really need a list, a quick fix is e.g. list(map(...)), but a better fix is often to use a list comprehension (especially when the original code uses lambda), or rewriting the code so it doesn't need a list at all. Particularly tricky is map() invoked for the side effects of the function; the correct transformation is to use a regular for loop (since creating a list would just be wasteful).
- [x] zip() now returns an iterator.

Ordering Comparisons
==============
- [ ] The ordering comparison operators (<, <=, >=, >) raise a TypeError exception when the operands don't have a meaningful natural ordering. Thus, expressions like 1 < '', 0 > None or len <= len are no longer valid, and e.g. None < None raises TypeError instead of returning False. A corollary is that sorting a heterogeneous list no longer makes sense - all the elements must be comparable to each other. Note that this does not apply to the == and != operators: objects of different incomparable types always compare unequal to each other.
- [ ] builtin.sorted() and list.sort() no longer accept the cmp argument providing a comparison function. Use the key argument instead. N.B. the key and reverse arguments are now "keyword-only"
- [ ] The cmp() function should be treated as gone, and the \_\_cmp\_\_() special method is no longer supported. Use \_\_lt\_\_() for sorting, \_\_eq\_\_() with \_\_hash\_\_(), and other rich comparisons as needed. (If you really need the cmp() functionality, you could use the expression (a > b) - (a < b) as the equivalent for cmp(a, b).)

Integers
=====
- [ ] PEP 0237: Essentially, long renamed to int. That is, there is only one built-in integral type, named int; but it behaves mostly like the old long type.
- [x] PEP 0238: An expression like 1/2 returns a float. Use 1//2 to get the truncating behavior. (The latter syntax has existed for years, at least since Python 2.2.)
- [x] The sys.maxint constant was removed, since there is no longer a limit to the value of integers. However, sys.maxsize can be used as an integer larger than any practical list or string index. It conforms to the implementation's "natural" integer size and is typically the same as sys.maxint in previous releases on the same platform (assuming the same build options).
- [x] The repr() of a long integer doesn't include the trailing L anymore, so code that unconditionally strips that character will chop off the last digit instead. (Use str() instead.)
- [x] Octal literals are no longer of the form 0720; use 0o720 instead.

Text Vs. Data Instead Of Unicode Vs. 8-bit
===========================
- [ ] Python 3.0 uses the concepts of text and (binary) data instead of Unicode strings and 8-bit strings. All text is Unicode; however encoded Unicode is represented as binary data. The type used to hold text is str, the type used to hold data is bytes. The biggest difference with the 2.x situation is that any attempt to mix text and data in Python 3.0 raises TypeError, whereas if you were to mix Unicode and 8-bit strings in Python 2.x, it would work if the 8-bit string happened to contain only 7-bit (ASCII) bytes, but you would get UnicodeDecodeError if it contained non-ASCII values. This value-specific behavior has caused numerous sad faces over the years.
- [ ] As a consequence of this change in philosophy, pretty much all code that uses Unicode, encodings or binary data most likely has to change. The change is for the better, as in the 2.x world there were numerous bugs having to do with mixing encoded and unencoded text. To be prepared in Python 2.x, start using unicode for all unencoded text, and str for binary or encoded data only. Then the 2to3 tool will do most of the work for you.
- [x] ~~You can no longer use u"..." literals for Unicode text.~~ (Readded in Python 3.3). However, you must use b"..." literals for binary data.
- [ ] As the str and bytes types cannot be mixed, you must always explicitly convert between them. Use str.encode() to go from str to bytes, and bytes.decode() to go from bytes to str. You can also use bytes(s, encoding=...) and str(b, encoding=...), respectively.
- [ ] Like str, the bytes type is immutable. There is a separate mutable type to hold buffered binary data, bytearray. Nearly all APIs that accept bytes also accept bytearray. The mutable API is based on collections.MutableSequence.
- [ ] All backslashes in raw string literals are interpreted literally. This means that '\U' and '\u' escapes in raw strings are not treated specially. For example, r'\u20ac' is a string of 6 characters in Python 3.0, whereas in 2.6, ur'\u20ac' was the single "euro" character. (Of course, this change only affects raw string literals; the euro character is '\u20ac' in Python 3.0.)
- [x] The builtin basestring abstract type was removed. Use str instead. The str and bytes types don't have functionality enough in common to warrant a shared base class. The 2to3 tool (see below) replaces every occurrence of basestring with str.
- [ ] Files opened as text files (still the default mode for open()) always use an encoding to map between strings (in memory) and bytes (on disk). Binary files (opened with a b in the mode argument) always use bytes in memory. This means that if a file is opened using an incorrect mode or encoding, I/O will likely fail loudly, instead of silently producing incorrect data. It also means that even Unix users will have to specify the correct mode (text or binary) when opening a file. There is a platform-dependent default encoding, which on Unixy platforms can be set with the LANG environment variable (and sometimes also with some other platform-specific locale-related environment variables). In many cases, but not all, the system default is UTF-8; you should never count on this default. Any application reading or writing more than pure ASCII text should probably have a way to override the encoding. There is no longer any need for using the encoding-aware streams in the codecs module.
- [ ] Filenames are passed to and returned from APIs as (Unicode) strings. This can present platform-specific problems because on some platforms filenames are arbitrary byte strings. (On the other hand, on Windows filenames are natively stored as Unicode.) As a work-around, most APIs (e.g. open() and many functions in the os module) that take filenames accept bytes objects as well as strings, and a few APIs have a way to ask for a bytes return value. Thus, os.listdir() returns a list of bytes instances if the argument is a bytes instance, and os.getcwdb() returns the current working directory as a bytes instance. Note that when os.listdir() returns a list of strings, filenames that cannot be decoded properly are omitted rather than raising UnicodeError.
- [ ] Some system APIs like os.environ and sys.argv can also present problems when the bytes made available by the system is not interpretable using the default encoding. Setting the LANG variable and rerunning the program is probably the best approach.
- [x] PEP 3138: The repr() of a string no longer escapes non-ASCII characters. It still escapes control characters and code points with non-printable status in the Unicode standard, however.
- [ ] PEP 3120: The default source encoding is now UTF-8.
- [ ] PEP 3131: Non-ASCII letters are now allowed in identifiers. (However, the standard library remains ASCII-only with the exception of contributor names in comments.)
- [x] The StringIO and cStringIO modules are gone. Instead, import the io module and use io.StringIO or io.BytesIO for text and data respectively.


New Syntax
========
- [ ] PEP 3107: Function argument and return value annotations. This provides a standardized way of annotating a function's parameters and return value. There are no semantics attached to such annotations except that they can be introspected at runtime using the \_\_annotations\_\_ attribute. The intent is to encourage experimentation through metaclasses, decorators or frameworks.
- [ ] PEP 3102: Keyword-only arguments. Named parameters occurring after *args in the parameter list must be specified using keyword syntax in the call. You can also use a bare * in the parameter list to indicate that you don't accept a variable-length argument list, but you do have keyword-only arguments.
- [ ] Keyword arguments are allowed after the list of base classes in a class definition. This is used by the new convention for specifying a metaclass (see next section), but can be used for other purposes as well, as long as the metaclass supports it.
- [ ] PEP 3104: nonlocal statement. Using nonlocal x you can now assign directly to a variable in an outer (but non-global) scope. nonlocal is a new reserved word.
- [ ] PEP 3132: Extended Iterable Unpacking. You can now write things like a, b, *rest = some\_sequence. And even *rest, a = stuff. The rest object is always a (possibly empty) list; the right-hand side may be any iterable
- [x] Dictionary comprehensions: {k: v for k, v in stuff} means the same thing as dict(stuff) but is more flexible. (This is PEP 0274 vindicated. :-)
- [x] Set literals, e.g. {1, 2}. Note that {} is an empty dictionary; use set() for an empty set. Set comprehensions are also supported; e.g., {x for x in stuff} means the same thing as set(stuff) but is more flexible.
- [x] New octal literals, e.g. 0o720 (already in 2.6). The old octal literals (0720) are gone.
- [x] New binary literals, e.g. 0b1010 (already in 2.6), and there is a new corresponding builtin function, bin().
- [ ] Bytes literals are introduced with a leading b or B, and there is a new corresponding builtin function, bytes().

Changed Syntax
==========
- [x] PEP 3109 and PEP 3134: new raise statement syntax: raise \[expr \[from expr\]\].
- [x] as and with are now reserved words. (Since 2.6, actually.)
- [x] True, False, and None are reserved words. (2.6 partially enforced the restrictions on None already.)
- [x] Change from except exc, var to except exc as var. See PEP 3110
- [ ] PEP 3115: New Metaclass Syntax
- [ ] List comprehensions no longer support the syntactic form [... for var in item1, item2, ...]. Use [... for var in (item1, item2, ...)] instead.
- [ ] The ellipsis (...) can be used as an atomic expression anywhere. (Previously it was only allowed in slices.) Also, it must now be spelled as .... (Previously it could also be spelled as . . ., by a mere accident of the grammar.)

Removed Syntax
===========
- [x] PEP 3113: Tuple parameter unpacking removed. You can no longer write def foo(a, (b, c)): .... Use def foo(a, b\_c): b, c = b\_c instead.
- [x] Removed backticks (use repr() instead).
- [x] Removed <> (use != instead).
- [x] Removed keyword: exec() is no longer a keyword; it remains as a function
- [x] Integer literals no longer support a trailing l or L.
- [x] ~~String literals no longer support a leading u or U.~~ (Readded in Python 3.3)
- [ ] The from module import * syntax is only allowed at the module level, no longer inside functions.
- [x] The only acceptable syntax for relative imports is from .[module] import name. All import forms not starting with . are interpreted as absolute imports. (PEP 0328)
- [x] Classic classes are gone.

Library Changes
==========
- [x] \_winreg renamed to winreg
- [x] copy\_reg renamed to copyreg
- [ ] Cleanup of the sys module: removed sys.exitfunc(), sys.exc\_clear(), sys.exc\_type, sys.exc\_value, sys.exc\_traceback.
- [ ] Cleanup of the array.array type: the read() and write() methods are gone; use fromfile() and tofile() instead. Also, the 'c' typecode for array is gone - use either 'b' for bytes or 'u' for Unicode characters.
- [x] Cleanup of the operator module: removed sequenceIncludes() and isCallable().
- [ ] Cleanup of the thread module: acquire\_lock() and release\_lock() are gone; use acquire() and release() instead.
- [x] Cleanup of the random module: removed the jumpahead() API.
- [x] The functions os.tmpnam(), os.tempnam() and os.tmpfile() have been removed in favor of the tempfile module.
- [ ] string.letters and its friends (string.lowercase and string.uppercase) are gone. Use string.ascii\_letters etc. instead. (The reason for the removal is that string.letters and friends had locale-specific behavior, which is a bad idea for such attractively-named global "constants".)

PEP 3101: A New Approach To String Formatting
======================
- [x] A new system for built-in string formatting operations replaces the % string formatting operator. (However, the % operator is still supported; it will be deprecated in Python 3.1 and removed from the language at some later time.) Read PEP 3101 for the full scoop.

Changes To Exceptions
===========
- [ ] PEP 0352: All exceptions must be derived (directly or indirectly) from BaseException. This is the root of the exception hierarchy. This is not new as a recommendation, but the requirement to inherit from BaseException is new. (Python 2.6 still allowed classic classes to be raised, and placed no restriction on what you can catch.) As a consequence, string exceptions are finally truly and utterly dead.
- [ ] Almost all exceptions should actually derive from Exception; BaseException should only be used as a base class for exceptions that should only be handled at the top level, such as SystemExit or KeyboardInterrupt. The recommended idiom for handling all exceptions except for this latter category is to use except Exception.
- [x] StandardError was removed (in 2.6 already).
- [ ] Exceptions no longer behave as sequences. Use the args attribute instead.
- [x] PEP 3109: Raising exceptions. You must now use raise Exception(args) instead of raise Exception, args. Additionally, you can no longer explicitly specify a traceback; instead, if you have to do this, you can assign directly to the \_\_traceback\_\_ attribute (see below).
- [x] PEP 3110: Catching exceptions. You must now use except SomeException as variable instead of except SomeException, variable. Moreover, the variable is explicitly deleted when the except block is left.
- [x] PEP 3134: Exception chaining. There are two cases: implicit chaining and explicit chaining. Implicit chaining happens when an exception is raised in an except or finally handler block. This usually happens due to a bug in the handler block; we call this a secondary exception. In this case, the original exception (that was being handled) is saved as the \_\_context\_\_ attribute of the secondary exception. Explicit chaining is invoked with this syntax: raise SecondaryException() from primary\_exception (where primary\_exception is any expression that produces an exception object, probably an exception that was previously caught). In this case, the primary exception is stored on the \_\_cause\_\_ attribute of the secondary exception. The traceback printed when an unhandled exception occurs walks the chain of \_\_cause\_\_ and \_\_context\_\_ attributes and prints a separate traceback for each component of the chain, with the primary exception at the top. (Java users may recognize this behavior.)
- [x] PEP 3134: Exception objects now store their traceback as the \_\_traceback\_\_ attribute. This means that an exception object now contains all the information pertaining to an exception, and there are fewer reasons to use sys.exc\_info() (though the latter is not removed).
- [ ] A few exception messages are improved when Windows fails to load an extension module. For example, error code 193 is now %1 is not a valid Win32 application. Strings now deal with non-English locales.

Operators And Special Methods
===============
- [ ] != now returns the opposite of ==, unless == returns NotImplemented.
- [ ] The concept of "unbound methods" has been removed from the language. When referencing a method as a class attribute, you now get a plain function object.
- [x] \_\_getslice\_\_(), \_\_setslice\_\_() and \_\_delslice\_\_() were killed. The syntax a[i:j] now translates to a.\_\_getitem\_\_(slice(i, j)) (or \_\_setitem\_\_() or \_\_delitem\_\_(), when used as an assignment or deletion target, respectively).
- [x] PEP 3114: the standard next() method has been renamed to \_\_next\_\_().
- [x] The \_\_oct\_\_() and \_\_hex\_\_() special methods are removed - oct() and hex() use \_\_index\_\_() now to convert the argument to an integer.
- [ ] Removed support for \_\_members\_\_ and \_\_methods\_\_.
- [x] The function attributes named func\_X have been renamed to use the \_\_X\_\_ form, freeing up these names in the function attribute namespace for user-defined attributes. To wit, func\_closure, func\_code, func\_defaults, func\_dict, func\_doc, func\_globals, func\_name were renamed to \_\_closure\_\_, \_\_code\_\_, \_\_defaults\_\_, \_\_dict\_\_, \_\_doc\_\_, \_\_globals\_\_, \_\_name\_\_, respectively.
- [x] \_\_nonzero\_\_() is now \_\_bool\_\_().

Builtins
=====
- [ ] PEP 3135: New super(). You can now invoke super() without arguments and (assuming this is in a regular instance method defined inside a class statement) the right class and instance will automatically be chosen. With arguments, the behavior of super() is unchanged.
- [ ] PEP 3111: raw\_input() was renamed to input(). That is, the new input() function reads a line from sys.stdin and returns it with the trailing newline stripped. It raises EOFError if the input is terminated prematurely. To get the old behavior of input(), use eval(input()).
- [ ] A new builtin next() was added to call the \_\_next\_\_() method on an object.
- [x] Moved intern() to sys.intern()
- [x] Removed: apply(). Instead of apply(f, args) use f(*args).
- [x] ~~Removed callable(). Instead of callable(f) you can use hasattr(f, '\_\_call\_\_').~~ (Readded in Python 3.2) The operator.isCallable() function is also gone.
- [x] Removed coerce(). This function no longer serves a purpose now that classic classes are gone.
- [x] Removed execfile(). Instead of execfile(fn) use exec(open(fn).read()).
- [x] Removed file. Use open().
- [x] Removed reduce(). Use functools.reduce()
- [x] Removed reload(). Use imp.reload().
- [x] Removed. dict.has\_key() - use the in operator instead.