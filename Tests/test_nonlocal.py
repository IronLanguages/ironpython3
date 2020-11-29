# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import sys
import unittest

from iptest import IronPythonTestCase, run_test, is_cli, is_cpython


class SyntaxTests(IronPythonTestCase):

    def check_compile_error(self, code, msg, lineno):
        with self.assertRaises(SyntaxError) as cm:
            compile(code, "<testcase>", "exec")
        self.assertEqual(cm.exception.msg, msg)
        self.assertEqual(cm.exception.lineno, lineno)

    def test_no_binding_func(self):
        source = """if True:
            def foo():
                nonlocal x
                return x
            f()
            """
        self.check_compile_error(source, "no binding for nonlocal 'x' found", 3)

    def test_no_binding_func_global(self):
        source = """if True:
            x = 1
            def foo():
                nonlocal x
                return x
            f()
            """
        self.check_compile_error(source, "no binding for nonlocal 'x' found", 4)

    def test_no_binding_class(self):
        source = """if True:
            class Foo():
                x = 1
                class Bar():
                    nonlocal x
                    def f(self):
                        return x
            """
        self.check_compile_error(source, "no binding for nonlocal 'x' found", 5)

    def test_global_nonlocal(self):
        source = """if True:
            def foo():
                global x    # CPython's error location
                nonlocal x  # IronPython's error location
                return x
            f()
            """
        self.check_compile_error(source, "name 'x' is nonlocal and global", 4 if is_cli else 3)

    def test_nonlocal_global(self):
        source = """if True:
            def foo():
                nonlocal x  # CPython's error location
                global x    # IronPython's error location
                return x
            f()
            """
        self.check_compile_error(source, "name 'x' is nonlocal and global", 4 if is_cli else 3)

    def test_missing_nonlocal(self):
        source = """if True:
            def foo(x):
                def bar():
                    nonlocal y
                    return x
                return bar()
            f(1)
            """
        self.check_compile_error(source, "no binding for nonlocal 'y' found", 4)

    @unittest.skipIf(is_cpython and sys.version_info <= (3,6), "CPython 3.4, 3.5 issues SyntaxWarning for this case")
    def test_prior_assignment(self):
        source = """if True:
            def foo(x):
                def bar():
                    x = 0
                    nonlocal x
                    return x
                return bar()
            f(1)
            """
        self.check_compile_error(source, "name 'x' is assigned to before nonlocal declaration", 5)

    @unittest.skipIf(is_cpython and sys.version_info <= (3,6), "CPython 3.4, 3.5 issues SyntaxWarning for this case")
    def test_prior_use(self):
        source = """if True:
            def foo(x):
                def bar():
                    y = x
                    nonlocal x
                    return x
                return bar()
            f(1)
            """
        self.check_compile_error(source, "name 'x' is used prior to nonlocal declaration", 5)

    @unittest.skipIf(is_cpython and sys.version_info <= (3,6), "CPython 3.4, 3.5 issues SyntaxWarning for this case")
    def test_prior_assignment_and_use(self):
        source = """if True:
            def foo(x):
                def bar():
                    class x: pass  # assignment
                    x = 0          # assignment
                    x += 1         # use
                    nonlocal x
                    return x
                return bar()
            f(1)
            """
        self.check_compile_error(source, "name 'x' is assigned to before nonlocal declaration", 7)

    @unittest.skipIf(is_cpython and sys.version_info <= (3,6), "CPython 3.4, 3.5 issues SyntaxWarning for this case")
    def test_prior_use_and_assignment(self):
        source = """if True:
            def foo(x):
                def bar():
                    x += 1         # use
                    class x: pass  # assignment
                    x = 0          # assignment
                    nonlocal x
                    return x
                return bar()
            f(1)
            """
        self.check_compile_error(source, "name 'x' is assigned to before nonlocal declaration", 7)

    @unittest.skipIf(is_cpython and sys.version_info <= (3,6), "CPython 3.4, 3.5 issues SyntaxWarning for this case")
    def test_prior_call_and_assignment(self):
        source = """if True:
            def foo(x):
                def bar():
                    x()            # use
                    class x: pass  # assignment
                    x = 0          # assignment
                    nonlocal x
                    return x
                return bar()
            f(int)
            """
        if is_cli:
            self.check_compile_error(source, "name 'x' is assigned to before nonlocal declaration", 7)
        else:
            self.check_compile_error(source, "name 'x' is used prior to nonlocal declaration", 7)

class FunctionalTests(IronPythonTestCase):

    def test_local_scope_nonlocal(self):
        # Test that nonlocal declarations are limited to a local scope (do not propagate to inner scopes)
        def foo():
            x = 1 # local in foo
            def bar():
                nonlocal x # from foo
                x = 2
                self.assertEqual(x, 2)
                def gek():
                    # x is a readable reference to foo.<locals>.x
                    self.assertEqual(x, 2)
                    def lee():
                        nonlocal x # from foo
                        x = 3 # modifies foo.<locals>.x
                        self.assertEqual(x, 3)
                        def kao():
                            x = 4 # local in kao
                            self.assertEqual(x, 4)
                        kao()
                        self.assertEqual(x, 3) # not changed by kao
                    lee()
                    self.assertEqual(x, 3) # changed by lee
                gek()
                self.assertEqual(x, 3) # changed by lee
            bar()
            self.assertEqual(x, 3) # changed by lee
        foo()

    def test_nonlocal_del(self):
        # Test that a nonlocal does not rebind to an unshadowed variable after del
        def foo():
            x1, x2 = 'foo:x1', 'foo:x2' # local in foo
            x3 = 'foo:x3'
            def bar():
                with self.assertRaises(UnboundLocalError) as cm:
                    del x3 # x3 becomes local in bar but unassigned
                self.assertEqual(cm.exception.args[0], "local variable 'x3' referenced before assignment")

                with self.assertRaises(NameError) as cm:
                    dummy = x4 # x4 is local in foo but unassigned
                self.assertEqual(cm.exception.args[0], "free variable 'x4' referenced before assignment in enclosing scope")

                x1, x2 = 'bar:x1', 'bar:x2' # local in bar, shadowing foo

                def gek():
                    nonlocal x1, x2, x3 # from bar
                    self.assertEqual(x1, 'bar:x1')
                    self.assertEqual(x2, 'bar:x2')

                    x1, x2 = 'gek:x1', 'gek:x2' # reassigned locals in bar
                    del x1 # deletes a local in bar
                    with self.assertRaises(NameError) as cm:
                        del x1 # x1 in bar is already deleted
                    self.assertEqual(cm.exception.args[0], "free variable 'x1' referenced before assignment in enclosing scope")

                    del x2 # deletes a local in bar
                    x2 = 'gek:x2+' # reassigns a variable in bar, bringing it back to life

                    with self.assertRaises(NameError) as cm:
                        dummy = x3 # x3 in bar is not yet assigned
                    self.assertEqual(cm.exception.args[0], "free variable 'x3' referenced before assignment in enclosing scope")
                gek()

                x3 = 'bar:x3' # finally x3 is assigned and declared local in bar

                with self.assertRaises(UnboundLocalError) as cm:
                    dummy = x1 # x1 is already deleted by gek
                self.assertEqual(cm.exception.args[0], "local variable 'x1' referenced before assignment")
                with self.assertRaises(UnboundLocalError) as cm:
                    del x1 # x1 is already deleted by gek
                self.assertEqual(cm.exception.args[0], "local variable 'x1' referenced before assignment")

                self.assertEqual(x2, 'gek:x2+') # killed and resurrected by gek
            bar()

            self.assertEqual(x1, 'foo:x1') # unchanged
            self.assertEqual(x2, 'foo:x2') # unchanged
            self.assertEqual(x3, 'foo:x3') # unchanged
            x4 = 'foo:x4' # made local in foo
        foo()

    def test_class_scope(self):
        x = 'func'
        class Foo():
            x = 'class'
            class Bar():
                nonlocal x
                def f(self):
                    return x
            def get_bar(self):
                return self.Bar()

        self.assertEqual(Foo.Bar().f(), 'func')
        self.assertEqual(Foo().get_bar().f(), 'func')

    def test_nonlocal_class_del(self):
        def foo():
            class Bar():
                def del_bar(self):
                    nonlocal Bar
                    del Bar
                def get_bar(self):
                    return Bar()
            bar = Bar()
            return bar.get_bar, bar.del_bar

        get_bar, del_bar = foo()
        # str(get_bar()) produces something like
        # <__main__.FunctionalTests.test_nonlocal_class_del.<locals>.foo.<locals>.Bar object at 0x000002426EEEE908>
        self.assertEqual(str(get_bar())[1:].split()[0].split('.')[-1], 'Bar') # get_bar() works
        self.assertEqual(del_bar(), None) # delete class Bar
        with self.assertRaises(NameError) as cm:
            get_bar() # cannot instantiate a nonexistent class
        self.assertEqual(cm.exception.args[0], "free variable 'Bar' referenced before assignment in enclosing scope")

    @unittest.skipIf(is_cli, "https://github.com/IronLanguages/ironpython3/issues/30")
    def test_nonlocal_names(self):
        def foo():
            x = 'foo:x' # local in foo
            def bar():
                nonlocal x # from foo
                class x(): # reassigns foo.<locals>.x to bar.<locals>.x
                    def f(self):
                        return x()
                self.assertRegex(str(x().f()), 
                    r"^<%s\.FunctionalTests\.test_nonlocal_names\.<locals>\.foo.<locals>.bar.<locals>.x object at 0x[0-9A-F]+>$" % __name__)
            bar()
            self.assertEqual(str(x), 
                "<class '%s.FunctionalTests.test_nonlocal_names.<locals>.foo.<locals>.bar.<locals>.x'>" % __name__)
            bar_x = x
            def gek():
                nonlocal x, x # from foo
                x = 'gek:x' # reassigns foo.<locals>.x to a local string
            gek()
            self.assertEqual(str(x), 'gek:x') # reasigned by gek
            self.assertEqual(str(bar_x), # maintains bar.<locals>.x
                "<class '%s.FunctionalTests.test_nonlocal_names.<locals>.foo.<locals>.bar.<locals>.x'>" % __name__)
            # bar_x.f sees x from foo, not class x from bar
            self.assertIsInstance(bar_x(), object)
            self.assertRaises(TypeError, bar_x().f)
        foo()

    def test_nonlocal_import(self):
        maxunicode = 0
        some_number = 0
        def foo():
            nonlocal maxunicode, some_number
            from sys import maxunicode
            from sys import maxsize as some_number
        foo()
        self.assertEqual(maxunicode, 1114111)
        self.assertGreaterEqual(some_number, 0x7FFFFFFF)

run_test(__name__)