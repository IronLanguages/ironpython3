@ECHO OFF
REM ---------------------------------------------------------------------------
REM  Copyright (c) Microsoft Corporation. All rights reserved.
REM
REM This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
REM copy of the license can be found in the License.html file at the root of this distribution. If 
REM you cannot locate the  Apache License, Version 2.0, please send an email to 
REM ironpy@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
REM by the terms of the Apache License, Version 2.0.
REM
REM You must not remove this notice, or any other, from this software.
REM ---------------------------------------------------------------------------
REM
REM This is a test runner for dynamic_sanity.cs.  To run this:
REM  1.  Copy the entire contents of the dynamic_sanity directory into
REM      the install location of IronPython.msi.  You need the CPython standard
REM      library available for this test to pass.
REM  2.  Call "dynamic_runner.bat v4.0.20506" where "v4.0.20506" is the new 
REM      version of the CLR supporting the 'dynamic' keyword being tested.
REM  3.  Normal output of this script should look similar to:
REM          C:\Program Files\IronPython 2.6 CTP for .NET 4.0 Beta 1>dynamic_runner.bat v4.0.20506
REM          Microsoft (R) Visual C# 2010 Compiler version 4.0.20506.1
REM          Copyright (C) Microsoft Corporation. All rights reserved.
REM          
REM          Hello 3.14159265359
REM ---------------------------------------------------------------------------

set FRAMEWORKVER=%1
set FRAMEWORKDIR=%windir%\Microsoft.NET\Framework\%FRAMEWORKVER%

REM ---------------------------------------------------------------------------
REM Sanity checks
if "%FRAMEWORKVER%" == ""  (
	echo The DotNet Framework version is a required parameter to this script.
	exit /b 1
)
if not exist "%FRAMEWORKDIR%\csc.exe" (
	echo Cannot run this test without %FRAMEWORKDIR%\csc.exe.
	exit /b 1
)

REM ---------------------------------------------------------------------------
REM Build the test
%FRAMEWORKDIR%\csc.exe /r:IronPython.dll,Microsoft.Scripting.dll dynamic_sanity.cs
if not "%ERRORLEVEL%" == "0" (
	echo Failed to build the dynamic sanity test.
	exit /b 1
)

if not exist "%CD%\dynamic_sanity.exe" (
	echo Failed to generate dynamic_sanity.exe
	exit /b 1
)

REM ---------------------------------------------------------------------------
REM Run the test
dynamic_sanity.exe
if not "%ERRORLEVEL%" == "0" (
	echo Failed to run dynamic_sanity.exe.
	exit /b 1
)

REM Verify that the output is sane
dynamic_sanity.exe | findstr Hello > NUL 2>&1
if not "%ERRORLEVEL%" == "0" (
	echo Failed to detect the string Hello in the output.
	echo Test failed.
	exit /b 1
)
