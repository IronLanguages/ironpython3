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

##IMPORTS######################################################################
import nt
import sys

##LOGGING######################################################################
from iptest.util     import get_env_var

try:
    import logging as l
    
    __log_file = get_env_var("TMP") + "\\iptest.log"
    l.basicConfig(level=l.DEBUG,
                  format="%(asctime)s %(levelname)-8s %(message)s",
                  filename=__log_file,
                  filemode="w+")
    __temp_handler = l.StreamHandler(sys.stdout)
    __temp_formatter = l.Formatter("%(asctime)s %(levelname)-8s %(message)s")
    __temp_handler.setFormatter(__temp_formatter)
    __temp_handler.setLevel(l.INFO)
    l.getLogger().addHandler(__temp_handler)

    #Hack needed because IP doesn't implement sys._getframe by default
    if sys.platform!="win32":
        l.getLogger().findCaller = lambda: ("Unknown", 0, "Unknown")
except:
    #Fake the implementation of logging module under Silverlight
    class __L(object):
        def debug(self, stuff):
            pass
        def info(self, stuff):
            print "INFO -", stuff
    l = __L()


##COMMAND-LINE OPTIONS#########################################################
class options:
    #Run test cases.
    RUN_TESTS=True
    
    #Indicates whether we should generate a test plan for IronPython from pydoc
    #comments.
    GEN_TEST_PLAN=False

#--help
if "--help" in sys.argv:
    print """iptest is used to run IronPython tests.

Notes:
- ???

Typical usage would be:
    ipy harness.py interop.net --plan
"""
    sys.exit(0)

#--no_testing
if "--no_testing" in sys.argv:
    options.RUN_TESTS = False
    sys.argv.remove("--no_testing")

#--plan
if "--plan" in sys.argv:
    options.GEN_TEST_PLAN = True
    sys.argv.remove("--plan")

#Do a little post processing
if options.GEN_TEST_PLAN:
    import pydoc

#Dump the flags
l.debug("sys.argv after processing: %s" % str(sys.argv))
l.debug("Command-line options:")
for x in [temp for temp in dir(options) if not temp.startswith("__")]:
    y = eval("options." + x)
    l.debug("\t%s = %s" % (x, y))
l.debug("")