# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import collections
import sys
import unittest

from iptest import IronPythonTestCase, is_cli, path_modifier, run_test

glb = 0
res = ''
global_class_member = "wrong value for the global_class_member"

def nop():
    pass

class gc:
    pass

try:
    import System
    from System import GC

    gc.collect = GC.Collect
    gc.WaitForPendingFinalizers = GC.WaitForPendingFinalizers
except ImportError:
    import gc
    gc.collect = gc.collect
    gc.WaitForPendingFinalizers = nop


def FullCollect():
    gc.collect()
    gc.WaitForPendingFinalizers()

def Hello():
    global res
    res = 'Hello finalizer'

#########################################
# class implements finalizer

class Foo:
    def __del__(self):
        global res
        res = 'Foo finalizer'

#########################################
# class doesn't implement finalizer

class Bar:
    pass

######################
# Try to delete a builtin name. This should fail since "del" should not
# lookup the builtin namespace

def DoDelBuiltin():
    global pow
    del(pow)

def DoDelGlobal():
    global glb
    del glb
    return True

######################
# Try to delete a name from an enclosing function. This should fail since "del" should not
# lookup the enclosing namespace

def EnclosingFunction():
    val = 1
    def DelEnclosingName():
        del val
    DelEnclosingName()

def get_true():
    return True

def get_false():
    return False

def get_n(n):
    return n

def fooCheck():
    exec("foo = 42")
    return locals()["foo"]

selph = None

def execAdd():
    exec('a=2')
    selph.assertTrue(locals() == {'a': 2})

def execAddExisting():
    b = 5
    exec('a=2')
    selph.assertTrue(locals() == {'a': 2, 'b':5})

def execAddExistingArgs(c):
    b = 5
    exec('a=2')
    selph.assertTrue(locals() == {'a': 2, 'b': 5, 'c':7})

def execDel():
    a = 5
    exec('del(a)')
    if is_cli: # https://github.com/IronLanguages/ironpython3/issues/1029
        selph.assertEqual(locals(), {})
    else:
        selph.assertEqual(locals(), {'a': 5})

def nolocals():
    selph.assertEqual(locals(), {})

def singleLocal():
    a = True
    selph.assertEqual(locals(), {'a' : True})

def nolocalsWithArg(a):
    selph.assertEqual(locals(), {'a' : 5})

def singleLocalWithArg(b):
    a = True
    selph.assertEqual(locals(), {'a' : True, 'b' : 5})

def delSimple():
    a = 5
    selph.assertTrue(locals() == {'a' : 5})
    del(a)
    selph.assertTrue(locals() == {})

def iteratorFunc():
    for i in range(1):
        selph.assertTrue(locals() == {'i' : 0})
        yield i

def iteratorFuncLocals():
    a = 3
    for i in range(1):
        selph.assertTrue(locals() == {'a' : 3, 'i' : 0})
        yield i

def iteratorFuncWithArg(b):
    for i in range(1):
        selph.assertEqual(locals(), {'i' : 0, 'b' : 5})
        yield i

def iteratorFuncLocalsWithArg(b):
    a = 3
    for i in range(1):
        selph.assertTrue(locals() == {'a' : 3, 'i' : 0, 'b' : 5})
        yield i

def delIter():
    a = 5
    yield 2
    selph.assertTrue(locals() == {'a' : 5})
    del(a)
    yield 3
    selph.assertTrue(locals() == {})

def unassigned():
    selph.assertTrue(locals() == {})
    a = 5
    selph.assertEqual(locals(), {'a' : 5})

def unassignedIter():
    yield 1
    selph.assertTrue(locals() == {})
    yield 2
    a = 5
    yield 3
    selph.assertTrue(locals() == {'a' : 5})
    yield 4

def reassignLocalsIter():
    yield 1
    locals = 2
    yield 2
    selph.assertTrue(locals == 2)

def nested_locals():
    # Test locals in nested functions
    x = 5
    def f():
        a = 10
        selph.assertEqual(locals(),{'a' : 10})
    f()

def nested_locals2():
    # Test locals in nested functions with locals also used in outer function
    x = 5
    def f():
        a = 10
        selph.assertEqual(locals(),{'a' : 10})
    selph.assertEqual(sorted(locals().keys()), ['f', 'x'])
    f()

def modifyingLocal(a):
    selph.assertEqual(a, 10)
    a = 8
    selph.assertEqual(a, 8)
    selph.assertEqual(locals(), {'a' : 8 })

def test_namebinding_locals_and_class_impl():
    xyz = False
    class C:
        locals()["xyz"] = True
        passed = xyz

    selph.assertTrue(C.passed == True)

def localsAfterExpr():
    exec("pass")
    10
    exec("pass")

def my_locals():
    selph.fail("Calling wrong locals")

class NameBindingTest(IronPythonTestCase):
    def setUp(self):
        super(NameBindingTest, self).setUp()
        # this is a hack to get scoping requirements for testing correct
        # python treats scope differently if you are nested inside of a class
        global selph
        selph = self

    def test_nolocals(self):
        nolocals()

    def test_singlelocal(self):
        singleLocal()

    def test_nolocalsWithArg(self):
        nolocalsWithArg(5)

    def test_singleLocalWithArg(self):
        singleLocalWithArg(5)

    def test_delSimple(self):
        delSimple()

    def test_iteratorFuncs(self):
        for a in iteratorFunc(): pass
        for a in iteratorFuncLocals(): pass
        for a in iteratorFuncWithArg(5): pass
        for a in iteratorFuncLocalsWithArg(5): pass
        for a in delIter(): pass

    def test_exec(self):
        execAdd()
        execAddExisting()
        execAddExistingArgs(7)
        execDel()

    def test_reassignLocals(self):
        def reassignLocals():
            locals = 2
            self.assertTrue(locals == 2)
        reassignLocals()

    def test_unassigned(self):
        unassigned()

    def test_iter(self):
        for a in unassignedIter(): pass
        for a in reassignLocalsIter(): pass

    def test_nested_locals(self):
        nested_locals()
        nested_locals2()

    def test_namebinding_locals_and_class(self):
        """Test that namebinding uses name lookup when locals() is accessed inside a class"""
        test_namebinding_locals_and_class_impl()

    def test_modifying_local(self):
        modifyingLocal(10)

    def test_namebinding_in_functon(self):
        # name binding in a function
        def f():
            a = 2
            class c:
                exec("a = 42")
                abc = a
            return c

        self.assertEqual(f().abc, 42)

    def test_DelBuiltin(self):
        # Check that "pow" is defined
        global pow
        p = pow

        self.assertRaises(NameError, DoDelBuiltin)
        self.assertRaises(NameError, DoDelBuiltin)

    def test_SimpleTest(self):
        """simple case"""
        global res

        res = ''
        f = Foo()
        del(f)
        FullCollect()

        self.assertTrue(res == 'Foo finalizer')

    def test_00_DelUndefinedGlobal(self):
        del globals()["glb"]
        self.assertRaises(NameError, DoDelGlobal)
        self.assertRaises(NameError, DoDelGlobal)

    def test_01_DefineGlobal(self):
        globals()["glb"] = 10

    def test_02_DelDefinedGlobal(self):
        # Check that "glb" is defined
        global glb
        l = glb

        self.assertTrue(DoDelGlobal() == True)
        self.assertRaises(NameError, DoDelGlobal)
        self.assertRaises(NameError, DoDelGlobal)


    def test_PerInstOverride(self):
        """per-instance override"""
        global res
        res = ''
        def testIt():
            f = Foo()
            f.__del__ = Hello
            self.assertTrue(hasattr(Foo, '__del__'))
            self.assertTrue(hasattr(f, '__del__'))
            del(f)

        testIt()
        FullCollect()

        self.assertTrue(res == 'Foo finalizer')

    def test_PerInstOverrideAndRemove(self):
        """per-instance override & remove"""
        global res

        def testit():
            global res
            global Hello
            res = ''
            f = Foo()
            f.__del__ = Hello
            self.assertTrue(hasattr(Foo, '__del__'))

            self.assertTrue(hasattr(f, '__del__'))

            del(f.__del__)
            self.assertTrue(hasattr(Foo, '__del__'))
            self.assertTrue(hasattr(f, '__del__'))

            del(f)

        testit()
        FullCollect()
        self.assertTrue(res == 'Foo finalizer')


    def PerInstOverrideAndRemoveBoth(self):
        """per-instance override & remove both"""
        global res
        res = ''
        f = Foo()
        self.assertTrue(hasattr(Foo, '__del__'))
        self.assertTrue(hasattr(f, '__del__'))

        f.__del__ = Hello

        self.assertTrue(hasattr(Foo, '__del__'))
        self.assertTrue(hasattr(f, '__del__'))

        FullCollect()
        FullCollect()

        del(Foo.__del__)
        self.assertTrue(hasattr(Foo, '__del__') == False)
        self.assertTrue(hasattr(f, '__del__'))

        del(f.__del__)
        dir(f)

        self.assertTrue(hasattr(Foo, '__del__') == False)
        self.assertTrue(hasattr(f, '__del__') == False)

        FullCollect()
        FullCollect()

        del(f)
        FullCollect()

        self.assertTrue(res == '')


    def test_NoFinAddToInstance(self):
        """define finalizer after instance creation"""

        global res
        res = ''

        def inner():
            b = Bar()
            self.assertTrue(hasattr(Bar, '__del__') == False)

            self.assertTrue(hasattr(b, '__del__') == False)

            b.__del__ = Hello
            self.assertTrue(hasattr(Bar, '__del__') == False)
            self.assertTrue(hasattr(b, '__del__'))

            del(b)

        inner()
        FullCollect()

        self.assertTrue(res == '')


    def test_NoFinAddToInstanceAndRemove(self):
        """define & remove finalizer after instance creation"""
        global res
        res = ''
        b = Bar()
        self.assertTrue(hasattr(Bar, '__del__') == False)

        self.assertTrue(hasattr(b, '__del__') == False)

        b.__del__ = Hello
        self.assertTrue(hasattr(Bar, '__del__') == False)
        self.assertTrue(hasattr(b, '__del__'))

        del(b.__del__)
        self.assertTrue(hasattr(Bar, '__del__') == False)
        self.assertTrue(hasattr(b, '__del__') == False)

        del(b)
        FullCollect()

        self.assertTrue(res == '')

    def _test_undefined(self, function, *args):
        try:
            function(*args)
        except NameError as n:
            self.assertTrue("'undefined'" in str(n), "%s: expected undefined variable exception, but different NameError caught: %s" % (function.__name__, n))
        else:
            self.fail("%s: expected undefined variable exception, but no exception caught" % function.__name__)

    def _test_unassigned(self, function, *args):
        try:
            function(*args)
        except UnboundLocalError as n:
            pass
        else:
            self.fail("%s: expected unassigned variable exception, but no exception caught" % function.__name__)

    def _test_attribute_error(function, *args):
        try:
            function(*args)
        except AttributeError:
            pass
        else:
            self.fail("%s: expected AttributeError, but no exception caught" % function.__name__)

    def test_undefined_local(self):
        """straightforward undefined local"""
        def test():
            x = undefined
            undefined = 1           # create local binding

        self._test_undefined(test)

    def test_undefined_multiple_assign(self):
        """longer assignment statement"""
        def test():
            x = y = z = undefined
            undefined = 1           # create local binding

        self._test_undefined(test)

    def test_udefined_aug(self):
        """aug assignment"""
        def test():
            undefined += 1          # binds already

        self._test_undefined(test)

    def test_undefined_assigned_to_self(self):
        """assigned to self"""
        def test():
            undefined = undefined

        self._test_undefined(test)

    def test_explicit_deletion(self):
        """explicit deletion"""
        def test():
            del undefined

        self._test_undefined(test)

    def test_if_statement(self):
        """if statement"""
        def test():
            if get_false():
                undefined = 1       # unreachable
            x = undefined

        self._test_undefined(test)

    def test_if_statement_2(self):
        """if statement"""
        def test():
            if get_false():
                undefined = 1
            else:
                x = 1
            x = undefined

        self._test_undefined(test)

    def test_if_statement_3(self):
        """if statement"""
        def test():
            if get_true():
                pass
            else:
                undefined = 1
            x = undefined

        self._test_undefined(test)

    def test_nested_if_statement(self):
        """nested if statements"""
        def test():
            if get_false():
                if get_true():
                    undefined = 1
                else:
                    undefined = 1
            x = undefined

        self._test_undefined(test)

    def test_if_elif_elif_elif(self):
        """if elif elif elif"""
        def test():
            n = get_n(10)
            if n == 1:
                undefined = n
            elif n == 2:
                undefined = n
            elif n == 3:
                undefined = n
            elif n == 4:
                undefined = n
            elif n == 5:
                undefined = n
            else:
                pass
            n = undefined

        self._test_undefined(test)

    def test_for_loop(self):
        """for"""
        def test():
            for i in range(get_n(0)):
                undefined = i
            x = undefined

        self._test_undefined(test)

    def test_for_loop_2(self):
        """more for with else that doesn't always bind"""
        def test():
            for i in range(get_n(0)):
                undefined = i
            else:
                if get_false():
                    undefined = 1
                elif get_false():
                    undefined = 1
                else:
                    pass
            x = undefined

        self._test_undefined(test)

    def test_for_with_break(self):
        """for with break"""
        def test():
            for i in range(get_n(10)):
                break
                undefined = 10
            x = undefined

        self._test_undefined(test)

    def test_for_with_break_and_else(self):
        """for with break and else"""
        def test():
            for i in range(get_n(10)):
                break
                undefined = 10
            else:
                undefined = 20
            x = undefined

        self._test_undefined(test)

    def test_for_with_break_and_else_2(self):
        """for with break and else"""
        def test():
            for i in range(get_n(10)):
                if get_true():
                    break
                undefined = 10
            else:
                undefined = 20
            x = undefined

        self._test_undefined(test)

    def test_for_with_break_and_else_conditional_init(self):
        """for with break and else and conditional initialization"""
        def test():
            for i in range(get_n(10)):
                if get_false():
                    undefined = 10
                if get_true():
                    break
                undefined = 10
            else:
                undefined = 20
            x = undefined

        self._test_undefined(test)

    def test_deep_delete(self):
        """delete somewhere deep"""
        def test():
            for i in range(get_n(10)):
                undefined = 10
                if get_false():
                    del undefined
                if get_true():
                    del undefined
                if get_true():
                    break
                undefined = 10
            else:
                undefined = 20
            x = undefined

        self._test_undefined(test)

    def test_bound_by_for(self):
        # bound by for
        def test():
            for undefined in []:
                pass
            print(undefined)

        self._test_undefined(test)

    def test_more_binding_constructs(self):
        """more binding constructs"""
        def test():
            try:
                1/0
                undefined = 1
            except:
                pass
            x = undefined

        self._test_undefined(test)

        def test():
            try:
                pass
            except Error as undefined:
                pass
            x = undefined

        self._test_undefined(test)

    def test_bound_by_import(self):
        """bound by import statement"""
        def test():
            x = undefined
            import undefined

        self._test_undefined(test)

    def test_bound_by_import_2(self):
        """same here"""
        def test():
            x = undefined
            import defined as undefined

        self._test_undefined(test)

    def test_del_var_1(self):
        """del"""
        def test():
            undefined = 1
            del undefined
            x = undefined

        self._test_undefined(test)

    def test_conditional_del(self):
        """conditional del"""
        def test():
            undefined = 10
            if get_true():
                del undefined
            else:
                undefined = 1
            x = undefined

        self._test_undefined(test)

    def test_del_in_loop(self):
        """del in the loop"""

        def test():
            undefined = 10
            for i in [1]:
                if i == get_n(1):
                    del undefined
            x = undefined

        self._test_undefined(test)

    def test_del_in_loop_in_condition(self):
        """del in the loop in condition"""

        def test():
            undefined = 10
            for i in [1,2,3]:
                if i == get_n(1):
                    continue
                elif i == get_n(2):
                    del undefined
                else:
                    break
            x = undefined

        self._test_undefined(test)

    def test_params_del(self):
        def test(undefined):
            self.assertEqual(undefined, 1)
            del undefined
            x = undefined

        self._test_undefined(test, 1)

    def test_params_del_2(self):
        def test(undefined):
            self.assertEqual(undefined, 1)
            x = undefined
            self.assertEqual(x, 1)
            del undefined
            y = x
            self.assertEqual(y, 1)
            x = undefined

        self._test_undefined(test, 1)

    def test_params_del_3(self):
        def test(a):
            if get_false(): return
            x = a
            undefined = a
            del undefined
            x = undefined

        self._test_undefined(test, 1)

    def test_default_params_unassigned(self):
        def test():
            if get_false():
                x = 1

            def f(a = x):
                locals()

        self._test_unassigned(test)

    def test_class_base(self):
        def test():
            if get_false():
                x = dict

            class C(x):
                locals()

        self._test_unassigned(test)

    def test_conditional_member_def(self):
        def test():
            class C(object):
                if get_false():
                    x = 1

            C().x

        self._test_attribute_error(test)

    def test_member_access_on_undefined(self):
        def test():
            undefined.member = 1
            locals()

        self._test_undefined(test)

    def test_item_access_on_undefined(self):
        def test():
            undefined[1] = 1
            locals()

        self._test_undefined(test)

    def test_item_access_on_undefined_in_tuple(self):
        def test():
            (undefined[1],x) = 1,2
            locals()

        self._test_undefined(test)

    def test_item_access_on_undefined_in_list(self):
        def test():
            [undefined[1],x] = 1,2
            locals()

        self._test_undefined(test)

    def test_item_index_undefined(self):
        def test():
            dict()[undefined] = 1
            locals()

        self._test_undefined(test)

    def test_nested_scope_variable_access(self):
        def test():
            def g():
                def f():
                    x = undefined
                return f
            g()()
            undefined = 1

        self._test_undefined(test)

    def test_del_local_var(self):
        result = "Failed"

        a = 2
        b = a
        del a


        try:
            b = a
        except NameError:
            result = "Success"

        self.assertTrue(result == "Success")

    def test_del_class_attr(self):
        class C:
            pass

        c = C()
        c.a = 10
        self.assertTrue("a" in dir(c))
        del c.a
        self.assertTrue(not "a" in dir(c))
        C.a = 10
        self.assertTrue("a" in dir(C))
        del C.a
        self.assertTrue(not "a" in dir(C))

    def test_dir_list(self):
        for m in dir([]):
            c = callable(m)
            a = getattr([], m)

    def test_del_undefined(self):
        success = False
        try:
            del this_name_is_undefined
        except NameError:
            success = True
        self.assertTrue(success)

    def test_delete_builting_func(self):
        ## delete builtin func
        import builtins

        try:
            del pow
            self.fail("Unreachable code reached: should have thrown")
        except NameError: pass

    def test_class_var(self):
        class C(object):
            name = None
            def test(self):
                print(name)

        self.assertRaises(NameError, C().test)

    def test_delete_from_builtins(self):
        import builtins
        from importlib import reload
        p = builtins.pow
        try:
            del builtins.pow
            self.assertRaises(NameError, lambda: pow)
            self.assertRaises(AttributeError, lambda: builtins.pow)
        finally:
            reload(builtins)
            # check if we still have access to builtins after reloading - see https://github.com/IronLanguages/ironpython3/issues/1028
            if sys.version_info >= (3,5):
                self.assertRaises(NameError, lambda: pow)
            else:
                self.assertEqual(pow(2,2), 4) # bug 359890
            if sys.version_info >= (3,5):
                self.assertRaises(AttributeError, lambda: builtins.pow)
            else:
                self.assertEqual(builtins.pow(2,2), 4)
            builtins.pow = p
            self.assertEqual(pow(2,2), 4)
            self.assertEqual(builtins.pow(2,2), 4)
            dir('abc')

    def test_override_builtin_method(self):
        """Overriding builtins method inconsistent with -X:LightweightScopes flag"""
        import builtins
        builtins.help = 10
        self.assertRaisesPartialMessage(TypeError, "is not callable", lambda: help(dir))

    def test_runtime_name_lookup_class_scopes(self):
        # Test that run time name lookup skips over class scopes
        # (because class variables aren't implicitly accessible inside member functions)
        a = 123
        class C(object):
            a = 456
            def foo(self):
                # this exec statement is here to cause the "a" in the next statement
                # to have its name looked up at run time.
                exec("dummy = 10", {}, locals())
                return a  # a should bind to the global a, not C.a

        self.assertEqual(C().foo(), 123)

    def test_cp20956(self):
        class C:
            codeplex_20956 = 3
            def foo(self):
                return codeplex_20956

        self.assertRaisesMessage(NameError, "name 'codeplex_20956' is not defined",
                            C().foo)

    def test_import_as(self):
        class ImportAsInClass:
            import sys as sys_in_class

        class ImportInClass:
            import sys

        self.assertTrue(ImportAsInClass.sys_in_class is ImportInClass.sys)

        class FromImportAsInClass:
            from sys import path as sys_path

        class FromImportInClass:
            from sys import path

        self.assertTrue(FromImportAsInClass.sys_path is FromImportInClass.path)


    def test_global_and_class_member(self):
        class ClassWithGlobalMember:
            global global_class_member
            global_class_member = "global class member value"

        self.assertTrue(not hasattr(ClassWithGlobalMember, "global_class_member"), "invalid binding of global in a class")
        self.assertEqual(global_class_member, "global class member value")


    def test_locals_copy(self):
        def f():
            abc = eval("locals()")
            abc['foo'] = 42
            print(foo)

        self.assertRaises(NameError, f)


        def f():
            x = locals()
            x['foo'] = 42
            print(foo)

        self.assertRaises(NameError, f)

        def f():
            x = vars()
            x['foo'] = 42
            print(foo)

        self.assertRaises(NameError, f)

        def f():
            import os
            fname = 'temptest_%d.py' % os.getpid()
            with open(fname, 'w+') as f:
                f.write('foo = 42')

            try:
                execfile(fname)
            finally:
                os.unlink(fname)
            return foo

        self.assertRaises(NameError, f)


        self.assertEqual(fooCheck(), 42)

        def f():
            import os
            module_name = 'temptest_%d' % os.getpid()
            fname = module_name + '.py'
            with open(fname, 'w+') as f:
                f.write('foo = 42')
            try:
                with path_modifier('.'):
                    foo = __import__(module_name).foo
            finally:
                os.unlink(fname)
            return foo

        self.assertEqual(f(), 42)


        def f():
            exec("foo = 42", {})
            return foo

        self.assertRaises(NameError, f)

        def F():
            a = 4
            class C:
                field=7
                def G(self):
                    b = a
                    b = 4
                    return locals()
            c = C()
            return c.G()

        self.assertEqual(set(F()), set(['self', 'b', 'a']))

        def f():
            a = 10
            def g1():
                return a, locals()
            def g2():
                return locals()
            res = [g1()]
            res.append(g2())
            return res

        self.assertEqual(f(), [(10, {'a':10}), {}])

        X = (42, 43)
        class Y:
            def outer_f(self):
                def f(z=X):
                    (a, b) = z
                    return a, b
                return f


        a = Y()
        self.assertEqual(a.outer_f()(), (42, 43))

run_test(__name__)

if __name__ == '__main__':
    selph.assertTrue('__builtins__' in locals()) # __builtins__ is an implementation detail of CPython
    a = 5
    selph.assertTrue('a' in locals())

    exec('a = a+1')

    selph.assertTrue(locals()['a'] == 6)

    exec("pass")
    locals = my_locals
    exec("pass")
    import builtins
    save_locals = builtins.locals
    try:
        builtins.locals = my_locals
        exec("pass")
    finally:
        builtins.locals = save_locals

    localsAfterExpr()
    del locals

    # verify builtins is accessed if a global isn't defined
    xyz = 'aaa'
    selph.assertEqual(xyz, 'aaa')

    import builtins
    builtins.xyz = 'abc'

    xyz = 'def'
    selph.assertEqual(xyz, 'def')
    del xyz
    selph.assertEqual(xyz, 'abc')

    del builtins.xyz
    try:
        a = xyz
    except NameError: pass
