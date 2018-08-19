# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.


""" This provides a more convenient harness for running this
    benchmark and collecting separate timings for each component.
"""

import sys, os
sys.path.append(os.path.join([os.environ[x] for x in list(os.environ.keys()) if x.lower() == "dlr_root"][0], "External.LCA_RESTRICTED", "Languages", "IronPython", "27", "Lib", "test"))

def test_main(type="short"):
    import pystone
    loops = { "full": 50000, "short" : 50000, "medium" : 250000, "long" : 1000000 }[type]
    pystone.main(loops)

if __name__=="__main__":
    kind = "short"
    if len(sys.argv) > 1: kind = sys.argv[1]
    test_main(kind)
