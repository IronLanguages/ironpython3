IronPython 3
==============
**IronPython3 is NOT ready for use yet. There is still much that needs to be done to support Python 3.x. We are working on it, albeit slowly. We welcome all those who would like to help!**

[Official Website](http://ironpython.net)

IronPython is an open-source implementation of the Python programming language which is tightly integrated with the .NET Framework. IronPython can use the .NET Framework and Python libraries, and other .NET languages can use Python code just as easily.

| **What?** | **Where?** |
| --------: | :------------: |
| **Windows/Linux/macOS Builds** | [![Build status](https://dotnet.visualstudio.com/IronLanguages/_apis/build/status/ironpython3)](https://dotnet.visualstudio.com/IronLanguages/_build/latest?definitionId=43) |
| **Downloads** | No releases |
| **Help** | [![Gitter chat](https://badges.gitter.im/IronLanguages/ironpython.svg)](https://gitter.im/IronLanguages/ironpython) [![StackExchange](https://img.shields.io/stackexchange/stackoverflow/t/ironpython.svg)](http://stackoverflow.com/questions/tagged/ironpython) |


Comparison of IronPython vs. C# for 'Hello World'

C#:

```cs
using System;
class Hello
{
    static void Main() 
    {
        Console.WriteLine("Hello World");
    }
}
```

IronPython:

```py
print("Hello World")
```

IronPython 3 targets Python 3, including the re-organized standard library, Unicode strings, and all of the other new features.

## Code of Conduct
This project has adopted the code of conduct defined by the Contributor Covenant to clarify expected behavior in our community.
For more information see the [.NET Foundation Code of Conduct](https://dotnetfoundation.org/code-of-conduct).

## Installation
Builds of IronPython 3 are not yet provided.

## Build
Make sure you **clone the Git repository recursively** (with `--recurse`) to clone all submodules.

On Windows machines, start a Visual Studio command prompt and type:

    > make
    
On Unix machines, make sure Mono is installed and in the PATH, and type:

    $ make

Since the main development is on Windows, Mono bugs may inadvertantly be introduced
- please report them!

## Supported Platforms
IronPython 3 targets .NET 4.5 and .NET Core 2.0/2.1.
