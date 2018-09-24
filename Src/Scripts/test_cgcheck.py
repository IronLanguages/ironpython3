# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

from __future__ import print_function

import collections
import functools

def test_main(level='full'):
    import sys
    import operator

    old_args = sys.argv
    sys.argv = ['checkonly']

    # !!! Instead of a whitelist, we should have a blacklist so that any newly added
    # generators automatically get included in this tests
    generators = [
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
        'generate_dynamic_instructions',
        'generate_comdispatch',
		# TODO: uncomment when we have whole Core sources in Snap/test
		# 'generate_exception_factory',
	]
    if sys.implementation.name != "ironpython":
        generators.remove('generate_alltypes')
        generators.remove('generate_exceptions')
        generators.remove('generate_walker')
        generators.remove('generate_comdispatch')

    failures = 0

    for gen in generators:
        print("Running", gen)
        g = __import__(gen)
        one = g.main()

        if isinstance(one, collections.Sequence):
            failures = functools.reduce(
                operator.__add__,
                map(lambda r: 0 if r else 1, one),
                failures
            )
        else:
            print("    FAIL:", gen, "generator didn't return valid result")
            failures += 1

    if failures > 0:
        print("FAIL:", failures, "generator" + ("s" if failures > 1 else "") + " failed")
        sys.exit(1)
    else:
        print("PASS")

    sys.argv = old_args

if __name__=="__main__":
    test_main()
