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

import nt

#------------------------------------------------------------------------------
#--Helper functions

def get_env_var(key):
    '''
    Returns the environment variable denoted by key.  This helper function is
    necessary as IronPython's testing infrastructure throws away the case
    of environment variables.
    '''
    #nt.environ won't be on platforms like Silverlight
    if not hasattr(nt, "environ"):
        raise Exception("nt.environ not implemented!")
        
    l = [nt.environ[x] for x in nt.environ.keys() if x.lower() == key.lower()]
    if len(l)>0: 
        return l[0]
    else: 
        return None


def get_temp_dir():
    '''
    Returns a temporary directory.
    '''
    temp = get_environ_variable("TMP")
    if temp == None: 
        temp = get_environ_variable("TEMP")
    if (temp == None) or (' ' in temp): 
        temp = r"C:\temp"
    return temp

