# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

from common import runtests
from .shared import for_loop_maker
from .shared import setGenerator, setKnownFailures, test_exceptions

setGenerator(for_loop_maker)
setKnownFailures([  #IP emits SyntaxError...CPy emits SystemError.  Known incompatibility.
                    8553, 8554, 8555, 8556, 8656, 8657, 8658, 8697, 8698, 8699, 
                    8738, 8739, 8740,
                    ])


runtests(test_exceptions)
