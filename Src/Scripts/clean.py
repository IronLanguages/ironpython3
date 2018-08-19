# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.


import os

def is_binary(filename):
    root, ext = os.path.splitext(filename)
    return ext in ['.pyc', '.pyo', '.pdb', '.exe', '.dll', '.projdata']

def do_dir(dirname):
    if dirname == BIN_DIR: return

    for file in os.listdir(dirname):
        filename = os.path.join(dirname, file)
        if os.path.isdir(filename):
            do_dir(filename)
        elif is_binary(filename):
            print 'deleting', filename
            os.remove(filename)

TOP_DIR = "c:\\IronPython-0.7"
BIN_DIR = os.path.join(TOP_DIR, "bin")

do_dir(TOP_DIR)



