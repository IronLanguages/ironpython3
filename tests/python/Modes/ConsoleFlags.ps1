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

###################################################################################
#Tests the various combinations/permutations of executable modes
#TODO:
# * need a much better mechanism for testing -u
# * nonsense modes
# * negative modes

#HACK - see http://www.leeholmes.com/blog/WorkaroundTheOSHandlesPositionIsNotWhatFileStreamExpected.aspx
if ($Host.Version.Major -eq 1) {
	$bindingFlags = [Reflection.BindingFlags] "Instance,NonPublic,GetField" 
	$consoleHost = $host.GetType().GetField("externalHost", $bindingFlags).GetValue($host) 
	[void] $consoleHost.GetType().GetProperty("IsStandardOutputRedirected", $bindingFlags).GetValue($consoleHost, @()) 
	$field = $consoleHost.GetType().GetField("standardOutputWriter", $bindingFlags) 
	$field.SetValue($consoleHost, [Console]::Out)
	$field2 = $consoleHost.GetType().GetField("standardErrorWriter", $bindingFlags)
	$field2.SetValue($consoleHost, [Console]::Out)
}
elseif ($Host.Version.Major -eq 2) {
	$bindingFlags = [Reflection.BindingFlags] "Instance,NonPublic,GetField" 
	$objectRef = $host.GetType().GetField("externalHostRef", $bindingFlags).GetValue($host) 
	$bindingFlags = [Reflection.BindingFlags] "Instance,NonPublic,GetProperty" 
	$consoleHost = $objectRef.GetType().GetProperty("Value", $bindingFlags).GetValue($objectRef, @()) 
	[void] $consoleHost.GetType().GetProperty("IsStandardOutputRedirected", $bindingFlags).GetValue($consoleHost, @()) 
	$bindingFlags = [Reflection.BindingFlags] "Instance,NonPublic,GetField" 
	$field = $consoleHost.GetType().GetField("standardOutputWriter", $bindingFlags) 
	$field.SetValue($consoleHost, [Console]::Out)
	$field2 = $consoleHost.GetType().GetField("standardErrorWriter", $bindingFlags)
	$field2.SetValue($consoleHost, [Console]::Out)
}
#ENDHACK

$old_ErrorActionPreference = $ErrorActionPreference
$ErrorActionPreference = "stop"
$CURRPATH = split-path -parent $MyInvocation.MyCommand.Definition

#Emit the line needed to rerun this test
echo "To re-run this test execute the following line from a PowerShell prompt:"
echo     $MyInvocation.Line

#Total number of errors encountered
$global:ERRORS = 0

#Generated tests
$global:TEST_DIR = "$env:TMP\ModeTests"
$global:HELLO = "$global:TEST_DIR\hello_test.py"
$global:RECURSION = "$global:TEST_DIR\recursion_test.py"
$global:EXCEPTION = "$global:TEST_DIR\except_test.py"
$global:DOCSTRING = "$global:TEST_DIR\docstring_test.py"
$global:TRACEBACK = "$global:TEST_DIR\traceback_test.py"

#There are tests which are specifically disabled or enabled for debug/release binaries
$global:IS_DEBUG   = "$env:DLR_BIN".ToLower().EndsWith("bin\debug")
$global:IS_RELEASE = "$env:DLR_BIN".ToLower().EndsWith("bin\release")

#Some tests fail under x64
$global:IS_64 = (test-path $env:SystemRoot\SYSWOW64) -or (test-path $env:systemroot\SYSWOW64)

###################################################################################
function show-failure ()
{
	$host.ui.writeerrorline($args[0])
	$global:ERRORS++
	$host.ui.writeerrorline("    At line " + (gv -scope 0 myinvocation).value.ScriptLineNumber)
	trap { continue }
	1..3 | % { 
	$msg = "    At " + (gv -scope $_ myinvocation).value.MyCommand + ":" + (gv -scope $_ myinvocation).value.ScriptLineNumber

	$host.ui.writeerrorline($msg) }
	echo ""
}

function remove-ws {
	return $args[0].Replace(" ", "")
}

function test-setup
{
	echo "Setting up test run."
	
	#Add the temporary test directory to the path if necessary
	if ($env:IRONPYTHONPATH -eq $null)
	{
		$env:IRONPYTHONPATH="$global:TEST_DIR"
	}
	elseif (! $env:IRONPYTHONPATH.Contains($global:TEST_DIR))
	{
		$env:IRONPYTHONPATH="$env:IRONPYTHONPATH;$global:TEST_DIR"
	}
	
	#create the temporary test directory
	if (test-path $global:TEST_DIR) { rm -recurse -force $global:TEST_DIR }
	mkdir $global:TEST_DIR > $null
	
	#--Hello World
	echo "print 'Hello World 1st Line'" | out-file -encoding ascii $global:HELLO
	echo "x = 2 * 3" | out-file -encoding ascii -append $global:HELLO
	echo "print x" | out-file -encoding ascii -append $global:HELLO
	
	#--Recursion
	echo "def fact(n):" | out-file -encoding ascii $global:RECURSION
    echo "    if n==1: return 1" | out-file -encoding ascii -append $global:RECURSION
    echo "    else: return n * fact(n-1)" | out-file -encoding ascii -append $global:RECURSION
    echo "" | out-file -encoding ascii -append $global:RECURSION
    echo "import sys" | out-file -encoding ascii -append $global:RECURSION
    echo "print fact(int(sys.argv[1]))" | out-file -encoding ascii -append $global:RECURSION
	
	#--Exceptions
	echo "def hwExcept():" | out-file -encoding ascii $global:EXCEPTION
	echo "    raise Exception('Hello World Exception')" | out-file -encoding ascii -append $global:EXCEPTION
	echo "" | out-file -encoding ascii -append $global:EXCEPTION
	echo "def complexExcept():" | out-file -encoding ascii -append $global:EXCEPTION
	echo "    import System" | out-file -encoding ascii -append $global:EXCEPTION
	echo "    import exceptions" | out-file -encoding ascii -append $global:EXCEPTION
	echo "    raise exceptions.OverflowError(exceptions.RuntimeError(System.FormatException('format message', System.Exception('clr message'))))" | out-file -encoding ascii -append $global:EXCEPTION
	echo "" | out-file -encoding ascii -append $global:EXCEPTION
	
	#--Doc strings
	echo "def a():" | out-file -encoding ascii $global:DOCSTRING
	echo "    '''stuff and more stuff taking up space'''" | out-file -encoding ascii -append $global:DOCSTRING
	echo "    pass" | out-file -encoding ascii -append $global:DOCSTRING
	echo "" | out-file -encoding ascii -append $global:DOCSTRING
	echo "print a.__doc__" | out-file -encoding ascii -append $global:DOCSTRING

	#--TRACEBACK
	echo "def abc(): return 1/0" | out-file -encoding ascii $global:TRACEBACK
	echo "try:" | out-file -encoding ascii -append $global:TRACEBACK
	echo "    abc()" | out-file -encoding ascii -append $global:TRACEBACK
	echo "except:" | out-file -encoding ascii -append $global:TRACEBACK
	echo "    import sys" | out-file -encoding ascii -append $global:TRACEBACK
	echo "    if sys.exc_info()[2]==None:" | out-file -encoding ascii -append $global:TRACEBACK
	echo "        print 'No traceback'" | out-file -encoding ascii -append $global:TRACEBACK
	echo "        sys.exit(0)" | out-file -encoding ascii -append $global:TRACEBACK
	echo "    print sys.exc_info()[2].tb_lineno" | out-file -encoding ascii -append $global:TRACEBACK
	echo ""
}

###################################################################################
function hello-helper
{
	set-alias exe $args[0]

	$host.ui.write($args)
	$host.ui.write(" ")
	$host.ui.writeline($global:HELLO)
	
	$stuff = exe $args[1..$args.Length] $global:HELLO
	if (! $?) 
	{
		$host.ui.writeerrorline("Failed hello-helper (" + $args[0] + " " + $args[1..$args.Length] + " $global:HELLO)")
		show-failure $args[0] + " terminated with a non-zero exit code."		
	}
	elseif ($stuff[0] -ne "Hello World 1st Line") 
	{
		$host.ui.writeerrorline("Failed hello-helper (" + $args[0] + " " + $args[1..$args.Length] + " $global:HELLO)")
		show-failure "Missing output: Hello World 1st Line"		
	}
	elseif($stuff[1] -ne 6) 
	{
		$host.ui.writeerrorline("Failed hello-helper (" + $args[0] + " " + $args[1..$args.Length] + " $global:HELLO)")
		show-failure "Missing output: 6"		
	}
}

###################################################################################
function test-pymodes($pyexe) 
{	
	set-alias pyexe $pyexe

	echo "Testing Python modes using $pyexe"
	echo ""

	#------------------------------------------------------------------------------
	echo "The following modes are well tested by other tests and will not be covered"
	echo "by this script:"
	echo "    -c (test_stdconsole.py)"
	echo "    -x (test_stdconsole.py)"
	echo "    -V (test_stdconsole.py)"
	echo "    -i (test_interactive.py)"
	echo "    -S (test_stdconsole.py)"
	echo "    -t (test_stdconsole.py)"
	echo "    -tt (test_stdconsole.py)"
	echo "    -E (test_stdconsole.py)"
	echo "    -W (test_stdconsole.py)"
	echo ""

	#------------------------------------------------------------------------------
	## -c
	echo ""
	echo "Testing -c ..."
	
	$stuff = pyexe -c "True"
	if ($stuff -ne $null) { show-failure "Failed: $stuff";  }
	
	$stuff = pyexe -c "print True"
	if ($stuff -ne "True") { show-failure "Failed: $stuff";  }
	
	#------------------------------------------------------------------------------
	## -h
	echo ""
	echo "Testing -h ..."
	
	#Just a simple sanity check will suffice as there are differences in output
	#between CPython and IronPython
	
	$stuff = pyexe -h
	$temp_content = $stuff
	$stuff = [string]$stuff
	if ($pyexe.ToLower().EndsWith("ipy.exe")) {
		$temp_content2 = pyexe /?
		if ($stuff -ne [string]$temp_content2) {
			show-failure "'ipy -h' and 'ipy /?' output should be the same!"
		}
	}
	
	
	
	$expected_stuff = "-c cmd", "-V ", "-h "

	foreach ($expected in $expected_stuff)
	{
		if (!$stuff.Contains($expected))
		{
			show-failure "Failed: $expected"; 
		}
	}
	if ($stuff.Contains("-?"))
		{
			show-failure "Failed: should not have found '-?'"; 
		}
	
	#The following is a very strict mechanism to ensure that existing
	#ipy.exe command-line flags are not accidentally ripped out and also that
	#new -X:* flags added are indeed appropriate for public consumption.
	#The justification for this is that we've had several regressions in 
	#this area in the past (e.g., CodePlex 21691)
	if ($pyexe.ToLower().EndsWith("ipy.exe")) {
		if ($IS_DEBUG -eq $true) {
			$static_content = get-content $CURRPATH\ConsoleHelp.Debug.out
		}
		elseif ($IS_RELEASE -eq $true) {
			$static_content = get-content $CURRPATH\ConsoleHelp.Release.out
		}
		else {
			show-failure "Need Debug or Release binaries to test 'ipy -h' output!"
		}
		
		$help_compare = compare-object $static_content $temp_content
		if ($help_compare -ne $null) {
			show-failure "There are differences between the output of '$pyexe -h' and what was expected in $CURRPATH\ConsoleHelp.out:"
			echo         "    $help_compare"
			echo         "Please correct the output of '$pyexe -h' or update $CURRPATH\ConsoleHelp.out."
		}
	}
	elseif (! $pyexe.ToLower().EndsWith("python.exe")) {
		show-failure "$pyexe is an unknown Python interpreter!"
		echo "Please adapt this test case to work with $pyexe."
	}
	
	#------------------------------------------------------------------------------
	## -?
	echo ""
	echo "Testing -? ..."
	
	#Just a simple sanity check will suffice as there are differences in output
	#between CPython and IronPython
	
	$stuff = pyexe -?
	$stuff = [string]$stuff
	
	$expected_stuff = "-c cmd", "-V ", "-h "

	foreach ($expected in $expected_stuff)
	{
		if (!$stuff.Contains($expected))
		{
			show-failure "Failed: $expected"; 
		}
	}
	if ($stuff.Contains("-?"))
		{
			show-failure "Failed: should not have found '-?'"; 
		}
	
	#-? compared to -h
	$stuff_h = pyexe -h
	$stuff_q = pyexe -?
	if($stuff_h.Count -ne $stuff_q.Count) {
		show-failure "Failed: results of '-h' and '-?' should be the same."; 
	}
	
	
	#------------------------------------------------------------------------------
	## -O
	echo ""
	echo "Testing -O ..."
	
	#This flag should only affect *.pyo files which IronPython does not 
	#support.  Just make sure it doesn't break anything.
	
	#Run Hello World test w/o -O (test modes M1 and M2 both use -O flag)
	hello-helper $pyexe
	
	hello-helper $pyexe -O
	
	#------------------------------------------------------------------------------
	## -v
	echo ""
	echo "Testing -v ..."

	if(! $pyexe.Endswith("python.exe"))
	{
		hello-helper $pyexe -v 2> $null  #Send stderr to $null for CPython
	}
	else
	{
		echo "Skipping hello-helper test for CPython -v flag (python.exe emits a non-zero exit code)"
	}

	echo "CodePlex Work Item 10819"
	if($pyexe.Endswith("python.exe")) {
		$ErrorActionPreference = "continue" #The invocation below sends output to stderr
	}
	$stuff = pyexe -v -c "print 'HelloWorld'" 2>&1
	if($pyexe.Endswith("python.exe")) {
		$ErrorActionPreference = "stop"
	}
	#Just ensure there's more output than "HelloWorld"
	if ($false) #$stuff.Length -le "HelloWorld".Length) 
	{
		$stuff = $stuff.Length
		show-failure "Failed: $stuff"
		
	}
	
	
	#------------------------------------------------------------------------------
	## -u
	echo "Need verification for -u."

	#For now just ensure it does not throw
	hello-helper $pyexe -u
	
	#------------------------------------------------------------------------------
	## -OO
	echo ""
	echo "Testing -OO ..."
	
	#This flag should only affect *.pyo files which IronPython does not 
	#support.  Just make sure it doesn't break anything.
	
	hello-helper $pyexe -OO
	
	echo "CodePlex Work Item 11102"
	$stuff = pyexe -OO $global:DOCSTRING
	#if ($stuff -ne "stuff and more stuff taking up space") {show-failure "Failed: $stuff"; }

	#------------------------------------------------------------------------------
	## -Q arg
	echo ""
	echo "testing -Q ..."
	
	#check stdout first...
	$test_hash = @{ '-Qnew -c "print 3/2"' = "1.5";
					'-Qold -c "print 3/2"' = "1";
					'-Qwarn -c "print 3/2"' = "1";
					'-Qwarnall -c "print 3/2"' = "1";
					'-Q new -c "print 3/2"' = "1.5";
					'-Q old -c "print 3/2"' = "1";
					'-Q warn -c "print 3/2"' = "1";
					'-Q warnall -c "print 3/2"' = "1";
					}
	
	foreach($key in $test_hash.Keys)
	{
		$ErrorActionPreference = "continue" #The invocation below sends output to stderr
		$stuff = pyexe $key.Split(" ") 2> $null 
		$ErrorActionPreference = "stop"
		if ($stuff -ne $test_hash[$key]) {show-failure "Failed: $stuff"; }
	}
	
	#next stderr...
	echo "CodePlex Work Item 11111"
	$test_hash = @{ '-Qnew -c "print 3/2"' = "1.5";
					'-Qold -c "print 3/2"' = "1";
					#'-Qwarn -c "print 3/2"' = "1 -c:1: DeprecationWarning: classic int division";
					#'-Qwarnall -c "print 3/2"' = "1 -c:1: DeprecationWarning: classic int division";
					'-Q new -c "print 3/2"' = "1.5";
					'-Q old -c "print 3/2"' = "1";
					#'-Q warn -c "print 3/2"' = "1 -c:1: DeprecationWarning: classic int division";
					#'-Q warnall -c "print 3/2"' = "1 -c:1: DeprecationWarning: classic int division";
					}
	
	foreach($key in $test_hash.Keys)
	{
		$stuff = pyexe $key.Split(" ") 2>&1
		if ("$stuff" -ne $test_hash[$key]) {show-failure "Failed: $stuff"; }
	}
    
}
	
###############################################################################
function nodebug-helper
{
	$dlrexe = $args[0]
	
	$pyPath = $env:DLR_ROOT + "\External.LCA_RESTRICTED\Languages\IronPython\27\Lib\.*"
	$pyPath = $pyPath.Replace("\", "\\")
	echo $pyPath
	hello-helper $dlrexe "-D" "-X:NoDebug" $pyPath
}

function assembliesdir-helper
{
	$dlrexe = $args[0]
	if (! $global:IS_DEBUG) {
		$host.ui.write("-X:AssembliesDir unsupported with non-debug builds")
		return
	}
		
	if (test-path $env:TMP\assemblies_dir) { rm -recurse -force $env:TMP\assemblies_dir }
	mkdir $env:TMP\assemblies_dir > $null
	hello-helper $dlrexe "-X:AssembliesDir" $env:TMP\assemblies_dir $args[1..$args.Length] 
	$stuff = dir $env:TMP\assemblies_dir
	#Just check to make sure it's empty...save assemblies option was not used.
	if ($stuff -ne $null) {show-failure "Failed: $stuff"; }
}

function saveassemblies-helper
{
	set-alias dlrexe $args[0]
	if (! $global:IS_DEBUG) {
		$host.ui.write("-X:SaveAssemblies unsupported with non-debug builds")
		return
	}
	
	mkdir $global:TEST_DIR\SaveAssembliesTemp > $null
	pushd $global:TEST_DIR\SaveAssembliesTemp 
	
	#####
				
	hello-helper $dlrexe -D "-X:SaveAssemblies" $args[1..$args.Length]
	
	expect-files(("Snippets.debug.scripting.dll", "Snippets.debug.scripting.pdb"))

	rm -recurse -force $global:TEST_DIR\SaveAssembliesTemp\*

	#####
	
	mkdir SnippetsDir > $null
	
	hello-helper $dlrexe -D "-X:SaveAssemblies" "-X:AssembliesDir" SnippetsDir $args[1..$args.Length]
	
	cd SnippetsDir
	
	expect-files(("Snippets.debug.scripting.dll", "Snippets.debug.scripting.pdb"))

	popd
	
	rm -recurse -force $global:TEST_DIR\SaveAssembliesTemp
}

function expect-files($expected) 
{
    $actual = dir -Name
	
	foreach($file in $expected)
	{
		if (($actual | select-string $file) -eq $null) 
		{
			$host.ui.writeerrorline("Failed saveassemblies-helper!")
			show-failure "Expected '$file', but found '$files'" 
			
		}
	}
}

function not-expect-files($notExpected) 
{
    $actual = dir -Name
	
	foreach($file in $notExpected)
	{
		if (($actual | select-string $file) -ne $null) 
		{
			$host.ui.writeerrorline("Failed saveassemblies-helper!")
			show-failure "'$file' was not expected" 
			
		}
	}
}


function exceptiondetail-helper
{
	set-alias dlrexe $args[0]
	$dlrexe = $args[0]

	hello-helper $dlrexe "-X:ExceptionDetail"
	$ErrorActionPreference = "continue" #The invocation below sends output to stderr
	$stuff = dlrexe "-X:ExceptionDetail" $args[1..$args.Length] -c "from except_test import *;hwExcept()" 2>&1
	$ErrorActionPreference = "stop"
	if (! "$stuff".Contains("Hello World Exception")) {show-failure "Failed: $stuff"; }
	if (! "$stuff".Contains("at Microsoft.Scripting.")) {show-failure "Failed: $stuff"; }
	#Merlin 395056
	#if (! "$stuff".Contains(":line ")) {show-failure "Failed: $stuff"; }
	
	$ErrorActionPreference = "continue" #The invocation below sends output to stderr
	$stuff = dlrexe "-X:ExceptionDetail" $args[1..$args.Length] -c "from except_test import *;complexExcept()" 2>&1
	$ErrorActionPreference = "stop"
	$stuff = remove-ws "$stuff"
	if (! "$stuff".Contains("OverflowError:System.FormatException:formatmessage--->System.Exception:clrmessage")) 
	{
		show-failure "Failed: $stuff"; 
	}
}

function maxrecursion-helper
{
	set-alias dlrexe $args[0]
	$dlrexe = $args[0]

	#--Trivial cases w/o flag
	$stuff = dlrexe $args[1..$args.Length] $global:RECURSION 1
	if ($stuff -ne 1) {show-failure "Failed."; }
	
	$stuff = dlrexe $args[1..$args.Length] $global:RECURSION 2
	if ($stuff -ne 2) {show-failure "Failed."; }
	
	$stuff = dlrexe $args[1..$args.Length] $global:RECURSION 3
	if ($stuff -ne 6) {show-failure "Failed."; }
	
	$stuff = dlrexe $args[1..$args.Length] $global:RECURSION 20
	if ($stuff -ne 2432902008176640000L) {show-failure "Failed."; }
		
	#--Trivial cases w/ flag
	
	#non-recursive script
	foreach ($i in 10..12)
	{
		hello-helper $dlrexe "-X:MaxRecursion" $args[1..$args.Length] $i
	}
	
	#simple recursive script
	foreach($i in 13..15)
	{
		$stuff = dlrexe "-X:MaxRecursion" $i $args[1..$args.Length] $global:RECURSION 13
		if ($stuff -ne 6227020800) {show-failure "Failed."; }
	}
	
	#simple recursive script where we miss the MaxRecursion depth by one
	#which should trigger a RuntimeError exception
	foreach ($i in 10..12)
	{
		$x_param = $i + 12
		$script_param = $i + 13
		$ErrorActionPreference = "continue" #The invocation below sends output to stderr
		$stuff = dlrexe "-X:MaxRecursion" $x_param $args[1..$args.Length] $global:RECURSION $script_param 2>&1
		$ErrorActionPreference = "stop"
		$stuff = remove-ws "$stuff.ErrorDetails"
		if (!$stuff.Contains("maximumrecursiondepthexceeded")) {show-failure "Failed: $stuff"; }
	}
	
	foreach ($i in @(-2, -1, 0, 1, 8, 9))
	{
		$ErrorActionPreference = "continue" #The invocation below sends output to stderr
		$stuff = dlrexe "-X:MaxRecursion" $i $args[1..$args.Length] $global:RECURSION 3 2>&1
		$ErrorActionPreference = "stop"
		$stuff = "$stuff.ErrorDetails"
		if (!$stuff.Contains("The argument for the -X:MaxRecursion option must be an integer >= 10.")) {show-failure "Failed."; }
	}
}

function gcstress-helper
{
	set-alias dlrexe $args[0]
	$dlrexe = $args[0]

		
	#non-recursive script
	foreach ($i in 0..2)
	{
		hello-helper $dlrexe "-X:GCStress" $args[1..$args.Length] $i
	}

    $ErrorActionPreference = "continue" #The invocation below sends output to stderr
	$stuff = dlrexe "-X:GCStress" -1 $args[1..$args.Length] $global:HELLO 3 2>&1
	$ErrorActionPreference = "stop"
	
	$stuff = "$stuff.ErrorDetails"
	echo $stuff
	if (!$stuff.Contains("The argument for the -X:GCStress")) {show-failure "Failed."; }

    $ErrorActionPreference = "continue" #The invocation below sends output to stderr
	$stuff = dlrexe "-X:GCStress" 100 $args[1..$args.Length] $global:HELLO 3 2>&1
	$ErrorActionPreference = "stop"
	$stuff = "$stuff.ErrorDetails"
	if (!$stuff.Contains("The argument for the -X:GCStress")) {show-failure "Failed."; }
}

function noadaptivecompilation-helper 
{
	set-alias dlrexe $args[0]
	$dlrexe = $args[0]

	#--Sanity
	hello-helper $dlrexe "-X:NoAdaptiveCompilation"
	
	#--Make sure the interpreter is NOT used with -X:NoAdaptiveCompilation
	$ErrorActionPreference = "continue" #The invocation below sends output to stderr
	$stuff = dlrexe "-X:NoAdaptiveCompilation" "-X:ExceptionDetail" $args[1..$args.Length] -c "1/0" 2>&1
	$ErrorActionPreference = "stop"
	if("$stuff".Contains("Microsoft.Scripting.Interpreter")) {show-failure "Failed (1): $stuff"; }
	
	#--Make the sure the interpreter is used without -X:NoAdaptiveCompilation
	$ErrorActionPreference = "continue" #The invocation below sends output to stderr
	$stuff = dlrexe "-X:ExceptionDetail" $args[1..$args.Length] -c "1/0" 2>&1
	$ErrorActionPreference = "stop"
	if(! "$stuff".Contains("Microsoft.Scripting.Interpreter")) {show-failure "Failed (1): $stuff"; }
}

function compilationthreshold-helper {
	set-alias dlrexe $args[0]
	$dlrexe = $args[0]

	#--Sanity
	hello-helper $dlrexe "-X:CompilationThreshold" 1000
}

function showclrexceptions-helper
{
	set-alias dlrexe $args[0]
	$dlrexe = $args[0]

	hello-helper $dlrexe "-X:ShowClrExceptions"
	
	$ErrorActionPreference = "continue" #The invocation below sends output to stderr
	$stuff = dlrexe "-X:ShowClrExceptions" $args[1..$args.Length] -c "from except_test import *; hwExcept()" 2>&1
	$ErrorActionPreference = "stop"
	$stuff = remove-ws "$stuff"
	if(! $stuff.Contains("HelloWorldException")) {show-failure "Failed (1): $stuff"; }
	if(! $stuff.Contains("CLRException:")) {show-failure "Failed (2): $stuff"; }
	
	$ErrorActionPreference = "continue" #The invocation below sends output to stderr
	$stuff = dlrexe "-X:ShowClrExceptions" $args[1..$args.Length] -c "from except_test import *;complexExcept()" 2>&1
	$ErrorActionPreference = "stop"
	$stuff = remove-ws "$stuff"
	if (! $stuff.Contains("OverflowError:System.FormatException:formatmessage--->System.Exception:clrmessage")) 
	{
		show-failure "Failed: $stuff"; 
	}
	if (! $stuff.Contains("CLRException:")) { show-failure "Failed (3): $stuff";  }
	if (! $stuff.Contains("OverflowException")) { show-failure "Failed (4): $stuff";  }
}

function mta-helper
{
	#non-winforms apps.  test with System.Thread*
	$dlrexe = $args[0]
	
	echo "CodePlex Work Item 11014"
	#hello-helper $dlrexe "-X:MTA" $args[1..$args.Length] 
}

###################################################################################
function test-dlrmodes($dlrexe) 
{	
	set-alias dlrexe $dlrexe

	echo ""
	echo "Testing IronPython modes using $dlrexe"
	echo ""

	#------------------------------------------------------------------------------
	echo "The following modes already have sufficient coverage in other tests:"
	echo "    -X:AutoIndent (test_superconsole.py)"
	echo "    -X:LightweightScopes ('M1' RunTests.py option)"
	echo "    -X:PrivateBinding (test_privateBinding.py)"
	echo "    -X:SaveAssmeblies ('M2' RunTests.py option...need verification)"
	echo "    -X:TabCompletion (test_superconsole.py)"
	echo ""

	#------------------------------------------------------------------------------
	echo "The following modes are (or will soon be) undocumented and will not be tested:"
	echo "    -X:PassExceptions"
	echo ""
	
	#------------------------------------------------------------------------------
	echo "The following modes are or will soon be removed and will not be tested:"
	echo "    -X:ColorfulConsole (likely to be merged)"
	echo ""

	#------------------------------------------------------------------------------
	## -D
	echo "-D needs more coverage"
	
	hello-helper $dlrexe -D
	$stuff = dlrexe -c "print __debug__"
	if ($stuff -ne "True") { show-failure "Failed: $stuff";  }
	$stuff = dlrexe -D -c "print __debug__"
	if ($stuff -ne "True") { show-failure "Failed: $stuff";  }
	$stuff = dlrexe -O -c "print __debug__"
	if ($stuff -ne "False") { show-failure "Failed: $stuff";  }
	$stuff = dlrexe -O -D -c "print __debug__"
	if ($stuff -ne "False") { show-failure "Failed: $stuff";  }

	#------------------------------------------------------------------------------
	## -X:NoDebug
	echo ""
	echo "Testing -X:NoDebug..."	
    nodebug-helper $dlrexe
    
	#------------------------------------------------------------------------------
	## -X:AssembliesDir
	echo ""
	echo "Testing -X:AssembliesDir ..."	
	assembliesdir-helper $dlrexe 
	
	#------------------------------------------------------------------------------
	## -X:SaveAssemblies
	echo ""
	echo "Testing -X:SaveAssemblies ..."	
	saveassemblies-helper $dlrexe 
	
	#------------------------------------------------------------------------------
	## -X:ExceptionDetail
	echo ""
	echo "Testing -X:ExceptionDetail ..."
	exceptiondetail-helper $dlrexe
	
	#------------------------------------------------------------------------------
	## -X:MaxRecursion
	echo ""
	echo "Testing -X:MaxRecursion ..."
	maxrecursion-helper $dlrexe	
	
	#------------------------------------------------------------------------------
	## -X:GCStress
	echo ""
	echo "Testing -X:GCStress..."
	gcstress-helper $dlrexe	

	#------------------------------------------------------------------------------
	## -X:MTA
	echo ""
	echo "-X:MTA needs more coverage"
	mta-helper $dlrexe
	
	#------------------------------------------------------------------------------
	## -X:ShowClrExceptions
	echo ""
	echo "Testing -X:ShowClrExceptions ..."
	showclrexceptions-helper $dlrexe
	
	#------------------------------------------------------------------------------
	## -X:NoAdaptiveCompilation
	echo ""
	echo "Testing -X:NoAdaptiveCompilation ..."
	
	noadaptivecompilation-helper $dlrexe
	
	#------------------------------------------------------------------------------
	## -X:CompilationThreshold
	echo ""
	echo "Testing -X:CompilationThreshold ..."
	
	compilationthreshold-helper $dlrexe
}

###############################################################################
function test-relatedpy($pyexe) 
{	
	set-alias pyexe $pyexe

	echo "Testing related IronPython modes using $pyexe"
	echo ""
	
	#-X:AutoIndent, -X:ColorfulConsole, -X:TabCompletion, -t, -tt
	echo ""
	echo "Testing -X:AutoIndent, -X:ColorfulConsole, -X:TabCompletion, -X:EnabeProfiler, -t, -tt ..."
	#Just run a few sanity checks to make sure nothing breaks
	hello-helper $pyexe "-X:AutoIndent" "-X:ColorfulConsole" "-X:TabCompletion" -t -tt
	hello-helper $pyexe "-X:AutoIndent" "-X:ColorfulConsole"
	hello-helper $pyexe "-X:AutoIndent" "-X:TabCompletion"
	hello-helper $pyexe "-X:ColorfulConsole" "-X:TabCompletion"
	hello-helper $pyexe "-X:TabCompletion" -t -tt
	hello-helper $pyexe -t -tt
	hello-helper $pyexe "-X:EnableProfiler"
	
	#-X:AssembliesDir, -X:SaveAssemblies
	echo ""
	echo "-X:AssembliesDir and -X:SaveAssemblies are already well tested together."
	
	#-X:ExceptionDetail, -X:ShowClrExceptions
	echo ""
	echo "Testing -X:ExceptionDetail, -X:ShowClrExceptions ..."
	hello-helper $pyexe "-X:ExceptionDetail" "-X:ShowClrExceptions"
	exceptiondetail-helper $pyexe "-X:ShowClrExceptions"
	showclrexceptions-helper $pyexe "-X:ExceptionDetail"
	
	echo "Testing compatible IronPython modes together ..."
	if (! $global:IS_DEBUG) {
		$host.ui.write("-X:AssembliesDir unsupported with non-debug builds")
	}
	else {
		hello-helper $pyexe -O -v -u -E -OO -Qwarn -S -t -tt "-X:AutoIndent" "-X:AssembliesDir" $env:TMP "-X:ColorfulConsole" "-X:ExceptionDetail" "-X:LightweightScopes" "-X:MaxRecursion" 10 "-X:PassExceptions" "-X:SaveAssemblies" "-X:ShowClrExceptions" "-X:TabCompletion"
	}
}
	
###############################################################################
function test-nonsensemodes($pyexe) 
{	
	echo "Testing nonsense (Iron)Python modes using $exe"
	echo ""

	#------------------------------------------------------------------------------
	## 
	echo "TBD"

}
	
###############################################################################
function test-negmodes($pyexe) 
{	
	echo "Testing negative (Iron)Python modes using $exe"
	echo ""

	#---------------------------------------------------------------------------
	## 
	echo "TBD"
	
}

###############################################################################
function test-nostdlib($pyexe)
{
	echo "Testing (Iron)Python without use of the standard library"
	echo ""

	#--Nuke %IRONPYTHONPATH%
	$ORIG_IPYPATH = $env:IRONPYTHONPATH
	$env:IRONPYTHONPATH = ""
	
	#--Make sure the standard library really isn't available
	$ErrorActionPreference = "continue" #The invocation below sends output to stderr
	$output = [string](&"$pyexe" -c "import os" 2>&1)
	$ErrorActionPreference = "stop"
	
	if (! $output.EndsWith("No module named os")) {
		show-failure "Failed: $output";
	}
	
	#--Simply re-use existing tests
	test-pymodes $pyexe
	
	$env:IRONPYTHONPATH="$global:TEST_DIR"
	test-dlrmodes $pyexe
	
	#--Restore %IRONPYTHONPATH%
	$env:IRONPYTHONPATH = $ORIG_IPYPATH
}

###############################################################################
function test-regressions($pyexe)
{
	echo "Testing regressions..."
	echo ""
	set-alias pyexe $pyexe
    
	hello-helper $pyexe "-D" "-X:FullFrames" "-X:Tracing" "-X:TabCompletion"
	
	hello-helper $pyexe "-D" "-S" "-X:FullFrames" "-X:ColorfulConsole" "-X:EnableProfiler"
	pyexe "-D" "-S" "-X:FullFrames" "-X:ColorfulConsole" "-X:EnableProfiler" "-c" "import random"
	if (! $?) {
		show-failure "Failed: pyexe -D -S -X:FullFrames -X:ColorfulConsole -X:EnableProfiler -c 'import random'"
	}
}

###############################################################################


#sanity checks
if (!(test-path $env:DLR_BIN\ipy.exe)) 
{
	$host.ui.writeerrorline("DLR_BIN environment variable is not set or ipy.exe not built!")
	exit 1
}

echo "---------------------------------------------------------------------"
test-setup


$tests = @("test-pymodes", "test-dlrmodes", "test-relatedpy", "test-nonsensemodes", "test-negmodes", "test-nostdlib", "test-regressions")
foreach ($exe_test in $tests)
{
	echo "---------------------------------------------------------------------"
	&$exe_test $env:DLR_BIN\ipy.exe
	echo ""
}


#CPython is a special case
echo "---------------------------------------------------------------------"
test-pymodes $env:DLR_ROOT\External.LCA_RESTRICTED\Languages\IronPython\27\python.exe

$ErrorActionPreference = $old_ErrorActionPreference
if ($global:ERRORS -gt 0)
{
	$host.ui.writeerrorline("$global:ERRORS test(s) failed!")
	exit 1
}

echo "All tests passed."

