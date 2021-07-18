# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import math
import os
import unittest

from iptest import IronPythonTestCase, is_cli, is_netcoreapp, is_netcoreapp21, long, run_test, skipUnlessIronPython

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

format_rounds_to_even = "%.0f" % 2.5 == "2"

class FormattingTest(IronPythonTestCase):
    def test_format_rounds_to_even(self):
        # https://github.com/IronLanguages/ironpython2/issues/634
        self.assertEqual(format_rounds_to_even, is_netcoreapp and not is_netcoreapp21 or not is_cli)

    def test_floats(self):
        """Formatting of floats"""

        def str(x): return "%.12g" % x

        # 12 significant digits near the decimal point

        self.assertEqual(str(12345678901.2), "12345678901.2")
        self.assertEqual(str(1.23456789012), "1.23456789012")

        # 12 significant digits near the decimal point, preceeded by upto 3 0s

        self.assertEqual(str(123456789012.00), "123456789012")
        self.assertEqual(str(123456789012.0), "123456789012")
        self.assertEqual(str(00.123456789012), "0.123456789012")
        self.assertEqual(str(0.000123456789012), "0.000123456789012")

        # 12 significant digits near the decimal point, followed by 0s, or preceeded more than 3 0s

        self.assertEqual(str(1234567890120.00), "1.23456789012e+12")
        self.assertEqual(str(0.0000123456789012), "1.23456789012e-05")

        # More than 12 significant digits near the decimal point, with rounding down

        self.assertEqual(str(12345678901.23), "12345678901.2")
        self.assertEqual(str(123456789012.3), "123456789012")
        self.assertEqual(str(1.234567890123), "1.23456789012")

        # More than 12 significant digits near the decimal point, with rounding up

        self.assertEqual(str(12345678901.25), "12345678901.2" if format_rounds_to_even else "12345678901.3")
        self.assertEqual(str(123456789012), "123456789012")
        self.assertEqual(str(1.234567890125), "1.23456789012" if format_rounds_to_even else "1.23456789013")
        self.assertEqual(str(1.234567890126), "1.23456789013")

        # Signficiant digits away from the decimal point

        self.assertEqual(str(100000000000.0), "100000000000")
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

        self.assertEqual(str(2.0), "2")
        self.assertEqual(str(.0), "0")
        self.assertEqual(str(-.0), "-0")
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
                (0.00001, "1e-05"),

                (123456.5, "123456" if format_rounds_to_even else "123457"),
        ]

        for v in values:
            self.assertEqual("%g" % v[0], v[1])
            self.assertEqual("%.6g" % v[0], v[1])
            self.assertEqual("% .6g" % v[0], " " + v[1])

    def test_formatting_issues(self):
        # these were not working properly
        values = (
            ("0%.1f" % -0.01, "0-0.0"),
            ("%+01.0f" % -0.0, "-0"),
            ("%#9f" % 1.0, " 1.000000"),
            ("%#9e" % 1.0, "1.000000e+00"),
            ("%#9g" % 1.0, "  1.00000"),
        )

        for a, b in values:
            self.assertEqual(a, b)

        # TODO: fix these
        values = (
            ("%-#3.0f" % 1.0, "1. "),
            ( "%104.100f" % 1.0, "  1." + ("0" * 100)),
            ("% 104.100f" % 1.0, "  1." + ("0" * 100)),
            ("%+104.100f" % 1.0, " +1." + ("0" * 100)),
            #("%.200f" % 1.0, "1." + ("0" * 200)), # OverflowError
            ("%+07.0e" % -0.0, "-00e+00"),
            ( "% 7.0e" % -0.0, " -0e+00"),
            ("%0104.100f" % 1.0, "001." + ("0" * 100)),
            ("%0#7.0e" % 1.0, "01.e+00"),
        )

        if is_cli:
            for a, b in values:
                self.assertNotEqual(a, b)
        else:
            for a, b in values:
                self.assertEqual(a, b)

    def test_ints(self):
        # Test results can be generated by running the following with CPython:
        import itertools
        def gen_results(val, suffix):
            flags = "0- +"
            all_combinations = list(itertools.chain.from_iterable((list("".join(x) for x in itertools.combinations(flags, i)) for i in range(len(flags)+1))))
            res = {}
            for flags in all_combinations:
                res.setdefault(("%" + flags + suffix + "d") % val, []).append(flags)
            return res

        tests = {
            (1, ''): {'+1': ['+', '0+', '-+', ' +', '0-+', '0 +', '- +', '0- +'], '1': ['', '0', '-', '0-'], ' 1': [' ', '0 ', '- ', '0- ']},
            (-1, ''): {'-1': ['', '0', '-', ' ', '+', '0-', '0 ', '0+', '- ', '-+', ' +', '0- ', '0-+', '0 +', '- +', '0- +']},
            (1, '4'): {'   1': ['', ' '], '1   ': ['-', '0-'], '+1  ': ['-+', '0-+', '- +', '0- +'], '  +1': ['+', ' +'], ' 001': ['0 '], '0001': ['0'], '+001': ['0+', '0 +'], ' 1  ': ['- ', '0- ']},
            (-1, '4'): {'  -1': ['', ' ', '+', ' +'], '-001': ['0', '0 ', '0+', '0 +'], '-1  ': ['-', '0-', '- ', '-+', '0- ', '0-+', '- +', '0- +']},
            (1, '.2'): {'01': ['', '0', '-', '0-'], ' 01': [' ', '0 ', '- ', '0- '], '+01': ['+', '0+', '-+', ' +', '0-+', '0 +', '- +', '0- +']},
            (-1, '.2'): {'-01': ['', '0', '-', ' ', '+', '0-', '0 ', '0+', '- ', '-+', ' +', '0- ', '0-+', '0 +', '- +', '0- +']},
            (1, '4.2'): {'  01': ['', ' '], '0001': ['0'], '01  ': ['-', '0-'], ' +01': ['+', ' +'], ' 001': ['0 '], '+001': ['0+', '0 +'], ' 01 ': ['- ', '0- '], '+01 ': ['-+', '0-+', '- +', '0- +']},
            (-1, '4.2'): {' -01': ['', ' ', '+', ' +'], '-001': ['0', '0 ', '0+', '0 +'], '-01 ': ['-', '0-', '- ', '-+', '0- ', '0-+', '- +', '0- +']},
            (1, '110'): {' ' * 109 + '1': ['', ' '], '0' * 109 + '1': ['0'], '1' + ' ' * 109: ['-', '0-'], ' ' * 108 + '+1': ['+', ' +'], ' ' + '0' * 108 + '1': ['0 '], '+' + '0' * 108 + '1': ['0+', '0 +'], ' 1' + ' ' * 108: ['- ', '0- '], '+1' + ' ' * 108: ['-+', '0-+', '- +', '0- +']},
            (-1, '110'): {' ' * 108 + '-1': ['', ' ', '+', ' +'], '-' + '0' * 108 + '1': ['0', '0 ', '0+', '0 +'], '-1' + ' ' * 108: ['-', '0-', '- ', '-+', '0- ', '0-+', '- +', '0- +']},
            (1, '.102'): {'0' * 101 + '1': ['', '0', '-', '0-'], ' ' + '0' * 101 + '1': [' ', '0 ', '- ', '0- '], '+' + '0' * 101 + '1': ['+', '0+', '-+', ' +', '0-+', '0 +', '- +', '0- +']},
            (-1, '.102'): {'-' + '0' * 101 + '1': ['', '0', '-', ' ', '+', '0-', '0 ', '0+', '- ', '-+', ' +', '0- ', '0-+', '0 +', '- +', '0- +']},
            (1, '110.102'): {' ' * 8 + '0' * 101 + '1': ['', ' '], '0' * 109 + '1': ['0'], '0' * 101 + '1' + ' ' * 8: ['-', '0-'], ' ' * 7 + '+' + '0'*101 + '1': ['+', ' +'], ' ' + '0' * 108 + '1': ['0 '], '+' + '0' * 108 + '1': ['0+', '0 +'], ' ' + '0' * 101 + '1' + ' ' * 7: ['- ', '0- '], '+' + '0' * 101 + '1' + ' ' * 7: ['-+', '0-+', '- +', '0- +']},
            (-1, '110.102'): {' ' * 7 + '-' + '0' * 101 + '1': ['', ' ', '+', ' +'], '-' + '0' * 108 + '1': ['0', '0 ', '0+', '0 +'], '-' + '0' * 101 + '1' + ' ' * 7: ['-', '0-', '- ', '-+', '0- ', '0-+', '- +', '0- +']},
        }

        for k, test_res in tests.items():
            val, suffix = k
            self.assertEqual(gen_results(val, suffix), test_res)
            for res, v in test_res.items():
                for flags in v:
                    self.assertEqual(("%" + flags + suffix + "d") % val, res, msg="{!r} % {}".format("%" + flags + suffix + "d", val))
                    self.assertEqual(("%" + flags + suffix + "d") % long(val), res, msg="{!r} % long({})".format("%" + flags + suffix + "d", val))
                    # alternate form does nothing
                    self.assertEqual(("%#" + flags + suffix + "d") % val, res, msg="{!r} % {}".format("%#" + flags + suffix + "d", val))
                    self.assertEqual(("%#" + flags + suffix + "d") % long(val), res, msg="{!r} % long({})".format("%#" + flags + suffix + "d", val))

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

    def test_long(self):
        # these were not working properly
        self.assertEqual("%x" % (-1 << 31), '-80000000')
        self.assertEqual("% 9x" % (1 << 31), ' 80000000')

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

        self.assertEqual(repr("\u00F4"), "'\xf4'")
        self.assertEqual(repr("\u10F4"), "'\u10f4'")

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

    def test_inf_nan(self):
        inf_nan_cases = """
-- nans and infinities
%f nan -> nan
%f inf -> inf
%f -infinity -> -inf
%F nan -> NAN
%F infinity -> INF
%F -inf -> -INF

-- nans and infinities
%e nan -> nan
%e inf -> inf
%e -infinity -> -inf
%E nan -> NAN
%E infinity -> INF
%E -inf -> -INF

-- nans and infinities
%g nan -> nan
%g inf -> inf
%g -infinity -> -inf
%G nan -> NAN
%G infinity -> INF
%G -inf -> -INF
""".strip().split("\n")

        self.check_lines(inf_nan_cases)

    def test_format_testfile(self):
        """the following is borrowed from stdlib"""

        expected_failures = []
        if is_cli:
            expected_failures = """
%#.0e 0.01 -> 1.e-02
%#.0e 0.1 -> 1.e-01
%#.0e 1 -> 1.e+00
%#.0e 10 -> 1.e+01
%#.0e 100 -> 1.e+02
%#.0e 0.012 -> 1.e-02
%#.0e 0.12 -> 1.e-01
%#.0e 1.2 -> 1.e+00
%#.0e 12 -> 1.e+01
%#.0e 120 -> 1.e+02
%#.0e 123.456 -> 1.e+02
%#.0e 0.000123456 -> 1.e-04
%#.0e 123456000 -> 1.e+08
%#.0e 0.5 -> 5.e-01
%#.0e 1.4 -> 1.e+00
%#.0e 1.5 -> 2.e+00
%#.0e 1.6 -> 2.e+00
%#.0e 2.4999999 -> 2.e+00
%#.0e 2.5 -> 2.e+00
%#.0e 2.5000001 -> 3.e+00
%#.0e 3.499999999999 -> 3.e+00
%#.0e 3.5 -> 4.e+00
%#.0e 4.5 -> 4.e+00
%#.0e 5.5 -> 6.e+00
%#.0e 6.5 -> 6.e+00
%#.0e 7.5 -> 8.e+00
%#.0e 8.5 -> 8.e+00
%#.0e 9.4999 -> 9.e+00
%#.0e 9.5 -> 1.e+01
%#.0e 10.5 -> 1.e+01
%#.0e 14.999 -> 1.e+01
%#.0e 15 -> 2.e+01
%#.2g 0 -> 0.0
%#.3g 0 -> 0.00
%#.4g 0 -> 0.000
%#.2g 0.2 -> 0.20
%#.3g 0.2 -> 0.200
%#.4g 0.2 -> 0.2000
%#.10g 0.2 -> 0.2000000000
%#.0g 20 -> 2.e+01
%#.1g 20 -> 2.e+01
%#.0g 234.56 -> 2.e+02
%#.1g 234.56 -> 2.e+02
%r 0.03 -> 0.03
%r 0.04 -> 0.04
%r 0.05 -> 0.05
%r 9999999999999999 -> 1e+16
%r 1e16 -> 1e+16
%r 1.001e-4 -> 0.0001001
%r 1.00000000001e-4 -> 0.000100000000001
%r 1.0000000001e-4 -> 0.00010000000001
%r 0.9999999999999999e-4 -> 9.999999999999999e-05
%r 0.999999999999e-4 -> 9.99999999999e-05
%r 0.999e-4 -> 9.99e-05
%r 1e-5 -> 1e-05
""".strip().split("\n")

        if not format_rounds_to_even:
            expected_failures += """
%.0f 2.5 -> 2
%.0f 1e49 -> 9999999999999999464902769475481793196872414789632
%.0f 9.9999999999999987e+49 -> 99999999999999986860582406952576489172979654066176
%.0f 1e50 -> 100000000000000007629769841091887003294964970946560
%.1f 0.25 -> 0.2
%.2f 0.125 -> 0.12
%#.0f 2.5 -> 2.
%.0e 2.5 -> 2e+00
%.0e 4.5 -> 4e+00
%.0e 6.5 -> 6e+00
%.0e 8.5 -> 8e+00
%r 9999999999999998 -> 9999999999999998.0
""".strip().split("\n")

        import test
        with open(os.path.join(test.__path__[0], 'formatfloat_testcases.txt')) as testfile:
            self.check_lines(testfile, expected_failures=expected_failures)

    def check_lines(self, lines, expected_failures=None):
        for i, line in enumerate(lines):
            if line.startswith('--'):
                continue
            line = line.strip()
            if not line:
                continue

            lhs, rhs = map(str.strip, line.split('->'))
            fmt, arg = lhs.split()
            arg = float(arg)
            if expected_failures and line in expected_failures:
                self.assertNotEqual(fmt % arg, rhs, "line " + str(i+1))
                continue
            self.assertEqual(fmt % arg, rhs, "line " + str(i+1))
            if not math.isnan(arg) and math.copysign(1.0, arg) > 0.0:
                self.assertEqual(fmt % -arg, '-' + rhs)

run_test(__name__)
