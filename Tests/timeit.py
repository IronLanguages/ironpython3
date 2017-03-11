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

import time
import sys

if len(sys.argv) < 2:
    print('usage: test [loops]')
    print()
    print('default loop count is 1000')

file = sys.argv[1]

print('running from file', file)
if len(sys.argv) > 2:
    loopCnt = int(sys.argv[2])
else:
    loopCnt = 1000


runnable = __import__(file)
if not hasattr(runnable, 'run'):
    print(file, 'must have function named run')
    
if not callable(runnable.run):
    print(runnable.run, 'in', file, 'is not callable')

loops = list(range(loopCnt))
times = []

for x in loops:
        start = time.clock()
        runnable.run()
        end = time.clock()
        times.append(end-start)
        
        
minTime = 1<<32
maxTime = -1
total = 0

for x in times:
    minTime = min(minTime, x)
    maxTime = max(maxTime, x)
    total += x

mean = total / loopCnt

# std deviaton
totalDev = 0
for x in times:
    deviation = x - mean
    sqdev = deviation * deviation
    totalDev += sqdev
    
stddev = (totalDev / (loopCnt-1)) ** .5

times.sort()
print('min', minTime)
print('max', maxTime)
print('mean', mean)
print('median', times[loopCnt/2])
print('std deviation', stddev)
print('total', total)
