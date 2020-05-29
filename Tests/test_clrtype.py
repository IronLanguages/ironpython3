# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import unittest

from iptest import IronPythonTestCase, is_cli, is_netcoreapp21, is_posix, run_test, skipUnlessIronPython

if is_cli:
    import clr
    import clrtype

    clr.AddReference("System.Xml")

    import System
    from System.Reflection import BindingFlags

    class IProduct(object, metaclass=clrtype.ClrInterface):
        __metaclass__ = clrtype.ClrInterface # https://github.com/IronLanguages/ironpython3/issues/836

        _clrnamespace = "IronPython.Samples.ClrType"

        @property
        @clrtype.accepts()
        @clrtype.returns(str)
        def Name(self): raise RuntimeError("this should not get called")

        @property
        @clrtype.accepts()
        @clrtype.returns(float)
        def Cost(self): raise RuntimeError("this should not get called")

        @clrtype.accepts()
        @clrtype.returns(bool)
        def IsAvailable(self): raise RuntimeError("this should not get called")

    class Product(IProduct, metaclass=clrtype.ClrClass):
        __metaclass__ = clrtype.ClrClass # https://github.com/IronLanguages/ironpython3/issues/836

        _clrnamespace = "IronPython.Samples.ClrType"

        _clrfields = {
            "name" : str,
            "cost" : float,
            "_quantity" : int
        }

        CLSCompliant = clrtype.attribute(System.CLSCompliantAttribute)
        clr.AddReference("System.Xml")
        XmlRoot = clrtype.attribute(System.Xml.Serialization.XmlRootAttribute)

        _clrclassattribs = [
            # Use System.Attribute subtype directly for custom attributes without arguments
            System.ObsoleteAttribute,
            # Use clrtype.attribute for custom attributes with arguments (either positional, named, or both)
            CLSCompliant(False),
            XmlRoot("product", Namespace="www.contoso.com")
        ]

        def __init__(self, name, cost, quantity):
            self.name = name
            self.cost = cost
            self._quantity = quantity

        # IProduct methods
        def Name(self): return self.name
        def Cost(self): return self.cost
        def IsAvailable(self): return self.quantity != 0

        @property
        @clrtype.accepts()
        @clrtype.returns(int)
        def quantity(self): return self._quantity

        @quantity.setter
        @clrtype.accepts(int)
        @clrtype.returns()
        def quantity(self, value): self._quantity = value

        @clrtype.accepts(float)
        @clrtype.returns(float)
        def calc_total(self, discount=0.0):
            return (self.cost - discount) * self.quantity

    if not is_netcoreapp21 and not is_posix:
        class NativeMethods(object, metaclass=clrtype.ClrClass):
            # Note that you could also the "ctypes" modules instead of pinvoke declarations
            __metaclass__ = clrtype.ClrClass # https://github.com/IronLanguages/ironpython3/issues/836

            from System.Runtime.InteropServices import DllImportAttribute, PreserveSigAttribute
            DllImport = clrtype.attribute(DllImportAttribute)
            PreserveSig = clrtype.attribute(PreserveSigAttribute)

            @staticmethod
            @DllImport("user32.dll")
            @PreserveSig()
            @clrtype.accepts(System.Char)
            @clrtype.returns(System.Boolean)
            def IsCharAlpha(c): raise RuntimeError("this should not get called")

            @staticmethod
            @DllImport("user32.dll")
            @PreserveSig()
            @clrtype.accepts(System.IntPtr, System.String, System.String, System.UInt32)
            @clrtype.returns(System.Int32)
            def MessageBox(hwnd, text, caption, type): raise RuntimeError("this should not get called")

@skipUnlessIronPython()
class ClrTypeTest(IronPythonTestCase):
    def setUp(self):
        super(ClrTypeTest, self).setUp()
        self.p = Product("Widget", 10.0, 42)

    @unittest.skipIf(is_netcoreapp21, 'No DefinePInvokeMethod')
    @unittest.skipIf(is_posix, 'Windows specific P/Invoke')
    def test_pinvoke_method(self):
        self.assertTrue(NativeMethods.IsCharAlpha('A'))
        # Call statically-typed method from another .NET language (simulated using Reflection)
        isCharAlpha = clr.GetClrType(NativeMethods).GetMethod('IsCharAlpha')
        args = System.Array[object](('1'.Chars[0],))
        self.assertFalse(isCharAlpha.Invoke(None, args))

    def test_classattributes(self):
        t = clr.GetClrType(self.p.GetType())
        oa = t.GetCustomAttributes(System.ObsoleteAttribute, True)[0]
        clsc = t.GetCustomAttributes(System.CLSCompliantAttribute, True)[0]
        xmlr = t.GetCustomAttributes(System.Xml.Serialization.XmlRootAttribute, True)[0]

        self.assertEqual(oa.Message, None)
        self.assertFalse(clsc.IsCompliant)
        self.assertEqual(xmlr.ElementName, 'product')
        self.assertEqual(xmlr.Namespace, 'www.contoso.com')

    def test_clrtypeinfo(self):
        t = self.p.GetType()
        self.assertEqual(t.FullName, 'IronPython.Samples.ClrType.Product')

        # fields and properties
        bf = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy
        f = [field.Name for field in t.GetFields(bf)]
        self.assertTrue('name' in f)
        self.assertTrue('cost' in f)
        self.assertTrue('_quantity' in f)

        p = [property.Name for property in t.GetProperties(bf)]
        self.assertTrue('quantity' in p)

        # methods
        m = [method.Name for method in t.GetMethods() if method.DeclaringType == method.ReflectedType]
        self.assertTrue('calc_total' in m)
        self.assertTrue('quantity' in m)

    def test_interface_members(self):
        # Calling IProduct.Name and IProduct.IsAvailable as a strongly-typed members
        name = clr.GetClrType(IProduct).GetProperty("Name").GetGetMethod()
        self.assertEqual(name.Invoke(self.p, None), 'Widget')
        isAvailable = clr.GetClrType(IProduct).GetMethod("IsAvailable")
        self.assertTrue(isAvailable.Invoke(self.p, None))

    def _call_typed_method(self):
        """Calling calc_total as a strongly-typed method using Reflection"""
        calc_total = self.p.GetType().GetMethod("calc_total")
        args = System.Array[object]( (1.0,) )
        res = calc_total.Invoke(self.p, args)
        return res

    # these methods are named with numbers because they need to run in a specific order

    def test_0typed_method(self):
        self.assertEqual(self._call_typed_method(), 378.0)

    def test_1python_dynamism(self):
        # The object is still a Python object, and so allows setting new attributes
        self.p.sale_discount = 2.0

        # The class is also a Python class, and can also be modified
        def new_calc_total(self, discount=0.0):
            return (self.cost - (discount + self.sale_discount)) * self.quantity
        Product.calc_total = new_calc_total
        self.assertTrue(self._call_typed_method(), 294.0)

run_test(__name__)
