# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

"""
Example usage:
    ipy gen_stdlib_test.py test_codecs
    ipy gen_stdlib_test.py test_bitfields ctypes modules/type_related
"""

import importlib
import os
import re
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

import {package}test.{name}

def load_tests(loader, standard_tests, pattern):
    if sys.implementation.name == 'ironpython':
        suite = unittest.TestSuite()
{tests}
        return suite

    else:
        return loader.loadTestsFromModule({package}test.{name}, pattern)

run_test(__name__)
"""

if __name__ == "__main__":
    name = sys.argv[1]
    package = sys.argv[2] if len(sys.argv) > 2 else None
    location = sys.argv[3] if len(sys.argv) > 3 else "."

    filepath = os.path.join(location, name + ("_" + package if package else "") + "_stdlib.py")

    sys.path.insert(0, os.path.abspath(os.path.join(__file__, "../../Src/StdLib/Lib")))
    module = importlib.import_module("{package}test.{name}".format(name=name, package=package + "." if package else ""))

    existing_tests = {}
    try:
        re_failure = re.compile(r'^\s*suite\.addTest\(unittest\.expectedFailure\((.*)\)\)( #.*)?$')
        re_ok = re.compile(r'^\s*suite\.addTest\((.*)\)( #.*)?$')
        with open(filepath, "r", encoding="utf-8") as f:
            for line in f:
                match = re_failure.match(line) or re_ok.match(line)
                if match:
                    existing_tests[match.group(1)] = match.group(0)
    except FileNotFoundError:
        pass

    tests = []
    for suite in unittest.defaultTestLoader.loadTestsFromModule(module):
        for test in suite:
            tests.append("{}('{}')".format(unittest.util.strclass(test.__class__), test._testMethodName))

    tests = [existing_tests.get(t, "        suite.addTest({})".format(t)) for t in tests]

    with open(filepath, "w", encoding="utf-8") as f:
        f.write(template.format(name=name, package=package + "." if package else "", tests="\n".join(tests)))
