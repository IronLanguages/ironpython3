#!/usr/bin/env pwsh
[CmdletBinding()]
Param(
    [Parameter(Position=1)]
    [String] $target = "build",
    [String] $configuration = "Release",
    [String[]] $frameworks=@('net46','netcoreapp2.1','netcoreapp3.1','net5.0'),
    [String] $platform = "x64",
    [switch] $runIgnored,
    [int] $jobs = [System.Environment]::ProcessorCount
)

$ErrorActionPreference="Continue"

[int] $global:Result = 0
[bool] $global:isUnix = [System.Environment]::OSVersion.Platform -eq [System.PlatformID]::Unix

$_BASEDIR = Split-Path -Path $MyInvocation.MyCommand.Definition -Parent

function EnsureMSBuild() {
    $_VSWHERE = [System.IO.Path]::Combine(${env:ProgramFiles(x86)}, 'Microsoft Visual Studio\Installer\vswhere.exe')
    $_VSINSTPATH = ''

    if([System.IO.File]::Exists($_VSWHERE)) {
        $_VSINSTPATH = & "$_VSWHERE" -latest -requires Microsoft.Component.MSBuild -property installationPath
    } else {
        Write-Error "Visual Studio 2019 16.8 or later is required"
        Exit 1
    }

    if(-not [System.IO.Directory]::Exists($_VSINSTPATH)) {
        Write-Error "Could not determine installation path to Visual Studio"
        Exit 1
    }

    if([System.IO.File]::Exists([System.IO.Path]::Combine($_VSINSTPATH, 'MSBuild\Current\Bin\MSBuild.exe'))) {
        $_MSBUILDPATH = [System.IO.Path]::Combine($_VSINSTPATH, 'MSBuild\Current\Bin\')
        if ($env:PATH -split ';' -notcontains $_MSBUILDPATH) {
            $env:PATH = [String]::Join(';', $env:PATH, $_MSBUILDPATH)
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

    if (!$global:isUnix -And ($target -eq "Package")) {
        EnsureMSBuild
        msbuild Build.proj /m /t:$target /p:Configuration=$configuration /verbosity:minimal /nologo /p:Platform="Any CPU" /bl:build-$target-$configuration.binlog
    }
    else {
        dotnet msbuild Build.proj /m /t:$target /p:Configuration=$configuration /verbosity:minimal /nologo /p:Platform="Any CPU" /bl:build-$target-$configuration.binlog
    }
    # use the exit code of msbuild as the exit code for this script
    $global:Result = $LastExitCode
}

function GenerateRunSettings([String] $framework, [String] $platform, [String] $configuration, [bool] $runIgnored) {
    [System.Xml.XmlDocument]$doc = New-Object System.Xml.XmlDocument

#   <RunSettings>
#     <TestRunParameters>
#       <Parameter name="FRAMEWORK" value="net46" />
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

function RunTestTasks($tasks) {
    $maxJobs = $jobs
    if ($tasks.Count -lt $maxJobs) {
        $maxJobs = $tasks.Count
    }

    $testScript = {
        & dotnet test $args
        # pass the status out in case of failure
        if($LastExitCode -ne 0) {
            throw $LastExitCode
        }
    }

    # create initial jobs
    $bgJobs = @{}
    $nextJob = 0
    for ($i = 0; $i -lt $maxJobs; $i++) {
        $j = Start-Job -ScriptBlock $testScript -ArgumentList $tasks[$nextJob].params
        $bgJobs[$j] = $nextJob;
        $nextJob += 1
    }

    $failedTests = @()
    while ($bgJobs.values.Count -gt 0) {
        $finished = Wait-Job -Any -Timeout 10 -Job @($bgJobs.keys)

        # redirect output
        foreach ($j in $bgJobs.keys) {
            Receive-Job $j | ForEach-Object { "$($tasks[$bgJobs.$j].name)  $_" }
        }

        # clean up finished jobs and create new ones
        if ($null -ne $finished) {
            $testName = $tasks[$bgJobs.$finished].name
            Write-Host
            if ($finished.State -eq 'Failed') {
                Write-Host "$testName failed"
                $global:Result = $finished.ChildJobs[0].JobStateInfo.Reason.ErrorRecord.TargetObject
                $failedTests += $testName
            } else {
                Write-Host "$testName succeeded"
            }

            $bgJobs.Remove($finished)
            Remove-Job $finished
            if ($nextJob -lt $tasks.Count) {
                $j = Start-Job -ScriptBlock $testScript -ArgumentList $tasks[$nextJob].params
                $bgJobs[$j] = $nextJob;
                $nextJob += 1
            }
            Write-Host "$($bgJobs.Count) jobs running: $(($bgJobs.values | ForEach-Object {$tasks[$_].name}) -Join ", ")"
            Write-Host "$($tasks.Count - $nextJob) jobs pending: $(($nextJob..$tasks.Count | ForEach-Object {$tasks[$_].name}) -Join ", ")"
            if ($failedTests) {
                Write-Host "$($failedTests.Count) jobs failed: $($failedTests -Join ", ")"
            }
            Write-Host
        }
    }
}

function Test([String] $target, [String] $configuration, [String[]] $frameworks, [String] $platform) {
    Write-Host "Scheduling Test Tasks"
    Write-Host

    $tasks = @()
    foreach ($framework in $frameworks) {
        # generate the runsettings file for the settings
        $runSettings = GenerateRunSettings $framework $platform $configuration $runIgnored

        function createTask($filtername, $filter) {
            [Object[]] $args = @("$_BASEDIR/Src/IronPythonTest/IronPythonTest.csproj", '-f', "$framework", '-o', "$_BASEDIR/bin/$configuration/$framework", '-c', "$configuration", '--no-build', '-l', "trx;LogFileName=$filtername-$framework-$configuration-result.trx", '-s', "$runSettings", "--filter=$filter");
            Write-Host "Enqueue [$framework $filtername]:"
            Write-Host "dotnet test $args"
            Write-Host
            return @{ name = "[$framework $filtername]"; params = $args }
        }

        $filter = $target

        switch ($filter.ToLower()) {
            "all"        {
                $tasks += createTask "ironpython" "TestCategory=IronPython"
                $tasks += createTask "cpython" "TestCategory=CPython"
                $tasks += createTask "cs" "TestCategory!=IronPython&TestCategory!=CPython"
            }
            "ironpython" { $tasks += createTask "ironpython" "TestCategory=IronPython" }
            "cpython"    { $tasks += createTask "cpython" "TestCategory=CPython" }
            default      {
                if ($filter.ToLower().StartsWith('ironpython')) {
                    $tasks += createTask "query" "TestCategory=IronPython&$filter"
                } elseif ($filter.ToLower().StartsWith('cpython')) {
                    $tasks += createTask "query" "TestCategory=CPython&$filter"
                } else {
                    $tasks += createTask "query" $filter
                }
            }
        }
    }

    RunTestTasks $tasks
}

switch -wildcard ($target) {
    # debug targets
    "restore-debug" { Main "RestoreReferences" "Debug" }
    "debug"         { Main "Build" "Debug" }
    "clean-debug"   { Main "Clean" "Debug" }
    "stage-debug"   { Main "Stage" "Debug" }
    "package-debug" { Main "Package" "Debug" }
    "test-debug-*"  { Test $target.Substring(11) "Debug" $frameworks $platform; break }
    "test-debug"    { Test "all" "Debug" $frameworks $platform; break }

    # release targets
    "release"       { Main "Build" "Release" }

    # general targets
    "restore"       { Main "RestoreReferences" $configuration }
    "build"         { Main "Build" $configuration }
    "clean"         { Main "Clean" $configuration }
    "stage"         { Main "Stage" $configuration }
    "package"       { Main "Package" $configuration }
    "test-*"        { Test $target.Substring(5) $configuration $frameworks $platform; break }
    "test"          { Test "all" $configuration $frameworks $platform; break }

    # utility targets
    "ngen"          {
        if(!$global:isUnix) {
            $imagePath = [System.IO.Path]::Combine($_BASEDIR, "bin\$configuration\net46\ipy.exe")
            & "${env:SystemRoot}\Microsoft.NET\Framework\v4.0.30319\ngen.exe" install $imagePath
        }
    }

    default { Write-Error "No target '$target'" ; Exit -1 }
}

Exit $global:Result
