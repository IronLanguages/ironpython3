# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Test weakref
##
## * Since the IronPython GC heavily differs from CPython GC (absence of reference counting), 
##   the CPython unit tests cannot fully be made pass on IronPython without modification
##
## * Comments below in double quotes are from the Python standard library documentation.
##
## * Issues of the current implementation of _weakref.cs:
##
##    - weakref finalization callbacks are run in the CLR finalizer thread. 
##      This is likely to cause data races in user code.
##    - WeakRefTracker.cs code and internal state handling most likely is not
##      implemented in a thread-safe way.
##

import gc
import weakref

from iptest import IronPythonTestCase, run_test

class C(object):
    def __init__(self, value=0):
        self.value = value
    def __hash__(self):
        return hash(self.value)
    def __eq__(self, other):
        return isinstance(other, C) and self.value == other.value
    def __ne__(self, other):
        return not self.__eq__(other)

class WeakrefTest(IronPythonTestCase):
    def _create_weakrefs(self, o, count, cb = None):
        # force creation of different instances for the same target
        if not cb and count > 1:
            cb = lambda r: None

        if count==1:
            return weakref.ref(o, cb)
        elif count==2:
            r1, r2 = weakref.ref(o, cb), weakref.ref(o, cb)
            self.assertTrue(r1 is not r2)
            return r1, r2
        else:
            raise Exception("not implemented")

    def test_ref_callable(self):
        # "if the referent is no longer alive, calling the reference object will cause None to 
        # be returned"

        o = C("a")
        r = self._create_weakrefs(o, 1)
        # for reasons stated in create_weakrefs(), we cannot test on instance equality
        self.assertTrue(r().value == "a")

        del o
        gc.collect()

        self.assertTrue(r() is None)

    def test_ref_hashable(self):
        # "Weak references are hashable if the object is hashable. They will maintain their hash value 
        # even after the object was deleted. If hash() is called the first time only after the object 
        # was deleted, the call will raise TypeError."

        o = C("a")
        r1, r2 = self._create_weakrefs(o, 2)
        self.assertTrue(hash(r1) == hash("a"))

        del o
        gc.collect()

        self.assertTrue(r1() is None)
        self.assertTrue(r2() is None)
        self.assertTrue(hash(r1) == hash("a"))
        self.assertRaises(TypeError, lambda: hash(r2))

    def test_ref_equality(self):
        # "If the referents are still alive, two references have the same equality relationship as 
        # their referents (regardless of the callback). If either referent has been deleted, the 
        # references are equal only if the reference objects are the same object."

        o, o2 = C("a"), C("a")
        r1, r2 = self._create_weakrefs(o, 2)
        r3 = self._create_weakrefs(o2, 1)
        self.assertTrue(r1 == r2)
        self.assertTrue(r1 == r3)

        del o, o2
        gc.collect()

        self.assertTrue(r1() is None)
        self.assertTrue(r3() is None)
        self.assertTrue(r1 != r2)
        self.assertTrue(r1 != r3)

run_test(__name__)
