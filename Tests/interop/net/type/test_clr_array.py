# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.
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

import unittest

from iptest import IronPythonTestCase, run_test, skipUnlessIronPython

@skipUnlessIronPython()
class ClrArrayTest(IronPythonTestCase):
    def test_creation(self):
        from System import Array
        Array[int]([1,2])
    
run_test(__name__)
