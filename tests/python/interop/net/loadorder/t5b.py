# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.
    
from iptest.assert_util import *


add_clr_assemblies("loadorder_5")

# namespace NS {
#     public class Target<T> {
#         public static string Flag = typeof(Target<>).FullName;
#     }
#     public class Target<T1, T2> {
#         public static string Flag = typeof(Target<,>).FullName;
#     }
# }

import NS
from NS import *

add_clr_assemblies("loadorder_5b")

# namespace NS {
#     public class Target<T1, T2, T3> {
#         public static string Flag = typeof(Target<,,>).FullName;
#     }
# }

AreEqual(Target[int].Flag, "NS.Target`1")
AreEqual(Target[int, str].Flag, "NS.Target`2")
AssertError(ValueError, lambda: Target[str, int, str].Flag)

from NS import *

AreEqual(Target[int].Flag, "NS.Target`1")
AreEqual(Target[int, str].Flag, "NS.Target`2")
AreEqual(Target[str, int, str].Flag, "NS.Target`3")

AreEqual(NS.Target[int].Flag, "NS.Target`1")
AreEqual(NS.Target[int, str].Flag, "NS.Target`2")
AreEqual(NS.Target[str, int, str].Flag, "NS.Target`3")

