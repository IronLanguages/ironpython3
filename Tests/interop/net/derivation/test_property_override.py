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

import unittest

from iptest import IronPythonTestCase, is_mono, run_test, skipUnlessIronPython

@skipUnlessIronPython()
class PropertyOverrideTest(IronPythonTestCase):
    def setUp(self):
        super(PropertyOverrideTest, self).setUp()
        if is_mono:
            self.add_clr_assemblies("baseclasscs", "typesamples")
        else:
            self.add_clr_assemblies("baseclasscs", "baseclassvb", "typesamples")
        
    def test_read_write_interface(self):
        from Merlin.Testing.BaseClass import IProperty10
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
        #self.assertEqual(p.GetValue(x), 10)
        
        C.IntProperty = property(C.get_IntProperty, C.set_IntProperty)
        p.__set__(x, 20)
        self.assertEqual(p.GetValue(x), 20)
        
        self.assertTrue(not hasattr(IProperty10, 'set_IntProperty'))
        self.assertTrue(not hasattr(IProperty10, 'get_IntProperty'))
        
        # negative
        self.assertRaises(TypeError, lambda: p.SetValue(x, 'str'))
        del C.IntProperty   # workaround: bug 327528
        C.IntProperty = property(C.get_IntProperty, C.bad_set)  
        self.assertRaises(TypeError, lambda: p.__set__(x, 1))
        self.assertRaises(TypeError, lambda: p.__set__(x, 1, 2))
        
        class C(IProperty10):
            def __init__(self):
                self.field = 30
                
            @property
            def IntProperty(self): 
                return self.field
        
        x = C()
        self.assertEqual(p.GetValue(x), 30)
        self.assertRaisesMessage(AttributeError, "readonly attribute", lambda: p.__set__(x, 40))

    def test_readonly_interface(self):
        from Merlin.Testing.BaseClass import IProperty11
        class C(IProperty11):
            def set_StrProperty(self, value):
                self.field = value
            def get_StrProperty(self):
                return self.field
            StrProperty = property(get_StrProperty, set_StrProperty)
            
        x = C()
        p = IProperty11.StrProperty
        p.__set__(x, 'abc')     # no-op equivalent?
        self.assertRaises(SystemError, lambda: p.SetValue(x, 'def'))  # ?
        self.assertRaises(AttributeError, lambda: x.field)  # make sure x.field not set yet
        
        x.field = 'python'
        self.assertEqual(p.GetValue(x), 'python')
        self.assertEqual(p.__get__(x), 'python')
    
    def test_writeonly_interface(self):
        from Merlin.Testing.BaseClass import IProperty12
        class C(IProperty12):
            def set_DoubleProperty(self, value):
                self.field = value
            def get_DoubleProperty(self):
                return self.field
            DoubleProperty = property(get_DoubleProperty, set_DoubleProperty)
        
        x = C()
        p = IProperty12.DoubleProperty
        
        p.__set__(x, 1.23)
        self.assertEqual(x.field, 1.23)
        
        for l in [ p.GetValue, p.__get__]:
            self.assertRaisesMessage(AttributeError, "unreadable property", l, x)

    def test_csindexer(self):
        from Merlin.Testing.BaseClass import Callback, IIndexer20
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
        #self.assertEqual(IIndexer20.get_Item(x, 2), 'Two')

        x.dict = {1 : 'One', 2 : 'Two'}
        Callback.On1(x)
        self.assertEqual(x.dict[1], 'one')
        self.assertEqual(x.dict[2], 'TWO')   
        
        x[3] = 'Three'
        self.assertEqual('TWO', x[2])
        
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
        #self.assertEqual(x.field, "+1 2 start+3 4inside clr")
        
        x = C()
        x[1, 2] = x[3, 4] + "something"
        self.assertEqual(x.field, "-1 2 start-3 4something")

    @unittest.skipIf(is_mono, 'VB compile currently failing https://github.com/IronLanguages/main/issues/1438')
    def test_vbindexer(self):
        from Merlin.Testing.BaseClass import CVbIndexer30, IVbIndexer10, IVbIndexer11, IVbIndexer20, VbCallback
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
        self.assertEqual(x.f, 1112)
        
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
    
    @unittest.skipIf(is_mono, "mono doesn't handle this properly, needs debug https://github.com/IronLanguages/main/issues/1593")
    def test_virtual_property(self):
        from Merlin.Testing.BaseClass import Callback, CProperty30
        class C(CProperty30):
            pass

        x = C()
        Callback.On(x)
        self.assertEqual(x.Property, 220)
        
        self.assertTrue(not hasattr(CProperty30, 'set_Property'))
        self.assertTrue(not hasattr(CProperty30, 'get_Property'))
        
        class C(CProperty30):
            def __init__(self):
                self.field = 3
            def get_Property(self):
                return self.field;
            def set_Property(self, value):
                self.field = value + 30
        x = C()
        Callback.On(x)
        self.assertEqual(x.field, 233)  # we read field, we added 200 from C#, and added 30 ourself
        self.assertEqual(x.Property, 233)   
        
        x.field = 3
        C.Property = property(C.get_Property, C.set_Property)
        Callback.On(x)
        self.assertEqual(x.field, 233)
        self.assertEqual(x.Property, 233)
        
        self.assertTrue(not hasattr(CProperty30, 'set_Property'))
        self.assertTrue(not hasattr(CProperty30, 'get_Property'))
        
        del C.Property  # workaround: remove after bug 327528
        C.Property = property(C.get_Property)
        self.assertRaisesMessage(AttributeError, 
                "readonly attribute", 
                Callback.On, x)
    
    def test_abstract_property(self):
        from Merlin.Testing.BaseClass import Callback, CProperty31
        class C(CProperty31): 
            pass
        x = C()
        self.assertRaises(AttributeError, Callback.On, x)
        
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
        self.assertEqual(x.field, 111)
        
        x = C()
        self.assertTrue(not hasattr(CProperty31, 'get_Property'))
        self.assertTrue(not hasattr(CProperty31, 'set_Property'))
        self.assertTrue(not hasattr(x, 'get_Property'))
        self.assertTrue(not hasattr(x, 'set_Property'))

    def test_final_property(self):
        from Merlin.Testing.BaseClass import Callback, CProperty32
        class C(CProperty32): 
            pass
            
        x = C()
        Callback.On(x)  # 0 - 4 + 400 + 40
        pv = x.Property  # -4
        self.assertEqual(432, pv)  
        
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
        #self.assertEqual(x.Property, 5)  # bug 372831
        x.Property = 6
        #self.assertEqual(x.Property, 56)
    
    def test_static_property(self):
        from Merlin.Testing.BaseClass import CProperty33
        class C(CProperty33): 
            pass

        x = C()
                
        CProperty33.Property = 6
        self.assertEqual(CProperty33.Property, 66)
        self.assertEqual(x.Property, 66)
        self.assertEqual(C.Property, 66)
        
        ## test order matters here: x -> C
        
        #x.Property = 7  # bug 372840
        #self.assertEqual(x.Property, 7)  
        #self.assertEqual(CProperty33.Property, 66)

        C.Property = 8
        self.assertEqual(C.Property, 8)
        self.assertEqual(CProperty33.Property, 66)

    def test_readonly_writeonly_indexer(self):
        from Merlin.Testing.BaseClass import IIndexer21, IIndexer22
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
        self.assertEqual(IIndexer21.__getitem__(x, 10), 11)
        self.assertRaises(AttributeError, lambda: IIndexer21.__setitem__)
        x[10] = 100
        self.assertEqual(x.f, 110)
        self.assertEqual(x[1000], 1110)

        x = WO()
        IIndexer22.__setitem__(x, 10, 100)
        self.assertEqual(x.f, 110)
        #self.assertRaises(AttributeError, lambda: IIndexer22.__getitem__)  # ??
        #IIndexer22.__getitem__(x, 1000)  # otherwise
        
    def test_super_on_property(self):
        from Merlin.Testing.BaseClass import Callback, CProperty30
        class C(CProperty30):
            def get_Property(self): 
                return super(C, self).Property
            def set_Property(self, value):
                CProperty30.Property.SetValue(self, value + 500)
            Property = property(get_Property, set_Property)
        
        x = C()
        Callback.On(x)   # read (base_read)/+200/+500/write (base_write/+20)
        self.assertEqual(x.Property, 720)
        
        x = C()
        x.Property = 1
        self.assertEqual(x.Property, 521)
    
        ## bad user code attempt: use 'Property' directly
        class C(CProperty30): 
            def get_Property(self): 
                return super(C, self).Property
            def set_Property(self, value):
                super(C, self).Property = value
            Property = property(get_Property, set_Property)
        x = C()
        self.assertRaises(AttributeError, Callback.On, x)
        self.assertEqual(x.Property, 0)      # read
        
        def f(): x.Property = 1  # write
        self.assertRaises(AttributeError, f)   # cannot set slot
        #similar scenaio in CPython: TypeError: 'super' object has only read-only attributes (assign to .Property)

    def test_super_on_default_index(self):
        from Merlin.Testing.BaseClass import Callback, CIndexer40
        class C(CIndexer40):
            def __setitem__(self, index, value):
                super(C, self).__setitem__(index, value)
            def __getitem__(self, index):
                return super(C, self).__getitem__(index)
        
        x = C()
        Callback.On(x)
        self.assertEqual(x[0], 12)
        x[1] = 90
        self.assertEqual(x[1], 90)
    

run_test(__name__)