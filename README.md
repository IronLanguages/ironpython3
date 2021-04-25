IronPython 3
============
**There is still much that needs to be done to support Python 3.x. We are working on it, albeit slowly. We welcome all those who would like to help!**

[Official Website](http://ironpython.net)

IronPython is an open-source implementation of the Python programming language which is tightly integrated with .NET. IronPython can use .NET and Python libraries, and other .NET languages can use Python code just as easily.

IronPython 3 targets Python 3, including the re-organized standard library, Unicode strings, and all of the other new features.


| **What?** | **Where?** |
| --------: | :------------: |
| **Windows/Linux/macOS Builds** | [![Build status](https://dotnet.visualstudio.com/IronLanguages/_apis/build/status/ironpython3)](https://dotnet.visualstudio.com/IronLanguages/_build/latest?definitionId=43) [![Github build status](https://github.com/IronLanguages/ironpython3/workflows/CI/badge.svg)](https://github.com/IronLanguages/ironpython3/actions?workflow=CI) |
| **Downloads** | [![NuGet](https://img.shields.io/nuget/vpre/IronPython.svg)](https://www.nuget.org/packages/IronPython/3.4.0-alpha1) [![Release](https://img.shields.io/github/release/IronLanguages/ironpython3.svg?include_prereleases)](https://github.com/IronLanguages/ironpython3/releases/latest)|
| **Help** | [![Gitter chat](https://badges.gitter.im/IronLanguages/ironpython.svg)](https://gitter.im/IronLanguages/ironpython) [![StackExchange](https://img.shields.io/stackexchange/stackoverflow/t/ironpython.svg)](http://stackoverflow.com/questions/tagged/ironpython) |


## Examples

The following C# program:

```cs
using System.Windows.Forms;

MessageBox.Show("Hello World!", "Greetings", MessageBoxButtons.OKCancel);
```

can be written in IronPython as follows:

```py
import clr
clr.AddReference("System.Windows.Forms")
from System.Windows.Forms import MessageBox, MessageBoxButtons

MessageBox.Show("Hello World!", "Greetings", MessageBoxButtons.OKCancel)
```

Here is an example how to call Python code from a C# program.

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
This example assumes that `IronPython` has been added to the C# project as a NuGet package.

## Code of Conduct
This project has adopted the code of conduct defined by the Contributor Covenant to clarify expected behavior in our community.
For more information see the [.NET Foundation Code of Conduct](https://dotnetfoundation.org/code-of-conduct).

## State of the Project
The current target is Python 3.4, although features and behaviors from later versions may be included.

See the following lists for features from each version of CPython that have been implemented:

- [What's New In Python 3.0](WhatsNewInPython30.md)
- [What's New In Python 3.1](WhatsNewInPython31.md)
- [What's New In Python 3.2](WhatsNewInPython32.md)
- [What's New In Python 3.3](WhatsNewInPython33.md)
- [What's New In Python 3.4](WhatsNewInPython34.md)
- [What's New In Python 3.5](WhatsNewInPython35.md)

## Upgrading from IronPython 2
For details on upgrading from IronPython 2 to 3 see the [Upgrading from IronPython 2 to 3](Documentation/upgrading-from-ipy2.md) article.

## Differences with CPython
While compatibility with CPython is one of our main goals with IronPython 3, there are still some differences that may cause issues. See [Differences from CPython](Documentation/differences-from-c-python.md) for details.

## Installation
Binaries of IronPython 3 can be downloaded from the [release page](https://github.com/IronLanguages/ironpython3/releases/latest), available in various formats: `.msi`, `.zip`, `.deb`, `.pkg`. The IronPython package is also available on [NuGet](https://www.nuget.org/packages/IronPython/3.4.0-alpha1).

## Build
See the [building document](Documentation/building.md). Since the main development is on Windows, bugs on other platforms may inadvertently be introduced - please report them!

## Supported Platforms
IronPython 3 targets .NET Framework 4.6, .NET Core 2.1/3.1 and .NET 5.0. The support for .NET Core and .NET 5 will follow the lifecycle defined on [.NET Core and .NET 5 Support Policy](https://dotnet.microsoft.com/platform/support/policy/dotnet-core).
