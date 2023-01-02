# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import _string
import sys
import _locale

from iptest import IronPythonTestCase, is_cli, is_cpython, is_netcoreapp21, big, run_test, skipUnlessIronPython

allChars = ''
for y in [chr(x) for x in range(256) if chr(x) != '[' and chr(x) != '.']:
    allChars += y

class TestException(Exception): pass

class bad_str(object):
    def __str__(self):
        raise TestException('booh')

class StrFormatTest(IronPythonTestCase):

    def setUp(self):
        super().setUp()
        self.saved_lc = [(lc, _locale.setlocale(lc, None)) for lc in
                            (getattr(_locale, lc_name) for lc_name in
                                dir(_locale) if lc_name.startswith('LC_') and lc_name != 'LC_ALL')
                        ]
    _locale.setlocale(_locale.LC_ALL, 'C')

    def tearDown(self):
        for lc, setting in self.saved_lc:
            _locale.setlocale(lc, setting)
        super().tearDown()

    def test_formatter_parser_errors(self):
        errors = [ ("{0!",             "unmatched '{' in format spec" if is_cli else "end of string while looking for conversion specifier"),
                ("}a{",             "Single '}' encountered in format string"),
                ("{0:0.1a",         "unmatched '{' in format spec"),
                ("{0:{}",           "unmatched '{' in format spec"),
                ("{0:aa{ab}",       "unmatched '{' in format spec"),
                ("{0!}",            "end of format while looking for conversion specifier" if is_cli else "unmatched '{' in format spec"),
                ("{0!",             "unmatched '{' in format spec" if is_cli else "end of string while looking for conversion specifier"),
                ("{0!aa}",          "expected ':' after format specifier" if is_cli else "expected ':' after conversion specifier"),
                ("{0.{!:{.}.}}",    "expected ':' after format specifier" if is_cli else "unexpected '{' in field name"),
                ("{",               "Single '{' encountered in format string"),
                ]

        if is_cli: # https://github.com/IronLanguages/ironpython3/issues/867
            errors += [
                ("{0!}}",           "end of format while looking for conversion specifier"),
            ]
        else:
            _string.formatter_parser("{0!}}")

        for format, errorMsg in errors:
            self.assertRaisesMessage(ValueError, errorMsg, list, _string.formatter_parser(format))

    def test_formatter_parser(self):
        tests = [ ('{0.}',             [('', '0.', '', None)]),
                ('{0:.{abc}}',       [('', '0', '.{abc}', None)]),
                ('{0[]}',            [('', '0[]', '', None)]),
                ('{0!!}',            [('', '0', '', '!')]),
                ('{0:::!!::!::}',    [('', '0', '::!!::!::', None)]),
                ('{0.]}',            [('', '0.]', '', None)]),
                ('{0..}',            [('', '0..', '', None)]),
                ('{{',               [('{', None, None, None)]),
                ('}}',               [('}', None, None, None)]),
                ('{{}}',             [('{', None, None, None), ('}', None, None, None)]),
                ]

        for format, expected in tests:
            self.assertEqual(list(_string.formatter_parser(format)), expected)

        tests = [
                ('{0.{:{.}.}}',      [('', '0.{', '{.}.}', None)]),
                ('{0.{:.}.}',        [('', '0.{', '.}.', None)]),
                ('{0.{!.:{.}.}}',    [('', '0.{', '{.}.}', '.')]),
                ('{0.{!!:{.}.}}',    [('', '0.{', '{.}.}', '!')]),
                ('{0[}',             [('', '0[', '', None)]),
                ('{0.[}',            [('', '0.[', '', None)]),
        ]

        for format, expected in tests:
            if is_cli: # https://github.com/IronLanguages/ironpython3/issues/867
                self.assertEqual(list(_string.formatter_parser(format)), expected)
            else:
                with self.assertRaises(ValueError):
                    list(_string.formatter_parser(format))

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
        tests = [ ('0',                [big(0), []]),
                ('abc.foo',          ['abc', [(True, 'foo')]]),
                ('abc[2]',           ['abc', [(False, big(2))]]),
                ('1[2]',             [big(1), [(False, big(2))]]),
                ('1.abc',            [big(1), [(True, 'abc')]]),
                ('abc 2.abc',        ['abc 2', [(True, 'abc')]]),
                ('abc!2.abc',        ['abc!2', [(True, 'abc')]]),
                ('].abc',            [']', [(True, 'abc')]]),
                ("abc[[]",           ['abc', [(False, '[')]] ),
                ("abc[[[]",          ['abc', [(False, '[[')]] ),
                ]

        if not is_cpython: #http://ironpython.codeplex.com/workitem/28331
            tests.append(("abc[2]#x",         ['abc', [(False, big(2))]] ))
        tests.append([allChars, [allChars, []]])
        tests.append([allChars + '.foo', [allChars, [(True, 'foo')]]])
        tests.append([allChars + '[2]', [allChars, [(False, big(2))]]])

        for format, expected in tests:
            res = list(_string.formatter_field_name_split(format))
            res[1] = list(res[1])
            self.assertEqual(res, expected)

    def test_format_arg_errors(self):
        self.assertRaises(IndexError, '{0}'.format)
        self.assertRaisesMessage(ValueError, "Empty attribute in format string", '{0.}'.format, 42)
        self.assertRaisesMessage(ValueError, "Empty attribute in format string", '{0[]}'.format, 42)
        self.assertRaisesMessage(ValueError, "Missing ']' in format string" if is_cli else "expected '}' before end of string", '{0[}'.format, 42)
        self.assertRaises(IndexError if is_cli else ValueError, '{0[}'.format)

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

        self.assertRaisesMessage(TypeError, "bad.__format__ must return a str, not NoneType" if is_cli else "__format__ method did not return string", '{0}'.format, bad())
        self.assertRaisesMessage(TypeError, "bad2.__format__ must return a str, not int" if is_cli else "__format__ method did not return string", '{0}'.format, bad2())

        self.assertRaisesMessage(ValueError, "Unknown conversion specifier x", '{0!x}'.format, 'abc')

        self.assertRaisesMessage(TypeError, "bad.__format__ must return a str, not NoneType" if is_cli else "__format__ method did not return string", format, bad())
        self.assertRaisesMessage(TypeError, "bad2.__format__ must return a str, not int" if is_cli else "__format__ method did not return string", format, bad2())

    def test_object__format__(self):
        self.assertEqual(object.__format__("aaa", ""), "aaa")

        with self.assertRaises(TypeError):
            object.__format__("aaa", "6") # TypeError: non-empty format string passed to object.__format__

    def test_str___format__(self):
        x = "abc"

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
                ('\0<6', 'abc\0\0\0'),
                ('.0',   ''),
                ('.1',   'a'),
                ('5.2',  'ab   '),
                ('5.2s', 'ab   '),
                ]

        for spec, result in tests:
            self.assertEqual(str.__format__(x, spec), result)

    def test_str___format___errors(self):
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
                x = ord(char)
                if char==',':
                    errors.append(('10' + char, "Cannot specify ',' with 's'."))
                elif 0x20 < x < 0x80:
                    errors.append(('10' + char, "Unknown format code '%s' for object of type 'str'" % char))
                else:
                    errors.append(('10' + char, "Unknown format code '\\x%x' for object of type 'str'" % x))

        for errorFmt, errorMsg in errors:
            self.assertRaisesMessage(ValueError, errorMsg, str().__format__, errorFmt)

        if is_cli or sys.version_info >= (3, 5):
            self.assertRaisesMessage(TypeError, 'unsupported format string passed to bad_str.__format__', bad_str().__format__, '+')
        else:
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
                        (23.0,             '.1',       '2e+01'),
                        (23.0,             '.2',       '2.3e+01'),
                        (230.5,            '.3',       '2.3e+02'),
                        (11230.54,         '.5',       '1.1231e+04'),
                        (111230.54,        '.1',       '1e+05'),
                        (100000.0,           '.5',     '1e+05'),
                        (0.0,              '1.1',      '0e+00'),
                        (0.0,              '1.0',      '0e+00'),
                        (1.0,              '.0',       '1e+00'),
                        (1.1,              '.0',       '1e+00'),
                        (1.1,              '.1',       '1e+00'),
                        (10.0,             '.1',       '1e+01'),
                        (10.0,             '.0',       '1e+01'),
                        (1000000000.12,    '1.10',     '1e+09'),
                        (1000000000.12,    '1.3',      '1e+09'),
                        (999999999999.9,   '1.0',      '1e+12'),
                        (999999999999.9,   '1.2',      '1e+12'),
                    ]
        else:
            tests+= [   (2.0,              '6.1',      '   2.0'),
                        (2.5,              '6.1',      '   2.0'),
                        (2.25,             '6.1',      '   2.0'),
                        (23.0,             '.1',       '2.0e+01'),
                        (23.0,             '.2',       '23.0'),
                        (230.5,            '.3',       '230.0'),
                        (11230.54,         '.5',       '11231.0'),
                        (111230.54,        '.1',       '1.0e+05'),
                        (100000.0,           '.5',     '1.0e+05'),
                        (0.0,              '1.1',      '0.0'),
                        (0.0,              '1.0',      '0.0'),
                        (1.0,              '.0',       '1.0'),
                        (1.1,              '.0',       '1.0'),
                        (1.1,              '.1',       '1.0'),
                        (10.0,             '.1',       '1.0e+01'),
                        (10.0,             '.0',       '1.0e+01'),
                        (1000000000.12,    '1.10',     '1000000000.0'),
                        (1000000000.12,    '1.3',      '1.0e+09'),
                        (999999999999.9,   '1.0',      '1.0e+12'),
                        (999999999999.9,   '1.2',      '1.0e+12'),
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
                    (2.0,              '\0^ 9.10', '\0\0 2.0\0\0\0'),
                    (2.23,             '6.2',      '   2.2'),
                    (2.25,             '6.2',      '   2.2'),
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

                    (23.0,             'f',        '23.000000'),
                    (23.0,             '.6f',      '23.000000'),
                    (23.0,             '.0f',      '23'),
                    (23.0,             '.1f',      '23.0'),
                    (23.0,             '.2f',      '23.00'),
                    (23.0,             '.3f',      '23.000'),
                    (23.0,             '.4f',      '23.0000'),
                    (230.0,            '.2f',      '230.00'),
                    (230.1,            '.2f',      '230.10'),
                    (230.5,            '.3f',      '230.500'),
                    (230.5,            '.4f',      '230.5000'),
                    (230.54,           '.4f',      '230.5400'),
                    (230.54,           '.5f',      '230.54000'),
                    (1230.54,          '.5f',      '1230.54000'),
                    (11230.54,         '.5f',      '11230.54000'),
                    (111230.54,        '.5f',      '111230.54000'),
                    (111230.54,        '.4f',      '111230.5400'),
                    (111230.54,        '.3f',      '111230.540'),
                    (111230.54,        '.2f',      '111230.54'),
                    (111230.54,        '.1f',      '111230.5'),
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
                    (230.5,            '.3g',       '230'),
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
                    (230.5,            '.3n',       '230'),
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
                    (100000000000.0,   '',         '100000000000.0'),
                    (1000000000000.0,  '',         '1000000000000.0'),
                    (1000000000000.0,  'g',         '1e+12'),
                    (-1000000000000.0, '',         '-1000000000000.0'),
                    (-1000000000000.0, 'g',        '-1e+12'),
                    (-1000000000000.0, 'G',        '-1E+12'),
                    (-1000000000000.0, '.1g',      '-1e+12'),
                    (-1000000000000.0, '.1G',      '-1E+12'),
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
                    (10e667,           '',         'inf'),
                    (-10e667,          '',         '-inf'),
                    (10e667/10e667,    '',         'nan'),
                    ]

        upper_spec_trans = str.maketrans({"f": "F", "e": "E", "g": "G"})

        for value, spec, result in tests:
            actual = value.__format__(spec)
            self.assertEqual(actual, result, "value:{0}, spec:{1}, (expected) result:{2}, actual:{3}, expr:({0}).__format__('{1}')".format(value, spec, result, actual))

            # upper-case version
            upper_spec = spec.translate(upper_spec_trans)
            if spec != upper_spec:
                actual = value.__format__(upper_spec)
                result = result.upper()
                self.assertEqual(actual, result, "value:{0}, spec:{1}, (expected) result:{2}, actual:{3}, expr:({0}).__format__('{1}')".format(value, upper_spec, result, actual))

        if is_netcoreapp21: return # https://github.com/IronLanguages/ironpython3/issues/751

        # check locale specific formatting
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

        okChars = set(['\0', '%', 'E', 'F', 'G', 'e', 'f', 'g', 'n', ','] + [chr(x) for x in range(ord('0'), ord('9') + 1)])
        # verify the okChars are actually ok
        for char in okChars:
            2.0.__format__('10' + char)

        for char in allChars:
            if char not in okChars:
                x = ord(char)
                if 0x20 < x < 0x80:
                    errors.append((2.0, '10' + char, "Unknown format code '%s' for object of type 'float'" % char))
                else:
                    errors.append((2.0, '10' + char, "Unknown format code '\\x%x' for object of type 'float'" % x))

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
                (2,    '\0^ 9',    '\0\0\0 2\0\0\0\0'),

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
                (3,    '\0^ 9b',    '\0\0\0 11\0\0\0'),
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
                (9,    '\0^ 9o',    '\0\0\0 11\0\0\0'),
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
                (999999,     'G',  '999999'),
                (-999999,    'g',  '-999999'),
                (-999999,    'G',  '-999999'),
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
                (10200000,   '10G',  '  1.02E+07'),
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
                (111234540100, 'E',  '1.112345E+11'),

                # thousands separator
                (1000,      ',d',       '1,000'),
                (1000,      '+,d',      '+1,000'),
                (1000,      '-,d',      '1,000'),
                (1000,      ' ,d',      ' 1,000'),
                (-1000,     ',d',       '-1,000'),
                (-1000,     '+,d',      '-1,000'),
                (-1000,     '-,d',      '-1,000'),
                (-1000,     ' ,d',      '-1,000'),

                # thousands separator (zero padded)
                (1000,      '08,d',    '0,001,000'),
                (1000,      '+08,d',   '+001,000'),
                (1000,      '-08,d',   '0,001,000'),
                (1000,      ' 08,d',   ' 001,000'),
                (-1000,     '08,d',    '-001,000'),
                (-1000,     '+08,d',   '-001,000'),
                (-1000,     '-08,d',   '-001,000'),
                (-1000,     ' 08,d',   '-001,000'),

                # float format with precision - https://github.com/IronLanguages/ironpython3/issues/1629
                (1,         '.0%',      '100%'),
                (1,         '.0e',      '1e+00'),
                (1,         '.0E',      '1E+00'),
                (1,         '.0f',      '1'),
                (1,         '.0F',      '1'),
                (1,         '.0g',      '1'),
                (1,         '.0G',      '1'),
                ]

        for value, spec, result in tests:
            self.assertEqual(value.__format__(spec), result)
            self.assertEqual(big(value).__format__(spec), result)

        locale_tests = [
            (1000,          'n',    '1000',         '1,000'),
            (-1000,         'n',    '-1000',        '-1,000'),
            (100000,        'n',    '100000',       '100,000'),
            (-100000,       'n',    '-100000',      '-100,000'),
            (100000000,     'n',    '100000000',    '100,000,000'),
            (-100000000,    'n',    '-100000000',   '-100,000,000'),

            # zero padded
            (1000,          '08n',  '00001000',     '0,001,000'),
            (-1000,         '08n',  '-0001000',     '-001,000'),
        ]

        # check locale specific formatting
        try:
            if is_cli:
                _locale.setlocale(_locale.LC_ALL, 'en_US')
            else:
                _locale.setlocale(_locale.LC_ALL, 'English_United States.1252')
            for value, spec, _, result in locale_tests:
                self.assertEqual(value.__format__(spec), result)
                self.assertEqual(big(value).__format__(spec), result)
        finally:
            # and restore it back...
            _locale.setlocale(_locale.LC_ALL, 'C')
            for value, spec, result, _ in locale_tests:
                self.assertEqual(value.__format__(spec), result)
                self.assertEqual(big(value).__format__(spec), result)

    def test_int___format___errors(self):
        errors = [
                    (ValueError, 2, '6.1', "Precision not allowed in integer format specifier"),
                    (ValueError, 2, '+c', "Sign not allowed with integer format specifier 'c'"),
                    (ValueError, 2, '-c', "Sign not allowed with integer format specifier 'c'"),
                    (ValueError, 2, ' c', "Sign not allowed with integer format specifier 'c'"),
                    (OverflowError, -2, 'c', "%c arg not in range(0x110000)"),
                    (OverflowError, 0x110000, 'c', "%c arg not in range(0x110000)"),
                    #(-2, 'c', ),
                    #(-2, '%', "Sign not allowed with integer format specifier 'c'"),
                ]

        if is_cli: #http://ironpython.codeplex.com/workitem/28373
            errors.append((OverflowError, sys.maxsize + 1, 'c', "Python int too large to convert to System.Int32"))
        else:
            errors.append((OverflowError, sys.maxsize + 1, 'c', "Python int too large to convert to C long"))

        okChars = set(['%', 'E', 'F', 'G', 'X', 'x', 'b', 'c', 'd', 'o', 'e', 'f', 'g', 'n', ','] + [chr(x) for x in range(ord('0'), ord('9') + 1)])

        # verify the okChars are actually ok
        for char in okChars:
            (2).__format__('10' + char)
            big(2).__format__('10' + char)

        for char in allChars:
            if char not in okChars and (char < '0' or char > '9'):
                x = ord(char)
                if 0x20 < x < 0x80:
                    errors.append((ValueError, 2, '10' + char, "Unknown format code '%s' for object of type 'int'" % char))
                else:
                    errors.append((ValueError, 2, '10' + char, "Unknown format code '\\x%x' for object of type 'int'" % x))

        for error, value, errorFmt, errorMsg in errors:
            self.assertRaisesMessage(error, errorMsg, value.__format__, errorFmt)
            self.assertRaisesMessage(error, errorMsg, big(value).__format__, errorFmt)

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
