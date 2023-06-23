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
        test.test_typing.AllTests('test_all')

        test.test_typing.AnyTests('test_any_instance_type_error')
        test.test_typing.AnyTests('test_any_subclass_type_error')
        test.test_typing.AnyTests('test_any_works_with_alias')
        test.test_typing.AnyTests('test_cannot_instantiate')
        test.test_typing.AnyTests('test_cannot_subclass')
        test.test_typing.AnyTests('test_errors')
        test.test_typing.AnyTests('test_repr')
        test.test_typing.CallableTests('test_callable_instance_type_error')
        test.test_typing.CallableTests('test_callable_instance_works')
        test.test_typing.CallableTests('test_callable_with_ellipsis')
        test.test_typing.CallableTests('test_callable_wrong_forms')
        test.test_typing.CallableTests('test_cannot_instantiate')
        test.test_typing.CallableTests('test_ellipsis_in_generic')
        test.test_typing.CallableTests('test_eq_hash')
        test.test_typing.CallableTests('test_repr')
        test.test_typing.CallableTests('test_self_subclass')
        test.test_typing.CastTests('test_basics')
        test.test_typing.CastTests('test_errors')
        test.test_typing.ClassVarTests('test_basics')
        test.test_typing.ClassVarTests('test_cannot_init')
        test.test_typing.ClassVarTests('test_cannot_subclass')
        test.test_typing.ClassVarTests('test_no_isinstance')
        test.test_typing.ClassVarTests('test_repr')
        test.test_typing.CollectionsAbcTests('test_abstractset')
        test.test_typing.CollectionsAbcTests('test_async_generator')
        test.test_typing.CollectionsAbcTests('test_async_iterable')
        test.test_typing.CollectionsAbcTests('test_async_iterator')
        test.test_typing.CollectionsAbcTests('test_awaitable')
        test.test_typing.CollectionsAbcTests('test_bytestring')
        test.test_typing.CollectionsAbcTests('test_chainmap_subclass')
        test.test_typing.CollectionsAbcTests('test_collection')
        test.test_typing.CollectionsAbcTests('test_collections_as_base')
        test.test_typing.CollectionsAbcTests('test_container')
        test.test_typing.CollectionsAbcTests('test_coroutine')
        test.test_typing.CollectionsAbcTests('test_counter')
        test.test_typing.CollectionsAbcTests('test_counter_subclass_instantiation')

        test.test_typing.CollectionsAbcTests('test_defaultdict_subclass')
        test.test_typing.CollectionsAbcTests('test_deque')

        test.test_typing.CollectionsAbcTests('test_dict')
        test.test_typing.CollectionsAbcTests('test_dict_subclass')
        test.test_typing.CollectionsAbcTests('test_frozenset')
        test.test_typing.CollectionsAbcTests('test_frozenset_subclass_instantiation')
        test.test_typing.CollectionsAbcTests('test_generator')
        test.test_typing.CollectionsAbcTests('test_hashable')
        test.test_typing.CollectionsAbcTests('test_iterable')
        test.test_typing.CollectionsAbcTests('test_iterator')
        test.test_typing.CollectionsAbcTests('test_list')
        test.test_typing.CollectionsAbcTests('test_list_subclass')
        test.test_typing.CollectionsAbcTests('test_mapping')
        test.test_typing.CollectionsAbcTests('test_mutablemapping')
        test.test_typing.CollectionsAbcTests('test_mutablesequence')
        test.test_typing.CollectionsAbcTests('test_mutableset')
        test.test_typing.CollectionsAbcTests('test_no_async_generator_instantiation')
        test.test_typing.CollectionsAbcTests('test_no_dict_instantiation')
        test.test_typing.CollectionsAbcTests('test_no_frozenset_instantiation')
        test.test_typing.CollectionsAbcTests('test_no_generator_instantiation')
        test.test_typing.CollectionsAbcTests('test_no_list_instantiation')
        test.test_typing.CollectionsAbcTests('test_no_set_instantiation')
        test.test_typing.CollectionsAbcTests('test_no_tuple_instantiation')
        test.test_typing.CollectionsAbcTests('test_sequence')
        test.test_typing.CollectionsAbcTests('test_set')
        test.test_typing.CollectionsAbcTests('test_set_subclass_instantiation')
        test.test_typing.CollectionsAbcTests('test_sized')

        test.test_typing.CollectionsAbcTests('test_subclassing_async_generator')
        test.test_typing.CollectionsAbcTests('test_subclassing_register')
        test.test_typing.CollectionsAbcTests('test_subclassing_subclasshook')
        test.test_typing.ForwardRefTests('test_basics')
        test.test_typing.ForwardRefTests('test_callable_forward')
        test.test_typing.ForwardRefTests('test_callable_with_ellipsis_forward')
        test.test_typing.ForwardRefTests('test_default_globals')
        test.test_typing.ForwardRefTests('test_delayed_syntax_error')
        test.test_typing.ForwardRefTests('test_forward_equality')
        test.test_typing.ForwardRefTests('test_forward_equality_gth')
        test.test_typing.ForwardRefTests('test_forward_equality_hash')
        test.test_typing.ForwardRefTests('test_forward_equality_namespace')
        test.test_typing.ForwardRefTests('test_forward_recursion_actually')
        test.test_typing.ForwardRefTests('test_forward_repr')
        test.test_typing.ForwardRefTests('test_forwardref_instance_type_error')
        test.test_typing.ForwardRefTests('test_forwardref_subclass_type_error')
        test.test_typing.ForwardRefTests('test_meta_no_type_check')
        test.test_typing.ForwardRefTests('test_name_error')
        test.test_typing.ForwardRefTests('test_no_type_check')
        test.test_typing.ForwardRefTests('test_no_type_check_class')
        test.test_typing.ForwardRefTests('test_no_type_check_no_bases')
        test.test_typing.ForwardRefTests('test_syntax_error')
        test.test_typing.ForwardRefTests('test_tuple_forward')
        test.test_typing.ForwardRefTests('test_type_error')
        test.test_typing.ForwardRefTests('test_union_forward')
        test.test_typing.ForwardRefTests('test_union_forward_recursion')
        test.test_typing.GenericTests('test_abc_bases')
        test.test_typing.GenericTests('test_abc_registry_kept')
        test.test_typing.GenericTests('test_all_repr_eq_any')
        test.test_typing.GenericTests('test_basics')
        test.test_typing.GenericTests('test_chain_repr')
        test.test_typing.GenericTests('test_copy_and_deepcopy')

        test.test_typing.GenericTests('test_dict')
        test.test_typing.GenericTests('test_eq_1')
        test.test_typing.GenericTests('test_eq_2')
        test.test_typing.GenericTests('test_errors')
        test.test_typing.GenericTests('test_extended_generic_rules_eq')
        test.test_typing.GenericTests('test_extended_generic_rules_repr')
        test.test_typing.GenericTests('test_extended_generic_rules_subclassing')
        test.test_typing.GenericTests('test_fail_with_bare_generic')
        test.test_typing.GenericTests('test_fail_with_bare_union')
        test.test_typing.GenericTests('test_false_subclasses')
        test.test_typing.GenericTests('test_generic_errors')
        test.test_typing.GenericTests('test_generic_forward_ref')
        test.test_typing.GenericTests('test_implicit_any')
        test.test_typing.GenericTests('test_init')
        test.test_typing.GenericTests('test_init_subclass')
        test.test_typing.GenericTests('test_multi_subscr_base')
        test.test_typing.GenericTests('test_multiple_bases')
        test.test_typing.GenericTests('test_multiple_inheritance')

        test.test_typing.GenericTests('test_nested')
        test.test_typing.GenericTests('test_new_no_args')
        test.test_typing.GenericTests('test_new_repr')
        test.test_typing.GenericTests('test_new_repr_bare')
        test.test_typing.GenericTests('test_new_repr_complex')
        test.test_typing.GenericTests('test_new_with_args')
        test.test_typing.GenericTests('test_new_with_args2')
        test.test_typing.GenericTests('test_orig_bases')


        test.test_typing.GenericTests('test_pickle')
        test.test_typing.GenericTests('test_repr')
        test.test_typing.GenericTests('test_subscript_meta')




        test.test_typing.GenericTests('test_weakref_all')
        test.test_typing.GetTypeHintTests('test_get_type_hints_ClassVar')
        test.test_typing.GetTypeHintTests('test_get_type_hints_classes')
        test.test_typing.GetTypeHintTests('test_get_type_hints_for_builtins')
        test.test_typing.GetTypeHintTests('test_get_type_hints_for_object_with_annotations')
        test.test_typing.GetTypeHintTests('test_get_type_hints_from_various_objects')
        test.test_typing.GetTypeHintTests('test_get_type_hints_modules')
        test.test_typing.GetTypeHintTests('test_get_type_hints_modules_forwardref')
        test.test_typing.GetTypeHintTests('test_previous_behavior')
        test.test_typing.GetTypeHintTests('test_respect_no_type_check')
        test.test_typing.IOTests('test_binaryio')
        test.test_typing.IOTests('test_io')

        test.test_typing.IOTests('test_textio')
        test.test_typing.NamedTupleTests('test_annotation_usage')
        test.test_typing.NamedTupleTests('test_annotation_usage_with_default')
        test.test_typing.NamedTupleTests('test_annotation_usage_with_methods')
        test.test_typing.NamedTupleTests('test_basics')
        test.test_typing.NamedTupleTests('test_namedtuple_errors')
        test.test_typing.NamedTupleTests('test_namedtuple_keyword_usage')
        test.test_typing.NamedTupleTests('test_namedtuple_pyversion')
        test.test_typing.NamedTupleTests('test_namedtuple_special_keyword_names')
        test.test_typing.NamedTupleTests('test_pickle')
        test.test_typing.NewTypeTests('test_basic')
        test.test_typing.NewTypeTests('test_errors')
        test.test_typing.NoReturnTests('test_cannot_instantiate')
        test.test_typing.NoReturnTests('test_cannot_subclass')
        test.test_typing.NoReturnTests('test_noreturn_instance_type_error')
        test.test_typing.NoReturnTests('test_noreturn_subclass_type_error')
        test.test_typing.NoReturnTests('test_not_generic')
        test.test_typing.NoReturnTests('test_repr')
        test.test_typing.OtherABCTests('test_async_contextmanager')
        test.test_typing.OtherABCTests('test_contextmanager')
        test.test_typing.OverloadTests('test_overload_fails')
        test.test_typing.OverloadTests('test_overload_succeeds')
        test.test_typing.ProtocolTests('test_protocol_instance_type_error')
        test.test_typing.ProtocolTests('test_reversible')
        test.test_typing.ProtocolTests('test_supports_abs')
        test.test_typing.ProtocolTests('test_supports_bytes')
        test.test_typing.ProtocolTests('test_supports_complex')
        test.test_typing.ProtocolTests('test_supports_float')
        test.test_typing.ProtocolTests('test_supports_index')
        test.test_typing.ProtocolTests('test_supports_int')
        test.test_typing.ProtocolTests('test_supports_round')
        test.test_typing.RETests('test_alias_equality')
        test.test_typing.RETests('test_errors')
        test.test_typing.RETests('test_repr')
        test.test_typing.TupleTests('test_basics')
        test.test_typing.TupleTests('test_equality')
        test.test_typing.TupleTests('test_errors')
        test.test_typing.TupleTests('test_repr')
        test.test_typing.TupleTests('test_tuple_instance_type_error')
        test.test_typing.TupleTests('test_tuple_subclass')
        test.test_typing.TypeTests('test_type_basic')
        test.test_typing.TypeTests('test_type_optional')
        test.test_typing.TypeTests('test_type_typevar')
        test.test_typing.TypeVarTests('test_basic_plain')
        test.test_typing.TypeVarTests('test_bound_errors')
        test.test_typing.TypeVarTests('test_cannot_instantiate_vars')
        test.test_typing.TypeVarTests('test_cannot_subclass_var_itself')
        test.test_typing.TypeVarTests('test_cannot_subclass_vars')
        test.test_typing.TypeVarTests('test_constrained_error')
        test.test_typing.TypeVarTests('test_no_bivariant')
        test.test_typing.TypeVarTests('test_no_redefinition')
        test.test_typing.TypeVarTests('test_repr')
        test.test_typing.TypeVarTests('test_typevar_instance_type_error')
        test.test_typing.TypeVarTests('test_typevar_subclass_type_error')
        test.test_typing.TypeVarTests('test_union_constrained')
        test.test_typing.TypeVarTests('test_union_unique')
        test.test_typing.UnionTests('test_base_class_disappears')
        test.test_typing.UnionTests('test_basics')
        test.test_typing.UnionTests('test_cannot_instantiate')
        test.test_typing.UnionTests('test_cannot_subclass')
        test.test_typing.UnionTests('test_empty')
        test.test_typing.UnionTests('test_etree')
        test.test_typing.UnionTests('test_function_repr_union')
        test.test_typing.UnionTests('test_no_eval_union')
        test.test_typing.UnionTests('test_optional')
        test.test_typing.UnionTests('test_repr')
        test.test_typing.UnionTests('test_single_class_disappears')
        test.test_typing.UnionTests('test_subclass_error')
        test.test_typing.UnionTests('test_union_any')
        test.test_typing.UnionTests('test_union_compare_other')
        test.test_typing.UnionTests('test_union_generalization')
        test.test_typing.UnionTests('test_union_instance_type_error')
        test.test_typing.UnionTests('test_union_object')
        test.test_typing.UnionTests('test_union_str_pattern')
        test.test_typing.UnionTests('test_union_union')
        test.test_typing.UnionTests('test_unordered')

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
