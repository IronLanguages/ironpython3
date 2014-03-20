#####################################################################################
#
#  Copyright (c) Microsoft Corporation. All rights reserved.
#
# This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
# copy of the license can be found in the License.html file at the root of this distribution. If 
# you cannot locate the  Apache License, Version 2.0, please send an email to 
# ironpy@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
# by the terms of the Apache License, Version 2.0.
#
# You must not remove this notice, or any other, from this software.
#
#
#####################################################################################
    
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
