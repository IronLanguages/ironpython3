# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

from generate import generate

class VariantType:
    def __init__(self,
        variantType,
        managedType,
        emitAccessors=True,
        isPrimitiveType=True,
        unmanagedRepresentationType=None,
        includeInUnionTypes=True,
        getStatements=None,
        setStatements=None,
        critical=False):

        self.emitAccessors = emitAccessors
        self.variantType = variantType
        self.managedType = managedType
        self.isPrimitiveType = isPrimitiveType
        if unmanagedRepresentationType == None: self.unmanagedRepresentationType = managedType
        else: self.unmanagedRepresentationType = unmanagedRepresentationType

        self.includeInUnionTypes = includeInUnionTypes

        self.getStatements = getStatements
        self.setStatements = setStatements

        self.managedFieldName = "_" + self.variantType.lower()
        firstChar = self.variantType[0]
        self.name = self.variantType.lower().replace(firstChar.lower(), firstChar, 1)
        self.accessorName = "As" + self.name
        self.critical = critical

    def write_UnionTypes(self, cw):
        if not self.includeInUnionTypes: return
        if self.unmanagedRepresentationType == "IntPtr":
            cw.write('[SuppressMessage("Microsoft.Reliability", "CA2006:UseSafeHandleToEncapsulateNativeResources")]')
        if self.managedFieldName == '_bstr':
            cw.write('[SuppressMessage("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]')
        cw.write("[FieldOffset(0)] internal %s %s;" % (self.unmanagedRepresentationType, self.managedFieldName))

    def write_ToObject(self, cw):
        cw.write("case VarEnum.VT_%s: return %s;" % (self.variantType, self.accessorName))

    def write_accessor(self, cw, transparent):
        if self.emitAccessors == False :
            return

        cw.write("// VT_%s" % self.variantType)

        cw.enter_block('public %s %s' % (self.managedType, self.accessorName))

        # Getter
        if not transparent and self.critical: gen_exposed_code_security(cw)
        cw.enter_block("get")
        cw.write("Debug.Assert(VariantType == VarEnum.VT_%s);" % self.variantType)
        if self.getStatements == None:
            cw.write("return _typeUnion._unionTypes.%s;" % self.managedFieldName)
        else:
            for s in self.getStatements: cw.write(s)
        cw.exit_block()

        # Setter
        if not transparent and self.critical: gen_exposed_code_security(cw)
        cw.enter_block("set")
        cw.write("Debug.Assert(IsEmpty); // The setter can only be called once as VariantClear might be needed otherwise")
        cw.write("VariantType = VarEnum.VT_%s;" % self.variantType)
        if self.setStatements == None:
            cw.write("_typeUnion._unionTypes.%s = value;" % self.managedFieldName)
        else:
            for s in self.setStatements: cw.write(s)
        cw.exit_block()

        cw.exit_block()

        # Byref Setter
        cw.writeline()
        if not transparent: gen_exposed_code_security(cw)

        cw.enter_block("public void SetAsByref%s(ref %s value)" % (self.name, self.unmanagedRepresentationType))
        cw.write("Debug.Assert(IsEmpty); // The setter can only be called once as VariantClear might be needed otherwise")
        cw.write("VariantType = (VarEnum.VT_%s | VarEnum.VT_BYREF);" % self.variantType)
        cw.write("_typeUnion._unionTypes._byref = UnsafeMethods.Convert%sByrefToPtr(ref value);" % self.unmanagedRepresentationType)
        cw.exit_block()

        cw.writeline()

    def write_accessor_propertyinfo(self, cw):
        if self.emitAccessors == True :
            cw.write('case VarEnum.VT_%s: return typeof(Variant).GetProperty("%s");' % (self.variantType, self.accessorName))

    def write_byref_setters(self, cw):
        if self.emitAccessors == True :
            cw.write('case VarEnum.VT_%s: return typeof(Variant).GetMethod("SetAsByref%s");' % (self.variantType, self.name))

    def write_ComToManagedPrimitiveTypes(self, cw):
        wrapper_types = ["CY", "DISPATCH", "UNKNOWN", "ERROR"]
        if not self.isPrimitiveType or (self.variantType in wrapper_types) : return
        cw.write("dict[VarEnum.VT_%s] = typeof(%s);" % (self.variantType, self.managedType))

    def write_IsPrimitiveType(self, cw):
        if not self.isPrimitiveType: return
        cw.write("case VarEnum.VT_%s:" % self.variantType)

    def write_ConvertByrefToPtr(self, cw, transparent):
        if self.isPrimitiveType and self.unmanagedRepresentationType == self.managedType and self.variantType != "ERROR":
            if not transparent: gen_exposed_code_security(cw)
            cw.write('[SuppressMessage("Microsoft.Design", "CA1045:DoNotPassTypesByReference")]')
            if self.unmanagedRepresentationType == 'Int32':
                cw.enter_block("public static unsafe IntPtr Convert%sByrefToPtr(ref %s value)" % (self.unmanagedRepresentationType, self.unmanagedRepresentationType))
            else:
                cw.enter_block("internal static unsafe IntPtr Convert%sByrefToPtr(ref %s value)" % (self.unmanagedRepresentationType, self.unmanagedRepresentationType))
            cw.enter_block('fixed (%s *x = &value)' % self.unmanagedRepresentationType)
            cw.write('AssertByrefPointsToStack(new IntPtr(x));')
            cw.write('return new IntPtr(x);')
            cw.exit_block()
            cw.exit_block()
            cw.write('')

    def write_ConvertByrefToPtr_Outer(self, cw, transparent):
        if self.isPrimitiveType and self.unmanagedRepresentationType == self.managedType and self.variantType != "ERROR":
            if not transparent: gen_exposed_code_security(cw)
            cw.write('[SuppressMessage("Microsoft.Design", "CA1045:DoNotPassTypesByReference")]')
            cw.write("public static IntPtr Convert%sByrefToPtr(ref %s value) { return _Convert%sByrefToPtr(ref value); }" % (self.unmanagedRepresentationType, self.unmanagedRepresentationType, self.unmanagedRepresentationType))

    def write_ConvertByrefToPtrDelegates(self, cw):
        if self.isPrimitiveType and self.unmanagedRepresentationType == self.managedType and self.variantType != "ERROR":
            cw.write("private static readonly ConvertByrefToPtrDelegate<%s> _Convert%sByrefToPtr = (ConvertByrefToPtrDelegate<%s>)Delegate.CreateDelegate(typeof(ConvertByrefToPtrDelegate<%s>), _ConvertByrefToPtr.MakeGenericMethod(typeof(%s)));" % (5 * (self.unmanagedRepresentationType,)))

def gen_exposed_code_security(cw):
    cw.write("#if CLR2")
    cw.write("[PermissionSet(SecurityAction.LinkDemand, Unrestricted = true)]")
    cw.write("#endif")
    cw.write("[SecurityCritical]")

variantTypes = [
  # VariantType('varEnum', 'managed_type')
    VariantType('I1', "SByte"),
    VariantType('I2', "Int16"),
    VariantType('I4', "Int32"),
    VariantType('I8', "Int64"),

    VariantType('UI1', "Byte"),
    VariantType('UI2', "UInt16"),
    VariantType('UI4', "UInt32"),
    VariantType('UI8', "UInt64"),

    VariantType('INT', "IntPtr"),
    VariantType('UINT', "UIntPtr"),

    VariantType('BOOL', "Boolean",
        unmanagedRepresentationType="Int16",
        getStatements=["return _typeUnion._unionTypes._bool != 0;"],
        setStatements=["_typeUnion._unionTypes._bool = value ? (Int16)(-1) : (Int16)0;"]),

    VariantType("ERROR", "Int32"),

    VariantType('R4', "Single"),
    VariantType('R8', "Double"),
    VariantType('DECIMAL', "Decimal",
        includeInUnionTypes=False,
        getStatements=["// The first byte of Decimal is unused, but usually set to 0",
                       "Variant v = this;",
                       "v._typeUnion._vt = 0;",
                       "return v._decimal;"],
        setStatements=["_decimal = value;",
                        "// _vt overlaps with _decimal, and should be set after setting _decimal",
                        "_typeUnion._vt = (ushort)VarEnum.VT_DECIMAL;"]),
    VariantType("CY", "Decimal",
        unmanagedRepresentationType="Int64",
        getStatements=["return Decimal.FromOACurrency(_typeUnion._unionTypes._cy);"],
        setStatements=["_typeUnion._unionTypes._cy = Decimal.ToOACurrency(value);"]),

    VariantType('DATE', "DateTime",
        unmanagedRepresentationType="Double",
        getStatements=["return DateTime.FromOADate(_typeUnion._unionTypes._date);"],
        setStatements=["_typeUnion._unionTypes._date = value.ToOADate();"]),
    VariantType('BSTR', "String",
        unmanagedRepresentationType="IntPtr",
        getStatements=[
                "if (_typeUnion._unionTypes._bstr != IntPtr.Zero) {",
                "    return Marshal.PtrToStringBSTR(_typeUnion._unionTypes._bstr);",
                "}",
                "return null;"
        ],
        setStatements=[
                "if (value != null) {",
                "    Marshal.GetNativeVariantForObject(value, UnsafeMethods.ConvertVariantByrefToPtr(ref this));",
                "}"
        ],
        critical=True),
    VariantType("UNKNOWN", "Object",
        isPrimitiveType=False,
        unmanagedRepresentationType="IntPtr",
        getStatements=[
                "if (_typeUnion._unionTypes._dispatch != IntPtr.Zero) {",
                "    return Marshal.GetObjectForIUnknown(_typeUnion._unionTypes._unknown);",
                "}",
                "return null;"
        ],
        setStatements=[
                "if (value != null) {",
                "    _typeUnion._unionTypes._unknown = Marshal.GetIUnknownForObject(value);",
                "}"
        ],
        critical=True),
    VariantType("DISPATCH", "Object",
        isPrimitiveType=False,
        unmanagedRepresentationType="IntPtr",
        getStatements=[
                "if (_typeUnion._unionTypes._dispatch != IntPtr.Zero) {",
                "    return Marshal.GetObjectForIUnknown(_typeUnion._unionTypes._dispatch);",
                "}",
                "return null;"
        ],
        setStatements=[
                "if (value != null) {",
                "    _typeUnion._unionTypes._unknown = GetIDispatchForObject(value);",
                "}"
        ],
        critical=True),
    VariantType("VARIANT", "Object",
        emitAccessors=False,
        isPrimitiveType=False,
        unmanagedRepresentationType="Variant",
        includeInUnionTypes=False,              # will use "this"
        getStatements=["return Marshal.GetObjectForNativeVariant(UnsafeMethods.ConvertVariantByrefToPtr(ref this));"],
        setStatements=["UnsafeMethods.InitVariantForObject(value, ref this);"],
        critical=True)

]

managed_types_to_variant_types_add = [
    ("Char",			"UI2"),
    ("CurrencyWrapper", "CY"),
    ("ErrorWrapper",    "ERROR"),
]

def gen_UnionTypes(cw):
    for variantType in variantTypes:
        variantType.write_UnionTypes(cw)

def gen_ToObject(cw):
    for variantType in variantTypes:
        variantType.write_ToObject(cw)

def gen_accessors(transparent):
    def gen_accessors(cw):
        for variantType in variantTypes:
            variantType.write_accessor(cw, transparent)
    return gen_accessors

def gen_accessor_propertyinfo(cw):
    for variantType in variantTypes:
        variantType.write_accessor_propertyinfo(cw)

def gen_byref_setters(cw):
    for variantType in variantTypes:
        variantType.write_byref_setters(cw)

def gen_ComToManagedPrimitiveTypes(cw):
    for variantType in variantTypes:
        variantType.write_ComToManagedPrimitiveTypes(cw)

def gen_ManagedToComPrimitiveTypes(cw):
    import System
    import clr
    # build inverse map
    type_map = {}
    for variantType in variantTypes:
        # take them in order, first one wins ... handles ERROR and INT32 conflict
        if variantType.isPrimitiveType and variantType.managedType not in type_map:
            type_map[variantType.managedType] = variantType.variantType

    for managedType, variantType in managed_types_to_variant_types_add:
        type_map[managedType] = variantType

    def is_system_type(name):
        t = getattr(System, name, None)
        return t and System.Type.GetTypeCode(t) not in [System.TypeCode.Empty, System.TypeCode.Object]

    system_types = filter(is_system_type, type_map.keys())
    system_types = sorted(system_types, key=lambda name: int(System.Type.GetTypeCode(getattr(System, name))))
    other_types = sorted(set(type_map.keys()).difference(set(system_types)))

    # switch from sytem types
    cw.enter_block("switch (Type.GetTypeCode(argumentType))")
    for t in system_types:
        cw.write("""case TypeCode.%(code)s:
    primitiveVarEnum = VarEnum.VT_%(vt)s;
    return true;""", code = System.Type.GetTypeCode(getattr(System, t)).ToString(), vt = type_map[t])
    cw.exit_block()

    # if statements from the rest
    for t in other_types:
        clrtype = getattr(System, t, None)
        if not clrtype: clrtype = getattr(System.Runtime.InteropServices, t, None)
        clrtype = clr.GetClrType(clrtype)
        cw.write("""
if (argumentType == typeof(%(type)s)) {
    primitiveVarEnum = VarEnum.VT_%(vt)s;
    return true;
}""", type = clrtype.Name, vt = type_map[t])

def gen_IsPrimitiveType(cw):
    for variantType in variantTypes:
        variantType.write_IsPrimitiveType(cw)

def gen_ConvertByrefToPtr(transparent):
    def gen_ConvertByrefToPtr(cw):
        for variantType in variantTypes:
            if transparent:
                variantType.write_ConvertByrefToPtr_Outer(cw, transparent)
            else:
                variantType.write_ConvertByrefToPtr(cw, transparent)
    return gen_ConvertByrefToPtr

def gen_ConvertByrefToPtrDelegates(cw):
    for variantType in variantTypes:
        variantType.write_ConvertByrefToPtrDelegates(cw)

def main():
    return generate(
        ("Convert ByRef Delegates", gen_ConvertByrefToPtrDelegates),
        ("Outer Managed To COM Primitive Type Map", gen_ManagedToComPrimitiveTypes),
        ("Outer Variant union types", gen_UnionTypes),
        ("Outer Variant ToObject", gen_ToObject),
        ("Outer Variant accessors", gen_accessors(True)),
        ("Outer Variant accessors PropertyInfos", gen_accessor_propertyinfo),
        ("Outer Variant byref setter", gen_byref_setters),
        ("Outer ComToManagedPrimitiveTypes", gen_ComToManagedPrimitiveTypes),
        ("Outer Variant IsPrimitiveType", gen_IsPrimitiveType),
        ("Outer ConvertByrefToPtr", gen_ConvertByrefToPtr(True)),
        #TODO: we don't build ndp\fx\src\Dynamic any more for IronPython
        #("Managed To COM Primitive Type Map", gen_ManagedToComPrimitiveTypes),
        #("Variant union types", gen_UnionTypes),
        #("Variant ToObject", gen_ToObject),
        #("Variant accessors", gen_accessors(False)),
        #("Variant accessors PropertyInfos", gen_accessor_propertyinfo),
        #("Variant byref setter", gen_byref_setters),
        #("ComToManagedPrimitiveTypes", gen_ComToManagedPrimitiveTypes),
        #("Variant IsPrimitiveType", gen_IsPrimitiveType),
        #("ConvertByrefToPtr", gen_ConvertByrefToPtr(False)),
    )

if __name__ == "__main__":
    main()
