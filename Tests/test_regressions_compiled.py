# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

"""
Test issues related to compiled code.
"""

from iptest import IronPythonTestCase, run_test

class RegressionCompiledTest(IronPythonTestCase):
    def test_gh_ipy2_563(self):
        """https://github.com/IronLanguages/ironpython2/issues/563"""
        eval('3.14')

run_test(__name__)
