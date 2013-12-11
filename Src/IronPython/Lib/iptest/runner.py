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

#------------------------------------------------------------------------------
#--IMPORTS
import sys
import nt
from exceptions import SystemExit

from iptest import *

if sys.platform!="win32":
    from System.IO.Path import GetFullPath as get_path
    from System.IO.File import Exists as file_exists
else:
    from os.path import abspath as get_path
    from os.path import exists  as file_exists
    

#------------------------------------------------------------------------------
#--GLOBALS

#Logging levels
__DEBUG_LVL = 0
__INFO_LVL  = 1
__WARN_LVL  = 2
__ERROR_LVL = 3
__CRITICAL_LVL = 4

#Current logging level
LOGGING_LVL = 1

#Save the current working directory before running any tests which in turn may
#inadvertantly change it
__CWD = nt.getcwd()


    

#------------------------------------------------------------------------------
#--HELPER FUNCTIONS

#If we could take a dependency on CPython, we should use the logging module 
#here instead.  In any event, these should be moved out of runner.py later.
def log_debug(msg):
    if LOGGING_LVL <= __DEBUG_LVL:
        print "DEBUG", str(msg)

def log_info(msg):
    if LOGGING_LVL <= __INFO_LVL:
        print "INFO", str(msg)
        
def log_warn(msg):
    if LOGGING_LVL <= __WARN_LVL:
        print "WARN", str(msg)
        
def log_error(msg):
    if LOGGING_LVL <= __INFO_LVL:
        print "ERROR", str(msg)
    
def log_critical(msg):
    if LOGGING_LVL <= __CRITICAL_LVL:
        print "CRITICAL", str(msg)


#------------------------------------------------------------------------------
def run_test_pkg(pkg_name, do_not_run=[]):
    log_info("--%s package----------------------------------------" % pkg_name)
    
    #Determine where the test package is and ensure it exists
    log_debug("The current working directory is " + __CWD)
    pkg_dir_name = __CWD + "\\" + pkg_name.replace(".", "\\")
    log_debug("The test package location is " + pkg_dir_name)
    if not file_exists(pkg_dir_name + r"\__init__.py"):
        err_msg = "No such test package: %s" % pkg_dir_name
        log_error(err_msg)
        raise Exception(err_msg)
    
    #Build up a list of all subpackages/modules contained in test package
    subpkg_list = [x for x in nt.listdir(pkg_dir_name) if not x.endswith(".py") and file_exists(pkg_dir_name + "\\" + x + "\\__init__.py")]
    log_debug("Subpackages found: %s" % (str(subpkg_list)))
    module_list = [x for x in nt.listdir(pkg_dir_name) if x.endswith(".py") and x!="__init__.py"]
    log_debug("Modules found: %s" % (str(module_list)))
    if len(module_list)==0:
        log_warn("No test modules found in the %s test package!" % pkg_name)
        print ""
    
    if options.GEN_TEST_PLAN:
        l.info("Generating test documentation for '%s' package..." % pkg_name)
        pydoc.writedoc(pkg_name)
    
    #Import all tests
    for test_module in module_list:
        test_module = pkg_name + "." + test_module.split(".py", 1)[0]
        
        if options.RUN_TESTS:
            if test_module in do_not_run:
                log_info("--Testing of %s has been disabled!" % test_module)
                continue
            log_info("--Testing %s..." % test_module)
            try:
                __import__(test_module)
            except SystemExit, e:
                if e.code!=0: 
                    raise Exception("Importing '%s' caused an unexpected exit code: %s" % (test_module, str(e.code)))
            print ""
        
        if options.GEN_TEST_PLAN:
            l.info("Generating test documentation for '%s' module..." % test_module)
            pydoc.writedoc(test_module)

    
    #Recursively import subpackages
    for subpkg in subpkg_list:
        run_test_pkg(pkg_name + "." + subpkg)
        