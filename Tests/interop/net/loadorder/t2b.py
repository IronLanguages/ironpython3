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

add_clr_assemblies("loadorder_2b")

# // non-generic type, which has different namespace, different name from First.Nongeneric1
# namespace Second {
#     public class Nongeneric2 {
#         public static string Flag = typeof(Nongeneric2).FullName;
#     }
# }

import Second

AreEqual(First.Nongeneric1.Flag, "First.Nongeneric1")
AreEqual(Second.Nongeneric2.Flag, "Second.Nongeneric2")

# AttributeError for them
AssertError(AttributeError, lambda: First.Nongeneric2)
AssertError(AttributeError, lambda: Second.Nongeneric1)

from First import *

AreEqual(Nongeneric1.Flag, "First.Nongeneric1")

from Second import *

AreEqual(Nongeneric1.Flag, "First.Nongeneric1")
AreEqual(Nongeneric2.Flag, "Second.Nongeneric2")

from First import *

AreEqual(Nongeneric1.Flag, "First.Nongeneric1")
AreEqual(Nongeneric2.Flag, "Second.Nongeneric2")
