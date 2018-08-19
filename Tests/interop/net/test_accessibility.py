# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.
'''
NOTES:
- needs to be rewritten
'''

import unittest

from iptest import IronPythonTestCase, run_test, skipUnlessIronPython

@skipUnlessIronPython()
class AccessibilityTest(IronPythonTestCase):
    def setUp(self):
        super(AccessibilityTest, self).setUp()
        self.add_clr_assemblies("baseclasscs")

    def pass_for_read_protected(self, x):
        x.protected_static_field
        x.protected_static_method
        #x.protected_static_property  # bug 370438
        #x.protected_static_event     # bug 370432
        if str(x).startswith("<C1 object") or str(x).startswith("<C2 object"):
            print("Skipping (https://github.com/IronLanguages/main/issues/721)...")
        else:
            x.protected_static_nestedclass
    
    def all_read(self, x):
        self.assertRaises(AttributeError, lambda: x.private_static_field)
        self.assertRaises(AttributeError, lambda: x.private_static_method)
        self.assertRaises(AttributeError, lambda: x.private_static_property)
        self.assertRaises(AttributeError, lambda: x.private_static_event)
        self.assertRaises(AttributeError, lambda: x.private_static_nestedclass)
        self.assertRaises(AttributeError, lambda: x.internal_static_field)
        self.assertRaises(AttributeError, lambda: x.internal_static_method)
        self.assertRaises(AttributeError, lambda: x.internal_static_property)
        self.assertRaises(AttributeError, lambda: x.internal_static_event)
        self.assertRaises(AttributeError, lambda: x.internal_static_nestedclass)
        x.protected_static_field
        x.protected_static_method
        #x.protected_static_property  # bug 370438
        #x.protected_static_event     # bug 370432
        self.assertTrue(not hasattr('x', 'protected_static_nestedclass')) # not supported
        x.public_static_field
        x.public_static_method
        x.public_static_property
        x.public_static_event
        x.public_static_nestedclass
        
        self.assertRaises(AttributeError, lambda: x.private_instance_field)
        self.assertRaises(AttributeError, lambda: x.private_instance_method)
        self.assertRaises(AttributeError, lambda: x.private_instance_property)
        self.assertRaises(AttributeError, lambda: x.private_instance_event)
        self.assertRaises(AttributeError, lambda: x.private_instance_nestedclass)
        self.assertRaises(AttributeError, lambda: x.internal_instance_field)
        self.assertRaises(AttributeError, lambda: x.internal_instance_method)
        self.assertRaises(AttributeError, lambda: x.internal_instance_property)
        self.assertRaises(AttributeError, lambda: x.internal_instance_event)
        self.assertRaises(AttributeError, lambda: x.internal_instance_nestedclass)
        x.protected_instance_field
        x.protected_instance_method
        x.protected_instance_property
        #x.protected_instance_event     # bug 370432
        self.assertTrue(not hasattr('x', 'protected_instance_nestedclass')) # not supported
        x.public_instance_field
        x.public_instance_method
        x.public_instance_property
        x.public_instance_event
        x.public_instance_nestedclass
    
    def test_access_outside(self):
        from Merlin.Testing.Accessibility import CliClass, DerivedCliClass
        class C1(CliClass): 
            __slots__ = []
        
        class C2(DerivedCliClass): 
            __slots__ = []

        for x in [C1, C2, C1(), C2()]:
            self.all_read(x)
            self.pass_for_read_protected(x)
            
            # extra methods
            self.assertRaises(AttributeError, lambda: x.get_private_static_property)
            self.assertRaises(AttributeError, lambda: x.remove_private_static_event)
            self.assertRaises(AttributeError, lambda: x.set_internal_static_property)
            self.assertRaises(AttributeError, lambda: x.add_internal_static_event)
            self.assertRaises(AttributeError, lambda: x.set_protected_static_property)
            self.assertRaises(AttributeError, lambda: x.get_public_static_property)
            self.assertTrue(hasattr(x, 'add_protected_static_event'))
            x.remove_public_static_event

            self.assertRaises(AttributeError, lambda: x.set_private_instance_property)
            self.assertRaises(AttributeError, lambda: x.add_private_instance_event)
            self.assertRaises(AttributeError, lambda: x.get_internal_instance_property)
            self.assertRaises(AttributeError, lambda: x.remove_internal_instance_event)
            self.assertRaises(AttributeError, lambda: x.get_protected_instance_property)
            self.assertRaises(AttributeError, lambda: x.set_public_instance_property)
            self.assertTrue(hasattr(x, 'remove_protected_instance_event'))
            x.add_public_instance_event
            
        def f(*arg): pass
        
        # write 
        for x in [C1, C2]:
            x.protected_static_field = 1
            x.protected_static_property = 2
            #x.protected_static_event += f
            x.public_static_field = 3
            x.public_static_property = 4
            #x.public_static_event += f
        
        for x in [C1(), C2()]: 
            x.protected_instance_field = 11
            self.assertTrue(hasattr(x, 'protected_instance_property'))
            #x.protected_instance_event += f
            x.public_instance_field = 13
            x.public_instance_property = 14
            x.public_instance_event += f
            
        # you may find it a bit surprising 
        # also assign it a string, not int
        
        C1.public_static_field
        C1.public_static_field = "this is python"
        C1.public_static_field 
        
        C1.method = 1
        C1.method
        
        C2.private_static_field = "this is python!"   
        self.assertEqual(C2.private_static_field, "this is python!")

    def test_access_inside(self):
        from Merlin.Testing.Accessibility import CliClass, DerivedCliClass
        all_read = self.all_read
        class C1(CliClass):
            def m(self):
                for x in [C1, self]: all_read(x)
        class C2(DerivedCliClass):
            def m(self):
                for x in [C2, self]: all_read(x)

        for C in [C1, C2]:
            x = C()
            x.m()

# # TODO: Try against PythonDerivedType1, PythonDerivedType2?
# class PythonType1(CliClass): pass
# class PythonDerivedType1(PythonType1): pass
# class PythonType2(DerivedCliClass): pass
# class PythonDerivedType2(PythonType2): pass

    # TODO: cover x.member where x is CLR type or instance.
    #       x.protected_instance_method should throw.
    def test_reflected_type(self):
        from Merlin.Testing.Accessibility import CliClass, DerivedCliClass
        for C in [CliClass, DerivedCliClass]:
            # hasattr reports false because accessing the attribute
            # raises, this is consistent w/ CPython when accessing a
            # user defined property w/ a getter that raises.
            self.assertTrue(not hasattr(C, 'internal_static_field'))
            self.assertTrue(hasattr(C, 'protected_static_field'))
            self.assertTrue('internal_static_field' not in dir(C))
            self.assertTrue('protected_static_field' in dir(C))

            x = C()
            self.assertTrue(not hasattr(x, 'protected_instance_field'))
            self.assertTrue('protected_instance_field' in dir(C))
            self.assertTrue('protected_instance_field' in dir(x))
            self.assertRaises(TypeError, lambda : x.protected_instance_field)

run_test(__name__)
