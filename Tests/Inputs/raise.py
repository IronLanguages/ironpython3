# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

x = 1

class C(object):
    def __init__(self):
        self.attr = 1
        raise StopIteration

c = C()

# We will never get to this statement
y = 1