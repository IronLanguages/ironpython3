# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

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
