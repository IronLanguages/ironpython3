# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS

    Activates an IronPython environment, placing its commands at the head of $Env:PATH.

.DESCRIPTION

    This script modifies the current PowerShell environment such that a given IronPython installation becomes the active one. By default, the installation containing this script is being activated, or, if the script is not part of an installation, the latest MSI-installed version of IronPython (Windows only).

    IronPython environments do not nest. Entering a new environment from within an active environment automatically exits the current one.

    The following modifications are made:

    * The Env:PATH variable is modified by adding the path to the environment commands at the beginning of the existing list of paths.
    * The PowerShell prompt is modified to display the name of the environment.
    * A new function Exit-IronPythonEnvironment is defined, with an alias "exipy". Use this function to undo all modifications done on entering.

    When switch -Isolated is used, additional modifications are performed:

    * Variable Env:IRONPYTHONPATH is cleared.
    * Alias:ipy, if exists, is deleted.
    * Function:ipy, if exists, is deleted.
#>
[CmdletBinding()]
param (
    # Path to an IronPython environment to enter.
    [Parameter(Position=0)]
    [SupportsWildcards()]
    [string] $Path,

    # On enter, unset Env:IRONPYTHONPATH, Alias:ipy, and Function:ipy. Restore on exit.
    [switch] $Isolated,

    # Color of the environment name printed on the prompt.
    [ConsoleColor] $PromptColor = [ConsoleColor]::DarkGray
)

if (-not $Path) {
    # Locate default environment to activate
    if (Test-Path (Join-Path $PSScriptRoot "IronPython.dll")) {
        $Path = $PSScriptRoot
    } elseif ($IsWindows -and (Test-Path HKLM:\SOFTWARE\IronPython\*\InstallPath)) {
        $installKey = (Resolve-Path HKLM:\SOFTWARE\IronPython\*\InstallPath)[-1] # grab the latest
        $Path = Get-ItemPropertyValue -Path $installKey -Name '(default)'
    } else {
        Write-Error "Cannot locate default environment."
        exit 1
    }
}

if (-not (Test-Path $Path -PathType Container)) {
    Write-Error "Environment not found: $Path"
    exit 1
}

if ((Resolve-Path $Path).Count -gt 1) {
    Write-Error "Environment path resolved to multiple locations:"
    Resolve-Path $Path | Write-Error
    exit 1
}

function global:Exit-IronPythonEnvironment ([switch] $NonDestructive) {
    <#
    .SYNOPSIS
        Undo all environment modifications done by Enter-IronPythonEnvironment

    .PARAMETER NonDestructive
        Keep function Exit-IronPythonEnvironment and its alias. Subsequent invocations of this function while not within an IronPython environment will result in no-op.
    #>

    if (Test-Path Function:IronPythonParentPrompt) {
        Copy-Item Function:IronPythonParentPrompt Function:prompt
        Remove-Item Function:IronPythonParentPrompt
    }

    if (Test-Path Variable:IronPythonParentEnvironment) {
        $Env:IRONPYTHONPATH = $IronPythonParentEnvironment["libpath"]
        $Env:PATH = $IronPythonParentEnvironment["binpath"]

        $oldAlias = $IronPythonParentEnvironment["alias"]
        if ($null -ne $oldAlias) {
            Set-Alias -Name ipy `
                -Value $oldAlias.Definition `
                -Description $oldAlias.Description `
                -Option $oldAlias.Options `
                -Scope global
        }
        if ($null -ne $IronPythonParentEnvironment["function"]) {
            Copy-Item $IronPythonParentEnvironment["function"] Function:ipy
        }
        Remove-Item Variable:IronPythonParentEnvironment
    }

    Remove-Variable -Name IronPythonEnvironment* -Scope global

    if (-not $NonDestructive) {
        Remove-Item Function:Exit-IronPythonEnvironment
        Remove-Item Alias:exipy -ErrorAction Ignore
    }
}

Exit-IronPythonEnvironment -NonDestructive

$global:IronPythonEnvironmentPath = Resolve-Path $Path
$global:IronPythonEnvironmentName = Split-Path $IronPythonEnvironmentPath -Leaf
$global:IronPythonEnvironmentColor = "$PromptColor"

# Save stuff that already exists

$global:IronPythonParentEnvironment = @{ "binpath" = $Env:PATH }

if ($Isolated) {
    if (Test-Path Alias:ipy) {
        $IronPythonParentEnvironment["alias"] = Get-Item Alias:ipy
        Remove-Item Alias:ipy
    }

    if (Test-Path Function:ipy) {
        $IronPythonParentEnvironment["function"] = Get-Item function:ipy
        Remove-Item Function:ipy
    }

    if (Test-Path Env:IRONPYTHONPATH) {
        $IronPythonParentEnvironment["libpath"] = $Env:IRONPYTHONPATH
        Remove-Item Env:IRONPYTHONPATH
    }
}

function global:IronPythonParentPrompt {""}
Copy-Item Function:prompt Function:IronPythonParentPrompt

# Set new stuff for the environment to be operational

function global:prompt {
    Write-Host -NoNewline -ForegroundColor $IronPythonEnvironmentColor "«$IronPythonEnvironmentName» "
    IronPythonParentPrompt
}

$env:PATH = $IronPythonEnvironmentPath, $env:PATH -join [IO.Path]::PathSeparator

Set-Alias exipy Exit-IronPythonEnvironment -Scope global
