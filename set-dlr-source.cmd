@echo off
setlocal

if [%1]==[] goto usage

set _src_name=dlr-dev

set _root=%~dp0
set _nuget=%_root%Util\NuGet\nuget.exe
set _nuget_config=%_root%NuGet.config

set _dlr_root=%~dpn1

:: Should probably allow switching between Debug and Release
set _dlr_src=%_dlr_root%\Package\Release\dlr-1.2.0-alpha0

if exist "%_nuget_config%" (call :check-config-size %_nuget_config%) else (call :fill-config)

:action
"%_nuget%" sources list -ConfigFile "%_nuget_config%" | findstr "%_src_name%" > NUL
if %ERRORLEVEL% equ 0 (set _action=update) else (set _action=add)

:config
"%_nuget%" sources %_action% -Name "%_src_name%" -Source "%_dlr_src%" -ConfigFile "%_nuget_config%"
goto exit

:check-config-size
if %~z1 equ 0 (call :fill-config)
goto:eof

:fill-config
echo foo
echo ^<?xml version="1.0" encoding="utf-8"?^> > "%_nuget_config%"
echo ^<configuration^> >> "%_nuget_config%"
echo ^</configuration^> >> "%_nuget_config%"
goto:eof

:usage
echo %~n0 ^<path^\to^\dlr^>
exit /B 1

:exit
endlocal
