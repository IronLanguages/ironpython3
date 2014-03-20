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
## To test __future__ related areas where __future__ is NOT enabled
## in the module scope
##

from iptest.assert_util import *

skiptest("silverlight")

from iptest.file_util import *

def always_true():
    exec "AreEqual(1 / 2, 0)"
    exec "from __future__ import division; AreEqual(1 / 2, 0.5)"
    AreEqual(1/2, 0)
    AreEqual(eval("1/2"), 0)

tempfile = path_combine(testpath.temporary_dir, "temp_future.py")

assert_code = '''
def CustomAssert(c):
    if not c: raise AssertionError("Assertion Failed")

'''

code1  = assert_code + '''
exec "CustomAssert(1/2 == 0)"
exec "from __future__ import division; CustomAssert(1/2 == 0.5)"
CustomAssert(1/2 == 0)
CustomAssert(eval('1/2') == 0)
'''

code2 = '''
from __future__ import division
''' + assert_code + '''
exec "CustomAssert(1/2 == 0.5)"
exec "from __future__ import division; CustomAssert(1/2 == 0.5)"
CustomAssert(1/2 == 0.5)
CustomAssert(eval('1/2') == 0.5)
'''

def f1(): execfile(tempfile)
def f2(): exec(compile(code, tempfile, "exec"))
def f3(): exec(code)
def f4():
    if is_cli:
        # import IronPython
        #pe = IronPython.Hosting.PythonEngine()
        #issue around py hosting py again.
        pass
        
always_true()
try:
    import sys
    save = sys.path[:]
    sys.path.append(testpath.temporary_dir)
    
    for code in (code1, code2):
        always_true()
        write_to_file(tempfile, code)

        for f in (f1, f2, f3, f4):
            f()
            always_true()

        # test after importing
        import temp_future
        always_true()
        print temp_future
        reloaded = reload(temp_future)
        always_true()
        
finally:
    sys.path = save
    delete_files(tempfile)
    
