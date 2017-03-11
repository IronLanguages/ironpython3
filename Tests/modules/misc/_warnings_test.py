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
'''
CPython's _warnings module. http://docs.python.org/library/warnings.html
'''

#--IMPORTS---------------------------------------------------------------------
from iptest.assert_util import *
skiptest("silverlight")

import _warnings
from iptest.assert_util import stderr_trapper as output_trapper

#--GLOBALS---------------------------------------------------------------------
EXPECTED = [] # expected output (ignoring filename, lineno, and line)
WARN_TYPES = [Warning, UserWarning, PendingDeprecationWarning, SyntaxWarning, 
              RuntimeWarning, FutureWarning, ImportWarning, UnicodeWarning, 
              BytesWarning]

#--HELPERS---------------------------------------------------------------------
def cleanup():
    '''
    Clean up after possible incomplete test runs.
    '''
    global EXPECTED
    EXPECTED = []
    
    
def expect(warn_type, message):
    '''
    Helper for test output
    '''
    for filter in _warnings.filters:
        if filter[0] == "ignore" and issubclass(warn_type, filter[2]):
            return
    EXPECTED.append(": " + warn_type.__name__ + ": " + message + "\n")


#--TEST CASES------------------------------------------------------------------
@skip("multiple_execute")
def test_sanity():
    global EXPECTED
    try:    
        with output_trapper() as output:
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
        AreEqual(nlines, len(EXPECTED))
        
        # match lines
        for line in temp_messages:
            if line.startswith("  "):
                continue
            temp = EXPECTED.pop(0).rstrip()
            Assert(line.endswith(temp), str(line) + " does not end with " + temp)
    
    finally:
        # remove generated files
        cleanup()


def test_default_action():
    print("TODO")
    
def test_filters():
    print("TODO")
    
def test_once_registry():
    print("TODO")
    
def test_warn():
    print("TODO")
    
def test_warn_explicit():
    print("TODO")


##############################################################################


#--MAIN------------------------------------------------------------------------
run_test(__name__)
