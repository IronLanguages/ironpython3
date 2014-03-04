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
'''
#------------------------------------------------------------------------------
from iptest import *
from iptest.assert_util import *
skiptest("silverlight")

add_clr_assemblies("fieldtests", "typesamples", "baseclasscs")
if options.RUN_TESTS: #TODO - bug when generating Pydoc
    from Merlin.Testing.FieldTest import *
    from Merlin.Testing.TypeSample import *


def test_get_set():
    o = EnumInt32()
    AreEqual(o.A, EnumInt32.A)
   
    desc = EnumInt32.__dict__['B']
    AreEqual(EnumInt32.B, desc)
    
    def f(): o.A = 10
    AssertErrorWithMatch(AttributeError, "attribute 'A' of 'EnumInt32' object is read-only", f)
    
    def f(): EnumInt32.B = 10
    AssertErrorWithMatch(AttributeError, "attribute 'B' of 'EnumInt32' object is read-only", f)

    def f(): EnumInt32.B = EnumInt32.A
    AssertErrorWithMatch(AttributeError, "attribute 'B' of 'EnumInt32' object is read-only", f)

def test_enum_bool():
    from Merlin.Testing.BaseClass import EmptyEnum
    
    #An empty enumeration
    Assert(not bool(EmptyEnum())) 

    #__nonzero__
    o = EnumInt32()
    Assert(not o.A.__nonzero__())
    Assert(o.B.__nonzero__())

    for enum_type in [
                        EnumByte,
                        EnumSByte,
                        EnumUInt16, 
                        EnumInt16,
                        EnumUInt32, 
                        EnumInt32,
                        EnumUInt64, 
                        EnumInt64,
                        ]:
        Assert(not bool(enum_type().A))
        Assert(not bool(enum_type.A))
        Assert(bool(enum_type().B))
        Assert(bool(enum_type.B))
        Assert(bool(enum_type().C))
        Assert(bool(enum_type.C))
        Assert(enum_type)
        Assert(not bool(enum_type()))
    
run_test(__name__)

