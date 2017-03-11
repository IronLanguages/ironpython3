#####################################################################################
#
#  Copyright (c) Michael van der Kolff. All rights reserved.
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

##
## Test whether struct.pack (& others) are threadsafe.  For the purpose of this test,
## the only thing we care about is that no exception is thrown.
##

from iptest.assert_util import *
import struct
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

def test_packs():
    pack_threads = [PackThread() for i in range(10)]
    for t in pack_threads:
        t.start()
    for t in pack_threads:
        t.join()
    Assert(all(t.retval for t in pack_threads), "struct.pack: Is not threadsafe")

run_test(__name__)
