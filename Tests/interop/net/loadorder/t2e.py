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


add_clr_assemblies("loadorder_2")

# namespace First {
#     public class Nongeneric1 {
#         public static string Flag = typeof(Nongeneric1).FullName;
#     }
# }

import First
from First import *

add_clr_assemblies("loadorder_2e")

# // generic type, which has different namespace, same name from First.Nongeneric1
# namespace Second {
#     public class Nongeneric1<T> {
#         public static string Flag = typeof(Nongeneric1<>).FullName;
#     }
# }

AssertError(NameError, lambda: Second)

import Second

AreEqual(First.Nongeneric1.Flag, "First.Nongeneric1")
AreEqual(Second.Nongeneric1[int].Flag, "Second.Nongeneric1`1")

AreEqual(Nongeneric1.Flag, "First.Nongeneric1")

from Second import *

AssertError(SystemError, lambda: Nongeneric1.Flag)
AreEqual(Nongeneric1[float].Flag, "Second.Nongeneric1`1")


