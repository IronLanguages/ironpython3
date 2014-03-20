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

from iptest.assert_util import *

# Simple case

def trick(p):
    return "trick"

@trick
def f():
    pass

AreEqual(f, "trick")

# Class as decorator

class wrap:
    def __init__(self, fnc):
        self.fnc = fnc
    def __call__(self):
        return "wrapped"

@wrap
def f():
    pass
Assert(isinstance(f, wrap))
AreEqual(f(), "wrapped")

# Parameters

class eat:
    def __call__(self, fnc):
        return self

def parm(a,b,c="default c", d="default d"):
    e = eat()
    e.args = a,b,c,d
    return e

@parm(1,2)
def f():
    pass

Assert(isinstance(f, eat))
AreEqual(f.args, (1,2,"default c", "default d"))

@parm(1,2,"new c")
def f():
    pass

Assert(isinstance(f, eat))
AreEqual(f.args, (1,2,"new c", "default d"))

# Execution order

def d1(a):
    a.append("d1")
    return a

def d2(a):
    a.append("d2")
    return a

def d3(a):
    a.append("d3")
    return a

def d4(a):
    a.append("d4")
    return a

def d5(a):
    a.append("d5")
    return a

def first(f):
    return ["first"]

@d1
@d2
@d3
@d4
@d5
@first
def f():
    return 10

AreEqual(f, ["first", "d5", "d4", "d3", "d2", "d1"])

# More complicated cases

class capture:
    def __init__(self, *args):
        self.args = args
    def __call__(self, f):
        f.append(self.args)
        return f

@capture(1,2)
@d3
@capture(3,4,5)
@d4
@capture("Hello")
@d5
@capture("First")
@first
def f():
    pass

AreEqual(f, ['first', ('First',), 'd5', ('Hello',), 'd4', (3, 4, 5), 'd3', (1, 2)])

# Dotted names

class dn:
    def d1(self, a):
        a.append("dn.d1")
        return a

    def d2(self, a):
        a.append("dn.d2")
        return a

    def d3(self, a):
        a.append("dn.d3")
        return a

    def d4(self, a):
        a.append("dn.d4")
        return a

    def d5(self, a):
        a.append("dn.d5")
        return a

    def first(self, f):
        return ["dn.first"]

x = dn()

@x.d1
@d2
@x.d3
@d4
@x.d5
@first
def f():
    return 10

AreEqual(f, ['first', 'dn.d5', 'd4', 'dn.d3', 'd2', 'dn.d1'])

 