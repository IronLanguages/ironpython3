#!/usr/bin/env pwsh
[CmdletBinding()]
Param(
    [Parameter(Position=1)]
    [String] $target = "release",
    [String] $configuration = "Release",
    [String[]] $frameworks=@('net45','netcoreapp2.1'),
    [String] $platform = "x64",
    [switch] $runIgnored
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
        Write-Error "Visual Studio 2017 15.9 or later is required"
        Exit 1
    }

    if(-not [System.IO.Directory]::Exists($_VSINSTPATH)) {
        Write-Error "Could not determine installation path to Visual Studio"
        Exit 1
    }

    if([System.IO.File]::Exists([System.IO.Path]::Combine($_VSINSTPATH, 'MSBuild\Current\Bin\MSBuild.exe'))) {
        $_MSBUILDPATH = [System.IO.Path]::Combine($_VSINSTPATH, 'MSBuild\Current\Bin\')
        if ($env:PATH -notcontains $_MSBUILDPATH) {
            $env:PATH = [String]::Join(';', $env:PATH, $_MSBUILDPATH)
        }
    }
    elseif([System.IO.File]::Exists([System.IO.Path]::Combine($_VSINSTPATH, 'MSBuild\15.0\Bin\MSBuild.exe'))) {
        $_MSBUILDPATH = [System.IO.Path]::Combine($_VSINSTPATH, 'MSBuild\15.0\Bin\')
        if ($env:PATH -notcontains $_MSBUILDPATH) {
            $env:PATH = [String]::Join(';', $env:PATH, $_MSBUILDPATH)
        }
    }
}

$_defaultFrameworkSettings = @{
    "runner" = "dotnet";
    "args" = @('test', '__BASEDIR__/Src/IronPythonTest/IronPythonTest.csproj', '-f', '__FRAMEWORK__', '-o', '__BASEDIR__/bin/__CONFIGURATION__/__FRAMEWORK__', '-c', '__CONFIGURATION__', '--no-build', '-l', "trx;LogFileName=__FILTERNAME__-__FRAMEWORK__-__CONFIGURATION__-result.trx", '-s', '__RUNSETTINGS__');
    "filterArg" = '--filter="__FILTER__"';
    "filters" = @{
        "all" = "";
        "smoke" = "TestCategory=StandardCPython";
        "cpython" = "TestCategory=StandardCPython | TestCategory=AllCPython";
        "ironpython" = "TestCategory=IronPython";
        "ctypes" = "TestCategory=CTypesCPython";
        "single" = "Name=__TESTNAME__";
        "query" = "FullyQualifiedName__QUERY__";
    }
}

# Overrides for the default framework settings
$_FRAMEWORKS = @{}

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

    msbuild Build.proj /m /t:$target /p:Configuration=$configuration /verbosity:minimal /nologo /p:Platform="Any CPU" /bl:build-$target-$configuration.binlog
    # use the exit code of msbuild as the exit code for this script
    $global:Result = $LastExitCode
}

function GenerateRunSettings([String] $framework, [String] $platform, [String] $configuration, [bool] $runIgnored) {
    [System.Xml.XmlDocument]$doc = New-Object System.Xml.XmlDocument

#   <RunSettings>
#     <TestRunParameters>
#       <Parameter name="FRAMEWORK" value="net45" />
#     </TestRunParameters>
#   </RunSettings>

    $dec = $doc.CreateXmlDeclaration("1.0","UTF-8",$null)
    $doc.AppendChild($dec) | Out-Null

    $runSettings = $doc.CreateElement("RunSettings")

    $runConfiguration = $doc.CreateElement("RunConfiguration")
    $runSettings.AppendChild($runConfiguration) | Out-Null
    $targetPlatform = $doc.CreateElement("TargetPlatform")
    $targetPlatform.InnerText = $platform
    $runConfiguration.AppendChild($targetPlatform) | Out-Null

    $testRunParameters = $doc.CreateElement("TestRunParameters")
    $runSettings.AppendChild($testRunParameters) | Out-Null

    $parameter = $doc.CreateElement("Parameter")
    $parameter.SetAttribute("name", "FRAMEWORK")
    $parameter.SetAttribute("value", $framework)
    $testRunParameters.AppendChild($parameter) | Out-Null

    $parameter = $doc.CreateElement("Parameter")
    $parameter.SetAttribute("name", "CONFIGURATION")
    $parameter.SetAttribute("value", $configuration)
    $testRunParameters.AppendChild($parameter) | Out-Null

    if($runIgnored) {
        $parameter = $doc.CreateElement("Parameter")
        $parameter.SetAttribute("name", "RUN_IGNORED")
        $parameter.SetAttribute("value", "true")
        $testRunParameters.AppendChild($parameter) | Out-Null
    }

    $doc.AppendChild($runSettings) | Out-Null

    $fileName = [System.IO.Path]::Combine($_BASEDIR, "Src", "IronPythonTest", "runsettings.$framework.xml")
    $doc.Save($fileName)
    return $fileName
}

function Test([String] $target, [String] $configuration, [String[]] $frameworks, [String] $platform) {
    foreach ($framework in $frameworks) {
        $testname = "";
        $filtername = $target

        # generate the runsettings file for the settings
        $runSettings = GenerateRunSettings $framework $platform $configuration $runIgnored

        $frameworkSettings = $_FRAMEWORKS[$framework]
        if ($frameworkSettings -eq $null) { $frameworkSettings = $_defaultFrameworkSettings }

        if ($target -imatch "^ID[=~]") {
            $testquery = $target.Substring(2)
            $filtername = "query"
        } elseif(!$frameworkSettings["filters"].ContainsKey($target)) {
            Write-Warning "No tests available for '$target' trying to run single test '$framework.$target'"
            $testname = "$framework.$target"
            $filtername = "single"
        }

        $filter = $frameworkSettings["filters"][$filtername]

        $replacements = @{
            "__FRAMEWORK__" = $framework;
            "__CONFIGURATION__" = $configuration;
            "__FILTERNAME__" = $filtername;
            "__FILTER__" = $filter;
            "__BASEDIR__" = $_BASEDIR;
            "__TESTNAME__" = $testname;
            "__QUERY__" = $testquery;
            "__RUNSETTINGS__" = $runSettings;
        };

        $runner = $frameworkSettings["runner"]
        # make a copy of the args array
        [Object[]] $args = @() + $frameworkSettings["args"]
        # replace the placeholders with actual values
        for([int] $i = 0; $i -lt $args.Length; $i++) {
            foreach($r in $replacements.Keys) {
                $args[$i] = $args[$i].Replace($r, $replacements[$r])
            }
        }

        if($filter.Length -gt 0) {
            $tmp = $frameworkSettings["filterArg"].Replace('__FILTER__', $replacements['__FILTER__'])
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
    "test-debug-*"  { Test $target.Substring(11) "Debug" $frameworks $platform; break }
    
    # release targets
    "restore"       { Main "RestoreReferences" "Release" }
    "release"       { Main "Build" "Release" }
    "clean"         { Main "Clean" "Release" }
    "stage"         { Main "Stage" "Release" }
    "package"       { Main "Package" "Release" }
    "test-*"        { Test $target.Substring(5) "Release" $frameworks $platform; break }

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
