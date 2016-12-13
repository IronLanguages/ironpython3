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
TODO:
- no test cases defined here!
'''
#------------------------------------------------------------------------------
from iptest.assert_util import *
skiptest("silverlight")

add_clr_assemblies("userdefinedconversions", "typesamples")

import System
from Merlin.Testing import *
from Merlin.Testing.Call import *
from Merlin.Testing.TypeSample import *

# to cover __int__, __long__, __float__, __complex__, __bool__ 
# on CLR objects (and derived python object)

run_test(__name__)

