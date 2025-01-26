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

add_clr_assemblies("loadorder_5c")

# namespace NS {
#     public class Target<T1> { // different generic parameter name
#         public static string Flag = typeof(Target<>).FullName + "_T1";
#     }
# }

AreEqual(NS.Target[int].Flag, "NS.Target`1_T1")
AreEqual(NS.Target[int, str].Flag, "NS.Target`2")
