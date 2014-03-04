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
'''
INTERESTING LOAD SEQUENCES

Simply Load
* What should become visible?
* What should not?
  - Type/namespaces in the referenced assembly
  - 
Type with static constructor
* Thread-safety: we could run the static ctor while initializing the package 
  and we shouldn't have any locks held when that happens.

Different loading approaches
* clr.AddReferenceXXX
* Assembly.LoadXXX
* ?

Loading the same assembly multiple times
* Loading one assembly simultaneously 
* Loading multiple assemblies simultaneously
* Fetching types in one assembly simultaneously
  - From mscorlib, or from user assembly
  - "old", or new

Loading one .NET assembly after another .NET assembly
The loaded type is NS.C, now you try to load
* non-generic type which has type name NS
* generic type which has type name NS
The loaded non-generic type is C, now you try to load
* type which has namespace C
The loaded generic type is C`1, now you try to load
*  type which has namespace C
The loaded type is non-generic type, now you try to load
* non-generic type, or generic type 
  -  which has different namespace, different name
  - which has different namespace, same name
  - which has same namespace, different name
  - which has same namespace, same name
The loaded type is generic type, now you try to 
* non-generic type, or generic type 
  -  which has different namespace, different name
  - which has different namespace, same name
  - which has same namespace, different name
  - which has same namespace, same name
The loaded "type" C is already a merged non-generic type C and generic type 
C`1 (if supported), now you try to load
* non-generic type which has same namespace, same name
* generic type which has same namespace, same name
The loaded "type" C is already a merged generic type C`1 and generic type 
C`2 (if supported), now you try to load
* non-generic type which has same namespace, same name
* generic type which has same namespace, same name

Reload scenario (or think loading 2 assemblies in different angle)
The updated assembly now has
* one top-level type removed, added, unchanged, changed
* one nested type removed, add, unchanged, changed
* one type under namespace removed, added, unchanged, changed
* one whole namespace removed, added, unchanged, changed	
* one member (method, field, ...) removed, added, unchanged, changed under one 
  type

Loading one interesting .NET assembly after another DLR module 
The loaded DLR module is C, now you try to load
* a non-generic type which has type name C
* a generic type which has type name C`2
* a type which has namespace C

Loading one interesting DLR module after another .NET assembly
The loaded type is NS.C, now you try to load 
* a DLR module "NS"
The loaded non-generic type is C, now you try to load
* a DLR module "C"
The loaded generic type is C`3, now you try to load
* a DLR module "C"
'''
#------------------------------------------------------------------------------
from iptest.assert_util import *
from iptest.process_util import *

skiptest("silverlight")

def test_all():
    directory = testpath.public_testdir + r"\interop\net\loadorder"

    count = 0
    for x in nt.listdir(directory):
        if not x.startswith("t") or not x.endswith(".py"):
            continue
    
        # skip list
        if x in [ 't6.py' ]:
            continue
        
        # running ipy with parent's switches
        if is_cli:
            result = launch_ironpython_changing_extensions(directory + "\\" + x)
        
            if result == 0: 
                print "%s: pass" % x
            else:
                count += 1 
                print "%s: fail" % x
    
    if count != 0:
        Fail("there are %s failures" % count)

#####################################################################################
run_test(__name__)
#####################################################################################