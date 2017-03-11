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

######################################################################################
# Formatting of floats
#

# 12 significant digits near the decimal point

AreEqual(str(12345678901.2), "12345678901.2")
AreEqual(str(1.23456789012), "1.23456789012")

# 12 significant digits near the decimal point, preceeded by upto 3 0s

if is_cpython: #http://ironpython.codeplex.com/workitem/28215
    AreEqual(str(123456789012.00), "1.23456789012e+11")
    AreEqual(str(123456789012.0), "1.23456789012e+11")
else:
    AreEqual(str(123456789012.00), "123456789012.0")
    AreEqual(str(123456789012.0), "123456789012.0")
AreEqual(str(00.123456789012), "0.123456789012")
AreEqual(str(0.000123456789012), "0.000123456789012")

# 12 significant digits near the decimal point, followed by 0s, or preceeded more than 3 0s

AreEqual(str(1234567890120.00), "1.23456789012e+12")
AreEqual(str(0.0000123456789012), "1.23456789012e-05")

# More than 12 significant digits near the decimal point, with rounding down

AreEqual(str(12345678901.23), "12345678901.2")
if is_cpython: #http://ironpython.codeplex.com/workitem/28215
    AreEqual(str(123456789012.3), "1.23456789012e+11")
else:
    AreEqual(str(123456789012.3), "123456789012.0")
AreEqual(str(1.234567890123), "1.23456789012")

# More than 12 significant digits near the decimal point, with rounding up

if is_cpython: #http://ironpython.codeplex.com/workitem/28215
    AreEqual(str(12345678901.25), "12345678901.2")
    AreEqual(str(123456789012.5), "1.23456789012e+11")
else:
    AreEqual(str(12345678901.25), "12345678901.3")
    AreEqual(str(123456789012.5), "123456789013.0")
if (is_cli or is_silverlight):
    AreEqual(str(1.234567890125), "1.23456789013")
else:
    AreEqual(str(1.234567890125), "1.23456789012")
AreEqual(str(1.234567890126), "1.23456789013")

# Signficiant digits away from the decimal point

if is_cpython: #http://ironpython.codeplex.com/workitem/28215
    AreEqual(str(100000000000.0), "1e+11")
else:
    AreEqual(str(100000000000.0), "100000000000.0")
AreEqual(str(1000000000000.0), "1e+12")
AreEqual(str(0.0001), "0.0001")
AreEqual(str(0.00001), "1e-05")

# Near the ends of the number line

# System.Double.MaxValue
AreEqual(str(1.79769313486232e+308), "inf")
AreEqual(str(1.79769313486231e+308), "1.79769313486e+308")
# System.Double.MinValue
AreEqual(str(-1.79769313486232e+308), "-inf")
AreEqual(str(-1.79769313486231e+308), "-1.79769313486e+308")
# System.Double.Epsilon
AreEqual(str(4.94065645841247e-324), "4.94065645841e-324")
# NaN
AreEqual(str((1.79769313486232e+308 * 2.0) * 0.0), "nan")

AreEqual(str(2.0), "2.0")
AreEqual(str(.0), "0.0")
AreEqual(str(-.0), "-0.0")
# verify small strings display all precision by default
x = 123.456E-19 * 2.0
AreEqual(str(x), "2.46912e-17")

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
if is_cpython: #http://ironpython.codeplex.com/workitem/28206
    values.append((123456.5, "123456"))
else:
    values.append((123456.5, "123457"))

for v in values:
    AreEqual("%g" % v[0], v[1])
    AreEqual("%.6g" % v[0], v[1])
    # AreEqual("% .6g" % v[0], v[1])

######################################################################################
# Formatting of System.Single

if is_cli or is_silverlight:
    load_iron_python_test()
    import IronPythonTest
    f = IronPythonTest.DoubleToFloat.ToFloat(1.0)
    AreEqual(str(f), "1.0")
    AreEqual("%g" % f, "1")
    f = IronPythonTest.DoubleToFloat.ToFloat(1.1)
    AreEqual(str(f), "1.1")
    AreEqual("%g" % f, "1.1")
    f = IronPythonTest.DoubleToFloat.ToFloat(1.2345678)
    AreEqual(str(f), "1.23457")
    AreEqual("%g" % f, "1.23457")
    f = IronPythonTest.DoubleToFloat.ToFloat(1234567.8)
    AreEqual(str(f), "1.23457e+06")
    AreEqual("%g" % f, "1.23457e+06")

######################################################################################

def formatError():
    "%d" % (1,2)
      
AssertError(TypeError, formatError, None)

def formatError_earlyEnd():
    "%" % None

AssertError(ValueError, formatError_earlyEnd)

AreEqual(len('%10d' %(1)), 10)

#formatting should accept a float for int formatting...
AreEqual('%d' % 3.7, '3')

AreEqual("%0.3f" % 10.8, "10.800")

# test that %s / %r work correctly

# oldstyle class
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
        
a = A()
AreEqual("%s" % a, "str")
AreEqual("%r" % a, "repr")
b = B()
# BUG 153
#AreEqual("%s" % b, "str")
# /BUG
AreEqual("%r" % b, "repr")

# if str() returns Unicode, so should
# test character
AreEqual("%c" % 23, chr(23))
AssertError(OverflowError, (lambda: "%c" % -1) )
AreEqual("%c" % str('x'), str('x'))
try:
    AreEqual("%c" % 65535, '\uffff')
except OverflowError:
    pass
    
# test %i, %u, not covered in test_format
AreEqual('%i' % 23, '23')
AreEqual('%i' % 23.9,  '23')
AreEqual('%+u' % 5, '+5')
AreEqual('%05u' % 3, '00003')
AreEqual('% u' % 19, ' 19')
AreEqual('%*u' % (5,10), '   10')
AreEqual('%e' % 1000000, '1.000000e+06')
AreEqual('%E' % 1000000, '1.000000E+06')

AreEqual('%.2e' % 1000000, '1.00e+06')
AreEqual('%.2E' % 1000000, '1.00E+06')
AreEqual('%.2g' % 1000000, '1e+06')

AreEqual('%G' % 100, '100')


# test named inputs
fmtstr = '%(x)+d -- %(y)s -- %(z).2f'
AreEqual(fmtstr % {'x':9, 'y':'quux', 'z':3.1415}, '+9 -- quux -- 3.14')
AssertError(KeyError, (lambda:fmtstr % {}))
AssertError(KeyError, (lambda:fmtstr % {'x': 3}))
AssertError(TypeError, (lambda:fmtstr % {'x': 'notanint', 'y':'str', 'z':2.1878}))

AreEqual('%(key)s %(yek)d' % {'key':'ff', 'yek':200}, "ff 200")

AreEqual(repr("\u00F4"), "u'\\xf4'")
AreEqual(repr("\u10F4"), "u'\\u10f4'")

AssertError(TypeError, lambda: "%5c" % None)
AreEqual("%5c" % 'c', '    c')
AreEqual("%+5c" % 'c', '    c')
AreEqual("%-5c" % 'c', 'c    ')

AreEqual("%5s" % None, ' None')
AreEqual("%5s" % 'abc', '  abc')
AreEqual("%+5s" % 'abc', '  abc')
AreEqual("%-5s" % 'abc', 'abc  ')
 

#
# Test named inputs with nested ()
#

# Success cases
s = '_%((key))s_' % {'(key)':10}
AreEqual(s, '_10_')

s = '_%((((key))))s_' % {'(((key)))':20}
AreEqual(s, '_20_')

s = '%((%s))s' % { '(%s)' : 30 }
AreEqual(s, '30')

s = '%()s' % { '': 40 }
AreEqual(s,'40')

s = '%(a(b)c)s' % { 'a(b)c' : 50 }
AreEqual(s,'50')

s = '%(a)s)s' % { 'a' : 60 }
AreEqual(s,'60)s')

s = '%(((a)s))s' % {'((a)s)': 70, 'abc': 'efg'}
AreEqual(s, '70')

# Error cases

# ensure we properly count number of expected closing ')'
def Error1():
  return '%(a))s' % { 'a' : 10, 'a)' : 20 }

AssertError(ValueError, Error1)

# Incomplete format key, not enough closing ')'
def Error2():
  return '%((a)s' % { 'a' : 10, '(a' : 20 }

AssertError(ValueError, Error2)


AreEqual('%*s' %(-5,'abc'), 'abc  ')


# cp28936
AreEqual('%10.1e' % 1, '   1.0e+00')
AreEqual('% 10.1e' % 1, '   1.0e+00')
AreEqual('%+10.1e' % 1, '  +1.0e+00')

AreEqual('%10.1e' % -1, '  -1.0e+00')
AreEqual('% 10.1e' % -1, '  -1.0e+00')
AreEqual('%+10.1e' % -1, '  -1.0e+00')

AreEqual('%-10.1e' % 1, '1.0e+00   ')
AreEqual('% -10.1e' % 1, ' 1.0e+00  ')
AreEqual('%+-10.1e' % 1, '+1.0e+00  ')

AreEqual('%-10.1e' % -1, '-1.0e+00  ')
AreEqual('% -10.1e' % -1, '-1.0e+00  ')
AreEqual('%+-10.1e' % -1, '-1.0e+00  ')


# the following is borrowed from stdlib
import os
import math

#locate file with float format test values
test_dir = os.path.dirname(__file__) or os.curdir
format_testfile = os.path.join(test_dir, 'formatfloat_testcases.txt')



def test_format_testfile():
    with open(format_testfile) as testfile:
        for line in open(format_testfile):
            print(line)
            if line.startswith('--'):
                continue
            line = line.strip()
            if not line:
                continue

            lhs, rhs = list(map(str.strip, line.split('->')))
            fmt, arg = lhs.split()
            arg = float(arg)
            AreEqual(fmt % arg, rhs)
            if not math.isnan(arg) and math.copysign(1.0, arg) > 0.0:
                print("minus")
                AreEqual(fmt % -arg, '-' + rhs)

test_format_testfile()
