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

##
## Test "-X:PrivateBinding"
##

from iptest.assert_util import *
skiptest("win32")
import System

privateBinding = "-X:PrivateBinding" in System.Environment.GetCommandLineArgs()

load_iron_python_test()
import IronPythonTest
from IronPythonTest import *

clsPart = ClsPart()

def Negate(i): return -i

def test_Common():
    AreEqual("InternalClsPart" in dir(IronPythonTest), privateBinding)
    AreEqual("InternalClsPart" in globals(), privateBinding)
    AreEqual("_ClsPart__privateField" in dir(ClsPart), privateBinding)
    AreEqual("_ClsPart__privateProperty" in dir(ClsPart), privateBinding)
    AreEqual("_ClsPart__privateEvent" in dir(ClsPart), privateBinding)
    AreEqual("_ClsPart__privateMethod" in dir(ClsPart), privateBinding)

if not privateBinding:
    def test_NormalBinding():
        try:
            from IronPythonTest.BinderTest import PrivateClass
        except ImportError:
            pass

        # mixed namespace
        import IronPython.Runtime
        AssertError(AttributeError, lambda: IronPython.Runtime.SetHelpers)
        
else:
    def test_PrivateBinding():
        # entirely internal namespace
        from IronPythonTest.BinderTest import PrivateClass
        
        # mixed namespace
        import Microsoft.Scripting
        x = Microsoft.Scripting.Actions.TopNamespaceTracker
        
        clsPart._ClsPart__privateField = 1
        AreEqual(clsPart._ClsPart__privateField, 1)
        clsPart._ClsPart__privateProperty = 1
        AreEqual(clsPart._ClsPart__privateProperty, 1)
        def bad_assign():
            clsPart._ClsPart__privateEvent = Negate
        AssertError(AttributeError, bad_assign)
        clsPart._ClsPart__privateEvent += Negate
        clsPart._ClsPart__privateEvent -= Negate
        AreEqual(clsPart._ClsPart__privateMethod(1), -1)
        
        # !!! internalClsPart = InternalClsPart()
        internalClsPart = IronPythonTest.InternalClsPart()
        internalClsPart._InternalClsPart__Field = 1
        AreEqual(internalClsPart._InternalClsPart__Field, 1)
        internalClsPart._InternalClsPart__Property = 1
        AreEqual(internalClsPart._InternalClsPart__Property, 1)
        def bad_assign():
            internalClsPart._InternalClsPart__Event = Negate
        AssertError(AttributeError, bad_assign)
        internalClsPart._InternalClsPart__Event += Negate
        internalClsPart._InternalClsPart__Event -= Negate
        AreEqual(internalClsPart._InternalClsPart__Method(1), -1)
        
        
    def test_PrivateStaticMethod():
        AreEqual(ClsPart._ClsPart__privateStaticMethod(), 100)
        
        AreEqual("_InternalClsPart__Field" in dir(IronPythonTest.InternalClsPart), True)
        AreEqual("_InternalClsPart__Property" in dir(InternalClsPart), True)
        AreEqual("_InternalClsPart__Method" in dir(InternalClsPart), True)

    @skip("netstandard", "posix") # no System.Windows.Forms
    def test_override_createparams():
        """verify we can override the CreateParams property and get the expected value from the base class"""
    
        clr.AddReference("System.Windows.Forms")
        from System.Windows.Forms import Label, Control
        
        for val in [20, 0xffff]:
            global called
            called = False
            class TransLabel(Label):
                def get_CreateParams(self):
                    global called
                    cp = super(TransLabel, self).CreateParams
                    cp.ExStyle = cp.ExStyle | val
                    called = True
                    return cp
                CreateParams = property(fget=get_CreateParams)
        
            a = TransLabel()
            AreEqual(called, True)

    def test_misc_coverage():
        import clr
        clr.AddReference("IronPython")
        from IronPython.Runtime.Types import SlotFieldAttribute as SFA
        
        temp = SFA()
        AreEqual(temp.GetType().Name, "SlotFieldAttribute")

# use this when running standalone
#run_test(__name__)

run_test(__name__, noOutputPlease=True)

if not privateBinding:
    from iptest.process_util import launch_ironpython_changing_extensions
    AreEqual(launch_ironpython_changing_extensions(__file__, add=["-X:PrivateBinding"]), 0)
