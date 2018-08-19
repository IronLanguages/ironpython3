# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

from common import runtests
from .shared import define_maker
from .shared import setGenerator, test_exceptions
setGenerator(define_maker)
runtests(test_exceptions)
