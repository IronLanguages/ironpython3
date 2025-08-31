# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.
    
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

