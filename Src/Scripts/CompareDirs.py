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

import sys,nt
sys.path.append(nt.environ['IP_ROOT'] + '\\External\\Regress\\Python24\\Lib')
import filecmp

def compare_dirs(dir1, dir2):
    "Tests whether two folders, including all their contents and subdirectories are identical or not (True == they're the same, False == they are not the same)"
    dc = filecmp.dircmp(dir1,dir2)
    if len(dc.funny_files) > 0 or len(dc.left_only) > 0 or len(dc.right_only) > 0 or len(dc.diff_files) > 0:
        dc.report()
        return False
    else:
        for subdir in dc.common_dirs:
            if not compare_dirs(dir1 + "\\" + subdir, dir2 + "\\" + subdir):
                return False
        return True


def main():
    if len(sys.argv)!=3:
        print 'Usage: CompareDirs <dir1> <dir2>'
        sys.exit(-1)
        
    if compare_dirs(sys.argv[1],sys.argv[2]):
        print "The directories are identical"
        sys.exit(0)
    else: #the part that differed is explained via dircmp.report() above
        print "The directories differ"
        sys.exit(1)

if __name__=="__main__":
    main()
