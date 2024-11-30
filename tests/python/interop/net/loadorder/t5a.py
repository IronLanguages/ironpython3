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

add_clr_assemblies("loadorder_5a")

# namespace NS {
#     public class Target {
#         public static string Flag = typeof(Target).FullName;
#     }
# }

AssertError(SystemError, lambda: Target.Flag)           # THE DIFF!!!
AreEqual(Target[int].Flag, "NS.Target`1")
AreEqual(Target[int, str].Flag, "NS.Target`2")

AreEqual(NS.Target.Flag, "NS.Target")
AreEqual(NS.Target[int].Flag, "NS.Target`1")
AreEqual(NS.Target[int, str].Flag, "NS.Target`2")

# still have the old Target...
AssertError(SystemError, lambda: Target.Flag)
AreEqual(Target[int].Flag, "NS.Target`1")
AreEqual(Target[int, str].Flag, "NS.Target`2")

from NS import *
AreEqual(Target.Flag, "NS.Target")                      # THE DIFF!!!
AreEqual(Target[int].Flag, "NS.Target`1")
AreEqual(Target[int, str].Flag, "NS.Target`2")
