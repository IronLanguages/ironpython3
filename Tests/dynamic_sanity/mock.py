# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import random, math

class Mock(object): 
    def __getattr__(self, key):
        """Mock objects of this type will dynamically implement any requested member"""
        return random.choice(["world", math.pi])

m = Mock()