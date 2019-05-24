# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import os
import unittest

from iptest import IronPythonTestCase, is_cli, is_netcoreapp30, run_test, skipUnlessIronPython

class A:
    def __str__(self):
        return "str"
    def __repr__(self):
        return "repr"

class B(object):
    def __str__(self):
        return "str"
    def __repr__(self):
        return "repr"

class FormattingTest(IronPythonTestCase):
    def test_floats(self):
        """Formatting of floats"""

        # 12 significant digits near the decimal point

        self.assertEqual(str(12345678901.2), "12345678901.2")
        self.assertEqual(str(1.23456789012), "1.23456789012")

        # 12 significant digits near the decimal point, preceeded by upto 3 0s

        if is_cli: #https://github.com/IronLanguages/ironpython2/issues/17
            self.assertEqual(str(123456789012.00), "123456789012.0")
            self.assertEqual(str(123456789012.0), "123456789012.0")
        else:
            self.assertEqual(str(123456789012.00), "1.23456789012e+11")
            self.assertEqual(str(123456789012.0), "1.23456789012e+11")
            
        self.assertEqual(str(00.123456789012), "0.123456789012")
        self.assertEqual(str(0.000123456789012), "0.000123456789012")

        # 12 significant digits near the decimal point, followed by 0s, or preceeded more than 3 0s

        self.assertEqual(str(1234567890120.00), "1.23456789012e+12")
        self.assertEqual(str(0.0000123456789012), "1.23456789012e-05")

        # More than 12 significant digits near the decimal point, with rounding down

        self.assertEqual(str(12345678901.23), "12345678901.2")

        #https://github.com/IronLanguages/ironpython2/issues/17
        if is_cli:
            self.assertEqual(str(123456789012.3), "123456789012.0")
        else:
            self.assertEqual(str(123456789012.3), "1.23456789012e+11")
            
        self.assertEqual(str(1.234567890123), "1.23456789012")

        # More than 12 significant digits near the decimal point, with rounding up

        if is_cli and not is_netcoreapp30: # https://github.com/IronLanguages/ironpython2/issues/634
            self.assertEqual(str(12345678901.25), "12345678901.3")
            self.assertEqual(str(123456789012.5), "123456789013.0")
        else:
            self.assertEqual(str(12345678901.25), "12345678901.2")
            if is_cli: # https://github.com/IronLanguages/ironpython2/issues/17
                self.assertEqual(str(123456789012.5), "123456789012.0")
            else:
                self.assertEqual(str(123456789012.5), "1.23456789012e+11")

        if is_cli and not is_netcoreapp30: # https://github.com/IronLanguages/ironpython2/issues/634
            self.assertEqual(str(1.234567890125), "1.23456789013")
        else:
            self.assertEqual(str(1.234567890125), "1.23456789012")
        self.assertEqual(str(1.234567890126), "1.23456789013")

        # Signficiant digits away from the decimal point

        if is_cli: # https://github.com/IronLanguages/ironpython2/issues/17
            self.assertEqual(str(100000000000.0), "100000000000.0")
        else:
            self.assertEqual(str(100000000000.0), "1e+11")
        self.assertEqual(str(1000000000000.0), "1e+12")
        self.assertEqual(str(0.0001), "0.0001")
        self.assertEqual(str(0.00001), "1e-05")

        # Near the ends of the number line

        # System.Double.MaxValue
        self.assertEqual(str(1.79769313486232e+308), "inf")
        self.assertEqual(str(1.79769313486231e+308), "1.79769313486e+308")
        # System.Double.MinValue
        self.assertEqual(str(-1.79769313486232e+308), "-inf")
        self.assertEqual(str(-1.79769313486231e+308), "-1.79769313486e+308")
        # System.Double.Epsilon
        self.assertEqual(str(4.94065645841247e-324), "4.94065645841e-324")
        # NaN
        self.assertEqual(str((1.79769313486232e+308 * 2.0) * 0.0), "nan")

        self.assertEqual(str(2.0), "2.0")
        self.assertEqual(str(.0), "0.0")
        self.assertEqual(str(-.0), "-0.0")
        # verify small strings display all precision by default
        x = 123.456E-19 * 2.0
        self.assertEqual(str(x), "2.46912e-17")

        ######################################################################################

        values = [
                # 6 significant digits near the decimal point

                (123456, "123456"),
                (123456.0, "123456"),
                (12345.6, "12345.6"),
                (1.23456, "1.23456"),
                (0.123456, "0.123456"),

                # 6 significant digits near the decimal point, preceeded by upto 3 0s

                (0.000123456, "0.000123456"),

                # More than 6 significant digits near the decimal point, with rounding down

                (123456.4, "123456"),
                (0.0001234564, "0.000123456"),

                # More than 6 significant digits near the decimal point, with rounding up

                (0.0001234565, "0.000123457"),

                # Signficiant digits away from the decimal point

                (100000.0, "100000"),
                (1000000.0, "1e+06"),
                (0.0001, "0.0001"),
                (0.00001, "1e-05")]
        if is_cli and not is_netcoreapp30: # https://github.com/IronLanguages/ironpython2/issues/634
            values.append((123456.5, "123457"))
        else:
            values.append((123456.5, "123456"))

        for v in values:
            self.assertEqual("%g" % v[0], v[1])
            self.assertEqual("%.6g" % v[0], v[1])
            # self.assertEqual("% .6g" % v[0], v[1])

    @skipUnlessIronPython()
    def test_single(self):
        """Formatting of System.Single"""
        self.load_iron_python_test()
        import IronPythonTest
        f = IronPythonTest.DoubleToFloat.ToFloat(1.0)
        self.assertEqual(str(f), "1.0")
        self.assertEqual("%g" % f, "1")
        f = IronPythonTest.DoubleToFloat.ToFloat(1.1)
        self.assertEqual(str(f), "1.1")
        self.assertEqual("%g" % f, "1.1")
        f = IronPythonTest.DoubleToFloat.ToFloat(1.2345678)
        self.assertEqual(str(f), "1.23457")
        self.assertEqual("%g" % f, "1.23457")
        f = IronPythonTest.DoubleToFloat.ToFloat(1234567.8)
        self.assertEqual(str(f), "1.23457e+06")
        self.assertEqual("%g" % f, "1.23457e+06")

    def test_errors(self):
        def formatError():
            "%d" % (1,2)
            
        self.assertRaises(TypeError, formatError, None)

        def formatError_earlyEnd():
            "%" % None

        self.assertRaises(ValueError, formatError_earlyEnd)

    def test_basic(self):
        self.assertEqual(len('%10d' %(1)), 10)

        #formatting should accept a float for int formatting...
        self.assertEqual('%d' % 3.7, '3')

        self.assertEqual("%0.3f" % 10.8, "10.800")

    def test__repr__and__str__(self):
        """test that %s / %r work correctly"""

        # oldstyle class
        a = A()
        self.assertEqual("%s" % a, "str")
        self.assertEqual("%r" % a, "repr")
        b = B()
        # BUG 153
        #self.assertEqual("%s" % b, "str")
        # /BUG
        self.assertEqual("%r" % b, "repr")

    def test_unicode(self):
        """# if str() returns Unicode, so should test character"""
        self.assertEqual("%c" % 23, chr(23))
        self.assertRaises(OverflowError, (lambda: "%c" % -1) )
        self.assertEqual("%c" % str('x'), str('x'))
        try:
            self.assertEqual("%c" % 65535, '\uffff')
        except OverflowError:
            pass

    def test_i_u(self):
        """test %i, %u, not covered in test_format"""
        self.assertEqual('%i' % 23, '23')
        self.assertEqual('%i' % 23.9,  '23')
        self.assertEqual('%+u' % 5, '+5')
        self.assertEqual('%05u' % 3, '00003')
        self.assertEqual('% u' % 19, ' 19')
        self.assertEqual('%*u' % (5,10), '   10')
        self.assertEqual('%e' % 1000000, '1.000000e+06')
        self.assertEqual('%E' % 1000000, '1.000000E+06')

        self.assertEqual('%.2e' % 1000000, '1.00e+06')
        self.assertEqual('%.2E' % 1000000, '1.00E+06')
        self.assertEqual('%.2g' % 1000000, '1e+06')

        self.assertEqual('%G' % 100, '100')

    def test_named_inputs(self):
        """test named inputs"""
        fmtstr = '%(x)+d -- %(y)s -- %(z).2f'
        self.assertEqual(fmtstr % {'x':9, 'y':'quux', 'z':3.1415}, '+9 -- quux -- 3.14')
        self.assertRaises(KeyError, (lambda:fmtstr % {}))
        self.assertRaises(KeyError, (lambda:fmtstr % {'x': 3}))
        self.assertRaises(TypeError, (lambda:fmtstr % {'x': 'notanint', 'y':'str', 'z':2.1878}))

        self.assertEqual('%(key)s %(yek)d' % {'key':'ff', 'yek':200}, "ff 200")

        self.assertEqual(repr("\u00F4"), "u'\\xf4'")
        self.assertEqual(repr("\u10F4"), "u'\\u10f4'")

        self.assertRaises(TypeError, lambda: "%5c" % None)
        self.assertEqual("%5c" % 'c', '    c')
        self.assertEqual("%+5c" % 'c', '    c')
        self.assertEqual("%-5c" % 'c', 'c    ')

        self.assertEqual("%5s" % None, ' None')
        self.assertEqual("%5s" % 'abc', '  abc')
        self.assertEqual("%+5s" % 'abc', '  abc')
        self.assertEqual("%-5s" % 'abc', 'abc  ')
 
    def test_named_inputs_nested_parens(self):
        """Test named inputs with nested ()"""

        # Success cases
        s = '_%((key))s_' % {'(key)':10}
        self.assertEqual(s, '_10_')

        s = '_%((((key))))s_' % {'(((key)))':20}
        self.assertEqual(s, '_20_')

        s = '%((%s))s' % { '(%s)' : 30 }
        self.assertEqual(s, '30')

        s = '%()s' % { '': 40 }
        self.assertEqual(s,'40')

        s = '%(a(b)c)s' % { 'a(b)c' : 50 }
        self.assertEqual(s,'50')

        s = '%(a)s)s' % { 'a' : 60 }
        self.assertEqual(s,'60)s')

        s = '%(((a)s))s' % {'((a)s)': 70, 'abc': 'efg'}
        self.assertEqual(s, '70')

        # Error cases

        # ensure we properly count number of expected closing ')'
        def Error1():
            return '%(a))s' % { 'a' : 10, 'a)' : 20 }

        self.assertRaises(ValueError, Error1)

        # Incomplete format key, not enough closing ')'
        def Error2():
            return '%((a)s' % { 'a' : 10, '(a' : 20 }

        self.assertRaises(ValueError, Error2)


        self.assertEqual('%*s' %(-5,'abc'), 'abc  ')

    def test_cp28936(self):
        self.assertEqual('%10.1e' % 1, '   1.0e+00')
        self.assertEqual('% 10.1e' % 1, '   1.0e+00')
        self.assertEqual('%+10.1e' % 1, '  +1.0e+00')

        self.assertEqual('%10.1e' % -1, '  -1.0e+00')
        self.assertEqual('% 10.1e' % -1, '  -1.0e+00')
        self.assertEqual('%+10.1e' % -1, '  -1.0e+00')

        self.assertEqual('%-10.1e' % 1, '1.0e+00   ')
        self.assertEqual('% -10.1e' % 1, ' 1.0e+00  ')
        self.assertEqual('%+-10.1e' % 1, '+1.0e+00  ')

        self.assertEqual('%-10.1e' % -1, '-1.0e+00  ')
        self.assertEqual('% -10.1e' % -1, '-1.0e+00  ')
        self.assertEqual('%+-10.1e' % -1, '-1.0e+00  ')


    def test_format_testfile(self):
        """the following is borrowed from stdlib"""
        import math
        format_testfile = 'formatfloat_testcases.txt'
        bugged = {
            ('%.2f', 0.004999): "0.01",
            ('%f', 4.9989999999999997e-07): "0.000001",
        }
        with open(os.path.join(self.test_dir, format_testfile)) as testfile:
            for line in testfile:
                print(line)
                if line.startswith('--'):
                    continue
                line = line.strip()
                if not line:
                    continue

                lhs, rhs = map(str.strip, line.split('->'))
                fmt, arg = lhs.split()
                arg = float(arg)
                if is_netcoreapp30: # https://github.com/dotnet/corefx/issues/37524
                    rhs = bugged.get((fmt, arg), rhs)
                self.assertEqual(fmt % arg, rhs)
                if not math.isnan(arg) and math.copysign(1.0, arg) > 0.0:
                    print("minus")
                    self.assertEqual(fmt % -arg, '-' + rhs)

run_test(__name__)
