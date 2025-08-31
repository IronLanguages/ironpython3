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
from First import *

add_clr_assemblies("loadorder_3i")

# namespace First {
#     public class Generic2<T> {
#         public static string Flag = typeof(Generic2<>).FullName;
#     }
# }


AreEqual(First.Generic1[str, str].Flag, "First.Generic1`2")      
AreEqual(First.Generic2[str].Flag, "First.Generic2`1")      

AreEqual(Generic1[str, str].Flag, "First.Generic1`2")      
AssertError(NameError, lambda: Generic2[int])

from First import *

AreEqual(Generic1[str, str].Flag, "First.Generic1`2")      
AreEqual(Generic2[str].Flag, "First.Generic2`1")      

