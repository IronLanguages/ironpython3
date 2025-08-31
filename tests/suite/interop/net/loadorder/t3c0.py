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

add_clr_assemblies("loadorder_3c")

# namespace First {
#     public class Generic1 {
#         public static string Flag = typeof(Generic1).FullName;
#     }
# }

AreEqual(First.Generic1.Flag, "First.Generic1")
AreEqual(First.Generic1[int, int].Flag, "First.Generic1`2")

from First import Generic1

AreEqual(Generic1.Flag, "First.Generic1")
AreEqual(Generic1[str, str].Flag, "First.Generic1`2")

