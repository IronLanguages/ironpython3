# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

from generate import generate

Int32 = "Int32"
Double = "Double"
Boolean = "Boolean"
Int64 = "Int64"
Int16 = "Int16"
UInt32 = "UInt32"
UInt64 = "UInt64"
UInt16 = "UInt16"
SByte = "SByte"
Byte = "Byte"
Single = "Single"
Char = "Char"
Decimal = "Decimal"
Object = "Object"

types = [ Int32, Double, Boolean, Int64, Int16, UInt32, UInt64, UInt16, SByte, Byte, Single, Char, Decimal ]
additional_cached = [ Object ]
non_cls_types = [ UInt32, UInt64, UInt16, SByte ]
enum_types = [ Byte, SByte, Int16, UInt16, Int32, UInt32, Int64, UInt64 ]

# Invalid casts.
# This is reflective so only one direction is enough
invalid_casts = [
    ( Int32,        Boolean ),
    ( Double,       Boolean ),
    ( Int64,        Boolean ),
    ( Int16,        Boolean ),
    ( UInt32,       Boolean ),
    ( UInt64,       Boolean ),
    ( UInt16,       Boolean ),
    ( SByte,        Boolean ),
    ( Byte,         Boolean ),
    ( Single,       Boolean ),
    ( Char,         Boolean ),
    ( Decimal,      Boolean ),
]

# intermediate enum types to use for enum casts
# e.g. enum to Single will go via EnumToInt64 -> Single
enum_casts = {
     Int32  : Int32,
     Double : Int64,
     Int64  : Int64,
     Int16  : Int16,
     UInt32 : UInt32,
     UInt64 : UInt64,
     UInt16 : UInt16,
     SByte  : SByte,
     Byte   : Byte,
     Single : Int64,
     Char   : Int32,
     Decimal : Int64,
}

def generate_nullable_instance(cw):
    cw.enter_block("public static object NewNullableInstance(Type type)")
    cond = cw.conditions()
    for t in types:
        cond.condition("if (type == %(type)sType)", type = t)
        cw.write("return new Nullable<%(type)s>();", type = t)
    cw.else_block()
    cw.write("return NewNullableInstanceSlow(type);")
    cw.exit_block()
    cw.exit_block()

def valid_cast(f, t):
    return not (f, t) in invalid_casts and not (t, f) in invalid_casts

def get_enum_type_for(t):
    return enum_casts[t]

def mark_cls_compliance(cw, t):
    if t in non_cls_types:
        cw.write("[CLSCompliant(false)]")

def generate_type_cast(cw, t):
    mark_cls_compliance(cw, t)
    cw.enter_block("public static %(type)s ExplicitCastTo%(type)s(object o)", type = t)
    cw.enter_block("if (o != null)")
    cw.write("Type type = o.GetType();");

    cond = cw.conditions()
    # direct casts
    for f in types:
        if valid_cast(f, t):
            cond.condition("if (type == %(from_t)sType)", from_t = f, to_t = t)
            cw.write("return (%(to_t)s)(%(from_t)s)o;", from_t = f, to_t = t)

    # enums (only if int cast is valid)
    if valid_cast(Int32, t):
        cond.condition("if (type.IsEnum())")
        cw.write("return (%(to_t)s)ExplicitCastEnumTo%(enum)s(o);", to_t = t, enum = get_enum_type_for(t))

    # nullables
    for f in types:
        if valid_cast(f, t):
            cond.condition("if (type == Nullable%(from_t)sType)", from_t = f, to_t = t)
            cw.write("return (%(to_t)s)(Nullable<%(from_t)s>)o;", from_t = f, to_t = t)

    cond.close()
    cw.exit_block()
    cw.write("throw InvalidCast(o, \"%(type)s\");", type = t)
    cw.exit_block()


def generate_nullable_type_cast(cw, t):
    mark_cls_compliance(cw, t)
    cw.enter_block("public static Nullable<%(type)s> ExplicitCastToNullable%(type)s(object o)", type = t)
    cw.enter_block("if (o == null)")
    cw.write("return new Nullable<%(type)s>();", type = t);
    cw.exit_block()
    cw.write("Type type = o.GetType();");

    cond = cw.conditions()
    # direct casts
    for f in types:
        if valid_cast(f, t):
            cond.condition("if (type == %(from_t)sType)", from_t = f, to_t = t)
            cw.write("return (Nullable<%(to_t)s>)(%(from_t)s)o;", from_t = f, to_t = t)

    # enums (only if int cast is valid)
    if valid_cast(Int32, t):
        cond.condition("if (type.IsEnum())")
        cw.write("return (Nullable<%(to_t)s>)ExplicitCastEnumTo%(enum)s(o);", to_t = t, enum = get_enum_type_for(t))
        
    # nullables
    for f in types:
        if valid_cast(f, t):
            cond.condition("if (type == Nullable%(from_t)sType)", from_t = f, to_t = t)
            cw.write("return (Nullable<%(to_t)s>)(Nullable<%(from_t)s>)o;", from_t = f, to_t = t)

    cond.close()
    cw.write("throw InvalidCast(o, \"%(type)s\");", type = t)
    cw.exit_block()

def generate_type_casts(cw):
    for t in sorted(types):
        generate_type_cast(cw, t)
        cw.writeline()
    for t in sorted(types):
        generate_nullable_type_cast(cw, t)
        cw.writeline()

def generate_type_cache(cw):
    cache = list(types)
    cache.extend(additional_cached)
    # regulars
    for t in sorted(cache):
        cw.write("internal static readonly Type %(type)sType = typeof(%(type)s);", type = t)
    cw.writeline()
    for t in sorted(types):
        cw.write("internal static readonly Type Nullable%(type)sType = typeof(Nullable<%(type)s>);", type = t)

def generate_enum_cast(cw, t):
    cw.enter_block("internal static %(type)s ExplicitCastEnumTo%(type)s(object o)", type = t)
    cw.write("Debug.Assert(o is Enum);")
    cw.enter_block("switch (((Enum)o).GetTypeCode())")
    for f in enum_types:
        cw.case_label("case TypeCode.%(from_t)s: return (%(to_t)s)(%(from_t)s)o;", from_t = f, to_t = t)
        cw.dedent()
    cw.exit_block()
    cw.write("throw new InvalidOperationException(\"Invalid enum\");")
    cw.exit_block()

def generate_enum_casts(cw):
    for t in enum_types:
        generate_enum_cast(cw, t)
        cw.writeline()

def main():
    return generate(
        ("Nullable Instance", generate_nullable_instance),
        ("Type Cache", generate_type_cache),
        ("Type Casts", generate_type_casts),
        ("Enum Casts", generate_enum_casts),
    )

if __name__ == "__main__":
    main()
