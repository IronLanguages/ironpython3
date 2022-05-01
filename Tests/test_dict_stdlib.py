# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_dict from StdLib
##

import unittest
import sys

from iptest import run_test, is_mono

import test.test_dict

def load_tests(loader, standard_tests, pattern):
    if sys.implementation.name == 'ironpython':
        suite = unittest.TestSuite()
        suite.addTest(test.test_dict.CAPITest('test_getitem_knownhash'))
        suite.addTest(test.test_dict.DictTest('test_bad_key'))
        suite.addTest(test.test_dict.DictTest('test_bool'))
        suite.addTest(test.test_dict.DictTest('test_clear'))
        suite.addTest(test.test_dict.DictTest('test_constructor'))
        if is_mono:
            suite.addTest(unittest.expectedFailure(test.test_dict.DictTest('test_container_iterator'))) # https://github.com/IronLanguages/ironpython3/issues/544
        else:
            suite.addTest(test.test_dict.DictTest('test_container_iterator'))
        suite.addTest(test.test_dict.DictTest('test_contains'))
        suite.addTest(test.test_dict.DictTest('test_copy'))
        suite.addTest(test.test_dict.DictTest('test_dict_copy_order'))
        suite.addTest(test.test_dict.DictTest('test_dictitems_contains_use_after_free'))
        suite.addTest(test.test_dict.DictTest('test_dictview_mixed_set_operations'))
        suite.addTest(test.test_dict.DictTest('test_dictview_set_operations_on_items'))
        suite.addTest(test.test_dict.DictTest('test_dictview_set_operations_on_keys'))
        suite.addTest(test.test_dict.DictTest('test_empty_presized_dict_in_freelist'))
        suite.addTest(test.test_dict.DictTest('test_eq'))
        suite.addTest(unittest.expectedFailure(test.test_dict.DictTest('test_equal_operator_modifying_operand')))
        suite.addTest(unittest.expectedFailure(test.test_dict.DictTest('test_errors_in_view_containment_check')))
        suite.addTest(test.test_dict.DictTest('test_free_after_iterating'))
        suite.addTest(test.test_dict.DictTest('test_fromkeys'))
        suite.addTest(test.test_dict.DictTest('test_fromkeys_operator_modifying_dict_operand'))
        suite.addTest(test.test_dict.DictTest('test_fromkeys_operator_modifying_set_operand'))
        suite.addTest(test.test_dict.DictTest('test_get'))
        suite.addTest(test.test_dict.DictTest('test_getitem'))
        suite.addTest(test.test_dict.DictTest('test_init_use_after_free'))
        suite.addTest(unittest.expectedFailure(test.test_dict.DictTest('test_instance_dict_getattr_str_subclass')))
        suite.addTest(test.test_dict.DictTest('test_invalid_keyword_arguments'))
        suite.addTest(unittest.expectedFailure(test.test_dict.DictTest('test_itemiterator_pickling')))
        suite.addTest(test.test_dict.DictTest('test_items'))
        suite.addTest(test.test_dict.DictTest('test_iterator_pickling'))
        suite.addTest(test.test_dict.DictTest('test_keys'))
        suite.addTest(test.test_dict.DictTest('test_keys_contained'))
        suite.addTest(test.test_dict.DictTest('test_len'))
        suite.addTest(test.test_dict.DictTest('test_literal_constructor'))
        suite.addTest(unittest.expectedFailure(test.test_dict.DictTest('test_merge_and_mutate')))
        suite.addTest(test.test_dict.DictTest('test_missing'))
        suite.addTest(test.test_dict.DictTest('test_mutating_iteration'))
        suite.addTest(test.test_dict.DictTest('test_mutating_lookup'))
        suite.addTest(test.test_dict.DictTest('test_object_set_item_single_instance_non_str_key'))
        suite.addTest(unittest.expectedFailure(test.test_dict.DictTest('test_oob_indexing_dictiter_iternextitem')))
        suite.addTest(test.test_dict.DictTest('test_pop'))
        suite.addTest(test.test_dict.DictTest('test_popitem'))
        suite.addTest(test.test_dict.DictTest('test_reentrant_insertion'))
        suite.addTest(test.test_dict.DictTest('test_repr'))
        suite.addTest(test.test_dict.DictTest('test_repr_deep'))
        suite.addTest(test.test_dict.DictTest('test_resize1'))
        suite.addTest(test.test_dict.DictTest('test_resize2'))
        suite.addTest(test.test_dict.DictTest('test_setdefault'))
        suite.addTest(unittest.expectedFailure(test.test_dict.DictTest('test_setdefault_atomic')))
        suite.addTest(test.test_dict.DictTest('test_setitem_atomic_at_resize'))
        suite.addTest(test.test_dict.DictTest('test_splittable_del'))
        suite.addTest(test.test_dict.DictTest('test_splittable_pop'))
        suite.addTest(test.test_dict.DictTest('test_splittable_pop_pending'))
        suite.addTest(test.test_dict.DictTest('test_splittable_popitem'))
        suite.addTest(test.test_dict.DictTest('test_splittable_setattr_after_pop'))
        suite.addTest(test.test_dict.DictTest('test_splittable_setdefault'))
        suite.addTest(test.test_dict.DictTest('test_track_dynamic'))
        suite.addTest(test.test_dict.DictTest('test_track_literals'))
        suite.addTest(test.test_dict.DictTest('test_track_subtypes'))
        suite.addTest(test.test_dict.DictTest('test_tuple_keyerror'))
        suite.addTest(test.test_dict.DictTest('test_update'))
        suite.addTest(test.test_dict.DictTest('test_values'))
        suite.addTest(test.test_dict.DictTest('test_valuesiterator_pickling'))
        suite.addTest(test.test_dict.GeneralMappingTests('test_bool'))
        suite.addTest(test.test_dict.GeneralMappingTests('test_constructor'))
        suite.addTest(test.test_dict.GeneralMappingTests('test_get'))
        suite.addTest(test.test_dict.GeneralMappingTests('test_getitem'))
        suite.addTest(test.test_dict.GeneralMappingTests('test_items'))
        suite.addTest(test.test_dict.GeneralMappingTests('test_keys'))
        suite.addTest(test.test_dict.GeneralMappingTests('test_len'))
        suite.addTest(test.test_dict.GeneralMappingTests('test_pop'))
        suite.addTest(test.test_dict.GeneralMappingTests('test_popitem'))
        suite.addTest(test.test_dict.GeneralMappingTests('test_read'))
        suite.addTest(test.test_dict.GeneralMappingTests('test_setdefault'))
        suite.addTest(test.test_dict.GeneralMappingTests('test_update'))
        suite.addTest(test.test_dict.GeneralMappingTests('test_values'))
        suite.addTest(test.test_dict.GeneralMappingTests('test_write'))
        suite.addTest(test.test_dict.SubclassMappingTests('test_bool'))
        suite.addTest(test.test_dict.SubclassMappingTests('test_constructor'))
        suite.addTest(test.test_dict.SubclassMappingTests('test_get'))
        suite.addTest(test.test_dict.SubclassMappingTests('test_getitem'))
        suite.addTest(test.test_dict.SubclassMappingTests('test_items'))
        suite.addTest(test.test_dict.SubclassMappingTests('test_keys'))
        suite.addTest(test.test_dict.SubclassMappingTests('test_len'))
        suite.addTest(test.test_dict.SubclassMappingTests('test_pop'))
        suite.addTest(test.test_dict.SubclassMappingTests('test_popitem'))
        suite.addTest(test.test_dict.SubclassMappingTests('test_read'))
        suite.addTest(test.test_dict.SubclassMappingTests('test_setdefault'))
        suite.addTest(test.test_dict.SubclassMappingTests('test_update'))
        suite.addTest(test.test_dict.SubclassMappingTests('test_values'))
        suite.addTest(test.test_dict.SubclassMappingTests('test_write'))
        return suite

    else:
        return loader.loadTestsFromModule(test.test_dict, pattern)

run_test(__name__)
