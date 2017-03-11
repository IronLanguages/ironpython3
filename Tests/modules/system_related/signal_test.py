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
skiptest("silverlight")
import signal

'''
TODO: until we can send signals to other processes consistently (e.g., os.kill),
      signal is fairly difficult to test in an automated fashion. For now this
      will have to do.
'''

#--GLOBALS---------------------------------------------------------------------
SUPPORTED_SIGNALS = [   signal.SIGBREAK,
                        signal.SIGABRT,
                        signal.SIGINT, 
                        signal.SIGTERM,
                        signal.SIGFPE, 
                        signal.SIGILL, 
                        signal.SIGSEGV,
                        ]
#--RUN THIS FIRST--------------------------------------------------------------
def run_me_first():
    for x in [x for x in SUPPORTED_SIGNALS if x!=signal.SIGINT]:
        AreEqual(signal.getsignal(x), 0)
    AreEqual(signal.getsignal(signal.SIGINT), signal.default_int_handler)

    for x in range(1, 23):
        if x in SUPPORTED_SIGNALS: continue
        AreEqual(signal.getsignal(x), None)

run_me_first()

#--TEST CASES------------------------------------------------------------------
def test_signal_getsignal_negative():
    for x in [-2, -1, 0, 23, 24, 25]:
        AssertErrorWithMessage(ValueError, "signal number out of range",
                               signal.getsignal, x)
    for x in [None, "abc", "14"]:
        AssertError(TypeError, signal.getsignal, x)

def test_module_constants():
    AreEqual(signal.NSIG, 23)
    AreEqual(signal.SIGABRT, 22)
    AreEqual(signal.SIGBREAK, 21)
    AreEqual(signal.SIGFPE, 8)
    AreEqual(signal.SIGILL, 4)
    AreEqual(signal.SIGINT, 2)
    AreEqual(signal.SIGSEGV, 11)
    AreEqual(signal.SIGTERM, 15)
    AreEqual(signal.SIG_DFL, 0)
    AreEqual(signal.SIG_IGN, 1)

def test_doc():
    Assert("get the signal action for a given signal" in signal.__doc__)
    Assert("The default handler for SIGINT installed by Python" in signal.default_int_handler.__doc__)
    Assert("Return the current action for the given signal" in signal.getsignal.__doc__)
    Assert("Set the action for the given signal" in signal.signal.__doc__)

@skip("win32") #http://bugs.python.org/issue9324
def test_signal_signal_neg():
    def a(b, c):
        pass
    
    WEIRD_WORKING_CASES = [6]
    NO_SUCH_DIR = [21]
    
    for x in range(1, 23):
        if x in SUPPORTED_SIGNALS: continue
        if x in WEIRD_WORKING_CASES: continue
        AssertError(RuntimeError,
                    signal.signal, x, a)

    for x in [-2, -1, 0, 23, 24, 25]:
        AssertErrorWithMessage(ValueError, "signal number out of range",
                               signal.signal, x, a)

    def bad_sig0(): pass
    def bad_sig1(a): pass
    def bad_sig3(a,b,c): pass

    for y in SUPPORTED_SIGNALS + WEIRD_WORKING_CASES:
        bad_handlers = [-2, -1, 2, 3, 4, 10, 22, 23, 24, None] 
        for x in bad_handlers:
            AssertErrorWithMessage(TypeError, "signal handler must be signal.SIG_IGN, signal.SIG_DFL, or a callable object",
                                   signal.signal, y, x)


def test_signal_signal():
    WORKING_CASES = SUPPORTED_SIGNALS + [6]
    WEIRD_CASES = {
                   6: None,
                   2: signal.default_int_handler}
    for x in WORKING_CASES:
        #Ideal handler signature
        def a(signum, frame):
            return x
        
        ret_val = signal.signal(x, a)
        if x not in list(WEIRD_CASES.keys()):
            AreEqual(ret_val, signal.SIG_DFL)
        else:
            AreEqual(ret_val, WEIRD_CASES[x])
        AreEqual(a, signal.getsignal(x))
        
    #Strange handler signatures
    class KNew(object):
        def __call__(self, *args, **kwargs):
            pass
    
    a = KNew()
    ret_val = signal.signal(signal.SIGBREAK, a)
    AreEqual(a, signal.getsignal(signal.SIGBREAK))
                 

#--MAIN------------------------------------------------------------------------   
run_test(__name__)
