# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import unittest

from iptest import is_cli, is_netcoreapp, run_test, skipUnlessIronPython

# set this flag to True to have the test trace progress
trace = False

skipTypes =  [
    'System.Void',
    'System.ArgIterator',
    'System.RuntimeArgumentHandle',
    'System.TypedReference'
    ]

counter = 1

if is_cli:
    import clr
    import System
    import System.IO as IO
    import System.Reflection as Reflection
    import System.Reflection.Emit as Emit
    import System.Runtime.InteropServices as Interop
    import System.Diagnostics as Diagnostics
    from System.Reflection.Emit import OpCodes

    try:
        System.Type.IsByRefLike
        supportsIsByRefLike = True
    except AttributeError:
        supportsIsByRefLike = False

def MakeArray(type, *values):
    a = System.Array.CreateInstance(clr.GetClrType(type), len(values))
    for i in range(len(values)):
        a[i] = values[i]
    return a

class AssemblyGenerator:
    def __init__(self, fileName, ab, mb, sdw):
        self.fileName = fileName
        self.ab = ab
        self.mb = mb
        self.sdw = sdw

    def DefineType(self, name):
        return TypeGenerator(self, self.mb.DefineType(name, Reflection.TypeAttributes.Public))

    def Save(self):
        self.ab.Save(self.fileName, Reflection.PortableExecutableKinds.ILOnly, Reflection.ImageFileMachine.I386);

def CreateAssemblyGenerator(path, name):
    dir = IO.Path.GetDirectoryName(path)
    file = IO.Path.GetFileName(path)

    aname = Reflection.AssemblyName(name)
    domain = System.AppDomain.CurrentDomain
    ab = domain.DefineDynamicAssembly(aname, Emit.AssemblyBuilderAccess.RunAndSave, dir, None)
    mb = ab.DefineDynamicModule(file, file, True);

    ab.DefineVersionInfoResource()

    constructor = clr.GetClrType(Diagnostics.DebuggableAttribute).GetConstructor(MakeArray(System.Type, clr.GetClrType(Diagnostics.DebuggableAttribute.DebuggingModes)))
    attributeValue = MakeArray(System.Object,
        Diagnostics.DebuggableAttribute.DebuggingModes.Default |
        Diagnostics.DebuggableAttribute.DebuggingModes.DisableOptimizations |
        Diagnostics.DebuggableAttribute.DebuggingModes.IgnoreSymbolStoreSequencePoints
    )
    cab = Emit.CustomAttributeBuilder(constructor, attributeValue)

    ab.SetCustomAttribute(cab)
    mb.SetCustomAttribute(cab)

    return AssemblyGenerator(file, ab, mb, None)

class TypeGenerator:
    def __init__(self, ag, tb):
        self.tb = tb
        self.ag = ag

    def DefineMethod(self, name, attributes, returnType, parameterTypes):
        params = MakeArray(System.Type, parameterTypes)
        mb = self.tb.DefineMethod(name, attributes, returnType, params)
        ilg = mb.GetILGenerator()
        return CodeGenerator(self, mb, ilg)

    def CreateType(self):
        return self.tb.CreateType()

class CodeGenerator:
    def __init__(self, tg, mb, ilg):
        self.tg = tg
        self.mb = mb
        self.ilg = ilg
        self.sdw = tg.ag.sdw

def EmitArg(cg, arg):
    cg.ilg.Emit(OpCodes.Ldarg_0)

    TypeCode = System.TypeCode

    if arg.IsByRef:
        base = arg.GetElementType()
        if not base.IsValueType:
            cg.ilg.Emit(OpCodes.Ldind_Ref)
        else:
            tc = System.Type.GetTypeCode(base)
            if tc == TypeCode.Int32:
                cg.ilg.Emit(OpCodes.Ldind_I4)
            elif tc == TypeCode.Object:
                cg.ilg.Emit(OpCodes.Ldobj, base)
            elif tc == TypeCode.Boolean:
                cg.ilg.Emit(OpCodes.Ldind_I1)
            elif tc == TypeCode.Byte:
                cg.ilg.Emit(OpCodes.Ldind_U1)
            elif tc == TypeCode.Char:
                cg.ilg.Emit(OpCodes.Ldind_I2)
            elif tc == TypeCode.DateTime:
                cg.ilg.Emit(OpCodes.Ldobj, base)
            elif tc == TypeCode.DBNull: pass
            elif tc == TypeCode.Decimal:
                cg.ilg.Emit(OpCodes.Ldobj, base)
            elif tc == TypeCode.Double:
                cg.ilg.Emit(OpCodes.Ldind_R8)
            #elif tc == TypeCode.Empty: pass
            elif tc == TypeCode.Int16:
                cg.ilg.Emit(OpCodes.Ldind_I2)
            elif tc == TypeCode.Int64:
                cg.ilg.Emit(OpCodes.Ldind_I8)
            elif tc == TypeCode.SByte:
                cg.ilg.Emit(OpCodes.Ldind_I1)
            elif tc == TypeCode.Single:
                cg.ilg.Emit(OpCodes.Ldind_R4)
            #elif tc == TypeCode.String: pass
            elif tc == TypeCode.UInt16:
                cg.ilg.Emit(OpCodes.Ldind_U2)
            elif tc == TypeCode.UInt32:
                cg.ilg.Emit(OpCodes.Ldind_U4)
            elif tc == TypeCode.UInt64:
                cg.ilg.Emit(OpCodes.Ldind_I8)
            else:
                raise AssertionError("Unknown type", arg, base, tc)

def EmitTestMethod(tg, name, argType):
    cg = tg.DefineMethod(name, Reflection.MethodAttributes.Public | Reflection.MethodAttributes.Static, str, argType)
    pb = cg.mb.DefineParameter(1, Reflection.ParameterAttributes.In | Reflection.ParameterAttributes.Optional, "o");

    tostring = cg.ilg.DeclareLocal(str)

    baseType = argType
    if baseType.IsByRef:
        baseType = baseType.GetElementType()

    if not baseType.IsValueType:
        is_null = cg.ilg.DefineLabel()
        end = cg.ilg.DefineLabel()
        EmitArg(cg, argType)
        cg.ilg.Emit(OpCodes.Brfalse_S, is_null)
        cg.ilg.BeginExceptionBlock()
        EmitArg(cg, argType)
        cg.ilg.EmitCall(OpCodes.Callvirt, clr.GetClrType(object).GetMethod("ToString"), None)
        cg.ilg.Emit(OpCodes.Stloc, tostring)
        cg.ilg.BeginCatchBlock(clr.GetClrType(System.Exception))
        cg.ilg.Emit(OpCodes.Callvirt, clr.GetClrType(System.Exception).GetMethod("get_Message"))
        cg.ilg.Emit(OpCodes.Stloc, tostring)
        cg.ilg.Emit(OpCodes.Ldstr, "#EXCEPTION#")
        cg.ilg.Emit(OpCodes.Ldloc, tostring)
        cg.ilg.EmitCall(OpCodes.Call, clr.GetClrType(str).GetMethod("Concat", MakeArray(System.Type, clr.GetClrType(System.String), clr.GetClrType(System.String))), None)
        cg.ilg.Emit(OpCodes.Stloc, tostring)
        cg.ilg.EndExceptionBlock()
        cg.ilg.Emit(OpCodes.Br_S, end);
        cg.ilg.MarkLabel(is_null)
        cg.ilg.Emit(OpCodes.Ldstr, "(null)")
        cg.ilg.Emit(OpCodes.Stloc, tostring)
        cg.ilg.MarkLabel(end)
    else:
        cg.ilg.BeginExceptionBlock()
        EmitArg(cg, argType)
        cg.ilg.Emit(OpCodes.Box, baseType);
        cg.ilg.EmitCall(OpCodes.Callvirt, clr.GetClrType(object).GetMethod("ToString"), None)
        cg.ilg.Emit(OpCodes.Stloc, tostring)
        cg.ilg.BeginCatchBlock(clr.GetClrType(System.Exception))
        cg.ilg.Emit(OpCodes.Callvirt, clr.GetClrType(System.Exception).GetMethod("get_Message"))
        cg.ilg.Emit(OpCodes.Stloc, tostring)
        cg.ilg.Emit(OpCodes.Ldstr, "#EXCEPTION#")
        cg.ilg.Emit(OpCodes.Ldloc, tostring)
        cg.ilg.EmitCall(OpCodes.Call, clr.GetClrType(System.String).GetMethod("Concat", MakeArray(System.Type, clr.GetClrType(System.String), clr.GetClrType(System.String))), None)
        cg.ilg.Emit(OpCodes.Stloc, tostring)
        cg.ilg.EndExceptionBlock()

    cg.ilg.Emit(OpCodes.Ldstr, name + "$" + argType.FullName + "$")
    cg.ilg.Emit(OpCodes.Ldloc, tostring)
    cg.ilg.EmitCall(OpCodes.Call, clr.GetClrType(System.String).GetMethod("Concat", MakeArray(System.Type, clr.GetClrType(System.String), clr.GetClrType(System.String))), None)
    if trace:
        cg.ilg.Emit(OpCodes.Dup)
        cg.ilg.EmitCall(OpCodes.Call, clr.GetClrType(System.Console).GetMethod("WriteLine", MakeArray(System.Type, clr.GetClrType(System.String))), None)
    cg.ilg.Emit(OpCodes.Ret)

def GenerateMethods(ag):
    global counter
    tg = ag.DefineType("Abomination")

    system_assembly   = clr.LoadAssemblyByName('System, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089')
    mscorlib_assembly = clr.LoadAssemblyByName('mscorlib, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089')

    for ref in (system_assembly, mscorlib_assembly):
        for t in ref.GetExportedTypes():
            if t.ContainsGenericParameters or t.FullName in skipTypes: continue
            if supportsIsByRefLike and t.IsByRefLike: continue

            if trace: print(counter, t.FullName)
            EmitTestMethod(tg, "Test_%d" % counter, t)
            EmitTestMethod(tg, "TestRef_%d" % counter, t.MakeByRefType())
            counter += 1

    return tg.CreateType()

def TestCalls(t):
    t = clr.GetPythonType(t)
    for i in range(1, counter):
        n = "Test_%d" % i
        r = getattr(t, n)()
        if trace: print(r)

        n = "TestRef_%d" % i
        r = getattr(t, n)()
        if trace: print(r)

@unittest.skipIf(is_netcoreapp, "TODO: figure out, make parallel-safe")
@skipUnlessIronPython()
class MissingTest(unittest.TestCase):
    def test_main(self):
        path = IO.Path.GetTempPath()
        ag = CreateAssemblyGenerator(IO.Path.Combine(path, "MissingTest.dll"), "MissingTest")
        t = GenerateMethods(ag)
        TestCalls(t)

        ag.Save()

run_test(__name__)
