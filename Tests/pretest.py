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

# pretest.py
# ----------
# Passed to SuperConsole script to redirect standard output to file, and populate
# the dictionary for tab-completion tests.

import sys

STDOUT_ORIG = sys.stdout
STDERR_ORIG = sys.stderr
STDOUT_TEMP = None
STDERR_TEMP = None

def outputRedirectStart(errToOut=False):
    '''
    Redirects stdout/stderr to files.
    '''
    global STDOUT_TEMP
    global STDERR_TEMP

    #replace stdout with our own file
    STDOUT_TEMP = open('ip_session.log', 'w')
    sys.stdout = STDOUT_TEMP
    
    #send stderr to stdout or its own file
    if errToOut:
        STDERR_TEMP = STDOUT_TEMP
    else:
        STDERR_TEMP = open('ip_session_stderr.log', 'w')
    sys.stderr = STDERR_TEMP
    
def outputRedirectStop():
    global STDOUT_TEMP
    global STDERR_TEMP
    
    #close fake stdout/stderr
    STDOUT_TEMP.close()
    if STDOUT_TEMP!=STDERR_TEMP:
        STDERR_TEMP.close()

    #restore output streams
    sys.stdout = STDOUT_ORIG
    sys.stderr = STDERR_ORIG

# Ensure that an attribute with a prefix unique to the dictionary is properly completed.
######################################################################################################

# Only one attribute has 'z' has a prefix
zoltar = "zoltar"

# Two attributes have 'y' as a prefix, but only one has 'yo'
yorick = "yorick"
yak = "yak"

# Ensure that tabbing on a non-unique prefix cycles through the available options
######################################################################################################

# yorick and yak are used here also

# Ensure that tabbing after 'ident.' cycles through the available options
######################################################################################################

class C:
    'Cdoc'
    pass

c = C()
