# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import sys
import os
newline = os.linesep

pats = [0L, 1L, 42L, 0x7fffffffL, 0x80000000L, 0xabcdef01L, 0xffffffffL]
nums = []
for p0 in pats:
    for p1 in pats:
        #for p2 in pats:
            n = p0+(p1<<32) #+(p2<<64)
            nums.append(n)
            nums.append(-n)

bignums = []
for p0 in pats:
    for p1 in pats:
        for p2 in pats:
            n = p0+(p1<<32)+(p2<<64)
            bignums.append(n)
            bignums.append(-n)
#!!! should add 2 or 3 larger numbers to check for any issues there
print len(bignums), len(bignums)**2



import operator, time
ops = [
    ('+', operator.add),
    ('-', operator.sub),
    ('*', operator.mul),
    ('/', operator.div),
    ('%', operator.mod),
    ('&', operator.and_),
    ('|', operator.or_),
    ('^', operator.xor),
]

def buildit(name, nums):
    print 'expected', (len(nums)**2)*len(ops)
    t0 = time.clock()

    for sym, op in ops:
        if not name.startswith('time'):
            fp = open('%s_%s.txt' % (name, op.__name__), 'w')
        else:
            fp = None
        print 'computing', sym
        for x in nums:
            for y in nums:
                try:
                    z = op(x, y)
                    if name == 'time1' or fp:
                        sz = str(z)
                    if fp:
                        fp.write(sz)
                        fp.write(newline)
                except:
                    if fp:
                        fp.write('ERROR' + newline)
        if fp: fp.close()
    t1 = time.clock()
    print 'time', (t1-t0)

buildit('time1', bignums)
buildit('short', nums)
buildit('full', bignums)
