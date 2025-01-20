# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.
    
from iptest.assert_util import *


add_clr_assemblies("loadorder_3")

# namespace First {
#     public class Generic1<K, V> {
#         public static string Flag = typeof(Generic1<,>).FullName;
#     }
# }

import First

add_clr_assemblies("loadorder_3e")

# namespace Second {
#     public class Generic1<K, V> {
#         public static string Flag = typeof(Generic1<,>).FullName;
#     }
# }

import Second

AreEqual(First.Generic1[int, int].Flag, "First.Generic1`2")
AreEqual(Second.Generic1[int, int].Flag, "Second.Generic1`2")

from First import *

AreEqual(Generic1[int, int].Flag, "First.Generic1`2")

from Second import *

AreEqual(Generic1[int, int].Flag, "Second.Generic1`2")
