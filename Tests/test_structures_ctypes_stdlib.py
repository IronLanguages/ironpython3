# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_structures from StdLib
##

import unittest
import sys

from iptest import is_osx, run_test

import ctypes.test.test_structures

def load_tests(loader, standard_tests, pattern):
    if sys.implementation.name == 'ironpython':
        suite = unittest.TestSuite()
        suite.addTest(ctypes.test.test_structures.PointerMemberTestCase('test'))
        suite.addTest(ctypes.test.test_structures.PointerMemberTestCase('test_none_to_pointer_fields'))
        suite.addTest(ctypes.test.test_structures.StructureTestCase('test_abstract_class'))
        suite.addTest(unittest.expectedFailure(ctypes.test.test_structures.StructureTestCase('test_conflicting_initializers'))) # AssertionError
        suite.addTest(ctypes.test.test_structures.StructureTestCase('test_empty'))
        suite.addTest(ctypes.test.test_structures.StructureTestCase('test_fields'))
        suite.addTest(ctypes.test.test_structures.StructureTestCase('test_huge_field_name'))
        suite.addTest(ctypes.test.test_structures.StructureTestCase('test_init_errors'))
        suite.addTest(ctypes.test.test_structures.StructureTestCase('test_initializers'))
        suite.addTest(ctypes.test.test_structures.StructureTestCase('test_intarray_fields'))
        suite.addTest(ctypes.test.test_structures.StructureTestCase('test_invalid_field_types'))
        suite.addTest(ctypes.test.test_structures.StructureTestCase('test_invalid_name'))
        suite.addTest(ctypes.test.test_structures.StructureTestCase('test_keyword_initializers'))
        suite.addTest(ctypes.test.test_structures.StructureTestCase('test_methods'))
        suite.addTest(ctypes.test.test_structures.StructureTestCase('test_nested_initializers'))
        suite.addTest(ctypes.test.test_structures.StructureTestCase('test_packed'))
        suite.addTest(ctypes.test.test_structures.StructureTestCase('test_packed_c_limits'))
        if is_osx:
            suite.addTest(unittest.expectedFailure(ctypes.test.test_structures.StructureTestCase('test_pass_by_value')))
        else:
            suite.addTest(ctypes.test.test_structures.StructureTestCase('test_pass_by_value'))
        suite.addTest(unittest.expectedFailure(ctypes.test.test_structures.StructureTestCase('test_pass_by_value_in_register'))) # NotImplementedError: in dll
        suite.addTest(ctypes.test.test_structures.StructureTestCase('test_positional_args'))
        suite.addTest(ctypes.test.test_structures.StructureTestCase('test_simple_structs'))
        suite.addTest(ctypes.test.test_structures.StructureTestCase('test_struct_alignment'))
        suite.addTest(ctypes.test.test_structures.StructureTestCase('test_structures_with_wchar'))
        suite.addTest(ctypes.test.test_structures.StructureTestCase('test_subclass_creation'))
        suite.addTest(ctypes.test.test_structures.StructureTestCase('test_unions'))
        suite.addTest(ctypes.test.test_structures.SubclassesTest('test_subclass'))
        suite.addTest(ctypes.test.test_structures.SubclassesTest('test_subclass_delayed'))
        suite.addTest(ctypes.test.test_structures.TestRecursiveStructure('test_contains_itself'))
        suite.addTest(ctypes.test.test_structures.TestRecursiveStructure('test_vice_versa'))
        return suite

    else:
        return loader.loadTestsFromModule(ctypes.test.test_structures, pattern)

run_test(__name__)
