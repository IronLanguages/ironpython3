# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_userlist from StdLib
##

import unittest
import sys

from iptest import run_test

import test.test_userlist

def load_tests(loader, standard_tests, pattern):
    if sys.implementation.name == 'ironpython':
        suite = unittest.TestSuite()
        suite.addTest(test.test_userlist.UserListTest('test_add_specials'))
        suite.addTest(test.test_userlist.UserListTest('test_addmul'))
        suite.addTest(test.test_userlist.UserListTest('test_append'))
        suite.addTest(test.test_userlist.UserListTest('test_bigrepeat'))
        suite.addTest(test.test_userlist.UserListTest('test_clear'))
        suite.addTest(test.test_userlist.UserListTest('test_constructor_exception_handling'))
        suite.addTest(test.test_userlist.UserListTest('test_constructors'))
        suite.addTest(test.test_userlist.UserListTest('test_contains'))
        suite.addTest(test.test_userlist.UserListTest('test_contains_fake'))
        suite.addTest(test.test_userlist.UserListTest('test_contains_order'))
        suite.addTest(test.test_userlist.UserListTest('test_copy'))
        suite.addTest(test.test_userlist.UserListTest('test_count'))
        suite.addTest(test.test_userlist.UserListTest('test_delitem'))
        suite.addTest(test.test_userlist.UserListTest('test_delslice'))
        suite.addTest(test.test_userlist.UserListTest('test_exhausted_iterator'))
        suite.addTest(test.test_userlist.UserListTest('test_extend'))
        suite.addTest(test.test_userlist.UserListTest('test_extendedslicing'))
        suite.addTest(unittest.expectedFailure(test.test_userlist.UserListTest('test_free_after_iterating'))) # https://github.com/IronLanguages/ironpython3/issues/1246
        suite.addTest(test.test_userlist.UserListTest('test_getitem'))
        suite.addTest(test.test_userlist.UserListTest('test_getitem_error'))
        suite.addTest(test.test_userlist.UserListTest('test_getitemoverwriteiter'))
        suite.addTest(test.test_userlist.UserListTest('test_getslice'))
        suite.addTest(test.test_userlist.UserListTest('test_iadd'))
        suite.addTest(test.test_userlist.UserListTest('test_imul'))
        suite.addTest(test.test_userlist.UserListTest('test_index'))
        suite.addTest(test.test_userlist.UserListTest('test_init'))
        suite.addTest(test.test_userlist.UserListTest('test_insert'))
        suite.addTest(test.test_userlist.UserListTest('test_len'))
        suite.addTest(test.test_userlist.UserListTest('test_minmax'))
        suite.addTest(test.test_userlist.UserListTest('test_mixedadd'))
        suite.addTest(test.test_userlist.UserListTest('test_mixedcmp'))
        suite.addTest(test.test_userlist.UserListTest('test_pickle'))
        suite.addTest(test.test_userlist.UserListTest('test_pop'))
        suite.addTest(test.test_userlist.UserListTest('test_print'))
        suite.addTest(test.test_userlist.UserListTest('test_radd_specials'))
        suite.addTest(test.test_userlist.UserListTest('test_remove'))
        suite.addTest(test.test_userlist.UserListTest('test_repeat'))
        suite.addTest(test.test_userlist.UserListTest('test_repr'))
        suite.addTest(test.test_userlist.UserListTest('test_repr_deep'))
        suite.addTest(test.test_userlist.UserListTest('test_reverse'))
        suite.addTest(test.test_userlist.UserListTest('test_reversed'))
        suite.addTest(test.test_userlist.UserListTest('test_set_subscript'))
        suite.addTest(test.test_userlist.UserListTest('test_setitem'))
        suite.addTest(test.test_userlist.UserListTest('test_setslice'))
        suite.addTest(test.test_userlist.UserListTest('test_slice'))
        suite.addTest(test.test_userlist.UserListTest('test_sort'))
        suite.addTest(test.test_userlist.UserListTest('test_subscript'))
        suite.addTest(test.test_userlist.UserListTest('test_truth'))
        suite.addTest(test.test_userlist.UserListTest('test_userlist_copy'))
        return suite

    else:
        return loader.loadTestsFromModule(test.test_userlist, pattern)

run_test(__name__)
