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



