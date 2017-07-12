@echo off
setlocal
set PATH=%PATH%;%ProgramFiles(x86)%\MSBuild\14.0\Bin\;%WINDIR%\Microsoft.NET\Framework\v4.0.30319

:getopts
if "%1"=="" (goto :default) else (goto :%1)
goto :exit

:default
goto :release

:debug
set _target=Build
set _flavour=Debug
goto :main

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
pushd bin\Release
IronPythonTest.exe --labels=All --where:Category==StandardCPython --result:smoke-net45-release-result.xml
popd
goto :exit

:test-smoke-debug
pushd bin\Debug
IronPythonTest.exe --labels=All --where:Category==StandardCPython --result:smoke-net45-debug-result.xml
popd
goto :exit

:test-ironpython
pushd bin\Release
IronPythonTest.exe --labels=All --where:Category==IronPython --result:ironpython-net45-release-result.xml
popd
goto :exit

:test-ironpython-debug
pushd bin\Release
IronPythonTest.exe --labels=All --where:Category==IronPython --result:ironpython-net45-debug-result.xml
popd
goto :exit

:test-cpython
pushd bin\Release
IronPythonTest.exe --labels=All --where:"Category==StandardCPython || Category==AllCPython" --result:cpython-net45-release-result.xml
popd
goto :exit

:test-cpython-debug
pushd bin\Debug
IronPythonTest.exe --labels=All --where:"Category==StandardCPython || Category==AllCPython" --result:cpython-net45-debug-result.xml
popd
goto :exit

:test-all
pushd bin\Release
IronPythonTest.exe --labels=All --result:all-net45-release-result.xml
popd
goto :exit

:test-all-debug
pushd bin\Debug
IronPythonTest.exe --labels=All --result:all-net45-debug-result.xml
popd
goto :exit

:test-custom
pushd bin\Release
shift
IronPythonTest.exe --labels=All --result:custom-result.xml %1 %2 %3 %4 %5 %6 %7 %8 %9
popd
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
%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\ngen.exe install bin\Release\ipy.exe
goto :exit

:main
msbuild Build.proj /t:%_target% /p:BuildFlavour=%_flavour% /verbosity:minimal /nologo /p:Platform="Any CPU"
if "%_target%" == "Build" (
    echo "Copying %_flavour% test dlls"
    xcopy /y /q Src\DLR\bin\%_flavour%\*.dll bin\%_flavour%\
    echo "Copying v4%_flavour% test dlls"
    xcopy /y /q Src\DLR\bin\v4%_flavour%\*.dll bin\v4%_flavour%\
)
goto :exit

:exit
endlocal
