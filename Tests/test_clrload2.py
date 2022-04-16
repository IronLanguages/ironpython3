# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import sys

from iptest import IronPythonTestCase, is_netcoreapp, is_mono, run_test, skipUnlessIronPython

@skipUnlessIronPython()
class ClrLoad2Test(IronPythonTestCase):
    """Test cases for CLR types that don't involve actually loading CLR into the module using the CLR types"""

    def test_nested_classes(self):
        old_path = [x for x in sys.path]
        sys.path.append(self.test_inputs_dir)

        import UseCLI

        if not is_netcoreapp and not is_mono: # no System.Windows.Forms in netstandard
            UseCLI.Form().Controls.Add(UseCLI.Control())

        nc = UseCLI.NestedClass()

        ic = UseCLI.NestedClass.InnerClass()

        tc = UseCLI.NestedClass.InnerClass.TripleNested()

        # This will use TypeCollision
        gc1 = UseCLI.NestedClass.InnerGenericClass[int]()
        gc2 = UseCLI.NestedClass.InnerGenericClass[int, int]()

        # access methods, fields, and properties on the class w/ nesteds,
        # the nested class, and the triple nested class
        for x in ((nc, ''), (ic, 'Inner'), (tc, 'Triple'), (gc1, "InnerGeneric"), (gc2, "InnerGeneric")):
            obj, name = x[0], x[1]

            self.assertEqual(getattr(obj, 'CallMe' + name)(), name + ' Hello World')

            self.assertEqual(getattr(obj, name+'Field'), None)

            self.assertEqual(getattr(obj, name+'Property'), None)

            setattr(obj, name+'Property', name)

            self.assertEqual(getattr(obj, name+'Field'), name)

            self.assertEqual(getattr(obj, name+'Property'), name)

        sys.path = [x for x in old_path]

run_test(__name__)
