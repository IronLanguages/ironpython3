# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

from common import runtests
from .shared import try_except_maker3
from .shared import setGenerator, test_exceptions
setGenerator(try_except_maker3)
runtests(test_exceptions)
