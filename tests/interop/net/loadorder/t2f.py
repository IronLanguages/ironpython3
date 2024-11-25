# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.
    
from iptest.assert_util import *


add_clr_assemblies("loadorder_2")

# namespace First {
#     public class Nongeneric1 {
#         public static string Flag = typeof(Nongeneric1).FullName;
#     }
# }

add_clr_assemblies("loadorder_2f")

# // generic type, which has different namespace, different name from First.Nongeneric1
# namespace Second {
#     public class Nongeneric2<T> {
#         public static string Flag = typeof(Nongeneric2<>).FullName;
#     }
# }


from First import *
from Second import *

AreEqual(Nongeneric1.Flag, "First.Nongeneric1")
AreEqual(Nongeneric2[int].Flag, "Second.Nongeneric2`1")

import First, Second

AreEqual(First.Nongeneric1.Flag, "First.Nongeneric1")
AreEqual(Second.Nongeneric2[str].Flag, "Second.Nongeneric2`1")
