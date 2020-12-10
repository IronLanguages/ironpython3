// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;

namespace IronPythonTest.BinderTest {
    public interface I { }
    public class C1 : I { }
    public class C2 : C1 { }

    public class C3 { } // no relationship with others

    public struct S1 : I { }

    public abstract class A { }
    public class C6 : A { }

    public enum E1 { A, B, C }
    public enum E2 : ushort { A, B, C }

    public class Flag {
        public static int Value;
        public static bool BValue;
    }

    /// <summary>
    /// all methods here have no overloads. 
    /// 
    /// !!! Adding methods to this class could cause "many" updates in test_methodbinder1.py !!!
    /// you can always add other non-overload related methods to COtherConcern/GOtherConcern`1
    /// 
    /// </summary>
    public class CNoOverloads {
        // no args
        public void M100() { Flag.Value = 100; }

        //
        // primitive types: 
        //
        // 1. python native
        public void M201(Int32 arg) { Flag.Value = 201; }  // int
        public void M202(Double arg) { Flag.Value = 202; } // float
        public void M203(BigInteger arg) { Flag.Value = 203; } // long
        public void M204(Boolean arg) { Flag.BValue = arg; Flag.Value = 204; } // bool
        public void M205(String arg) { Flag.Value = 205; } // str

        // 2. not python native 
        // 2.1 -- signed
        public void M301(SByte arg) { Flag.Value = 301; }
        public void M302(Int16 arg) { Flag.Value = 302; }
        public void M303(Int64 arg) { Flag.Value = 303; }
        public void M304(Single arg) { Flag.Value = 304; }
        // 2.2 -- unsigned 
        public void M310(Byte arg) { Flag.Value = 310; }
        public void M311(UInt16 arg) { Flag.Value = 311; }
        public void M312(UInt32 arg) { Flag.Value = 312; }
        public void M313(UInt64 arg) { Flag.Value = 313; }
        // 2.3 -- special
        public void M320(Char arg) { Flag.Value = 320; }
        public void M321(Decimal arg) { Flag.Value = 321; }

        //
        // Reference type or value type
        //
        public void M400(object arg) { Flag.Value = 400; } // object

        public void M401(I arg) { Flag.Value = 401; }
        public void M402(C1 arg) { Flag.Value = 402; }
        public void M403(C2 arg) { Flag.Value = 403; }
        public void M404(S1 arg) { Flag.Value = 404; }

        public void M410(A arg) { Flag.Value = 410; }
        public void M411(C6 arg) { Flag.Value = 411; }

        public void M450(E1 arg) { Flag.Value = 450; }
        public void M451(E2 arg) { Flag.Value = 451; }

        // 
        // array
        //
        public void M500(Int32[] arg) { Flag.Value = 500; }
        public void M510(I[] arg) { Flag.Value = 510; }

        //
        // params array 
        //
        public void M600(params Int32[] arg) { Flag.Value = 600; }
        public void M610(params I[] arg) { Flag.Value = 610; }
        public void M611(params S1[] arg) { Flag.Value = 611; }
        public void M620(Int32 arg, params Int32[] arg2) { Flag.Value = 620; }
        public void M630(I arg, params I[] arg2) { Flag.Value = 630; }

        //
        // collections/generics
        //
        public void M650(IList<int> arg) { Flag.Value = 650; }
        public void M651(Array arg) { Flag.Value = 651; }
        public void M652(IEnumerable<int> arg) { Flag.Value = 652; }
        public void M653(IEnumerator<int> arg) { Flag.Value = 653; }

        // Nullable
        public void M680(Int32? arg) { Flag.Value = 680; }

        // ByRef, Out
        public void M700(ref Int32 arg) { arg = 1; Flag.Value = 700; }
        public void M701(out Int32 arg) { arg = 2; Flag.Value = 701; }

        // Default Value
        public void M710(Int32 arg=10) { Flag.Value = 710; }
        public void M715(Int32 arg, Int32 arg2=10) { Flag.Value = 715; }

    }

    /// <summary>
    /// Other concerns when there is no overloads
    /// </summary>
    public class COtherConcern {
        // static 
        public static void M100() { Flag.Value = 100; }
        public static void M101(COtherConcern arg) { Flag.Value = 101; }
        public void M102(COtherConcern arg) { Flag.Value = 102; }

        // generic method
        public void M200<T>(T arg) { Flag.Value = 200; }

        // out on non-byref
        public void M222([Out] int arg) { Flag.Value = 222; }

        // what does means when passing in None 
        public void M300(params C1[] args) {
            Flag.BValue = (args == null);
            Flag.Value = 300;
        }

        // ref, out ...
        public void M400(ref Int32 arg1, out Int32 arg2, Int32 arg3) { arg1 = arg2 = arg3; }
        public void M401(ref Int32 arg1, Int32 arg3, out Int32 arg2) { arg1 = arg2 = arg3; }
        public void M402(Int32 arg3, ref Int32 arg1, out Int32 arg2) { arg1 = arg2 = arg3; }

        // default value does get used
        public void M450(Int32 arg=80) { Flag.Value = arg; }

        // 8 args
        public void M500(Int32 arg1, Int32 arg2, Int32 arg3, Int32 arg4, Int32 arg5, Int32 arg6, Int32 arg7, Int32 arg8) {
            Flag.Value = 500;
        }

        // this is supported
        public void M550(IDictionary<int, int> arg) { Flag.Value = 550; }

        // not supported (or partially)
        public void M600(Hashtable arg) { Flag.Value = 600; }
        public void M601(ArrayList arg) { Flag.Value = 601; }
        public void M602(List<int> arg) { Flag.Value = 602; }

        // iterator support ??
        public void M620(IEnumerable arg) { IEnumerator ienum = arg.GetEnumerator(); int i = 0; while (ienum.MoveNext()) i++; Flag.Value = i; }
        public void M621(IEnumerator arg) { int i = 0; while (arg.MoveNext()) i++; Flag.Value = i; }
        public void M622(IList arg) { Flag.Value = arg.Count; }

        public void M630(IEnumerable<Char> arg) { IEnumerator ienum = arg.GetEnumerator(); int i = 0; while (ienum.MoveNext()) i++; Flag.Value = i; }
        public void M631(IEnumerator<Char> arg) { int i = 0; while (arg.MoveNext()) i++; Flag.Value = i; }
        public void M632(IList<Char> arg) { Flag.Value = arg.Count; }

        public void M640(IEnumerable<Int32> arg) { IEnumerator ienum = arg.GetEnumerator(); int i = 0; while (ienum.MoveNext()) i++; Flag.Value = i; }
        public void M641(IEnumerator<Int32> arg) { int i = 0; while (arg.MoveNext()) i++; Flag.Value = i; }
        public void M642(IList<Int32> arg) { Flag.Value = arg.Count; }

        // delegate 
        public void M700(Delegate arg) {
            IntIntDelegate d = (IntIntDelegate)arg;
            Flag.Value = d(10);
        }
        public void M701(IntIntDelegate d) { Flag.Value = d(10); }

        // byte array
        public void M702(ref Byte[] arg) { arg = new Byte[0]; Flag.Value = 702; }
        public int M703(ref Byte[] arg) { arg = new Byte[0]; Flag.Value = 703; return 42; }
        public int M704(Byte[] inp, ref Byte[] arg) { arg = inp; Flag.Value = 704; return 42; }

        // keywords 
        public void M800(Int32 arg1, object arg2, ref string arg3) { arg3 = arg3.ToUpper(); }

        // more ref/out 
        public void M850(ref S1 arg) { arg = new S1(); }
        public void M851(ref C1 arg) { arg = new C1(); }
        public void M852(out S1 arg) { arg = new S1(); }
        public void M853(out C1 arg) { arg = new C1(); }
        public void M854(ref Boolean arg) { Flag.Value = 854; }
        public void M855(out Boolean arg) { Flag.Value = 855; arg = true; }

        public void M860(ref Int32 arg1, Int32 arg2, out Int32 arg3) { arg3 = arg1 + arg2; arg1 = 100; }

    }

    public struct SOtherConcern {
        private short _p100;
        public short P100 {
            get {
                return _p100;
            }
            set {
                _p100 = value;
            }
        }
    }

    public class GOtherConcern<T> {
        public void M100(T arg) { Flag.Value = 100; }
    }

    public class COtherOverloadConcern {
        // one is private
#pragma warning disable 169 // unusued method...
        private void M100(Int32 arg) { Flag.Value = 100; }
#pragma warning restore 169
        public void M100(object arg) { Flag.Value = 200; }

        // static / instance
        public void M110(COtherOverloadConcern arg1, Int32 arg2) { Flag.Value = 110; }
        public static void M110(Int32 arg) { Flag.Value = 210; }

        // static / instance 2
        public void M111(Int32 arg2) { Flag.Value = 111; }
        public static COtherOverloadConcern M111(COtherOverloadConcern arg1, Int32 arg) { Flag.Value = 211; return null; }

        // statics 
        public static void M120(COtherOverloadConcern arg1, Int32 arg2) { Flag.Value = 120; }
        public static void M120(Int32 arg) { Flag.Value = 220; }

        // generics
        public void M130(Int32 arg) { Flag.Value = 130; }
        public void M130<T>(T arg) { Flag.Value = 230; }

        // narrowing levels and __int__ conversion:
        // If 2 instances of a Python subclass of C3 that implements __int__ are passed as arguments this 
        // the first overload should be preferred.
        public void M140(C3 a, int b) { Flag.Value = 140; }
        public void M140(int a, int b) { Flag.Value = 240; } 
    }

    public interface I1 { void M(); }
    public interface I2 { void M(); }
    public interface I3<T> { void M(); }
    public interface I4 { void M(int arg); }

    public class CInheritMany1 : I1 {
        void I1.M() { Flag.Value = 100; }
    }

    public class CInheritMany2 : I1 {
        void I1.M() { Flag.Value = 200; }
        public void M() { Flag.Value = 201; }
    }

    public class CInheritMany3 : I2, I1 {
        void I1.M() { Flag.Value = 300; }
        void I2.M() { Flag.Value = 301; }
    }

    public class CInheritMany4 : I3<object> {
        void I3<object>.M() { Flag.Value = 400; }
        public void M() { Flag.Value = 401; }
    }
    public class CInheritMany5 : I1, I2, I3<object> {
        void I1.M() { Flag.Value = 500; }
        void I2.M() { Flag.Value = 501; }
        void I3<object>.M() { Flag.Value = 502; }
        public void M() { Flag.Value = 503; }
    }
    public class CInheritMany6<T> : I3<T> {
        void I3<T>.M() { Flag.Value = 600; }
        public void M() { Flag.Value = 601; }
    }
    public class CInheritMany7<T> : I3<T> {
        void I3<T>.M() { Flag.Value = 700; }
    }

    public class CInheritMany8 : I1, I4 {
        void I1.M() { Flag.Value = 800; }
        void I4.M(int arg) { Flag.Value = 801; }
    }

    public class COverloads_ClrReference {
        public void M100(Object arg) { Flag.Value = 100; }
        public void M100(ref Object arg) { arg = typeof(string); Flag.Value = 200; }

        public void M101(Boolean arg) { Flag.Value = 101; }
        public void M101(ref Boolean arg) { arg = true; Flag.Value = 201; }

        public void M102(ref Int32 arg) { arg = 100; Flag.Value = 102; }
        public void M102(ref Boolean arg) { arg = true; Flag.Value = 202; }

        public void M103(ref Int32 arg) { arg = 100; Flag.Value = 103; }
        public void M103(ref Byte arg) { arg = 10; Flag.Value = 203; }

        public void M104(ref Int32 arg) { arg = 100; Flag.Value = 104; }
        public void M104(ref Object arg) { arg = typeof(int); Flag.Value = 204; }

        public void M105(ref Boolean arg) { arg = true; Flag.Value = 105; }
        public void M105(ref Object arg) { arg = typeof(Type); Flag.Value = 205; }

        public void M106(ref C1 arg) { arg = new C1(); Flag.Value = 106; }
        public void M106(ref C2 arg) { arg = new C2(); Flag.Value = 206; }

        public void M107(ref C1 arg) { arg = new C1(); Flag.Value = 107; }
        public void M107(ref object arg) { arg = typeof(C1); Flag.Value = 207; }
    }

    public class COverloads_NoArgNecessary {
        #region generated codes
        public void M100() { Flag.Value = 100; }
        public void M100(out Int32 arg) { arg = 1; Flag.Value = 200; }

        public void M101() { Flag.Value = 101; }
        public void M101(Int32 arg=1) { Flag.Value = 201; }

        public void M102() { Flag.Value = 102; }
        public void M102(params Int32[] arg) { Flag.Value = 202; }

        public void M103(out Int32 arg) { arg = 1; Flag.Value = 103; }
        public void M103(Int32 arg=1) { Flag.Value = 203; }

        public void M104(out Int32 arg) { arg = 1; Flag.Value = 104; }
        public void M104(params Int32[] arg) { Flag.Value = 204; }

        public void M105(Int32 arg=1) { Flag.Value = 105; }
        public void M105(params Int32[] arg) { Flag.Value = 205; }

        public void M106(Int32 arg) { Flag.Value = 106; }
        public void M106(params Int32[] arg) { Flag.Value = 206; }
        #endregion
    }
    public class COverloads_OneArg_NormalArg {
        #region generated codes
        public void M100(Int32 arg) { Flag.Value = 100; }
        public void M100(ref Int32 arg) { Flag.Value = 200; }

        public void M101(Int32 arg) { Flag.Value = 101; }
        public void M101(Int32? arg) { Flag.Value = 201; }

        public void M102(Int32 arg) { Flag.Value = 102; }
        public void M102(Int32 arg1, Int32 arg2) { Flag.Value = 202; }

        public void M103(Int32 arg) { Flag.Value = 103; }
        public void M103(Int32 arg1, out Int32 arg) { arg = 1; Flag.Value = 203; }

        public void M104(Int32 arg) { Flag.Value = 104; }
        public void M104(ref Int32 arg1, out Int32 arg) { arg = 1; Flag.Value = 204; }

        public void M105(Int32 arg) { Flag.Value = 105; }
        public void M105(out Int32 arg, Int32 arg2) { arg = 1; Flag.Value = 205; }

        public void M106(Int32 arg) { Flag.Value = 106; }
        public void M106(out Int32 arg, ref Int32 arg2) { arg = 1; Flag.Value = 206; }

        public void M107(Int32 arg) { Flag.Value = 107; }
        public void M107(Int32 arg, Int32 arg2=1) { Flag.Value = 207; }

        public void M108(Int32 arg) { Flag.Value = 108; }
        public void M108(Int32 arg, params Int32[] arg2) { Flag.Value = 208; }

        public void M109(Int32 arg) { Flag.Value = 109; }
        public void M109(Int32 arg, Int32[] arg2) { Flag.Value = 209; }

        #endregion
    }
    public class COverloads_OneArg_RefArg {
        #region generated codes
        public void M100(ref Int32 arg) { Flag.Value = 100; }
        public void M100(Int32? arg) { Flag.Value = 200; }

        public void M101(ref Int32 arg) { Flag.Value = 101; }
        public void M101(Int32 arg1, Int32 arg2) { Flag.Value = 201; }

        public void M102(ref Int32 arg) { Flag.Value = 102; }
        public void M102(Int32 arg1, out Int32 arg) { arg = 1; Flag.Value = 202; }

        public void M103(ref Int32 arg) { Flag.Value = 103; }
        public void M103(ref Int32 arg1, out Int32 arg) { arg = 1; Flag.Value = 203; }

        public void M104(ref Int32 arg) { Flag.Value = 104; }
        public void M104(out Int32 arg, Int32 arg2) { arg = 1; Flag.Value = 204; }

        public void M105(ref Int32 arg) { Flag.Value = 105; }
        public void M105(out Int32 arg, ref Int32 arg2) { arg = 1; Flag.Value = 205; }

        public void M106(ref Int32 arg) { Flag.Value = 106; }
        public void M106(Int32 arg, Int32 arg2=1) { Flag.Value = 206; }

        public void M107(ref Int32 arg) { Flag.Value = 107; }
        public void M107(Int32 arg, params Int32[] arg2) { Flag.Value = 207; }

        public void M108(ref Int32 arg) { Flag.Value = 108; }
        public void M108(Int32 arg, Int32[] arg2) { Flag.Value = 208; }

        #endregion
    }
    public class COverloads_OneArg_NullableArg {
        #region generated codes
        public void M100(Int32? arg) { Flag.Value = 100; }
        public void M100(Int32 arg1, Int32 arg2) { Flag.Value = 200; }

        public void M101(Int32? arg) { Flag.Value = 101; }
        public void M101(Int32 arg1, out Int32 arg) { arg = 1; Flag.Value = 201; }

        public void M102(Int32? arg) { Flag.Value = 102; }
        public void M102(ref Int32 arg1, out Int32 arg) { arg = 1; Flag.Value = 202; }

        public void M103(Int32? arg) { Flag.Value = 103; }
        public void M103(out Int32 arg, Int32 arg2) { arg = 1; Flag.Value = 203; }

        public void M104(Int32? arg) { Flag.Value = 104; }
        public void M104(out Int32 arg, ref Int32 arg2) { arg = 1; Flag.Value = 204; }

        public void M105(Int32? arg) { Flag.Value = 105; }
        public void M105(Int32 arg, Int32 arg2=1) { Flag.Value = 205; }

        public void M106(Int32? arg) { Flag.Value = 106; }
        public void M106(Int32 arg, params Int32[] arg2) { Flag.Value = 206; }

        public void M107(Int32? arg) { Flag.Value = 107; }
        public void M107(Int32 arg, Int32[] arg2) { Flag.Value = 207; }

        #endregion
    }
    public class COverloads_OneArg_TwoArgs {
        #region generated codes
        public void M100(Int32 arg1, Int32 arg2) { Flag.Value = 100; }
        public void M100(Int32 arg1, out Int32 arg) { arg = 1; Flag.Value = 200; }

        public void M101(Int32 arg1, Int32 arg2) { Flag.Value = 101; }
        public void M101(ref Int32 arg1, out Int32 arg) { arg = 1; Flag.Value = 201; }

        public void M102(Int32 arg1, Int32 arg2) { Flag.Value = 102; }
        public void M102(out Int32 arg, Int32 arg2) { arg = 1; Flag.Value = 202; }

        public void M103(Int32 arg1, Int32 arg2) { Flag.Value = 103; }
        public void M103(out Int32 arg, ref Int32 arg2) { arg = 1; Flag.Value = 203; }

        public void M104(Int32 arg1, Int32 arg2) { Flag.Value = 104; }
        public void M104(Int32 arg, params Int32[] arg2) { Flag.Value = 204; }

        public void M105(Int32 arg1, Int32 arg2) { Flag.Value = 105; }
        public void M105(Int32 arg, Int32[] arg2) { Flag.Value = 205; }

        #endregion
    }
    public class COverloads_OneArg_NormalOut {
        #region generated codes
        public void M100(Int32 arg1, out Int32 arg) { arg = 1; Flag.Value = 100; }
        public void M100(ref Int32 arg1, out Int32 arg) { arg = 1; Flag.Value = 200; }

        public void M101(Int32 arg1, out Int32 arg) { arg = 1; Flag.Value = 101; }
        public void M101(out Int32 arg, Int32 arg2) { arg = 1; Flag.Value = 201; }

        public void M102(Int32 arg1, out Int32 arg) { arg = 1; Flag.Value = 102; }
        public void M102(out Int32 arg, ref Int32 arg2) { arg = 1; Flag.Value = 202; }

        public void M103(Int32 arg1, out Int32 arg) { arg = 1; Flag.Value = 103; }
        public void M103(Int32 arg, Int32 arg2=1) { Flag.Value = 203; }

        public void M104(Int32 arg1, out Int32 arg) { arg = 1; Flag.Value = 104; }
        public void M104(Int32 arg, params Int32[] arg2) { Flag.Value = 204; }

        public void M105(Int32 arg1, out Int32 arg) { arg = 1; Flag.Value = 105; }
        public void M105(Int32 arg, Int32[] arg2) { Flag.Value = 205; }

        #endregion
    }
    public class COverloads_OneArg_RefOut {
        #region generated codes
        public void M100(ref Int32 arg1, out Int32 arg) { arg = 1; Flag.Value = 100; }
        public void M100(out Int32 arg, Int32 arg2) { arg = 1; Flag.Value = 200; }

        public void M101(ref Int32 arg1, out Int32 arg) { arg = 1; Flag.Value = 101; }
        public void M101(Int32 arg, Int32 arg2=1) { Flag.Value = 201; }

        public void M102(ref Int32 arg1, out Int32 arg) { arg = 1; Flag.Value = 102; }
        public void M102(Int32 arg, params Int32[] arg2) { Flag.Value = 202; }

        public void M103(ref Int32 arg1, out Int32 arg) { arg = 1; Flag.Value = 103; }
        public void M103(Int32 arg, Int32[] arg2) { Flag.Value = 203; }

        #endregion
    }
    public class COverloads_OneArg_OutNormal {
        #region generated codes
        public void M100(out Int32 arg, Int32 arg2) { arg = 1; Flag.Value = 100; }
        public void M100(out Int32 arg, ref Int32 arg2) { arg = 1; Flag.Value = 200; }

        public void M101(out Int32 arg, Int32 arg2) { arg = 1; Flag.Value = 101; }
        public void M101(Int32 arg, Int32 arg2=1) { Flag.Value = 201; }

        public void M102(out Int32 arg, Int32 arg2) { arg = 1; Flag.Value = 102; }
        public void M102(Int32 arg, params Int32[] arg2) { Flag.Value = 202; }

        public void M103(out Int32 arg, Int32 arg2) { arg = 1; Flag.Value = 103; }
        public void M103(Int32 arg, Int32[] arg2) { Flag.Value = 203; }

        #endregion
    }
    public class COverloads_OneArg_OutRef {
        #region generated codes
        public void M100(out Int32 arg, ref Int32 arg2) { arg = 1; Flag.Value = 100; }
        public void M100(Int32 arg, Int32 arg2=1) { Flag.Value = 200; }

        public void M101(out Int32 arg, ref Int32 arg2) { arg = 1; Flag.Value = 101; }
        public void M101(Int32 arg, params Int32[] arg2) { Flag.Value = 201; }

        public void M102(out Int32 arg, ref Int32 arg2) { arg = 1; Flag.Value = 102; }
        public void M102(Int32 arg, Int32[] arg2) { Flag.Value = 202; }

        #endregion
    }
    public class COverloads_OneArg_NormalDefault {
        #region generated codes
        public void M100(Int32 arg, Int32 arg2=1) { Flag.Value = 100; }
        public void M100(Int32 arg, params Int32[] arg2) { Flag.Value = 200; }

        public void M101(Int32 arg, Int32 arg2=1) { Flag.Value = 101; }
        public void M101(Int32 arg, Int32[] arg2) { Flag.Value = 201; }

        #endregion
    }
    public class COverloads_String {
        #region generated codes
        public void M100(String arg) { Flag.Value = 100; }
        public void M100(Char arg) { Flag.Value = 200; }

        public void M101(Object arg) { Flag.Value = 101; }
        public void M101(Char arg) { Flag.Value = 201; }

        public void M102(Object arg) { Flag.Value = 102; }
        public void M102(String arg) { Flag.Value = 202; }

        #endregion
    }
    public class COverloads_Enum {
        #region generated codes
        public void M100(E1 arg) { Flag.Value = 100; }
        public void M100(Int32 arg) { Flag.Value = 200; }

        public void M101(E2 arg) { Flag.Value = 101; }
        public void M101(UInt16 arg) { Flag.Value = 201; }

        #endregion
    }
    public class COverloads_UserDefined {
        #region generated codes
        public void M100(I arg) { Flag.Value = 100; }
        public void M100(C1 arg) { Flag.Value = 200; }

        public void M101(I arg) { Flag.Value = 101; }
        public void M101(C2 arg) { Flag.Value = 201; }

        public void M102(I arg) { Flag.Value = 102; }
        public void M102(Int32 arg) { Flag.Value = 202; }

        public void M103(I arg) { Flag.Value = 103; }
        public void M103(Object arg) { Flag.Value = 203; }

        public void M104(C1 arg) { Flag.Value = 104; }
        public void M104(C2 arg) { Flag.Value = 204; }

        public void M105(A arg) { Flag.Value = 105; }
        public void M105(C6 arg) { Flag.Value = 205; }

        #endregion
    }
    #region generated codes
    public class COverloads_Base_Number {
        public void M100(Int32 arg) { Flag.Value = 100; }
        public void M101(Int32 arg) { Flag.Value = 101; }
        public void M102(Int32 arg) { Flag.Value = 102; }
        public void M103(Int32 arg) { Flag.Value = 103; }
        public void M104(Byte arg) { Flag.Value = 104; }
        public void M105(Double arg) { Flag.Value = 105; }
        public void M106(Object arg) { Flag.Value = 106; }
    }
    public class COverloads_Derived_Number : COverloads_Base_Number {
        public void M100(Byte arg) { Flag.Value = 200; }
        public void M101(Double arg) { Flag.Value = 201; }
#pragma warning disable CA1061 // Do not hide base class methods
        public void M102(Object arg) { Flag.Value = 202; }
#pragma warning restore CA1061 // Do not hide base class methods
        public void M103(Int32? arg) { Flag.Value = 203; }
        public void M104(Int32 arg) { Flag.Value = 204; }
        public void M105(Int32 arg) { Flag.Value = 205; }
        public void M106(Int32 arg) { Flag.Value = 206; }
    }
    #endregion
    public class COverloads_Collections {
        #region generated codes
        public void M100(IList<int> arg) { Flag.Value = 100; }
        public void M100(IList<object> arg) { Flag.Value = 200; }

        public void M101(Array arg) { Flag.Value = 101; }
        public void M101(Int32[] arg) { Flag.Value = 201; }

        public void M102(IList<int> arg) { Flag.Value = 102; }
        public void M102(Int32[] arg) { Flag.Value = 202; }

        public void M103(IEnumerable<int> arg) { Flag.Value = 103; }
        public void M103(IList<int> arg) { Flag.Value = 203; }

        public void M104(IEnumerable<int> arg) { Flag.Value = 104; }
        public void M104(Int32[] arg) { Flag.Value = 204; }

        #endregion
    }
    public class COverloads_Boolean {
        #region generated codes
        public void M100(Boolean arg) { Flag.Value = 100; }
        public void M100(Char arg) { Flag.Value = 200; }

        public void M101(Boolean arg) { Flag.Value = 101; }
        public void M101(Byte arg) { Flag.Value = 201; }

        public void M102(Boolean arg) { Flag.Value = 102; }
        public void M102(SByte arg) { Flag.Value = 202; }

        public void M103(Boolean arg) { Flag.Value = 103; }
        public void M103(UInt16 arg) { Flag.Value = 203; }

        public void M104(Boolean arg) { Flag.Value = 104; }
        public void M104(Int16 arg) { Flag.Value = 204; }

        public void M105(Boolean arg) { Flag.Value = 105; }
        public void M105(UInt32 arg) { Flag.Value = 205; }

        public void M106(Boolean arg) { Flag.Value = 106; }
        public void M106(Int32 arg) { Flag.Value = 206; }

        public void M107(Boolean arg) { Flag.Value = 107; }
        public void M107(UInt64 arg) { Flag.Value = 207; }

        public void M108(Boolean arg) { Flag.Value = 108; }
        public void M108(Int64 arg) { Flag.Value = 208; }

        public void M109(Boolean arg) { Flag.Value = 109; }
        public void M109(Decimal arg) { Flag.Value = 209; }

        public void M110(Boolean arg) { Flag.Value = 110; }
        public void M110(Single arg) { Flag.Value = 210; }

        public void M111(Boolean arg) { Flag.Value = 111; }
        public void M111(Double arg) { Flag.Value = 211; }

        public void M112(Boolean arg) { Flag.Value = 112; }
        public void M112(Object arg) { Flag.Value = 212; }

        #endregion
    }
    public class COverloads_Byte {
        #region generated codes
        public void M100(Byte arg) { Flag.Value = 100; }
        public void M100(Boolean arg) { Flag.Value = 200; }

        public void M101(Byte arg) { Flag.Value = 101; }
        public void M101(Char arg) { Flag.Value = 201; }

        public void M102(Byte arg) { Flag.Value = 102; }
        public void M102(SByte arg) { Flag.Value = 202; }

        public void M103(Byte arg) { Flag.Value = 103; }
        public void M103(UInt16 arg) { Flag.Value = 203; }

        public void M104(Byte arg) { Flag.Value = 104; }
        public void M104(Int16 arg) { Flag.Value = 204; }

        public void M105(Byte arg) { Flag.Value = 105; }
        public void M105(UInt32 arg) { Flag.Value = 205; }

        public void M106(Byte arg) { Flag.Value = 106; }
        public void M106(Int32 arg) { Flag.Value = 206; }

        public void M107(Byte arg) { Flag.Value = 107; }
        public void M107(UInt64 arg) { Flag.Value = 207; }

        public void M108(Byte arg) { Flag.Value = 108; }
        public void M108(Int64 arg) { Flag.Value = 208; }

        public void M109(Byte arg) { Flag.Value = 109; }
        public void M109(Decimal arg) { Flag.Value = 209; }

        public void M110(Byte arg) { Flag.Value = 110; }
        public void M110(Single arg) { Flag.Value = 210; }

        public void M111(Byte arg) { Flag.Value = 111; }
        public void M111(Double arg) { Flag.Value = 211; }

        public void M112(Byte arg) { Flag.Value = 112; }
        public void M112(Object arg) { Flag.Value = 212; }

        #endregion
    }
    public class COverloads_Int16 {
        #region generated codes
        public void M100(Int16 arg) { Flag.Value = 100; }
        public void M100(Boolean arg) { Flag.Value = 200; }

        public void M101(Int16 arg) { Flag.Value = 101; }
        public void M101(Char arg) { Flag.Value = 201; }

        public void M102(Int16 arg) { Flag.Value = 102; }
        public void M102(Byte arg) { Flag.Value = 202; }

        public void M103(Int16 arg) { Flag.Value = 103; }
        public void M103(SByte arg) { Flag.Value = 203; }

        public void M104(Int16 arg) { Flag.Value = 104; }
        public void M104(UInt16 arg) { Flag.Value = 204; }

        public void M105(Int16 arg) { Flag.Value = 105; }
        public void M105(UInt32 arg) { Flag.Value = 205; }

        public void M106(Int16 arg) { Flag.Value = 106; }
        public void M106(Int32 arg) { Flag.Value = 206; }

        public void M107(Int16 arg) { Flag.Value = 107; }
        public void M107(UInt64 arg) { Flag.Value = 207; }

        public void M108(Int16 arg) { Flag.Value = 108; }
        public void M108(Int64 arg) { Flag.Value = 208; }

        public void M109(Int16 arg) { Flag.Value = 109; }
        public void M109(Decimal arg) { Flag.Value = 209; }

        public void M110(Int16 arg) { Flag.Value = 110; }
        public void M110(Single arg) { Flag.Value = 210; }

        public void M111(Int16 arg) { Flag.Value = 111; }
        public void M111(Double arg) { Flag.Value = 211; }

        public void M112(Int16 arg) { Flag.Value = 112; }
        public void M112(Object arg) { Flag.Value = 212; }

        #endregion
    }
    public class COverloads_Int32 {
        #region generated codes
        public void M100(Int32 arg) { Flag.Value = 100; }
        public void M100(Boolean arg) { Flag.Value = 200; }

        public void M101(Int32 arg) { Flag.Value = 101; }
        public void M101(Char arg) { Flag.Value = 201; }

        public void M102(Int32 arg) { Flag.Value = 102; }
        public void M102(Byte arg) { Flag.Value = 202; }

        public void M103(Int32 arg) { Flag.Value = 103; }
        public void M103(SByte arg) { Flag.Value = 203; }

        public void M104(Int32 arg) { Flag.Value = 104; }
        public void M104(UInt16 arg) { Flag.Value = 204; }

        public void M105(Int32 arg) { Flag.Value = 105; }
        public void M105(Int16 arg) { Flag.Value = 205; }

        public void M106(Int32 arg) { Flag.Value = 106; }
        public void M106(UInt32 arg) { Flag.Value = 206; }

        public void M107(Int32 arg) { Flag.Value = 107; }
        public void M107(UInt64 arg) { Flag.Value = 207; }

        public void M108(Int32 arg) { Flag.Value = 108; }
        public void M108(Int64 arg) { Flag.Value = 208; }

        public void M109(Int32 arg) { Flag.Value = 109; }
        public void M109(Decimal arg) { Flag.Value = 209; }

        public void M110(Int32 arg) { Flag.Value = 110; }
        public void M110(Single arg) { Flag.Value = 210; }

        public void M111(Int32 arg) { Flag.Value = 111; }
        public void M111(Double arg) { Flag.Value = 211; }

        public void M112(Int32 arg) { Flag.Value = 112; }
        public void M112(Object arg) { Flag.Value = 212; }

        #endregion
    }
    public class COverloads_Double {
        #region generated codes
        public void M100(Double arg) { Flag.Value = 100; }
        public void M100(Boolean arg) { Flag.Value = 200; }

        public void M101(Double arg) { Flag.Value = 101; }
        public void M101(Char arg) { Flag.Value = 201; }

        public void M102(Double arg) { Flag.Value = 102; }
        public void M102(Byte arg) { Flag.Value = 202; }

        public void M103(Double arg) { Flag.Value = 103; }
        public void M103(SByte arg) { Flag.Value = 203; }

        public void M104(Double arg) { Flag.Value = 104; }
        public void M104(UInt16 arg) { Flag.Value = 204; }

        public void M105(Double arg) { Flag.Value = 105; }
        public void M105(Int16 arg) { Flag.Value = 205; }

        public void M106(Double arg) { Flag.Value = 106; }
        public void M106(UInt32 arg) { Flag.Value = 206; }

        public void M107(Double arg) { Flag.Value = 107; }
        public void M107(Int32 arg) { Flag.Value = 207; }

        public void M108(Double arg) { Flag.Value = 108; }
        public void M108(UInt64 arg) { Flag.Value = 208; }

        public void M109(Double arg) { Flag.Value = 109; }
        public void M109(Int64 arg) { Flag.Value = 209; }

        public void M110(Double arg) { Flag.Value = 110; }
        public void M110(Decimal arg) { Flag.Value = 210; }

        public void M111(Double arg) { Flag.Value = 111; }
        public void M111(Single arg) { Flag.Value = 211; }

        public void M112(Double arg) { Flag.Value = 112; }
        public void M112(Object arg) { Flag.Value = 212; }

        #endregion
    }

    public class PublicEventArgs : EventArgs { }
    internal class PrivateEventArgs : PublicEventArgs { }
    public delegate IPublicInterface PublicDelegateType(IPublicInterface sender, PublicEventArgs args);

    // Private class
    internal class PrivateClass : IPublicInterface {
        public IPublicInterface Hello {
            get { return this; }
            set { }
        }

        public void Foo(IPublicInterface f) {
        }

        public IPublicInterface RetInterface() {
            return this;
        }

        public event PublicDelegateType MyEvent;
        public IPublicInterface FireEvent(PublicEventArgs args) {
            return MyEvent(this, args);
        }

        public PublicEventArgs GetEventArgs() {
            return new PrivateEventArgs();
        }
    }

    // Public Interface
    public interface IPublicInterface {
        IPublicInterface Hello { get; set; }
        void Foo(IPublicInterface f);
        IPublicInterface RetInterface();
        event PublicDelegateType MyEvent;
        IPublicInterface FireEvent(PublicEventArgs args);
        PublicEventArgs GetEventArgs();
    }

    // Access the private class via the public interface
    public class InterfaceOnlyTest {
        public static IPublicInterface PrivateClass {
            get {
                return new PrivateClass(); 
            }
        }
    }

    public class KeywordBase {
        public object SomeField;

        public object SomeProperty {
            get {
                return SomeField;
            }
            set {
                SomeField = value;
            }
        }
    }

    public class KeywordDerived : KeywordBase {
        public new string SomeField;

        public new string SomeProperty {
            get {
                return SomeField;
            }
            set {
                SomeField = value;
            }
        }
    }

    public class GenericOnlyConflict<T> { }
    public class GenericOnlyConflict<T1, T2> { }

    internal class BaseClassNoInterface {
        #region I1 Members

        public void M() {
            InterfaceTestHelper.Flag = true;
        }

        #endregion
        
    }

    internal class DerivedClassWithInterface : BaseClassNoInterface, I1 {
    }

    internal abstract class AbstractClassWithInterface : I1 {

        #region I1 Members

        public abstract void M();

        #endregion
    }

    internal class DerivedClassWithInterfaceImpl : AbstractClassWithInterface {
        #region I1 Members

        public override void M() {
            InterfaceTestHelper.Flag = true;
        }

        #endregion
    }

    public class InterfaceTestHelper {
        public static bool Flag = false;

        public static object GetObject() { return new DerivedClassWithInterface(); }
        public static object GetObject2() { return new DerivedClassWithInterfaceImpl(); }
    }

    public struct ValueTypeWithFields {
        public int X;
        public long Y;
    }

}
