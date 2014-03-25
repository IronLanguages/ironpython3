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

max_uint = 0xffffffffl
print max_uint

digits = ['0','0']
radii = ['0','0']
for i in range(2, 37):
    p = 1
    while i**(p+1) <= max_uint:
        p = p+1
    print i, p, i**p
    digits.append(str(p))
    radii.append(str(i**p))
print digits, radii

print ", ".join(digits)
print ", ".join(radii)

