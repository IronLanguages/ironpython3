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

def test_main(level='full'):
    import sys
    import operator

    old_args = sys.argv
    sys.argv = ['checkonly']

    # !!! Instead of a whitelist, we should have a blacklist so that any newly added
    # generators automatically get included in this tests
    generators = [
        'generate_AssemblyTypeNames',
        'generate_alltypes',
        'generate_calls', 
        'generate_casts',
        'generate_dict_views',
        'generate_dynsites',
        'generate_exceptions', 
        'generate_math', 
        'generate_ops',
        'generate_reflected_calls',
        'generate_set',
        'generate_walker',
        'generate_typecache',
        'generate_tree',
        'generate_dynamic_instructions',
        'generate_comdispatch',
		# TODO: uncomment when we have whole Core sources in Snap/test
		# 'generate_exception_factory',        
	]
    #Merlin 277482
    import System
    if [System.IntPtr.Size==8]:
        generators.remove('generate_ops')

    failures = 0

    for gen in generators:
        print "Running", gen
        g = __import__(gen)
        one = g.main()

        if operator.isSequenceType(one):
            failures = reduce(
                operator.__add__,
                map(lambda r: 0 if r else 1, one),
                failures
            )
        else:
            print "    FAIL:", gen, "generator didn't return valid result"
            failures += 1

    if failures > 0:
        print "FAIL:", failures, "generator" + ("s" if failures > 1 else "") + " failed"
        sys.exit(1)
    else:
        print "PASS"

    sys.argv = old_args
    
if __name__=="__main__":
    test_main()    
