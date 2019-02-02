# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

'''
CPython's _warnings module. http://docs.python.org/library/warnings.html
'''

import unittest
import _warnings
from iptest import IronPythonTestCase, run_test, stderr_trapper as stderr_trapper

#--GLOBALS---------------------------------------------------------------------
EXPECTED = [] # expected output (ignoring filename, lineno, and line)
WARN_TYPES = [Warning, UserWarning, PendingDeprecationWarning, SyntaxWarning,
              RuntimeWarning, FutureWarning, ImportWarning, UnicodeWarning,
              BytesWarning]

def cleanup():
    '''Clean up after possible incomplete test runs.'''
    global EXPECTED
    EXPECTED = []


def expect(warn_type, message):
    '''Helper for test output'''
    for filter in _warnings.filters:
        if filter[0] == "ignore" and issubclass(warn_type, filter[2]):
            return
    EXPECTED.append(": " + warn_type.__name__ + ": " + message + "\n")


class _WarningsTest(IronPythonTestCase):
    #TODO: @skip("multiple_execute")
    def test_sanity(self):
        global EXPECTED
        try:
            with stderr_trapper() as output:
                # generate test output
                _warnings.warn("Warning Message!")
                expect(UserWarning, "Warning Message!")
                for warn_type in WARN_TYPES:
                    _warnings.warn(warn_type("Type-overriding message!"), UnicodeWarning)
                    expect(warn_type, "Type-overriding message!")
                    _warnings.warn("Another Warning Message!", warn_type)
                    expect(warn_type, "Another Warning Message!")
                    _warnings.warn_explicit("Explicit Warning!", warn_type, "nonexistent_file.py", 12)
                    expect(warn_type, "Explicit Warning!")
                    _warnings.warn_explicit("Explicit Warning!", warn_type, "test_python26.py", 34)
                    expect(warn_type, "Explicit Warning!")
                    _warnings.warn_explicit("Explicit Warning!", warn_type, "nonexistent_file.py", 56, "module.py")
                    expect(warn_type, "Explicit Warning!")
                    _warnings.warn_explicit("Explicit Warning!", warn_type, "test_python26.py", 78, "module.py")
                    expect(warn_type, "Explicit Warning!")

            temp_messages = output.messages

            #No point in going further if the number of lines is not what we expect
            nlines = len([x for x in temp_messages if not x.startswith("  ")])
            self.assertEqual(nlines, len(EXPECTED))

            # match lines
            for line in temp_messages:
                if line.startswith("  "):
                    continue
                temp = EXPECTED.pop(0).rstrip()
                self.assertTrue(line.endswith(temp), str(line) + " does not end with " + temp)

        finally:
            # remove generated files
            cleanup()

    def test_gh23(self):
        def test():
            object.__init__(object, None)
        self.assertWarns(DeprecationWarning, test)


    def test_default_action(self):
        print("TODO")

    def test_filters(self):
        print("TODO")

    def test_once_registry(self):
        print("TODO")

    def test_warn(self):
        print("TODO")

    def test_warn_explicit(self):
        print("TODO")

run_test(__name__)
