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
This test module verifies that properties of COM methods are identical to those
of new-style class methods.
'''
#------------------------------------------------------------------------------
from iptest.assert_util import skiptest
skiptest("silverlight")
from iptest.cominterop_util import *

#------------------------------------------------------------------------------
#--GLOBALS
com_obj = getRCWFromProgID("DlrComLibrary.DlrUniversalObj")

#COM methods
m0    = com_obj.m0
m2    = com_obj.m2
m1kw1 = com_obj.m1kw1

#All Python methods should have the following attributes
STD_METHOD_ATTRIBUTES = ['__call__', '__class__', '__cmp__', '__delattr__', 
                        '__doc__', '__get__', '__getattribute__', '__hash__', 
                        '__init__', '__new__', '__reduce__', '__reduce_ex__', 
                        '__repr__', '__setattr__', '__str__', 'im_class', 
                        'im_func', 'im_self']

#Attributes which are missing from COM methods in IP with no flags used                        
#Merlin 370042
BROKEN_STD_METHOD_ATTRIBUTES = ['__cmp__', 'im_class', 'im_func', 'im_self']

#Attributes which are missing from COM methods in IP w/o -X:PreferComInteropAssembly
#Merlin 370043
BROKEN_ID_STD_METHOD_ATTRIBUTES = ['__call__', '__cmp__', '__get__', 'im_class', 'im_func', 'im_self']

#------------------------------------------------------------------------------
#--CLASSES AND HELPER FUNCTIONS

class stdout_reader:
    '''
    Used to take over stdout.
    '''
    def __init__(self):
        self.text = ''
    def write(self, text):
        self.text += text

class DlrUniversalObj(object):
    '''
    Standard new-style Python class which provides methods similar to the COM 
    methods being tested by this module.
    '''
    def m0(self): return
    def m2(self, arg1, arg2): 
        if type(arg1)!=str or type(arg2)!=str:
            raise TypeError()
        return
    def m1kw1(self, arg1, arg2=None): return

#------------------------------------------------------------------------------
#--HIGH-PRIORITY TESTS
def test_sanity():

    #--Just make sure all newstyle class method attributes are there
    m0_attributes = dir(m0)
    
    for std_attr in STD_METHOD_ATTRIBUTES:
        try:
            Assert(std_attr in m0_attributes, 
                   std_attr + " is not a method attribute of " + str(m0))
        except Exception, e:
            #Ignore known failures
            if is_pywin32: raise e
            
            if std_attr not in BROKEN_ID_STD_METHOD_ATTRIBUTES: raise e

#--------------------------------------    
def test_args_m0():
    AreEqual(m0(),          None)
    AreEqual(m0(*[]),       None)
    AreEqual(m0(**{}),      None)
    AreEqual(m0(*[], **{}), None)

def test_args_m0_neg():
    #Dev10 409926
    #expected_except_type = TypeError
    expected_except_type = StandardError

    bad_cases = [None, 1, "None", object, u""]

    AssertError(expected_except_type, 
                lambda: m0(None))
    AssertError(expected_except_type, 
                lambda: m0(1))  
    AssertError(expected_except_type, 
                lambda: m0(1, "None"))  
    AssertError(expected_except_type, 
                lambda: m0(1, "None", object))   
    AssertError(expected_except_type, 
                lambda: m0(None, *[]))   
    AssertError(expected_except_type, 
                lambda: m0(None, *[None]))         
    AssertError(expected_except_type, 
                lambda: m0(None, **{None:None}))       
    AssertError(expected_except_type, 
                lambda: m0(None, *[None], **{None:None}))              
    
    #m0(*[])
    for bad in bad_cases:
        for i in xrange(1, 4):
            AssertError(expected_except_type, 
                        lambda: m0(*list([bad] * i)))

    #m0(**{})
    for i in xrange(1, len(bad_cases)):
        t_dict = {}
        for j in xrange(i):
            t_dict[bad_cases[j]] = bad_cases[j]
        
        AssertError(expected_except_type, 
                    lambda: m0(**t_dict))                        
    
    #m0(*[], **{})
    for i in xrange(0, len(bad_cases)):
        t_dict = {}
        for j in xrange(i):
            t_dict[bad_cases[j]] = bad_cases[j]
        
        for arg_bad in bad_cases:
            for k in xrange(0, 4):
                if k + len(t_dict.keys())==0: continue
                AssertError(expected_except_type, 
                            lambda: m0( *list([arg_bad] * k), 
                                        **t_dict))              
    
    
def test_args_m2():
    AreEqual(m2("a", "b"),              None)
    AreEqual(m2(arg1="a", arg2="b"),    None)
    AreEqual(m2("a", *["b"]),           None)
    AreEqual(m2(arg2="b", *["a"]),      None)
    AreEqual(m2(*["a", "b"]),           None)
    AreEqual(m2("a", "b", **{}),        None)
    AreEqual(m2(*["a", "b"], **{}),     None)

def test_args_m2_neg():
    #pywin32 throws a pywintypes.com_error.  Just skip it
    if sys.platform=="win32" and not isinstance(com_obj, DlrUniversalObj):
        print "...skipping under pywin32"
        return
    
    #Dev10 409926
    #expected_except_type = TypeError
    expected_except_type = StandardError
    
    bad_cases = ["", "None", "xyz"]
    #m2(.)
    for bad in bad_cases:
        AssertError(expected_except_type, 
                    lambda: m2(bad))
    
    #m2(...)
    for bad in bad_cases:
        AssertError(expected_except_type, 
                    lambda: m2(bad, bad, bad))
    
    #m2(.,?)
    #m2(.., ?)
    for arg_len in xrange(3):
        for kwarg_len in xrange(3):
            for bad in bad_cases:
                t_args = [bad] * arg_len
                t_kwargs = {}
                #This does not really permute the keyword args though
                for i in xrange(kwarg_len): t_kwargs[bad_cases[i] + str(i)] = bad_cases[i]

                if arg_len + kwarg_len!=1: #m2(., ?)
                    AssertError(expected_except_type, 
                                lambda: m2(bad,
                                           *t_args, 
                                           **t_kwargs))   
                                
                if arg_len + kwarg_len!=0: #m2(.., ?)
                    AssertError(expected_except_type, 
                                lambda: m2(bad, bad,
                                           *t_args, 
                                           **t_kwargs))   
                
    
def test_args_m1kw1():
    AreEqual(m1kw1("a"),                None)
    AreEqual(m1kw1(*["a"]),             None)
    AreEqual(m1kw1(arg1="3"),           None)
    
    AreEqual(m1kw1("a", **{}),          None)
    AreEqual(m1kw1(*["a"], **{}),       None)
    
    AreEqual(m1kw1("a", "b"),           None)
    AreEqual(m1kw1("a", *["b"]),        None)
    AreEqual(m1kw1(*["a", "b"]),        None)
    
    AreEqual(m1kw1("a", "b", **{}),     None)
    AreEqual(m1kw1("a", *["b"], **{}),  None)
    AreEqual(m1kw1(*["a", "b"], **{}),  None)
    
    AreEqual(m1kw1("a", **{"arg2":1}),  None)
    AreEqual(m1kw1("a", arg2=1),        None)
    
    AreEqual(m1kw1(arg1=7, arg2=1),     None)
    AreEqual(m1kw1(arg2=1, arg1=7),     None)
    
def test_args_m1kw1_neg():
    '''
    TODO: there are still a few more interesting cases here.
    '''
    #Dev10 409926
    #expected_except_type = TypeError
    expected_except_type = StandardError
    
    #pywin32 throws a pywintypes.com_error.  Just skip it
    if sys.platform=="win32" and not isinstance(com_obj, DlrUniversalObj):
        print "...skipping under pywin32"
        return
    
    
    #m1kw1()
    AssertError(expected_except_type, 
                lambda: m1kw1()) 
    #m1kw1(*[])
    AssertError(expected_except_type, 
                lambda: m1kw1(*[])) 
    #m1kw1(**{})
    AssertError(expected_except_type, 
                lambda: m1kw1(**{})) 
    #m1kw1(*[], **{})
    AssertError(expected_except_type, 
                lambda: m1kw1(*[], **{})) 
                
    #m1kw1(arg2=.)
    AssertError(expected_except_type, 
                lambda: m1kw1(arg2=3))
    #m1kw1(arg2=., **{arg2:?})      
    AssertError(expected_except_type, 
                lambda: m1kw1(arg2=3, **{"arg2":7}))
    #m1kw1(arg2=., *[], **{})      
    AssertError(expected_except_type, 
                lambda: m1kw1(arg2=3, *[], **{}))
    #m1kw1(arg2=., c=.)
    AssertError(expected_except_type, 
                lambda: m1kw1(arg2=3, c=7))
    
    #m1kw1(arg1, arg1=.)
    AssertError(expected_except_type, 
                lambda: m1kw1(1, arg1=3))    
    #m1kw1(arg1, arg2=., **{arg2:?})      
    AssertError(expected_except_type, 
                lambda: m1kw1(4, arg2=3, **{"arg2":7}))
                
    #m1kw1(arg1, arg2=., **{c:?})      
    AssertError(expected_except_type, 
                lambda: m1kw1(4, arg2=3, **{"c":9}))
                
    #m1kw1(arg1, arg2=., c=.)   
    AssertError(expected_except_type, 
                lambda: m1kw1(4, arg2=3, c=7))  
                
    #m1kw1(*[arg1,arg2,c])   
    AssertError(expected_except_type, 
                lambda: m1kw1(*[1, 2, 3]))  
    
    #m1kw1(*[arg1, arg2], **{arg2:?})
    AssertError(expected_except_type, 
                lambda: m1kw1(*[1, 2], **{"arg2": 3}))  
    
    
#--------------------------------------
def test_as_callable_obj():
    '''
    Also covers:
    - limited name binding
    - deletion
    - is
    '''
    #--------------------------------------
    #Can the method be assigned to a var?
    t_m0 = com_obj.m0
    AreEqual(t_m0(), None)
    
    #--------------------------------------
    #Can the method be attached to a class?
    class KOld:
        static_m0 = com_obj.m0
        
    class KNew(object):
        static_m0 = com_obj.m0
        
    AreEqual(KOld.static_m0(), None)
    AreEqual(KNew.static_m0(), None)
    
    #--------------------------------------
    #Can the method be attached to an object?
    ko = KOld()
    ko.attr_m0 = com_obj.m0
    AreEqual(ko.attr_m0(), None)
    
    kn = KNew()
    kn.attr_m0 = com_obj.m0
    AreEqual(kn.attr_m0(), None)
    
    #--------------------------------------
    #Can the method be passed to a function?
    #Can the method be returned from the function?
    def tempFunc(someMethod):
        AreEqual(someMethod(), None)
        return someMethod
    
    names_list = [  com_obj.m0, t_m0, 
                KOld.static_m0, KNew.static_m0,
                ko.attr_m0, kn.attr_m0,
                ]
        
    for x in names_list:
                AreEqual(tempFunc(x)(), None)
                AreEqual(tempFunc(x), x)
    
    #--------------------------------------
    #Are the methods showing up where they should be?
    Assert("t_m0" in dir())
    Assert("static_m0" in dir(KOld))
    Assert("static_m0" in dir(KNew))
    Assert("attr_m0" in dir(ko))
    Assert("attr_m0" in dir(kn))
        
    #--------------------------------------
    #Can the method be deleted?
    del t_m0 
    del KOld.static_m0
    del KNew.static_m0
    del ko.attr_m0 
    del kn.attr_m0
    
    #--------------------------------------
    #Does the main object still have a reference
    import gc
    gc.collect()
    Assert(hasattr(com_obj, "m0"))
    
    #--------------------------------------
    #Has the method really been deleted?
    if not is_cli:  #Likely to fail under CLI randomly
        Assert("t_m0" not in dir())
        Assert("static_m0" not in dir(KOld))
        Assert("static_m0" not in dir(KNew))
        Assert("attr_m0" not in dir(ko))
        Assert("attr_m0" not in dir(kn))
    

def test_method_del():
    '''
    Also some coverage in test_as_callable_obj.
    '''
    try:
        del com_obj.m0
        Fail("Should have been an AttributeError when attempting to delete a class method.")
    except AttributeError, e:
        pass

def test_method_globals():
    t_globals = {}
    t_globals["m2"] = m2
    t_globals["a"]  = "abc"
    t_globals["AreEqual"]  = AreEqual
    
    t_locals = {}
    
    exec "AreEqual(m2(a, 'xyz'), None)" in t_globals, t_locals
    Assert(t_globals.has_key("__builtins__")) #Should be injected into globals.
    AreEqual(len(t_globals.keys()), 4)
    AreEqual(t_locals, {})
    
    exec "AreEqual(m2(a, 'xyz'), None)" in t_globals
    AreEqual(len(t_globals.keys()), 4)
    
def test_method_locals():
    t_globals = {}
    t_locals = {}
    t_locals["m2"] = m2
    t_locals["a"]  = "abc"
    t_locals["AreEqual"]  = AreEqual
    
    exec "AreEqual(m2(a, 'xyz'), None)" in t_globals, t_locals
    Assert(t_globals.has_key("__builtins__")) #Should be injected into globals.
    AreEqual(len(t_globals.keys()), 1)
    AreEqual(len(t_locals.keys()), 3)
    
#--------------------------------------
def test_invoke_from_exec():
    for exec_string in [
                        "AreEqual(m0(), None)",
                        "AreEqual(m2('abc', 'xyz'), None)",
                        "AreEqual(m1kw1(1, arg2=None), None)",
                        ]:
        exec exec_string in globals(), {}
        
        
def test_invoke_from_exec_neg():
    #pywin32 throws a pywintypes.com_error.  Just skip it
    if sys.platform=="win32" and not isinstance(com_obj, DlrUniversalObj):
        print "...skipping under pywin32"
        return
        
    try:
        exec "AreEqual(m0(3), None)" in globals(), {}
        Fail("Should have raised an EnvironmentError")
    except EnvironmentError, e:
        #TODO: this is a bug
        pass
    except TypeError, e:
        if isinstance(com_obj, DlrUniversalObj):
            pass
        
    try:
        exec "AreEqual(m2('xyz'), None)" in globals(), {}
        Fail("Should have raised a StandardError")
    except StandardError, e:
        pass
        
    try:
        exec "AreEqual(m1kw1(arg2=None), None)" in globals(), {}
        Fail("Should have raised a StandardError")
    except StandardError, e:
        pass


def test_invoke_from_eval():
    for eval_string in [
                        "m0()",
                        "m2('abc', 'xyz')",
                        "m1kw1(1, arg2=None)",
                        ]:
        AreEqual(eval(eval_string, globals(), {}),
                 None)
        
def test_invoke_from_eval_neg():
    #pywin32 throws a pywintypes.com_error.  Just skip it
    if sys.platform=="win32" and not isinstance(com_obj, DlrUniversalObj):
        print "...skipping under pywin32"
        return
    
    AssertError(StandardError, eval, "m0(3)", globals(), {})
    AssertError(StandardError, eval, "m2('xyz')", globals(), {})
    AssertError(StandardError, eval, "m1kw1(arg2=None)", globals(), {})
    

@skip("win32")
def test_from_cmdline():
    '''
    '''
    from iptest.console_util import IronPythonInstance
    from sys import executable, exec_prefix
    from System import Environment
    
    extraArgs = ""
    if "-X:PreferComInteropAssembly" in Environment.GetCommandLineArgs():
        extraArgs += "-X:PreferComInteropAssembly"
        
    #Does it work from the commandline with positive cases?
    ipi = IronPythonInstance(executable, exec_prefix, extraArgs)
    AreEqual(ipi.Start(), True)
    try:
        ipi.ExecuteLine("from System import Type, Activator")
        ipi.ExecuteLine("com_obj = Activator.CreateInstance(Type.GetTypeFromProgID('DlrComLibrary.DlrUniversalObj'))")
        
        #Dev10 409941    
        Assert("System.__ComObject" in ipi.ExecuteLine("print com_obj"))            
        AreEqual(ipi.ExecuteLine("print com_obj.m0()"), "None")
    finally:
        ipi.End()
        
@skip("win32")
def test_from_cmdline_neg():
    '''
    '''
    from iptest.console_util import IronPythonInstance
    from sys import executable, exec_prefix
    from System import Environment
    
    extraArgs = ""
    if "-X:PreferComInteropAssembly" in Environment.GetCommandLineArgs():
        extraArgs += "-X:PreferComInteropAssembly"
        
    #Does it work from the commandline with negative cases?
    ipi = IronPythonInstance(executable, exec_prefix, extraArgs)
    AreEqual(ipi.Start(), True)
    try:
        ipi.ExecuteLine("from System import Type, Activator")
        ipi.ExecuteLine("import sys")
        ipi.ExecuteLine("com_obj = Activator.CreateInstance(Type.GetTypeFromProgID('DlrComLibrary.DlrUniversalObj'))")
        ipi.ExecuteLine("sys.stderr = sys.stdout")  #Limitation of ipi.  Cannot get sys.stderr...
        response = ipi.ExecuteLine("com_obj.m0(3)")
        Assert("Does not support a collection." in response)  #Merlin 324233
        Assert("EnvironmentError:" in response)  #Merlin 324233
            
    finally:
        ipi.End()
    
#--------------------------------------
def test_help():
    '''
    TODO:
    - do this for m2 and m1kw1 as well?
    '''
    import sys
    stdout_bak = sys.stdout
    fake_stdout = stdout_reader() 

    sys.stdout = fake_stdout
    try:
        help(com_obj.m0)
    finally:
        sys.stdout = stdout_bak


    if isinstance(com_obj, DlrUniversalObj):  #Pure Python version of universal obj
        expected = """Help on method m0 in module %s:

m0(self) method of %s.DlrUniversalObj instance
""" % (__name__, __name__)

    else: #COM universal obj
        #pywin32
        if sys.platform=="win32":
            expected = """Help on method m0 in module win32com.client.dynamic:

m0(self) method of win32com.client.CDispatch instance

"""
        else:
            #Dev10 409942
            expected = """Help on DispCallable"""
            Assert(expected in fake_stdout.text, fake_stdout.text)
            return
            
    #verification
    try:
        AreEqual(expected, fake_stdout.text)
    except Exception, e:
        print fake_stdout.text
        raise e

#--------------------------------------    
def test_is():
    m_list = [m0, m2, m1kw1]
    def simple0(): pass
    class K(object):
        m0_attr = m0
        def m0(self): pass
        
        
    #Does 'is' work?
    Assert(m0 is K.m0_attr)
    for x in m_list:
        Assert(x is x)
    
    #Does 'is not' work?
    Assert(m0 is not simple0)
    Assert(m0 is not K().m0)
    
    bad_is_list = [ None, True, False,
                    1, 0, -1, 1L, 3L, 3.14,
                    "abc", u"", 
                    object, Exception, object(), type, str,
                    [], (), xrange(3), __builtins__, {}
                    ]
    for m in m_list:
        for bad in bad_is_list:
            Assert(m is not bad)

def test_type():
    for m in m0, m2, m1kw1:
        if isinstance(com_obj, DlrUniversalObj) or not is_cli:
            AreEqual(str(type(m)), "<type 'instancemethod'>")
        else:
            #Dev10 409943
            AreEqual(str(type(m)), "<type 'DispCallable'>")
    
def test_isinstance():
    from iptest.type_util import types
    for m in m0, m2, m1kw1:
        #Merlin 378009 and 378011
        if isinstance(com_obj, DlrUniversalObj) or not is_cli:
            Assert(isinstance(m, types.instancemethodType))
        
#------------------------------------------------------------------------------
#--MEDIUM PRIORITY TESTS

#--Method attributes
@disabled("Dev10 409928")
def test__call__():
    pass

def test__class__():
    pass
    
@skip("cli") #Merlin 370042    
def test__cmp__():
    pass
    
def test__delattr__():
    pass
    
def test__doc__():
    pass    
    
@disabled("Dev10 409928")
def test__get__():
    pass
    
def test__getattribute__():
    pass
    
def test__hash__():
    pass
    
def test__init__():
    pass
    
def test__new__():
    pass
    
def test__reduce__():
    pass
    
def test__reduce_ex__():
    pass
    
def test__repr__():
    pass

def test__setattr__():
    pass
    
def test__str__():
    pass    


#------------------------------------------------------------------------------
def test_as_obj():
    pass

#--------------------------------------
def test_attr_access():
    pass
    
def test_attr_setting():    
    pass

#--------------------------------------
def test_method_return():
    pass
    
def test_method_yield():
    pass
    
def test_method_raise():
    pass

#--------------------------------------
@skip("cli") #Merlin 370042 
def test_im_func():
    '''
    ['__call__', '__class__', '__delattr__', '__dict__', '__doc__', '__get__', 
    '__getattribute__', '__hash__', '__init__', '__module__', '__name__', 
    '__new__', '__reduce__', '__reduce_ex__', '__repr__', '__setattr__', 
    '__str__', 'func_closure', 'func_code', 'func_defaults', 'func_dict', 
    'func_doc', 'func_globals', 'func_name']
    '''
    pass

#--------------------------------------  
@skip("cli") #Merlin 370042 
def test_im_self():
    '''
    ['__class__', '__delattr__', '__dict__', '__doc__', '__getattribute__', 
    '__hash__', '__init__', '__module__', '__new__', '__reduce__', '__reduce_ex__', 
    '__repr__', '__setattr__', '__str__', '__weakref__', 'm', 'm0']
    '''
    pass
    
#--------------------------------------
@skip("cli") #Merlin 370042 
def test_im_class():
    '''
    ['__class__', '__delattr__', '__dict__', '__doc__', '__getattribute__', 
    '__hash__', '__init__', '__module__', '__new__', '__reduce__', '__reduce_ex__', 
    '__repr__', '__setattr__', '__str__', '__weakref__', 'm', 'm0']
    '''
    pass    


#---------------------------------------
def test_dot_op():
    pass
    
def test_str_op():
    pass 
    

#--Internal types for code objects    
def test_im_func_func_code():
    '''
    ['__class__', '__cmp__', '__delattr__', '__doc__', '__getattribute__', 
    '__hash__', '__init__', '__new__', '__reduce__', '__reduce_ex__', 
    '__repr__', '__setattr__', '__str__', 'co_argcount', 'co_cellvars', 
    'co_code', 'co_consts', 'co_filename', 'co_firstlineno', 'co_flags', 
    'co_freevars', 'co_lnotab', 'co_name', 'co_names', 'co_nlocals', 
    'co_stacksize', 'co_varnames']
    '''
    pass    
    
#------------------------------------------------------------------------------
#--LOWER PRIORITY TESTS
    
def test_method_simple_ops():
    '''
    com_obj.m0 + 3
    not com_obj.m0
    '''
    pass
        
    

    
def test_misc_conversions():
    '''
    conv_methods = [ int, long, float, complex,
                    str, repr, eval, tuple, list, set, dict,
                    frozenset, chr, unichr, ord, hex, oct
                    ]
    '''
    pass
    

def test_invoke_with_apply():
    pass
    
def test_compile():
    pass    

#------------------------------------------------------------------------------
run_com_test(__name__, __file__)

#Keep ourselves honest by running the test again using a normal Python class
print
print "#" * 80
print "-- Re-running test with standard Python class substitute for DlrComLibrary.DlrUniversalObj..."
com_obj = DlrUniversalObj()
m0 = com_obj.m0
m2 = com_obj.m2
m1kw1 = com_obj.m1kw1

run_test(__name__)