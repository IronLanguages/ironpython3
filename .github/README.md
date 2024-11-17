<p align="center">
  <img alt="IronPython Console" src="https://github.com/user-attachments/assets/ea038aa3-36fa-42c3-b72f-a79ae06fdada" />
</p>

<p align="center">
  <a style="text-decoration:none" href="https://ironpython.net/">
    <img src="https://img.shields.io/badge/IronPython-Website-darkgreen" alt="IronPython Website" /></a>
  <a style="text-decoration:none" href="https://github.com/IronLanguages/ironpython3/actions?workflow=CI">
    <img src="https://github.com/IronLanguages/ironpython3/workflows/CI/badge.svg" alt="GitHub CI Status" /></a>
  <a style="text-decoration:none" href="https://dotnet.visualstudio.com/IronLanguages/_build/latest?definitionId=43">
    <img src="https://dotnet.visualstudio.com/IronLanguages/_apis/build/status/ironpython3" alt="Azure CI Status" /></a>
  <a style="text-decoration:none" href="https://gitter.im/IronLanguages/ironpython">
    <img src="https://badges.gitter.im/IronLanguages/ironpython.svg" alt="Gitter" /></a>
</p>

**IronPython** is a popular, open-source implementation of Python 3.x for .NET that is built on top of its very own [Dynamic Language Runtime](https://github.com/IronLanguages/dlr).

> [!NOTE]
> There is still much that needs to be done to support Python 3. We are working on it, albeit slowly.
> We welcome all those who would like to help! ❤️

IronPython is an open-source implementation of the Python programming language that is tightly integrated with .NET. IronPython can use .NET and Python libraries, and other .NET languages can use Python code just as easily.

IronPython 3 targets Python 3, including the re-organized standard library, Unicode strings, and all of the other new features.

## ✍️ Examples

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

> This example assumes that `IronPython` has been added to the C# project as a NuGet package.

## ⚖️ Code of Conduct
This project has adopted the code of conduct defined by the Contributor Covenant to clarify expected behavior in our community.
For more information see the [.NET Foundation Code of Conduct](https://dotnetfoundation.org/code-of-conduct).

## 🐍 State of the Project

The current target is Python 3.4, although features and behaviors from later versions may be included.

See the following lists for features from each version of CPython that have been implemented:

- [`Python3.0`](https://github.com/IronLanguages/ironpython3/wiki/WhatsNew%E2%80%90Python3.0)
- [`Python3.1`](https://github.com/IronLanguages/ironpython3/wiki/WhatsNew%E2%80%90Python3.1)
- [`Python3.2`](https://github.com/IronLanguages/ironpython3/wiki/WhatsNew%E2%80%90Python3.2)
- [`Python3.3`](https://github.com/IronLanguages/ironpython3/wiki/WhatsNew%E2%80%90Python3.3)
- [`Python3.4`](https://github.com/IronLanguages/ironpython3/wiki/WhatsNew%E2%80%90Python3.4)
- [`Python3.5`](https://github.com/IronLanguages/ironpython3/wiki/WhatsNew%E2%80%90Python3.5)
- [`Python3.6`](https://github.com/IronLanguages/ironpython3/wiki/WhatsNew%E2%80%90Python3.6)

## 🙋 Contributing
For details on contributing see the [Contributing](CONTRIBUTING.md) article.

## ⬆️ Upgrading from IronPython 2
For details on upgrading from IronPython 2 to 3 see the [Upgrading from IronPython 2 to 3](https://github.com/IronLanguages/ironpython3/wiki/Upgrading-from-IronPython2) article.

## 🧭 Differences with CPython
While compatibility with CPython is one of our main goals with IronPython 3, there are still some differences that may cause issues. See [Differences from CPython](https://github.com/IronLanguages/ironpython3/wiki/Differences-from-CPython) for details.

## 📦 Package compatibility
See the [Package compatibility](https://github.com/IronLanguages/ironpython3/wiki/Package-compatibility) document for information on compatibility with popular packages.

## 🎁 Installation
Binaries of IronPython 3 can be downloaded from the [release page](https://github.com/IronLanguages/ironpython3/releases/latest), available in various formats: `.msi`, `.zip`, `.deb`, `.pkg`. The IronPython package is also available on [NuGet](https://www.nuget.org/packages/IronPython/3.4.0). See the [installation document](https://github.com/IronLanguages/ironpython3/wiki/Installing) for detailed instructions on how to install a standalone IronPython interpreter on various operating systems and .NET frameworks.

## 🛠️ Build
See the [building document](https://github.com/IronLanguages/ironpython3/wiki/Building). Since the main development is on Windows, bugs on other platforms may inadvertently be introduced - please report them!

## Supported Platforms
IronPython 3 targets .NET Framework 4.6.2, .NET Standard 2.0 and .NET 6.0. The support for .NET and .NET Core follow the lifecycle defined on [.NET and .NET Core Support Policy](https://dotnet.microsoft.com/platform/support/policy/dotnet-core).
