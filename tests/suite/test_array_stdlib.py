# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_array from StdLib
##

from iptest import is_ironpython, generate_suite, run_test, is_windows

import test.test_array

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(test.test_array, pattern=pattern)

    if is_ironpython:
        failing_tests = [
            test.test_array.ByteTest('test_free_after_iterating'), # https://github.com/IronLanguages/ironpython3/issues/767
            test.test_array.ByteTest('test_overflow'), # https://github.com/IronLanguages/ironpython3/issues/767
            test.test_array.ByteTest('test_subclass_with_kwargs'), # https://github.com/IronLanguages/ironpython3/issues/767
            test.test_array.ByteTest('test_type_error'), # https://github.com/IronLanguages/ironpython3/issues/767
            test.test_array.DoubleTest('test_free_after_iterating'), # https://github.com/IronLanguages/ironpython3/issues/767
            test.test_array.DoubleTest('test_subclass_with_kwargs'), # https://github.com/IronLanguages/ironpython3/issues/767
            test.test_array.FloatTest('test_free_after_iterating'), # https://github.com/IronLanguages/ironpython3/issues/767
            test.test_array.FloatTest('test_subclass_with_kwargs'), # https://github.com/IronLanguages/ironpython3/issues/767
            test.test_array.IntTest('test_free_after_iterating'), # https://github.com/IronLanguages/ironpython3/issues/767
            test.test_array.IntTest('test_subclass_with_kwargs'), # https://github.com/IronLanguages/ironpython3/issues/767
            test.test_array.IntTest('test_type_error'), # https://github.com/IronLanguages/ironpython3/issues/767
            test.test_array.LongLongTest('test_free_after_iterating'), # https://github.com/IronLanguages/ironpython3/issues/767
            test.test_array.LongLongTest('test_overflow'), # https://github.com/IronLanguages/ironpython3/issues/767
            test.test_array.LongLongTest('test_subclass_with_kwargs'), # https://github.com/IronLanguages/ironpython3/issues/767
            test.test_array.LongLongTest('test_type_error'), # https://github.com/IronLanguages/ironpython3/issues/767
            test.test_array.LongTest('test_free_after_iterating'), # https://github.com/IronLanguages/ironpython3/issues/767
            test.test_array.LongTest('test_subclass_with_kwargs'), # https://github.com/IronLanguages/ironpython3/issues/767
            test.test_array.LongTest('test_type_error'), # https://github.com/IronLanguages/ironpython3/issues/767
            test.test_array.ShortTest('test_free_after_iterating'), # https://github.com/IronLanguages/ironpython3/issues/767
            test.test_array.ShortTest('test_overflow'), # https://github.com/IronLanguages/ironpython3/issues/767
            test.test_array.ShortTest('test_subclass_with_kwargs'), # https://github.com/IronLanguages/ironpython3/issues/767
            test.test_array.ShortTest('test_type_error'), # https://github.com/IronLanguages/ironpython3/issues/767
            test.test_array.UnicodeTest('test_free_after_iterating'), # https://github.com/IronLanguages/ironpython3/issues/767
            test.test_array.UnicodeTest('test_subclass_with_kwargs'), # https://github.com/IronLanguages/ironpython3/issues/767
            test.test_array.UnsignedByteTest('test_free_after_iterating'), # https://github.com/IronLanguages/ironpython3/issues/767
            test.test_array.UnsignedByteTest('test_overflow'), # https://github.com/IronLanguages/ironpython3/issues/767
            test.test_array.UnsignedByteTest('test_subclass_with_kwargs'), # https://github.com/IronLanguages/ironpython3/issues/767
            test.test_array.UnsignedByteTest('test_type_error'), # https://github.com/IronLanguages/ironpython3/issues/767
            test.test_array.UnsignedIntTest('test_free_after_iterating'), # https://github.com/IronLanguages/ironpython3/issues/767
            test.test_array.UnsignedIntTest('test_overflow'), # https://github.com/IronLanguages/ironpython3/issues/767
            test.test_array.UnsignedIntTest('test_subclass_with_kwargs'), # https://github.com/IronLanguages/ironpython3/issues/767
            test.test_array.UnsignedIntTest('test_type_error'), # https://github.com/IronLanguages/ironpython3/issues/767
            test.test_array.UnsignedLongLongTest('test_free_after_iterating'), # https://github.com/IronLanguages/ironpython3/issues/767
            test.test_array.UnsignedLongLongTest('test_overflow'), # https://github.com/IronLanguages/ironpython3/issues/767)
            test.test_array.UnsignedLongLongTest('test_subclass_with_kwargs'), # https://github.com/IronLanguages/ironpython3/issues/767
            test.test_array.UnsignedLongLongTest('test_type_error'), # https://github.com/IronLanguages/ironpython3/issues/767
            test.test_array.UnsignedLongTest('test_free_after_iterating'), # https://github.com/IronLanguages/ironpython3/issues/767
            test.test_array.UnsignedLongTest('test_overflow'), # https://github.com/IronLanguages/ironpython3/issues/767
            test.test_array.UnsignedLongTest('test_subclass_with_kwargs'), # https://github.com/IronLanguages/ironpython3/issues/767
            test.test_array.UnsignedLongTest('test_type_error'), # https://github.com/IronLanguages/ironpython3/issues/767
            test.test_array.UnsignedShortTest('test_free_after_iterating'), # https://github.com/IronLanguages/ironpython3/issues/767
            test.test_array.UnsignedShortTest('test_overflow'), # https://github.com/IronLanguages/ironpython3/issues/767
            test.test_array.UnsignedShortTest('test_subclass_with_kwargs'), # https://github.com/IronLanguages/ironpython3/issues/767
            test.test_array.UnsignedShortTest('test_type_error'), # https://github.com/IronLanguages/ironpython3/issues/767
        ]

        if not is_windows:
            failing_tests += [
                test.test_array.LongTest('test_overflow'), # OverflowError: couldn't convert Intable to Int64
            ]

        return generate_suite(tests, failing_tests)

    else:
        return tests

run_test(__name__)
