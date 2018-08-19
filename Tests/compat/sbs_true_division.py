# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.



# not all are testing true division

import sys
from common import *
import testdata

collection = testdata.merge_lists(
                    [None],
                    testdata.list_int,
                    testdata.list_float,
                    testdata.list_bool,
                    testdata.list_long,
                    testdata.list_complex,
                    
                    testdata.list_myint,
                    testdata.list_myfloat,
                    testdata.list_mylong,
                    testdata.list_mycomplex,
                )

class oldstyle:
    def __init__(self, value):
        self.value = value
    def __truediv__(self, other):
        return self.value / other
    def __rtruediv__(self, other):
        return other / self.value        
    def __itruediv__(self, other):
        # left side is no longer an oldstyle instance after /=
        return self.value / other 
    def __repr__(self):
        return "oldstyle(%s)" % str(self.value)

class newstyle(object):         
    def __init__(self, value):
        self.value = value
    def __truediv__(self, other):
        return self.value / other
    def __rtruediv__(self, other):
        return other / self.value        
    def __itruediv__(self, other):
        return self.value / other        
    def __repr__(self):
        return "newstyle(%s)" % str(self.value)

collection_oldstyle = [oldstyle(x) for x in collection]
collection_newstyle = [newstyle(x) for x in collection]

def clone_list(l):
    l2 = []
    for x in l:
        if x is newstyle:
            l2.append(newstyle(x.value))
        elif x is oldstyle:
            l2.append(oldstyle(x.value))
        else :
            l2.append(x)
    return l2
        
class common(object):
    def division(self, leftc, rightc):
        for x in leftc:
            for y in rightc:
                printwith("case", x, "/", y, type(x), type(y))
                try: 
                    ret = x / y
                    printwithtype(ret)
                except:
                    printwith("same", sys.exc_info()[0])
    def inplace(self, leftc, rightc):
        rc = clone_list(rightc)
        for y in rc:
            for x in clone_list(leftc):
                printwith("case", x, "/=", y, type(x), type(y))
                try: 
                    x /= y
                    printwithtype(x)
                except:
                    printwith("same", sys.exc_info()[0])
                    
class test_division(common):
    def test_simple(self):      super(test_division, self).division(collection, collection)
    
    def test_class_ol(self):    super(test_division, self).division(collection_oldstyle, collection)
    def test_class_or(self):    super(test_division, self).division(collection, collection_oldstyle)
    def test_class_nl(self):    super(test_division, self).division(collection_newstyle, collection)
    def test_class_nr(self):    super(test_division, self).division(collection, collection_newstyle)
    
    def test_ip_simple(self):      super(test_division, self).inplace(collection, collection)
    
    def test_ip_class_ol(self):    super(test_division, self).inplace(collection_oldstyle, collection)
    def test_ip_class_or(self):    super(test_division, self).inplace(collection, collection_oldstyle)
    def test_ip_class_nl(self):    super(test_division, self).inplace(collection_newstyle, collection)
    def test_ip_class_nr(self):    super(test_division, self).inplace(collection, collection_newstyle)

runtests(test_division)

