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

import First
from First import *

add_clr_assemblies("loadorder_2g")

# // generic type, which has same namespace, same name from First.Nongeneric1
# namespace First {
#     public class Nongeneric1<T> {
#         public static string Flag = typeof(Nongeneric1<>).FullName;
#     }
# }

AreEqual(First.Nongeneric1.Flag, "First.Nongeneric1")
AreEqual(First.Nongeneric1[int].Flag, "First.Nongeneric1`1")  # no need to import First again

AreEqual(Nongeneric1.Flag, "First.Nongeneric1")
AssertError(SystemError, lambda: Nongeneric1[str])  # MakeGenericType on non-generic type

from First import *

AreEqual(Nongeneric1.Flag, "First.Nongeneric1")
AreEqual(Nongeneric1[float].Flag, "First.Nongeneric1`1")
