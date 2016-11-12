@echo off
setlocal
set PATH=%PATH%;%WINDIR%\Microsoft.NET\Framework\v4.0.30319;%WINDIR%\Microsoft.NET\Framework\v3.5

:getopts
if "%1"=="" (goto :default) else (goto :%1)
goto :exit

:default
goto :debug

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
pushd bin\v2Debug
IronPythonTest.exe -include:StandardCPython -result:smoke-result-net35.xml
popd
pushd bin\Debug
IronPythonTest.exe -include:StandardCPython -result:smoke-result-net40.xml
popd
pushd bin\v45Debug
IronPythonTest.exe -include:StandardCPython -result:smoke-result-net45.xml
popd
goto :exit

:test-ironpython
pushd bin\Debug
IronPythonTest.exe -include:IronPython -result:ironpython-result.xml
popd
goto :exit

:test-cpython
pushd bin\Debug
IronPythonTest.exe -include:StandardCPython,AllCPython -result:cpython-result.xml
popd
goto :exit

:test-all
pushd bin\Debug
IronPythonTest.exe -result:all-result.xml
popd
goto :exit

:test-custom
pushd bin\Debug
shift
IronPythonTest.exe -result:custom-result.xml %1 %2 %3 %4 %5 %6 %7 %8 %9
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

:main
msbuild Build.proj /t:%_target% /p:BuildFlavour=%_flavour% /verbosity:minimal /nologo /p:Platform="Any CPU"
goto :exit

:exit
endlocal
