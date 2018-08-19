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

add_clr_assemblies("loadorder_2h")

# // generic type, which has same namespace, different name from First.Nongeneric1
# namespace First {
#     public class Nongeneric2<T> {
#         public static string Flag = typeof(Nongeneric2<>).FullName;
#     }
# }

AreEqual(First.Nongeneric2[str].Flag, "First.Nongeneric2`1")        # no need to import First again
AreEqual(First.Nongeneric1.Flag, "First.Nongeneric1")

AreEqual(Nongeneric1.Flag, "First.Nongeneric1")
AssertError(NameError, lambda: Nongeneric2)


from First import *

AreEqual(First.Nongeneric2[str].Flag, "First.Nongeneric2`1")
AreEqual(First.Nongeneric1.Flag, "First.Nongeneric1")
AreEqual(Nongeneric1.Flag, "First.Nongeneric1")
AreEqual(Nongeneric2[float].Flag, "First.Nongeneric2`1")
