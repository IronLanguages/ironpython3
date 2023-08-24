# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_descr from StdLib
##

from iptest import is_ironpython, generate_suite, run_test

import test.test_descr

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(test.test_descr)

    if is_ironpython:
        failing_tests = [
            test.test_descr.ClassPropertiesAndMethods('test_altmro'), # NotImplementedError: Overriding type.mro is not implemented
            test.test_descr.ClassPropertiesAndMethods('test_classmethods'), # AttributeError: 'classmethod' object has no attribute '__dict__'
            test.test_descr.ClassPropertiesAndMethods('test_cycle_through_dict'), # NotImplementedError: gc.get_objects isn't implemented
            test.test_descr.ClassPropertiesAndMethods('test_descrdoc'), # AssertionError: 'True if the file is closed\r\n' != 'True if the file is closed'
            test.test_descr.ClassPropertiesAndMethods('test_init'), # AssertionError: did not test __init__() for None return
            test.test_descr.ClassPropertiesAndMethods('test_metaclass'), # AttributeError: 'type' object has no attribute '__get__'
            test.test_descr.ClassPropertiesAndMethods('test_method_wrapper'), # AssertionError: <built-in method __add__ of list object at 0x000000000000003E> != <built-in method __add__ of list object at 0x000000000000003F>
            test.test_descr.ClassPropertiesAndMethods('test_mutable_bases'), # AttributeError: 'E' object has no attribute 'meth'
            test.test_descr.ClassPropertiesAndMethods('test_mutable_bases_catch_mro_conflict'), # AssertionError: didn't catch MRO conflict
            test.test_descr.ClassPropertiesAndMethods('test_mutable_bases_with_failing_mro'), # NotImplementedError: Overriding type.mro is not implemented
            test.test_descr.ClassPropertiesAndMethods('test_properties'), # AssertionError: expected AttributeError from trying to set readonly '__doc__' attr on a property
            test.test_descr.ClassPropertiesAndMethods('test_proxy_call'), # AssertionError: TypeError not raised
            test.test_descr.ClassPropertiesAndMethods('test_qualname'), # AssertionError: 'member_descriptor' != 'getset_descriptor'
            test.test_descr.ClassPropertiesAndMethods('test_qualname_dict'), # AssertionError: 'Foo' != 'some.name'
            test.test_descr.ClassPropertiesAndMethods('test_set_and_no_get'), # https://github.com/IronLanguages/ironpython3/issues/1722
            test.test_descr.ClassPropertiesAndMethods('test_set_class'), # AssertionError: shouldn't allow <J object at 0x000000000000004B>.__class__ = <class 'test.test_descr.J'>
            test.test_descr.ClassPropertiesAndMethods('test_set_dict'), # AssertionError: shouldn't allow <class 'test.test_descr.D'>.__dict__ = {}
            test.test_descr.ClassPropertiesAndMethods('test_set_doc'), # AttributeError: readonly attribute
            test.test_descr.ClassPropertiesAndMethods('test_special_method_lookup'), # AssertionError: __getattribute__ called with __iter__
            test.test_descr.ClassPropertiesAndMethods('test_slots'), # NotImplementedError: gc.get_objects isn't implemented
            test.test_descr.ClassPropertiesAndMethods('test_slots_descriptor'), # SystemError: Object reference not set to an instance of an object.
            test.test_descr.ClassPropertiesAndMethods('test_staticmethods'), # AttributeError: 'staticmethod' object has no attribute '__dict__'
            test.test_descr.ClassPropertiesAndMethods('test_supers'), # AttributeError: 'mysuper' object has no attribute 'meth'
            test.test_descr.ClassPropertiesAndMethods('test_vicious_descriptor_nonsense'), # AssertionError: True is not false : <C object at 0x0000000000000058> has unexpected attribute 'attr'
            test.test_descr.MiscTests('test_type_lookup_mro_reference'), # AssertionError: 'from Base' != 'from Base2'
            test.test_descr.MroTest('test_incomplete_extend'), # NotImplementedError: Overriding type.mro is not implemented
            test.test_descr.MroTest('test_incomplete_set_bases_on_self'), # NotImplementedError: Overriding type.mro is not implemented
            test.test_descr.MroTest('test_incomplete_super'), # NotImplementedError: Overriding type.mro is not implemented
            test.test_descr.MroTest('test_reent_set_bases_on_base'), # NotImplementedError: Overriding type.mro is not implemented
            test.test_descr.MroTest('test_reent_set_bases_on_direct_base'), # NotImplementedError: Overriding type.mro is not implemented
            test.test_descr.MroTest('test_reent_set_bases_tp_base_cycle'), # NotImplementedError: Overriding type.mro is not implemented
            test.test_descr.MroTest('test_tp_subclasses_cycle_error_return_path'), # NotImplementedError: Overriding type.mro is not implemented
            test.test_descr.MroTest('test_tp_subclasses_cycle_in_update_slots'), # NotImplementedError: Overriding type.mro is not implemented
            test.test_descr.PicklingTests('test_issue24097'), # TypeError: __reduce__() takes exactly 1 argument (1 given)
            test.test_descr.PicklingTests('test_reduce'), # AssertionError: Tuples differ: (<function __newobj__ at 0x00000000000000AB>, (<class 'test.t[31 chars]None) != (<function __newobj_ex__ at 0x00000000000000AC>, (<class 'tes[40 chars]None)
            test.test_descr.PicklingTests('test_reduce_copying'), # TypeError: __getnewargs__() takes exactly 1 argument (2 given)
            test.test_descr.PicklingTests('test_special_method_lookup'), # AssertionError: Tuples differ: (<fun[19 chars] 0x00000000000000AB>, (<class 'test.test_descr.Picky'>,), None) != (<fun[19 chars] 0x00000000000000AB>, (<class 'test.test_descr.Picky'>,), {})
        ]

        skip_tests = []

        return generate_suite(tests, failing_tests, skip_tests)

    else:
        return tests

run_test(__name__)
