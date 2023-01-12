# Installing IronPython Released Binaries

IronPython can be used as a standalone interpreter, or embedded within another .NET application. For embedding, the recommended approach is to install IronPython within the target project using NuGet. This document describes various ways of installing the standalone interpreter on the target system. Once installed, IronPython is invocable from the command line as `ipy`. Use `ipy -h` for a list of available commadline options. Use `ipy -VV` to produce version information for reporting issues.

## IronPython on .NET (Core)

Since .NET is a cross-platform framework, the instructions in this section apply to all supported operating systems (Windows, Linux, macOS), unless explicitly indicated.

### .NET SDK

If the target system already has a full .NET SDK installed, the most straightforward method to install a standalone IronPython interpreter is by using [`dotnet tool`](https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools).

#### _Global Tool_

"Global" .NET tool means one installation for the current user. It is not available to other users in the system. Install IronPython with:

```
dotnet tool install --global ironpython.console
```

The switch `--global` can be abbreviated to `-g`. The `ipy` program will be installed in `~/.dotnet/tools`, together with the Python Standard Library for IronPython. The directory `~/.dotnet/tools` should be added to the search path for the user (AKA `PATH` environment variable), if not yet done.

Note that any additional Python packages installed using `ipy -m pip` from this location will be installed inside that directory, hence being "global" for the user.

#### _Project Tool_

IronPython can be installed as a tool local to an existing .NET project. Navigate to the root directory of the .NET project and make sure that the tool manifest file is already present(`./.config/dotnet-tools.json`). If not, create a new empty tool manifest with:

```
dotnet new tool-manifest
```

There is one tools manifest for all .NET tools configured for a given project, tracking all .NET tools used within the project. Consider adding `dotnet-tools.json` to the source control management system (like _git_).

Install IronPython and Python Standard Library with:

```
dotnet tool install ironpython.console
```

and invoke it with `dotnet ipy` from within any folder within the project.

**NOTE**: Any additional packages installed with `dotnet ipy -m pip` are **NOT** local to the project, but installed in a location shared by all .NET project using `ipy ` as a local tool (`~/.nuget/packages/ironpython.console`).

If the project is cloned onto another system and the tools manifest is already present and contains the reference to `ironpython.console`, all that is needed to install `ipy` locally is:

```
dotnet tool restore
```

#### _Virtual Environment_

The third way of installing IronPython as a .NET tool is to specify the installation directory explicitly:

```
dotnet tool install ironpython.console --tool-path /path/to/install/directory
```

This installs IronPython, together with the matching Python Standard Library, in a way that is independent from any other IronPython installations on the system, effectively creating an equivalent of a Python virtual environment. The target directory is created and will contain executable `ipy` (`ipy.exe` on Windows). When installed by the admin in a location accessible by all users (e.g. "C:\IronPython", or "/usr/local/bin"), it can serve as a "system-wide" installation.

Any additional packages installed with `/path/to/install/directory/ipy -m pip` are only visible for this particular installation.

### .NET Runtime

If the target system does not have .NET SDK installed, but does have a .NET Runtime installed (i.e `dotnet --version` works), a way to install IronPython is to use the zip file published on the project's [release page](https://github.com/IronLanguages/ironpython3/releases/latest).

Installations from the zip file are self-contained, so in a sense work like a Python virtual environment. For PowerShell users, the zip archive also contains script `Enter-IronPythonEnvironment.ps1` that works similarly to Anaconda's `Enter-CondaEnvironment.ps1` or CPython's `activate.ps1`. Use `help` on this file for detailed usage information.

#### _Manual_

Download and unzip `IronPython.3.X.Y.zip` (`X` and `Y` being the minor and patch version numbers). The unzipped directory contains several .NET Runtime version-specific subdirectories and a subdirectory `lib`. Pick a subdirectory that matches the .NET Runtime version installed on your system (or lower), and **move** subdirectory `lib` into that directory. IronPython can then be launched with

```
dotnet /path/to/unzipped/netXXX/ipy.dll
```

#### _Scripted_

The zip file contains a helper PowerShell script to install IronPython properly in a designated location and creates a launcher script. In this way one zip file can "seed" multiple virtual environments, each starting "blank" that is, with only the Python Standard Library present.

```
/path/to/unzipped/scripts/Install-IronPython.ps1 -Path ~/ipyenv -ZipFile ~/Downloads/IronPython.3.X.Y.zip
```

Use Powershell's `help` command on the script for information about available options and switches.

The script is also available online, so it can be downloaded and invoked without unzipping the archive first.

```
PS> Invoke-WebRequest https://raw.githubusercontent.com/IronLanguages/ironpython3/master/Src/Scripts/Install-IronPython.ps1 -OutFile ./Install-IronPython.ps1
PS> ./Install-IronPython ~/ipyenv ~/Downloads/IronPython.3.X.Y.zip
PS> ~/ipyenv/Enter-IronPythonEnvironment
«ipyenv» PS> ipy
IronPython 3.4.0 (3.4.0.1000)
[.NETCoreApp,Version=v6.0 on .NET 6.0.12 (64-bit)] on win32
Type "help", "copyright", "credits" or "license" for more information.
>>>
```

## IronPython on .NET Framework/Mono

### Windows

IronPython for .NET Framework requires .NET Framework version 4.6.2 or higher, which comes preinstalled on modern versions of Windows. To install IronPython, download the `.msi` file from the project's [release page](https://github.com/IronLanguages/ironpython3/releases/latest) and execute it. The installation of `.msi` registers IronPython in the System Registry in compliance with [PEP 514](https://peps.python.org/pep-0514/) so that other tools may detect and use it.

Alternatively, use _Chocolatey_ package manager:

```
choco install ironpython
```

### Linux (Debian-like)

On Linux, Mono provides the necessary .NET Framework. First install Mono following installation instructions from the [Mono Project page](https://www.mono-project.com/download/stable/#download-lin). After installation, verify that command `mono` is available at the shell prompt, with, e.g. `mono --version`.

Then download the `.deb` package from the project's [release page](https://github.com/IronLanguages/ironpython3/releases/latest).

Finally install the package using `dpkg`, e.g.:

```
dpkg -i ~/Downloads/ironpython_3.X.Y.deb
```

### macOS

On macOS (AKA OSX, Darwin), Mono provides the necessary .NET Framework. First install Mono following installation instructions from the [Mono Project page](https://www.mono-project.com/download/stable/#download-mac).  After installation, verify that command `mono` is available at the shell prompt, with, e.g. `mono --version`.

Then download the `.pkg` installer from the project's [release page](https://github.com/IronLanguages/ironpython3/releases/latest) and execute it.


# Installing Non-Released Versions

After a release, the development of IronPython continues so it is possible that a bug or a feature that is important to you was handled after the latest release. As each commit to the main project branch creates precompiled artifacts, it is still possible to install the relevant (or latest development) version of IronPython without the need to compile the whole project from scratch.

Go to the project's [_Actions_ page](https://github.com/IronLanguages/ironpython3/actions) and find the commit you are interested in. Or simply find the topmost commit to `master` that has all tests passing. The _Status_ and _Branch_ filters in the top bar are helpful to narrow the list down. Then click on the commit hyperlink to access the CI run summary. At the bottom of that page there is artifact `packages`, which contains all binary artifacts the project produces. Download it and unzip. Choose the right package for your needs and follow instructions above for the officially released artifacts. For convenience, here is a table with usable packages:

| Artifact             | Framework                      | Operating System                    |
| -------------------- | ------------------------------ | ----------------------------------- |
| IronPython.3.X.Y.zip | all supported                  | all supported                       |
| IronPython-3.X.Y.msi | .NET Framework 4.6.2 or higher | Windows                             |
| ironpython_3.X.Y.deb | Mono 6.12 or higher            | Linux (Debian, Ubuntu, and similar) |
| IronPython-3.X.Y.pkg | Mono 6.12 or higher            | macOS                               |


# Installing from Sources

To build and install IronPython from sources, first follow instructions in [_Getting the Sources_](https://github.com/IronLanguages/ironpython3/blob/master/Documentation/getting-the-sources.md) and [_Building IronPython3_](https://github.com/IronLanguages/ironpython3/blob/master/Documentation/building.md).

When the command `./make.ps1 debug` completes successfully, runnable and usable `ipy` executables are available in subdirectories of `./bin/Debug`. To run executables from the release configuration (produced by a successful run of `./make.ps1`), first set environment variable `IRONPYTHONPATH`.

If those executables test out successfully, the binaries can be installed outside the project directory, or on another system. Create the installation artifacts with:

```
./make.ps1 package
```

The artifacts are placed in directory `./Package/Release/Packages/IronPython-3.X.Y`. Pick a package suitable for your installation target and follow instructions above for the officially released packages.

Note: as a convenience, if you run `Install-IronPython.ps1` directly from directory `./Src/Scripts` to install IronPython from the zip file, there is no need to pass the location to the zip file; the script finds it automatically using the relative path. 

Installation example:

```
./Src/Scripts/Install-IronPython.ps1 /path/to/install/directory -framework net462
```
