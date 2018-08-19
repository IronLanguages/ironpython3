# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.
'''
How to re-define an event in Python.

NOTES:
- all bugs in this module are currently test blocking.
'''

import unittest

from iptest import IronPythonTestCase, run_test, skipUnlessIronPython

EVENT_COUNT = 0

@skipUnlessIronPython()
class EventOverrideTest(IronPythonTestCase):
    def setUp(self):
        super(EventOverrideTest, self).setUp()
        self.add_clr_assemblies("baseclasscs", "typesamples")

    def test_sanity_interface_impl(self):
        from Merlin.Testing.BaseClass import IEvent10

        global EVENT_COUNT
        EVENT_COUNT = 0
        
        class PySubclass(IEvent10):
            def __init__(self):
                self.events = []
            def add_Act(self, value):
                self.events.append(value)
            def remove_Act(self, value):
                self.events.remove(value)
            def call(self):
                for x in self.events:
                    x(1, 2)
        
        x = PySubclass()
        def f(x, y):
            global EVENT_COUNT
            EVENT_COUNT += 1
            print(x, y)
        
        x.add_Act(f)
        x.call()
        self.assertEqual(EVENT_COUNT, 1)
        x.remove_Act(f)
        x.call()
        self.assertEqual(EVENT_COUNT, 1)

    def test_sanity_derived_neg(self):
        '''
        The snippet below does not work and the related bug, Dev10 438724,
        was closed by design.  Keeping this around as a negative scenario.
        '''
        from Merlin.Testing.BaseClass import CEvent40

        global EVENT_COUNT
        EVENT_COUNT = 0
        
        class PySubclass(CEvent40):
            def add_Act(self, value):
                self.Act += value
            def remove_Act(self, value):
                self.Act -= value
            def call(self):
                self.Act(1, 2)
        
        x = PySubclass()
        def f(x, y):
            EVENT_COUNT += 1
            print(x, y)
        
        x.add_Act(f)
        self.assertRaisesMessage(TypeError, "BoundEvent is not callable", x.call)
        #x.call()
        #self.assertEqual(EVENT_COUNT, 1)
        #x.remove_Act(f)
        #x.call()
        #self.assertEqual(EVENT_COUNT, 1)


run_test(__name__)
