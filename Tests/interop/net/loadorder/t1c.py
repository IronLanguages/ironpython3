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
    
from iptest.assert_util import *

add_clr_assemblies("loadorder_1b")

# public class NamespaceOrType {
#     public static string Flag = typeof(NamespaceOrType).FullName;
# }

import NamespaceOrType

add_clr_assemblies("loadorder_1a")

# namespace NamespaceOrType {
#     public class C {
#         public static string Flag = typeof(C).FullName;
#     }
# }

AreEqual(NamespaceOrType.Flag, "NamespaceOrType")

import NamespaceOrType

AssertError(AttributeError, lambda: NamespaceOrType.Flag)

AreEqual(NamespaceOrType.C.Flag, "NamespaceOrType.C")

