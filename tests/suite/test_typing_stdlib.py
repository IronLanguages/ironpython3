# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_typing from StdLib
##

from iptest import is_ironpython, generate_suite, run_test

import test.test_typing

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(test.test_typing)

    if is_ironpython:
        failing_tests = [
            test.test_typing.GenericTests('test_generic_hashes'), # https://github.com/IronLanguages/ironpython3/issues/30
            test.test_typing.GenericTests('test_repr_2'), # https://github.com/IronLanguages/ironpython3/issues/30
            test.test_typing.GenericTests('test_parameterized_slots_dict'), # TypeError: slots must be one string or a list of strings
            test.test_typing.GenericTests('test_type_erasure_special'), # TypeError: Parameterized Tuple cannot be used with isinstance().
            test.test_typing.IOTests('test_io_submodule'), # ImportError: Cannot import name __name__
            test.test_typing.RETests('test_basics'), # TypeError: issubclass(): _TypeAlias is not a class nor a tuple of classes
            test.test_typing.RETests('test_cannot_subclass'), # AssertionError
            test.test_typing.RETests('test_re_submodule'), # ImportError: Cannot import name __name__

            # TypeError: Parameterized generics cannot be used with class or instance checks
            test.test_typing.CollectionsAbcTests('test_chainmap_instantiation'),
            test.test_typing.CollectionsAbcTests('test_counter_instantiation'),
            test.test_typing.CollectionsAbcTests('test_defaultdict_instantiation'),
            test.test_typing.CollectionsAbcTests('test_deque_instantiation'),
            test.test_typing.CollectionsAbcTests('test_subclassing'),
            test.test_typing.GenericTests('test_copy_generic_instances'),
            test.test_typing.GenericTests('test_naive_runtime_checks'),
            test.test_typing.GenericTests('test_parameterized_slots'),
            test.test_typing.GenericTests('test_subscripted_generics_as_proxies'),
            test.test_typing.GenericTests('test_substitution_helper'),
            test.test_typing.GenericTests('test_type_erasure'),
        ]

        skip_tests = []

        return generate_suite(tests, failing_tests, skip_tests)

    else:
        return tests

run_test(__name__)
