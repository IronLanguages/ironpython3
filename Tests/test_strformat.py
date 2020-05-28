# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import _string
import sys
import unittest

from iptest import IronPythonTestCase, is_cli, is_cpython, is_netcoreapp21, long, run_test, skipUnlessIronPython

allChars = ''
for y in [chr(x) for x in range(256) if chr(x) != '[' and chr(x) != '.']:
    allChars += y

class TestException(Exception): pass

class bad_str(object):
    def __str__(self):
        raise TestException('booh')

class StrFormatTest(IronPythonTestCase):

    def test_formatter_parser_errors(self):
        errors = [ ("{0!",             "unmatched '{' in format"),
                ("{0!}}",           "end of format while looking for conversion specifier"),
                ("}a{",             "Single '}' encountered in format string"),
                ("{0:0.1a",         "unmatched '{' in format"),
                ("{0:{}",           "unmatched '{' in format"),
                ("{0:aa{ab}",       "unmatched '{' in format"),
                ("{0!}}",           "end of format while looking for conversion specifier"),
                ("{0!}",            "end of format while looking for conversion specifier"),
                ("{0!",             "unmatched '{' in format"),
                ("{0!aa}",          "expected ':' after format specifier"),
                ("{0.{!:{.}.}}",    "expected ':' after format specifier"),
                ("{",               "Single '{' encountered in format string"),
                ]

        for format, errorMsg in errors:
            self.assertRaisesMessage(ValueError, errorMsg, list, _string.formatter_parser(format))

    def test_formatter_parser(self):
        tests = [ ('{0.}',             [('', '0.', '', None)]),
                ('{0:.{abc}}',       [('', '0', '.{abc}', None)]),
                ('{0[]}',            [('', '0[]', '', None)]),
                ('{0.{:{.}.}}',      [('', '0.{', '{.}.}', None)]),
                ('{0.{:.}.}',        [('', '0.{', '.}.', None)]),
                ('{0.{!.:{.}.}}',    [('', '0.{', '{.}.}', '.')]),
                ('{0.{!!:{.}.}}',    [('', '0.{', '{.}.}', '!')]),
                ('{0!!}',            [('', '0', '', '!')]),
                ('{0[}',             [('', '0[', '', None)]),
                ('{0:::!!::!::}',    [('', '0', '::!!::!::', None)]),
                ('{0.]}',            [('', '0.]', '', None)]),
                ('{0.[}',            [('', '0.[', '', None)]),
                ('{0..}',            [('', '0..', '', None)]),
                ('{{',               [('{', None, None, None)]),
                ('}}',               [('}', None, None, None)]),
                ('{{}}',             [('{', None, None, None), ('}', None, None, None)]),
                ]

        for format, expected in tests:
            self.assertEqual(list(_string.formatter_parser(format)), expected)

    def test_format_field_name_split_errors(self):
        if is_cpython: #http://ironpython.codeplex.com/workitem/28224
            temp = _string.formatter_field_name_split('') #Just ensure it doesn't throw
        else:
            self.assertRaisesMessage(ValueError, "empty field name", _string.formatter_field_name_split, '')
            self.assertRaisesMessage(ValueError, "empty field name", _string.formatter_field_name_split, '[')
            self.assertRaisesMessage(ValueError, "empty field name", _string.formatter_field_name_split, '.')
            self.assertRaisesMessage(ValueError, "empty field name", _string.formatter_field_name_split, '[.abc')
            self.assertRaisesMessage(ValueError, "empty field name", _string.formatter_field_name_split, '.abc')

        errors = [ ("0[",              "Missing ']' in format string"),
                ("abc.",            "Empty attribute in format string"),
                ("abc[]",           "Empty attribute in format string"),
                ]

        for format, errorMsg in errors:
            self.assertRaisesMessage(ValueError, errorMsg, list, _string.formatter_field_name_split(format)[1])

    def test_format_field_name_split(self):
        tests = [ ('0',                [long(0), []]),
                ('abc.foo',          ['abc', [(True, 'foo')]]),
                ('abc[2]',           ['abc', [(False, long(2))]]),
                ('1[2]',             [long(1), [(False, long(2))]]),
                ('1.abc',            [long(1), [(True, 'abc')]]),
                ('abc 2.abc',        ['abc 2', [(True, 'abc')]]),
                ('abc!2.abc',        ['abc!2', [(True, 'abc')]]),
                ('].abc',            [']', [(True, 'abc')]]),
                ("abc[[]",           ['abc', [(False, '[')]] ),
                ("abc[[[]",          ['abc', [(False, '[[')]] ),
                ]

        if not is_cpython: #http://ironpython.codeplex.com/workitem/28331
            tests.append(("abc[2]#x",         ['abc', [(False, long(2))]] ))
        tests.append([allChars, [allChars, []]])
        tests.append([allChars + '.foo', [allChars, [(True, 'foo')]]])
        tests.append([allChars + '[2]', [allChars, [(False, long(2))]]])

        for format, expected in tests:
            res = list(_string.formatter_field_name_split(format))
            res[1] = list(res[1])
            self.assertEqual(res, expected)

    def test_format_arg_errors(self):
        self.assertRaises(IndexError, '{0}'.format)
        self.assertRaisesMessage(ValueError, "Empty attribute in format string", '{0.}'.format, 42)
        self.assertRaisesMessage(ValueError, "Empty attribute in format string", '{0[]}'.format, 42)
        self.assertRaisesMessage(ValueError, "Missing ']' in format string", '{0[}'.format, 42)
        self.assertRaises(IndexError, '{0[}'.format)

    @skipUnlessIronPython()
    def test_format_cli_interop(self):
        # test classes implementing IFormattable where we pass
        # the format spec through
        import System
        dt = System.DateTime(2008, 10, 26)

        class x(object):
            abc = dt

        self.assertEqual(format(dt, 'MM-dd'), '10-26')
        self.assertEqual('{0:MM-dd}'.format(dt), '10-26')
        self.assertEqual('{abc:MM-dd}'.format(abc=dt), '10-26')
        self.assertEqual('{0.abc:MM-dd}'.format(x), '10-26')

        # test accessing a .NET attribute
        self.assertEqual('{0.Year}'.format(dt), '2008')

        # indexing into .NET dictionaries
        strDict = System.Collections.Generic.Dictionary[str, object]()
        strDict['abc'] = dt
        self.assertEqual('{0[abc]:MM-dd}'.format(strDict), '10-26')

        intDict = System.Collections.Generic.Dictionary[int, object]()
        intDict[42] = dt
        self.assertEqual('{0[42]:MM-dd}'.format(intDict), '10-26')

        objDict = System.Collections.Generic.Dictionary[object, object]()
        objDict[42], objDict['42'] = 'abc', 'def'
        self.assertEqual('{0[42]}'.format(objDict), 'abc')

        # import clr doesn't flow through
        self.assertRaises(AttributeError, '{0.MaxValue}'.format, int)

    def test_format_object_access(self):
        class y(object):
            bar = 23

        class x(object):
            def __getitem__(self, index):
                return type(index).__name__ + ' ' + str(index)
            abc = 42
            baz = y
            def __str__(self): return 'foo'
            def __repr__(self): return 'bar'

        self.assertEqual('{0.abc}'.format(x), '42')
        self.assertEqual('{0.abc}xyz'.format(x), '42xyz')
        self.assertEqual('{0[42]}'.format(x()), 'int 42')
        self.assertEqual('{0[abc]}'.format(x()), 'str abc')
        self.assertEqual('{0.baz.bar}'.format(x()), '23')
        self.assertEqual('{0[abc]!r}'.format(x()), "'str abc'")
        self.assertEqual('{0[abc]!s}'.format(x()), 'str abc')
        self.assertEqual('{0!r}'.format(x()), 'bar')
        self.assertEqual('{0!s}'.format(x()), 'foo')
        self.assertEqual('{abc!s}'.format(abc = x()), 'foo')
        self.assertEqual('{abc!r}'.format(abc = x()), 'bar')

    def test_format(self):
        class x(object):
            def __format__(self, formatSpec):
                return formatSpec

        # computed format specs
        self.assertEqual('{0:{1}}'.format(x(), 'abc'), 'abc')
        self.assertRaisesMessage(ValueError, "Max string recursion exceeded", '{0:{1:{2}}}'.format, x(), x(), 'abc')

        # built-in format method
        self.assertEqual(format(x()), '')
        self.assertEqual(format(x(), 'abc'), 'abc')

        class x:
            def __format__(self, *args):
                return 'abc'

        self.assertEqual(format(x(), ''), 'abc')
        self.assertEqual('{0}'.format(x()), 'abc')

        self.assertTrue('__format__' in type(x()).__dict__)

    def test_format_errors(self):
        class bad(object):
            def __format__(self, *args):
                return None

        class bad2(object):
            def __format__(self, *args):
                return 42

        self.assertRaisesMessage(TypeError, "bad.__format__ must return string or unicode, not NoneType", '{0}'.format, bad())
        self.assertRaisesMessage(TypeError, "bad2.__format__ must return string or unicode, not int", '{0}'.format, bad2())

        self.assertRaisesMessage(ValueError, "Unknown conversion specifier x", '{0!x}'.format, 'abc')

        self.assertRaisesMessage(TypeError, "bad.__format__ must return string or unicode, not NoneType", format, bad())
        self.assertRaisesMessage(TypeError, "bad2.__format__ must return string or unicode, not int", format, bad2())

    def test_object___format__(self):
        class x(object):
            def __str__(self):
                return 'abc'

        tests = [ ('',     'abc'),
                ('6',    'abc   '),
                ('6s',   'abc   '),
                ('<6',   'abc   '),
                ('>6',   '   abc'),
                ('^6',   ' abc  '),
                ('x^6',  'xabcxx'),
                ('x<6',  'abcxxx'),
                ('<<6',  'abc<<<'),
                ('x>6',  'xxxabc'),
                ('}^6',  '}abc}}'),
                ('\0<6', 'abc   '),
                ('.0',   ''),
                ('.1',   'a'),
                ('5.2',  'ab   '),
                ('5.2s', 'ab   '),
                ]

        for spec, result in tests:
            self.assertEqual(object.__format__(x(), spec), result)

    def test_object___format___errors(self):
        errors = [ ("+",             "Sign not allowed in string format specifier"),
                ("=+",            "Sign not allowed in string format specifier"),
                ('=10',            "'=' alignment not allowed in string format specifier"),
                ("10r",            "Unknown format code 'r' for object of type 'str'"),
                ("=+r",            "Unknown format code 'r' for object of type 'str'"),
                (".",              "Format specifier missing precision"),
                (".a",             "Format specifier missing precision"),
                ]

        # ensure only the s format type is recognized
        for char in allChars:
            if char != 's' and (char < '0' or char > '9'):
                if char==',':
                    errors.append(('10' + char, "Cannot specify ',' with 's'."))
                else:
                    errors.append(('10' + char, "Unknown format code '%s' for object of type 'str'" % char))


        for errorFmt, errorMsg in errors:
            self.assertRaisesMessage(ValueError, errorMsg, object().__format__, errorFmt)

        # __str__ is called before processing spec
        self.assertRaisesMessage(TestException, 'booh', bad_str().__format__, '+')
        self.assertRaisesMessage(TestException, 'booh', bad_str().__format__, '=10')
        self.assertRaisesMessage(TestException, 'booh', bad_str().__format__, '.')

    def test_float___format__(self):
        tests = []
        if is_cpython: #In large part due to http://ironpython.codeplex.com/workitem/28206
            tests+= [   (2.0,              '6.1',      ' 2e+00'),
                        (2.5,              '6.1',      ' 2e+00'),
                        (2.25,             '6.1',      ' 2e+00'),
                        (2.25,             '6.2',      '   2.2'),
                        (23.0,             '.1',       '2e+01'),
                        (23.0,             '.2',       '2.3e+01'),
                        (230.5,            '.3',       '2.3e+02'),
                        (11230.54,         '.5',       '1.1231e+04'),
                        (111230.54,        '.1',       '1e+05'),
                        (100000.0,           '.5',     '1e+05'),
                        (230.5,            '.3g',       '230'),
                        (230.5,            '.3n',       '230'),
                        (0.0,              '1.1',      '0e+00'),
                        (0.0,              '1.0',      '0e+00'),
                        (1.0,              '.0',       '1e+00'),
                        (1.1,              '.0',       '1e+00'),
                        (1.1,              '.1',       '1e+00'),
                        (10.0,             '.1',       '1e+01'),
                        (10.0,             '.0',       '1e+01'),
                        (100000000000.0,   '',         '1e+11'),
                        (1000000000.12,    '1.10',     '1e+09'),
                        (1000000000.12,    '1.3',      '1e+09'),
                        (999999999999.9,   '1.0',      '1e+12'),
                        (999999999999.9,   '1.2',      '1e+12'),
                        (999999999999.0,   '',         '9.99999999999e+11'),
                        (-999999999999.0,  '',         '-9.99999999999e+11'),
                        (10e667,           '+',        '+inf'),
                        (-10e667,          '+',        '-inf'),
                        (10e667/10e667,    '+',        '+nan'),
                        (10e667,           '-',        'inf'),
                        (-10e667,          '-',        '-inf'),
                        (10e667/10e667,    '-',        'nan'),
                        (10e667,           ' ',        ' inf'),
                        (-10e667,          ' ',        '-inf'),
                        (10e667/10e667,    ' ',        ' nan'),
                    ]
        else:
            tests+= [   (2.0,              '6.1',      '   2.0'),
                        (2.5,              '6.1',      '   2.0'),
                        (2.25,             '6.1',      '   2.0'),
                        (2.25,             '6.2',      '   2.2'),
                        (23.0,             '.1',       '2.0e+01'),
                        (23.0,             '.2',       '23.0'),
                        (230.5,            '.3',       '230.0'),
                        (11230.54,         '.5',       '11231.0'),
                        (111230.54,        '.1',       '1.0e+05'),
                        (100000.0,           '.5',     '1.0e+05'),
                        (230.5,            '.3g',       '230'),
                        (230.5,            '.3n',       '230'),
                        (0.0,              '1.1',      '0.0'),
                        (0.0,              '1.0',      '0.0'),
                        (1.0,              '.0',       '1.0'),
                        (1.1,              '.0',       '1.0'),
                        (1.1,              '.1',       '1.0'),
                        (10.0,             '.1',       '1.0e+01'),
                        (10.0,             '.0',       '1.0e+01'),
                        (100000000000.0,   '',         '100000000000.0'),
                        (1000000000.12,    '1.10',     '1000000000.0'),
                        (1000000000.12,    '1.3',      '1.0e+09'),
                        (999999999999.9,   '1.0',      '1.0e+12'),
                        (999999999999.9,   '1.2',      '1.0e+12'),
                        (999999999999.0,   '',         '999999999999.0'),
                        (-999999999999.0,  '',         '-999999999999.0'),
                        (10e667,           '+',        '+inf'),
                        (-10e667,          '+',        '-inf'),
                        (10e667/10e667,    '+',        '+nan'),
                        (10e667,           '-',        'inf'),
                        (-10e667,          '-',        '-inf'),
                        (10e667/10e667,    '-',        'nan'),
                        (10e667,           ' ',        ' inf'),
                        (-10e667,          ' ',        '-inf'),
                        (10e667/10e667,    ' ',        ' nan'),
                    ]

        tests+= [ (2.0,              '',         '2.0'),
                    (2.0,              'g',         '2'),
                    (2.0,              'f',         '2.000000'),
                    (2.5,              '',         '2.5'),
                    (2.5,              'g',         '2.5'),
                    (2.0,              '+',        '+2.0'),
                    (2.0,              '-',        '2.0'),
                    (2.0,              ' ',        ' 2.0'),
                    (2.0,              '<5',       '2.0  '),
                    (2.0,              '>5',       '  2.0'),
                    (2.0,              '=5',       '  2.0'),
                    (2.0,              '^6',       ' 2.0  '),
                    (2.0,              '6',        '   2.0'),
                    (2.0,              'x< 10.10', ' 2.0xxxxxx'),
                    (2.01,             'x< 10.10', ' 2.01xxxxx'),
                    (2.0,              'x> 10.10', 'xxxxxx 2.0'),
                    (2.0,              'x= 10.10', ' xxxxxx2.0'),
                    (2.0,              'x^ 10.10', 'xxx 2.0xxx'),
                    (2.0,              'x^ 9.10',  'xx 2.0xxx'),
                    (2.0,              '\0^ 9.10', '   2.0   '),
                    (2.23,             '6.2',      '   2.2'),
                    (2.25,             '6.3',      '  2.25'),
                    (2.123456789,      '2.10',     '2.123456789'),


                    (230.0,            '.2',       '2.3e+02'),
                    (230.1,            '.2',       '2.3e+02'),
                    (230.5,            '.4',       '230.5'),
                    (230.54,           '.4',       '230.5'),
                    (230.54,           '.5',       '230.54'),
                    (1230.54,          '.5',       '1230.5'),
                    (111230.54,        '.5',       '1.1123e+05'),
                    (111230.54,        '.4',       '1.112e+05'),
                    (111230.54,        '.3',       '1.11e+05'),
                    (111230.54,        '.2',       '1.1e+05'),


                    (23.0,             'e',        '2.300000e+01'),
                    (23.0,             '.6e',      '2.300000e+01'),
                    (23.0,             '.0e',      '2e+01'),
                    (23.0,             '.1e',      '2.3e+01'),
                    (23.0,             '.2e',      '2.30e+01'),
                    (23.0,             '.3e',      '2.300e+01'),
                    (23.0,             '.4e',      '2.3000e+01'),
                    (230.0,            '.2e',      '2.30e+02'),
                    (230.1,            '.2e',      '2.30e+02'),
                    (230.5,            '.3e',      '2.305e+02'),
                    (230.5,            '.4e',      '2.3050e+02'),
                    (230.54,           '.4e',      '2.3054e+02'),
                    (230.54,           '.5e',      '2.30540e+02'),
                    (1230.54,          '.5e',      '1.23054e+03'),
                    (11230.54,         '.5e',      '1.12305e+04'),
                    (111230.54,        '.5e',      '1.11231e+05'),
                    (111230.54,        '.4e',      '1.1123e+05'),
                    (111230.54,        '.3e',      '1.112e+05'),
                    (111230.54,        '.2e',      '1.11e+05'),
                    (111230.54,        '.1e',      '1.1e+05'),

                    (23.0,             'E',        '2.300000E+01'),
                    (23.0,             '.6E',      '2.300000E+01'),
                    (23.0,             '.0E',      '2E+01'),
                    (23.0,             '.1E',      '2.3E+01'),
                    (23.0,             '.2E',      '2.30E+01'),
                    (23.0,             '.3E',      '2.300E+01'),
                    (23.0,             '.4E',      '2.3000E+01'),
                    (230.0,            '.2E',      '2.30E+02'),
                    (230.1,            '.2E',      '2.30E+02'),
                    (230.5,            '.3E',      '2.305E+02'),
                    (230.5,            '.4E',      '2.3050E+02'),
                    (230.54,           '.4E',      '2.3054E+02'),
                    (230.54,           '.5E',      '2.30540E+02'),
                    (1230.54,          '.5E',      '1.23054E+03'),
                    (11230.54,         '.5E',      '1.12305E+04'),
                    (111230.54,        '.5E',      '1.11231E+05'),
                    (111230.54,        '.4E',      '1.1123E+05'),
                    (111230.54,        '.3E',      '1.112E+05'),
                    (111230.54,        '.2E',      '1.11E+05'),
                    (111230.54,        '.1E',      '1.1E+05'),

                    (23.0,             'F',        '23.000000'),
                    (23.0,             '.6F',      '23.000000'),
                    (23.0,             '.0F',      '23'),
                    (23.0,             '.1F',      '23.0'),
                    (23.0,             '.2F',      '23.00'),
                    (23.0,             '.3F',      '23.000'),
                    (23.0,             '.4F',      '23.0000'),
                    (230.0,            '.2F',      '230.00'),
                    (230.1,            '.2F',      '230.10'),
                    (230.5,            '.3F',      '230.500'),
                    (230.5,            '.4F',      '230.5000'),
                    (230.54,           '.4F',      '230.5400'),
                    (230.54,           '.5F',      '230.54000'),
                    (1230.54,          '.5F',      '1230.54000'),
                    (11230.54,         '.5F',      '11230.54000'),
                    (111230.54,        '.5F',      '111230.54000'),
                    (111230.54,        '.4F',      '111230.5400'),
                    (111230.54,        '.3F',      '111230.540'),
                    (111230.54,        '.2F',      '111230.54'),
                    (111230.54,        '.1F',      '111230.5'),
                    (111230.55,        '.1F',      '111230.6'),
                    (-111230.55,       '.1F',      '-111230.6'),
                    (111230.55,        '.1f',      '111230.6'),
                    (-111230.55,       '.1f',      '-111230.6'),

                    (23.0,             '%',        '2300.000000%'),
                    (23.0,             '.6%',      '2300.000000%'),
                    (23.0,             '.0%',      '2300%'),
                    (23.0,             '.1%',      '2300.0%'),
                    (23.0,             '.2%',      '2300.00%'),
                    (23.0,             '.3%',      '2300.000%'),
                    (23.0,             '.4%',      '2300.0000%'),
                    (230.0,            '.2%',      '23000.00%'),
                    (230.1,            '.2%',      '23010.00%'),
                    (230.5,            '.3%',      '23050.000%'),
                    (230.5,            '.4%',      '23050.0000%'),
                    (230.54,           '.4%',      '23054.0000%'),
                    (230.54,           '.5%',      '23054.00000%'),
                    (1230.54,          '.5%',      '123054.00000%'),
                    (11230.54,         '.5%',      '1123054.00000%'),
                    (111230.54,        '.5%',      '11123054.00000%'),
                    (111230.54,        '.4%',      '11123054.0000%'),
                    (111230.54,        '.3%',      '11123054.000%'),
                    (111230.54,        '.2%',      '11123054.00%'),
                    (111230.54,        '.1%',      '11123054.0%'),
                    (111230.55,        '.1%',      '11123055.0%'),
                    (-111230.55,       '.1%',      '-11123055.0%'),

                    (23.0,             '.1g',       '2e+01'),
                    (23.0,             '.0g',       '2e+01'),
                    (230.0,            '.1g',       '2e+02'),
                    (23.0,             '.2g',       '23'),
                    (23.5,             '.2g',       '24'),
                    (23.4,             '.2g',       '23'),
                    (23.45,            '.2g',       '23'),
                    (230.0,            '.2g',       '2.3e+02'),
                    (230.1,            '.2g',       '2.3e+02'),

                    (230.5,            '.4g',       '230.5'),
                    (230.54,           '.4g',       '230.5'),
                    (230.54,           '.5g',       '230.54'),
                    (1230.54,          '.5g',       '1230.5'),
                    (11230.54,         '.5g',       '11231'),
                    (111230.54,        '.5g',       '1.1123e+05'),
                    (111230.54,        '.4g',       '1.112e+05'),
                    (111230.54,        '.3g',       '1.11e+05'),
                    (111230.54,        '.2g',       '1.1e+05'),
                    (111230.54,        '.1g',       '1e+05'),

                    (23.0,             '.1n',       '2e+01'),
                    (23.0,             '.2n',       '23'),
                    (230.0,            '.2n',       '2.3e+02'),
                    (230.1,            '.2n',       '2.3e+02'),

                    (230.5,            '.4n',       '230.5'),
                    (230.54,           '.4n',       '230.5'),
                    (230.54,           '.5n',       '230.54'),
                    (1230.54,          '.5n',       '1230.5'),
                    (11230.54,         '.5n',       '11231'),
                    (111230.54,        '.5n',       '1.1123e+05'),
                    (111230.54,        '.4n',       '1.112e+05'),
                    (111230.54,        '.3n',       '1.11e+05'),
                    (111230.54,        '.2n',       '1.1e+05'),
                    (111230.54,        '.1n',       '1e+05'),
                    (11231.54,         'n',        '11231.5'),
                    (111230.54,        'n',        '111231'),
                    (111230.54,        'g',        '111231'),
                    (0.0,              '',         '0.0'),
                    (0.0,              '1',        '0.0'),
                    (1.1,              '.2',       '1.1'),
                    (1000000.0,        '',         '1000000.0'),
                    (10000000.0,       '',         '10000000.0'),
                    (100000000.0,      '',         '100000000.0'),
                    (1000000000.0,     '',         '1000000000.0'),
                    (10000000000.0,    '',         '10000000000.0'),
                    (1000000000000.0,  '',         '1000000000000.0'),
                    (1000000000000.0,  'g',         '1e+12'),
                    (-1000000000000.0, '',         '-1000000000000.0'),
                    (-1000000000000.0, 'g',        '-1e+12'),
                    (-1000000000000.0, 'G',        '-1E+12'),
                    (-1000000000000.0, '.1g',      '-1e+12'),
                    (-1000000000000.0, '.1G',      '-1E+12'),
                    (10e667,           '',         'inf'),
                    (-10e667,          '',         '-inf'),
                    (10e667/10e667,    '',         'nan'),
                    ]


        for value, spec, result in tests:
            actual = value.__format__(spec)
            self.assertEqual(actual, result, "value:{0}, spec:{1}, (expected) result:{2}, actual:{3}, expr:({0}).__format__('{1}')".format(value, spec, result, actual))

        if is_netcoreapp21: return # https://github.com/IronLanguages/ironpython3/issues/751

        # check locale specific formatting
        import _locale
        try:
            if is_cli:
                _locale.setlocale(_locale.LC_ALL, 'en_US')
            else:
                _locale.setlocale(_locale.LC_ALL, 'English_United States.1252')

            tests = [ 
                        (1000.0,              'n',         '1,000'),
                        (1000.12345,          'n',         '1,000.12'),
                        (1000.5,              'n',         '1,000.5'),
                        (100000.0,            'n',         '100,000'),

                        (100000.0,            '.5n',       '1e+05'),
                        (100000.5,            '.5n',       '1e+05'),

                        (100000.5,            '.7n',       '100,000.5'),
                    ]
            if is_cpython: #http://ironpython.codeplex.com/workitem/28206
                tests+= [
                            (100000.5,            'n',         '100,000'),
                            (100000.5,            '.6n',       '100,000'),
                        ]
            else:
                tests+= [
                            (100000.5,            'n',         '100,000'),
                            (100000.5,            '.6n',       '100,000'),
                        ]

            for value, spec, result in tests:
                actual = value.__format__(spec)
                self.assertEqual(actual, result, "value:{0}, spec:{1}, (expected) result:{2}, actual:{3}, expr:({0}).__format__('{1}')".format(value, spec, result, actual))

        finally:
            # and restore it back...
            _locale.setlocale(_locale.LC_ALL, 'C')
            self.assertEqual(100000.0.__format__('n'), '100000')

    def test_float___format___errors(self):
        errors = []

        okChars = set(['\0', '%', 'E', 'F', 'G', 'e', 'f', 'g', 'n', ','])
        # verify the okChars are actually ok
        for char in okChars:
            2.0.__format__('10' + char)

        for char in allChars:
            if char not in okChars and (char < '0' or char > '9'):
                errors.append((2.0, '10' + char, "Unknown format code '%s' for object of type 'float'" % char))

        for value, errorFmt, errorMsg in errors:
            self.assertRaisesMessage(ValueError, errorMsg, value.__format__, errorFmt)

    def test_int___format__(self):
        tests = [
                (0,    '+',        '+0'),
                (0,    ' ',        ' 0'),
                (0,    '-',        '0'),
                (2,    '',         '2'),
                (2,    '+',        '+2'),
                (2,    '-',        '2'),
                (2,    ' ',        ' 2'),
                (2,    '<5',       '2    '),
                (2,    '05',       '00002'),
                (20000,'+4',       '+20000'),
                (2,    '>5',       '    2'),
                (2,    '=5',       '    2'),
                (2,    '^6',       '  2   '),
                (2,    '6',        '     2'),
                (2,    'x< 10',    ' 2xxxxxxxx'),
                (2,    'x> 10',    'xxxxxxxx 2'),
                (2,    'x= 10',    ' xxxxxxxx2'),
                (2,    'x^ 10',    'xxxx 2xxxx'),
                (2,    'x^ 9',     'xxx 2xxxx'),
                (2,    'x<+10',    '+2xxxxxxxx'),
                (2,    'x>+10',    'xxxxxxxx+2'),
                (2,    'x=+10',    '+xxxxxxxx2'),
                (2,    'x^+10',    'xxxx+2xxxx'),
                (2,    'x^+9',     'xxx+2xxxx'),
                (2,    'x<-10',    '2xxxxxxxxx'),
                (2,    'x>-10',    'xxxxxxxxx2'),
                (2,    'x=-10',    'xxxxxxxxx2'),
                (2,    'x^-10',    'xxxx2xxxxx'),
                (2,    'x^-9',     'xxxx2xxxx'),
                (-2,   'x<-10',    '-2xxxxxxxx'),
                (-2,   'x>-10',    'xxxxxxxx-2'),
                (-2,   'x=-10',   '-xxxxxxxx2'),
                (-2,   'x^-10',    'xxxx-2xxxx'),
                (-2,   'x^-9',     'xxx-2xxxx'),
                (-2,   'x<+10',    '-2xxxxxxxx'),
                (-2,   'x>+10',    'xxxxxxxx-2'),
                (-2,   'x=+10',   '-xxxxxxxx2'),
                (-2,   'x^+10',    'xxxx-2xxxx'),
                (-2,   'x^+9',     'xxx-2xxxx'),
                (-2,   'x< 10',    '-2xxxxxxxx'),
                (-2,   'x> 10',    'xxxxxxxx-2'),
                (-2,   'x= 10',   '-xxxxxxxx2'),
                (-2,   'x^ 10',    'xxxx-2xxxx'),
                (-2,   'x^ 9',     'xxx-2xxxx'),
                (2,    '\0^ 9',    '    2    '),

                (2,    'c',         '\x02'),
                (2,    '<5c',       '\x02    '),
                (2,    '>5c',       '    \x02'),
                (2,    '=5c',       '    \x02'),
                (2,    '^6c',       '  \x02   '),
                (2,    '6c',        '     \x02'),

                (3,    'b',         '11'),
                (3,    '+b',        '+11'),
                (3,    '-b',        '11'),
                (3,    ' b',        ' 11'),
                (3,    '<5b',       '11   '),
                (3,    '>5b',       '   11'),
                (3,    '=5b',       '   11'),
                (3,    '^6b',       '  11  '),
                (3,    '6b',        '    11'),
                (3,    'x< 010b',   ' 11xxxxxxx'),
                (3,    '< 010b',    ' 110000000'),
                (3,    'x< 010b',    ' 11xxxxxxx'),
                (3,    'x< 10b',    ' 11xxxxxxx'),
                (3,    'x< 10b',    ' 11xxxxxxx'),
                (3,    'x> 10b',    'xxxxxxx 11'),
                (3,    'x= 10b',    ' xxxxxxx11'),
                (3,    'x^ 10b',    'xxx 11xxxx'),
                (3,    'x^ 9b',     'xxx 11xxx'),
                (3,    'x<+10b',    '+11xxxxxxx'),
                (3,    'x>+10b',    'xxxxxxx+11'),
                (3,    'x=+10b',    '+xxxxxxx11'),
                (3,    'x^+10b',    'xxx+11xxxx'),
                (3,    'x^+9b',     'xxx+11xxx'),
                (3,    'x<-10b',    '11xxxxxxxx'),
                (3,    'x>-10b',    'xxxxxxxx11'),
                (3,    'x=-10b',    'xxxxxxxx11'),
                (3,    'x^-10b',    'xxxx11xxxx'),
                (3,    'x^-9b',     'xxx11xxxx'),
                (-3,   'x<-10b',    '-11xxxxxxx'),
                (-3,   'x>-10b',    'xxxxxxx-11'),
                (-3,   'x=-10b',    '-xxxxxxx11'),
                (-3,   'x^-10b',    'xxx-11xxxx'),
                (-3,   'x^-9b',     'xxx-11xxx'),
                (-3,   'x<+10b',    '-11xxxxxxx'),
                (-3,   'x>+10b',    'xxxxxxx-11'),
                (-3,   'x=+10b',    '-xxxxxxx11'),
                (-3,   'x^+10b',    'xxx-11xxxx'),
                (-3,   'x^+9b',     'xxx-11xxx'),
                (-3,   'x< 10b',    '-11xxxxxxx'),
                (-3,   'x> 10b',    'xxxxxxx-11'),
                (-3,   'x= 10b',    '-xxxxxxx11'),
                (-3,   'x^ 10b',    'xxx-11xxxx'),
                (-3,   'x^ #10b',   'xx-0b11xxx'),
                (-3,   'x^ 9b',     'xxx-11xxx'),
                (3,    '\0^ 9b',    '    11   '),
                (-2147483648, 'b',  '-10000000000000000000000000000000'),
                (0,      'b',  '0'),

                (9,    'o',         '11'),
                (9,    '+o',        '+11'),
                (9,    '-o',        '11'),
                (9,    ' o',        ' 11'),
                (9,    '<5o',       '11   '),
                (9,    '>5o',       '   11'),
                (9,    '=5o',       '   11'),
                (9,    '^6o',       '  11  '),
                (9,    '6o',        '    11'),
                (9,    'x< 10o',    ' 11xxxxxxx'),
                (9,    'x> 10o',    'xxxxxxx 11'),
                (9,    'x= 10o',    ' xxxxxxx11'),
                (9,    'x^ 10o',    'xxx 11xxxx'),
                (9,    'x^ 9o',     'xxx 11xxx'),
                (9,    'x<+10o',    '+11xxxxxxx'),
                (9,    'x>+10o',    'xxxxxxx+11'),
                (9,    'x=+10o',    '+xxxxxxx11'),
                (9,    'x^+10o',    'xxx+11xxxx'),
                (9,    'x^+9o',     'xxx+11xxx'),
                (9,    'x<-10o',    '11xxxxxxxx'),
                (9,    'x>-10o',    'xxxxxxxx11'),
                (9,    'x=-10o',    'xxxxxxxx11'),
                (9,    'x^-10o',    'xxxx11xxxx'),
                (9,    'x^-9o',     'xxx11xxxx'),
                (-9,   'x<-10o',    '-11xxxxxxx'),
                (-9,   'x>-10o',    'xxxxxxx-11'),
                (-9,   'x=-10o',   '-xxxxxxx11'),
                (-9,   'x^-10o',    'xxx-11xxxx'),
                (-9,   'x^-9o',     'xxx-11xxx'),
                (-9,   'x<+10o',    '-11xxxxxxx'),
                (-9,   'x>+10o',    'xxxxxxx-11'),
                (-9,   'x=+10o',   '-xxxxxxx11'),
                (-9,   'x^+10o',    'xxx-11xxxx'),
                (-9,   'x^+9o',     'xxx-11xxx'),
                (-9,   'x< 10o',    '-11xxxxxxx'),
                (-9,   'x< #10o',    '-0o11xxxxx'),
                (-9,   'x> 10o',    'xxxxxxx-11'),
                (-9,   'x= 10o',   '-xxxxxxx11'),
                (-9,   'x^ 10o',    'xxx-11xxxx'),
                (-9,   'x^ 9o',     'xxx-11xxx'),
                (9,    '\0^ 9o',    '    11   '),
                (-9,   'x^ 9o',     'xxx-11xxx'),

                (-2147483648, 'o',  '-20000000000'),
                (-42,         'o',  '-52'),
                (42,          'o',  '52'),
                (0,           'o',  '0'),

                (-2147483648, 'X',  '-80000000'),
                (-2147483648, 'x',  '-80000000'),
                (-42,         'X',  '-2A'),
                (-42,         'x',  '-2a'),
                (42,          'X',  '2A'),
                (42,          'x',  '2a'),
                (2147483647,  'X',  '7FFFFFFF'),
                (0,           'x',  '0'),
                (2147483647,  'x',  '7fffffff'),
                (2147483647,  '#x',  '0x7fffffff'),
                (2147483647,  '#X',  '0X7FFFFFFF'),

                (2147483647, 'f',  '2147483647.000000'),
                (2147483647, '%', '214748364700.000000%'),

                (999999,     '-g',  '999999'),
                (999999,     '+g',  '+999999'),
                (999999,     ' g',  ' 999999'),
                (999999,     'g',  '999999'),
                (999999 ,    'G',  '999999'),
                (-999999,    'g',  '-999999'),
                (-999999 ,   'G',  '-999999'),
                (100000,     'g',  '100000'),
                (100000,     'G',  '100000'),
                (-1000000,   'g',  '-1e+06'),
                (-1000000,   'G',  '-1E+06'),
                (1000000,    'g',  '1e+06'),
                (1000000,    'G',  '1E+06'),
                (10000000,   'g',  '1e+07'),
                (10000000,   'G',  '1E+07'),
                (100000000,  'g',  '1e+08'),
                (100000000,  'G',  '1E+08'),
                (1000000,    '10g',  '     1e+06'),
                (1000000,    '10G',  '     1E+06'),
                (10000000,   '10g',  '     1e+07'),
                (10000000,   '10G',  '     1E+07'),
                (100000000,  '10g',  '     1e+08'),
                (100000000,  '10G',  '     1E+08'),
                (110000000,  'g',  '1.1e+08'),
                (110000000,  'G',  '1.1E+08'),
                (112000000,  'g',  '1.12e+08'),
                (112000000,  'G',  '1.12E+08'),
                (112300000,  'g',  '1.123e+08'),
                (112300000,  'G',  '1.123E+08'),
                (112340000,  'g',  '1.1234e+08'),
                (112340000,  'G',  '1.1234E+08'),
                (112345000,  'g',  '1.12345e+08'),
                (112345000,  'G',  '1.12345E+08'),
                (112345600,  'g',  '1.12346e+08'),
                (112345600,  'G',  '1.12346E+08'),
                (112345500,  'g',  '1.12346e+08'),
                (112345500,  'G',  '1.12346E+08'),
                (112345510,  'g',  '1.12346e+08'),
                (112345510,  'G',  '1.12346E+08'),
                (112345400,  'g',  '1.12345e+08'),
                (112345400,  'G',  '1.12345E+08'),
                (112345401,  'g',  '1.12345e+08'),
                (112345401,  'G',  '1.12345E+08'),
                (-112345000,  'g',  '-1.12345e+08'),
                (-112345000,  'G',  '-1.12345E+08'),
                (-112345600,  'g',  '-1.12346e+08'),
                (-112345600,  'G',  '-1.12346E+08'),
                (-112345500,  'g',  '-1.12346e+08'),
                (-112345500,  'G',  '-1.12346E+08'),
                (-112345510,  'g',  '-1.12346e+08'),
                (-112345510,  'G',  '-1.12346E+08'),
                (-112345400,  'g',  '-1.12345e+08'),
                (-112345400,  'G',  '-1.12345E+08'),
                (-112345401,  'g',  '-1.12345e+08'),
                (-112345401,  'G',  '-1.12345E+08'),

                (2147483647, 'g',  '2.14748e+09'),
                (2147483647, 'G',  '2.14748E+09'),
                (-2147483647, 'g',  '-2.14748e+09'),
                (-2147483647, 'G',  '-2.14748E+09'),

                (2147483647, 'e',  '2.147484e+09'),
                (100000,     'e',  '1.000000e+05'),
                (100000,     'E',  '1.000000E+05'),
                (10000000,   'E',  '1.000000E+07'),
                (100000000,  'e',  '1.000000e+08'),
                (100000000,  'E',  '1.000000E+08'),
                (110000000,  'e',  '1.100000e+08'),
                (110000000,  'E',  '1.100000E+08'),
                (112000000,  'e',  '1.120000e+08'),
                (112000000,  'E',  '1.120000E+08'),
                (112300000,  'e',  '1.123000e+08'),
                (112300000,  'E',  '1.123000E+08'),
                (112340000,  'e',  '1.123400e+08'),
                (112340000,  'E',  '1.123400E+08'),
                (112345000,  'e',  '1.123450e+08'),
                (112345000,  'E',  '1.123450E+08'),
                (1112345600, 'e',  '1.112346e+09'),
                (1112345600, 'E',  '1.112346E+09'),
                (1112345500, 'e',  '1.112346e+09'),
                (1112345500, 'E',  '1.112346E+09'),
                (1112345510, 'e',  '1.112346e+09'),
                (1112345510, 'E',  '1.112346E+09'),
                (1112345400, 'e',  '1.112345e+09'),
                (1112345400, 'E',  '1.112345E+09'),
                (1112345401, 'e',  '1.112345e+09'),
                (1112345401, 'E',  '1.112345E+09'),

                (100000,     'n',  '100000'),
                ]

        for value, spec, result in tests:
            self.assertEqual(value.__format__(spec), result)

        # check locale specific formatting
        import _locale
        try:
            if is_cli:
                _locale.setlocale(_locale.LC_ALL, 'en_US')
            else:
                _locale.setlocale(_locale.LC_ALL, 'English_United States.1252')
            x = 100000
            self.assertEqual(x.__format__('n'), '100,000')
        finally:
            # and restore it back...
            _locale.setlocale(_locale.LC_ALL, 'C')
            self.assertEqual(x.__format__('n'), '100000')

    def test_int___format___errors(self):
        errors = [
                    (ValueError, 2, '6.1', "Precision not allowed in integer format specifier"),
                    (ValueError, 2, '+c', "Sign not allowed with integer format specifier 'c'"),
                    (ValueError, 2, '-c', "Sign not allowed with integer format specifier 'c'"),
                    (ValueError, 2, ' c', "Sign not allowed with integer format specifier 'c'"),
                    (OverflowError, -2, 'c', "%c arg not in range(0x10000)"),
                    #(-2, 'c', ),
                    #(-2, '%', "Sign not allowed with integer format specifier 'c'"),
                ]

        okChars = set(['%', 'E', 'F', 'G', 'X', 'x', 'b', 'c', 'd', 'o', 'e', 'f', 'g', 'n', ','])

        # verify the okChars are actually ok
        for char in okChars:
            (2).__format__('10' + char)

        for char in allChars:
            if char not in okChars and (char < '0' or char > '9'):
                errors.append((ValueError, 2, '10' + char, "Unknown format code '%s'" % char))

        for error, value, errorFmt, errorMsg in errors:
            self.assertRaisesPartialMessage(error, errorMsg, value.__format__, errorFmt)

    def test_long___format__(self):
        tests = [
                (long(0),    '+',        '+0'),
                (long(0),    ' ',        ' 0'),
                (long(0),    '-',        '0'),
                (long(2),    '',         '2'),
                (long(2),    '+',        '+2'),
                (long(2),    '-',        '2'),
                (long(2),    ' ',        ' 2'),
                (long(2),    '<5',       '2    '),
                (long(2),    '>5',       '    2'),
                (long(2),    '=5',       '    2'),
                (long(2),    '^6',       '  2   '),
                (long(2),    '6',        '     2'),
                (long(2),    'x< 10',    ' 2xxxxxxxx'),
                (long(2),    'x> 10',    'xxxxxxxx 2'),
                (long(2),    'x= 10',    ' xxxxxxxx2'),
                (long(2),    'x^ 10',    'xxxx 2xxxx'),
                (long(2),    'x^ 9',     'xxx 2xxxx'),
                (long(2),    'x<+10',    '+2xxxxxxxx'),
                (long(2),    'x>+10',    'xxxxxxxx+2'),
                (long(2),    'x=+10',    '+xxxxxxxx2'),
                (long(2),    'x^+10',    'xxxx+2xxxx'),
                (long(2),    'x^+9',     'xxx+2xxxx'),
                (long(2),    'x<-10',    '2xxxxxxxxx'),
                (long(2),    'x>-10',    'xxxxxxxxx2'),
                (long(2),    'x=-10',    'xxxxxxxxx2'),
                (long(2),    'x^-10',    'xxxx2xxxxx'),
                (long(2),    'x^-9',     'xxxx2xxxx'),
                (-long(2),   'x<-10',    '-2xxxxxxxx'),
                (-long(2),   'x>-10',    'xxxxxxxx-2'),
                (-long(2),   'x=-10',   '-xxxxxxxx2'),
                (-long(2),   'x^-10',    'xxxx-2xxxx'),
                (-long(2),   'x^-9',     'xxx-2xxxx'),
                (-long(2),   'x<+10',    '-2xxxxxxxx'),
                (-long(2),   'x>+10',    'xxxxxxxx-2'),
                (-long(2),   'x=+10',   '-xxxxxxxx2'),
                (-long(2),   'x^+10',    'xxxx-2xxxx'),
                (-long(2),   'x^+9',     'xxx-2xxxx'),
                (-long(2),   'x< 10',    '-2xxxxxxxx'),
                (-long(2),   'x> 10',    'xxxxxxxx-2'),
                (-long(2),   'x= 10',   '-xxxxxxxx2'),
                (-long(2),   'x^ 10',    'xxxx-2xxxx'),
                (-long(2),   'x^ 9',     'xxx-2xxxx'),
                (long(2),    '\0^ 9',    '    2    '),

                (long(2),    'c',         '\x02'),
                (long(2),    '<5c',       '\x02    '),
                (long(2),    '>5c',       '    \x02'),
                (long(2),    '=5c',       '    \x02'),
                (long(2),    '^6c',       '  \x02   '),
                (long(2),    '6c',        '     \x02'),

                (long(3),    'b',         '11'),
                (long(3),    '+b',        '+11'),
                (long(3),    '-b',        '11'),
                (long(3),    ' b',        ' 11'),
                (long(3),    '<5b',       '11   '),
                (long(3),    '>5b',       '   11'),
                (long(3),    '=5b',       '   11'),
                (long(3),    '^6b',       '  11  '),
                (long(3),    '6b',        '    11'),
                (long(3),    'x< 010b',   ' 11xxxxxxx'),
                (long(3),    '< 010b',    ' 110000000'),
                (long(3),    'x< 010b',    ' 11xxxxxxx'),
                (long(3),    'x< 10b',    ' 11xxxxxxx'),
                (long(3),    'x< 10b',    ' 11xxxxxxx'),
                (long(3),    'x> 10b',    'xxxxxxx 11'),
                (long(3),    'x= 10b',    ' xxxxxxx11'),
                (long(3),    'x^ 10b',    'xxx 11xxxx'),
                (long(3),    'x^ 9b',     'xxx 11xxx'),
                (long(3),    'x<+10b',    '+11xxxxxxx'),
                (long(3),    'x>+10b',    'xxxxxxx+11'),
                (long(3),    'x=+10b',    '+xxxxxxx11'),
                (long(3),    'x^+10b',    'xxx+11xxxx'),
                (long(3),    'x^+9b',     'xxx+11xxx'),
                (long(3),    'x<-10b',    '11xxxxxxxx'),
                (long(3),    'x>-10b',    'xxxxxxxx11'),
                (long(3),    'x=-10b',    'xxxxxxxx11'),
                (long(3),    'x^-10b',    'xxxx11xxxx'),
                (long(3),    'x^-9b',     'xxx11xxxx'),
                (-long(3),   'x<-10b',    '-11xxxxxxx'),
                (-long(3),   'x>-10b',    'xxxxxxx-11'),
                (-long(3),   'x=-10b',    '-xxxxxxx11'),
                (-long(3),   'x^-10b',    'xxx-11xxxx'),
                (-long(3),   'x^-9b',     'xxx-11xxx'),
                (-long(3),   'x<+10b',    '-11xxxxxxx'),
                (-long(3),   'x>+10b',    'xxxxxxx-11'),
                (-long(3),   'x=+10b',    '-xxxxxxx11'),
                (-long(3),   'x^+10b',    'xxx-11xxxx'),
                (-long(3),   'x^+9b',     'xxx-11xxx'),
                (-long(3),   'x< 10b',    '-11xxxxxxx'),
                (-long(3),   'x> 10b',    'xxxxxxx-11'),
                (-long(3),   'x= 10b',    '-xxxxxxx11'),
                (-long(3),   'x^ 10b',    'xxx-11xxxx'),
                (-long(3),   'x^ #10b',   'xx-0b11xxx'),
                (-long(3),   'x^ 9b',     'xxx-11xxx'),
                (long(3),    '\0^ 9b',    '    11   '),
                (-long(2147483648), 'b',  '-10000000000000000000000000000000'),
                (long(0),      'b',  '0'),

                (long(9),    'o',         '11'),
                (long(9),    '+o',        '+11'),
                (long(9),    '-o',        '11'),
                (long(9),    ' o',        ' 11'),
                (long(9),    '<5o',       '11   '),
                (long(9),    '>5o',       '   11'),
                (long(9),    '=5o',       '   11'),
                (long(9),    '^6o',       '  11  '),
                (long(9),    '6o',        '    11'),
                (long(9),    'x< 10o',    ' 11xxxxxxx'),
                (long(9),    'x> 10o',    'xxxxxxx 11'),
                (long(9),    'x= 10o',    ' xxxxxxx11'),
                (long(9),    'x^ 10o',    'xxx 11xxxx'),
                (long(9),    'x^ 9o',     'xxx 11xxx'),
                (long(9),    'x<+10o',    '+11xxxxxxx'),
                (long(9),    'x>+10o',    'xxxxxxx+11'),
                (long(9),    'x=+10o',    '+xxxxxxx11'),
                (long(9),    'x^+10o',    'xxx+11xxxx'),
                (long(9),    'x^+9o',     'xxx+11xxx'),
                (long(9),    'x<-10o',    '11xxxxxxxx'),
                (long(9),    'x>-10o',    'xxxxxxxx11'),
                (long(9),    'x=-10o',    'xxxxxxxx11'),
                (long(9),    'x^-10o',    'xxxx11xxxx'),
                (long(9),    'x^-9o',     'xxx11xxxx'),
                (-long(9),   'x<-10o',    '-11xxxxxxx'),
                (-long(9),   'x>-10o',    'xxxxxxx-11'),
                (-long(9),   'x=-10o',   '-xxxxxxx11'),
                (-long(9),   'x^-10o',    'xxx-11xxxx'),
                (-long(9),   'x^-9o',     'xxx-11xxx'),
                (-long(9),   'x<+10o',    '-11xxxxxxx'),
                (-long(9),   'x>+10o',    'xxxxxxx-11'),
                (-long(9),   'x=+10o',   '-xxxxxxx11'),
                (-long(9),   'x^+10o',    'xxx-11xxxx'),
                (-long(9),   'x^+9o',     'xxx-11xxx'),
                (-long(9),   'x< 10o',    '-11xxxxxxx'),
                (-long(9),   'x< #10o',    '-0o11xxxxx'),
                (-long(9),   'x> 10o',    'xxxxxxx-11'),
                (-long(9),   'x= 10o',   '-xxxxxxx11'),
                (-long(9),   'x^ 10o',    'xxx-11xxxx'),
                (-long(9),   'x^ 9o',     'xxx-11xxx'),
                (long(9),    '\0^ 9o',    '    11   '),
                (-long(9),   'x^ 9o',     'xxx-11xxx'),

                (-long(2147483648), 'o',  '-20000000000'),
                (-long(42),         'o',  '-52'),
                (0,            'o',  '0'),
                (long(42),          'o',  '52'),

                (0,            'x',  '0'),
                (-long(2147483648), 'X',  '-80000000'),
                (-long(2147483648), 'x',  '-80000000'),
                (-long(42),         'X',  '-2A'),
                (-long(42),         'x',  '-2a'),
                (long(42),          'X',  '2A'),
                (long(42),          'x',  '2a'),
                (long(2147483647),  'X',  '7FFFFFFF'),
                (long(2147483647),  'x',  '7fffffff'),
                (long(2147483647),  '#x',  '0x7fffffff'),
                (long(2147483647),  '#X',  '0X7FFFFFFF'),

                (long(2147483647), 'f',  '2147483647.000000'),
                (long(2147483647), '%', '214748364700.000000%'),

                (long(999999),     '-g',  '999999'),
                (long(999999),     '+g',  '+999999'),
                (long(999999),     ' g',  ' 999999'),
                (long(999999),     'g',  '999999'),
                (long(999999),    'G',  '999999'),
                (-long(999999),    'g',  '-999999'),
                (-long(999999),   'G',  '-999999'),
                (long(100000),     'g',  '100000'),
                (long(100000),     'G',  '100000'),
                (-long(1000000),   'g',  '-1e+06'),
                (-long(1000000),   'G',  '-1E+06'),
                (long(1000000),    'g',  '1e+06'),
                (long(1000000),    'G',  '1E+06'),
                (long(10000000),   'g',  '1e+07'),
                (long(10000000),   'G',  '1E+07'),
                (long(100000000),  'g',  '1e+08'),
                (long(100000000),  'G',  '1E+08'),
                (long(1000000),    '10g',  '     1e+06'),
                (long(1000000),    '10G',  '     1E+06'),
                (long(10000000),   '10g',  '     1e+07'),
                (long(10000000),   '10G',  '     1E+07'),
                (long(10200000),   '10G',  '  1.02E+07'),
                (long(100000000),  '10g',  '     1e+08'),
                (long(100000000),  '10G',  '     1E+08'),
                (long(110000000),  'g',  '1.1e+08'),
                (long(110000000),  'G',  '1.1E+08'),
                (long(112000000),  'g',  '1.12e+08'),
                (long(112000000),  'G',  '1.12E+08'),
                (long(112300000),  'g',  '1.123e+08'),
                (long(112300000),  'G',  '1.123E+08'),
                (long(112340000),  'g',  '1.1234e+08'),
                (long(112340000),  'G',  '1.1234E+08'),
                (long(112345000),  'g',  '1.12345e+08'),
                (long(112345000),  'G',  '1.12345E+08'),
                (long(112345600),  'g',  '1.12346e+08'),
                (long(112345600),  'G',  '1.12346E+08'),
                (long(112345500),  'g',  '1.12346e+08'),
                (long(112345500),  'G',  '1.12346E+08'),
                (long(112345510),  'g',  '1.12346e+08'),
                (long(112345510),  'G',  '1.12346E+08'),
                (long(112345400),  'g',  '1.12345e+08'),
                (long(112345400),  'G',  '1.12345E+08'),
                (long(112345401),  'g',  '1.12345e+08'),
                (long(112345401),  'G',  '1.12345E+08'),
                (-long(112345000),  'g',  '-1.12345e+08'),
                (-long(112345000),  'G',  '-1.12345E+08'),
                (-long(112345600),  'g',  '-1.12346e+08'),
                (-long(112345600),  'G',  '-1.12346E+08'),
                (-long(112345500),  'g',  '-1.12346e+08'),
                (-long(112345500),  'G',  '-1.12346E+08'),
                (-long(112345510),  'g',  '-1.12346e+08'),
                (-long(112345510),  'G',  '-1.12346E+08'),
                (-long(112345400),  'g',  '-1.12345e+08'),
                (-long(112345400),  'G',  '-1.12345E+08'),
                (-long(112345401),  'g',  '-1.12345e+08'),
                (-long(112345401),  'G',  '-1.12345E+08'),

                (long(2147483647), 'g',  '2.14748e+09'),
                (long(2147483647), 'G',  '2.14748E+09'),
                (-long(2147483647), 'g',  '-2.14748e+09'),
                (-long(2147483647), 'G',  '-2.14748E+09'),

                (long(2147483647), 'e',  '2.147484e+09'),
                (long(100000),     'e',  '1.000000e+05'),
                (long(100000),     'E',  '1.000000E+05'),
                (long(10000000),   'E',  '1.000000E+07'),
                (long(100000000),  'e',  '1.000000e+08'),
                (long(100000000),  'E',  '1.000000E+08'),
                (long(110000000),  'e',  '1.100000e+08'),
                (long(110000000),  'E',  '1.100000E+08'),
                (long(112000000),  'e',  '1.120000e+08'),
                (long(112000000),  'E',  '1.120000E+08'),
                (long(112300000),  'e',  '1.123000e+08'),
                (long(112300000),  'E',  '1.123000E+08'),
                (long(112340000),  'e',  '1.123400e+08'),
                (long(112340000),  'E',  '1.123400E+08'),
                (long(112345000),  'e',  '1.123450e+08'),
                (long(112345000),  'E',  '1.123450E+08'),
                (long(1112345600), 'e',  '1.112346e+09'),
                (long(1112345600), 'E',  '1.112346E+09'),
                (long(1112345500), 'e',  '1.112346e+09'),
                (long(1112345500), 'E',  '1.112346E+09'),
                (long(1112345510), 'e',  '1.112346e+09'),
                (long(1112345510), 'E',  '1.112346E+09'),
                (long(1112345400), 'e',  '1.112345e+09'),
                (long(1112345400), 'E',  '1.112345E+09'),
                (long(1112345401), 'e',  '1.112345e+09'),
                (long(1112345401), 'E',  '1.112345E+09'),
                (long(111234540100), 'E',  '1.112345E+11'),

                (long(100000),     'n',  '100000'),
                ]

        for value, spec, result in tests:
            self.assertEqual(value.__format__(spec), result)

        # check locale specific formatting
        import _locale
        try:
            if is_cli:
                _locale.setlocale(_locale.LC_ALL, 'en_US')
            else:
                _locale.setlocale(_locale.LC_ALL, 'English_United States.1252')
            self.assertEqual(long(100000).__format__('n'), '100,000')
            self.assertEqual(long(100000000).__format__('n'), '100,000,000')
        finally:
            # and restore it back...
            _locale.setlocale(_locale.LC_ALL, 'C')
            self.assertEqual(long(100000).__format__('n'), '100000')
            self.assertEqual(long(100000000).__format__('n'), '100000000')

    def test_long___format___errors(self):
        errors = [
                    (ValueError, long(2), '6.1', "Precision not allowed in integer format specifier"),
                    (ValueError, long(2), '+c', "Sign not allowed with integer format specifier 'c'"),
                    (ValueError, long(2), '-c', "Sign not allowed with integer format specifier 'c'"),
                    (ValueError, long(2), ' c', "Sign not allowed with integer format specifier 'c'"),
                    (OverflowError, -long(2), 'c', "%c arg not in range(0x10000)"),
                    (OverflowError, long(1000000), 'c', "%c arg not in range(0x10000)"),
                ]

        if is_cli: #http://ironpython.codeplex.com/workitem/28373
            errors.append((OverflowError, sys.maxsize + 1, 'c', "long int too large to convert to int"))
        else:
            errors.append((OverflowError, sys.maxsize + 1, 'c', "Python int too large to convert to C long"))

        okChars = set(['%', 'E', 'F', 'G', 'X', 'x', 'b', 'c', 'd', 'o', 'e', 'f', 'g', 'n', ','])

        # verify the okChars are actually ok
        for char in okChars:
            (long(2)).__format__('10' + char)

        for char in allChars:
            if char not in okChars and (char < '0' or char > '9'):
                errors.append((ValueError, long(2), '10' + char, "Unknown format code '%s'" % char))

        for exceptionType, value, errorFmt, errorMsg in errors:
            self.assertRaisesPartialMessage(exceptionType, errorMsg, value.__format__, errorFmt)


    def test_builtin_types_that_implement_format(self):
        import builtins
        types = [getattr(builtins, typeName) for typeName in dir(builtins) if type(getattr(builtins, typeName)) is type]
        formatTypes = list(set([builtinType.__name__ for builtinType in types if '__format__' in builtinType.__dict__]))

        formatTypes.sort()
        if is_cli:
            # why does bool have __format__ in ipy?
            self.assertEqual(formatTypes, ['bool', 'complex', 'float', 'int', 'object', 'str'])
        else:
            self.assertEqual(formatTypes, ['complex', 'float', 'int', 'object', 'str'])


    def test_computed_format(self):
        self.assertEqual("|{0:10}|".format("a"), "|a         |")
        self.assertEqual("|{0:*^10}|".format("a"), "|****a*****|")
        self.assertEqual("|{0:*^{1}}|".format("a", 10), "|****a*****|")
        self.assertEqual("{0:*{2}10}".format("a", "*", "^", "10"), "****a*****")
        self.assertEqual("{0:{1}^{3}}".format("a", "*", "^", "10"), "****a*****")
        self.assertEqual("{0:{1}{2}{3}}".format("a", "*", "^", "10"), "****a*****")
        self.assertEqual("{0:{1}*^{2}}".format("a", "", "10"), "****a*****")

    def test_none_format(self):
        self.assertEqual("{0} {1}".format(None, 10), "None 10")
        self.assertEqual("{0}".format(None), 'None')

run_test(__name__)
