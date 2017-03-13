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

from common import *
import testdata

class oldstyle: 
    def __init__(self, value):          self.value = value
    def __repr__(self):                 return "oldstyle(%s)" % self.value
    def __add__(self, other):           return self.value + other
    def __sub__(self, other):           return self.value - other
    def __mul__(self, other):           return self.value * other
    def __div__(self, other):           return self.value / other
    def __floordiv__(self, other):      return self.value // other
    def __mod__(self, other):           return self.value % other
    def __divmod__(self, other):        return divmod(self.value,  other)
    def __pow__(self, other):           return self.value ** other
    def __lshift__(self, other):        return self.value << other
    def __rshift__(self, other):        return self.value >> other
    def __and__(self, other):           return self.value & other
    def __xor__(self, other):           return self.value ^ other
    def __or__(self, other):            return self.value | other

class oldstyle_reflect: 
    def __init__(self, value):          self.value = value
    def __repr__(self):                 return "oldstyle_reflect(%s)" % self.value
    def __radd__(self, other):          return other + self.value
    def __rsub__(self, other):          return other - self.value
    def __rmul__(self, other):          
        print("\toldstyle_reflect.__rmul__")
        return other * self.value
    def __rdiv__(self, other):          return other / self.value
    def __rfloordiv__(self, other):     return other // self.value
    def __rmod__(self, other):          return other % self.value
    def __rdivmod__(self, other):       return divmod(other, self.value)
    def __rpow__(self, other):          return other ** self.value
    def __rlshift__(self, other):       return other << self.value
    def __rrshift__(self, other):       return other >> self.value
    def __rand__(self, other):          return self.value & other
    def __rxor__(self, other):          return self.value ^ other
    def __ror__(self, other):           return self.value | other
        
class oldstyle_inplace: 
    def __init__(self, value):          self.value = value
    def __repr__(self):                 return "oldstyle_inplace(%s)" % self.value
    def __iadd__(self, other):          return self.value + other
    def __isub__(self, other):          return self.value - other
    def __imul__(self, other):          return self.value * other
    def __idiv__(self, other):          return self.value / other
    def __ifloordiv__(self, other):     return self.value // other
    def __imod__(self, other):          return self.value % other
    def __idivmod__(self, other):        return divmod(self.value,  other)
    def __ipow__(self, other):          return self.value ** other
    def __ilshift__(self, other):       return self.value << other
    def __irshift__(self, other):       return self.value >> other
    def __iand__(self, other):           return self.value & other
    def __ixor__(self, other):          return self.value ^ other
    def __ior__(self, other):           return self.value | other   

class oldstyle_notdefined: 
    def __init__(self, value):          self.value = value
    def __repr__(self):                 return "oldstyle_notdefined(%s)" % self.value

class newstyle(object): 
    def __init__(self, value):          self.value = value
    def __repr__(self):                 return "newstyle(%s, %r)" % (self.value, type(self.value))
    def __add__(self, other):           return self.value + other
    def __sub__(self, other):           return self.value - other
    def __mul__(self, other):           return self.value * other
    def __div__(self, other):           return self.value / other
    def __floordiv__(self, other):      return self.value // other
    def __mod__(self, other):           return self.value % other
    def __divmod__(self, other):        return divmod(self.value,  other)
    def __pow__(self, other):           return self.value ** other
    def __lshift__(self, other):        return self.value << other
    def __rshift__(self, other):        return self.value >> other
    def __and__(self, other):           return self.value & other
    def __xor__(self, other):           return self.value ^ other
    def __or__(self, other):            return self.value | other

class newstyle_reflect(object): 
    def __init__(self, value):          self.value = value
    def __repr__(self):                 return "newstyle_reflect(%s, %r)" % (self.value, type(self.value))
    def __radd__(self, other):          return other + self.value
    def __rsub__(self, other):          return other - self.value
    def __rmul__(self, other):          
        print("\tnewstyle_reflect.__rmul__")
        return other * self.value
    def __rdiv__(self, other):          return other / self.value
    def __rfloordiv__(self, other):     return other // self.value
    def __rmod__(self, other):          return other % self.value
    def __rdivmod__(self, other):       return divmod(other, self.value)
    def __rpow__(self, other):          return other ** self.value
    def __rlshift__(self, other):       return other << self.value
    def __rrshift__(self, other):       return other >> self.value
    def __rand__(self, other):          return self.value & other
    def __rxor__(self, other):          return self.value ^ other
    def __ror__(self, other):           return self.value | other
        
class newstyle_inplace(object): 
    def __init__(self, value):          self.value = value
    def __repr__(self):                 return "newstyle_inplace(%s, %r)" % (self.value, type(self.value))
    def __iadd__(self, other):          return self.value + other
    def __isub__(self, other):          return self.value - other
    def __imul__(self, other):          return self.value * other
    def __idiv__(self, other):          return self.value / other
    def __ifloordiv__(self, other):     return self.value // other
    def __imod__(self, other):          return self.value % other
    def __idivmod__(self, other):        return divmod(self.value,  other)
    def __ipow__(self, other):          return self.value ** other
    def __ilshift__(self, other):       return self.value << other
    def __irshift__(self, other):       return self.value >> other
    def __iand__(self, other):           return self.value & other
    def __ixor__(self, other):          return self.value ^ other
    def __ior__(self, other):           return self.value | other   

class newstyle_notdefined(object): 
    def __init__(self, value):          self.value = value
    def __repr__(self):                 return "newstyle_notdefined(%s, %r)" % (self.value, type(self.value))

import sys

class common(object):
    def normal(self, leftc, rightc):
        for a in leftc:
            for b in rightc:
                try:
                    printwith("case", a, "+",  b, type(a), type(b))
                    printwithtype(a + b)
                except: 
                    printwith("same", sys.exc_info()[0])
                try:
                    printwith("case", a, "-",  b, type(a), type(b))
                    printwithtype(a - b)
                except: 
                    printwith("same", sys.exc_info()[0])
                try:
                    printwith("case", a, "*",  b, type(a), type(b))
                    printwithtype(a * b)
                except: 
                    printwith("same", sys.exc_info()[0])
                try:
                    printwith("case", a, "/",  b, type(a), type(b))
                    printwithtype(a / b)
                except: 
                    printwith("same", sys.exc_info()[0])
                try:
                    printwith("case", a, "//",  b, type(a), type(b))
                    printwithtype(a // b)
                except: 
                    printwith("same", sys.exc_info()[0])
                try:
                    printwith("case", a, "%",  b, type(a), type(b))
                    printwithtype(a % b)
                except: 
                    printwith("same", sys.exc_info()[0])
                try:
                    printwith("case", a, "**",  b, type(a), type(b))
                    printwithtype(a ** b)
                except: 
                    printwith("same", sys.exc_info()[0])
                try:
                    printwith("case", a, "<<",  b, type(a), type(b))
                    printwithtype(a << b)
                except: 
                    printwith("same", sys.exc_info()[0])
                try:
                    printwith("case", a, ">>",  b, type(a), type(b))
                    printwithtype(a >> b)
                except: 
                    printwith("same", sys.exc_info()[0])
                try:
                    printwith("case", a, "&",  b, type(a), type(b))
                    printwithtype(a & b)
                except: 
                    printwith("same", sys.exc_info()[0])
                try:
                    printwith("case", a, "^",  b, type(a), type(b))
                    printwithtype(a ^ b)
                except: 
                    printwith("same", sys.exc_info()[0])
                try:
                    printwith("case", a, "|",  b, type(a), type(b))
                    printwithtype(a | b)
                except: 
                    printwith("same", sys.exc_info()[0])
    
    def clone_list(self, l):
        l2 = []
        for x in l:
            if x is newstyle_inplace:
                l2.append(newstyle_inplace(x.value))
            elif x is oldstyle_inplace:
                l2.append(oldstyle_inplace(x.value))
            else :
                l2.append(x)
        return l2

    def inplace(self, leftc, rightc):
        rc = self.clone_list(rightc)
        for b in rc:
            lc = self.clone_list(leftc)
            for a in lc:
                try:
                    op = "+"
                    printwith("case", "%s %s= %s" % (a, op, b), type(a), type(b))
                    a += b
                    printwithtype(a)
                except: 
                    printwith("same", sys.exc_info()[0])    
            lc = self.clone_list(leftc)
            for a in lc:
                try:
                    op = "-"
                    printwith("case", "%s %s= %s" % (a, op, b), type(a), type(b))
                    a -= b
                    printwithtype(a)
                except: 
                    printwith("same", sys.exc_info()[0])    
            lc = self.clone_list(leftc)
            for a in lc:
                try:
                    op = "*"
                    printwith("case", "%s %s= %s" % (a, op, b), type(a), type(b))
                    a *= b
                    printwithtype(a)
                except: 
                    printwith("same", sys.exc_info()[0])    
            lc = self.clone_list(leftc)
            for a in lc:
                try:
                    op = "//"
                    printwith("case", "%s %s= %s" % (a, op, b), type(a), type(b))
                    a //= b
                    printwithtype(a)
                except: 
                    printwith("same", sys.exc_info()[0])    
            lc = self.clone_list(leftc)
            for a in lc:
                try:
                    op = "%"
                    printwith("case", "%s %s= %s" % (a, op, b), type(a), type(b))
                    a %= b
                    printwithtype(a)
                except: 
                    printwith("same", sys.exc_info()[0])    
            lc = self.clone_list(leftc)
            for a in lc:
                try:
                    op = "**"
                    printwith("case", "%s %s= %s" % (a, op, b), type(a), type(b))
                    a **= b
                    printwithtype(a)
                except: 
                    printwith("same", sys.exc_info()[0])    
            lc = self.clone_list(leftc)
            for a in lc:
                try:
                    op = "<<"
                    printwith("case", "%s %s= %s" % (a, op, b), type(a), type(b))
                    a <<= b
                    printwithtype(a)
                except: 
                    printwith("same", sys.exc_info()[0])    
            lc = self.clone_list(leftc)
            for a in lc:
                try:
                    op = ">>"
                    printwith("case", "%s %s= %s" % (a, op, b), type(a), type(b))
                    a >>= b
                    printwithtype(a)
                except: 
                    printwith("same", sys.exc_info()[0])    
            lc = self.clone_list(leftc)
            for a in lc:
                try:
                    op = "&"
                    printwith("case", "%s %s= %s" % (a, op, b), type(a), type(b))
                    a &= b
                    printwithtype(a)
                except: 
                    printwith("same", sys.exc_info()[0])    
            lc = self.clone_list(leftc)
            for a in lc:
                try:
                    op = "^"
                    printwith("case", "%s %s= %s" % (a, op, b), type(a), type(b))
                    a ^= b
                    printwithtype(a)
                except: 
                    printwith("same", sys.exc_info()[0])    
            lc = self.clone_list(leftc)
            for a in lc:
                try:
                    op = "|"
                    printwith("case", "%s %s= %s" % (a, op, b), type(a), type(b))
                    a |= b
                    printwithtype(a)
                except: 
                    printwith("same", sys.exc_info()[0])

class ops_simple(common):
    def __init__(self):
        self.collection = testdata.merge_lists(
                [None], 
                testdata.list_bool,
                testdata.list_int,
                testdata.list_float,
                testdata.list_long[:-1],        # the last number is very long
                testdata.list_complex,

                testdata.list_myint,
                testdata.list_myfloat,
                testdata.list_mylong,
                testdata.list_mycomplex,
                testdata.get_Int64_Byte(),
                )

        self.collection_oldstyle            = [oldstyle(x)              for x in self.collection]
        self.collection_oldstyle_reflect    = [oldstyle_reflect(x)      for x in self.collection]
        self.collection_oldstyle_notdefined = [oldstyle_notdefined(x)   for x in self.collection]
        self.collection_newstyle            = [newstyle(x)              for x in self.collection]
        self.collection_newstyle_reflect    = [newstyle_reflect(x)      for x in self.collection]
        self.collection_newstyle_notdefined = [newstyle_notdefined(x)   for x in self.collection]

        self.collection_oldstyle_inplace    = [oldstyle_inplace(x)      for x in self.collection]
        self.collection_newstyle_inplace    = [newstyle_inplace(x)      for x in self.collection]

    def test_normal(self):              super(ops_simple, self).normal(self.collection, self.collection)

    def test_normal_oc_left(self):      super(ops_simple, self).normal(self.collection_oldstyle, self.collection)
    def test_normal_oc_right(self):     super(ops_simple, self).normal(self.collection, self.collection_oldstyle)

    def test_normal_nc_left(self):      super(ops_simple, self).normal(self.collection_newstyle, self.collection)
    def test_normal_nc_right(self):     super(ops_simple, self).normal(self.collection, self.collection_newstyle)

    def test_reflect_oc_right(self):    super(ops_simple, self).normal(self.collection, self.collection_oldstyle_reflect)
    def test_reflect_nc_right(self):    super(ops_simple, self).normal(self.collection, self.collection_newstyle_reflect)

    def test_oc_notdefined(self):       super(ops_simple, self).normal(self.collection_oldstyle_notdefined, self.collection)
    def test_nc_notdefined(self):       super(ops_simple, self).normal(self.collection_newstyle_notdefined, self.collection)

    def test_oc_notdefined_oc_reflect(self):       super(ops_simple, self).normal(self.collection_oldstyle_notdefined, self.collection_oldstyle_reflect)
    def test_nc_notdefined_nc_reflect(self):       super(ops_simple, self).normal(self.collection_newstyle_notdefined, self.collection_newstyle_reflect)

    def test_inplace(self):             super(ops_simple, self).inplace(self.collection, self.collection)
    def test_inplace_ol(self):          super(ops_simple, self).inplace(self.collection_oldstyle_inplace, self.collection)
    def test_inplace_nl(self):          super(ops_simple, self).inplace(self.collection_newstyle_inplace, self.collection)

runtests(ops_simple)
