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

# -----------------------------------------------------------------------------
# -- Initial Setup

if ($env:DLR_ROOT -eq $null) {
	echo "DLR_ROOT is not set.  Cannot continue!"
	exit 1
}

. "$env:DLR_ROOT\Test\Scripts\install_dlrcom.ps1"
if (! $?) {
	echo "Failed to source and run "$env:DLR_ROOT\Test\Scripts\install_dlrcom.ps1".  Cannot continue!"
	exit 1
}

if ("$env:DLR_BIN" -eq "") {
	log-critical "DLR_BIN is not set.  Cannot continue!"
}

if (! (test-path $env:DLR_BIN\ipy.exe)) {
	log-critical "$env:DLR_BIN\ipy.exe does not exist.  Cannot continue!"
}
set-alias rowipy $env:DLR_ROOT\Languages\IronPython\Internal\ipy.bat



# ------------------------------------------------------------------------------
# -- Run the test

pushd $env:DLR_ROOT\Languages\IronPython\Tests

log-info "Running the following COM test: $PWD> $env:DLR_BIN\ipy.exe $args"
rowipy $args[0].Split(" ")
$EC = $LASTEXITCODE

popd
exit $EC