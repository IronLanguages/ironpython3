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

## 🎁 Installation

Binaries of IronPython 3 can be downloaded from the [release page](https://github.com/IronLanguages/ironpython3/releases/latest), available in various formats: `.msi`, `.zip`, `.deb`, `.pkg`. The IronPython package is also available on [NuGet](https://www.nuget.org/packages/IronPython). See the [installation article](https://github.com/IronLanguages/ironpython3/wiki/Installing) for detailed instructions on how to install a standalone IronPython interpreter on various operating systems and .NET frameworks.

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

More examples and documentation on how to use IronPython outside the interactive console can be found [here](https://github.com/IronLanguages/ironpython3/wiki/Using-IronPython).

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

Want to contribute to this project? Let us know with an [issue](https://github.com/IronLanguages/IronPython3/issues) that communicates your intent to create a [pull request](https://github.com/IronLanguages/IronPython3/pulls). Also, view our [contributing guidelines](CONTRIBUTING.md) to make sure you're up to date on the coding conventions.
For more details on contributing see the [Contributing](CONTRIBUTING.md) article.

#### 🫡 Code of Conduct
This project has adopted the code of conduct defined by the Contributor Covenant to clarify expected behavior in our community.
For more information see the [.NET Foundation Code of Conduct](https://dotnetfoundation.org/code-of-conduct).

### 🛠️ Building from source

See the article on [building from source](https://github.com/IronLanguages/ironpython3/wiki/Building). Since the main development is on Windows, bugs on other platforms may inadvertently be introduced - please report them!

### ⚖️ License

This project is licensed under the Apache-2.0 License as stated in the [`LICENSE`](https://github.com/IronLanguages/ironpython3/blob/56d1799/LICENSE).
> Copyright (c) .NET Foundation and Contributors.

### 📝 Wiki

The documentation and guides for IronPython are available [on the wiki](https://github.com/IronLanguages/ironpython3/wiki). Enjoy!
