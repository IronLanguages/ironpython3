# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.
    
from iptest.assert_util import *

import clr
clr.AddReferenceToFileAndPath(testpath.rowan_root + r"\Test\ClrAssembly\folder1\loadorder_6.dll") # TODO

import Namespace_ToBeRemoved

for x in clr.References: 
    print(x, x.Location)

clr.AddReferenceToFileAndPath(testpath.rowan_root + r"\Test\ClrAssembly\folder2\loadorder_6.dll") # TODO

for x in clr.References: 
    print(x, x.Location)

print(Namespace_ToBeRemoved.C.Flag)

import Namespace_JustAdded

print(Namespace_JustAdded.C.Flag)

del Namespace_ToBeRemoved
print(Namespace_ToBeRemoved.C.Flag)

import Namespace_ToBeRemoved

