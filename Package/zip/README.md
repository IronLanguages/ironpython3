IronPython
==========

IronPython is an open-source implementation of the Python programming language that is tightly integrated with .NET. IronPython can use .NET and Python libraries, and other .NET languages can use Python code just as easily.

This archive contains the IronPython executables usable for all supported platforms and systems. The executables require a .NET Runtime to be present on the system (not necessarily a .NET SDK). The archive also includes the Python Standard Library released by the Python project, but slightly modified to work better with IronPython and .NET.

The current target is Python 3.4, although features and behaviors from later versions may be included. Refer to the [source code repository](https://github.com/IronLanguages/ironpython3) for list of features from each version of CPython that have been implemented.

## Installation

After unpacking the archive, move or copy one of the `net` directories matching the desired runtime to use to a desired location. The name of the directory may be changed to something more appropriate, but its structure (subdirectories) should remain intact.

If access to the Python Standard Library is desired from IronPython, move or copy the whole `lib` directory **into** the moved/copied directory from the previous step. The `lib` directory has to be in the same directory as `IronPython.dll`.

## Command-line Usage

To start a command-line interpreter on Windows run `ipy.exe` (for .NET Framework) or `ipy.bat` (for .NET). On Posix systems, run `ipy.sh`. `ipy.sh` may be renamed to simply `ipy` for convenience.

Run `ipy -h` for a summary of command-line options. Most are identical to CPython, but there are a few IronPython-specific options.

When reporting issues on [IronPython Project Issues page](https://github.com/IronLanguages/ironpython3/issues), provide the output of `ipy -VV` in the report.

## Embedding IronPython Engine

To embed an IronPython interpreter in a .NET application, simply add references to the DLLs present in the installation directory to your project.

### Example

Execute Python code and call it from .NET code:

```cs
var eng = IronPython.Hosting.Python.CreateEngine();
var scope = eng.CreateScope();
eng.Execute(@"if True:
    def greetings(name):
        return 'Hello ' + name.title() + '!'
", scope);
dynamic greetings = scope.GetVariable("greetings");
System.Console.WriteLine(greetings("world"));
```

## Differences with CPython

While compatibility with CPython is one of our main goals with IronPython 3, there are still some differences that may cause issues. See [Differences from CPython](https://github.com/IronLanguages/ironpython3/blob/master/Documentation/differences-from-c-python.md) for details.

## Package compatibility

See the [Package compatibility](https://github.com/IronLanguages/ironpython3/blob/master/Documentation/package-compatibility.md) document for information on compatibility with popular packages. Note that to run most packages, IronPython Standard Library must be present.
