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


""" This provides a more convenient harness for running this
    benchmark and collecting separate timings for each component.
"""

from time import clock
import sys, nt
sys.path.append([nt.environ[x] for x in nt.environ.keys() if x.lower() == "dlr_root"][0] + "\\Languages\\IronPython\\External\\parrotbench")
if sys.platform=="cli":
    import System
    is_cli64 = System.IntPtr.Size == 8
    if is_cli64:
        print "CodePlex 18518"
        sys.exit(0)

def test_main(type="short"):
    oldRecursionDepth = sys.getrecursionlimit()
    try:
        sys.setrecursionlimit(1001)
        t0 = clock()
        import b0
        import b1
        import b2
        import b3
        import b4
        import b5
        import b6
        print 'import time = %.2f' % (clock()-t0)
    
        tests = [b0,b1,b2,b3,b4,b5,b6]
        N = { "short" : 1, "full" : 1, "medium" : 2, "long" : 4 }[type]
    
        results = {}
    
        t0 = clock()
        for i in range(N):
            for test in tests:
                ts0 = clock()
                test.main()
                tm = (clock()-ts0)
                results.setdefault(test, []).append(tm)
                print '%.2f sec running %s' % ( tm, test.__name__)
    
        for test in tests:
            print '%s = %f -- %r' % (test.__name__, sum(results[test])/N, results[test])
    
        print 'all done in %.2f sec' % (clock()-t0)
    finally:
        sys.setrecursionlimit(oldRecursionDepth)

if __name__=="__main__":
    kind = "short"
    if len(sys.argv) > 1: kind = sys.argv[1]
    test_main(kind)
