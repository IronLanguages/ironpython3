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
Test\test-ipy-tc.cmd /category:Languages\IronPython\IronPython\2.X
goto :exit

:restore
set _target=RestoreReferences
set _flavour=Release
goto :main

:distclean
msbuild /t:DistClean /p:BuildFlavour=Release /verbosity:minimal /nologo
msbuild /t:DistClean /p:BuildFlavour=Debug /verbosity:minimal /nologo
goto :main

:main
msbuild Build.proj /t:%_target% /p:BuildFlavour=%_flavour% /verbosity:minimal /nologo
goto :exit

:exit
endlocal
