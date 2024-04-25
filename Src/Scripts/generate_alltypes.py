# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

from generate import generate

import clr
clr.AddReference("System.Numerics")
from System import *
from System.Numerics import BigInteger

def get_min_max(type):
    if hasattr(type, 'MinValue'):
        return type.MinValue, type.MaxValue

    return Double.NegativeInfinity, Double.PositiveInfinity

class NumType:
    def __init__(self, type):
        self.name = clr.GetClrType(type).Name
        self.type = type
        self.ops = self.name+"Ops"
        self.min, self.max = get_min_max(type)

        self.is_signed = self.min < 0
        self.size = self.max-self.min + 1

        try:
            self.is_float = self.type(1) // self.type(2) != self.type(0.5)
        except:
            self.is_float = True

        try:
            class _(type): pass
            self.is_extensible = True
        except:
            self.is_extensible = False

        if self.is_float:
            self.cast_type = "double"
        elif self.max > Int32.MaxValue:
            self.cast_type = "BigInteger"
        else:
            self.cast_type = "int"

    def get_dict(self):
        toObj = "(%s)" % self.name
        toObjFooter = ""
        if self.name == "Int32":
            toObj = "Microsoft.Scripting.Runtime.ScriptingRuntimeHelpers.Int32ToObject((Int32)"
            toObjFooter = ")"
        if self.get_overflow_type() == bigint:
            op_type = 'BigInteger'
        else:
            op_type = 'Int32'
        return dict(type = self.name, bigger_type = self.get_overflow_type().get_signed().name,
            bigger_signed = self.get_overflow_type().get_signed().name,
            type_to_object = toObj, type_to_object_footer = toObjFooter, op_type=op_type, rop_type=op_type)

    def get_other_sign(self):
        if self.is_signed: return self.get_unsigned()
        else: return self.get_signed()

    def get_unsigned(self):
        if not self.is_signed: return self

        for ty in int_types:
            if not ty.is_signed and ty.size == self.size: return ty

        raise ValueError

    def get_signed(self):
        if self.is_signed: return self

        for ty in int_types:
            if ty.is_signed and ty.size == self.size: return ty

        raise ValueError(ty.name)

    def get_overflow_type(self):
        if self.is_float or self == bigint: return self
        if self.type == Int32: return bigint # special Python overflow rule (skips Int64)

        for ty in int_types:
            if ty.is_signed == self.is_signed and ty.size == self.size**2:
                return ty
        return bigint

    def is_implicit(self, oty):
        if self.is_float:
            if oty.is_float:
                return self.size <= oty.size
            else:
                return False
        else:
            if oty.is_float:
                return True
            elif self.is_signed:
                if oty.is_signed:
                    return self.size <= oty.size
                else:
                    return False
            else:
                if oty.is_signed:
                    return self.size < oty.size
                else:
                    return self.size <= oty.size

clr_primitive_int_types   = [SByte, Byte, Int16, UInt16, Int32, UInt32, Int64, UInt64]
clr_primitive_float_types = [Single, Double]

int_types   = [NumType(t) for t in clr_primitive_int_types]
float_types = [NumType(t) for t in clr_primitive_float_types]
primitive_types = int_types + float_types
bigint = NumType(BigInteger)

simple_identity_method = """
public static %(type)s %(method_name)s(%(type)s x) => x;"""

unchecked_cast_method = """
public static %(cast_type)s %(method_name)s(%(type)s x) => unchecked((%(cast_type)s)x);"""

getnewargs_method = """
public static PythonTuple __getnewargs__(%(type)s self) => PythonTuple.MakeTuple(unchecked((%(cast_type)s)self));"""

identity_method = """
[SpecialName]
public static %(type)s %(method_name)s(%(type)s x) => x;"""

simple_method = """
[SpecialName]
public static %(type)s %(method_name)s(%(type)s x) => (%(type)s)(%(symbol)s(x));"""


signed_abs = """
[SpecialName]
public static object Abs(%(type)s x) {
    if (x < 0) {
        if (x == %(type)s.MinValue) return -(%(bigger_signed)s)%(type)s.MinValue;
        else return (%(type)s)(-x);
    } else {
        return x;
    }
}"""

signed_negate = """
[SpecialName]
public static object Negate(%(type)s x) {
    if (x == %(type)s.MinValue) return -(%(bigger_signed)s)%(type)s.MinValue;
    else return (%(type)s)(-x);
}"""

unsigned_negate_or_invert = """
[SpecialName]
public static object %(method_name)s(%(type)s x) => %(bigger_signed)sOps.%(method_name)s((%(bigger_signed)s)x);"""

float_trunc = """
public static object __trunc__(%(type)s x) {
    if (x >= int.MaxValue || x <= int.MinValue) {
        return (BigInteger)x;
    } else {
        return (int)x;
    }
}"""

def gen_unaryops(cw, ty):
    cw.writeline()
    cw.writeline("#region Unary Operations")
    cw.write(identity_method, method_name="Plus")

    if ty.is_float:
        cw.write(simple_method, method_name="Negate", symbol="-")
        cw.write(simple_method, method_name="Abs", symbol="Math.Abs")
    elif ty.is_signed:
        cw.write(signed_negate)
        cw.write(signed_abs)
        cw.write(simple_method, method_name="OnesComplement", symbol="~")
    else:
        cw.write(unsigned_negate_or_invert, method_name="Negate")
        cw.write(identity_method, method_name="Abs")
        cw.write(unsigned_negate_or_invert, method_name="OnesComplement")

    if ty.name not in ['BigInteger']:
        cw.writeline()
        cw.write('public static bool __bool__(%s x) => (x != 0);' % (ty.name))

    # for floats, this is handled in another Ops file
    if not ty.is_float:
        cw.writeline()
        cw.write('public static string __repr__(%s x) => x.ToString(CultureInfo.InvariantCulture);' % (ty.name))

    # for extensible types, this is handled in another Ops file
    if not ty.is_extensible:
        cw.write(getnewargs_method, type=ty.name, cast_type=ty.cast_type)

    if ty.is_float:
        cw.write(float_trunc, type=ty.name)
    else:
        cw.write(simple_identity_method, type=ty.name, method_name="__trunc__")

        cw.write(unchecked_cast_method, type=ty.name, method_name="__int__", cast_type=ty.cast_type)
        cw.write(unchecked_cast_method, type=ty.name, method_name="__index__", cast_type=ty.cast_type)

        cw.writeline()
        cw.enter_block('public static int __hash__(%s x)' % (ty.name))
        if ty.max > Int32.MaxValue:
            if ty.is_signed:
                cw.enter_block('if (x < 0)')
                cw.writeline('if (x == long.MinValue) return -2;')
                cw.writeline('x = -x;')
                cw.writeline('var h = unchecked(-(int)((x >= int.MaxValue) ? (x % int.MaxValue) : x));')
                cw.writeline('if (h == -1) return -2;')
                cw.writeline('return h;')
                cw.exit_block()
            cw.writeline('return unchecked((int)((x >= int.MaxValue) ? (x % int.MaxValue) : x));')
        elif ty.max == Int32.MaxValue:
            cw.writeline("// for perf we use an if for the 3 int values with abs(x) >= int.MaxValue")
            cw.writeline('if (x == -1 || x == int.MinValue) return -2;')
            cw.writeline('if (x == int.MaxValue || x == int.MinValue + 1) return 0;')
            cw.writeline('return unchecked((int)x);')
        else:
            if ty.is_signed:
                cw.writeline('if (x == -1) return -2;')
            cw.writeline('return unchecked((int)x);')
        cw.exit_block()

    cw.writeline()
    cw.writeline("#endregion")


binop_decl = """
[SpecialName]
public static %(return_type)s %(method_name)s(%(rtype)s x, %(ltype)s y)"""

simple_body = "return x %(symbol)s y;"
cast_simple_body = "return (%(type)s)(x %(symbol)s y);"

simple_compare_body = "return x == y ? 0 : x > y ? 1 : -1;"

overflow1_body = """\
%(bigger_type)s result = (%(bigger_type)s)(((%(bigger_type)s)x) %(symbol)s ((%(bigger_type)s)y));
if (%(type)s.MinValue <= result && result <= %(type)s.MaxValue) {
    return %(type_to_object)s(result)%(type_to_object_footer)s;
} else {
    return result;
}"""

overflow2_body = """\
try {
    return %(type_to_object)s(checked(x %(symbol)s y))%(type_to_object_footer)s;
} catch (OverflowException) {
    return %(bigger_type)sOps.%(method_name)s((%(bigger_type)s)x, (%(bigger_type)s)y);
}"""

overflow3_body = """\
long result = (long)x %(symbol)s y;
if (%(type)s.MinValue <= result && result <= %(type)s.MaxValue) {
    return %(type_to_object)s(result)%(type_to_object_footer)s;
}
return %(bigger_type)sOps.%(method_name)s((%(bigger_type)s)x, (%(bigger_type)s)y);"""

unsigned_signed_body = "return %(bigger_signed)sOps.%(method_name)s((%(bigger_signed)s)x, (%(bigger_signed)s)y);"

int_divide_body = "return FloorDivide(x, y);"
float_divide_body = "return TrueDivide(x, y);"

float_floor_divide_body = """\
if (y == 0) throw PythonOps.ZeroDivisionError();
return (%(type)s)Math.Floor(x / y);"""

float_true_divide_body = """\
if (y == 0) throw PythonOps.ZeroDivisionError();
return x / y;"""

int_true_divide_body = """\
return DoubleOps.TrueDivide((double)x, (double)y);"""

div_body = """\
if (y == -1 && x == %(type)s.MinValue) {
    return -(%(bigger_type)s)%(type)s.MinValue;
} else {
    return (%(type)s)MathUtils.FloorDivideUnchecked(x, y);
}"""

#rshift, mod
cast_helper_body = "return (%(type)s)%(op_type)sOps.%(method_name)s((%(op_type)s)x, (%(rop_type)s)y);"
#lshift, pow
helper_body = "return %(op_type)sOps.%(method_name)s((%(op_type)s)x, (%(rop_type)s)y);"

def write_binop_raw(cw, body, name, ty, **kws):
    kws1 = dict(return_type=ty.name, method_name=name, rtype=ty.name, ltype=ty.name)
    kws1.update(kws)
    cw.enter_block(binop_decl, **kws1)
    cw.write(body, **kws1)
    cw.exit_block()

def write_binop1(cw, body, name, ty, **kws):
    write_binop1_general(write_binop_raw, cw, body, name, ty, **kws)

def write_binop1_general(func, cw, body, name, ty, **kws):
    func(cw, body, name, ty, **kws)

    if not ty.is_signed:
        if ty.name in ["UInt64"]:
            oty = bigint
        else:
            oty = ty.get_signed()
        if 'return_type' not in kws or kws['return_type'] == ty.name:
            if name == 'FloorDivide':
                kws['return_type'] = 'object'
            else:
                kws['return_type'] = cw.kws['bigger_signed']

        kws['ltype'] = oty.name
        func(cw, unsigned_signed_body, name, ty, **kws)

        kws['ltype'] = ty.name
        kws['rtype'] = oty.name
        func(cw, unsigned_signed_body, name, ty, **kws)

def write_rich_comp_raw(cw, body, name, ty, **kws):
    kws1 = dict(return_type=ty.name, method_name=name, rtype=ty.name, ltype=ty.name)
    kws1.update(kws)
    assert body.startswith("return")
    cw.write("""[SpecialName]
public static %(return_type)s %(method_name)s(%(rtype)s x, %(ltype)s y) =>""" + body[6:], **kws1)

def write_rich_comp(cw, body, name, ty, **kws):
    write_rich_comp_general(write_rich_comp_raw, cw, body, name, ty, **kws)

def write_rich_comp_general(func, cw, body, name, ty, **kws):
    func(cw, body, name, ty, **kws)

    if not ty.is_signed:
        if ty.name in ["UInt64"]:
            oty = bigint
        else:
            oty = ty.get_signed()
        kws['ltype'] = oty.name
        if cw.kws.get('bigger_signed') == "BigInteger":
            func(cw, unsigned_signed_body, name, ty, **kws)
        else:
            func(cw, body, name, ty, **kws)

def gen_binaryops(cw, ty):
    cw.writeline()
    cw.writeline("#region Binary Operations - Arithmetic")

    for symbol, name in [('+', 'Add'), ('-', 'Subtract'), ('*', 'Multiply')]:
        if ty.is_float:
            write_binop1(cw, simple_body, name, ty, symbol=symbol)
        else:
            if ty.name == "Int32":
                body = overflow3_body
            elif ty.get_overflow_type() == bigint:
                body = overflow2_body
            else:
                body = overflow1_body
            write_binop1(cw, body, name, ty, return_type='object', symbol=symbol)

    if ty.is_float:
        write_binop1(cw, float_true_divide_body, "TrueDivide", ty)
        write_binop1(cw, float_floor_divide_body, "FloorDivide", ty)
    else:
        write_binop1(cw, int_true_divide_body, "TrueDivide", ty, return_type='double')

        if ty.name not in ['BigInteger', 'Int32']:
            if ty.is_signed:
                write_binop1(cw, div_body, 'FloorDivide', ty, return_type='object')
                write_binop1(cw, cast_helper_body, 'Mod', ty)
            else:
                write_binop1(cw, cast_simple_body, 'FloorDivide', ty, symbol='/')
                write_binop1(cw, cast_simple_body, 'Mod', ty, symbol='%')
            write_binop1(cw, helper_body, 'Power', ty, return_type='object')

    cw.writeline()
    cw.writeline("#endregion")

    if not ty.is_float:
        cw.writeline()
        cw.writeline("#region Binary Operations - Bitwise")

        if ty.name not in ["BigInteger"]:
            ltypes = [('BigInteger', 'BigInteger')]
            if ty.size < Int32.MaxValue:
                ltypes.append( ('Int32', 'Int32') )
            for ltype, optype in ltypes:
                write_binop_raw(cw, helper_body, "LeftShift", ty, return_type='object', op_type=optype, rop_type=optype, ltype=ltype)
                write_binop_raw(cw, cast_helper_body, "RightShift", ty, op_type=optype, rop_type=optype, ltype=ltype)

        for symbol, name in [('&', 'BitwiseAnd'), ('|', 'BitwiseOr'), ('^', 'ExclusiveOr')]:
            write_binop1(cw, cast_simple_body, name, ty, symbol=symbol)

        cw.writeline()
        cw.writeline("#endregion")

        cw.writeline()
        cw.writeline("#region Binary Operations - Comparisons")
        cw.writeline()
        for symbol, name in [('<', 'LessThan'), ('<=', 'LessThanOrEqual'), ('>', 'GreaterThan'), ('>=', 'GreaterThanOrEqual'), ('==', 'Equals'), ('!=', 'NotEquals')]:
            write_rich_comp(cw, simple_body, name, ty, symbol=symbol, return_type='bool')
        cw.writeline()
        cw.writeline("#endregion")

implicit_conv = """
[SpecialName, ImplicitConversionMethod]
public static %(otype)s ConvertTo%(otype)s(%(type)s x) => (%(otype)s)x;"""

explicit_conv = """
[SpecialName, ExplicitConversionMethod]
public static %(otype)s ConvertTo%(otype)s(%(type)s x) {
    if (%(otype)s.MinValue <= x && x <= %(otype)s.MaxValue) {
        return (%(otype)s)x;
    }
    throw Converter.CannotConvertOverflow("%(otype)s", x);
}"""

explicit_conv_to_unsigned_from_signed = """
[SpecialName, ExplicitConversionMethod]
public static %(otype)s ConvertTo%(otype)s(%(type)s x) {
    if (x >= 0) {
        return (%(otype)s)x;
    }
    throw Converter.CannotConvertOverflow("%(otype)s", x);
}"""

explicit_conv_tosigned_from_unsigned = """
[SpecialName, ExplicitConversionMethod]
public static %(otype)s ConvertTo%(otype)s(%(type)s x) {
    if (x <= (%(type)s)%(otype)s.MaxValue) {
        return (%(otype)s)x;
    }
    throw Converter.CannotConvertOverflow("%(otype)s", x);
}"""


def write_conversion(cw, ty, oty):
    if ty.is_implicit(oty):
        cw.write(implicit_conv, otype=oty.name)
    elif ty.is_signed and not oty.is_signed and ty.size <= oty.size:
        cw.write(explicit_conv_to_unsigned_from_signed, otype=oty.name)
    elif not ty.is_signed and oty.is_signed:
        cw.write(explicit_conv_tosigned_from_unsigned, otype=oty.name)
    else:
        cw.write(explicit_conv, otype=oty.name)

def gen_conversions(cw, ty):
    cw.writeline()
    cw.writeline("#region Conversion operators")
    for oty in primitive_types:
        if oty == ty: continue

        write_conversion(cw, ty, oty)

    cw.writeline()
    cw.writeline("#endregion")

identity_property_method = """
[PropertyMethod, SpecialName]
public static %(type)s Get%(method_name)s(%(type)s x) => x;"""

const_property_method = """
[PropertyMethod, SpecialName]
public static %(type)s Get%(method_name)s(%(type)s x) => (%(type)s)%(const)s;"""

# const=None indicates an identity property, i.e. a property that returns 'self'
def write_property(cw, ty, name, const=None):
    if const == None:
        cw.write(identity_property_method, type=ty.name, method_name=name)
    else:
        cw.write(const_property_method, type=ty.name, method_name=name, const=const)

def gen_api(cw, ty):
    if ty.name in ["BigInteger"]:
        return
    cw.writeline()
    cw.writeline("#region Public API - Numerics")
    write_property(cw, ty, "real")
    write_property(cw, ty, "imag", const="0")
    cw.write(simple_identity_method, type=ty.name, method_name="conjugate")
    if not ty.is_float:
        cw.writeline()
        write_property(cw, ty, "numerator")
        write_property(cw, ty, "denominator", const="1")

        cast = "(int)"
        counter = "BitLength"
        if ty.size >= 4294967296:
            # 32- or 64-bit type
            cast = ""
            if not ty.is_signed:
                counter += "Unsigned"

        cw.writeline()
        cw.enter_block('public static int bit_length(%s value)' % ty.name)
        cw.write('return MathUtils.%s(%svalue);' % (counter, cast))
        cw.exit_block()

        if ty.name not in ['Int64', 'UInt64']:
            cw.writeline()
            cw.enter_block('public static Bytes to_bytes(%s value, int length, [NotNone] string byteorder, bool signed = false)' % ty.name)
            cw.write('// TODO: signed should be a keyword only argument')
            cw.write('return %s.to_bytes(value, length, byteorder, signed);' % ("Int64Ops" if ty.is_signed else "UInt64Ops"))
            cw.exit_block()

    cw.writeline()
    cw.writeline("#endregion")

type_header = """
#region Constructors

[StaticExtensionMethod]
public static object __new__(PythonType cls) {
    return __new__(cls, default(%(type)s));
}

[StaticExtensionMethod]
public static object __new__(PythonType cls, object value) {
    if (cls != DynamicHelpers.GetPythonTypeFromType(typeof(%(type)s))) {
        throw PythonOps.TypeError("%(type)s.__new__: first argument must be %(type)s type.");
    }
    if (value is IConvertible valueConvertible) {
        switch (valueConvertible.GetTypeCode()) {
            case TypeCode.Byte:   return checked((%(type)s)(Byte)value);
            case TypeCode.SByte:  return checked((%(type)s)(SByte)value);
            case TypeCode.Int16:  return checked((%(type)s)(Int16)value);
            case TypeCode.UInt16: return checked((%(type)s)(UInt16)value);
            case TypeCode.Int32:  return checked((%(type)s)(Int32)value);
            case TypeCode.UInt32: return checked((%(type)s)(UInt32)value);
            case TypeCode.Int64:  return checked((%(type)s)(Int64)value);
            case TypeCode.UInt64: return checked((%(type)s)(UInt64)value);
            case TypeCode.Single: return checked((%(type)s)(Single)value);
            case TypeCode.Double: return checked((%(type)s)(Double)value);
        }
    }
    if (value is String s) {
        try {
            return %(type)s.Parse(s, System.Globalization.NumberFormatInfo.InvariantInfo);
        } catch (FormatException ex) {
            throw PythonOps.ValueError("{0}", ex.Message);
        }
    } else if (value is BigInteger bi) {
        return checked((%(type)s)bi);
    } else if (value is Extensible<BigInteger> ebi) {
        return checked((%(type)s)ebi.Value);
    } else if (value is Extensible<double> ed) {
        return checked((%(type)s)ed.Value);
    }
    throw PythonOps.TypeError("can't convert {0} to %(type)s", PythonOps.GetPythonTypeName(value));
}

#endregion"""

def gen_header(cw, ty):
    if ty.name not in ['Double', 'Single', 'BigInteger']:
        cw.write(type_header)

def gen_type(cw, ty):
    cw.kws.update(ty.get_dict())
    extra = ""
    cw.enter_block("public static partial %(extra)sclass %(type)sOps", extra=extra)
    gen_header(cw, ty)
    gen_unaryops(cw, ty)
    gen_binaryops(cw, ty)
    gen_conversions(cw, ty)
    gen_api(cw, ty)
    cw.exit_block()
    cw.writeline()

def gen_int(cw):
    for ty in int_types:
        gen_type(cw, ty)

def gen_float(cw):
    for ty in float_types:
        gen_type(cw, ty)

def main():
    return generate(
        ("IntOps", gen_int),
        ("FloatOps", gen_float),
    )

if __name__ == "__main__":
    main()
