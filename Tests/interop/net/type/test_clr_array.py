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
Array Type
* Creation (type, rank, ...)
* initialize list
  - Does conversion occur here?
  - Assign something to array of interface/base type 
* Operation (indexing/member access)
  - Set, get: A[1][2] = 3, A[1,2] = 3
  - (python) Slicing?
* Passing the array object as argument to methods
  - C#: Array covariance specifically does not extend to arrays of value-types
'''
#------------------------------------------------------------------------------ 
from iptest.assert_util import *
skiptest("silverlight")

add_clr_assemblies("typesamples")

from Merlin.Testing import *
from Merlin.Testing.TypeSample import *

from System import Array 

def test_creation():
    Array[int]([1,2])
    
run_test(__name__)
