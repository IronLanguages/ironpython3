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

# Disabled due to Silverlight buffer overrun bug
# Silverlight bug #403321 in TFS, DLR tracking bug 414787
# skiptest('silverlightbug?') - bug fixed

from iptest.type_util import *

import sys

if is_cli or is_silverlight:
    load_iron_python_test()
    import System
    from IronPythonTest import *


@skip("win32")
def test_cli_inheritance():
    class InheritedClass(BaseClass):
        def ReturnHeight(self):
            return self.Height
        def ReturnWidth(self):
            return self.Width
        def ReturnSize(self):
            return self.Size
        
    i = InheritedClass()
    
    Assert(i.Width == 0)
    Assert(i.Height == 0)
    Assert(i.Size.width == 0)
    Assert(i.Size.height == 0)
    Assert(i.ReturnHeight() == 0)
    Assert(i.ReturnWidth() == 0)
    Assert(i.ReturnSize().width == 0)
    Assert(i.ReturnSize().height == 0)
        
    i.Width = 1
    i.Height = 2
        
    Assert(i.Width == 1)
    Assert(i.Height == 2)
    Assert(i.Size.width == 1)
    Assert(i.Size.height == 2)
    Assert(i.ReturnHeight() == 2)
    Assert(i.ReturnWidth() == 1)
    Assert(i.ReturnSize().width == 1)
    Assert(i.ReturnSize().height == 2)
        
    s = MySize(3, 4)
        
    i.Size = s
        
    Assert(i.Width == 3)
    Assert(i.Height == 4)
    Assert(i.Size.width == 3)
    Assert(i.Size.height == 4)
    Assert(i.ReturnHeight() == 4)
    Assert(i.ReturnWidth() == 3)
    Assert(i.ReturnSize().width == 3)
    Assert(i.ReturnSize().height == 4)
    
@skip("win32")
def test_cli_new_inheritance():
    """verifies that we do the right thing with new slot's (e.g. public int new Foo { get; })"""
    
    class InheritedClass(NewClass):
        def ReturnHeight(self):
            return self.Height
        def ReturnWidth(self):
            return self.Width
        def ReturnSize(self):
            return self.Size
        
    for i in (InheritedClass(), NewClass()):
        AreEqual(i.Width, 1)
        AreEqual(i.Height, 1)
        AreEqual(i.Size.width,  1)
        AreEqual(i.Size.height,  1)
        if hasattr(i, 'ReturnHeight'):
            AreEqual(i.ReturnHeight(),  1)
            AreEqual(i.ReturnWidth(),  1)
            AreEqual(i.ReturnSize().width,  1)
            AreEqual(i.ReturnSize().height,  1)
            
        i.Width = 1
        i.Height = 2
            
        AreEqual(i.Width,  3)
        AreEqual(i.Height,  5)
        AreEqual(i.Size.width,  2)
        AreEqual(i.Size.height,  3)
        if hasattr(i, 'ReturnHeight'):
            AreEqual(i.ReturnHeight(),  5)
            AreEqual(i.ReturnWidth(),  3)
            AreEqual(i.ReturnSize().width,  2)
            AreEqual(i.ReturnSize().height,  3)
            
        s = MySize(3, 4)
            
        i.Size = s
            
        AreEqual(i.Width,  7)
        AreEqual(i.Height,  9)
        AreEqual(i.Size.width,  4)
        AreEqual(i.Size.height,  5)
        
        if not hasattr(i, 'ReturnHeight'):
            # BUGBUG: We need to enable this for the inherited case.  Currently ReflectedTypeBUilder
            # is replacing the derived size w/ the base size.
            AreEqual(i.size.width, 3)
            AreEqual(i.size.height, 4)
        
        if hasattr(i, 'ReturnHeight'):
            AreEqual(i.ReturnHeight(),  9)
            AreEqual(i.ReturnWidth(),  7)
            AreEqual(i.ReturnSize().width,  4)
            AreEqual(i.ReturnSize().height,  5)
    
@skip("win32")
def test_override_hierarchy():
    AreEqual(CliVirtualStuff().VirtualMethod(1), 10)
        
    # overridden in base, should get base value
    class D1(CliVirtualStuff):
        def VirtualMethod(self, x):
            return 23
    class D2(D1): pass
        
    AreEqual(D2().VirtualMethod(1), 23)
        
    # multiple new-style classes
    class D3(CliVirtualStuff):
        def VirtualMethod(self, x):
            return 5
                
    class D4(D1, D3): pass
        
    AreEqual(D4().VirtualMethod(1), 23)
        
    class D5(D3, D1): pass
    
    AreEqual(D5().VirtualMethod(1), 5)
        
    class D6(D1, D3):
        def VirtualMethod(self, x):
            return 3
                
    AreEqual(D6().VirtualMethod(1), 3)
        
    class D7(D1, D3):
        def VirtualMethod(self, x):
            return 4
                
    AreEqual(D7().VirtualMethod(1), 4)

    # old-style class mixed in
    class C1:
        def VirtualMethod(self, x):
            return 42
                
    class D8(C1, D2): pass
        
    # old-style comes first, it should take precedence
    AreEqual(D8().VirtualMethod(1), 42)
        
    class D9(D2, C1): pass
        
    # new-style comes first, it should take precedence
    AreEqual(D9().VirtualMethod(1), 23)
        
    class D10(C1, CliVirtualStuff): pass
        
    AreEqual(D10().VirtualMethod(1), 42)

    class D11(CliVirtualStuff, C1): pass
        
    AreEqual(D11().VirtualMethod(1), 10)

@skip("win32", "silverlight") # remoting not supported in Silverlight
def test_mbr_inheritance():
    class InheritFromMarshalByRefObject(System.MarshalByRefObject):
        pass
    
@skip("win32")
def test_static_ctor_inheritance():
    class StaticConstructorInherit(BaseClassStaticConstructor):
        pass
    
    sci = StaticConstructorInherit()
        
    Assert(sci.Value == 10)

@skip("win32")
def test_cli_overriding():
    class PythonDerived(Overriding):
        def TemplateMethod(self):
            return "From Python"
        
        def BigTemplateMethod(self, *args):
            return ":".join(str(arg) for arg in args)
        
        def AbstractTemplateMethod(self):
            return "Overriden"
        
    o = PythonDerived()
    AreEqual(o.TopMethod(), "From Python - and Top")
        
    del PythonDerived.TemplateMethod
    AreEqual(o.TopMethod(), "From Base - and Top")
        
    def NewTemplateMethod(self):
        return "From Function"
        
    PythonDerived.TemplateMethod = NewTemplateMethod
    AreEqual(o.TopMethod(), "From Function - and Top")
        
    AreEqual(o.BigTopMethod(), "0:1:2:3:4:5:6:7:8:9 - and Top")
        
    del PythonDerived.BigTemplateMethod
    AreEqual(o.BigTopMethod(), "BaseBigTemplate - and Top")
        
        
    AreEqual(o.AbstractTopMethod(), "Overriden - and Top")
    del PythonDerived.AbstractTemplateMethod
        
    AssertError(AttributeError, o.AbstractTopMethod)
    
    class PythonDerived2(PythonDerived):
        def TemplateMethod(self):
            return "Python2"
        
    o = PythonDerived2()
    AreEqual(o.TopMethod(), "Python2 - and Top")
        
        
    del PythonDerived2.TemplateMethod
    ##!!! TODO
    ##AreEqual(o.TopMethod(), "From Function - and Top")
        
    del PythonDerived.TemplateMethod
    AreEqual(o.TopMethod(), "From Base - and Top")
        
        
    AreEqual(o.BigTopMethod(), "BaseBigTemplate - and Top")
#########################################################
@skip("win32")
def test_more_inheritance():
    class CTest(Inherited):
        def TopMethod(self):
            return "CTest"
        
    o = CTest()
    
#########################################################
@skip("win32")
def test_interface_inheritance():
    class C(ITestIt1, ITestIt2):
        def Method(self, x=None):
            if x: return "Python"+`x`
            else: return "Python"
        
    o = C()
    AreEqual(TestIt.DoIt1(o), "Python")
    AreEqual(TestIt.DoIt2(o), "Python42")
    
    Assert(isinstance(o, ITestIt2))
    Assert(issubclass(C, ITestIt2))
    Assert(ITestIt2 in C.__bases__)
        
    # inheritance from a single interface should be verifable
        
    class SingleInherit(ITestIt1): pass

@skip("win32")
def test_more_interface_inheritance():
    import System
    class Point(System.IFormattable):
        def __init__(self, x, y):
            self.x, self.y = x, y
        
        def ToString(self, format=None, fp=None):
            if format == 'x': return `(self.x, self.y)`
            return "Point(%r, %r)" % (self.x, self.y)
        
    p = Point(1,2)
    AreEqual(p.ToString(), "Point(1, 2)")
    AreEqual(p.ToString('x', None), "(1, 2)")
    #System.Console.WriteLine("{0}", p)

@skip("win32")
def test_interface_hierarchy():
    import System
    Assert(System.Collections.IEnumerable in System.Collections.IList.__bases__)
    AreEqual(System.Collections.IEnumerable.GetEnumerator, System.Collections.IList.GetEnumerator)
    Assert("GetEnumerator" in dir(System.Collections.IList))
    l = System.Collections.Generic.List[int]()
    l.Add(100)
    Assert(100 in System.Collections.IList.GetEnumerator(l))

def test_metaclass():
    class MetaClass(type):
        def __new__(metacls, name, bases, vars):
            cls = type.__new__(metacls, name, bases, vars)
            return cls
    
    MC = MetaClass('Foo', (), {})
    # vs CPython missing: ['__class__', '__delattr__', '__getattribute__', '__hash__', '__reduce__', '__reduce_ex__', '__setattr__', '__str__']
    if is_cli or is_silverlight:
        attrs = ['__dict__', '__doc__', '__init__', '__module__', '__new__', '__repr__', '__weakref__']
        
        has = dir(MC)
        for attr in attrs:
            Assert(has.index(attr) != -1)
    
    # metaclass such as defined in string.py
    class MetaClass2(type):
        def __init__(metacls, name, bases, vars):
            super(MetaClass2, name).__init__
    #!!! more meta testing todo


@skip("win32")
def test_override_param_testing():
    #########################################################
    class OverrideParamTesting(MoreOverridding):
        def Test1(self, *args):
            return "xx" + args[0] + args[1]
        def Test2(self, x, *args):
            return "xx" + x + args[0] + args[1]
        def Test3(self, xr):
            xr.Value += "xx"
            return "Test3"
        def Test4(self, x, yr):
            yr.Value += x
            return "Test4"
        def Test5(self, sc):
            return "xx" + str(sc)
        def Test6(self, x, sc):
            return "xx" + x + str(sc)
        
    a = OverrideParamTesting()
    x = a.CallTest1()
    Assert(x == 'xxaabb')
    x = a.CallTest2()
    Assert(x == 'xxaabbcc')
    AreEqual(a.CallTest3("@"), ("Test3", "@xx"))
    AreEqual(a.CallTest4("@"), ("Test4", "@aa"))
    x = a.CallTest5()
    Assert(x == 'xxOrdinal')
    x = a.CallTest6()
    Assert(x == 'xxaaOrdinal')
        
    try:
        a.CallTest3()
        Assert(False)
    except TypeError:
        pass
        
    try:
        a.CallTest4()
        Assert(False)
    except TypeError:
        pass
    


##############################################################

def test_mangling():
    class _ToMangle(object):
        def getPriv(self):
            return self.__value
        def setPriv(self, val):
            self.__value = val
    
    class AccessMangled(_ToMangle):
        def inheritGetPriv(self):
            return self._ToMangle__value
    
    a = AccessMangled()
    a.setPriv('def')
    Assert(a.inheritGetPriv() == 'def')

##############################################################

@skip("win32")
def test_even_more_overriding():
    class Test(BaseClass):
            def __new__(cls):
                return super(cls, Test).__new__(cls, Width=20, Height=30)
        
    a = Test()
    Assert(a.Width == 20)
    Assert(a.Height == 30)
        
    class Test(MoreOverridding):
        def Test1(self, *p):
            return "Override!"
        
    class OuterTest(Test): pass
        
    a = OuterTest()
    Assert(a.Test1() == "Override!")
        
    class OuterTest(Test):
        def Test1(self, *p):
            return "Override Outer!"
        
    a = OuterTest()
    Assert(a.Test1() == "Override Outer!")

def test_oldstyle_inheritance_dir():
    # BUG 463
    class PythonBaseClass:
        def Func(self):
            print "PBC::Func"
    
    class PythonDerivedClass(PythonBaseClass): pass
    
    Assert('Func' in dir(PythonDerivedClass))
    Assert('Func' in dir(PythonDerivedClass()))

if is_cli or is_silverlight:
    def test_cli_inheritance_dir():
        class PythonDerivedFromCLR(System.Collections.Generic.List[int]): pass
        #
        # now check that methods are available, but aren't visible in dir()
        Assert('Capacity' in dir(PythonDerivedFromCLR))
        Assert('InsertRange' in dir(PythonDerivedFromCLR()))
        Assert(hasattr(PythonDerivedFromCLR, 'Capacity'))
        Assert(hasattr(PythonDerivedFromCLR(), 'InsertRange'))
        #


##############################################################

class Flag:  pass
f = Flag()

@skip("win32")
def test_subclass_into_cli():
    ##
    ## Subclassing once as python class, use it; and pass into CLI again
    ##
    class PythonClass(CliInterface):
        def M1(self):
            f.v = 100
        def M2(self, x):
            f.v = 200 + x
        
    p = PythonClass()
    p.M1()
    AreEqual(f.v, 100)
        
    p.M2(1)
    AreEqual(f.v, 201)
        
    c = UseCliClass()
    c.AsParam10(p)
    AreEqual(f.v, 100)
        
    #Bug 562
    #c.AsParam11(p, 2)
    #AreEqual(f.v, 202)
        
    p.InstanceAttr = 200
    p2 = c.AsRetVal10(p)
    AreEqual(p, p2)
    AreEqual(p2.InstanceAttr, 200)
        
    # normal scenario
    class PythonClass(CliAbstractClass):
        def MV(self, x):
            f.v = 300 + x
        def MA(self, x):
            f.v = 400 + x
        
    p = PythonClass()
    p.MS(1)
    AreEqual(p.helperF, -2 * 1)
        
    p.MI(2)
    AreEqual(p.helperF, -3 * 2)
        
    p.MV(3)
    AreEqual(f.v, 303)
        
    p.MA(4)
    AreEqual(f.v, 404)
        
    c.AsParam20(p, 3)
    AreEqual(f.v, 303)
        
    c.AsParam21(p, 4)
    AreEqual(f.v, 404)
        
    c.AsParam22(p, 2)
    AreEqual(p.helperF,  - 2*2)
        
    c.AsParam23(p, 1)
    AreEqual(p.helperF, - 3*1)
    
    # virtual function is not overriden in the python class: call locally, and pass back to clr and call.
    class PythonClass(CliAbstractClass): pass
    p = PythonClass()
    p.MV(5)
    AreEqual(p.helperF, -4 * 5)
        
    c.AsParam20(p, 6)
    AreEqual(p.helperF, -4 * 6)
    
    # "override" a  non-virtual method, and pass the object back to clr. This method should not be called
    class PythonClass(CliAbstractClass):
        def MI(self, x):
                f.v = x * 300
    p = PythonClass()
    f.v = 0
    c.AsParam23(p, 2)
    AreEqual(f.v, 0)
    AreEqual(p.helperF, -3 * 2)

@skip("win32")
def test_subclass_twice():
    ##
    ## Subclassing twice
    ##
    class PythonClass(CliInterface): pass
    class PythonClass2(PythonClass): pass
        
    class PythonClass(CliAbstractClass): pass
    class PythonClass2(PythonClass): pass

@skip("win32")
def test_negative_cli():
        ##
        ## Negative cases: struct, enum, delegate
        ##
        try:
            class PythonClass(MySize): pass
            Fail("should thrown")
        except TypeError:    pass
        
        try:
            class PythonClass(DaysInt): pass
            Fail("should thrown")
        except TypeError:    pass
        
        try:
            class PythonClass(VoidDelegate): pass
            Fail("should thrown")
        except TypeError:    pass
    
@skip("win32")
def test_all_virtuals():
        ##
        ## All Virtual stuff can be overriden? and run
        ##
        class PythonClass(CliVirtualStuff): pass
        class PythonClass2(PythonClass):
            def VirtualMethod(self, x):
                return 20 * x
            def VirtualPropertyGetter(self):
                return 20 * self.InstanceAttr
            def VirtualPropertySetter(self, x):
                self.InstanceAttr = x
            VirtualProperty = property(VirtualPropertyGetter, VirtualPropertySetter)
        
            def VirtualProtectedMethod(self): return 2000
            VirtualProtectedProperty = property(VirtualPropertyGetter, VirtualPropertySetter)
        
        
        p = PythonClass()
        AreEqual(p.VirtualMethod(1), 10)
        p.VirtualProperty = 99
        AreEqual(p.VirtualProperty, 99)
        
        p2 = PythonClass2()
        AreEqual(p2.VirtualMethod(1), 20)
        p2.VirtualProperty = -1
        AreEqual(p2.VirtualProperty, -20)
        
        Assert(p2.PublicStuffCheckHelper(-1 * 20,20 * 10))
        
        p2.VirtualProtectedProperty = 999
        Assert(p2.ProtectedStuffCheckHelper(999 * 20,2000))


##############################################################

def test_direct_Type_call():
    result = type('a', (list,), dict()) ((1,2))
    result = type('a', (str,), dict()) ('abc')
    result = type('a', (tuple,), dict()) ('abc')
    result = type('a', (dict,), dict()) ()
    result = type('a', (int,), dict()) (0)



def test_override_tostr():
    class foo(object):
        def __str__(self):
            return 'abc'
            
            
    a = foo()
    AreEqual(str(a), 'abc')

##############################################################
# set virtual override on an instance method

@skip("win32")
def test_instance_override():
    class Foo(Overriding):
        pass
            
    def MyTemplate(self):
        return "I'm Derived"
            
    a = Foo()
    a.TemplateMethod = type(a.TemplateMethod)(MyTemplate, a)
        
    AreEqual(a.TopMethod(), "I'm Derived - and Top")


##############################################################
# new / init combos w/ inheritance from CLR class

@skip("win32")
def test_new_init():
    
    # CtorTest has no __init__, so the parameters passed directly
    # to Foo in these tests will always go to the ctor.
        
    # 3 ints
    class Foo(CtorTest):
        def __init__(self, a, b, c):
                AreEqual(self.CtorRan, 0)
                super(CtorTest, self).__init__(a, b, c)
        
    a = Foo(2,3,4)
        
        
    # 3 strings
    class Foo(CtorTest):
        def __init__(self, a, b, c):
                AreEqual(self.CtorRan, 1)
                super(CtorTest, self).__init__(a, b, c)
        
    a = Foo("2","3","4")
        
        
    # single int, init adds extra args
    class Foo(CtorTest):
        def __init__(self, a):
            AreEqual(self.CtorRan, 3)
            super(CtorTest, self).__init__(a, 2, 3)
        
    a = Foo(2)
        
    # single string, init adds extra args
        
    class Foo(CtorTest):
        def __init__(self, a):
            AreEqual(self.CtorRan, 2)
            super(CtorTest, self).__init__(a, "2", "3")
        
    a = Foo("2")
        
        
    # single string (shoudl go to string overload)
    class Foo(CtorTest):
        def __init__(self):
            AreEqual(self.CtorRan, -1)
            super(CtorTest, self).__init__("2")
        
    a = Foo()
        
    # single int (should go to object overload)
    class Foo(CtorTest):
        def __init__(self):
            AreEqual(self.CtorRan, -1)
            super(CtorTest, self).__init__(2)
        
    a = Foo()
        
        
    # init adds int, we call w/ no args, should go to object
    class Foo(CtorTest):
        def __init__(self):
            AreEqual(self.CtorRan, -1)
            super(CtorTest, self).__init__(2)
        
        
    a = Foo()
        
    class Foo(CtorTest):
        def __init__(self):
            AreEqual(self.CtorRan, -1)
            super(CtorTest, self).__init__(2,3,4)
        
        
    a = Foo()
        
        
    ########################################################
    # verify we can't call it w/ bad args...
        
    class Foo(CtorTest):
        def __init__(self):
                super(CtorTest, self).__init__()
        
    def BadFoo():
        a = Foo(2,3,4,5)
        
    AssertError(TypeError, BadFoo)
        
        
    ########################################################
    # now run the __new__ tests.  Overriding __new__ should
    # allow us to change the parameters that can be passed
    # to create the function
        
        
    class Foo(CtorTest):
        def __new__(cls, a, b, c):
                ret = CtorTest.__new__(CtorTest, a, b, c)
                return ret
        
    a = Foo(2,3,4)
    AreEqual(a.CtorRan, 0)
        
        
    a = Foo("2","3","4")
    AreEqual(a.CtorRan, 1)
        
    # use var-args to invoke arbitrary overloads...
        
    class Foo(CtorTest):
        def __new__(cls, *args):
                ret = CtorTest.__new__(CtorTest, *args)
                return ret
        
    
    a = Foo(2,3,4)
    AreEqual(a.CtorRan, 0)
        
    a = Foo("2","3","4")
    AreEqual(a.CtorRan, 1)
    
    a = Foo("abc")
    AreEqual(a.CtorRan, 2)
        
    a = Foo([])
    AreEqual(a.CtorRan, 3)
    
@skip("win32")
def test_new_init_combo():
    ########################################################
    # new/init combo tests...
        
        
    class Foo(CtorTest):
        def __new__(cls, *args):
            ret = CtorTest.__new__(CtorTest, *args)
            return ret
        def __init__(self, *args):  pass
              
                        
    # empty init, we should be able to create any of them...
        
    a = Foo(2,3,4)
    AreEqual(a.CtorRan, 0)
        
    a = Foo("2","3","4")
    AreEqual(a.CtorRan, 1)
        
    a = Foo("abc")
    AreEqual(a.CtorRan, 2)
        
    a = Foo([])
    AreEqual(a.CtorRan, 3)
               
        
    class Foo(CtorTest):
        def __new__(cls, *args):
            ret = CtorTest.__new__(Foo, *args)
            return ret
        def __init__(self):
            super(CtorTest, self).__init__(self)
        
    #ok, we have a compatbile init...
    a = Foo()
    AreEqual(a.CtorRan, -1)
        
    #should all fail due to incompatible init.
    AssertError(TypeError, Foo, 2,3,4)
    AssertError(TypeError, Foo, "2","3","4")
    AssertError(TypeError, Foo, "abc")
    AssertError(TypeError, Foo, [])
        
        
    class Foo(CtorTest):
        def __new__(cls, *args):
            ret = CtorTest.__new__(Foo, *args)
            return ret
        def __init__(self, x):
            super(CtorTest, self).__init__(self, x)
        
        
    a = Foo("abc")
    AreEqual(a.CtorRan, 2)
    a = Foo([])
    AreEqual(a.CtorRan, 3)
        
    AssertError(TypeError, Foo)
    AssertError(TypeError, Foo, 2, 3, 4)
    AssertError(TypeError, Foo, "2", "3", "4")
    AssertError(TypeError, Foo, "2", "3", "4", "5")

##################
# verify tuple is ok after deriving from it.

def test_tuple_inheritance():
    class T(tuple):
        pass
    
    result = 'a' in ('c','d','e')



def test_str_inheritance():
    ##################
    # inheriting from string should allow us to create extensible strings w/ no params
    
    class MyString(str): pass
    
    s = MyString()
    
    AreEqual(s, '')

#################
# inheritance from an interface w/ a property

@skip("win32")
def test_interface_with_property():
        class foo(ITestIt3):
            def get_Foo(self): return 'abc'
            Name = property(fget=get_Foo)
        
        
        a = foo()
        AreEqual(a.Name, 'abc')


def add(x, y): return x + y
    
@skip("win32")
def test_conversions():
        #######################################################################
        #### test converter logics and EmitCastFromObject
        
        #############################################
        ## no inherited stuffs, expecting those default value defined in CReturnTypes
        
        class DReturnTypes(CReturnTypes): pass
        
        used = UseCReturnTypes(DReturnTypes())
        used.Use_void()
        AreEqual(used.Use_Char(), System.Char.MaxValue)
        AreEqual(used.Use_Int32(), System.Int32.MaxValue)
        AreEqual(used.Use_String(), "string")
        AreEqual(used.Use_Int64(), System.Int64.MaxValue)
        AreEqual(used.Use_Double(), System.Double.MaxValue)
        AreEqual(used.Use_Boolean(), True)
        AreEqual(used.Use_Single(), System.Single.MaxValue)
        AreEqual(used.Use_Byte(), System.Byte.MaxValue)
        AreEqual(used.Use_SByte(), System.SByte.MaxValue)
        AreEqual(used.Use_Int16(), System.Int16.MaxValue)
        AreEqual(used.Use_UInt32(), System.UInt32.MaxValue)
        AreEqual(used.Use_UInt64(), System.UInt64.MaxValue)
        AreEqual(used.Use_UInt16(), System.UInt16.MaxValue)
        #See Merlin Work Item 294586 for details on why this isn't supported
        if not (is_silverlight):
            AreEqual(used.Use_Type(), System.Type.GetType("System.Int32"))
        AreEqual(used.Use_RtEnum(), RtEnum.A)
        AreEqual(used.Use_RtDelegate().Invoke(30), 30 * 2)
        AreEqual(used.Use_RtStruct().F, 1)
        AreEqual(used.Use_RtClass().F, 1)
        AreEqual(reduce(add, used.Use_IEnumerator()),  60)
    
@skip("win32")
def test_inherit_returntypes():
        #############################################
        ## inherited all, but with correct return types
        ## expect values defined here
        
        def func(arg): return arg
        
        global flag
        flag = 10
        class DReturnTypes(CReturnTypes):
            def M_void(self): global flag; flag = 20
            def M_Char(self): return System.Char.MinValue
            def M_Int32(self): return System.Int32.MinValue
            def M_String(self): return "hello"
            def M_Int64(self): return System.Int64.MinValue
            def M_Double(self): return System.Double.MinValue
            def M_Boolean(self): return False
            def M_Single(self): return System.Single.MinValue
            def M_Byte(self): return System.Byte.MinValue
            def M_SByte(self): return System.SByte.MinValue
            def M_Int16(self): return System.Int16.MinValue
            def M_UInt32(self): return System.UInt32.MinValue
            def M_UInt64(self): return System.UInt64.MinValue
            def M_UInt16(self): return System.UInt16.MinValue
            def M_Type(self):
                #See Merlin Work Item 294586 for details on this
                if is_silverlight:
                    return System.Int64
                return System.Type.GetType("System.Int64")
            def M_RtEnum(self): return RtEnum.B
            def M_RtDelegate(self): return func
            def M_RtStruct(self): return RtStruct(20)
            def M_RtClass(self): return RtClass(30)
            def M_IEnumerator(self): return iter([1, 2, 3, 4, 5])
        
        used = UseCReturnTypes(DReturnTypes())
        used.Use_void()
        AreEqual(flag, 20)
        AreEqual(used.Use_Char(), System.Char.MinValue)
        AreEqual(used.Use_Int32(), System.Int32.MinValue)
        AreEqual(used.Use_String(), "hello")
        AreEqual(used.Use_Int64(), System.Int64.MinValue)
        AreEqual(used.Use_Double(), System.Double.MinValue)
        AreEqual(used.Use_Boolean(), False)
        AreEqual(used.Use_Single(), System.Single.MinValue)
        AreEqual(used.Use_Byte(), System.Byte.MinValue)
        AreEqual(used.Use_SByte(), System.SByte.MinValue)
        AreEqual(used.Use_Int16(), System.Int16.MinValue)
        AreEqual(used.Use_UInt32(), System.UInt32.MinValue)
        AreEqual(used.Use_UInt64(), System.UInt64.MinValue)
        AreEqual(used.Use_UInt16(), System.UInt16.MinValue)

        #See Merlin Work Item 294586 for details on why this isn't supported
        if not (is_silverlight):
            AreEqual(used.Use_Type(), System.Type.GetType("System.Int64"))
        AreEqual(used.Use_RtEnum(), RtEnum.B)
        AreEqual(used.Use_RtDelegate().Invoke(100), 100)
        AreEqual(used.Use_RtStruct().F, 20)
        AreEqual(used.Use_RtClass().F, 30)
        AreEqual(list(used.Use_IEnumerator()), [1, 2, 3, 4, 5])
        AreEqual(reduce(add, used.Use_IEnumerable()), 66)
    
if is_silverlight or is_cli:
    ## return a class whose derived methods returns the same specified object
    def create_class(retObj):
        class NewC(CReturnTypes):
            def M_void(self): return retObj
            def M_Char(self): return retObj
            def M_Int32(self): return retObj
            def M_String(self): return retObj
            def M_Int64(self): return retObj
            def M_Double(self): return retObj
            def M_Boolean(self): return retObj
            def M_Single(self): return retObj
            def M_Byte(self): return retObj
            def M_SByte(self): return retObj
            def M_Int16(self): return retObj
            def M_UInt32(self): return retObj
            def M_UInt64(self): return retObj
            def M_UInt16(self): return retObj
            def M_Type(self): return retObj
            def M_RtEnum(self): return retObj
            def M_RtDelegate(self): return retObj
            def M_RtStruct(self): return retObj
            def M_RtClass(self): return retObj
            def M_IEnumerator(self): return retObj
            def M_IEnumerable(self): return retObj
        return NewC
            
    def test_inherited_returntypes_odd_returns():
        #############################################
        ## inherited all, but returns with a python old class, or new class,
        ##                    or with explicit ops
        
    
        
        ## all return None
        DReturnTypes = create_class(None)
        
        used = UseCReturnTypes(DReturnTypes())
        AreEqual(used.Use_Type(), None)
        AreEqual(used.Use_String(), None)
        AreEqual(used.Use_RtDelegate(), None)
        AreEqual(used.Use_RtClass(), None)
        AreEqual(used.Use_Boolean(), False)
        AreEqual(used.Use_IEnumerator(), None)
        
        for f in [used.Use_Char, used.Use_Int32, used.Use_Int64,
            used.Use_Double, used.Use_Single, used.Use_Byte, used.Use_SByte, used.Use_RtEnum,
            used.Use_Int16, used.Use_UInt32, used.Use_UInt64, used.Use_UInt16, used.Use_RtStruct, ]:
            AssertError(TypeError, f)
        
        ## return old class instance / user type instance
        class python_old_class: pass
        class python_new_class(object): pass
        
        def check_behavior(expected_obj):
            DReturnTypes = create_class(expected_obj)
            used = UseCReturnTypes(DReturnTypes())
            
            AreEqual(used.Use_void(), None)
            AreEqual(used.Use_Boolean(), True)
            
            for f in [used.Use_Char, used.Use_Int32, used.Use_String, used.Use_Int64,
                used.Use_Double, used.Use_Single, used.Use_Byte, used.Use_SByte,
                used.Use_Int16, used.Use_UInt32, used.Use_UInt64, used.Use_UInt16, used.Use_Type,
                used.Use_RtEnum, used.Use_RtStruct, used.Use_RtClass, used.Use_IEnumerator,
                #used.Use_RtDelegate,
                ]:
                AssertError(TypeError, f)
            
        check_behavior(python_old_class())
        check_behavior(python_new_class())

    def test_extensible_int():
        ## extensible int
        class python_my_int(int): pass
        
        def check_behavior(expected_obj):
            DReturnTypes = create_class(expected_obj)
            used = UseCReturnTypes(DReturnTypes())
            
            AreEqual(used.Use_void(), None)
            AreEqual(used.Use_Boolean(), True)
         
            AreEqual(used.Use_Int32(), System.Int32.Parse("10"))
            AreEqual(used.Use_Int64(), System.Int64.Parse("10"))
            AreEqual(used.Use_Double(), System.Double.Parse("10"))
            AreEqual(used.Use_Single(), System.Single.Parse("10"))
            AreEqual(used.Use_UInt32(), System.UInt32.Parse("10"))
            AreEqual(used.Use_UInt64(), System.UInt64.Parse("10"))
            AreEqual(used.Use_Byte(), System.Byte.Parse("10"))
            AreEqual(used.Use_SByte(), System.SByte.Parse("10"))
            AreEqual(used.Use_Int16(), System.Int16.Parse("10"))
            AreEqual(used.Use_UInt16(), System.UInt16.Parse("10"))
        
            for f in [used.Use_Char, used.Use_String, used.Use_Type, used.Use_RtEnum,
                      used.Use_RtStruct, used.Use_RtClass, used.Use_IEnumerator
                    # used.Use_RtDelegate,
                ]:
                AssertError(TypeError, f)
                
        check_behavior(python_my_int(10))
    
    def test_custom_number_conversion():
        ## customized __int__. __float__
        class python_old_class:
            def __int__(self): return 100
            def __float__(self): return 12345.6
        class python_new_class(object):
            def __int__(self): return 100
            def __float__(self): return 12345.6
            
        def check_behavior(expected_obj):
            DReturnTypes = create_class(expected_obj)
            used = UseCReturnTypes(DReturnTypes())
            
            AreEqual(used.Use_void(), None)
            AreEqual(used.Use_Boolean(), True)
            AreEqual(used.Use_Int32(), System.Int32.Parse("100"))
            AreEqual(used.Use_Double(), System.Double.Parse("12345.6", System.Globalization.CultureInfo.InvariantCulture))
        
            for f in [used.Use_Int16, used.Use_UInt32, used.Use_UInt64, used.Use_UInt16, used.Use_Single,
                      used.Use_Byte, used.Use_SByte, used.Use_Int64, used.Use_Char, used.Use_String,
                      used.Use_Type, used.Use_RtEnum, used.Use_RtStruct, used.Use_RtClass, used.Use_IEnumerator
                #used.Use_RtDelegate,
                ]:
                AssertError(TypeError, f)
        
        check_behavior(python_old_class())
        check_behavior(python_new_class())
    
    #System.Char.Parse('') does not exist in silverlight
    @skip("silverlight")
    def test_return_interesting():
        #############################################
        ## inherited all, but with more interesting return types
        
        class DReturnTypes(CReturnTypes): pass
        drt = DReturnTypes()
        used = UseCReturnTypes(drt)
        
        def func(self): global flag; flag = 60; return 70
        DReturnTypes.M_void = func
        AreEqual(used.Use_void(), None)
        AreEqual(flag, 60)
        
        ## Char
        DReturnTypes.M_Char = lambda self: ord('z')
        AssertError(TypeError, used.Use_Char)
        
        DReturnTypes.M_Char = lambda self: 'y'
        AreEqual(used.Use_Char(), System.Char.Parse('y'))
        
        DReturnTypes.M_Char = lambda self: ''
        AssertError(TypeError, used.Use_Char)
        
        DReturnTypes.M_Char = lambda self: 'abc'
        AssertError(TypeError, used.Use_Char)
        
        ## String
        DReturnTypes.M_String = lambda self: 'z'
        AreEqual(used.Use_String(), 'z')
        
        DReturnTypes.M_String = lambda self: ''
        AreEqual(used.Use_String(), System.String.Empty)
        
        DReturnTypes.M_String = lambda self: ord('z')
        AssertError(TypeError, used.Use_String)
        
        ## Int32
        DReturnTypes.M_Int32 = lambda self: System.Char.Parse('z')
        AssertError(TypeError, used.Use_Int32)
        
        DReturnTypes.M_Int32 = lambda self: System.SByte.Parse('-123')
        AreEqual(used.Use_Int32(), -123)
        
        DReturnTypes.M_Int32 = lambda self: 12345678901234
        AssertError(OverflowError, used.Use_Int32)
        
        ## RtClass
        class MyRtClass(RtClass):
            def __init__(self, value):
                super(MyRtClass, self).__init__(value)
            
        DReturnTypes.M_RtClass = lambda self: MyRtClass(500)
        AreEqual(used.Use_RtClass().F, 500)
        
        ## IEnumerator
        DReturnTypes.M_IEnumerator = lambda self: iter((2, 20, 200, 2000))
        AreEqual(tuple(used.Use_IEnumerator()), (2, 20, 200, 2000))
        
        ## IEnumerable
        DReturnTypes.M_IEnumerable = lambda self: (2, 20, 200, 2000)
        AreEqual(reduce(add, used.Use_IEnumerable()), 2222)
        
        DReturnTypes.M_IEnumerator = lambda self: iter({ 1 : "one", 10: "two", 100: "three"})
        AreEqual(set(used.Use_IEnumerator()), set([1, 10, 100]))
        
        DReturnTypes.M_IEnumerable = lambda self: { 1 : "one", 10: "two", 100: "three"}
        AreEqual(reduce(add, used.Use_IEnumerable()), 111)
        
        DReturnTypes.M_IEnumerator = lambda self: iter(System.Array[int](range(10)))
        AreEqual(list(used.Use_IEnumerator()), range(10))
        
        DReturnTypes.M_IEnumerable = lambda self: System.Array[int](range(10))
        AreEqual(reduce(add, used.Use_IEnumerable()), 45)
        
        ## RtDelegate
        def func2(arg1, arg2): return arg1 * arg2
        
        DReturnTypes.M_RtDelegate = lambda self : func2
        delegate = used.Use_RtDelegate()
        AssertError(TypeError, delegate, 1)

    def test_redefine_non_virtual():
        #############################################
        ## Redefine non-virtual method:
        
        class DReturnTypes(CReturnTypes): pass
        
        used = UseCReturnTypes(DReturnTypes())
        AreEqual(used.Use_NonVirtual(), 100)
        
        class DReturnTypes(CReturnTypes):
            def M_NonVirtual(self): return 200
        
        used = UseCReturnTypes(DReturnTypes())
        AreEqual(used.Use_NonVirtual(), 100)
    
    def test_interface_abstract_type():
        #############################################
        ## Similar but smaller set of test for interface/abstract Type
        ##
        def test_returntype(basetype, usetype):
            class derived(basetype): pass
            
            used = usetype(derived())
        
            for f in [ used.Use_void,used.Use_Char,used.Use_Int32,used.Use_String,used.Use_Int64,used.Use_Double,used.Use_Boolean,
                used.Use_Single,used.Use_Byte,used.Use_SByte,used.Use_Int16,used.Use_UInt32,used.Use_UInt64,used.Use_UInt16,
                used.Use_Type,used.Use_RtEnum,used.Use_RtDelegate,used.Use_RtStruct,used.Use_RtClass,used.Use_IEnumerator,
                ]:
                AssertError(AttributeError, f)
            
            class derived(basetype):
                def M_void(self): global flag; flag = 20
                def M_Char(self): return System.Char.MinValue
                def M_Int32(self): return System.Int32.MinValue
                def M_String(self): return "hello"
                def M_Int64(self): return System.Int64.MinValue
                def M_Double(self): return System.Double.MinValue
                def M_Boolean(self): return False
                def M_Single(self): return System.Single.MinValue
                def M_Byte(self): return System.Byte.MinValue
                def M_SByte(self): return System.SByte.MinValue
                def M_Int16(self): return System.Int16.MinValue
                def M_UInt32(self): return System.UInt32.MinValue
                def M_UInt64(self): return System.UInt64.MinValue
                def M_UInt16(self): return System.UInt16.MinValue
                def M_Type(self):
                    #See Merlin Work Item 294586 for details on this
                    if is_silverlight:
                        return System.Int64
                    return System.Type.GetType("System.Int64")
                def M_RtEnum(self): return RtEnum.B
                def M_RtDelegate(self): return lambda arg: arg * 5
                def M_RtStruct(self): return RtStruct(20)
                def M_RtClass(self): return RtClass(30)
                def M_IEnumerator(self): return iter([1, 2, 3, 4, 5])
                def M_IEnumerable(self): return [7, 8, 9, 10, 11]
        
            used = usetype(derived())
            used.Use_void()
            AreEqual(flag, 20)
            AreEqual(used.Use_Char(), System.Char.MinValue)
            AreEqual(used.Use_Int32(), System.Int32.MinValue)
            AreEqual(used.Use_String(), "hello")
            AreEqual(used.Use_Int64(), System.Int64.MinValue)
            AreEqual(used.Use_Double(), System.Double.MinValue)
            AreEqual(used.Use_Boolean(), False)
            AreEqual(used.Use_Single(), System.Single.MinValue)
            AreEqual(used.Use_Byte(), System.Byte.MinValue)
            AreEqual(used.Use_SByte(), System.SByte.MinValue)
            AreEqual(used.Use_Int16(), System.Int16.MinValue)
            AreEqual(used.Use_UInt32(), System.UInt32.MinValue)
            AreEqual(used.Use_UInt64(), System.UInt64.MinValue)
            AreEqual(used.Use_UInt16(), System.UInt16.MinValue)
            #See Merlin Work Item 294586 for details on why this isn't supported
            if not (is_silverlight):
                AreEqual(used.Use_Type(), System.Type.GetType("System.Int64"))
            AreEqual(used.Use_RtEnum(), RtEnum.B)
            AreEqual(used.Use_RtDelegate().Invoke(100), 100 * 5)
            AreEqual(used.Use_RtStruct().F, 20)
            AreEqual(used.Use_RtClass().F, 30)
            AreEqual(list(used.Use_IEnumerator()), [1, 2, 3, 4, 5])
            AreEqual(reduce(add, used.Use_IEnumerable()), 45)
        
        test_returntype(IReturnTypes, UseIReturnTypes)
        test_returntype(AReturnTypes, UseAReturnTypes)
    
    
    def test_bigvirtual_derived():
        # verify that classes w/ large vtables we get the
        # correct method dispatch when overriding bases
        class BigVirtualDerived(BigVirtualClass):
            def __init__(self):
                self.funcCalled = False
            for x in range(50):
                exec 'def M%d(self):\n    self.funcCalled = True\n    return super(type(self), self).M%d()' % (x, x)
        
        a = BigVirtualDerived()
        for x in range(50):
            # call from Python
            AreEqual(a.funcCalled, False)
            AreEqual(x, getattr(a, 'M'+str(x))())
            AreEqual(a.funcCalled, True)
            a.funcCalled = False
            # call non-virtual method that calls from C#
            AreEqual(x, getattr(a, 'CallM'+str(x))())
            AreEqual(a.funcCalled, True)
            a.funcCalled = False
    
def test_super_inheritance():
    # descriptor for super should return derived class, not a new instance of super
    class foo(super):
        def __init__(self, *args):
            return super(foo, self).__init__(*args)
    
    
    class bar(object): pass
    
    # when lookup comes from super's class it should
    # be from it's type, not from the super base type
    AreEqual(foo(bar).__class__, foo)

    bar.x = foo(bar)
    AreEqual(type(bar().x), foo)        # once via .
    x = foo(bar)
    AreEqual(type(x.__get__(bar, foo) ), foo)   # once by calling descriptor directly
    
def test_super_new_init():
    x = super.__new__(super)
    AreEqual(x.__thisclass__, None)
    AreEqual(x.__self__, None)
    AreEqual(x.__self_class__, None)

    x.__init__(super, None)
    
    AreEqual(x.__thisclass__, super)
    AreEqual(x.__self__, None)
    AreEqual(x.__self_class__, None)

    x.__init__(super, x)
    
    AreEqual(x.__thisclass__, super)
    AreEqual(x.__self__, x)
    AreEqual(x.__self_class__, super)
    
    Assert(repr(x.__self__).find('<super object>') != -1)  # __self__'s repr goes recursive and gets it's display tweaked

def test_super_class():
    """verify super on a class passes None for the instance"""
    
    class custDescr(object):
        def __get__(self, instance, owner):
            AreEqual(instance, None)
            return 'abc'
            
    class base(object):
        aProp = property(lambda self: 'foo')
        aDescr = custDescr()
        
    class sub(base):
        def test1(cls): return super(sub, cls).aProp
        def test2(cls): return super(sub, cls).aDescr
        test1 = classmethod(test1)
        test2 = classmethod(test2)
        
    AreEqual(sub.test2(), 'abc')
    AreEqual(type(sub.test1()), property)
     
     
def test_super_proxy():
    class mydescr(object):
        def __init__(self, func):
            self.func = func
        def __get__(self, instance, context):
            AreEqual(context, C)
            x = self.func.__get__(instance, context)
            return x
        
    class Proxy(object):
        def __init__(self, obj):
            self.__obj = obj
        def __getattribute__(self, name):
            if name.startswith("_Proxy__"): return object.__getattribute__(self, name)
            else: return getattr(self.__obj, name)
    
    class B(object):
        def f(self):
            return "B.f"
    
    class C(B):
        def f(self):
            return super(C, self).f() + "->C.f"
            
    C.f = mydescr(C.f)
    B.f = mydescr(B.f)
    obj = C()
    p = Proxy(obj)
    AreEqual(C.f(p), 'B.f->C.f')
    
def test_super_tostring():
    class C(object):        
        def __new__(cls):
            x = super(cls, C)
            Assert("<super" in str(x))
            return x.__new__(cls)

    c = C()    
	
@skip("win32")
def test_inherit_mixed_properties():
    for base in (MixedPropertiesInherited, MixedProperties):
        class MyClass(base):
            def __init__(self):
                AreEqual(self.Foo, None)
                self.Foo = 42
                AreEqual(self.Foo, 42)

                AreEqual(self.Bar, None)
                self.Bar = 23
                AreEqual(self.Bar, 23)
                
                AreEqual(self.GetFoo(), 42)
                AreEqual(self.GetBar(), 23)
        
        a = MyClass()
        AreEqual(a.GetFoo(), 42)
        AreEqual(a.GetBar(), 23)

run_test(__name__)
