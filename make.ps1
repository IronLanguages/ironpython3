#!/usr/bin/pwsh
[CmdletBinding()]
Param(
    [Parameter(Position=1)]
    [String] $target = "release",
    [String] $configuration = "Release",
    [String[]] $frameworks=@('net45','netcoreapp2.0')
)

[int] $global:Result = 0
[bool] $global:isUnix = [System.Environment]::OSVersion.Platform -eq [System.PlatformID]::Unix

$_BASEDIR = Split-Path -Path $MyInvocation.MyCommand.Definition -Parent

if(!$global:isUnix) {
    $_VSWHERE = [System.IO.Path]::Combine(${env:ProgramFiles(x86)}, 'Microsoft Visual Studio\Installer\vswhere.exe')
    $_VSINSTPATH = ''

    if([System.IO.File]::Exists($_VSWHERE)) {
        $_VSINSTPATH = & "$_VSWHERE" -latest -requires Microsoft.Component.MSBuild -property installationPath
    } else {
        Write-Error "Visual Studio 2017 15.2 or later is required"
        Exit 1
    }

    if([System.IO.File]::Exists([System.IO.Path]::Combine($_VSINSTPATH, 'MSBuild\15.0\Bin\MSBuild.exe'))) {
        $env:PATH = [String]::Join(';', $env:PATH, [System.IO.Path]::Combine($_VSINSTPATH, 'MSBuild\15.0\Bin'))
    }
}

# Details for running tests on the various frameworks
$_FRAMEWORKS = @{
    "net45" = @{
        "runner" = "dotnet";
        "args" = @('test', '__BASEDIR__/Src/IronPythonTest/IronPythonTest.csproj', '-f', '__FRAMEWORK__', '-o', '__BASEDIR__/bin/__CONFIGURATION__/__FRAMEWORK__', '-c', '__CONFIGURATION__', '--no-build', '-l', "trx;LogFileName=__FILTERNAME__-__FRAMEWORK__-__CONFIGURATION__-result.trx", '-s', 'runsettings.__FRAMEWORK__');
        "filterArg" = '--filter="__FILTER__"';
        "filters" = @{
            "all" = "";
            "smoke" = "TestCategory=StandardCPython";
            "cpython" = "TestCategory=StandardCPython | TestCategory=AllCPython";
            "ironpython" = "TestCategory=IronPython";
            "single" = "Name=__TESTNAME__";
        }
    };
    "netcoreapp2.0" = @{
        "runner" = "dotnet";
        "args" = @('test', '__BASEDIR__/Src/IronPythonTest/IronPythonTest.csproj', '-f', '__FRAMEWORK__', '-o', '__BASEDIR__/bin/__CONFIGURATION__/__FRAMEWORK__', '-c', '__CONFIGURATION__', '--no-build', '-l', "trx;LogFileName=__FILTERNAME__-__FRAMEWORK__-__CONFIGURATION__-result.trx", '-s', 'runsettings.__FRAMEWORK__');
        "filterArg" = '--filter="__FILTER__"';
        "filters" = @{
            "all" = "";
            "smoke" = "TestCategory=StandardCPython";
            "cpython" = "TestCategory=StandardCPython | TestCategory=AllCPython";
            "ironpython" = "TestCategory=IronPython";
            "single" = "Name=__TESTNAME__";
        }
    }
}

function Main([String] $target, [String] $configuration) {
    # verify that the DLR submodule has been initialized
    if(![System.Linq.Enumerable]::Any([System.IO.Directory]::EnumerateFileSystemEntries([System.IO.Path]::Combine($_BASEDIR, "Src/DLR")))) {
        if(Get-Command git -ErrorAction SilentlyContinue) {
            & git submodule update --init
        } else {
            Write-Error "Please initialize the DLR submodule (the equivalent of `git submodule update --init` for your Git toolset"
            $global:Result = -1
            return
        }
    }

    msbuild Build.proj /m /t:$target /p:BuildFlavour=$configuration /verbosity:minimal /nologo /p:Platform="Any CPU" /bl:build-$target-$configuration.binlog
    # use the exit code of msbuild as the exit code for this script
    $global:Result = $LastExitCode
}

function Test([String] $target, [String] $configuration, [String[]] $frameworks) {
    foreach ($framework in $frameworks) {
        $testname = "";
        $filtername = $target
        if(!$_FRAMEWORKS[$framework]["filters"].ContainsKey($target)) {
            Write-Warning "No tests available for '$target' trying to run single test '$framework.$target'"
            $testname = "$framework.$target"
            $filtername = "single"
        }

        $filter = $_FRAMEWORKS[$framework]["filters"][$filtername]

        $replacements = @{
            "__FRAMEWORK__" = $framework;
            "__CONFIGURATION__" = $configuration;
            "__FILTERNAME__" = $filtername;
            "__FILTER__" = $filter;
            "__BASEDIR__" = $_BASEDIR;
            "__TESTNAME__" = $testname;
        };

        $runner = $_FRAMEWORKS[$framework]["runner"]
        # make a copy of the args array
        $_TempCliXMLString = [System.Management.Automation.PSSerializer]::Serialize($_FRAMEWORKS[$framework]["args"], [int32]::MaxValue)
        [Object[]] $args = [System.Management.Automation.PSSerializer]::Deserialize($_TempCliXMLString)
        # replace the placeholders with actual values
        for([int] $i = 0; $i -lt $args.Length; $i++) {
            foreach($r in $replacements.Keys) {
                $args[$i] = $args[$i].Replace($r, $replacements[$r])
            }
        }

        if($filter.Length -gt 0) {
            $tmp = $_FRAMEWORKS[$framework]["filterArg"].Replace('__FILTER__', $replacements['__FILTER__'])
            foreach($r in $replacements.Keys) {
                $tmp = $tmp.Replace($r, $replacements[$r])
            }
            $filter = $tmp
        }

        Write-Host "$runner $args $filter"

        # execute the tests
        & $runner $args $filter

        # save off the status in case of failure
        if($LastExitCode -ne 0) {
            $global:Result = $LastExitCode
        }
    }
}

switch -wildcard ($target) {
    # debug targets
    "restore-debug" { Main "RestoreReferences" "Debug" }
    "debug"         { Main "Build" "Debug" }
    "clean-debug"   { Main "Clean" "Debug" }
    "stage-debug"   { Main "Stage" "Debug" }
    "package-debug" { Main "Package" "Debug" }
    "test-debug-*"  { Test $target.Substring(11) "Debug" $frameworks; break }
    
    # release targets
    "restore"       { Main "RestoreReferences" "Release" }
    "release"       { Main "Build" "Release" }
    "clean"         { Main "Clean" "Release" }
    "stage"         { Main "Stage" "Release" }
    "package"       { Main "Package" "Release" }
    "test-*"        { Test $target.Substring(5) "Release" $frameworks; break }

    # utility targets
    "ngen"          {
        if(!$global:isUnix) {
            $imagePath = [System.IO.Path]::Combine($_BASEDIR, "bin\$configuration\net45\ipy.exe")
            & "${env:SystemRoot}\Microsoft.NET\Framework\v4.0.30319\ngen.exe" install $imagePath 
        }
    }

    default { Write-Error "No target '$target'" ; Exit -1 }
}

Exit $global:Result
