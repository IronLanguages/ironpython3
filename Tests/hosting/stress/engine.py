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

'''
Testing IronPython Engine under a few stressful scenarios
'''

#--IMPORTS---------------------------------------------------------------------
from iptest.assert_util import *
load_iron_python_test()
import IronPythonTest

#--GLOBALS---------------------------------------------------------------------
engine = IronPythonTest.Stress.Engine()
multipleexecskips = [ "ScenarioXGC"]

#--TEST CASES------------------------------------------------------------------
for s in dir(engine):
    if s.startswith("Scenario"):
        if s in multipleexecskips:
            exec '@skip("multiple_execute") \ndef test_Engine_%s(): getattr(engine, "%s")()' % (s, s)
        else :
            exec 'def test_Engine_%s(): getattr(engine, "%s")()' % (s, s)

#--MAIN------------------------------------------------------------------------
run_test(__name__)
