// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;

namespace IronPythonTest {
    public enum DeTestEnum {
        Value_0,
        Value_1,
        Value_2,
        Value_3,
        Value_4
    }
    public enum DeTestEnumLong : long {
        Value_1 = 12345678901234L,
        Value_2 = 43210987654321L
    }

    public struct DeTestStruct {
        public int intVal;
        //public string stringVal;
        //public float floatVal;
        //public double doubleVal;
        //public DeTestEnum eshort;
        //public DeTestEnumLong elong;
#pragma warning disable 169
        void Init() {
            intVal = 0;
            //stringVal = "Hello";
            //floatVal = 0;
            //doubleVal = 0;
            //eshort = DeTestEnum.Value_3;
            //elong = DeTestEnumLong.Value_2;
        }
#pragma warning restore 169
    }

    public class DeTest {
        public delegate DeTestStruct TestStruct(DeTestStruct dts);
        public delegate string TestTrivial(string s1);
        public delegate string TestStringDelegate(string s1, string s2, string s3, string s4, string s5, string s6);
        public delegate int TestEnumDelegate(DeTestEnum e);
        public delegate long TestEnumDelegateLong(DeTestEnumLong e);
        public delegate string TestComplexDelegate(string s, int i, float f, double d, DeTestEnum e, string s2, DeTestEnum e2, DeTestEnumLong e3);

        public event TestStruct e_struct;
        public event TestTrivial e_tt;
        public event TestTrivial e_tt2;
        public event TestStringDelegate e_tsd;
        public event TestEnumDelegate e_ted;
        public event TestComplexDelegate e_tcd;
        public event TestEnumDelegateLong e_tedl;

        public TestStruct d_struct;
        public TestTrivial d_tt;
        public TestTrivial d_tt2;
        public TestStringDelegate d_tsd;
        public TestEnumDelegate d_ted;
        public TestComplexDelegate d_tcd;
        public TestEnumDelegateLong d_tedl;

        public string stringVal;
        public string stringVal2;
        public int intVal;
        public float floatVal;
        public double doubleVal;
        public DeTestEnum enumVal;
        public DeTestEnumLong longEnumVal;

        public DeTest() {
        }

        public void Init() {
            e_struct = ActualTestStruct;
            e_tt = VoidTestTrivial;
            e_tsd = VoidTestString;
            e_ted = VoidTestEnum;
            e_tedl = VoidTestEnumLong;
            e_tcd = VoidTestComplex;

            e_struct = ActualTestStruct;
            d_tt = VoidTestTrivial;
            d_tsd = VoidTestString;
            d_ted = VoidTestEnum;
            d_tedl = VoidTestEnumLong;
            d_tcd = VoidTestComplex;
        }

        public void RunTest() {
            if (e_struct != null) {
                DeTestStruct dts;
                dts.intVal = intVal;
                //dts.stringVal = stringVal;
                //dts.floatVal = floatVal;
                //dts.doubleVal = doubleVal;
                //dts.elong = longEnumVal;
                //dts.eshort = enumVal;

                e_struct(dts);
            }
            if (e_tt != null) {
                e_tt(stringVal);
            }
            if (e_tt2 != null) {
                e_tt2(stringVal2);
            }
            if (d_tt != null) {
                d_tt(stringVal);
            }
            if (e_tsd != null) {
                e_tsd(stringVal, stringVal2, stringVal, stringVal2, stringVal, stringVal2);
            }
            if (d_tsd != null) {
                d_tsd(stringVal, stringVal2, stringVal, stringVal2, stringVal, stringVal2);
            }
            if (e_ted != null) {
                e_ted(enumVal);
            }
            if (d_ted != null) {
                d_ted(enumVal);
            }
            if (e_tedl != null) {
                e_tedl(longEnumVal);
            }
            if (d_tedl != null) {
                d_tedl(longEnumVal);
            }
            if (e_tcd != null) {
                e_tcd(stringVal, intVal, floatVal, doubleVal, enumVal, stringVal2, enumVal, longEnumVal);
            }
            if (d_tcd != null) {
                d_tcd(stringVal, intVal, floatVal, doubleVal, enumVal, stringVal2, enumVal, longEnumVal);
            }
        }

        public DeTestStruct ActualTestStruct(DeTestStruct dts) {
            object x = dts;
            return (DeTestStruct)x;
        }

        public string ActualTestTrivial(string s1) {
            return s1;
        }

        public string VoidTestTrivial(string s1) {
            return String.Empty;
        }

        public string ActualTestString(string s1, string s2, string s3, string s4, string s5, string s6) {
            return s1 + " " + s2 + " " + s3 + " " + s4 + " " + s5 + " " + s6;
        }

        public string VoidTestString(string s1, string s2, string s3, string s4, string s5, string s6) {
            return String.Empty;
        }

        public int ActualTestEnum(DeTestEnum e) {
            return (int)e + 1000;
        }

        public int VoidTestEnum(DeTestEnum e) {
            return 0;
        }

        public long ActualTestEnumLong(DeTestEnumLong e) {
            return (long)e + 1000000000;
        }

        public long VoidTestEnumLong(DeTestEnumLong e) {
            return 0;
        }

        public string ActualTestComplex(string s, int i, float f, double d, DeTestEnum e, string s2, DeTestEnum e2, DeTestEnumLong e3) {
            return s + " " + i.ToString() + " " + f.ToString() + " " + d.ToString() + " " + s2 + " " + e2.ToString() + " " + e3.ToString();
        }

        public string VoidTestComplex(string s, int i, float f, double d, DeTestEnum e, string s2, DeTestEnum e2, DeTestEnumLong e3) {
            return String.Empty;
        }

        public void SetupE() {
            e_tt = ActualTestTrivial;
            e_tt2 = ActualTestTrivial;
            e_tsd = ActualTestString;
            e_ted = ActualTestEnum;
            e_tedl = ActualTestEnumLong;
            e_tcd = ActualTestComplex;
        }

        public void SetupS() {
            e_struct += ActualTestStruct;
            e_struct += ActualTestStruct;
        }

        public void SetupD() {
            d_tt = ActualTestTrivial;
            d_tt2 = ActualTestTrivial;
            d_tsd = ActualTestString;
            d_ted = ActualTestEnum;
            d_tedl = ActualTestEnumLong;
            d_tcd = ActualTestComplex;
        }

        public void Setup() {
            SetupE();
            SetupD();
        }

        public static DeTestStruct staticDeTestStruct(object o, DeTestStruct dts) {
            object oo = dts;
            return (DeTestStruct)oo;
        }

        public static void Test() {
            DeTest dt = new DeTest();

            dt.SetupS();
            dt.RunTest();
        }

    }

    public delegate float FloatDelegate(float f);

    public class ReturnTypes {
        public event FloatDelegate floatEvent;
        public float RunFloat(float f) {
            return floatEvent(f);
        }
    }

    public delegate void VoidDelegate();
    public class SimpleType {
        public event VoidDelegate SimpleEvent;
        public void RaiseEvent() {
            if (SimpleEvent != null)
                SimpleEvent();
        }
    }
}
