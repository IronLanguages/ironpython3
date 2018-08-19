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

AreEqual(First.Generic1[int, int].Flag, "First.Generic1`2")

add_clr_assemblies("loadorder_3g")

# namespace First {
#     public class Generic1<K, V> {
#         public static string Flag = typeof(Generic1<,>).FullName + "_Same";
#     }
# }

AreEqual(First.Generic1[int, int].Flag, "First.Generic1`2_Same")

from First import *

AreEqual(Generic1[int, int].Flag, "First.Generic1`2_Same")         