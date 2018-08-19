# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.
    
from iptest.assert_util import *

import module


add_clr_assemblies("loadorder_7b")

# public class module<T> {
# }

AreEqual(module.flag, "python")

import module
AreEqual(module.flag, "python")
