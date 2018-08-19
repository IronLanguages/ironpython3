# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.
    
from iptest.assert_util import *


add_clr_assemblies("loadorder_4")

# namespace NS {
#     public class Target {
#         public static string Flag = typeof(Target).FullName;
#     }
#     public class Target<T> {
#         public static string Flag = typeof(Target<>).FullName;
#     }
# }


import NS

AreEqual(dir(NS), ['Target'])

add_clr_assemblies("loadorder_4c")

# namespace NS {
#     public class Target<K, V> {
#         public static string Flag = typeof(Target<,>).FullName;
#     }
# }

AreEqual(dir(NS), ['Target'])

AreEqual(NS.Target.Flag, "NS.Target")
AreEqual(NS.Target[int].Flag, "NS.Target`1")
AreEqual(NS.Target[int, str].Flag, "NS.Target`2")

AreEqual(dir(NS), ['Target'])

from NS import *

AreEqual(Target.Flag, "NS.Target")
AreEqual(Target[int].Flag, "NS.Target`1")
AreEqual(Target[int, str].Flag, "NS.Target`2")

