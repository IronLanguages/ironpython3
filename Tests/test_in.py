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

from iptest.assert_util import *

Assert('abc' in 'abcd')

class C:
    x = "Hello"
    def __contains__(self, y):
        return self.x == y;


class D:
    x = (1,2,3,4,5,6,7,8,9,10)
    def __getitem__(self, y):
        return self.x[y];


h = "Hello"
c = C()
Assert(c.__contains__("Hello"))
Assert(c.__contains__(h))
Assert(not (c.__contains__('abc')))

Assert(h in c)
Assert("Hello" in c)

d = D()
Assert(1 in d)
Assert(not(11 in d))

#***** Above code are from 'InTest' *****
