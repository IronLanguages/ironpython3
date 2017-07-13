#####################################################################################
#
#  Copyright (c) Microsoft Corporation. All rights reserved.
#
# This source code is subject to terms and conditions of the Apache License, Version 2.0. A
# copy of the license can be found in the License.html file at the root of this distribution. If
# you cannot locate the  Apache License, Version 2.0, please send an email to
# ironpy@microsoft.com. By using this source code in any fashion, you are agreeing to be bound
# by the terms of the Apache License, Version 2.0.
#
# You must not remove this notice, or any other, from this software.
#
#
#####################################################################################

import sys
import unittest

from iptest import IronPythonTestCase, is_cli, is_netstandard, is_mono, run_test, skipUnlessIronPython

@skipUnlessIronPython()
class ClrLoad2Test(IronPythonTestCase):
    """Test cases for CLR types that don't involve actually loading CLR into the module using the CLR types"""

    def test_nested_classes(self):
        old_path = [x for x in sys.path]
        sys.path.append(self.test_inputs_dir)

        import UseCLI

        if not is_netstandard and not is_mono: # no System.Windows.Forms in netstandard
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
