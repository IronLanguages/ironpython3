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
    
# comparison among python numbers 

class oldstyle:
    def __init__(self, value):
        self.value = value
    def __cmp__(self, other):
        return cmp(self.value, other)
    def __repr__(self):
        return "oldstyle(%s)" % str(self.value)

class newstyle(object):         
    def __init__(self, value):
        self.value = value
    def __cmp__(self, other):
        return cmp(self.value, other)
    def __repr__(self):
        return "newstyle(%s)" % str(self.value)

import sys
                        
collection = testdata.merge_lists(
                                [None], 
                                testdata.list_int,
                                testdata.list_float,
                                testdata.list_long,
                                testdata.list_bool,
                                testdata.list_myint,
                                testdata.list_myfloat,
                                testdata.list_mylong,
                            )
                            
collection_oldstyle = [oldstyle(x) for x in collection]
collection_newstyle = [newstyle(x) for x in collection]


class common(object):
    def true_compare(self, leftc, rightc, oplist = ["<", ">", ">=", "<=", "==", "<>" ]):
        for a in leftc:
            for b in rightc:
                if '<' in oplist:  
                    try:
                        printwith("case", a, '<',  b, type(a), type(b))
                        printwith("same", a < b)
                    except: 
                        printwith("except", sys.exc_info()[0])
                if '>' in oplist:  
                    try:
                        printwith("case", a, '>',  b, type(a), type(b))
                        printwith("same", a > b)
                    except: 
                        printwith("except", sys.exc_info()[0])
                if '>=' in oplist:  
                    try:
                        printwith("case", a, '>=',  b, type(a), type(b))
                        printwith("same", a >= b)
                    except: 
                        printwith("except", sys.exc_info()[0])
                if '<=' in oplist:  
                    try:
                        printwith("case", a, '<=',  b, type(a), type(b))
                        printwith("same", a <= b)
                    except: 
                        printwith("except", sys.exc_info()[0])
                if '==' in oplist:  
                    try:
                        printwith("case", a, '==',  b, type(a), type(b))
                        printwith("same", a == b)
                    except: 
                        printwith("except", sys.exc_info()[0])
                if '<>' in oplist:  
                    try:
                        printwith("case", a, '<>',  b, type(a), type(b))
                        printwith("same", a != b)
                    except: 
                        printwith("except", sys.exc_info()[0])
                try: 
                    printwith("case", "cmp(", a, ",", b, ")", type(a), type(b))
                    printwith("same", cmp(a, b))
                except: 
                    printwith("except", sys.exc_info()[0])
                    
    def compare_asbool(self, leftc, rightc, oplist = ["<", ">", ">=", "<=", "==", "<>" ]):
        for a in leftc:
            for b in rightc:
                if '<' in oplist:
                    try:
                        printwith("case", "RetBool", a, "<",  b, type(a), type(b))
                        if a < b: print('same##', True) 
                        else: print('same##', False)
                    except: 
                        printwith("except", sys.exc_info()[0])
                if '>' in oplist:
                    try:
                        printwith("case", "RetBool", a, ">",  b, type(a), type(b))
                        if a > b: print('same##', True) 
                        else: print('same##', False)
                    except: 
                        printwith("except", sys.exc_info()[0])
                if '>=' in oplist:
                    try:
                        printwith("case", "RetBool", a, ">=",  b, type(a), type(b))
                        if a >= b: print('same##', True) 
                        else: print('same##', False)
                    except: 
                        printwith("except", sys.exc_info()[0])
                if '<=' in oplist:
                    try:
                        printwith("case", "RetBool", a, "<=",  b, type(a), type(b))
                        if a <= b: print('same##', True) 
                        else: print('same##', False)
                    except: 
                        printwith("except", sys.exc_info()[0])
                if '==' in oplist:
                    try:
                        printwith("case", "RetBool", a, "==",  b, type(a), type(b))
                        if a == b: print('same##', True) 
                        else: print('same##', False)
                    except: 
                        printwith("except", sys.exc_info()[0])
                if '<>' in oplist:
                    try:
                        printwith("case", "RetBool", a, "<>",  b, type(a), type(b))
                        if a != b: print('same##', True) 
                        else: print('same##', False)
                    except: 
                        printwith("except", sys.exc_info()[0])
                        
class test_simple(common): 
    def __init__(self):
        self.collection = collection
        self.collection_oldstyle = collection_oldstyle
        self.collection_newstyle = collection_newstyle
                            
    def test_true_compare(self):                super(test_simple, self).true_compare(self.collection, self.collection)
    def test_compare_asbool(self):              super(test_simple, self).compare_asbool(self.collection, self.collection)

    def test_true_compare_oldc_left(self):      super(test_simple, self).true_compare(self.collection_oldstyle, self.collection)
    def test_compare_asbool_oldc_left(self):    super(test_simple, self).compare_asbool(self.collection_oldstyle, self.collection)
    def test_true_compare_oldc_right(self):     super(test_simple, self).true_compare(self.collection, self.collection_oldstyle)
    def test_compare_asbool_oldc_right(self):   super(test_simple, self).compare_asbool(self.collection, self.collection_oldstyle)

    def test_true_compare_newc_left(self):      super(test_simple, self).true_compare(self.collection_newstyle, self.collection)
    def test_compare_asbool_newc_left(self):    super(test_simple, self).compare_asbool(self.collection_newstyle, self.collection)
    def test_true_compare_newc_right(self):     super(test_simple, self).true_compare(self.collection, self.collection_newstyle)
    def test_compare_asbool_newc_right(self):   super(test_simple, self).compare_asbool(self.collection, self.collection_newstyle)
        
class test_enum(test_simple):
    def __init__(self):
        self.collection = testdata.merge_lists(testdata.get_enums(), testdata.list_bool, testdata.list_int)
        self.collection_oldstyle = [oldstyle(x) for x in self.collection]
        self.collection_newstyle = [newstyle(x) for x in self.collection]
        
class test_onetype(common):
    def test_true_compare_pos(self):
        super(test_onetype, self).true_compare(self.data, self.data)
        super(test_onetype, self).true_compare(self.data, [newstyle(x) for x in self.data])
        super(test_onetype, self).true_compare([newstyle(x) for x in self.data], self.data)
        super(test_onetype, self).true_compare(self.data, [oldstyle(x) for x in self.data])
        super(test_onetype, self).true_compare([oldstyle(x) for x in self.data], self.data)
    def test_compare_asbool_pos(self):
        super(test_onetype, self).compare_asbool(self.data, self.data)
        super(test_onetype, self).compare_asbool(self.data, [newstyle(x) for x in self.data])
        super(test_onetype, self).compare_asbool([newstyle(x) for x in self.data], self.data)
        super(test_onetype, self).compare_asbool(self.data, [oldstyle(x) for x in self.data])
        super(test_onetype, self).compare_asbool([oldstyle(x) for x in self.data], self.data)
    def test_true_compare_neg(self):
        # comparing to None is still valid
        super(test_onetype, self).true_compare(self.data, collection)
        super(test_onetype, self).true_compare(collection, self.data)
        super(test_onetype, self).true_compare(self.data, collection_newstyle)
        super(test_onetype, self).true_compare(self.data, collection_oldstyle)
        super(test_onetype, self).true_compare(collection_newstyle, self.data)
        super(test_onetype, self).true_compare(collection_oldstyle, self.data)
        
    def test_compare_asbool_neg(self):
        super(test_onetype, self).compare_asbool(self.data, collection)
        super(test_onetype, self).compare_asbool(collection, self.data)
        super(test_onetype, self).compare_asbool(self.data, collection_newstyle)
        super(test_onetype, self).compare_asbool(self.data, collection_oldstyle)
        super(test_onetype, self).compare_asbool(collection_newstyle, self.data)
        super(test_onetype, self).compare_asbool(collection_oldstyle, self.data)

class test_string(test_onetype):
    def __init__(self):
        self.data = testdata.merge_lists(testdata.list_str, testdata.list_mystr)
        
class test_complex(test_onetype):
    def __init__(self):
        self.data = testdata.merge_lists(testdata.list_complex, testdata.list_mycomplex)

class test_dict(test_onetype):
    def __init__(self):
        self.data = testdata.merge_lists(testdata.list_dict)

    def test_true_compare_neg(self):
        '''
        Override this as its busted.
        Please remove the function entirely once Dev10 438851 gets fixed.
        '''
        if sys.platform == "cli" and not ({} < 2):
            printwith("Dev10 438851 has been fixed.  Please remove this.")
            

    def test_compare_asbool_neg(self):
        '''
        Override this as its busted.
        Please remove the function entirely once Dev10 438851 gets fixed.
        '''
        if sys.platform == "cli" and not ({} < 2):
            printwith("Dev10 438851 has been fixed.  Please remove this.")


runtests(test_simple)

#guess this is not going to happen
#runtests(test_enum)

runtests(test_string)
runtests(test_complex)
runtests(test_dict)

# redefine oldstyle/newstyle, since cmp does not work on set by spec
class oldstyle:
    def __init__(self, value):          self.value = value
    def __lt__(self, other):            return self.value < other
    def __gt__(self, other):            return self.value > other        
    def __le__(self, other):            return self.value <= other
    def __ge__(self, other):            return self.value >= other        
    def __eq__(self, other):            return self.value == other
    def __ne__(self, other):            return self.value != other        
    def __repr__(self):                 return "oldstyle(%s)" % str(self.value)

class newstyle(object):         
    def __init__(self, value):          self.value = value
    def __lt__(self, other):            return self.value < other
    def __gt__(self, other):            return self.value > other        
    def __le__(self, other):            return self.value <= other
    def __ge__(self, other):            return self.value >= other        
    def __eq__(self, other):            return self.value == other
    def __ne__(self, other):            return self.value != other        
    def __repr__(self):                 return "newstyle(%s)" % str(self.value)

class test_set(test_onetype):
    def __init__(self):     
        self.data = testdata.merge_lists(
            [None],
            testdata.list_set, 
            testdata.list_frozenset,
            )

#runtests(test_set)

