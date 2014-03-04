#####################################################################################
#
#  Copyright (c) Microsoft Corporation. All rights reserved.
#
# This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
# copy of the license can be found in the License.html file at the root of this distribution. If 
# you cannot locate the  Apache License, Version 2.0, please send an email to 
# ironpy@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
# by the terms of the Apache License, Version 2.0.
#
# You must not remove this notice, or any other, from this software.
#
#
#####################################################################################

#USAGE: powershell %CD%\test_pyc.ps1 ..\..\Public\Tools\Scripts


#------------------------------------------------------------------------------
#--Sanity
if ($env:DLR_BIN -eq $null) {
    echo "Cannot run this test without DLR_BIN being set!"
    exit 1
}
if (! (test-path $env:DLR_BIN\ipy.exe)) {
    echo "Cannot run this test without ipy.exe being built!"
    exit 1
}

if ([System.Environment]::Version.Major -eq 4) {
    echo "http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=24497"
    exit 0
}

#------------------------------------------------------------------------------
#--Prereqs and globals

set-alias IPY_CMD $env:DLR_BIN\ipy.exe 
$CURRPATH = split-path -parent $MyInvocation.MyCommand.Definition
$TOOLSPATH = $args[0]
if (! (test-path "$TOOLSPATH\pyc.py")) {
	echo "Cannot run this test without access to $TOOLSPATH\pyc.py!"
    exit 1
}
$FINALEXIT = 0  #We'll use this value for the final exit code of this script
$SLEEP_TIME = 3
pushd $TOOLSPATH #We'll invoke Pyc from Tools\Scripts


function prepare-testcase {
    #Kill off any stagnant test processes
    foreach ($x in @("winforms_hw")) {
        stop-process -name $x 2> $null
        sleep $SLEEP_TIME
        $proc_list = @(get-process | where-object { $_.ProcessName -match "^$x" })
        if ($proc_list.Length -ne 0)  {
            echo "FAILED: $x is currently running - $proc_list"
            $FINALEXIT = 1
        }    
    }
    
    #Remove existing crud
    rm -force *.dll
    rm -force *.exe

    #Copy over prereqs for running pyc.py
    cp -force $env:DLR_BIN\IronPython*dll .
    cp -force $env:DLR_BIN\Microsoft.*dll .
}


function testcase-helper ($cmd, $expected_files, $exe, $expected_exe_exitcode, $expected_exe_stdout, $arguments) {
    prepare-testcase
    echo "------------------------------------------------------------------------------"
    echo "TEST CASE: $cmd"

    #--Run pyc.py
    $cmd_array = $cmd.split(" ")
    IPY_CMD pyc.py $cmd_array
    if (! $?) {
        echo "FAILED: IPY_CMD pyc.py $cmd"
        $FINALEXIT = 1
        return
    }
    
    #--Ensure pyc.py generated all expected files
    foreach($x in $expected_files) {
        if (! (test-path $PWD\$x)) {
            echo "FAILED: $PWD\$x was not generated!"
            $FINALEXIT = 1
            return
        }
    }

    #--Ensure the exe we will run has been generated
    if (! (test-path $PWD\$exe)) {
        echo "FAILED: $PWD\$exe was not generated!"
        $FINALEXIT = 1
        return
    }
 
    testcase-runner $cmd $exe $expected_exe_exitcode $expected_exe_stdout $arguments
}

function testcase-runner ($cmd, $exe, $expected_exe_exitcode, $expected_exe_stdout, $arguments) {    
    #--Run the generated exe
    $command = Get-Command "$PWD\$exe"
    echo $arguments
    $exe_out = &$command $arguments
    if ($LASTEXITCODE -ne $expected_exe_exitcode) {
        echo "FAILED: actual exit code, $LASTEXITCODE, of $exe was not the expected value ($expected_exe_exitcode)"
        $FINALEXIT = 1
        return
    }
    
    #--Consoleless applications are a special case
    if ($cmd.Contains("/target:winexe")) {
        if ($exe_out -ne $null) {
            echo "FAILED: there should be no console output instead of '$exe_out' for $exe."
            $FINALEXIT = 1
            return
        }
        #The real expected exe stdout is hidden in the MainWindowTitle
        $temp_name = $exe.Split(".exe")[0]
        sleep $SLEEP_TIME
        $exe_out = (get-process | where-object { $_.ProcessName -match "^$temp_name" }).MainWindowTitle
        stop-process -name $temp_name 2> $null
    }
    
    if ($exe_out -ne $expected_exe_stdout) {
        echo "FAILED: '$exe_out' was not the expected output ($expected_exe_stdout) for $exe."
        $FINALEXIT = 1
        return
    }

    echo ""
    echo "PASSED"
    echo ""
}


#------------------------------------------------------------------------------
#--Test cases

#--Console HelloWorld
testcase-helper "/main:$CURRPATH\console_hw.py" @("console_hw.exe", "console_hw.dll") "console_hw.exe" 0 "(Compiled) Hello World"

#--Console HelloWorld with a single dependency
testcase-helper "$CURRPATH\other_hw.py /main:$CURRPATH\console_hw.py" @("console_hw.exe", "console_hw.dll") "console_hw.exe" 0 "(Compiled) Hello World"

#--Console HelloWorld w/ args
testcase-helper "/main:$CURRPATH\console_hw_args.py" @("console_hw_args.exe", "console_hw_args.dll") "console_hw_args.exe" 0 "(Compiled) Hello World ['foo']" "foo"

#--WinForms HelloWorld
testcase-helper "/main:$CURRPATH\winforms_hw.py /target:winexe" @("winforms_hw.exe", "winforms_hw.dll") "winforms_hw.exe" 0 "(Compiled WinForms) Hello World"

#--Console Package
testcase-helper "/main:$CURRPATH\pycpkgtest.py $CURRPATH\pkg\a.py $CURRPATH\pkg\b.py $CURRPATH\pkg\__init__.py " @("pycpkgtest.exe", "pycpkgtest.dll") "pycpkgtest.exe" 0 "<module 'pkg.b' from 'pkg\b'>"

#--CPython standard library is a very special case
function test-stdcpymodule {
	echo "Generating a precompiled module for the entire CPython standard library..."
	. $CURRPATH\stdmodules_ok.ps1

	#Generate the DLL
	pushd "$env:DLR_ROOT\External.LCA_RESTRICTED\Languages\CPython\27\lib"
	IPY_CMD -X:Frames $TOOLSPATH\Pyc.py /out:cpystdlib /target:dll $STDMODULES
	if (! $?) {
		echo "Failed to run Pyc against the CPython standard library!"
		$FINALEXIT = 1
	}
	if (test-path "$PWD\cpystdlib.dll") {
		copy -force "$PWD\cpystdlib.dll" $TOOLSPATH\
		rm -force "$PWD\cpystdlib.dll"
	}
	else {
		echo "Failed to generate the '$PWD\cpystdlib.dll' precompiled module!"
		$FINALEXIT = 1
	}
	popd

	#Minimal verification
	$TEMP_OUT = IPY_CMD -s -E -c "import os" 2>&1
	if ($?) {
		echo "Should not be able to import os w/o the standard library!"
		echo $TEMP_OUT
		$FINALEXIT = 1
	}

	$TEMP_OUT = IPY_CMD -s -E -c "import clr;clr.AddReference('cpystdlib.dll');import os;print os.__name__"
	if (! $?) {
		echo "Should be able to import os w/ precompiled standard library!"
		echo $TEMP_OUT
		$FINALEXIT = 1
	}
	if (! $TEMP_OUT.Contains("os")) {
		echo "'print os.__name__' resulted in unexpected output!"
		echo $TEMP_OUT
		$FINALEXIT = 1
	}
}
#http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=26593
#test-stdcpymodule

#------------------------------------------------------------------------------
#Cleanup and exit
rm -force *.dll
rm -force *.exe
popd

exit $FINALEXIT