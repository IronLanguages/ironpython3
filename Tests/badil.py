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

import clr
import System

clr.AddReference("System.Core")
clr.AddReference("Microsoft.Scripting")
clr.AddReference("IronPython")
clr.AddReference("Microsoft.Dynamic")

from Microsoft.Scripting.Generation import Snippets
from IronPython.Compiler import CallTarget0

paramTypes = System.Array[System.Type]([])

ilgen = Snippets.Shared.CreateDynamicMethod("test", System.Object, paramTypes, False)
ilgen.Emit(System.Reflection.Emit.OpCodes.Ret)
ilgen.CreateDelegate[CallTarget0](clr.Reference[System.Reflection.MethodInfo]())
