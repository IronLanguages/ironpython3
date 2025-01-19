# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

from common import runtests
from .shared import while_loop_maker
from .shared import setGenerator, setKnownFailures, test_exceptions
setGenerator(while_loop_maker)

'''
def test8553():
    global log
    log+="preloop"
    whilevar1_12755 = 0
    while whilevar1_12755 < 3:
        whilevar1_12755 += 1
        log+="inloop"
        log+="predefine"
        def func2_12756():
            global log
            try:
                log+="try"
                log+="break"
                break
            except:
                log+="except"
                log+=dump_exc_info()
                log+="pass"
                pass
        func2_12756()
##
same## <type 'exceptions.SystemError'>
same## <type 'exceptions.SyntaxError'>
same## preloopinlooppredefinetrybreak
'''

setKnownFailures([  #IP emits SyntaxError...CPy emits SystemError.  Known 
                    #incompatibility.  See the docstring above for an example
                    #of this incompat.
                    8553, 8554, 8555, 8556, 8656, 8657, 8658, 8697, 8698, 8699, 
                    8738, 8739, 8740,
                    ])

runtests(test_exceptions)
