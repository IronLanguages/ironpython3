#!/usr/bin/env pwsh
# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS
    Install IronPython 3 from an official zip file distributable.

.DESCRIPTION
    This script facilitates installation of IronPython binaries from a zip file. The zip file is assumed to have content as published on the IronPython's download page. The zip file is produced by IronPython's "package" build target.

.EXAMPLE

    PS>Invoke-WebRequest -Uri https://github.com/IronLanguages/ironpython3/releases/download/v3.4.0/IronPython.3.4.0.zip -OutFile IronPython.3.4.0.zip
    PS>Expand-Archive -Path IronPython.3.4.0.zip -DestinationPath IronPython-unzipped
    PS>./IronPython-unzipped/scripts/Install-IronPython -Path ~/ipyenv/v3.4.0

    The official binaries are downloaded from GitHub to the current directory, unzipped, and then the installation proceeds using the script from the unzipped directory. IronPython is installed into ~/ipyenv/v3.4.0.

.EXAMPLE

    PS>Invoke-WebRequest -Uri https://github.com/IronLanguages/ironpython3/releases/download/v3.4.0/IronPython.3.4.0.zip -OutFile IronPython.3.4.0.zip
    PS>Install-IronPython -Path ~/ipyenv/v3.4.0 -ZipFile IronPython.3.4.0.zip -Framework net462 -Force

    The official binaries are downloaded from GitHub to the current directory and then the installation proceeds using the downloaded zip file. IronPython is installed into ~/ipyenv/v3.4.0, overwriting any previous installation in that location. IronPython binaries running on .NET Framework 4.6.2 are used during the installation.
    This example assumes that the installation script is in a directory on the search path ($env:PATH).

.EXAMPLE

    PS>./make
    PS>./make package
    PS>./eng/scripts/Install-IronPython.ps1 env

    These commands should be issued on a Powershell prompt with the current directory set to the project root.
    The project is first built, then packaged, and finally the script uses the zipfile produced during packaging to install IronPython in directory "env".

#>
[CmdletBinding()]
Param(
    # Target directory to which IronPython is to be installed.
    [Parameter(Position=0, Mandatory)]
    [string] $Path,

    # The path to the downloaded zip file with IronPython binaries. If empty, the script will try to grab the package directly produced by the local build.
    [string] $ZipFile,

    # The moniker of the .NET platform to install for.
    [string] $Framework = "net8.0",

    # If the target path exists, it will be wiped clean beforehand.
    [switch] $Force
)

$ErrorActionPreference = "Stop"

if (-not $ZipFile) {
    # If zipfile path not given, try to locate it
    $splitPSScriptRoot = $PSScriptRoot -split "\$([IO.Path]::DirectorySeparatorChar)"
    if ($splitPSScriptRoot[-1] -eq "scripts" -and (Test-Path (Join-Path (Split-Path $PSScriptRoot) "lib"))) {
        # Script run from within already expanded zip file
        $unzipDir = $PSScriptRoot | Split-Path
    } elseif ($splitPSScriptRoot[-2] -eq "eng" -and $splitPSScriptRoot[-1] -eq "scripts") {
        # Script run from within a checked out code base
        # Locate the zip archive in the standard location of the package target
        $projectRoot = $PSScriptRoot | Split-Path | Split-Path
        $zipFiles = @(Resolve-Path (Join-Path $projectRoot "eng/Release/Packages/IronPython-*/IronPython.3.*.zip"))
        if ($zipFiles.Count -gt 1) {
            Write-Error (@("Ambiguous implicit project zip files:") + $zipFiles -join "`n")
        } elseif ($zipFiles.Count -lt 1) {
            Write-Error "Missing zip file. Have you run './make package'?"
        }
        $ZipFile = $zipFiles
    } else {
        Write-Error "Cannot locate implicit zip file. Provide path to the zip file using '-ZipFile <path>'."
    }
} elseif (-not (Test-Path $ZipFile)) {
    Write-Error "ZipFile not found: $ZipFile"
}

# Prepare destination path
if (Test-Path $Path) {
    if ($Force) {
        if ((Resolve-Path $Path).Count -gt 1) {
            Write-Error "Overwriting of multiple destinations not allowed: $Path"
        }
        Remove-Item -Path $Path -Force -Recurse
    } else {
        Write-Error "Path already exists: $Path"
    }
}
New-Item $Path -ItemType Directory | Out-Null

# Unzip archive
if (-not $unzipDir) {
    $unzipDir = Join-Path $Path "unzipped"
    Expand-Archive -Path $ZipFile -DestinationPath $unzipDir
    $unzipped = $true
}

# Copy files into place
Copy-Item -Path (Join-Path $unzipDir (Join-Path $Framework "*")) -Destination $Path -Recurse
Copy-Item -Path (Join-Path $unzipDir "lib") -Destination $Path -Recurse

# Prepare startup scripts
if ($Framework -notlike "net4*") {
    $ipyPath = Join-Path $Path "ipy.ps1"
    Set-Content -Path $ipyPath -Value @'
#!/usr/bin/env pwsh
dotnet (Join-Path $PSScriptRoot ipy.dll) @args
'@
    if ($PSVersionTable.PSEdition -eq "Desktop" -or $IsWindows) {
        $ipyPath = Join-Path $Path "ipy.bat"
    Set-Content -Path $ipyPath -Value @'
@dotnet "%~dp0ipy.dll" %*
'@
    }
    if ($IsMacOS -or $IsLinux) {
        chmod +x $ipyPath
        chmod +x (Join-Path $Path "ipy.sh")
        Move-Item -Path (Join-Path $Path "ipy.sh") -Destination (Join-Path $Path "ipy")
    }
} elseif ($IsMacOS -or $IsLinux) { # Mono
    $ipyPath = Join-Path $Path "ipy"
    Set-Content -Path $ipyPath -Value @'
#!/bin/sh
BASEDIR=$(dirname "$0")
ABS_PATH=$(cd "$BASEDIR"; pwd)
mono "$ABS_PATH/ipy.exe" "$@"
'@
    chmod +x $ipyPath
}

# Install additional scripts
Copy-Item -Path (Join-Path $unzipDir "scripts/Enter-IronPythonEnvironment.ps1") -Destination $Path
if ($IsMacOS -or $IsLinux) {
    chmod -x (Join-Path $Path "Enter-IronPythonEnvironment.ps1")
}

# Clean up unzipped files
if ($unzipped) {
    Remove-Item -Path $unzipDir -Recurse
}
