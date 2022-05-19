# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_itertools from StdLib
##

import unittest
import sys

from iptest import run_test

import test.test_itertools

def load_tests(loader, standard_tests, pattern):
    if sys.implementation.name == 'ironpython':
        suite = unittest.TestSuite()
        suite.addTest(test.test_itertools.LengthTransparency('test_repeat'))
        suite.addTest(test.test_itertools.LengthTransparency('test_repeat_with_negative_times'))
        suite.addTest(test.test_itertools.RegressionTests('test_issue30347_1'))
        suite.addTest(test.test_itertools.RegressionTests('test_issue30347_2'))
        suite.addTest(test.test_itertools.RegressionTests('test_long_chain_of_empty_iterables'))
        suite.addTest(test.test_itertools.RegressionTests('test_sf_793826'))
        suite.addTest(test.test_itertools.RegressionTests('test_sf_950057'))
        suite.addTest(test.test_itertools.SizeofTest('test_combinations_sizeof'))
        suite.addTest(test.test_itertools.SizeofTest('test_combinations_with_replacement_sizeof'))
        suite.addTest(test.test_itertools.SizeofTest('test_permutations_sizeof'))
        suite.addTest(test.test_itertools.SizeofTest('test_product_sizeof'))
        suite.addTest(test.test_itertools.SubclassWithKwargsTest('test_keywords_in_subclass'))
        suite.addTest(test.test_itertools.TestBasicOps('test_StopIteration'))
        suite.addTest(test.test_itertools.TestBasicOps('test_accumulate'))
        suite.addTest(test.test_itertools.TestBasicOps('test_bug_7244'))
        suite.addTest(test.test_itertools.TestBasicOps('test_chain'))
        suite.addTest(test.test_itertools.TestBasicOps('test_chain_from_iterable'))
        suite.addTest(test.test_itertools.TestBasicOps('test_chain_reducible'))
        suite.addTest(test.test_itertools.TestBasicOps('test_chain_setstate'))
        suite.addTest(unittest.expectedFailure(test.test_itertools.TestBasicOps('test_combinations'))) # pickling
        suite.addTest(test.test_itertools.TestBasicOps('test_combinations_overflow'))
        suite.addTest(test.test_itertools.TestBasicOps('test_combinations_tuple_reuse'))
        suite.addTest(unittest.expectedFailure(test.test_itertools.TestBasicOps('test_combinations_with_replacement'))) # pickling
        suite.addTest(test.test_itertools.TestBasicOps('test_combinations_with_replacement_overflow'))
        suite.addTest(test.test_itertools.TestBasicOps('test_combinations_with_replacement_tuple_reuse'))
        suite.addTest(test.test_itertools.TestBasicOps('test_combinatorics'))
        suite.addTest(unittest.expectedFailure(test.test_itertools.TestBasicOps('test_compress'))) # pickling
        suite.addTest(test.test_itertools.TestBasicOps('test_count'))
        suite.addTest(test.test_itertools.TestBasicOps('test_count_with_stride'))
        suite.addTest(unittest.expectedFailure(test.test_itertools.TestBasicOps('test_cycle'))) # pickling
        suite.addTest(unittest.expectedFailure(test.test_itertools.TestBasicOps('test_cycle_setstate'))) # __setstate__ not implemented
        suite.addTest(unittest.expectedFailure(test.test_itertools.TestBasicOps('test_dropwhile'))) # pickling
        suite.addTest(test.test_itertools.TestBasicOps('test_filter'))
        suite.addTest(unittest.expectedFailure(test.test_itertools.TestBasicOps('test_filterfalse'))) # pickling
        suite.addTest(unittest.expectedFailure(test.test_itertools.TestBasicOps('test_groupby'))) # pickling
        suite.addTest(unittest.expectedFailure(test.test_itertools.TestBasicOps('test_islice'))) # pickling
        suite.addTest(test.test_itertools.TestBasicOps('test_map'))
        suite.addTest(unittest.expectedFailure(test.test_itertools.TestBasicOps('test_permutations'))) # pickling
        suite.addTest(test.test_itertools.TestBasicOps('test_permutations_overflow'))
        suite.addTest(test.test_itertools.TestBasicOps('test_permutations_tuple_reuse'))
        suite.addTest(test.test_itertools.TestBasicOps('test_product'))
        suite.addTest(unittest.expectedFailure(test.test_itertools.TestBasicOps('test_product_issue_25021')))
        suite.addTest(test.test_itertools.TestBasicOps('test_product_overflow'))
        suite.addTest(unittest.expectedFailure(test.test_itertools.TestBasicOps('test_product_pickling')))
        suite.addTest(test.test_itertools.TestBasicOps('test_product_tuple_reuse'))
        suite.addTest(unittest.expectedFailure(test.test_itertools.TestBasicOps('test_repeat'))) # pickling
        suite.addTest(unittest.expectedFailure(test.test_itertools.TestBasicOps('test_repeat_with_negative_times')))
        suite.addTest(unittest.expectedFailure(test.test_itertools.TestBasicOps('test_starmap'))) # pickling
        suite.addTest(unittest.expectedFailure(test.test_itertools.TestBasicOps('test_takewhile'))) # pickling
        suite.addTest(unittest.expectedFailure(test.test_itertools.TestBasicOps('test_tee')))
        suite.addTest(test.test_itertools.TestBasicOps('test_tee_del_backward'))
        suite.addTest(test.test_itertools.TestBasicOps('test_zip'))
        suite.addTest(unittest.expectedFailure(test.test_itertools.TestBasicOps('test_zip_longest_pickling')))
        suite.addTest(test.test_itertools.TestBasicOps('test_zip_longest_tuple_reuse'))
        suite.addTest(test.test_itertools.TestBasicOps('test_zip_tuple_reuse'))
        suite.addTest(test.test_itertools.TestBasicOps('test_ziplongest'))
        suite.addTest(test.test_itertools.TestExamples('test_accumulate'))
        suite.addTest(test.test_itertools.TestExamples('test_accumulate_reducible'))
        suite.addTest(unittest.expectedFailure(test.test_itertools.TestExamples('test_accumulate_reducible_none'))) # pickling
        suite.addTest(test.test_itertools.TestExamples('test_chain'))
        suite.addTest(test.test_itertools.TestExamples('test_chain_from_iterable'))
        suite.addTest(test.test_itertools.TestExamples('test_combinations'))
        suite.addTest(test.test_itertools.TestExamples('test_combinations_with_replacement'))
        suite.addTest(test.test_itertools.TestExamples('test_compress'))
        suite.addTest(test.test_itertools.TestExamples('test_count'))
        suite.addTest(test.test_itertools.TestExamples('test_cycle'))
        suite.addTest(test.test_itertools.TestExamples('test_dropwhile'))
        suite.addTest(test.test_itertools.TestExamples('test_filter'))
        suite.addTest(test.test_itertools.TestExamples('test_filterfalse'))
        suite.addTest(test.test_itertools.TestExamples('test_groupby'))
        suite.addTest(test.test_itertools.TestExamples('test_islice'))
        suite.addTest(test.test_itertools.TestExamples('test_map'))
        suite.addTest(test.test_itertools.TestExamples('test_permutations'))
        suite.addTest(test.test_itertools.TestExamples('test_product'))
        suite.addTest(test.test_itertools.TestExamples('test_repeat'))
        suite.addTest(test.test_itertools.TestExamples('test_stapmap'))
        suite.addTest(test.test_itertools.TestExamples('test_takewhile'))
        suite.addTest(test.test_itertools.TestExamples('test_zip'))
        suite.addTest(test.test_itertools.TestExamples('test_zip_longest'))
        suite.addTest(test.test_itertools.TestGC('test_accumulate'))
        suite.addTest(test.test_itertools.TestGC('test_chain'))
        suite.addTest(test.test_itertools.TestGC('test_chain_from_iterable'))
        suite.addTest(test.test_itertools.TestGC('test_combinations'))
        suite.addTest(test.test_itertools.TestGC('test_combinations_with_replacement'))
        suite.addTest(test.test_itertools.TestGC('test_compress'))
        suite.addTest(test.test_itertools.TestGC('test_count'))
        suite.addTest(test.test_itertools.TestGC('test_cycle'))
        suite.addTest(test.test_itertools.TestGC('test_dropwhile'))
        suite.addTest(test.test_itertools.TestGC('test_filter'))
        suite.addTest(test.test_itertools.TestGC('test_filterfalse'))
        suite.addTest(test.test_itertools.TestGC('test_groupby'))
        suite.addTest(test.test_itertools.TestGC('test_islice'))
        suite.addTest(test.test_itertools.TestGC('test_issue2246'))
        suite.addTest(test.test_itertools.TestGC('test_map'))
        suite.addTest(test.test_itertools.TestGC('test_permutations'))
        suite.addTest(test.test_itertools.TestGC('test_product'))
        suite.addTest(test.test_itertools.TestGC('test_repeat'))
        suite.addTest(test.test_itertools.TestGC('test_starmap'))
        suite.addTest(test.test_itertools.TestGC('test_takewhile'))
        suite.addTest(test.test_itertools.TestGC('test_zip'))
        suite.addTest(test.test_itertools.TestGC('test_zip_longest'))
        suite.addTest(test.test_itertools.TestPurePythonRoughEquivalents('test_islice_recipe'))
        suite.addTest(test.test_itertools.TestVariousIteratorArgs('test_accumulate'))
        suite.addTest(test.test_itertools.TestVariousIteratorArgs('test_chain'))
        suite.addTest(unittest.expectedFailure(test.test_itertools.TestVariousIteratorArgs('test_compress')))
        suite.addTest(test.test_itertools.TestVariousIteratorArgs('test_cycle'))
        suite.addTest(test.test_itertools.TestVariousIteratorArgs('test_dropwhile'))
        suite.addTest(test.test_itertools.TestVariousIteratorArgs('test_filter'))
        suite.addTest(test.test_itertools.TestVariousIteratorArgs('test_filterfalse'))
        suite.addTest(test.test_itertools.TestVariousIteratorArgs('test_groupby'))
        suite.addTest(test.test_itertools.TestVariousIteratorArgs('test_islice'))
        suite.addTest(test.test_itertools.TestVariousIteratorArgs('test_map'))
        suite.addTest(test.test_itertools.TestVariousIteratorArgs('test_product'))
        suite.addTest(test.test_itertools.TestVariousIteratorArgs('test_starmap'))
        suite.addTest(test.test_itertools.TestVariousIteratorArgs('test_takewhile'))
        suite.addTest(test.test_itertools.TestVariousIteratorArgs('test_tee'))
        suite.addTest(test.test_itertools.TestVariousIteratorArgs('test_zip'))
        suite.addTest(test.test_itertools.TestVariousIteratorArgs('test_ziplongest'))
        return suite

    else:
        return loader.loadTestsFromModule(test.test_itertools, pattern)

run_test(__name__)
