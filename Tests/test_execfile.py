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

from iptest.assert_util import *
from iptest.file_util import path_combine
skiptest("silverlight")

import os


@skip("win32")
def test_sanity():
    root = testpath.public_testdir
    exec(compile(open(path_combine(root, "Inc", "toexec.py")).read(), path_combine(root, "Inc", "toexec.py"), 'exec'))
    exec(compile(open(path_combine(root, "Inc", "toexec.py")).read(), path_combine(root, "Inc", "toexec.py"), 'exec'))
    #execfile(root + "/doc.py")
    exec(compile(open(path_combine(root, "Inc", "toexec.py")).read(), path_combine(root, "Inc", "toexec.py"), 'exec'))

def test_negative():
    AssertError(TypeError, execfile, None) # arg must be string
    AssertError(TypeError, execfile, [])
    AssertError(TypeError, execfile, 1)
    AssertError(TypeError, execfile, "somefile", "")

def test_scope():
    root = testpath.public_testdir
    z = 10
    exec(compile(open(path_combine(root, "Inc", "execfile_scope.py")).read(), path_combine(root, "Inc", "execfile_scope.py"), 'exec'))
    

run_test(__name__)

