@echo off
setlocal

set _VSWHERE="%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if exist %_VSWHERE% (
  for /f "usebackq tokens=*" %%i in (`%_VSWHERE% -latest -requires Microsoft.Component.MSBuild -property installationPath`) do (
    set _VSINSTPATH=%%i
  )
)
if not exist "%_VSINSTPATH%" (
   echo Error: Visual Studio 2017 15.2 or later is required.
   exit /b 1
)

if exist "%_VSINSTPATH%\MSBuild\15.0\Bin\MSBuild.exe" (
  set "PATH=%PATH%;%_VSINSTPATH%\MSBuild\15.0\Bin\"
)

set FRAMEWORKS=net45

set CONSOLERUNNER="..\..\..\packages\nunit.consolerunner\3.7.0\tools\nunit3-console.exe"

:getopts
if "%1"=="" (goto :default) else (goto :%1)
goto :exit

:default
goto :release

:dotnet
goto :dotnet-release

:debug
set _target=Build
set _flavour=Debug
goto :main

:dotnet-debug
set _target=Build
set _flavour=Debug
goto :dotnet-main

:clean-debug
set _target=Clean
set _flavour=Debug
goto :main

:stage-debug
set _target=Stage
set _flavour=Debug
goto :main

:release
set _target=Build
set _flavour=Release
goto :main

:dotnet-release
set _target=Build
set _flavour=Release
goto :dotnet-main

:clean-release
set _target=Clean
set _flavour=Release
goto :main

:stage-release
set _target=Stage
set _flavour=Release
goto :main

:package-release
set _target=Package
set _flavour=Release
goto :main

:clean
echo No target 'clean'. Try 'clean-debug' or 'clean-release'.
goto :exit

:stage
echo No target 'stage'. Try 'stage-debug' or 'stage-release'.
goto :exit

:package
echo No target 'package'. Try 'package-release'.
goto :exit

:test
echo No target 'test'. Try 'test-smoke', 'test-ironpython', 'test-cpython', or 'test-all'.
goto :exit

:test-smoke
for %%f in ("%FRAMEWORKS:,=" "%") do (
  pushd bin\Release\%%f
  %CONSOLERUNNER% --labels=All --where:"Category==StandardCPython" --result:smoke-%%f-release-result.xml IronPythonTest.dll
  popd
)
goto :exit

:test-smoke-debug
for %%f in ("%FRAMEWORKS:,=" "%") do (
  pushd bin\Debug\%%f
  %CONSOLERUNNER% --labels=All --where:"Category==StandardCPython" --result:smoke-%%f-debug-result.xml IronPythonTest.dll
  popd
)
goto :exit

:test-ironpython
for %%f in ("%FRAMEWORKS:,=" "%") do (
  pushd bin\Release\%%f
  %CONSOLERUNNER% --labels=All --where:"Category==IronPython" --result:ironpython-%%f-release-result.xml IronPythonTest.dll
  popd
)
goto :exit

:test-ironpython-debug
for %%f in ("%FRAMEWORKS:,=" "%") do (
  pushd bin\Debug\%%f
  %CONSOLERUNNER% --labels=All --where:"Category==IronPython" --result:ironpython-%%f-debug-result.xml IronPythonTest.dll
  popd
)
goto :exit

:test-cpython
for %%f in ("%FRAMEWORKS:,=" "%") do (
  pushd bin\Release\%%f
  %CONSOLERUNNER% --labels=All --where:"Category==StandardCPython || Category==AllCPython" --result:cpython-%%f-release-result.xml IronPythonTest.dll
  popd
)
goto :exit

:test-cpython-debug
for %%f in ("%FRAMEWORKS:,=" "%") do (
  pushd bin\Debug\%%f
  %CONSOLERUNNER% --labels=All --where:"Category==StandardCPython || Category==AllCPython" --result:cpython-%%f-debug-result.xml IronPythonTest.dll
  popd
)
goto :exit

:test-all
for %%f in ("%FRAMEWORKS:,=" "%") do (
  pushd bin\Release\%%f
  %CONSOLERUNNER% --labels=All --result:all-%%f-release-result.xml IronPythonTest.dll
  popd
)
goto :exit

:test-all-debug
for %%f in ("%FRAMEWORKS:,=" "%") do (
  pushd bin\Debug\%%f
  %CONSOLERUNNER% --labels=All --result:all-%%f-debug-result.xml IronPythonTest.dll
  popd
)
goto :exit

:test-custom
pushd bin\Release
shift
net45\IronPythonTest.exe --labels=All --result:custom-result.xml %1 %2 %3 %4 %5 %6 %7 %8 %9
popd
goto :exit

:test-netcore-ironpython
FOR /F "delims=" %%F IN ("bin\Release\netcoreapp2.0") DO SET "BinDir=%%~fF"
dotnet test .\Src\IronPythonTest\IronPythonTest.csproj --framework netcoreapp2.0 -o %BinDir% --configuration Release --no-build --filter "TestCategory=IronPython"
goto :exit

:test-netcore-cpython
FOR /F "delims=" %%F IN ("bin\Release\netcoreapp2.0") DO SET "BinDir=%%~fF"
dotnet test .\Src\IronPythonTest\IronPythonTest.csproj --framework netcoreapp2.0 -o %BinDir% --configuration Release --no-build --filter "TestCategory=StandardCPython | TestCategory=AllCPython"
goto :exit

:test-netcore-all
FOR /F "delims=" %%F IN ("bin\Release\netcoreapp2.0") DO SET "BinDir=%%~fF"
dotnet test .\Src\IronPythonTest\IronPythonTest.csproj --framework netcoreapp2.0 -o %BinDir% --configuration Release --no-build
goto :exit

:restore
set _target=RestoreReferences
set _flavour=Release
goto :main

:distclean
msbuild /t:DistClean /p:BuildFlavour=Release /verbosity:minimal /nologo /p:Platform="Any CPU"
msbuild /t:DistClean /p:BuildFlavour=Debug /verbosity:minimal /nologo /p:Platform="Any CPU"
goto :main

:ngen
%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\ngen.exe install bin\Release\net45\ipy.exe
goto :exit

:main
msbuild Build.proj /t:%_target% /p:BuildFlavour=%_flavour% /verbosity:minimal /nologo /p:Platform="Any CPU" /bl:build-%_flavour%.binlog
goto :exit

:exit
endlocal
