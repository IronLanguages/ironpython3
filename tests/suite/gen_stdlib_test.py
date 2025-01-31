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

from iptest import is_ironpython, generate_suite, run_test

import {package}test.{name}

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule({package}test.{name}, pattern=pattern)

    if is_ironpython:
        {tests}

        failing_tests = []

        skip_tests = []

        return generate_suite(tests, failing_tests, skip_tests)

    else:
        return tests

run_test(__name__)
"""

if __name__ == "__main__":
    name = sys.argv[1]
    package = sys.argv[2] if len(sys.argv) > 2 else None
    location = sys.argv[3] if len(sys.argv) > 3 else "."

    filepath = os.path.join(location, name + ("_" + package if package else "") + "_stdlib.py")

    sys.path.insert(0, os.path.abspath(os.path.join(__file__, "../../src/core/IronPython.StdLib/lib")))
    module = importlib.import_module("{package}test.{name}".format(name=name, package=package + "." if package else ""))

    tests = []
    for suite in unittest.defaultTestLoader.loadTestsFromModule(module):
        for test in suite:
            tests.append("{}('{}')".format(unittest.util.strclass(test.__class__), test._testMethodName))

    if os.path.exists(filepath):
        with open(filepath, "r", encoding="utf-8") as f:
            lines = list(f)

        existing_tests = set()

        first = 0
        for i, line in enumerate(lines):
            if "{" in line: raise NotImplementedError

            if not first:
                if line.startswith("    if is_ironpython:"):
                    first = i + 1
            else:
                if line.startswith("        return generate_suite("):
                    break
                existing_tests.add(line.split("#")[0].strip().rstrip(","))

        tests = [t for t in tests if t not in existing_tests]

        if tests:
            lines.insert(first, "        {tests}\n")
        template = "".join(lines)

    with open(filepath, "w", encoding="utf-8") as f:
        f.write(template.format(name=name, package=package + "." if package else "", tests="\n        ".join(tests)))
