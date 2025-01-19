# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.
#
# Copyright (c) Michael van der Kolff
#

##
## Test whether struct.pack (& others) are threadsafe.  For the purpose of this test,
## the only thing we care about is that no exception is thrown.
##

import struct
import unittest

from iptest import run_test
from threading import Thread
from random import shuffle

struct_pack_args = [
    ("<II", 5, 7),
    (">II", 7, 9),
    ("<HHH", 5, 7, 9),
    (">HII", 5, 7, 9),
    (">QQQ", 5, 7, 9),
    ("<QQQ", 5, 7, 9),
    (">QQQQQ", 3, 9, 5, 7, 9),
    ("dd", 5., 7.9),
    ("fd", 5., 7.9),
    ("df", 5., 7.9),
    ("ff", 5., 7.9),
]

class PackThread(Thread):
    def __init__(self, group=None, target=None, name=None, args=(), kwargs={}):
        self.retval = None
        return super(PackThread, self).__init__(group, target, name, args, kwargs)

    def run(self):
        my_args = list(struct_pack_args)
        shuffle(my_args)
        try:
            for i in range(100000):
                for args in my_args:
                    struct.pack(*args)
        except:
            self.retval = False
            return
        self.retval = True

class StructThreadsafeTest(unittest.TestCase):

    def test_packs(self):
        pack_threads = [PackThread() for i in range(10)]
        for t in pack_threads:
            t.start()
        for t in pack_threads:
            t.join()
        self.assertTrue(all(t.retval for t in pack_threads), "struct.pack: Is not threadsafe")

run_test(__name__)
