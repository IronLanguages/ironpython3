IronPython 3
==============
[![Windows Build Status](https://ci.appveyor.com/api/projects/status/2g9hw68byv5ny14y?svg=true)](https://ci.appveyor.com/project/AlexEarl/ironpython3)
[![Linux/OSX Build Status](https://travis-ci.org/IronLanguages/ironpython3.svg?branch=master)](https://travis-ci.org/IronLanguages/ironpython3)
[![Release](https://img.shields.io/github/release/IronLanguages/ironpython3.svg)](https://github.com/IronLanguages/ironpython3/releases/latest)
[![Gitter chat](https://badges.gitter.im/IronLanguages/ironpython.svg)](https://gitter.im/IronLanguages/ironpython)
[![StackExchange](https://img.shields.io/stackexchange/stackoverflow/t/ironpython.svg)](http://stackoverflow.com/questions/tagged/ironpython)

[Official Website](http://ironpython.net)

IronPython is an open-source implementation of the Python programming language which is tightly integrated with the .NET Framework. IronPython can use the .NET Framework and Python libraries, and other .NET languages can use Python code just as easily.

Comparison of IronPython vs. C# for 'Hello World'

c#:

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
IronPython 3 targets .NET 4.5 and .NET Core 2.0.