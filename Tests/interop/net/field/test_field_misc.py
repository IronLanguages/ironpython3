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
'''
Operations on enum type and its' members
'''
#------------------------------------------------------------------------------
from iptest import *    
from iptest.assert_util import *
skiptest("silverlight")

add_clr_assemblies("fieldtests", "typesamples")

if options.RUN_TESTS: #TODO - bug when generating Pydoc
    from Merlin.Testing.FieldTest import *
    from Merlin.Testing.TypeSample import *

def test_accessibility():
    o = Misc()
    o.Set()
    AreEqual(o.PublicField, 100)
    Assert(not hasattr(o, 'ProtectedField'))
    AssertErrorWithMatch(AttributeError, "'Misc' object has no attribute 'PrivateField'", lambda: o.PrivateField)
    AreEqual(o.InterfaceField.PublicStaticField, 500)
    
    o = DerivedMisc()
    o.Set()
    AreEqual(o.PublicField, 400)
    Assert(not hasattr(o, 'ProtectedField'))

run_test(__name__)

