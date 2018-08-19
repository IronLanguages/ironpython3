// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace IronPythonTest.StaticTest {
    public class B { }
    public class D : B { }

    public delegate string MyEventHandler();

    public class Base {
        public static string Method_None() { return "Base.Method_None"; }
        public static string Method_OneArg(int arg) { return "Base.Method_OneArg"; }
        public static string Method_Base(Base arg) { return "Base.Method_Base"; }

        public static string Method_Inheritance1(B arg) { return "Base.Method_Inheritance1"; }
        public static string Method_Inheritance2(D arg) { return "Base.Method_Inheritance2"; }
        public static string Method_Inheritance3(B arg1, D arg2) { return "Base.Method_Inheritance3"; }

        public static string Field = "Base.Field";

        static string s_for_property = "Base.Property";
        public static string Property {
            get { return s_for_property; }
            set { s_for_property = value; }
        }

        public static event MyEventHandler Event;

        public static string TryEvent() {
            if (Event == null) {
                return "Still None";
            } else {
                return Event();
            }
        }
    }

    public class OverrideNothing : Base {
    }

    public class OverrideAll : Base {
        public new static string Method_None() { return "OverrideAll.Method_None"; }
        public new static string Method_OneArg(int arg) { return "OverrideAll.Method_OneArg"; }
        public static string Method_Base(OverrideAll arg) { return "OverrideAll.Method_Base"; }

        public static string Method_Inheritance1(D arg) { return "OverrideAll.Method_Inheritance1"; }
        public static string Method_Inheritance2(B arg) { return "OverrideAll.Method_Inheritance2"; }
        public static string Method_Inheritance3(D arg1, B arg2) { return "OverrideAll.Method_Inheritance3"; }

        public new static string Field = "OverrideAll.Field";

        static string s_for_property = "OverrideAll.Property";
        public new static string Property {
            get { return s_for_property; }
            set { s_for_property = value; }
        }

        public new static event MyEventHandler Event;

        public new static string TryEvent() {
            if (Event == null) {
                return "Still None here";
            } else {
                return Event();
            }
        }
    }

    public class GB<T> {
        public static string M1(T arg) { return "GB.M1"; }
        public static string M2(T arg) { return "GB.M2"; }
        public static string M3(T arg) { return "GB.M3"; }

        public static string M4() { return "GB.M4-A"; }
        public static string M4(ref T arg) { return "GB.M4-B"; }

        public static string M21(int arg1, int arg2=100) { return "GB.M21"; }
        public static string M22(int arg1) { return "GB.M22"; }
        public static string M23(int arg=100) { return "GB.M23"; }
        public static string M24(int arg=100) { return "GB.M24"; }
        public static string M25() { return "GB.M25"; }
    }

    public class GD : GB<string> {
        public static string M1(int arg) { return "GD.M1"; }
        public static string M2<T>(T arg) { return "GD.M2"; }
        public new static string M3(string arg) { return "GD.M3"; }

        public static string M4(ref int arg) { return "GD.M4"; }

        public static string M21(int arg1) { return "GD.M21"; }
        public static string M22(int arg1, int arg2=100) { return "GD.M22"; }
        public new static string M23(int arg1) { return "GD.M23"; }
        public static string M24() { return "GD.M24"; }
        public static string M25(int arg=100) { return "GD.M25"; }
    }

    public class KB {
        public static string M1(string arg) { return "KB.M1"; }
    }

    public class KD<T> : KB {
        public static string M1(T arg) { return "KD.M1"; }
    }
}

namespace IronPythonTest.StaticTest.Operator {
    public class G1<T> {
        internal T Field;
        public G1(T t) { Field = t; }
    }

    public class G2<T1, T2> {
        public T1 Field1;
        public T2 Field2;

        public G2(T1 t1, T2 t2) {
            Field1 = t1;
            Field2 = t2;
        }
        public static implicit operator G1<T1>(G2<T1, T2> x) { return new G1<T1>(x.Field1); }
        public static implicit operator G1<T2>(G2<T1, T2> x) { return new G1<T2>(x.Field2); }

        // well, not fit in the test purpose here; but just borrow the place to cover this
        public static explicit operator G2<T1, T2>(G1<T1> x) { return new G2<T1, T2>(x.Field, default(T2)); }
        public static explicit operator G2<T1, T2>(G1<T2> x) { return new G2<T1, T2>(default(T1), x.Field); }
    }

    public class SC : G2<int, string> {
        public SC(int t1, string t2) : base(t1, t2) { }
    }

    public class Use {
        public object M1(G1<int> x) { return x.Field; }
        public object M2(G1<string> x) { return x.Field; }
        public object M3(G1<object> x) { return x.Field; }

        public object M6(G2<int, string> x) { return x.Field1; }
    }
}
