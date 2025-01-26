# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

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

