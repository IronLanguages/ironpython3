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

from common import runtests
from .shared import for_loop_maker
from .shared import setGenerator, setKnownFailures, test_exceptions

setGenerator(for_loop_maker)
setKnownFailures([  #IP emits SyntaxError...CPy emits SystemError.  Known incompatibility.
                    8553, 8554, 8555, 8556, 8656, 8657, 8658, 8697, 8698, 8699, 
                    8738, 8739, 8740,
                    ])


runtests(test_exceptions)
