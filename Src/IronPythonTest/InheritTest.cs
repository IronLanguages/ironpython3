// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using IronPython.Runtime;

namespace IronPythonTest {

    public interface IOverrideTestInterface {
        object x { get;set;}
        string Method();
        string y { get;set;}
        string MethodOverridden();

        object this[int index] {
            get;
            set;
        }
    }

    public class OverrideTestDerivedClass : IOverrideTestInterface {
        public static string Value;

        object IOverrideTestInterface.x { get { return "IOverrideTestInterface.x invoked"; } set { Value = value.ToString() + "x";  } }
        string IOverrideTestInterface.Method() { return "IOverrideTestInterface.method() invoked"; }
        string IOverrideTestInterface.y { get { return "IOverrideTestInterface.y invoked"; } set { Value = value.ToString() + "y";  } }
        public string y { get { return "OverrideTestDerivedClass.y invoked"; } }
        string IOverrideTestInterface.MethodOverridden() { return "IOverrideTestInterface.MethodOverridden() invoked"; }
        public string MethodOverridden() { return "OverrideTestDerivedClass.MethodOverridden() invoked"; }

        object IOverrideTestInterface.this[int index] {
            get {
                return "abc";
            }
            set {
                throw new NotImplementedException();
            }
        }

        public object z;
    }

    public class ClassWithLiteral {
        public const int Literal = 5;
    }

    public struct MySize {
        public const int MaxSize = Int32.MaxValue;
        public int width;
        public int height;

        public MySize(int w, int h) {
            width = w;
            height = h;
        }
    }

    public class BaseClassStaticConstructor {
        private static int value;
        static BaseClassStaticConstructor() {
            value = 10;
        }

        public int Value {
            get {
                return value;
            }
        }

        public override string ToString() {
            return "HelloWorld\r\nGoodBye";
        }
    }

    public class BaseClass {
        public MySize size;
        protected int foo;

        public BaseClass()
            : this(0, 0) {
        }

        public BaseClass(MySize size) {
            this.size = size;
        }

        public BaseClass(int width, int height)
            : this(new MySize(width, height)) {
        }

        public void GetWidthAndHeight(out int height, out int width) {
            height = this.Height;
            width = this.Width;
        }

        public virtual MySize Size {
            get {
                return size;
            }
            set {
                size = value;
            }
        }

        public virtual int Height {
            get {
                return size.height;
            }
            set {
                size.height = value;
            }
        }

        public virtual int Width {
            get {
                return size.width;
            }
            set {
                size.width = value;
            }
        }

        /// <summary>
        /// mixed access property
        /// </summary>
        public int Area {
            get {
                return size.width * size.height;
            }
            protected set {
                if (size.width != 0) {
                    size.height = value / size.width;
                } else if (size.height != 0) {
                    size.width = value / size.height;
                } else {
                    size.width = size.height = (int)Math.Round(Math.Sqrt(value));
                }
            }
        }

        public static implicit operator int(BaseClass from) {
            return from.Area + 1001;
        }

        protected string ProtectedMethod() {
            return "BaseClass.Protected";
        }

        public override string ToString() {
            return "HelloWorld\rGoodBye";
        }
    }

    public class NewClass : BaseClass {
        public new MySize size;

        public NewClass()
            : base(-1, -1) {
        }

        public NewClass(int width, int height) : base(-1, -1) {
            size.height = height;
            size.width = width;
        }

        public new void GetWidthAndHeight(out int height, out int width) {
            height = size.height * 2 + 1;
            width = size.width * 2 + 1;
        }

        public new int Area {
            get {
                return (size.width * size.height) * 2;
            }
        }

        public new virtual MySize Size {
            get {
                return new MySize(size.width+1, size.height+1);
            }
            set {
                size = value;
            }
        }

        public new virtual int Height {
            get {
                return size.height*2 + 1;
            }
            set {
                size.height = value/2 + 1;
            }
        }

        public new virtual int Width {
            get {
                return size.width*2 + 1;
            }
            set {
                size.width = value/2 + 1;
            }
        }
    }

    public class UnaryClass {
        public int value;

        public UnaryClass(int v) {
            value = v;
        }

        [SpecialName]
        public UnaryClass op_UnaryNegation() {
            return new UnaryClass(-value);
        }

        [SpecialName]
        public UnaryClass op_OnesComplement() {
            return new UnaryClass(~value);
        }

        public override string ToString() {
            return "HelloWorld\nGoodBye";
        }
    }

    public class BigCtor {
        public int A, B, C, D, E;
        public BigCtor(int a, int b, int c, int d, int e) {
            A = a;
            B = b;
            C = c;
            D = d;
            E = e;
        }
    }

    public class BiggerCtor {
        public int A, B, C, D, E, F;
        public BiggerCtor(int a, int b, int c, int d, int e, int f) {
            A = a;
            B = b;
            C = c;
            D = d;
            E = e;
            F = f;
        }
    }

    public class MixedBigCtor {
        public int A, B, C, D, E;
        public MixedBigCtor(int a, int b) {
            A = a;
            B = b;
        }

        public MixedBigCtor(int a, int b, int c, int d, int e) {
            A = a;
            B = b;
            C = c;
            D = d;
            E = e;
        }
    }

    public class BindingTestClass {
        public static object Bind(bool parm) {
            return parm;
        }

        public static object Bind(string parm) {
            return parm;
        }

        public static object Bind(int parm) {
            return parm;
        }
    }

    public class InheritedBindingBase {
        public virtual object Bind(bool parm) {
            return "Base bool";
        }

        public virtual object Bind(string parm) {
            return "Base string";
        }

        public virtual object Bind(int parm) {
            return "Base int";
        }
    }

    public class InheritedBindingSub : InheritedBindingBase {
        public override object Bind(bool parm) {
            return "Subclass bool";
        }
        public override object Bind(string parm) {
            return "Subclass string";
        }
        public override object Bind(int parm) {
            return "Subclass int";
        }
    }

    public class Infinite {
        public virtual object __cmp__(object other) {
            return true;
        }
    }


    public abstract class Overriding {
        private string protVal = "Overriding.Protected";

        public virtual object __cmp__(object other) {
            return "Overriding.__cmp__(object)";
        }
        public virtual object __cmp__(string other) {
            return "Overriding.__cmp__(string)";
        }
        public virtual object __cmp__(double other) {
            return "Overriding.__cmp__(double)";
        }
        public virtual object __cmp__(int other) {
            return "Overriding.__cmp__(int)";
        }
        public virtual string TopMethod() {
            return TemplateMethod() + " - and Top";
        }

        public virtual string TemplateMethod() {
            return "From Base";
        }

        public virtual string BigTopMethod() {
            return BigTemplateMethod(0, 1, 2, 3, 4, 5, 6, 7, 8, 9) + " - and Top";
        }

        public virtual string BigTemplateMethod(int a0, int a1, int a2, int a3, int a4, int a5, object a6, object a7, object a8, object a9) {
            return "BaseBigTemplate";
        }

        public virtual string AbstractTopMethod() {
            return AbstractTemplateMethod() + " - and Top";
        }

        public abstract string AbstractTemplateMethod();

        protected virtual string ProtectedMethod() {
            return "Overriding.ProtectedMethod";
        }

        public string CallProtected() {
            return ProtectedMethod();
        }

        protected virtual string ProtectedProperty {
            get {
                return protVal;
            }
            set {
                protVal = value;
            }
        }

        public string CallProtectedProp() {
            return ProtectedProperty;
        }

        public void SetProtectedProp(string val) {
            ProtectedProperty = val;
        }

        /// <summary>
        /// not defined in Inherited
        /// </summary>
        public virtual string SkippedProperty {
            get {
                return "Skipped";
            }
        }
    }

    public class Inherited : Overriding {
        private String _str;
        public Inherited() {
            this._str = TopMethod();
        }

        public override string AbstractTemplateMethod() {
            throw new Exception("The method or operation is not implemented.");
        }

        protected override string ProtectedMethod() {
            return "Inherited.ProtectedMethod";
        }

        protected override string ProtectedProperty {
            get {
                return "Inherited.Protected";
            }
            set {
                base.ProtectedProperty = value;
            }
        }

        public string String {
            get {
                return _str;
            }
        }
    }

    public interface ITestIt1 {
        object Method();
    }

    public interface ITestIt2 {
        object Method(int n);
    }

    public interface ITestIt3 {
        string Name {
            get;
        }
    }

    public class TestIt {
        public static object DoIt1(ITestIt1 o) {
            return o.Method();
        }
        public static object DoIt2(ITestIt2 o) {
            return o.Method(42);
        }
    }

    public class MoreOverridding {
        public virtual object Test1(params object[] args) {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < args.Length; i++) {
                sb.Append(args[i]);
            }
            return "C# Test1" + sb.ToString();
        }

        public virtual object Test2(object x, params object[] args) {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < args.Length; i++) {
                sb.Append(args[i]);
            }
            return "C# Test2" + sb.ToString();
        }

        public virtual object Test3(ref object x) {
            return "C# Test3";
        }

        public virtual object Test4(object x, ref object y) {
            return "C# Test4";
        }

        public virtual object Test5(StringComparison sc) {
            return "C# Test5" + sc.ToString();
        }

        public virtual object Test6(object x, StringComparison sc) {
            return "C# Test6" + x.ToString() + sc.ToString();
        }
        public object CallTest1() {
            return Test1("aa", "bb");
        }
        public object CallTest2() {
            return Test2("aa", "bb", "cc");
        }
        public object CallTest3(ref object x) {
            return Test3(ref x);
        }
        public object CallTest4(ref object y) {
            return Test4("aa", ref y);
        }

        public object CallTest5() {
            return Test5(StringComparison.Ordinal);
        }

        public object CallTest6() {
            return Test6("aa", StringComparison.Ordinal);
        }
    }

    public abstract class CliAbstractClass {
        public static int helperF;

        public static void MS(int x) { helperF = -2 * x; }
        public void MI(int x) { helperF = -3 * x; }
        public virtual void MV(int x) { helperF = -4 * x; }
        public abstract void MA(int x);
    }

    public interface CliInterface {
        void M1();
        int M2(int x);
    }

    public class UseCliClass {
        public void AsParam10(CliInterface itf) { itf.M1(); }
        public void AsParam11(CliInterface itf, int x) { itf.M2(x); }

        public void AsParam20(CliAbstractClass abs, int x) { abs.MV(x); }
        public void AsParam21(CliAbstractClass abs, int x) { abs.MA(x); }
        public void AsParam22(CliAbstractClass abs, int x) { CliAbstractClass.MS(x); }
        public void AsParam23(CliAbstractClass abs, int x) { abs.MI(x); }

        public CliInterface AsRetVal10(CliInterface itf) { return itf; }
    }

    public class CliVirtualStuff {
        private int m_var;
        public virtual int VirtualProperty {
            get { return m_var; }
            set { m_var = value; }
        }

        public virtual int VirtualMethod(int x) { return 10 * x; }

        protected virtual int VirtualProtectedProperty {
            get { return m_var; }
            set { m_var = value; }
        }

        protected virtual int VirtualProtectedMethod() { return -10; }

        public bool ProtectedStuffCheckHelper(int var, int ret) {
            return (VirtualProtectedMethod() == ret) && (VirtualProtectedProperty == var);
        }

        public bool PublicStuffCheckHelper(int var, int ret) {
            return (VirtualMethod(10) == ret) && (VirtualProperty == var);
        }
    }

    public class CtorTest {
        public int CtorRan;
        public object[] vals;
        public CtorTest() {
            CtorRan = -1;
        }

        public CtorTest(int a, int b, int c) {
            CtorRan = 0;
            vals = new object[] { a, b, c };
        }

        public CtorTest(string a, string b, string c) {
            CtorRan = 1;
            vals = new object[] { a, b, c };
        }

        public CtorTest(string a) {
            CtorRan = 2;
            vals = new object[] { a };
        }

        public CtorTest(object a) {
            CtorRan = 3;
            vals = new object[] { a };
        }
    }

    public class ProtectedCtorTest {
        protected ProtectedCtorTest() { }
    }

    public class ProtectedCtorTest1 {
        protected ProtectedCtorTest1(object x) { }
    }

    public class ProtectedCtorTest2 {
        protected ProtectedCtorTest2(object x) { }
        protected ProtectedCtorTest2(object x, object y) { }
    }

    public class ProtectedCtorTest3 {
        public ProtectedCtorTest3(object x) { }
        protected ProtectedCtorTest3() { }
    }

    public class ProtectedCtorTest4 {
        protected ProtectedCtorTest4(object x) { }
        public ProtectedCtorTest4() { }
    }

    public class ProtectedInternalCtorTest {
        protected internal ProtectedInternalCtorTest() { }
    }

    public class ProtectedInternalCtorTest1 {
        protected internal ProtectedInternalCtorTest1(object x) { }
    }

    public class ProtectedInternalCtorTest2 {
        protected internal ProtectedInternalCtorTest2(object x) { }
        protected internal ProtectedInternalCtorTest2(object x, object y) { }
    }

    public class ProtectedInternalCtorTest3 {
        public ProtectedInternalCtorTest3(object x) { }
        protected internal ProtectedInternalCtorTest3() { }
    }

    public class ProtectedInternalCtorTest4 {
        protected internal ProtectedInternalCtorTest4(object x) { }
        public ProtectedInternalCtorTest4() { }
    }

    public enum RtEnum { A, B }
    public struct RtStruct { public int F; public RtStruct(int arg) { F = arg; } }
    public class RtClass { public int F; public RtClass(int arg) { F = arg; } }
    public delegate int RtDelegate(int arg);

    public class CReturnTypes {
        public virtual void M_void() { }
        public virtual Char M_Char() { return Char.MaxValue; }
        public virtual Int32 M_Int32() { return Int32.MaxValue; }
        public virtual String M_String() { return "string"; }
        public virtual Int64 M_Int64() { return Int64.MaxValue; }
        public virtual Double M_Double() { return Double.MaxValue; }
        public virtual Boolean M_Boolean() { return true; }
        public virtual Single M_Single() { return Single.MaxValue; }
        public virtual Byte M_Byte() { return Byte.MaxValue; }
        public virtual SByte M_SByte() { return SByte.MaxValue; }
        public virtual Int16 M_Int16() { return Int16.MaxValue; }
        public virtual UInt32 M_UInt32() { return UInt32.MaxValue; }
        public virtual UInt64 M_UInt64() { return UInt64.MaxValue; }
        public virtual UInt16 M_UInt16() { return UInt16.MaxValue; }
        public virtual Type M_Type() { return typeof(int); }
        public virtual RtEnum M_RtEnum() { return RtEnum.A; }
        public virtual RtDelegate M_RtDelegate() { return ForDelegate; }
        public virtual RtStruct M_RtStruct() { RtStruct s = new RtStruct(1); return s; }
        public virtual RtClass M_RtClass() { RtClass c = new RtClass(1); return c; }
        public virtual System.Collections.IEnumerator M_IEnumerator() {
            int[] array = { 10, 20, 30 };
            return array.GetEnumerator();
        }
        public virtual System.Collections.IEnumerable M_IEnumerable() {
            int[] array = { 11, 22, 33 };
            return array;
        }
        public int ForDelegate(int arg) { return arg * 2; }

        // Non-Virtual method (the derived class in python will have the same name method)
        public int M_NonVirtual() { return 100; }
    }

    public class UseCReturnTypes {
        private CReturnTypes type;
        public UseCReturnTypes(CReturnTypes type) { this.type = type; }

        public void Use_void() { type.M_void(); }
        public Char Use_Char() { return type.M_Char(); }
        public Int32 Use_Int32() { return type.M_Int32(); }
        public String Use_String() { return type.M_String(); }
        public Int64 Use_Int64() { return type.M_Int64(); }
        public Double Use_Double() { return type.M_Double(); }
        public Boolean Use_Boolean() { return type.M_Boolean(); }
        public Single Use_Single() { return type.M_Single(); }
        public Byte Use_Byte() { return type.M_Byte(); }
        public SByte Use_SByte() { return type.M_SByte(); }
        public Int16 Use_Int16() { return type.M_Int16(); }
        public UInt32 Use_UInt32() { return type.M_UInt32(); }
        public UInt64 Use_UInt64() { return type.M_UInt64(); }
        public UInt16 Use_UInt16() { return type.M_UInt16(); }
        public Type Use_Type() { return type.M_Type(); }
        public RtEnum Use_RtEnum() { return type.M_RtEnum(); }
        public RtDelegate Use_RtDelegate() { return type.M_RtDelegate(); }
        public RtStruct Use_RtStruct() { return type.M_RtStruct(); }
        public RtClass Use_RtClass() { return type.M_RtClass(); }
        public System.Collections.IEnumerator Use_IEnumerator() { return type.M_IEnumerator(); }
        public System.Collections.IEnumerable Use_IEnumerable() { return type.M_IEnumerable(); }

        // calling Non-Virtual method 
        public int Use_NonVirtual() { return type.M_NonVirtual(); }
    }

    public interface IReturnTypes {
        void M_void();
        Char M_Char();
        Int32 M_Int32();
        String M_String();
        Int64 M_Int64();
        Double M_Double();
        Boolean M_Boolean();
        Single M_Single();
        Byte M_Byte();
        SByte M_SByte();
        Int16 M_Int16();
        UInt32 M_UInt32();
        UInt64 M_UInt64();
        UInt16 M_UInt16();
        Type M_Type();
        RtEnum M_RtEnum();
        RtDelegate M_RtDelegate();
        RtStruct M_RtStruct();
        RtClass M_RtClass();
        System.Collections.IEnumerator M_IEnumerator();
        System.Collections.IEnumerable M_IEnumerable();
    }

    public class UseIReturnTypes {
        private IReturnTypes type;
        public UseIReturnTypes(IReturnTypes type) { this.type = type; }

        public void Use_void() { type.M_void(); }
        public Char Use_Char() { return type.M_Char(); }
        public Int32 Use_Int32() { return type.M_Int32(); }
        public String Use_String() { return type.M_String(); }
        public Int64 Use_Int64() { return type.M_Int64(); }
        public Double Use_Double() { return type.M_Double(); }
        public Boolean Use_Boolean() { return type.M_Boolean(); }
        public Single Use_Single() { return type.M_Single(); }
        public Byte Use_Byte() { return type.M_Byte(); }
        public SByte Use_SByte() { return type.M_SByte(); }
        public Int16 Use_Int16() { return type.M_Int16(); }
        public UInt32 Use_UInt32() { return type.M_UInt32(); }
        public UInt64 Use_UInt64() { return type.M_UInt64(); }
        public UInt16 Use_UInt16() { return type.M_UInt16(); }
        public Type Use_Type() { return type.M_Type(); }
        public RtEnum Use_RtEnum() { return type.M_RtEnum(); }
        public RtDelegate Use_RtDelegate() { return type.M_RtDelegate(); }
        public RtStruct Use_RtStruct() { return type.M_RtStruct(); }
        public RtClass Use_RtClass() { return type.M_RtClass(); }
        public System.Collections.IEnumerator Use_IEnumerator() { return type.M_IEnumerator(); }
        public System.Collections.IEnumerable Use_IEnumerable() { return type.M_IEnumerable(); }
    }

    public abstract class AReturnTypes {
        public abstract void M_void();
        public abstract Char M_Char();
        public abstract Int32 M_Int32();
        public abstract String M_String();
        public abstract Int64 M_Int64();
        public abstract Double M_Double();
        public abstract Boolean M_Boolean();
        public abstract Single M_Single();
        public abstract Byte M_Byte();
        public abstract SByte M_SByte();
        public abstract Int16 M_Int16();
        public abstract UInt32 M_UInt32();
        public abstract UInt64 M_UInt64();
        public abstract UInt16 M_UInt16();
        public abstract Type M_Type();
        public abstract RtEnum M_RtEnum();
        public abstract RtDelegate M_RtDelegate();
        public abstract RtStruct M_RtStruct();
        public abstract RtClass M_RtClass();
        public abstract System.Collections.IEnumerator M_IEnumerator();
        public abstract System.Collections.IEnumerable M_IEnumerable();
    }

    public class UseAReturnTypes {
        private AReturnTypes type;
        public UseAReturnTypes(AReturnTypes type) { this.type = type; }

        public void Use_void() { type.M_void(); }
        public Char Use_Char() { return type.M_Char(); }
        public Int32 Use_Int32() { return type.M_Int32(); }
        public String Use_String() { return type.M_String(); }
        public Int64 Use_Int64() { return type.M_Int64(); }
        public Double Use_Double() { return type.M_Double(); }
        public Boolean Use_Boolean() { return type.M_Boolean(); }
        public Single Use_Single() { return type.M_Single(); }
        public Byte Use_Byte() { return type.M_Byte(); }
        public SByte Use_SByte() { return type.M_SByte(); }
        public Int16 Use_Int16() { return type.M_Int16(); }
        public UInt32 Use_UInt32() { return type.M_UInt32(); }
        public UInt64 Use_UInt64() { return type.M_UInt64(); }
        public UInt16 Use_UInt16() { return type.M_UInt16(); }
        public Type Use_Type() { return type.M_Type(); }
        public RtEnum Use_RtEnum() { return type.M_RtEnum(); }
        public RtDelegate Use_RtDelegate() { return type.M_RtDelegate(); }
        public RtStruct Use_RtStruct() { return type.M_RtStruct(); }
        public RtClass Use_RtClass() { return type.M_RtClass(); }
        public System.Collections.IEnumerator Use_IEnumerator() { return type.M_IEnumerator(); }
        public System.Collections.IEnumerable Use_IEnumerable() { return type.M_IEnumerable(); }
    }

    public class BigVirtualClass {
        public virtual int M0() { return 0; }
        public virtual int M1() { return 1; }
        public virtual int M2() { return 2; }
        public virtual int M3() { return 3; }
        public virtual int M4() { return 4; }
        public virtual int M5() { return 5; }
        public virtual int M6() { return 6; }
        public virtual int M7() { return 7; }
        public virtual int M8() { return 8; }
        public virtual int M9() { return 9; }
        public virtual int M10() { return 10; }
        public virtual int M11() { return 11; }
        public virtual int M12() { return 12; }
        public virtual int M13() { return 13; }
        public virtual int M14() { return 14; }
        public virtual int M15() { return 15; }
        public virtual int M16() { return 16; }
        public virtual int M17() { return 17; }
        public virtual int M18() { return 18; }
        public virtual int M19() { return 19; }
        public virtual int M20() { return 20; }
        public virtual int M21() { return 21; }
        public virtual int M22() { return 22; }
        public virtual int M23() { return 23; }
        public virtual int M24() { return 24; }
        public virtual int M25() { return 25; }
        public virtual int M26() { return 26; }
        public virtual int M27() { return 27; }
        public virtual int M28() { return 28; }
        public virtual int M29() { return 29; }
        public virtual int M30() { return 30; }
        public virtual int M31() { return 31; }
        public virtual int M32() { return 32; }
        public virtual int M33() { return 33; }
        public virtual int M34() { return 34; }
        public virtual int M35() { return 35; }
        public virtual int M36() { return 36; }
        public virtual int M37() { return 37; }
        public virtual int M38() { return 38; }
        public virtual int M39() { return 39; }
        public virtual int M40() { return 40; }
        public virtual int M41() { return 41; }
        public virtual int M42() { return 42; }
        public virtual int M43() { return 43; }
        public virtual int M44() { return 44; }
        public virtual int M45() { return 45; }
        public virtual int M46() { return 46; }
        public virtual int M47() { return 47; }
        public virtual int M48() { return 48; }
        public virtual int M49() { return 49; }
        public int CallM0() { return M0(); }
        public int CallM1() { return M1(); }
        public int CallM2() { return M2(); }
        public int CallM3() { return M3(); }
        public int CallM4() { return M4(); }
        public int CallM5() { return M5(); }
        public int CallM6() { return M6(); }
        public int CallM7() { return M7(); }
        public int CallM8() { return M8(); }
        public int CallM9() { return M9(); }
        public int CallM10() { return M10(); }
        public int CallM11() { return M11(); }
        public int CallM12() { return M12(); }
        public int CallM13() { return M13(); }
        public int CallM14() { return M14(); }
        public int CallM15() { return M15(); }
        public int CallM16() { return M16(); }
        public int CallM17() { return M17(); }
        public int CallM18() { return M18(); }
        public int CallM19() { return M19(); }
        public int CallM20() { return M20(); }
        public int CallM21() { return M21(); }
        public int CallM22() { return M22(); }
        public int CallM23() { return M23(); }
        public int CallM24() { return M24(); }
        public int CallM25() { return M25(); }
        public int CallM26() { return M26(); }
        public int CallM27() { return M27(); }
        public int CallM28() { return M28(); }
        public int CallM29() { return M29(); }
        public int CallM30() { return M30(); }
        public int CallM31() { return M31(); }
        public int CallM32() { return M32(); }
        public int CallM33() { return M33(); }
        public int CallM34() { return M34(); }
        public int CallM35() { return M35(); }
        public int CallM36() { return M36(); }
        public int CallM37() { return M37(); }
        public int CallM38() { return M38(); }
        public int CallM39() { return M39(); }
        public int CallM40() { return M40(); }
        public int CallM41() { return M41(); }
        public int CallM42() { return M42(); }
        public int CallM43() { return M43(); }
        public int CallM44() { return M44(); }
        public int CallM45() { return M45(); }
        public int CallM46() { return M46(); }
        public int CallM47() { return M47(); }
        public int CallM48() { return M48(); }
        public int CallM49() { return M49(); }
    }

    public class StrangeOverrides {
        public virtual object SomeMethodWithContext(CodeContext context, object arg) {
            Debug.Assert(this != null && this is StrangeOverrides);
            return arg;
        }

        public virtual object ParamsMethodWithContext(CodeContext context, params object[] args) {
            Debug.Assert(this != null && this is StrangeOverrides);
            return args;
        }

        public virtual object ParamsIntMethodWithContext(CodeContext context, params int[] args) {
            Debug.Assert(this != null && this is StrangeOverrides);
            return args;
        }

        public object CallWithContext(CodeContext context, object arg) {
            return SomeMethodWithContext(context, arg);
        }

        public object CallParamsWithContext(CodeContext context, params object[] arg) {
            return ParamsMethodWithContext(context, arg);
        }

        public object CallIntParamsWithContext(CodeContext  context, params int[] arg) {
            return ParamsIntMethodWithContext(context, arg);
        }
    }

    // helper classes for testing events & interfaces

    public interface IEventInterface {
        event SimpleDelegate MyEvent;
    }

    public abstract class AbstractEvent {
        public abstract event SimpleDelegate MyEvent;

    }

    public static class UseEvent {
        public static bool Called;

        public static void Hook(IEventInterface eventInterface) {
            eventInterface.MyEvent += new SimpleDelegate(CallMe);
        }

        public static void Hook(AbstractEvent abstractEvent) {
            abstractEvent.MyEvent += new SimpleDelegate(CallMe);
        }

        public static void Hook(VirtualEvent virtualEvent) {
            virtualEvent.MyEvent += new SimpleDelegate(CallMe);
        }

        public static void Unhook(IEventInterface eventInterface) {
            eventInterface.MyEvent -= new SimpleDelegate(CallMe);
        }

        public static void Unhook(AbstractEvent abstractEvent) {
            abstractEvent.MyEvent -= new SimpleDelegate(CallMe);
        }

        public static void Unhook(VirtualEvent virtualEvent) {
            virtualEvent.MyEvent -= new SimpleDelegate(CallMe);
        }

        private static void CallMe() {
            Called = true;
        }
    }

    public class VirtualEvent {
        private List<SimpleDelegate> _events = new List<SimpleDelegate>();
        public virtual event SimpleDelegate MyEvent;
        public virtual event SimpleDelegate MyCustomEvent {
            add {
                LastCall = "Add";
                _events.Add(value);
            }
            remove {
                LastCall = "Remove";
                _events.Remove(value);
            }
        }
        public string LastCall;

        public virtual void FireEvent() {
            SimpleDelegate simple = MyEvent;
            simple?.Invoke();
        }
    }

    public class OverrideVirtualEvent : VirtualEvent {
        private List<SimpleDelegate> _events = new List<SimpleDelegate>();
        public override event SimpleDelegate MyEvent;
        public override event SimpleDelegate MyCustomEvent {
            add {
                LastCall = "OverrideAdd";
                _events.Add(value);
            }
            remove {
                LastCall = "OverrideRemove";
                _events.Remove(value);
            }
        }

        public override void FireEvent() {
            SimpleDelegate simple = MyEvent;
            simple?.Invoke();
        }
    }

    public class WriteOnly {
        public object Value;

        public object Writer {
            set {
                Value = value;
            }
        }
    }

    public class RudeObjectOverride {
        public override string ToString() {
            return null;
        }
    }

    public interface IGenericMethods {
        TParam Factory0<TParam>();
        T Factory1<T>(T arg);
        bool OutParam<T>(out T arg);
        void RefParam<T>(ref T arg);
        T3 Wild<T1, T2, T3>(bool first, ref IEnumerable<T1> second, out T2 third, IList<T3> fourth);

    }

    public class MixedProperties {
        private object _foo, _bar;
        public object Foo {
            get {
                return _foo;
            }
            protected set {
                _foo = value;
            }
        }

        public object Bar {
            protected get {
                return _bar;
            }
            set {
                _bar = value;
            }
        }

        public object GetFoo() {
            return _foo;
        }

        public object GetBar() {
            return _bar;
        }
    }

    public class MixedPropertiesInherited : MixedProperties {
    }

    public static class GenericMethodTester {
        public static int TestIntFactory0(IGenericMethods i) { return i.Factory0<int>(); }
        public static string TestStringFactory0(IGenericMethods i) { return i.Factory0<string>(); }
        public static int TestIntFactory1(IGenericMethods i, int test) { return i.Factory1<int>(test); }
        public static string TestStringFactory1(IGenericMethods i, string test) { return i.Factory1<string>(test); }
        public static int TestOutParamInt(IGenericMethods i) {
            int value;
            i.OutParam<int>(out value);
            return value;
        }
        public static string TestOutParamString(IGenericMethods i) {
            string value;
            i.OutParam<string>(out value);
            return value;
        }
        public static int TestRefParamInt(IGenericMethods i, int inValue) {
            i.RefParam<int>(ref inValue);
            return inValue;
        }
        public static string TestRefParamString(IGenericMethods i, string inValue) {
            i.RefParam<string>(ref inValue);
            return inValue;
        }
        private static IEnumerable<string> Yielder(string s) {
            yield return s;
        }
        public static object[] GoWild(IGenericMethods i, bool first, string second, IList<int> value) {
            double obj;
            IEnumerable<string> actualSecond = Yielder(second);
            int len = i.Wild<string, double, int>(first, ref actualSecond, out obj, value);
            object[] result = new object[len];
            result[0] = (object)obj;
            return result;
        }
    }

    public class FamilyOrAssembly {
        private object _value;
        
        protected internal int Method() {
            return 42;
        }

        protected internal object Property {
            get {
                return _value;
            }
            set {
                _value = value;
            }
        }
    }
}
