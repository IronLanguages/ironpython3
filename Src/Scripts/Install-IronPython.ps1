#!/usr/bin/env pwsh
# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS
    Install IronPython 3 for .NET 6 from an official zip file distributable.

.DESCRIPTION
    This script facilitates installation of IronPython on .NET 6 binaries from a zip file. The zip file is assumed to have content as published on the IronPython's download page. The zip file is produced by IronPython's "package" build target.

.EXAMPLE
    PS>./make
    PS>./make package
    PS>./Src/Scripts/Install-IronPython.ps1 env

    These commands should be issued on a Powershell prompt with the current directory set to the project root.
    The project is first built, then packaged, and finally the script uses the zipfile produced during packaging to install IronPython in directory "env"

.EXAMPLE

    PS>Invoke-WebRequest -Uri https://github.com/IronLanguages/ironpython3/releases/download/v3.4.0-beta1/IronPython.3.4.0-beta1.zip -OutFile IronPython.3.4.0-beta1.zip
    PS>Install-IronPython -Path ~/ipyenv/v3.4.0-beta1 -ZipFile IronPython.3.4.0-beta1.zip -Force

    The official binaries are downloaded from GitHub to the current directory and then the installation proceeds using the downloaded zip file. IronPython is installed into ~/ipyenv/v3.4.0-beta1, overwriting any previous installation in that location.
    This example assumes that the installation script is in a directory on the search path ($env:PATH).
#>
[CmdletBinding()]
Param(
    # Target directory to which IronPython is to be installed.
    [Parameter(Position=0, Mandatory)]
    [SupportsWildcards()]
    [string] $Path,

    # The path to the downloaded zip file with IronPython binaries. If empty, the script will try to grab the package directly produced by the local build.
    [string] $ZipFile,

    # If the target path exists, it will be wiped clean beforehand.
    [switch] $Force
)

$ErrorActionPreference = "Stop"

$defaultVersion = "3.4.0-beta1"

if (-not $ZipFile) {
    # If zipfile path not given, assume that the script is in the standard location within the source tree
    # and locate the zip archive in the standard location of the package target.
    $projectRoot = $PSScriptRoot | Split-Path | Split-Path
    $ZipFile = Join-Path $projectRoot "Package/Release/Packages/IronPython-$defaultVersion/IronPython.$defaultVersion.zip"
}

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

# Unzip archive and move files into place
$unzipDir = Join-Path $Path "zip"

Expand-Archive -Path $ZipFile -DestinationPath $unzipDir
Move-Item -Path (Join-Path $unzipDir "Lib") -Destination $Path
Move-Item -Path (Join-Path $unzipDir "net6.0/*") -Destination $Path -Exclude "*.xml","*.dll.config"
Remove-Item -Path $unzipDir -Recurse

# Create a startup script
$ipyPath = Join-Path $Path "ipy.ps1"
Set-Content -Path $ipyPath -Value @'
#!/usr/bin/env pwsh
dotnet (Join-Path $PSScriptRoot ipy.dll) @args
'@
if ($IsMacOS -or $IsLinux) {
    chmod +x $ipyPath
}

# Add items that are missing in the zip file, directly from the bin directory
if ($projectRoot) {
    $binPath = Join-Path $projectRoot "bin/Release/net6.0"
    Copy-Item (Join-Path $projectRoot "Src/Scripts/Enter-IronPythonEnvironment.ps1") $Path
    if ($IsWindows){
        Copy-Item (Join-Path $binPath "ipy.exe") $Path
    } else {
        Copy-Item (Join-Path $binPath "ipy") $Path
        Copy-Item (Join-Path $binPath "Mono.Unix.dll") $Path
        $arch = uname -m
        switch ($arch) {
            "x86_64" { $arch = "x64" }
        }
        if ($IsMacOS) {
            $os = "osx"
        } else {
            $os = "linux"
        }
        $libs = Join-Path $binPath "runtimes" "$os-$arch" "native/*" -Resolve
        Copy-Item $libs $Path
    }
}
