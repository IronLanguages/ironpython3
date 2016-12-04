/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the  Apache License, Version 2.0, please send an email to 
 * ironpy@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 *
 * ***************************************************************************/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime;
using IronPython.Runtime.Types;

namespace IronPythonTest {
    [Flags]
    public enum BindResult {
        None = 0,

        Bool = 1,
        Byte = 2,
        Char = 3,
        Decimal = 4,
        Double = 5,
        Float = 6,
        Int = 7,
        Long = 8,
        Object = 9,
        SByte = 10,
        Short = 11,
        String = 12,
        UInt = 13,
        ULong = 14,
        UShort = 15,

        Array = 0x1000,
        Out = 0x2000,
        Ref = 0x4000,
    }

    public class BindTest {
        public static object BoolValue = (bool)true;
        public static object ByteValue = (byte)0;
        public static object CharValue = (char)'\0';
        public static object DecimalValue = (decimal)0;
        public static object DoubleValue = (double)0;
        public static object FloatValue = (float)0;
        public static object IntValue = (int)0;
        public static object LongValue = (long)0;
#if !SILVERLIGHT
        public static object ObjectValue = (object)new System.Collections.Hashtable();
#else
        public static object ObjectValue = (object)new Dictionary<object, object>();
#endif
        public static object SByteValue = (sbyte)0;
        public static object ShortValue = (short)0;
        public static object StringValue = (string)String.Empty;
        public static object UIntValue = (uint)0;
        public static object ULongValue = (ulong)0;
        public static object UShortValue = (ushort)0;

        public static BindResult Bind() { return BindResult.None; }

        public static BindResult Bind(bool value) { return BindResult.Bool; }
        public static BindResult Bind(byte value) { return BindResult.Byte; }
        public static BindResult Bind(char value) { return BindResult.Char; }
        public static BindResult Bind(decimal value) { return BindResult.Decimal; }
        public static BindResult Bind(double value) { return BindResult.Double; }
        public static BindResult Bind(float value) { return BindResult.Float; }
        public static BindResult Bind(int value) { return BindResult.Int; }
        public static BindResult Bind(long value) { return BindResult.Long; }
        public static BindResult Bind(object value) { return BindResult.Object; }
        public static BindResult Bind(sbyte value) { return BindResult.SByte; }
        public static BindResult Bind(short value) { return BindResult.Short; }
        public static BindResult Bind(string value) { return BindResult.String; }
        public static BindResult Bind(uint value) { return BindResult.UInt; }
        public static BindResult Bind(ulong value) { return BindResult.ULong; }
        public static BindResult Bind(ushort value) { return BindResult.UShort; }

        public static BindResult Bind(bool[] value) { return BindResult.Bool | BindResult.Array; }
        public static BindResult Bind(byte[] value) { return BindResult.Byte | BindResult.Array; }
        public static BindResult Bind(char[] value) { return BindResult.Char | BindResult.Array; }
        public static BindResult Bind(decimal[] value) { return BindResult.Decimal | BindResult.Array; }
        public static BindResult Bind(double[] value) { return BindResult.Double | BindResult.Array; }
        public static BindResult Bind(float[] value) { return BindResult.Float | BindResult.Array; }
        public static BindResult Bind(int[] value) { return BindResult.Int | BindResult.Array; }
        public static BindResult Bind(long[] value) { return BindResult.Long | BindResult.Array; }
        public static BindResult Bind(object[] value) { return BindResult.Object | BindResult.Array; }
        public static BindResult Bind(sbyte[] value) { return BindResult.SByte | BindResult.Array; }
        public static BindResult Bind(short[] value) { return BindResult.Short | BindResult.Array; }
        public static BindResult Bind(string[] value) { return BindResult.String | BindResult.Array; }
        public static BindResult Bind(uint[] value) { return BindResult.UInt | BindResult.Array; }
        public static BindResult Bind(ulong[] value) { return BindResult.ULong | BindResult.Array; }
        public static BindResult Bind(ushort[] value) { return BindResult.UShort | BindResult.Array; }

        public static BindResult Bind(out bool value) { value = false; return BindResult.Bool | BindResult.Out; }
        public static BindResult Bind(out byte value) { value = 0; return BindResult.Byte | BindResult.Out; }
        public static BindResult Bind(out char value) { value = '\0'; return BindResult.Char | BindResult.Out; }
        public static BindResult Bind(out decimal value) { value = 0; return BindResult.Decimal | BindResult.Out; }
        public static BindResult Bind(out double value) { value = 0; return BindResult.Double | BindResult.Out; }
        public static BindResult Bind(out float value) { value = 0; return BindResult.Float | BindResult.Out; }
        public static BindResult Bind(out int value) { value = 0; return BindResult.Int | BindResult.Out; }
        public static BindResult Bind(out long value) { value = 0; return BindResult.Long | BindResult.Out; }
        public static BindResult Bind(out object value) { value = null; return BindResult.Object | BindResult.Out; }
        public static BindResult Bind(out sbyte value) { value = 0; return BindResult.SByte | BindResult.Out; }
        public static BindResult Bind(out short value) { value = 0; return BindResult.Short | BindResult.Out; }
        public static BindResult Bind(out string value) { value = null; return BindResult.String | BindResult.Out; }
        public static BindResult Bind(out uint value) { value = 0; return BindResult.UInt | BindResult.Out; }
        public static BindResult Bind(out ulong value) { value = 0; return BindResult.ULong | BindResult.Out; }
        public static BindResult Bind(out ushort value) { value = 0; return BindResult.UShort | BindResult.Out; }

        public static BindResult BindRef(ref bool value) { value = false; return BindResult.Bool | BindResult.Ref; }
        public static BindResult BindRef(ref byte value) { value = 0; return BindResult.Byte | BindResult.Ref; }
        public static BindResult BindRef(ref char value) { value = '\0'; return BindResult.Char | BindResult.Ref; }
        public static BindResult BindRef(ref decimal value) { value = 0; return BindResult.Decimal | BindResult.Ref; }
        public static BindResult BindRef(ref double value) { value = 0; return BindResult.Double | BindResult.Ref; }
        public static BindResult BindRef(ref float value) { value = 0; return BindResult.Float | BindResult.Ref; }
        public static BindResult BindRef(ref int value) { value = 0; return BindResult.Int | BindResult.Ref; }
        public static BindResult BindRef(ref long value) { value = 0; return BindResult.Long | BindResult.Ref; }
        public static BindResult BindRef(ref object value) { value = null; return BindResult.Object | BindResult.Ref; }
        public static BindResult BindRef(ref sbyte value) { value = 0; return BindResult.SByte | BindResult.Ref; }
        public static BindResult BindRef(ref short value) { value = 0; return BindResult.Short | BindResult.Ref; }
        public static BindResult BindRef(ref string value) { value = null; return BindResult.String | BindResult.Ref; }
        public static BindResult BindRef(ref uint value) { value = 0; return BindResult.UInt | BindResult.Ref; }
        public static BindResult BindRef(ref ulong value) { value = 0; return BindResult.ULong | BindResult.Ref; }
        public static BindResult BindRef(ref ushort value) { value = 0; return BindResult.UShort | BindResult.Ref; }

        public static object ReturnTest(string type) {
            switch (type) {
                case "char": return 'a';
                case "object": return new object();
                case "null": return null;
#if FEATURE_COM
                case "com":
                    Type t = Type.GetTypeFromProgID("JScript");
                    return Activator.CreateInstance(t);
#endif
            }
            throw new NotImplementedException("unknown type");
        }
    }

    namespace DispatchHelpers {
        public class B { }
        public class D : B { }

        public interface I { }
        public class C1 : I { }
        public class C2 : I { }

        public enum Color { Red, Blue }
    }

    public class Dispatch {
        public static int Flag = 0;

        public void M1(int arg) { Flag = 101; }
        public void M1(DispatchHelpers.Color arg) { Flag = 201; }

        public void M2(int arg) { Flag = 102; }
        public void M2(int arg, params int[] arg2) { Flag = 202; }

        public void M3(int arg) { Flag = 103; }
        public void M3(int arg, int arg2) { Flag = 203; }

        public void M4(int arg) { Flag = 104; }
        public void M4(int arg, __arglist) { Flag = 204; }

        public void M5(float arg) { Flag = 105; }
        public void M5(double arg) { Flag = 205; }

        public void M6(char arg) { Flag = 106; }
        public void M6(string arg) { Flag = 206; }

        public void M7(int arg) { Flag = 107; }
        public void M7(params int[] args) { Flag = 207; }

        public void M8(int arg) { Flag = 108; }
        public void M8(ref int arg) { Flag = 208; arg = 999; }

        public void M10(ref int arg) { Flag = 210; arg = 999; }

        public void M11(int arg, int arg2) { Flag = 111; }
        public void M11(DispatchHelpers.Color arg, int arg2) { Flag = 211; }

        public void M12(int arg, DispatchHelpers.Color arg2) { Flag = 112; }
        public void M12(DispatchHelpers.Color arg, int arg2) { Flag = 212; }

        public void M20(DispatchHelpers.B arg) { Flag = 120; }

        public void M22(DispatchHelpers.B arg) { Flag = 122; }
        public void M22(DispatchHelpers.D arg) { Flag = 222; }

        public void M23(DispatchHelpers.I arg) { Flag = 123; }
        public void M23(DispatchHelpers.C2 arg) { Flag = 223; }

        public void M50(params DispatchHelpers.B[] args) { Flag = 150; }

        public void M51(params DispatchHelpers.B[] args) { Flag = 151; }
        public void M51(params DispatchHelpers.D[] args) { Flag = 251; }

        public void M60(int? arg) { Flag = 160; }

        public void M70(Dispatch arg) { Flag = 170; }
        public static void M71(Dispatch arg) { Flag = 171; }

        public static void M81(Dispatch arg, int arg2) { Flag = 181; }
        public void M81(int arg) { Flag = 281; }

        public static void M82(bool arg) { Flag = 182; }
        public static void M82(string arg) { Flag = 282; }

        public void M83(bool arg) { Flag = 183; }
        public void M83(string arg) { Flag = 283; }

        public void M90<T>(int arg) { Flag = 190; }

        public void M91(int arg) { Flag = 191; }
        public void M91<T>(int arg) { Flag = 291; }

        public static int M92(out int i, out int j, out int k, bool boolIn) {
            i = 1;
            j = 2;
            k = 3;
            Flag = 192;
            return boolIn ? 4 : 5;
        }

        public int M93(out int i, out int j, out int k, bool boolIn) {
            i = 1;
            j = 2;
            k = 3;
            Flag = 193;
            return boolIn ? 4 : 5;
        }

        public int M94(out int i, out int j, bool boolIn, out int k) {
            i = 1;
            j = 2;
            k = 3;
            Flag = 194;
            return boolIn ? 4 : 5;
        }

        public static int M95(out int i, out int j, bool boolIn, out int k) {
            i = 1;
            j = 2;
            k = 3;
            Flag = 195;
            return boolIn ? 4 : 5;
        }

        public static int M96(out int x, out int j, params int[] extras) {
            x = 1;
            j = 2;
            int res = 0;
            for (int i = 0; i < extras.Length; i++) res += extras[i];
            Flag = 196;
            return res;
        }

        public int M97(out int x, out int j, params int[] extras) {
            x = 1;
            j = 2;
            int res = 0;
            for (int i = 0; i < extras.Length; i++) res += extras[i];
            Flag = 197;
            return res;
        }

        public void M98(string a, string b, string c, string d, out int x, ref Dispatch di) {
            x = Convert.ToInt32(a) + Convert.ToInt32(b) + Convert.ToInt32(c) + Convert.ToInt32(d);

            di = this;

            Flag = 198;
        }
        
        public Single P01 {
            get; set;
        }
    }

    public class DispatchBase {
        public virtual void M1(int arg) { Dispatch.Flag = 101; }
        public virtual void M2(int arg) { Dispatch.Flag = 102; }
        public virtual void M3(int arg) { Dispatch.Flag = 103; }
        public void M4(int arg) { Dispatch.Flag = 104; }
        public virtual void M5(int arg) { Dispatch.Flag = 105; }
        public virtual void M6(int arg) { Dispatch.Flag = 106; }
    }

    public class DispatchDerived : DispatchBase {
        public override void M1(int arg) { Dispatch.Flag = 201; }
        public virtual void M2(DispatchHelpers.Color arg) { Dispatch.Flag = 202; }
        public virtual void M3(string arg) { Dispatch.Flag = 203; }
        public void M4(string arg) { Dispatch.Flag = 204; }
        public new virtual void M5(int arg) { Dispatch.Flag = 205; }
    }

    public class ConversionDispatch {
        public object Array(object[] arr) {
            return arr;
        }

        public object IntArray(int[] arr) {
            return arr;
        }

        public object StringArray(string[] arr) {
            return arr;
        }

        public object Enumerable(IEnumerable<object> enm) {
            return enm;
        }

        public object StringEnumerable(IEnumerable<string> enm) {
            return enm;
        }

        public object StringEnumerator(IEnumerator<string> enm) {
            return enm;
        }

        public object ObjectEnumerator(IEnumerator<object> enm) {
            return enm;
        }

        public object NonGenericEnumerator(IEnumerator enm) {
            return enm;
        }

        public object IntEnumerable(IEnumerable<int> enm) {
            return enm;
        }

        public object ObjIList(IList<object> list) {
            return list;
        }

        public object IntIList(IList<int> list) {
            return list;
        }

        public object StringIList(IList<string> list) {
            return list;
        }

        public object ObjList(List<object> list) {
            return list;
        }

        public object IntList(List<int> list) {
            return list;
        }

        public object StringList(List<string> list) {
            return list;
        }

        public object DictTest(IDictionary<object, object> dict) {
            return dict;
        }

        public object IntDictTest(IDictionary<int, int> dict) {
            return dict;
        }

        public object StringDictTest(IDictionary<string, string> dict) {
            return dict;
        }

        public object MixedDictTest(IDictionary<string, int> dict) {
            return dict;
        }

#if !SILVERLIGHT
        public object ArrayList(System.Collections.ArrayList list) {
            return list;
        }

        public object HashtableTest(Hashtable dict) {
            return dict;
        }
#endif
    }

    public class MixedDispatch {
        public string instance_name;
        public string called;

        public MixedDispatch(string name) {
            instance_name = name;
        }

        public static object Combine(MixedDispatch md1, MixedDispatch md2) {
            MixedDispatch md_res = new MixedDispatch(md1.instance_name + md2.instance_name);
            md_res.called = "static";
            return md_res;
        }

        public object Combine(MixedDispatch other) {
            MixedDispatch md_res = new MixedDispatch(other.instance_name + instance_name);
            md_res.called = "instance";
            return md_res;
        }


        public object Combine2(MixedDispatch other) {
            MixedDispatch md_res = new MixedDispatch(other.instance_name + instance_name);
            md_res.called = "instance";
            return md_res;
        }

        public static object Combine2(MixedDispatch md1, MixedDispatch md2) {
            MixedDispatch md_res = new MixedDispatch(md1.instance_name + md2.instance_name);
            md_res.called = "static";
            return md_res;
        }

        public object Combine2(MixedDispatch other, MixedDispatch other2, MixedDispatch other3) {
            MixedDispatch md_res = new MixedDispatch(other.instance_name + instance_name + other2.instance_name + other3.instance_name);
            md_res.called = "instance_three";
            return md_res;
        }
    }

    public class FieldTest {
        public IList<Type> Field;
    }

    public class ComparisonTest {
        public delegate void Reporter(string name);
        public double value;
        public static Reporter report;
        public ComparisonTest(double value) {
            this.value = value;
        }
        public override string ToString() {
            return string.Format(System.Globalization.CultureInfo.InvariantCulture, "ct<{0}>", value);
        }
        public static bool operator <(ComparisonTest x, ComparisonTest y) {
            Report("<", x, y);
            return x.value < y.value;
        }
        public static bool operator >(ComparisonTest x, ComparisonTest y) {
            Report(">", x, y);
            return x.value > y.value;
        }
        public static bool operator <=(ComparisonTest x, ComparisonTest y) {
            Report("<=", x, y);
            return x.value <= y.value;
        }
        public static bool operator >=(ComparisonTest x, ComparisonTest y) {
            Report(">=", x, y);
            return x.value >= y.value;
        }
        private static void Report(string op, ComparisonTest x, ComparisonTest y) {
            if (report != null) {
                report(string.Format("{0} on [{1}, {2}]", op, x, y));
            }
        }
    }

    public enum BigEnum : long {
        None,
        BigValue = UInt32.MaxValue,
    }

    public class DefaultValueTest {
        public BindResult Test_Enum([DefaultParameterValue(BindResult.Bool)] BindResult param) {
            return param;
        }

        public BigEnum Test_BigEnum([DefaultParameterValue(BigEnum.BigValue)] BigEnum param) {
            return param;
        }

        public string Test_String([DefaultParameterValue("Hello World")] string param) {
            return param;
        }

        public int Test_Int([DefaultParameterValue(5)] int param) {
            return param;
        }

        public uint Test_UInt([DefaultParameterValue(uint.MaxValue)] uint param) {
            return param;
        }

        public bool Test_Bool([DefaultParameterValue(true)] bool param) {
            return param;
        }

        public char Test_Char([DefaultParameterValue('A')] char param) {
            return param;
        }

        public byte Test_Byte([DefaultParameterValue((byte)2)] byte param) {
            return param;
        }

        public sbyte Test_SByte([DefaultParameterValue((sbyte)2)] sbyte param) {
            return param;
        }

        public short Test_Short([DefaultParameterValue((short)2)] short param) {
            return param;
        }

        public ushort Test_UShort([DefaultParameterValue((ushort)2)] ushort param) {
            return param;
        }

        public long Test_Long([DefaultParameterValue(long.MaxValue)] long param) {
            return param;
        }

        public ulong Test_ULong([DefaultParameterValue(ulong.MaxValue)] ulong param) {
            return param;
        }

        public string Test_ByRef_Object([In, Optional] ref object o1, [In, Optional] ref object o2, [In, Optional] ref object o3) {
            return (o1 == null ? "(null)" : o1.ToString()) + "; " +
                   (o2 == null ? "(null)" : o2.ToString()) + "; " +
                   (o3 == null ? "(null)" : o3.ToString());
        }

        public string Test_Default_ValueType([DefaultParameterValue(1)] object o) {
            return o == null ? "(null)" : o.ToString();
        }

        public string Test_Default_Cast([Optional, DefaultParameterValue(1)]ref object o) {
            return o == null ? "(null)" : o.ToString();
        }
    }

    public struct Structure {
        public int a;
        public float b;
        public double c;
        public decimal d;
        public string e;
#if !SILVERLIGHT
        public Hashtable f;
#else
        public Dictionary<object, object> f;
#endif
    }

    public class MissingValueTest {
        public static string Test_1([In, Optional] bool o) { return "(bool)" + o.ToString(); }
        public static string Test_2([In, Optional] ref bool o) { return "(bool)" + o.ToString(); }

        public static string Test_3([In, Optional] sbyte o) { return "(sbyte)" + o.ToString(); }
        public static string Test_4([In, Optional] ref sbyte o) { return "(sbyte)" + o.ToString(); }

        public static string Test_5([In, Optional] byte o) { return "(byte)" + o.ToString(); }
        public static string Test_6([In, Optional] ref byte o) { return "(byte)" + o.ToString(); }

        public static string Test_7([In, Optional] short o) { return "(short)" + o.ToString(); }
        public static string Test_8([In, Optional] ref short o) { return "(short)" + o.ToString(); }

        public static string Test_9([In, Optional] ushort o) { return "(ushort)" + o.ToString(); }
        public static string Test_10([In, Optional] ref ushort o) { return "(ushort)" + o.ToString(); }

        public static string Test_11([In, Optional] int o) { return "(int)" + o.ToString(); }
        public static string Test_12([In, Optional] ref int o) { return "(int)" + o.ToString(); }

        public static string Test_13([In, Optional] uint o) { return "(uint)" + o.ToString(); }
        public static string Test_14([In, Optional] ref uint o) { return "(uint)" + o.ToString(); }

        public static string Test_15([In, Optional] long o) { return "(long)" + o.ToString(); }
        public static string Test_16([In, Optional] ref long o) { return "(long)" + o.ToString(); }

        public static string Test_17([In, Optional] ulong o) { return "(ulong)" + o.ToString(); }
        public static string Test_18([In, Optional] ref ulong o) { return "(ulong)" + o.ToString(); }

        public static string Test_19([In, Optional] decimal o) { return "(decimal)" + o.ToString(); }
        public static string Test_20([In, Optional] ref decimal o) { return "(decimal)" + o.ToString(); }

        public static string Test_21([In, Optional] float o) { return "(float)" + o.ToString(); }
        public static string Test_22([In, Optional] ref float o) { return "(float)" + o.ToString(); }

        public static string Test_23([In, Optional] double o) { return "(double)" + o.ToString(); }
        public static string Test_24([In, Optional] ref double o) { return "(double)" + o.ToString(); }

        public static string Test_25([In, Optional] DaysByte o) { return "(DaysByte)" + o.ToString(); }
        public static string Test_26([In, Optional] ref DaysByte o) { return "(DaysByte)" + o.ToString(); }

        public static string Test_27([In, Optional] DaysSByte o) { return "(DaysSByte)" + o.ToString(); }
        public static string Test_28([In, Optional] ref DaysSByte o) { return "(DaysSByte)" + o.ToString(); }

        public static string Test_29([In, Optional] DaysShort o) { return "(DaysShort)" + o.ToString(); }
        public static string Test_30([In, Optional] ref DaysShort o) { return "(DaysShort)" + o.ToString(); }

        public static string Test_31([In, Optional] DaysUShort o) { return "(DaysUShort)" + o.ToString(); }
        public static string Test_32([In, Optional] ref DaysUShort o) { return "(DaysUShort)" + o.ToString(); }

        public static string Test_33([In, Optional] DaysInt o) { return "(DaysInt)" + o.ToString(); }
        public static string Test_34([In, Optional] ref DaysInt o) { return "(DaysInt)" + o.ToString(); }

        public static string Test_35([In, Optional] DaysUInt o) { return "(DaysUInt)" + o.ToString(); }
        public static string Test_36([In, Optional] ref DaysUInt o) { return "(DaysUInt)" + o.ToString(); }

        public static string Test_37([In, Optional] DaysLong o) { return "(DaysLong)" + o.ToString(); }
        public static string Test_38([In, Optional] ref DaysLong o) { return "(DaysLong)" + o.ToString(); }

        public static string Test_39([In, Optional] DaysULong o) { return "(DaysULong)" + o.ToString(); }
        public static string Test_40([In, Optional] ref DaysULong o) { return "(DaysULong)" + o.ToString(); }

        public static string Test_41([In, Optional] char o) { return "(char)" + o.ToString(); }
        public static string Test_42([In, Optional] ref char o) { return "(char)" + o.ToString(); }

        public static string Test_43([In, Optional] Structure o) { return "(Structure)" + o.ToString(); }
        public static string Test_44([In, Optional] ref Structure o) { return "(Structure)" + o.ToString(); }

        public static string Test_45([In, Optional] EnumSByte o) { return "(EnumSByte)" + o.ToString(); }
        public static string Test_46([In, Optional] ref EnumSByte o) { return "(EnumSByte)" + o.ToString(); }

        public static string Test_47([In, Optional] EnumByte o) { return "(EnumByte)" + o.ToString(); }
        public static string Test_48([In, Optional] ref EnumByte o) { return "(EnumByte)" + o.ToString(); }

        public static string Test_49([In, Optional] EnumShort o) { return "(EnumShort)" + o.ToString(); }
        public static string Test_50([In, Optional] ref EnumShort o) { return "(EnumShort)" + o.ToString(); }

        public static string Test_51([In, Optional] EnumUShort o) { return "(EnumUShort)" + o.ToString(); }
        public static string Test_52([In, Optional] ref EnumUShort o) { return "(EnumUShort)" + o.ToString(); }

        public static string Test_53([In, Optional] EnumInt o) { return "(EnumInt)" + o.ToString(); }
        public static string Test_54([In, Optional] ref EnumInt o) { return "(EnumInt)" + o.ToString(); }

        public static string Test_55([In, Optional] EnumUInt o) { return "(EnumUInt)" + o.ToString(); }
        public static string Test_56([In, Optional] ref EnumUInt o) { return "(EnumUInt)" + o.ToString(); }

        public static string Test_57([In, Optional] EnumLong o) { return "(EnumLong)" + o.ToString(); }
        public static string Test_58([In, Optional] ref EnumLong o) { return "(EnumLong)" + o.ToString(); }

        public static string Test_59([In, Optional] EnumULong o) { return "(EnumULong)" + o.ToString(); }
        public static string Test_60([In, Optional] ref EnumULong o) { return "(EnumULong)" + o.ToString(); }

        public static string Test_61([In, Optional] string o) { return "(string)" + (o == null ? "(null)" : o); }
        public static string Test_62([In, Optional] ref string o) { return "(string)" + (o == null ? "(null)" : o); }

        public static string Test_63([In, Optional] object o) { return "(object)" + (o == null ? "(null)" : o.ToString()); }
        public static string Test_64([In, Optional] ref object o) { return "(object)" + (o == null ? "(null)" : o.ToString()); }

        public static string Test_65([In, Optional] MissingValueTest o) { return "(MissingValueTest)" + (o == null ? "(null)" : o.ToString()); }
        public static string Test_66([In, Optional] ref MissingValueTest o) { return "(MissingValueTest)" + (o == null ? "(null)" : o.ToString()); }
    }

    public class DispatchAgain {
        // ***************** OptimizedFunctionX *****************
        public string IM0() { return "IM0"; }
        public string IM1(int arg1) { return "IM1"; }
        public string IM2(int arg1, int arg2) { return "IM2"; }
        public string IM3(int arg1, int arg2, int arg3) { return "IM3"; }
        public string IM4(int arg1, int arg2, int arg3, int arg4) { return "IM4"; }
        public string IM5(int arg1, int arg2, int arg3, int arg4, int arg5) { return "IM5"; }
        public string IM6(int arg1, int arg2, int arg3, int arg4, int arg5, int arg6) { return "IM6"; }

        public static string SM0() { return "SM0"; }
        public static string SM1(int arg1) { return "SM1"; }
        public static string SM2(int arg1, int arg2) { return "SM2"; }
        public static string SM3(int arg1, int arg2, int arg3) { return "SM3"; }
        public static string SM4(int arg1, int arg2, int arg3, int arg4) { return "SM4"; }
        public static string SM5(int arg1, int arg2, int arg3, int arg4, int arg5) { return "SM5"; }
        public static string SM6(int arg1, int arg2, int arg3, int arg4, int arg5, int arg6) { return "SM6"; }

        // ***************** OptimizedFunctionN *****************
        public string IPM0(params int[] args) { return "IPM0-" + args.Length; }
        public static string SPM0(params int[] args) { return "SPM0-" + args.Length; }

        // suppose to be optimized?
        public string IPM1(int arg1, params int[] args) { return "IPM1-" + args.Length; }
        public static string SPM1(int arg1, params int[] args) { return "SPM1-" + args.Length; }

        // ***************** OptimizedFunctionAny *****************
        // 1. miss 1, 2 arguments, and no params
        public string IDM0() { return "IDM0-0"; }
        public string IDM0(int arg1, int arg2, int arg3) { return "IDM0-3"; }
        public string IDM0(int arg1, int arg2, int arg3, int arg4) { return "IDM0-4"; }

        public static string SDM0() { return "SDM0-0"; }
        public static string SDM0(int arg1, int arg2, int arg3) { return "SDM0-3"; }

        // 2. miss 0, 3, 4 arguments, but have params
        public string IDM1(int arg1) { return "IDM1-1"; }
        public string IDM1(params int[] args) { return "IDM1-x"; }

        public static string SDM1(int arg1) { return "SDM1-1"; }
        public static string SDM1(params int[] args) { return "SDM1-x"; }

        public string IDM4(int arg1, int arg2) { return "IDM4-2"; }
        public string IDM4(params int[] args) { return "IDM4-x"; }

        public static string SDM4(int arg1, int arg2) { return "SDM4-2"; }
        public static string SDM4(params int[] args) { return "SDM4-x"; }

        // 3. this is optimizated, and have all cases without params
        public string IDM2() { return "IDM2-0"; }
        public string IDM2(int arg1) { return "IDM2-1"; }
        public string IDM2(int arg1, int arg2) { return "IDM2-2"; }
        public string IDM2(int arg1, int arg2, int arg3) { return "IDM2-3"; }
        public string IDM2(int arg1, int arg2, int arg3, int arg4) { return "IDM2-4"; }

        public static string SDM2() { return "SDM2-0"; }
        public static string SDM2(int arg1) { return "SDM2-1"; }
        public static string SDM2(int arg1, int arg2) { return "SDM2-2"; }
        public static string SDM2(int arg1, int arg2, int arg3) { return "SDM2-3"; }
        public static string SDM2(int arg1, int arg2, int arg3, int arg4) { return "SDM2-4"; }
        public static string SDM2(int arg1, int arg2, int arg3, int arg4, int arg5) { return "SDM2-5"; }

        // 4. this is not optimized
        public string IDM5() { return "IDM5-0"; }
        public string IDM5(int arg1, int arg2, int arg3, int arg4, int arg5) { return "IDM5-5"; }

        public static string SDM5() { return "SDM5-0"; }
        public static string SDM5(int arg1, int arg2, int arg3, int arg4, int arg5, int arg6) { return "SDM5-6"; }

        // 5. this is optimizated, and have all cases, with params
        public string IDM3() { return "IDM3-0"; }
        public string IDM3(int arg1) { return "IDM3-1"; }
        public string IDM3(int arg1, int arg2) { return "IDM3-2"; }
        public string IDM3(int arg1, int arg2, int arg3) { return "IDM3-3"; }
        public string IDM3(int arg1, int arg2, int arg3, int arg4) { return "IDM3-4"; }
        public string IDM3(params int[] args) { return "IDM3-x"; }

        public static string SDM3() { return "SDM3-0"; }
        public static string SDM3(int arg1) { return "SDM3-1"; }
        public static string SDM3(int arg1, int arg2) { return "SDM3-2"; }
        public static string SDM3(int arg1, int arg2, int arg3) { return "SDM3-3"; }
        public static string SDM3(int arg1, int arg2, int arg3, int arg4) { return "SDM3-4"; }
        public static string SDM3(int arg1, int arg2, int arg3, int arg4, int arg5) { return "SDM3-5"; }
        public static string SDM3(params int[] args) { return "SDM3-x"; }
    }

    public class MultiCall {
        public int M0(int arg) { return 1; }
        public int M0(long arg) { return 2; }

        public int M1(int arg) { return 1; }
        public int M1(long arg) { return 2; }
        public int M1(object arg) { return 3; }

        public int M2(int arg1, int arg2) { return 1; }
        public int M2(long arg1, int arg2) { return 2; }
        public int M2(int arg1, long arg2) { return 3; }
        public int M2(long arg1, long arg2) { return 4; }
        public int M2(object arg1, object arg2) { return 5; }

        public int M4(int arg1, int arg2, int arg3, int arg4) { return 1; }
        public int M4(object arg1, object arg2, object arg3, object arg4) { return 2; }

        public int M5(DispatchHelpers.B arg1, DispatchHelpers.B args) { return 1; }
        public int M5(DispatchHelpers.D arg1, DispatchHelpers.B args) { return 2; }
        public int M5(object arg1, object args) { return 3; }

        public int M6(DispatchHelpers.B arg1, DispatchHelpers.B args) { return 1; }
        public int M6(DispatchHelpers.B arg1, DispatchHelpers.D args) { return 2; }
        public int M6(object arg1, DispatchHelpers.D args) { return 3; }

        public int M7(DispatchHelpers.B arg1, DispatchHelpers.B args) { return 1; }
        public int M7(DispatchHelpers.B arg1, DispatchHelpers.D args) { return 2; }
        public int M7(DispatchHelpers.D arg1, DispatchHelpers.B args) { return 3; }
        public int M7(DispatchHelpers.D arg1, DispatchHelpers.D args) { return 4; }

        public int M8(int arg1, int arg2) { return 1; }
        public int M8(DispatchHelpers.B arg1, DispatchHelpers.B args) { return 2; }
        public int M8(object arg1, object arg2) { return 3; }
    }

    public class GenericTypeInference {
        public static PythonType M0<T>(T x) {
            return DynamicHelpers.GetPythonTypeFromType(typeof(T));
        }

        public static PythonType M0CC<T>(CodeContext context, T x) {
            return DynamicHelpers.GetPythonTypeFromType(typeof(T));
        }

        public static PythonType M1<T>(T x, T y) {
            return DynamicHelpers.GetPythonTypeFromType(typeof(T));
        }

        public static PythonType M2<T>(T x, T y, T z) {
            return DynamicHelpers.GetPythonTypeFromType(typeof(T));
        }

        public static PythonType M3<T>(params T[] x) {
            return DynamicHelpers.GetPythonTypeFromType(typeof(T));
        }

        public static PythonType M4<T>(T x, params T[] args) {
            return DynamicHelpers.GetPythonTypeFromType(typeof(T));
        }

        public static PythonType M5<T>(T x) where T : class {
            return DynamicHelpers.GetPythonTypeFromType(typeof(T));
        }

        public static PythonType M6<T>(T x) where T : struct {
            return DynamicHelpers.GetPythonTypeFromType(typeof(T));
        }

        public static PythonType M7<T>(T x) where T : IList {
            return DynamicHelpers.GetPythonTypeFromType(typeof(T));
        }

        public static PythonTuple M8<T0, T1>(T0 x, T1 y) where T0 : T1 {
            return PythonTuple.MakeTuple(DynamicHelpers.GetPythonTypeFromType(typeof(T0)), DynamicHelpers.GetPythonTypeFromType(typeof(T1)));
        }

        public static PythonTuple M9<T0, T1>(object x, T1 y) where T0 : T1 {
            return PythonTuple.MakeTuple(DynamicHelpers.GetPythonTypeFromType(typeof(T0)), DynamicHelpers.GetPythonTypeFromType(typeof(T1)));
        }

        public static PythonTuple M9b<T0, T1>(T0 x, object y) where T0 : T1 {
            return PythonTuple.MakeTuple(DynamicHelpers.GetPythonTypeFromType(typeof(T0)), DynamicHelpers.GetPythonTypeFromType(typeof(T1)));
        }

        public static PythonTuple M10<T0, T1, T2>(T0 x, T1 y, T2 z)
            where T0 : T1
            where T1 : T2 {
            return PythonTuple.MakeTuple(
                DynamicHelpers.GetPythonTypeFromType(typeof(T0)),
                DynamicHelpers.GetPythonTypeFromType(typeof(T1)),
                DynamicHelpers.GetPythonTypeFromType(typeof(T2))
            );
        }

        public static PythonTuple M10c<T0, T1, T2>(T0 x, T1 y, T2 z)
            where T0 : T1
            where T1 : class {
            return PythonTuple.MakeTuple(
                DynamicHelpers.GetPythonTypeFromType(typeof(T0)),
                DynamicHelpers.GetPythonTypeFromType(typeof(T1)),
                DynamicHelpers.GetPythonTypeFromType(typeof(T2))
            );
        }

        public static PythonTuple M10IList<T0, T1, T2>(T0 x, T1 y, T2 z)
            where T0 : T1
            where T1 : IList {
            return PythonTuple.MakeTuple(
                DynamicHelpers.GetPythonTypeFromType(typeof(T0)),
                DynamicHelpers.GetPythonTypeFromType(typeof(T1)),
                DynamicHelpers.GetPythonTypeFromType(typeof(T2))
            );
        }

        public static PythonTuple M10PythonTuple<T0, T1, T2>(T0 x, T1 y, T2 z)
            where T0 : T1
            where T1 : PythonTuple {
            return PythonTuple.MakeTuple(
                DynamicHelpers.GetPythonTypeFromType(typeof(T0)),
                DynamicHelpers.GetPythonTypeFromType(typeof(T1)),
                DynamicHelpers.GetPythonTypeFromType(typeof(T2))
            );
        }

        public static PythonType M11<T>(object x) {
            return DynamicHelpers.GetPythonTypeFromType(typeof(T));
        }

        public static PythonType M12<T0, T1>(T0 x, object y) {
            return DynamicHelpers.GetPythonTypeFromType(typeof(T0));
        }

        public static PythonTuple M13<T>(T x, Func<T> y) {
            return PythonTuple.MakeTuple(DynamicHelpers.GetPythonTypeFromType(typeof(T)), y());
        }

        public static PythonType M14<T>(T x, Action<T> y) {
            return DynamicHelpers.GetPythonTypeFromType(typeof(T));
        }

        public static PythonTuple M15<T>(T x, IList<T> y) {
            PythonTuple res = PythonTuple.MakeTuple(DynamicHelpers.GetPythonTypeFromType(typeof(T)));
            res += PythonTuple.Make(y);
            return res;
        }

        public static PythonTuple M16<T>(T x, Dictionary<T, IList<T>> y) {
            PythonTuple res = PythonTuple.MakeTuple(DynamicHelpers.GetPythonTypeFromType(typeof(T)));
            res += PythonTuple.Make(y);
            return res;
        }

        public static PythonType M17<T>(T x, IEnumerable<T> y) {
            return DynamicHelpers.GetPythonTypeFromType(typeof(T));
        }

        
        public static PythonType M18<T>(T x) where T : IEnumerable<T> {
            return DynamicHelpers.GetPythonTypeFromType(typeof(T));
        }

        public static PythonTuple M19<T0, T1>(T0 x, T1 y) where T0 : IList<T1> {
            return PythonTuple.MakeTuple(DynamicHelpers.GetPythonTypeFromType(typeof(T0)), DynamicHelpers.GetPythonTypeFromType(typeof(T1)));
        }

        public static PythonTuple M20<T0, T1>(T0 x, T1 y) {
            return PythonTuple.MakeTuple(DynamicHelpers.GetPythonTypeFromType(typeof(T0)), DynamicHelpers.GetPythonTypeFromType(typeof(T1)));
        }

        public static PythonType M21<T>(IEnumerable<T> x) {
            return DynamicHelpers.GetPythonTypeFromType(typeof(T));
        }

        // overloads like Enumerable.Where<T>(...)
        public static PythonTuple M22<T>(IEnumerable<T> x, Func<T, bool> y) {
            return PythonTuple.MakeTuple(DynamicHelpers.GetPythonTypeFromType(typeof(T)), ScriptingRuntimeHelpers.True);
        }

        public static PythonTuple M22<T>(IEnumerable<T> x, Func<T, int, bool> y) {
            M23(new List<int>());
            return PythonTuple.MakeTuple(DynamicHelpers.GetPythonTypeFromType(typeof(T)), ScriptingRuntimeHelpers.False);
        }

        public static PythonType M23<T>(List<T> x) {
            return DynamicHelpers.GetPythonTypeFromType(typeof(T));
        }

        public static PythonType M24<T>(List<List<T>> x) {
            return DynamicHelpers.GetPythonTypeFromType(typeof(T));
        }

        public static PythonType M25<T>(Dictionary<T, T> x) {
            return DynamicHelpers.GetPythonTypeFromType(typeof(T));
        }

        public static PythonType M26<T>(List<T> x) where T : class {
            return DynamicHelpers.GetPythonTypeFromType(typeof(T));
        }

        public static PythonType M27<T>(List<T> x) where T : struct {
            return DynamicHelpers.GetPythonTypeFromType(typeof(T));
        }

        public static PythonType M28<T>(List<T> x) where T : new() {
            return DynamicHelpers.GetPythonTypeFromType(typeof(T));
        }

        public static PythonType M29<T>(Dictionary<Dictionary<T, T>, Dictionary<T, T>> x) {
            return DynamicHelpers.GetPythonTypeFromType(typeof(T));
        }

        public static PythonType M30<T>(Func<T, bool> x) where T : struct {
            return DynamicHelpers.GetPythonTypeFromType(typeof(T));
        }

        public static PythonType M31<T>(Func<T, bool> x) where T : IList {
            return DynamicHelpers.GetPythonTypeFromType(typeof(T));
        }

        public static PythonType M32<T>(List<T> x) where T : new() {
            return DynamicHelpers.GetPythonTypeFromType(typeof(T));
        }

        public static PythonType M33<T>(List<T> x) where T : class {
            return DynamicHelpers.GetPythonTypeFromType(typeof(T));
        }

        public static PythonType M34<T>(IList<T> x, IList<T> y) {
            return DynamicHelpers.GetPythonTypeFromType(typeof(T));
        }

        public static PythonType M35<T>(IList<T> x) {
            return DynamicHelpers.GetPythonTypeFromType(typeof(IList<T>));
        }

        public static PythonType M35<T>(T[] x) {
            return DynamicHelpers.GetPythonTypeFromType(typeof(T[]));
        }
    }

    public class SelfEnumerable : IEnumerable<SelfEnumerable> {
        #region IEnumerable<Test> Members

        IEnumerator<SelfEnumerable> IEnumerable<SelfEnumerable>.GetEnumerator() {
            throw new NotImplementedException();
        }

        #endregion

        #region IEnumerable Members

        IEnumerator IEnumerable.GetEnumerator() {
            throw new NotImplementedException();
        }

        #endregion
    }


    public class GenericTypeInferenceInstance {
        public PythonType MInst<T>(T x) {
            return DynamicHelpers.GetPythonTypeFromType(typeof(T));
        }

        public PythonType M0<T>(T x) {
            return DynamicHelpers.GetPythonTypeFromType(typeof(T));
        }

        public PythonType M0CC<T>(CodeContext context, T x) {
            return DynamicHelpers.GetPythonTypeFromType(typeof(T));
        }

        public PythonType M1<T>(T x, T y) {
            return DynamicHelpers.GetPythonTypeFromType(typeof(T));
        }

        public PythonType M2<T>(T x, T y, T z) {
            return DynamicHelpers.GetPythonTypeFromType(typeof(T));
        }

        public PythonType M3<T>(params T[] x) {
            return DynamicHelpers.GetPythonTypeFromType(typeof(T));
        }

        public PythonType M4<T>(T x, params T[] args) {
            return DynamicHelpers.GetPythonTypeFromType(typeof(T));
        }

        public PythonType M5<T>(T x) where T : class {
            return DynamicHelpers.GetPythonTypeFromType(typeof(T));
        }

        public PythonType M6<T>(T x) where T : struct {
            return DynamicHelpers.GetPythonTypeFromType(typeof(T));
        }

        public PythonType M7<T>(T x) where T : IList {
            return DynamicHelpers.GetPythonTypeFromType(typeof(T));
        }

        public PythonTuple M8<T0, T1>(T0 x, T1 y) where T0 : T1 {
            return PythonTuple.MakeTuple(DynamicHelpers.GetPythonTypeFromType(typeof(T0)), DynamicHelpers.GetPythonTypeFromType(typeof(T1)));
        }

        public PythonTuple M9<T0, T1>(object x, T1 y) where T0 : T1 {
            return PythonTuple.MakeTuple(DynamicHelpers.GetPythonTypeFromType(typeof(T0)), DynamicHelpers.GetPythonTypeFromType(typeof(T1)));
        }

        public PythonTuple M9b<T0, T1>(T0 x, object y) where T0 : T1 {
            return PythonTuple.MakeTuple(DynamicHelpers.GetPythonTypeFromType(typeof(T0)), DynamicHelpers.GetPythonTypeFromType(typeof(T1)));
        }

        public PythonTuple M10<T0, T1, T2>(T0 x, T1 y, T2 z)
            where T0 : T1
            where T1 : T2 {
            return PythonTuple.MakeTuple(
                DynamicHelpers.GetPythonTypeFromType(typeof(T0)),
                DynamicHelpers.GetPythonTypeFromType(typeof(T1)),
                DynamicHelpers.GetPythonTypeFromType(typeof(T2))
            );
        }

        public PythonTuple M10c<T0, T1, T2>(T0 x, T1 y, T2 z)
            where T0 : T1
            where T1 : class {
            return PythonTuple.MakeTuple(
                DynamicHelpers.GetPythonTypeFromType(typeof(T0)),
                DynamicHelpers.GetPythonTypeFromType(typeof(T1)),
                DynamicHelpers.GetPythonTypeFromType(typeof(T2))
            );
        }

        public PythonTuple M10IList<T0, T1, T2>(T0 x, T1 y, T2 z)
            where T0 : T1
            where T1 : IList {
            return PythonTuple.MakeTuple(
                DynamicHelpers.GetPythonTypeFromType(typeof(T0)),
                DynamicHelpers.GetPythonTypeFromType(typeof(T1)),
                DynamicHelpers.GetPythonTypeFromType(typeof(T2))
            );
        }

        public PythonTuple M10PythonTuple<T0, T1, T2>(T0 x, T1 y, T2 z)
            where T0 : T1
            where T1 : PythonTuple {
            return PythonTuple.MakeTuple(
                DynamicHelpers.GetPythonTypeFromType(typeof(T0)),
                DynamicHelpers.GetPythonTypeFromType(typeof(T1)),
                DynamicHelpers.GetPythonTypeFromType(typeof(T2))
            );
        }

        public PythonType M11<T>(object x) {
            return DynamicHelpers.GetPythonTypeFromType(typeof(T));
        }

        public PythonType M12<T0, T1>(T0 x, object y) {
            return DynamicHelpers.GetPythonTypeFromType(typeof(T0));
        }

        public PythonTuple M13<T>(T x, Func<T> y) {
            return PythonTuple.MakeTuple(DynamicHelpers.GetPythonTypeFromType(typeof(T)), y());
        }

        public PythonType M14<T>(T x, Action<T> y) {
            return DynamicHelpers.GetPythonTypeFromType(typeof(T));
        }

        public PythonTuple M15<T>(T x, IList<T> y) {
            PythonTuple res = PythonTuple.MakeTuple(DynamicHelpers.GetPythonTypeFromType(typeof(T)));
            res += PythonTuple.Make(y);
            return res;
        }

        public PythonTuple M16<T>(T x, Dictionary<T, IList<T>> y) {
            PythonTuple res = PythonTuple.MakeTuple(DynamicHelpers.GetPythonTypeFromType(typeof(T)));
            res += PythonTuple.Make(y);
            return res;
        }

        public PythonType M17<T>(T x, IEnumerable<T> y) {
            return DynamicHelpers.GetPythonTypeFromType(typeof(T));
        }


        public PythonType M18<T>(T x) where T : IEnumerable<T> {
            return DynamicHelpers.GetPythonTypeFromType(typeof(T));
        }

        public PythonTuple M19<T0, T1>(T0 x, T1 y) where T0 : IList<T1> {
            return PythonTuple.MakeTuple(DynamicHelpers.GetPythonTypeFromType(typeof(T0)), DynamicHelpers.GetPythonTypeFromType(typeof(T1)));
        }

        public PythonTuple M20<T0, T1>(T0 x, T1 y) {
            return PythonTuple.MakeTuple(DynamicHelpers.GetPythonTypeFromType(typeof(T0)), DynamicHelpers.GetPythonTypeFromType(typeof(T1)));
        }

        public PythonType M21<T>(IEnumerable<T> x) {
            return DynamicHelpers.GetPythonTypeFromType(typeof(T));
        }

        // overloads like Enumerable.Where<T>(...)
        public PythonTuple M22<T>(IEnumerable<T> x, Func<T, bool> y) {
            return PythonTuple.MakeTuple(DynamicHelpers.GetPythonTypeFromType(typeof(T)), ScriptingRuntimeHelpers.True);
        }

        public PythonTuple M22<T>(IEnumerable<T> x, Func<T, int, bool> y) {
            M23(new List<int>());
            return PythonTuple.MakeTuple(DynamicHelpers.GetPythonTypeFromType(typeof(T)), ScriptingRuntimeHelpers.False);
        }

        public PythonType M23<T>(List<T> x) {
            return DynamicHelpers.GetPythonTypeFromType(typeof(T));
        }

        public PythonType M24<T>(List<List<T>> x) {
            return DynamicHelpers.GetPythonTypeFromType(typeof(T));
        }

        public PythonType M25<T>(Dictionary<T, T> x) {
            return DynamicHelpers.GetPythonTypeFromType(typeof(T));
        }

        public PythonType M26<T>(List<T> x) where T : class {
            return DynamicHelpers.GetPythonTypeFromType(typeof(T));
        }

        public PythonType M27<T>(List<T> x) where T : struct {
            return DynamicHelpers.GetPythonTypeFromType(typeof(T));
        }

        public PythonType M28<T>(List<T> x) where T : new() {
            return DynamicHelpers.GetPythonTypeFromType(typeof(T));
        }

        public PythonType M29<T>(Dictionary<Dictionary<T, T>, Dictionary<T, T>> x) {
            return DynamicHelpers.GetPythonTypeFromType(typeof(T));
        }

        public PythonType M30<T>(Func<T, bool> x) where T : struct {
            return DynamicHelpers.GetPythonTypeFromType(typeof(T));
        }

        public PythonType M31<T>(Func<T, bool> x) where T : IList {
            return DynamicHelpers.GetPythonTypeFromType(typeof(T));
        }

        public PythonType M32<T>(List<T> x) where T : new() {
            return DynamicHelpers.GetPythonTypeFromType(typeof(T));
        }

        public PythonType M33<T>(List<T> x) where T : class {
            return DynamicHelpers.GetPythonTypeFromType(typeof(T));
        }

        public PythonType M34<T>(IList<T> x, IList<T> y) {
            return DynamicHelpers.GetPythonTypeFromType(typeof(T));
        }

        public static PythonType M35<T>(IList<T> x) {
            return DynamicHelpers.GetPythonTypeFromType(typeof(IList<T>));
        }

        public static PythonType M35<T>(T[] x) {
            return DynamicHelpers.GetPythonTypeFromType(typeof(T[]));
        }
    }

    public class WithCompare {
        public static bool Compare(object x, object y) {
            return true;
        }
    }
}