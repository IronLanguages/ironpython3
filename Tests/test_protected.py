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

import sys
from iptest.assert_util import *
skiptest("win32")

load_iron_python_test()
from IronPythonTest import *

# properties w/ differening access
def test_base():
    # TODO: revisit this
    # can't access protected methods directly in Silverlight
    # (need to create a derived class)
    #if not is_silverlight:
    #    a = BaseClass()
    #    AreEqual(a.Area, 0)
    #    a.Area = 16
    #    AreEqual(a.Area, 16)
        
    class WrapBaseClass(BaseClass): pass
    a = WrapBaseClass()
    AreEqual(a.Area, 0)
    a.Area = 16
    AreEqual(a.Area, 16)


def test_derived():
    class MyBaseClass(BaseClass):
        def MySetArea(self, size):
            self.Area = size

    a = MyBaseClass()
    AreEqual(a.Area, 0)

    a.MySetArea(16)
    AreEqual(a.Area, 16)

    a.Area = 36
    AreEqual(a.Area, 36)

    # protected fields
    AreEqual(a.foo, 0)
    a.foo = 7
    AreEqual(a.foo, 7)

def test_super_protected():
    class x(object): pass
    
    clone = super(x, x()).MemberwiseClone()
    AreEqual(type(clone), x)

def test_override():
    # overriding methods

    # can't access protected methods directly
    a = Inherited()
    
    # they are present...
    Assert('ProtectedMethod' in dir(a))
    Assert('ProtectedProperty' in dir(a))
    Assert(hasattr(a, 'ProtectedMethod'))
    
    # hasattr returns false if the getter raises...
    Assert(not hasattr(a, 'ProtectedProperty'))
    AssertErrorWithMessage(TypeError, "cannot access protected member ProtectedProperty without a python subclass of Inherited", lambda : a.ProtectedProperty)
    
    class WrapInherited(Inherited): pass
    a = WrapInherited()
    AreEqual(a.ProtectedMethod(), 'Inherited.ProtectedMethod')
    AreEqual(a.ProtectedProperty, 'Inherited.Protected')

    class MyInherited(Inherited):
        def ProtectedMethod(self):
            return "MyInherited"
        def ProtectedMethod(self):
            return "MyInherited Override"
        def ProtectedPropertyGetter(self):
            return "MyInherited.Protected"
        ProtectedProperty = property(ProtectedPropertyGetter)

    a = MyInherited()
    
    AreEqual(a.ProtectedMethod(), 'MyInherited Override')
    AreEqual(a.CallProtected(), 'MyInherited Override')
    AreEqual(a.ProtectedProperty, "MyInherited.Protected")
    AreEqual(a.CallProtectedProp(), "MyInherited.Protected")
    

run_test(__name__)
