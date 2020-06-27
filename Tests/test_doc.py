# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

"module doc"

import unittest

from iptest import IronPythonTestCase, is_cli, path_modifier, retryOnFailure, run_test, skipUnlessIronPython

class DocTest(IronPythonTestCase):

    def test_sanity(self):
        ## module
        global __doc__

        self.assertEqual(__doc__, "module doc")
        __doc__ = "new module doc"
        self.assertEqual(__doc__, "new module doc")

        ## builtin
        self.assertTrue(min.__doc__ is not None)

        self.assertEqual(abs.__doc__, "abs(number) -> number\n\nReturn the absolute value of the argument.")
        self.assertEqual(int.__add__.__doc__, "x.__add__(y) <==> x+y" if is_cli else "Return self+value.")

    def test_func_meth_class(self):
        def f_1():
            "f 1 doc"
            return __doc__

        def f_2():
            __doc__ = "f 2 doc"
            return __doc__

        def f_3():
            "f 3 doc"
            __doc__ = "new f 3 doc"
            return __doc__

        def f_4():
            return __doc__

        class c_1:
            "c 1 doc"
            self.assertEqual(__doc__, "c 1 doc")

        class c_2:
            "c 2 doc"
            self.assertEqual(__doc__, "c 2 doc")

        class c_3:
            "c 3 doc"
            self.assertEqual(__doc__, "c 3 doc")
            __doc__ = "c 3 doc 2"
            self.assertEqual(__doc__, "c 3 doc 2")

        class c_4:
            __doc__ = "c 4 doc"
            self.assertEqual(__doc__, "c 4 doc")

        class n_1(object):
            "n 1 doc"
            self.assertEqual(__doc__, "n 1 doc")

        class n_2(object):
            "n 2 doc"
            self.assertEqual(__doc__, "n 2 doc")

        class n_3(object):
            "n 3 doc"
            self.assertEqual(__doc__, "n 3 doc")
            __doc__ = "n 3 doc 2"
            self.assertEqual(__doc__, "n 3 doc 2")

        class n_4(object):
            __doc__ = "n 4 doc"
            self.assertEqual(__doc__, "n 4 doc")

        class d:
            "d doc 1"
            self.assertEqual(__doc__, "d doc 1")

            def m_1(self):
                "m 1 doc"
                return __doc__

            self.assertEqual(m_1.__doc__, "m 1 doc")
            self.assertEqual(__doc__, "d doc 1")
            __doc__ = "d doc 2"
            self.assertEqual(__doc__, "d doc 2")
            self.assertEqual(m_1.__doc__, "m 1 doc")

            def m_2(self):
                __doc__ = "m 2 doc"
                return __doc__

            self.assertEqual(m_2.__doc__, None)
            self.assertEqual(__doc__, "d doc 2")
            __doc__ = "d doc 3"
            self.assertEqual(__doc__, "d doc 3")
            self.assertEqual(m_2.__doc__, None)

            def m_3(self):
                "m 3 doc"
                __doc__ = "new m 3 doc"
                return __doc__

            self.assertEqual(m_3.__doc__, "m 3 doc")
            self.assertEqual(__doc__, "d doc 3")
            __doc__ = "d doc 4"
            self.assertEqual(__doc__, "d doc 4")
            self.assertEqual(m_3.__doc__, "m 3 doc")

            def m_4(self):
                return __doc__

            self.assertEqual(m_4.__doc__, None)
            self.assertEqual(__doc__, "d doc 4")
            __doc__ = "d doc 5"
            self.assertEqual(__doc__, "d doc 5")
            self.assertEqual(m_4.__doc__, None)

        self.assertEqual(f_1.__doc__, "f 1 doc")
        self.assertEqual(f_2.__doc__, None)
        self.assertEqual(f_3.__doc__, "f 3 doc")
        self.assertEqual(f_4.__doc__, None)

        self.assertEqual(c_1.__doc__, "c 1 doc")
        self.assertEqual(c_2.__doc__, "c 2 doc")
        self.assertEqual(c_3.__doc__, "c 3 doc 2")
        self.assertEqual(c_4.__doc__, "c 4 doc")

        self.assertEqual(n_1.__doc__, "n 1 doc")
        self.assertEqual(n_2.__doc__, "n 2 doc")
        self.assertEqual(n_3.__doc__, "n 3 doc 2")
        self.assertEqual(n_4.__doc__, "n 4 doc")

        self.assertEqual(d.__doc__, "d doc 5")
        self.assertEqual(d.m_1.__doc__, "m 1 doc")
        self.assertEqual(d.m_2.__doc__, None)
        self.assertEqual(d.m_3.__doc__, "m 3 doc")
        self.assertEqual(d.m_4.__doc__, None)

        dd = d()
        for x in (f_1, f_2, f_3, f_4,
                    c_1, c_2, c_3, c_4,
                    n_1, n_2, n_3, n_4,
                    dd.m_1, dd.m_2, dd.m_3, dd.m_4):
            x()

    @skipUnlessIronPython()
    def test_clr_doc(self):
        import System
        self.assertTrue(System.Collections.Generic.List.__doc__.find("List[T]()") != -1)
        self.assertTrue(System.Collections.Generic.List.__doc__.find("collection: IEnumerable[T]") != -1)

        # static TryParse(str s) -> (bool, float)
        self.assertTrue(System.Double.TryParse.__doc__.index('(bool, float)') >= 0)
        self.assertTrue(System.Double.TryParse.__doc__.index('(s: str)') >= 0)

    def test_none(self):
        self.assertEqual(None.__doc__, None)

    def test_types(self):
        self.assertEqual(Ellipsis.__doc__, None)
        self.assertEqual(NotImplemented.__doc__, None)

    def test_builtin_nones(self):
        for x in [Ellipsis, None, NotImplemented, ]:
            self.assertTrue(x.__doc__==None, str(x) + ".__doc__ != None")

    def test_builtin_nones_cpy_site(self):
        for x in [exit, quit, ]:
            self.assertTrue(x.__doc__ is None, str(x) + ".__doc__ != None")

    def test_class_doc(self):
        # sanity, you can assign to __doc__
        class x(object): pass

        class y(object):
            __slots__ = '__doc__'

        class z(object):
            __slots__ = '__dict__'

        for t in (x, y, z):
            a = t()
            a.__doc__ = 'Hello World'
            self.assertEqual(a.__doc__, 'Hello World')

        class x(object): __slots__ = []

        def f(a): a.__doc__ = 'abc'
        self.assertRaises(AttributeError, f, x())
        self.assertRaises(AttributeError, f, object())

    def test_exception_doc_cp20251(self):
        class KExcept(Exception):
            pass

        for e in [Exception(), KExcept(), Exception, KExcept,
                BaseException(), IOError(), BaseException, IOError]:
            self.assertTrue(hasattr(e, "__doc__"))
            e.__doc__

    @unittest.skip("intermittent failure during CI") # https://github.com/IronLanguages/ironpython3/issues/887
    def test_module_doc_cp21360(self):
        temp_filename_empty = "cp21360_empty.py"
        temp_filename = "cp21360.py"
        try:
            with path_modifier('.'):
                self.write_to_file(temp_filename_empty, "")
                import cp21360_empty
                self.assertEqual(cp21360_empty.__doc__, None)

            with path_modifier('.'):
                self.write_to_file(temp_filename, "x = 3.14")
                import cp21360
                self.assertEqual(cp21360.__doc__, None)
                self.assertEqual(cp21360.x, 3.14)

        finally:
            self.delete_files(temp_filename, temp_filename_empty)

run_test(__name__)
