# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

from common import *
import testdata

def get_classes(bonly = False):
    class oldstyle1:
        def __init__(self, value):
            self.value = value
        def __repr__(self):
            return "oldstyle1(%s)" % str(self.value)
    class oldstyle2:
        def __init__(self, value):
            self.value = value
        def __repr__(self):
            return "oldstyle2(%s)" % str(self.value)

    class usertype1(object):
        def __init__(self, value):
            self.value = value
        def __repr__(self):
            return "usertype1(%s)" % str(self.value)

    class usertype2(object):
        def __init__(self, value):
            self.value = value
        def __repr__(self):
            return "usertype2(%s)" % str(self.value)

    if bonly: 
        return (oldstyle1, oldstyle2, usertype1, usertype2)
    
    class oldstyled1(oldstyle1): pass
    class oldstyled2(oldstyle2): pass
    class usertyped1(usertype1): pass
    class usertyped2(usertype2): pass
    
    return (oldstyle1, oldstyle2, usertype1, usertype2, oldstyled1, oldstyled2, usertyped1, usertyped2)

def mylt(a, b):  return a.value < b.value
def mygt(a, b):  return a.value > b.value
def myle(a, b):  return a.value <= b.value
def myge(a, b):  return a.value >= b.value
def myeq(a, b):  return a.value == b.value
def myne(a, b):  return a.value != b.value
def mycmp(a, b): return cmp(a.value, b.value)
    
def get_instances(types):
    #basic_numbers = testdata.get_comparable_numbers_as_list()
    #basic_numbers = (None, 0, 1, 0.0, 34, -34.2, 1L, 123456789012345, False, True, testdata.myint(34), testdata.mylong(1), testdata.myfloat(-23), 2+3j)
    basic_numbers = (1, 2, 34, -34.2, 1,testdata.myint(34),)
    
    collection = []
    for x in basic_numbers: 
        for t in types:
            collection.append(t(x))

    return collection

class common(object):
    def compare(self, collection, oplist, docmp = True, preCmpCheck = None):
        self.compare2(collection, collection, oplist, docmp, preCmpCheck)
        
    def compare2(self, collection1, collection2, oplist, docmp = True, preCmpCheck = None):
        for a in collection1:
            for b in collection2:                
                if '<' in oplist:
                    if not (preCmpCheck != None and preCmpCheck(a, b, '<')):
                        try:
                            printwith("case", a.value, '<',  b.value, a, b)
                            classval = a < b
                            simplval = a.value < b.value
                            printwith("same", classval, simplval)
                            #if classval <> simplval: raise MyException
                        except Exception: 
                            printwith("except_unexpected")
                if '>' in oplist:
                    if not (preCmpCheck != None and preCmpCheck(a, b, '>')):
                        try:
                            printwith("case", a.value, '>',  b.value, a, b)
                            classval = a > b
                            simplval = a.value > b.value
                            printwith("same", classval, simplval)
                            #if classval <> simplval: raise MyException
                        except Exception: 
                            printwith("except_unexpected")
                if '>=' in oplist:
                    if not (preCmpCheck != None and preCmpCheck(a, b, '>=')): 
                        try:
                            printwith("case", a.value, '>=',  b.value, a, b)
                            classval = a >= b
                            simplval = a.value >= b.value
                            printwith("same", classval, simplval)
                            #if classval <> simplval: raise MyException
                        except Exception: 
                            printwith("except_unexpected")
                if '<=' in oplist:
                    if not (preCmpCheck != None and preCmpCheck(a, b, '<=')) : 
                        try:
                            printwith("case", a.value, '<=',  b.value, a, b)
                            classval = a <= b
                            simplval = a.value <= b.value
                            printwith("same", classval, simplval)
                            #if classval <> simplval: raise MyException
                        except Exception: 
                            printwith("except_unexpected")
                if '==' in oplist:
                    if not (preCmpCheck != None and preCmpCheck(a, b, '==')): 
                        try:
                            printwith("case", a.value, '==',  b.value, a, b)
                            classval = a == b
                            simplval = a.value == b.value
                            printwith("same", classval, simplval)
                            #if classval <> simplval: raise MyException
                        except Exception: 
                            printwith("except_unexpected")
                if '<>' in oplist:
                    if not (preCmpCheck != None and preCmpCheck(a, b, '<>')):
                        try:
                            printwith("case", a.value, '<>',  b.value, a, b)
                            classval = a != b
                            simplval = a.value != b.value
                            printwith("same", classval, simplval)
                            #if classval <> simplval: raise MyException
                        except Exception: 
                            printwith("except_unexpected")

                if docmp == False: continue
                if preCmpCheck != None and preCmpCheck(a, b, 'cmp'): continue
                    
                try: 
                    printwith("case", "cmp(", a.value, ",", b.value, ")", a, b)
                    classval = cmp(a, b)
                    simplval = cmp(a.value, b.value)
                    printwith("same", classval, simplval)
                    #if classval <> simplval: raise MyException
                except Exception: 
                    printwith("except_unexpected")
     
        # as condition
        for a in collection1:
            for b in collection2:
                if '<' in oplist:
                    try:
                        printwith("case", "condition", a.value, "<",  b.value, a, b)
                        if a < b: print('same##', True) 
                        else: print('same##', False)
                    except: 
                        printwith("except_unexpected")
                if '>' in oplist:
                    try:
                        printwith("case", "condition", a.value, ">",  b.value, a, b)
                        if a > b: print('same##', True) 
                        else: print('same##', False)
                    except: 
                        printwith("except_unexpected")
                if '>=' in oplist:
                    try:
                        printwith("case", "condition", a.value, ">=",  b.value, a, b)
                        if a >= b: print('same##', True) 
                        else: print('same##', False)
                    except: 
                        printwith("except_unexpected")
                if '<=' in oplist:
                    try:
                        printwith("case", "condition", a.value, "<=",  b.value, a, b)
                        if a <= b: print('same##', True) 
                        else: print('same##', False)
                    except: 
                        printwith("except_unexpected")
                if '==' in oplist:
                    try:
                        printwith("case", "condition", a.value, "==",  b.value, a, b)
                        if a == b: print('same##', True) 
                        else: print('same##', False)
                    except: 
                        printwith("except_unexpected")
                if '<>' in oplist:
                    try:
                        printwith("case", "condition", a.value, "<>",  b.value, a, b)
                        if a != b: print('same##', True) 
                        else: print('same##', False)
                    except: 
                        printwith("except_unexpected")

class test_classcmp(common): 
    # all define consistent __lt__
    def test_lt(self):
        types = get_classes()
        for t in types: t.__lt__ = mylt
            
        collection = get_instances(types)
        print(collection)
        super(test_classcmp, self).compare(collection, ["<", ">"], False)
        
    # all define consistent __le__
    def test_le(self):
        types = get_classes()
        for t in types: t.__le__ = myle
        
        collection = get_instances(types)
        super(test_classcmp, self).compare(collection, [">=", "<=",], False)
        
    # all define consistent __gt__
    def test_gt(self):
        types = get_classes()
        for t in types: t.__gt__ = mygt
        
        collection = get_instances(types)
        super(test_classcmp, self).compare(collection, ["<", ">"], False)

    # all define consistent __ge__
    def test_ge(self):
        types = get_classes()
        for t in types: t.__ge__ = myge
        
        collection = get_instances(types)
        super(test_classcmp, self).compare(collection, [">=", "<=",], False)
             
    # all define consistent __lt__/__le__/__eq__
    def test_lt_le(self):
        types = get_classes()
        
        for t in types:
            t.__lt__ = mylt
            t.__le__ = myle
            t.__eq__ = myeq
            t.__ne__ = myne
            
            
        collection = get_instances(types)
        super(test_classcmp, self).compare(collection, ["<", ">", ">=", "<=", "==", "<>"])

    # opposite way 
    # LAST STEP: TO RENAME xtest_ to test_; and remove the MyException thing
    def test_opposite(self):
        types = get_classes()
        
        for t in types:
            t.__gt__ = mylt
            t.__ge__ = myle
            t.__eq__ = myne
            t.__ne__ = myeq
        
        def preCmpCheck(a,b, op):
            if type(a) == type(b) and op == "cmp": return True
            return False
        
        collection = get_instances(types)
        super(test_classcmp, self).compare(collection, ["<", ">", ">=", "<=", "==", "<>"], preCmpCheck=preCmpCheck)
       
    # define __cmp__ only
    def test_cmp_only(self):
        types = get_classes()
        
        for t in types:
            t.__cmp__ = mycmp
            
        collection = get_instances(types)
        super(test_classcmp, self).compare(collection, ["<", ">", ">=", "<=", "==", "<>"])
    
    # some define __ge__
    def test_partial_ge(self):
        types = get_classes()
        
        types1 = types[::2]
        types2 = types[1::2]
        
        for t in types1: t.__ge__ = myge
            
        collection1 = get_instances(types1)
        collection2 = get_instances(types2)
        super(test_classcmp, self).compare2(collection1, collection2, [">="], False)
        super(test_classcmp, self).compare2(collection2, collection1, ["<="], False)
    
    # some define __lt__
    def test_partial_lt(self):
        types = get_classes()
        
        types1 = types[::2]
        types2 = types[1::2]
        
        for t in types1: t.__lt__ = mylt
        collection1 = get_instances(types1)
        collection2 = get_instances(types2)
        
        super(test_classcmp, self).compare2(collection1, collection2, ["<"], False)
        super(test_classcmp, self).compare2(collection2, collection1, [">"], False)
        
runtests(test_classcmp)        
