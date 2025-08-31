# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

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
