# IronPython 3
[Official Website](http://ironpython.net)

IronPython is an open-source implementation of the Python programming language which
is tightly integrated with the .NET Framework. IronPython can use the .NET Framework
and Python libraries, and other .NET languages can use Python code just as easily.

IronPython 3 targets Python 3, including the re-organized standard library, Unicode
strings, and all of the other new features.

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
IronPython 3 currently builds for .NET 3.5 SP1, .NET 4.0, and .NET 4.5. The main
platform will be .NET 4.5, but .NET 4.0, 3.5, and Silverlight 5 will still be supported
for embedding.

Support for Android, Windows 8 Store Apps (Metro), Window Phone 8, and iOS are also
planned (in roughly that order).

## Custom DLR
If you need to make changes to the DLR, you can point IronPython at your local DLR using
`set-dlr-source`:

    set-dlr-source ..\dlr

(Windows)

    ./set-dlr-source.sh ../dlr

(Unix)

After making DLR changes, commit them, update the version, and release an updated NuGet.
Then, update the `DlrVersion` property in `CurrentVersion.props`.
