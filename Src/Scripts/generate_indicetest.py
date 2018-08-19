# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

boundaries = (None, -7,-2,0,1,6,10)
steps = (-5,-3,-1,1,5,20)
lengths = (-100,-15,-3,0,1,5,10,100)

global counter
global tests
global file

counter = 0
tests = 0
file = None

one_test="t"
group_test=['a','b','c','d','e','f','g','h','i','j','k','l','m','n','o','p','q']

file = open("Tests\\IndiceTest.py", "w")
file.write("""#####################################################################################
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

from Util.Debug import *

""")

file.write("def " + one_test + "(i,j,k,l,r) :\n")
file.write("\trr = slice(i,j,k).indices(l)\n")
file.write("\tAssert(rr == r, \"slice(\" + str(i) + \",\" + str(j) + \",\" + str(k) + \").indices(\" + str(l) + \") != \" + str(r) + \": \" + str(rr))\n\n")



for start in boundaries:
    for stop in boundaries:
        for step in steps:
            for length in lengths:
                if tests % 2000 == 0:
                    func = "def " + group_test[counter] + "():\n"
                    file.write(func)
                    counter += 1
                tests += 1
                s = slice(start, stop, step)
                i = s.indices(length)
                file.write("\t"+ one_test + "(" + str(start) + "," + str(stop) + "," + str(step) + "," + str(length) + "," + str(i) + ")\n")


file.write("\n")
file.write("def main():\n")
fnc = 0
while fnc < counter:
    file.write("\t" + group_test[fnc] + "()\n")
    fnc += 1

file.write("\n\nmain()\n\n")

file.close()

print tests
print counter
