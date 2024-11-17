# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import sys
from iptest.runner import run_test_pkg

#List of all test packages to be run under IronPython
TEST_PKG_LIST = ["interop.net", "interop.com", "modules", "hosting", "versions", "stress"]

#List of specific modules contained within test packages
#that have been disabled
DO_NOT_RUN = ["interop.net.insert_csharp"]

#--Main------------------------------------------------------------------------
#If no test package has been specified from the command-line, run everything
#we know about.
if len(sys.argv)!=1:
    TEST_PKG_LIST = [sys.argv[1]]

for x in TEST_PKG_LIST:
    run_test_pkg(x, DO_NOT_RUN)
