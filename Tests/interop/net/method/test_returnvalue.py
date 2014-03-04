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
NOTES:
- seems not a good test?
'''
#------------------------------------------------------------------------------
from iptest.assert_util import *
skiptest("silverlight")

add_clr_assemblies("returnvalues", "typesamples")

from Merlin.Testing import *
from Merlin.Testing.Call import *
from Merlin.Testing.TypeSample import *


c = C()

def test_return_null():
    for f in [ 
        c.ReturnVoid, 
        c.ReturnNull, 
        c.ReturnNullableInt2, 
        c.ReturnNullableStruct2,
    ]:
        AreEqual(f(), None)
    
def test_return_numbers():
    for (f, t, val) in [
        (c.ReturnByte, System.Byte, 0), 
        (c.ReturnSByte, System.SByte, 1), 
        (c.ReturnUInt16, System.UInt16, 2), 
        (c.ReturnInt16, System.Int16, 3), 
        (c.ReturnUInt32, System.UInt32, 4), 
        (c.ReturnInt32, System.Int32, 5), 
        (c.ReturnUInt64, System.UInt64, 6), 
        (c.ReturnInt64, System.Int64, 7), 
        (c.ReturnDouble, System.Double, 8), 
        (c.ReturnSingle, System.Single, 9), 
        (c.ReturnDecimal, System.Decimal, 10), 
        
        (c.ReturnChar, System.Char, 'A'), 
        (c.ReturnBoolean, System.Boolean, True), 
        (c.ReturnString, System.String, "CLR"), 
        
        (c.ReturnEnum, EnumInt16, EnumInt16.C),
    ]:
        x = f()
        AreEqual(x.GetType(), clr.GetClrType(t))
        AreEqual(x, val)
        
    # return value type
    x = c.ReturnStruct()
    AreEqual(x.Flag, 100)
    
    x = c.ReturnClass()
    AreEqual(x.Flag, 200)
    
    x = c.ReturnNullableInt1()
    AreEqual(x.GetType(), clr.GetClrType(int))
    AreEqual(x, 300)
    
    x = c.ReturnNullableStruct1()
    AreEqual(x.GetType(), clr.GetClrType(SimpleStruct))
    AreEqual(x.Flag, 400)
    
    x = c.ReturnInterface()
    AreEqual(x.Flag, 500)
    
    # return delegate
    x = c.ReturnDelegate()
    AreEqual(x.GetType(), clr.GetClrType(Int32RInt32EventHandler))
    AreEqual(x(3), 6)
    AreEqual(x(3.0), 6)
    
    # array
    x = c.ReturnInt32Array()
    AreEqual(x[0], 1)
    AreEqual(x[1], 2)
    
    x = c.ReturnStructArray()
    AreEqual(x[0].Flag, 1)
    AreEqual(x[1].Flag, 2)

def test_return_from_generic():
    for (t, v) in [
        (int, 2), 
        (str, "python"),
    ]:
        g = G[t](v)
        
        AreEqual(g.ReturnT(), v)
        AreEqual(g.ReturnStructT().Flag, v)
        AreEqual(g.ReturnClassT().Flag, v)

        x = g.ReturnArrayT()
        AreEqual(len(x), 3)
        AreEqual(x[2], v)
    
run_test(__name__)

