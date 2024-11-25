# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

# ADO Database

from iptest.assert_util import skiptest
skiptest("win32", "cli64", "posix")
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