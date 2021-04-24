# Building IronPython3

To build IronPython3 you will need the [.NET SDK (minimum v5.0.100)](https://dotnet.microsoft.com/download/visual-studio-sdks).

See [Getting the Sources](getting-the-sources.md) for information on getting the source for IronPython3.

## Building from Visual Studio

Visual Studio 2019 v16.8 or above is required to build IronPython3.

 * Open `c:\path\to\ironpython3\IronPython.sln` solution file
 * Select the configuration options (Release,Debug, etc)
 * Press Ctrl+Shift+B or F6 to build the solution

## Building from the command line

IronPython3 uses PowerShell to run the build and testing from the command line. You can either use a PowerShell directly, or prefix the commands below with `powershell` on Windows, or `pwsh` on Linux/macOS. 

On Linux/macOS you will need to install [PowerShell](https://github.com/PowerShell/PowerShell/releases)

Change the working directory to the path where you cloned the sources and run `./make.ps1`

By default, with no options, make.ps1 will build Release mode binaries. If you would like to build debug binaries, you can run `./make.ps1 debug`

Other options available for `make.ps1` are

```
-configuration (debug/release)   The configuration to build for
-platform (x86/x64)              The platform to use in running tests
-runIgnored                      Run tests that are marked as ignored in the .ini manifests
-frameworks                      A comma separated list of frameworks to run tests for 
                                 (use nomenclature as is used in msbuild files for TargetFrameworks)
```

There are also other targets available for use with packaging and testing, most come in debug and release (default) versions, such as `package-debug` and `package`

```
package                         Creates packages supported by the current platform
stage                           Stages files ready for packaging
test-*                          Runs tests from `all` categories, `ironpython` specific tests, 
                                `cpython` tests from the CPython stdlib test suite
```

If the build is successful the binaries are stored in `ironpython3/bin/{Configuration}/{TargetFramework}`.

## Run

Before run the IronPython3, you should set the environment variable `IRONPYTHONPATH` to `Src/StdLib/Lib/`.
