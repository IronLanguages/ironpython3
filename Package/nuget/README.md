IronPython Engine
=================

IronPython is an open-source implementation of the Python programming language that is tightly integrated with .NET. IronPython can use .NET and Python libraries, and other .NET languages can use Python code just as easily.

This package contains the IronPython engine that allows embedding an IronPython interpreter in a .NET application. The interpreted Python code can call the .NET code and can be called from the .NET code. This package does not contain the IronPython Standard Library, which is distributed separatery as package `IronPython.StdLib`.

## Example

Execute Python code and call it from .NET code:

```cs
var eng = IronPython.Hosting.Python.CreateEngine();
var scope = eng.CreateScope();
eng.Execute(@"
def greetings(name):
    return 'Hello ' + name.title() + '!'
", scope);
dynamic greetings = scope.GetVariable("greetings");
System.Console.WriteLine(greetings("world"));
```

## Differences with CPython

While compatibility with CPython is one of our main goals with IronPython 3, there are still some differences that may cause issues. See [Differences from CPython](https://github.com/IronLanguages/ironpython3/blob/master/Documentation/differences-from-c-python.md) for details.

## Package compatibility

See the [Package compatibility](https://github.com/IronLanguages/ironpython3/blob/master/Documentation/package-compatibility.md) document for information on compatibility with popular Python packages. Note that to run most packages, IronPython Standard Library must be present.
