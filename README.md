IronPython 3
============
**There is still much that needs to be done to support Python 3.x. We are working on it, albeit slowly. We welcome all those who would like to help!**

[Official Website](https://ironpython.net)

IronPython is an open-source implementation of the Python programming language that is tightly integrated with .NET. IronPython can use .NET and Python libraries, and other .NET languages can use Python code just as easily.

IronPython 3 targets Python 3, including the re-organized standard library, Unicode strings, and all of the other new features.


| **What?** | **Where?** |
| --------: | :------------: |
| **Windows/Linux/macOS Builds** | [![Build status](https://dotnet.visualstudio.com/IronLanguages/_apis/build/status/ironpython3)](https://dotnet.visualstudio.com/IronLanguages/_build/latest?definitionId=43) [![Github build status](https://github.com/IronLanguages/ironpython3/workflows/CI/badge.svg)](https://github.com/IronLanguages/ironpython3/actions?workflow=CI) |
| **Downloads** | [![NuGet](https://img.shields.io/nuget/vpre/IronPython.svg)](https://www.nuget.org/packages/IronPython/3.4.0) [![Release](https://img.shields.io/github/release/IronLanguages/ironpython3.svg?include_prereleases)](https://github.com/IronLanguages/ironpython3/releases/latest)|
| **Help** | [![Gitter chat](https://badges.gitter.im/IronLanguages/ironpython.svg)](https://gitter.im/IronLanguages/ironpython) [![StackExchange](https://img.shields.io/badge/stack%20overflow-ironpython-informational?logo=stack-overflow&logoColor=white)](https://stackoverflow.com/questions/tagged/ironpython) |


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
- [What's New In Python 3.6](WhatsNewInPython36.md)

## Contributing

For details on contributing see the [Contributing](CONTRIBUTING.md) article.

## Upgrading from IronPython 2

For details on upgrading from IronPython 2 to 3 see the [Upgrading from IronPython 2 to 3](https://github.com/IronLanguages/ironpython3/wiki/Upgrading-from-IronPython2) article.

## Differences with CPython

While compatibility with CPython is one of our main goals with IronPython 3, there are still some differences that may cause issues. See [Differences from CPython](https://github.com/IronLanguages/ironpython3/wiki/Differences-from-CPython) for details.

## Package compatibility

See the [Package compatibility](https://github.com/IronLanguages/ironpython3/wiki/Package-compatibility) document for information on compatibility with popular packages.

## Installation

Binaries of IronPython 3 can be downloaded from the [release page](https://github.com/IronLanguages/ironpython3/releases/latest), available in various formats: `.msi`, `.zip`, `.deb`, `.pkg`. The IronPython package is also available on [NuGet](https://www.nuget.org/packages/IronPython/3.4.0). See the [installation document](https://github.com/IronLanguages/ironpython3/wiki/Installing) for detailed instructions on how to install a standalone IronPython interpreter on various operating systems and .NET frameworks.

### PowerShell

For usage in PowerShell, you can install using the Install-IronPython.ps1 within the aforementioned `.zip` file or using this one-liner:

```pwsh
$bootstrap = 'https://raw.githubusercontent.com/IronLanguages/ironpython3/main/eng/scripts/Install-IronPython.ps1'
& ([scriptblock]::Create((iwr $bootstrap).Content)) -Path "."
```

Once installed, you can start using IronPython directly in PowerShell!
- If installed without the one-liner, the following referenced files will be located at the path you provided with -Path (default is current working directory)

To use the ipy shim, you can use:
```pwsh
.\Enter-IronPythonEnvironment.ps1

ipy -c "print('Hello from IronPython!')"
```

... or to use IronPython embedded in PowerShell, you can use:
```pwsh
Import-Module ".\IronPython.dll"

& {
    [IronPython.Hosting.Python]::CreateEngine().
        CreateScriptSourceFromString("print('Hello from IronPython!')").
        Execute()
}
```

## Build

See the [building document](https://github.com/IronLanguages/ironpython3/wiki/Building). Since the main development is on Windows, bugs on other platforms may inadvertently be introduced - please report them!

## Supported Platforms

IronPython 3 targets .NET Framework 4.6.2, .NET Standard 2.0, .NET 6.0 and .NET 8.0. The support for .NET and .NET Core follow the lifecycle defined on [.NET and .NET Core Support Policy](https://dotnet.microsoft.com/platform/support/policy/dotnet-core).
