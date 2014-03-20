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

# ADO Database

from iptest.assert_util import skiptest
skiptest("win32", "silverlight", "cli64")
from iptest.cominterop_util import *

import System

#------------------------------------------------------------------------------
#--GLOBALS

#------------------------------------------------------------------------------
#--TESTS
def test_cp18225():
    com_obj_cmd = System.Activator.CreateInstance(System.Type.GetTypeFromProgID("ADODB.Command"))
    com_obj_cmd.CommandTimeout = 50
    AreEqual(com_obj_cmd.CommandTimeout, 50)
    
    com_obj_conn = System.Activator.CreateInstance(System.Type.GetTypeFromProgID("ADODB.Connection"))
    com_obj_conn.CommandTimeout = 40
    AreEqual(com_obj_conn.CommandTimeout, 40)
    
#------------------------------------------------------------------------------
run_com_test(__name__, __file__)