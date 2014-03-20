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
