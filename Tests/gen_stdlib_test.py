# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import importlib
import os
import sys
import unittest

template = """# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from {name} from StdLib
##

import unittest
import sys

from iptest import run_test

import test.{name}

def load_tests(loader, standard_tests, pattern):
    if sys.implementation.name == 'ironpython':
        suite = unittest.TestSuite()
{tests}
        return suite

    else:
        return loader.loadTestsFromModule(test.{name}, pattern)

run_test(__name__)
"""

if __name__ == "__main__":
    name = sys.argv[1]

    sys.path.insert(0, os.path.abspath(os.path.join(__file__, "../../Src/StdLib/Lib")))
    module = importlib.import_module("test.{name}".format(name=name))

    tests = []
    for suite in unittest.defaultTestLoader.loadTestsFromModule(module):
        for test in suite:
            tests.append("        suite.addTest({}('{}'))".format(unittest.util.strclass(test.__class__), test._testMethodName))

    with open(name + "_stdlib.py", "w") as f:
        f.write(template.format(name=name, tests="\n".join(tests)))
