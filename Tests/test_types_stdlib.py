# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_types from StdLib
##

from iptest import is_ironpython, generate_suite, run_test, is_linux, is_netcoreapp21

import test.test_types

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(test.test_types, pattern=pattern)

    if is_ironpython:
        failing_tests = [
            test.test_types.ClassCreationTests('test_bad___prepare__'), # AssertionError
            test.test_types.ClassCreationTests('test_one_argument_type'), # AssertionError: TypeError not raised
            test.test_types.CoroutineTests('test_async_def'), # https://github.com/IronLanguages/ironpython3/issues/98
            test.test_types.CoroutineTests('test_duck_coro'), # https://github.com/IronLanguages/ironpython3/issues/98
            test.test_types.CoroutineTests('test_duck_corogen'), # https://github.com/IronLanguages/ironpython3/issues/98
            test.test_types.CoroutineTests('test_duck_functional_gen'), # https://github.com/IronLanguages/ironpython3/issues/98
            test.test_types.CoroutineTests('test_duck_gen'), # https://github.com/IronLanguages/ironpython3/issues/98
            test.test_types.CoroutineTests('test_gen'), # https://github.com/IronLanguages/ironpython3/issues/98
            test.test_types.CoroutineTests('test_genfunc'), # https://github.com/IronLanguages/ironpython3/issues/98
            test.test_types.CoroutineTests('test_non_gen_values'), # https://github.com/IronLanguages/ironpython3/issues/98
            test.test_types.CoroutineTests('test_returning_itercoro'), # https://github.com/IronLanguages/ironpython3/issues/98
            test.test_types.CoroutineTests('test_wrapper_object'), # https://github.com/IronLanguages/ironpython3/issues/98
            test.test_types.MappingProxyTests('test_chainmap'), # TypeError: expected dict, got Object_1$1
            test.test_types.MappingProxyTests('test_constructor'), # TypeError: expected dict, got Object_1$1
            test.test_types.MappingProxyTests('test_customdict'), # AssertionError: False is not true
            test.test_types.MappingProxyTests('test_missing'), # AssertionError: 'missing=y' != None
            test.test_types.SimpleNamespaceTests('test_attrdel'), # KeyError: spam
            test.test_types.SimpleNamespaceTests('test_pickle'), # TypeError: protocol 0
            test.test_types.TypesTests('test_float__format__'), # AssertionError: '1.12339e+200' != '1.1234e+200'
            test.test_types.TypesTests('test_internal_sizes'), # AttributeError: 'type' object has no attribute '__basicsize__'
        ]

        return generate_suite(tests, failing_tests)

    else:
        return tests

run_test(__name__)
