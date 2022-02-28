# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

from __future__ import print_function

import collections
import functools
from pathlib import Path  

def test_main(level='full'):
    import sys
    import operator

    old_args = sys.argv
    if sys.argv.count('update') == 0:
        sys.argv = ['checkonly']

    # these generators will always be blocked
    generators_to_block = ['generate_exception_factory', 'generate_indicetest']
    # TODO: unblock 'generate_exception_factory' when we have 
    #       whole Core sources in Snap/test
    
    # these generators will be blocked if not running ironpython
    generators_to_block_if_not_ipy = ['generate_alltypes',
                                      'generate_exceptions',
                                      'generate_walker',
                                      'generate_comdispatch']


    # 'generate_exception_factory',  
    # populate list of generate_*.py from folder of this script
    generators = get_generators_in_folder()
    
    # filter list to remove blocked items, ie. they will not be run
    remove_blocked_generators(generators,generators_to_block)

    if sys.implementation.name != "ironpython":
        # filter list to remove blocked items if we are not ironpython
        remove_blocked_generators(generators,generators_to_block_if_not_ipy)
        
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


def get_generators_in_folder():
    # get the folder with this test
    path_of_test = Path(__file__)
    folder_of_test = path_of_test.parent

    # iterate over items in this folder and add generators
    generators = []
    
    for item in folder_of_test.iterdir():
        
        if item.is_file():
            # if current item is a file, get filename by accessing last path element
            filename = str(item.parts[-1])
            
            if filename.startswith("generate_") and filename.endswith(".py"):
                # found a generator, add it to our list (removing extension)
                generators.append(filename.replace(".py",""))
    
    return generators

def remove_blocked_generators(generators,blocklist):
    
    for g in blocklist:
        if g in generators:
            generators.remove(g)

if __name__=="__main__":
    test_main()
