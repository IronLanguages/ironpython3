# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

"""
Test issues related to compiled code.
"""

import os
import unittest

from iptest import IronPythonTestCase, is_netcoreapp, skipUnlessIronPython, run_test

class RegressionCompiledTest(IronPythonTestCase):
    def test_gh_ipy2_563(self):
        """https://github.com/IronLanguages/ironpython2/issues/563"""
        eval('3.14')

    @unittest.skipIf(is_netcoreapp, 'no clr.CompileModules')
    @skipUnlessIronPython()
    def test_gh1357(self):
        filename = os.path.join(self.temporary_dir, 'gh1357_%d.py' % os.getpid())
        dll = os.path.join(self.temporary_dir, "test_%d.dll" % os.getpid())
        with open(filename, 'w') as f:
            f.write('{(1,): None}')

        import clr
        try:
            clr.CompileModules(dll, filename)
        except:
            Fail('Failed to compile the specified file')
        finally:
            os.unlink(filename)
            os.unlink(dll)

    @unittest.skipIf(is_netcoreapp, 'no clr.CompileModules')
    @skipUnlessIronPython()
    def test_ipy3_gh1601(self):
        filename = os.path.join(self.temporary_dir, 'test_ipy3_gh1601_%d.py' % os.getpid())
        dll = os.path.join(self.temporary_dir, "test_ipy3_gh1601_%d.dll" % os.getpid())
        with open(filename, 'w') as f:
            f.write('class MyClass:\n')
            f.write('    """description"""\n')

        import clr
        try:
            clr.CompileModules(dll, filename)
        except:
            Fail('Failed to compile the specified file')
        finally:
            os.unlink(filename)
            os.unlink(dll)

run_test(__name__)
