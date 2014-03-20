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

add_clr_assemblies("loadorder_1a")

# namespace NamespaceOrType {
#     public class C {
#         public static string Flag = typeof(C).FullName;
#     }
# }

import NamespaceOrType

add_clr_assemblies("loadorder_1c")

# public class NamespaceOrType<T> {
#     public static string Flag = typeof(NamespaceOrType<>).FullName;
# }

AreEqual(NamespaceOrType.C.Flag, "NamespaceOrType.C")

import NamespaceOrType

AssertError(AttributeError, lambda: NamespaceOrType.C)
AreEqual(NamespaceOrType[int].Flag, "NamespaceOrType`1")