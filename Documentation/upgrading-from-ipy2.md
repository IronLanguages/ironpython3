# Upgrading from IronPython 2 to 3

IronPython 3.4 uses Python 3.4 syntax and standard libraries and so your Python code will need to be updated accordingly. There are numerous tools and guides available on the web to help porting from Python 2 to 3.

## Binary compatibility

The IronPython 3 binaries are not compatible with the IronPython 2 binaries. Modules compiled with `clr.CompileModules` using IronPython 2 are not compatible and will need to be recompiled using IronPython 3.

## Checking for IronPython

In an effort to improve compatibility, `sys.platform` no longer returns `cli`. If you wish to check if you're running on IronPython the recommended pattern is to check that `sys.implementation.name` is equal to `ironpython`:

```Python
if sys.implementation.name == "ironpython":
    print("IronPython!")
```

## `None` is a keyword

`None` is a keyword in Python 3 and trying to access a member called `None` will raise a `SyntaxError`. Since this name is frequently used in .NET code (e.g. in enums), code trying to use it is going to throw. You can use alternate syntax in order to access the .NET member, for example `getattr(x, "None")` or an accessor for enums `MyEnum["None"]`.

```python
# IronPython 2
System.StringSplitOptions.None
```

```python
# IronPython 3
System.StringSplitOptions["None"]
```

Similarly, `True` and `False` are also keywords in Python 3.

## `int` Type

One of the major backward incompatible changes in Python 3 is [PEP 237 – Unifying Long Integers and Integers][PEP 0237]: Essentially, `long` renamed to `int`. That is, there is only one built-in integral type, named `int`; but it behaves mostly like the old `long` type. From the pure Python perspective this means that `int` should be used wherever previously `long` was used. More consideration has to be applied in interop cases with .NET.

The Python `int` type in IronPython 3 is implemented as `System.Numerics.BigInteger` (and not as `System.Int32` as it was in IronPython 2). It can contain in theory an arbitrarily large integer (only limited by the 2 GByte memory boundary).

```pycon
>>> import clr
>>> clr.AddReference("System.Numerics")
>>> import System
>>> int is System.Numerics.BigInteger
True
>>> int is System.Int32
False
>>> clr.GetClrType(int).Name
'BigInteger'
```

This means that in interop cases, when the `int` type is used (think generics), it will mean `BigInteger` and not `Int32` (which was the case in IronPython 2). To retain IronPython 2 semantics, replace `int` with `System.Int32`.

Example:

```python
# IronPython 2
System.Collections.Generic.List[int]
```

```python
# IronPython 3
System.Collections.Generic.List[System.Int32]
```

Overview of `int` type equivalency:

| IronPython 2 | IronPython 3 | .NET                         |
| ------------ | ------------ | ---------------------------- |
| `long`       | `int`        | `System.Numerics.BigInteger` |
| `int`        | N/A          | `System.Int32`               |

### Instances of `int`

As for instances of `int`, mostly for performance reasons, IronPython may use instances of `System.Int32` to hold smaller integers, while `BigInteger` instances are used for large integers. This is done transparently from the Python side, but again the distinction may become relevant for interop cases. Examples:

```python
i = 1        # instance of Int32
j = 1 << 31  # instance of BigInteger
k = j - 1    # still BigInteger, as one of the arguments makes the result type BigInteger
```

This means that the type of `Int32` objects is always reported as `int` (which is the same as `BigInteger`). If it is important to check what is the actual type of a given integer object, test if the object is an instance of `System.Int32`. (An alternative way is a test for the presence of `MaxValue` or `MinValue`. For those properties to be visible, `System` has to be imported first.)

```pycon
>>> import System
>>> type(i)
<class 'int'>
>>> isinstance(i, System.Int32)
True
>>> type(j)
<class 'int'>
>>> isinstance(j, System.Int32)
False
>>> hex(i.MaxValue)
'0x7fffffff'
```

The creation of either `Int32` or `BigInteger` instances happens automatically by the `int` constructor. If for interop purposes it is important to create a `BigInteger` (despite the value fitting in 32 bits), use method `ToBigInteger`. It converts `Int32` values to `BigInteger` and leaves `BigInteger` values unaffected.

```pycon
>>> bi = i.ToBigInteger()
>>> isinstance(j, System.Int32)
False
```

In the opposite direction, if it is essential to create `Int32` objects, either use constructors for `int` or `Int32`. In the current implementation, the former converts an integer to `Int32` if the value fits in 32 bits, otherwise it leaves it as `BigInteger`. The latter throws an exception if the conversion is not possible. Although the behavior of the constructor `int` may or may not change in the future, it is always guaranteed to convert the value to the "canonical form" adopted for that version of IronPython.

```pycon
>>> # k is a BigInteger that fits in 32 bits
>>> isinstance(k, System.Int32)
False
>>> hex(k)
'0x7fffffff'
>>> ki = int(k)  # converts k to Int32
>>> isinstance(ki, System.Int32)
True
>>> ki = System.Int32(k) # also converts k to Int32
>>> isinstance(ki, System.Int32)
True
>>> # j is a BigInteger that does not fit in 32 bits
>>> isinstance(j, System.Int32)
False
>>> hex(j)
'0x80000000'
>>> j = int(j)  # no type change, j stays BigInteger
>>> isinstance(j, System.Int32)
False
>>> j = System.Int32(j)  # conversion fails
Traceback (most recent call last):
  File "<stdin>", line 1, in <module>
OverflowError: Arithmetic operation resulted in an overflow.
```

Such explicit conversions are in most cases unnecessary since the runtime recognizes `int`/`Int32` equivalence of instances and performs necessary conversions automatically.

```pycon
>>> import System
>>> int_list = System.Collections.Generic.List[int]()
>>> int_list.Add(1) # Int32 instance converted to BigInteger
>>> int32_list = System.Collections.Generic.List[System.Int32]()
>>> int32_list.Add((1).ToBigInteger()) # BigInteger instance converted to Int32
>>> int_list[0] == int32_list[0]
True
```

### Pickling and unpickling of `int`

When an `int` object is serialized using `pickle.dump(x, myfile)` and subsequently unpickled with `x = pickle.load(myfile)` (or `pickle.loads(pickle.dumps(x))`, this has the same effect as reconstructing the object using the `int` constructor, i.e. `x = int(x)`. In other words, if the `x` instance was `BigInteger` but the value fits in `Int32`, it will be reconstructed as `Int32`.

### BigIntegerV2 API

In IronPython 2, `long` type carries an obsolete `BigIntegerV2` API, accessible after importing `System`. In IronPython 3 this API is not available directly on `int` instances (regardless of whether the instance is `Int32` or `BigInteger`), but is still accessible in some form through `Microsoft.Scripting.Utils.MathUtils` in `Microsoft.Dynamic.dll`.

```pycon
>>> # IronPython 2
>>> i = 1        # instance of Int32 (int)
>>> j = 1 << 64  # instance of BigInteger (long)
>>> import System
>>> j.GetWords()
Array[UInt32]((0, 0, 1))
>>> i.GetWords()
Traceback (most recent call last):
  File "<stdin>", line 1, in <module>
AttributeError: 'int' object has no attribute 'GetWords'
>>> long.GetWords(i)
Array[UInt32]((1))
```

```pycon
>>> # IronPython 3
>>> i = 1        # instance of Int32 (int)
>>> j = 1 << 64  # instance of BigInteger (int)
>>> import clr
>>> clr.AddReference("Microsoft.Dynamic")
>>> import Microsoft.Scripting.Utils.MathUtils
>>> clr.ImportExtensions(Microsoft.Scripting.Utils.MathUtils)
>>> j.GetWords()
Array[UInt32]((0, 0, 1))
>>> i.GetWords()
Traceback (most recent call last):
  File "<stdin>", line 1, in <module>
AttributeError: 'int' object has no attribute 'GetWords'
>>> Microsoft.Scripting.Utils.MathUtils.GetWords(i)
Array[UInt32]((1))
```

Another set of Python-hidden methods on `long` in IronPython 2 that are not available on `int` in IronPython 3 are conversion methods with names like `ToXxx`. The recommended way to perform type conversions like those is to use type constructors. The exception is the conversion to `BigInteger` itself, for the reasons explained above.

```python
# IronPython 2
j = long(1)
i64 = j.ToInt64()
```

```python
# IronPython 3
import System
j = (1).ToBigInteger()
i64 = System.Int64(j)
```

### `range`

IronPython's `range` is a generator that produces a sequence of `int` values. The values are instances of `Int32` or `BigInteger`, depending on the actual integer value they represent. When `range` is used in a LINQ context, it exposes interface `IEnumerable<Int32>` and all values generated are of type `Int32`. This limits the possible value to the range `Int32.MinValue` to `Int32.MaxValue`.

[PEP 0237]: https://python.org/dev/peps/pep-0237


## Redirecting output

With IronPython 2, standard output was written to the runtime's `SharedIO.OutputWriter` (which was `Console.Out` by default). This is no longer the case with IronPython 3 where the standard output is a binary stream. The output is now written to runtime's `SharedIO.OutputStream`. Similarly, standard input and error are now using `SharedIO.InputStream` and `SharedIO.ErrorStream` respectively.

Because of this, using a `TextWriter` to capture output will no longer work. As a workaround, in order to use a `TextWriter` as the main method of redirection, one could wrap the writer inside a stream (for example, see [TextStream][TextStream]).

IronPython 2
```c#
var engine = Python.CreateEngine();
var textWriter = new MyTextWriter();
// no longer works!
engine.Runtime.IO.RedirectToConsole();
Console.SetOut(textWriter);
```

IronPython 3
```c#
var engine = Python.CreateEngine();
var textWriter = new MyTextWriter();
engine.Runtime.IO.SetOutput(new TextStream(textWriter), textWriter);
```

[TextStream]: https://github.com/IronLanguages/main/blob/master/Runtime/Microsoft.Dynamic/Utils/TextStream.cs
