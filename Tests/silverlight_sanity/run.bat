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

set CHIRON_PATH=%1
if not exist %CHIRON_PATH%\chiron.exe (
	echo Cannot run this Silverlight IronPython sanity test without %CHIRON_PATH%\chiron.exe.
	exit /b 1
)

pushd %~dp0
start "Silverlight IronPython Sanity Test" /WAIT %CHIRON_PATH%\chiron.exe /b:index.html"
set ECODE=%ERRORLEVEL%
popd

echo TODO: the ERRORLEVEL of Chiron was %ECODE%.