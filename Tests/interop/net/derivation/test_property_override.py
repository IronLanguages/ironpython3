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
'''
How to re-define a property in Python.
'''
#------------------------------------------------------------------------------

from iptest.assert_util import *
skiptest("silverlight")

add_clr_assemblies("baseclasscs", "baseclassvb", "typesamples")

from Merlin.Testing import *
from Merlin.Testing.BaseClass import *


def test_read_write_interface(): 
    class C(IProperty10):   
        def set_IntProperty(self, value):
            self.field = value
        def get_IntProperty(self):
            return self.field
        def bad_set(self, arg1, arg2): pass

    x = C()
    p = IProperty10.IntProperty
    
    # exception message: bug 372518
    #p.__set__(x, 10)
    #AreEqual(p.GetValue(x), 10)
    
    C.IntProperty = property(C.get_IntProperty, C.set_IntProperty)
    p.__set__(x, 20)
    AreEqual(p.GetValue(x), 20)
    
    Assert(not hasattr(IProperty10, 'set_IntProperty'))
    Assert(not hasattr(IProperty10, 'get_IntProperty'))
    
    # negative
    AssertError(TypeError, lambda: p.SetValue(x, 'str'))
    del C.IntProperty   # workaround: bug 327528
    C.IntProperty = property(C.get_IntProperty, C.bad_set)  
    AssertError(TypeError, lambda: p.__set__(x, 1))
    AssertError(TypeError, lambda: p.__set__(x, 1, 2))
    
    class C(IProperty10):
        def __init__(self):
            self.field = 30
            
        @property
        def IntProperty(self): 
            return self.field
    
    x = C()
    AreEqual(p.GetValue(x), 30)
    AssertErrorWithMessage(AttributeError, "readonly attribute", lambda: p.__set__(x, 40))
    
def test_readonly_interface():
    class C(IProperty11):
        def set_StrProperty(self, value):
            self.field = value
        def get_StrProperty(self):
            return self.field
        StrProperty = property(get_StrProperty, set_StrProperty)
        
    x = C()
    p = IProperty11.StrProperty
    p.__set__(x, 'abc')     # no-op equivalent?
    AssertError(SystemError, lambda: p.SetValue(x, 'def'))  # ?
    AssertError(AttributeError, lambda: x.field)  # make sure x.field not set yet
    
    x.field = 'python'
    AreEqual(p.GetValue(x), 'python')
    AreEqual(p.__get__(x), 'python')
    
def test_writeonly_interface():
    class C(IProperty12):
        def set_DoubleProperty(self, value):
            self.field = value
        def get_DoubleProperty(self):
            return self.field
        DoubleProperty = property(get_DoubleProperty, set_DoubleProperty)
    
    x = C()
    p = IProperty12.DoubleProperty
    
    p.__set__(x, 1.23)
    AreEqual(x.field, 1.23)
    
    for l in [ p.GetValue, p.__get__]:
        AssertErrorWithMessage(AttributeError, "unreadable property", l, x)

def test_csindexer():
    class C(IIndexer20):
        def __init__(self):
            self.dict = {}
        def __setitem__(self, index, value):
            self.dict[index] = value
        def __getitem__(self, index):
            return self.dict[index]
    
    x = C()
    #IIndexer20.set_Item(x, 1, 'One') # bug 363289
    #IIndexer20.set_Item(x, 2, 'Two') 
    #AreEqual(IIndexer20.get_Item(x, 2), 'Two')

    x.dict = {1 : 'One', 2 : 'Two'}
    Callback.On1(x)
    AreEqual(x.dict[1], 'one')
    AreEqual(x.dict[2], 'TWO')   
    
    x[3] = 'Three'
    AreEqual('TWO', x[2])
    
    class C(IIndexer20):
        def __init__(self):
            self.field = "start"
        def __setitem__(self, index, value):
            self.field = "-%s %s %s" % (index[0], index[1], value)
        def __getitem__(self, index):
            return self.field + "-%s %s" % (index[0], index[1])
        
        # experimental
        def set_Item(self, index1, index2, value):
            self.field = "+%s %s %s" % (index1, index2, value)
        def get_Item(self, index1, index2):
            return self.field + "+%s %s" % (index1, index2)
    
    x = C()
    #Callback.On2(x)   # bug 372940
    #AreEqual(x.field, "+1 2 start+3 4inside clr")
    
    x = C()
    x[1, 2] = x[3, 4] + "something"
    AreEqual(x.field, "-1 2 start-3 4something")

def test_vbindexer():
    class C(IVbIndexer10): 
        def __init__(self):
            self.f = 1
        
        '''    
        def set_IntProperty(self, index, value):
            self.f = self.f + index + value
        def get_IntProperty(self, index):
            return self.f
        '''
        
        def __setitem__(self, index, value):
            self.f = self.f + index + value
        def __getitem__(self, index):
            return self.f + index
        
    x = C()
    VbCallback.Act(x)  
    AreEqual(x.f, 1112)
    
    # TODO: I doubt it works for now
    class C(IVbIndexer11):
        pass
        
    class C(IVbIndexer20):
        def __init__(self):
            self.f = 0
        def set_DoubleProperty(self, index, value):
            self.f = index * 0.1 + value 
        def get_DoubleProperty(self, index):
            return self.f + index * 0.01

    x = C()
    # VbCallback.Act(x) 
    # currently AttributeError: 'C' object has no attribute 'get_DoubleProperty'
    
    # TODO
    class C(CVbIndexer30):
        pass
    
def test_virtual_property():
    class C(CProperty30):
        pass

    x = C()
    Callback.On(x)
    AreEqual(x.Property, 220)
    
    Assert(not hasattr(CProperty30, 'set_Property'))
    Assert(not hasattr(CProperty30, 'get_Property'))
    
    class C(CProperty30):
        def __init__(self):
            self.field = 3
        def get_Property(self):
            return self.field;
        def set_Property(self, value):
            self.field = value + 30
    x = C()
    Callback.On(x)
    AreEqual(x.field, 233)  # we read field, we added 200 from C#, and added 30 ourself
    AreEqual(x.Property, 233)   
    
    x.field = 3
    C.Property = property(C.get_Property, C.set_Property)
    Callback.On(x)
    AreEqual(x.field, 233)
    AreEqual(x.Property, 233)
    
    Assert(not hasattr(CProperty30, 'set_Property'))
    Assert(not hasattr(CProperty30, 'get_Property'))
    
    del C.Property  # workaround: remove after bug 327528
    C.Property = property(C.get_Property)
    AssertErrorWithMessage(AttributeError, 
            "readonly attribute", 
            Callback.On, x)
    
def test_abstract_property():
    class C(CProperty31): 
        pass
    x = C()
    AssertError(AttributeError, Callback.On, x)
    
    class C(CProperty31):
        def __init__(self):
            self.field = 1
        def get_PropertyX(self): 
            return self.field;
        def set_PropertyX(self, value):
            self.field = value + 10
        Property = property(get_PropertyX, set_PropertyX)
    
    x = C()
    Callback.On(x)
    AreEqual(x.field, 111)
    
    x = C()
    Assert(not hasattr(CProperty31, 'get_Property'))
    Assert(not hasattr(CProperty31, 'set_Property'))
    Assert(not hasattr(x, 'get_Property'))
    Assert(not hasattr(x, 'set_Property'))

def test_final_property():
    class C(CProperty32): 
        pass
        
    x = C()
    Callback.On(x)  # 0 - 4 + 400 + 40
    pv = x.Property  # -4
    AreEqual(432, pv)  
    
    class C(CProperty32):
        def __init__(self):
            self.field = 5
        def get_Property(self): 
            return self.field;
        def set_Property(self, value):
            self.field = value + 50
        Property = property(get_Property, set_Property)
        
    x = C()
    Callback.On(x)
    #AreEqual(x.Property, 5)  # bug 372831
    x.Property = 6
    #AreEqual(x.Property, 56)
    
def test_static_property():
    class C(CProperty33): 
        pass

    x = C()
            
    CProperty33.Property = 6
    AreEqual(CProperty33.Property, 66)
    AreEqual(x.Property, 66)
    AreEqual(C.Property, 66)
    
    ## test order matters here: x -> C
    
    #x.Property = 7  # bug 372840
    #AreEqual(x.Property, 7)  
    #AreEqual(CProperty33.Property, 66)

    C.Property = 8
    AreEqual(C.Property, 8)
    AreEqual(CProperty33.Property, 66)

def test_readonly_writeonly_indexer():
    def create_type(base):    
        class C(base): 
            def __init__(self): 
                self.f = 1
            def __setitem__(self, index, value):
                self.f = index + value 
            def __getitem__(self, index):
                return self.f + index
        return C
    
    RO, WO = map(create_type, [IIndexer21, IIndexer22])
    
    x = RO()
    AreEqual(IIndexer21.__getitem__(x, 10), 11)
    AssertError(AttributeError, lambda: IIndexer21.__setitem__)
    x[10] = 100
    AreEqual(x.f, 110)
    AreEqual(x[1000], 1110)

    x = WO()
    IIndexer22.__setitem__(x, 10, 100)
    AreEqual(x.f, 110)
    #AssertError(AttributeError, lambda: IIndexer22.__getitem__)  # ??
    #IIndexer22.__getitem__(x, 1000)  # otherwise
        
def test_super_on_property():
    class C(CProperty30):
        def get_Property(self): 
            return super(C, self).Property
        def set_Property(self, value):
            CProperty30.Property.SetValue(self, value + 500)
        Property = property(get_Property, set_Property)
    
    x = C()
    Callback.On(x)   # read (base_read)/+200/+500/write (base_write/+20)
    AreEqual(x.Property, 720)
    
    x = C()
    x.Property = 1
    AreEqual(x.Property, 521)
   
    ## bad user code attempt: use 'Property' directly
    class C(CProperty30): 
        def get_Property(self): 
            return super(C, self).Property
        def set_Property(self, value):
            super(C, self).Property = value
        Property = property(get_Property, set_Property)
    x = C()
    AssertError(AttributeError, Callback.On, x)
    AreEqual(x.Property, 0)      # read
    
    def f(): x.Property = 1  # write
    AssertError(AttributeError, f)   # cannot set slot
    #similar scenaio in CPython: TypeError: 'super' object has only read-only attributes (assign to .Property)

def test_super_on_default_index():
    class C(CIndexer40):
        def __setitem__(self, index, value):
            super(C, self).__setitem__(index, value)
        def __getitem__(self, index):
            return super(C, self).__getitem__(index)
    
    x = C()
    Callback.On(x)
    AreEqual(x[0], 12)
    x[1] = 90
    AreEqual(x[1], 90)
    
def test_super_on_non_default_index():
    # TODO
    pass
    

run_test(__name__)
