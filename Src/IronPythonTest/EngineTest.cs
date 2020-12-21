// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Linq.Expressions;
using System.Numerics;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security;
#if FEATURE_REMOTING
using System.Security.Policy;
#endif
using System.Text;
using System.Threading;
#if FEATURE_WPF
using System.Windows.Markup;
#endif

using Microsoft.Scripting;
using Microsoft.Scripting.Generation;
using Microsoft.Scripting.Hosting;
using Microsoft.Scripting.Runtime;

using IronPython;
using IronPython.Hosting;
using IronPython.Runtime;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

using NUnit.Framework;


#if FEATURE_WPF
using DependencyObject = System.Windows.DependencyObject;
#endif

[assembly: ExtensionType(typeof(IronPythonTest.IFooable), typeof(IronPythonTest.FooableExtensions))]
namespace IronPythonTest {
    internal class Common {
        public static string RootDirectory;
        public static string RuntimeDirectory;
        public static string ScriptTestDirectory;
        public static string InputTestDirectory;

        static Common() {
            RuntimeDirectory = Path.GetDirectoryName(typeof(PythonContext).Assembly.Location);
            RootDirectory = FindRoot();
            ScriptTestDirectory = Path.Combine(RootDirectory, "Tests");
            InputTestDirectory = Path.Combine(ScriptTestDirectory, "Inputs");
        }

        private static string FindRoot() {
            // we start at the current directory and look up until we find the "Src" directory
            var current = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var found = false;
            while (!found && !string.IsNullOrEmpty(current)) {
                var test = Path.Combine(current, "Src", "StdLib", "Lib");
                if (Directory.Exists(test)) {
                    return current;
                }

                current = Path.GetDirectoryName(current);
            }
            return string.Empty;
        }
    }

    public static class TestHelpers {
        public static LanguageContext GetContext(CodeContext context) {
            return context.LanguageContext;
        }

        public static int HashObject(object o) {
            return o.GetHashCode();
        }
    }

    public delegate int IntIntDelegate(int arg);
    public delegate string RefStrDelegate(ref string arg);
    public delegate int RefIntDelegate(ref int arg);
    public delegate T GenericDelegate<T, U, V>(U arg1, V arg2);

#if FEATURE_WPF
    [ContentProperty("Content")]
    public class XamlTestObject : DependencyObject {
        public event IntIntDelegate Event;
        public int Method(int arg) {
            if (Event != null)
                return Event(arg);
            
            return -1;
        }

        public object Content {
            get;
            set;
        }
    }

    [ContentProperty("Content")]
    [RuntimeNameProperty("MyName")]
    public class InnerXamlTextObject : DependencyObject {
        public object Content {
            get;
            set;
        }

        public string MyName {
            get;
            set;
        }
    }

    [ContentProperty("Content")]
    [RuntimeNameProperty("Name")]
    public class InnerXamlTextObject2 : DependencyObject {
        public object Content {
            get;
            set;
        }

        public string Name {
            get;
            set;
        }
    }
#endif

    public class ClsPart {
        public int Field;
        private int m_property;
        public int Property { get { return m_property; } set { m_property = value; } }
        public event IntIntDelegate Event;
        public int Method(int arg) {
            if (Event != null)
                return Event(arg);
            else
                return -1;
        }

        // Private members
#pragma warning disable 169
        // This field is accessed from the test
        private int privateField;
        private int privateProperty { get { return m_property; } set { m_property = value; } }
        private event IntIntDelegate privateEvent;
        private int privateMethod(int arg) {
            if (privateEvent != null)
                return privateEvent(arg);
            else
                return -1;
        }
        private static int privateStaticMethod() {
            return 100;
        }
#pragma warning restore 169
    }

    internal class InternalClsPart {
#pragma warning disable 649
        // This field is accessed from the test
        internal int Field;
#pragma warning restore 649
        private int m_property;
        internal int Property { get { return m_property; } set { m_property = value; } }
        internal event IntIntDelegate Event;
        internal int Method(int arg) {
            if (Event != null)
                return Event(arg);
            else
                return -1;
        }
    }

    public class EngineTest
#if FEATURE_REMOTING
        : MarshalByRefObject
#endif
    {
        private readonly ScriptEngine _pe;
        private readonly ScriptRuntime _env;

        public EngineTest() {
            // Load a script with all the utility functions that are required
            // pe.ExecuteFile(InputTestDirectory + "\\EngineTests.py");
            _env = Python.CreateRuntime();
            _pe = _env.GetEngine("py");
        }

        // Used to test exception thrown in another domain can be shown correctly.
        public void Run(string script) {
            ScriptScope scope = _env.CreateScope();
            _pe.CreateScriptSourceFromString(script, SourceCodeKind.File).Execute(scope);
        }

        private static readonly string clspartName = "clsPart";

#if FEATURE_REMOTING
        // [Test] // https://github.com/IronLanguages/ironpython3/issues/904
        public void ScenarioHostingHelpers() {
            AppDomain remote = AppDomain.CreateDomain("foo");
            Dictionary<string, object> options = new Dictionary<string,object>();
            // DLR ScriptRuntime options
            options["Debug"] = true;
            options["PrivateBinding"] = true;

            // python options
            options["StripDocStrings"] = true;
            options["Optimize"] = true;
            options["RecursionLimit"] = 42;
            options["IndentationInconsistencySeverity"] = Severity.Warning;
            options["WarningFilters"] = new string[] { "warnonme" };

            ScriptEngine engine1 = Python.CreateEngine();
            ScriptEngine engine2 = Python.CreateEngine(AppDomain.CurrentDomain);
            ScriptEngine engine3 = Python.CreateEngine(remote);

            TestEngines(null, new ScriptEngine[] { engine1, engine2, engine3 });

            ScriptEngine engine4 = Python.CreateEngine(options);
            ScriptEngine engine5 = Python.CreateEngine(AppDomain.CurrentDomain, options);
            ScriptEngine engine6 = Python.CreateEngine(remote, options);

            TestEngines(options, new ScriptEngine[] { engine4, engine5, engine6 });

            ScriptRuntime runtime1 = Python.CreateRuntime();
            ScriptRuntime runtime2 = Python.CreateRuntime(AppDomain.CurrentDomain);
            ScriptRuntime runtime3 = Python.CreateRuntime(remote);

            TestRuntimes(null, new ScriptRuntime[] { runtime1, runtime2, runtime3 });

            ScriptRuntime runtime4 = Python.CreateRuntime(options);
            ScriptRuntime runtime5 = Python.CreateRuntime(AppDomain.CurrentDomain, options);
            ScriptRuntime runtime6 = Python.CreateRuntime(remote, options);

            TestRuntimes(options, new ScriptRuntime[] { runtime4, runtime5, runtime6 });
        }

        private void TestEngines(Dictionary<string, object> options, ScriptEngine[] engines) {
            foreach (ScriptEngine engine in engines) {
                TestEngine(engine, options);
                TestRuntime(engine.Runtime, options);
            }
        }

        private void TestRuntimes(Dictionary<string, object> options, ScriptRuntime[] runtimes) {
            foreach (ScriptRuntime runtime in runtimes) {
                TestRuntime(runtime, options);

                TestEngine(Python.GetEngine(runtime), options);
            }
        }

        private void TestEngine(ScriptEngine scriptEngine, Dictionary<string, object> options) {
            // basic smoke tests that the engine is alive and working
            Assert.AreEqual((int)(object)scriptEngine.Execute("42"), 42);

            if(options != null) {
// TODO:
#pragma warning disable 618 // obsolete API
                PythonOptions po = (PythonOptions)Microsoft.Scripting.Hosting.Providers.HostingHelpers.CallEngine<object, LanguageOptions>(
                    scriptEngine,
                    (lc, obj) => lc.Options,
                    null
                );
#pragma warning restore 618

                Assert.AreEqual(po.StripDocStrings, true);
                Assert.AreEqual(po.Optimize, true);
                Assert.AreEqual(po.RecursionLimit, 42);
                Assert.AreEqual(po.IndentationInconsistencySeverity, Severity.Warning);
                Assert.AreEqual(po.WarningFilters[0], "warnonme");
            }

            Assert.AreEqual(Python.GetSysModule(scriptEngine).GetVariable<string>("platform"), "cli");
            Assert.AreEqual(Python.GetBuiltinModule(scriptEngine).GetVariable<bool>("True"), true);
            if(System.Environment.OSVersion.Platform == System.PlatformID.Unix) {
                Assert.AreEqual(Python.ImportModule(scriptEngine, "posix").GetVariable<int>("F_OK"), 0);
            } else {
                Assert.AreEqual(Python.ImportModule(scriptEngine, "nt").GetVariable<int>("F_OK"), 0);
            }
            Assert.Throws<ImportException>(() => {
                Python.ImportModule(scriptEngine, "non_existant_module");
            });
        }

        private void TestRuntime(ScriptRuntime runtime, Dictionary<string, object> options) {
            // basic smoke tests that the runtime is alive and working
            runtime.Globals.SetVariable("hello", 42);
            Assert.NotNull(runtime.GetEngine("py"));

            if (options != null) {
                Assert.AreEqual(runtime.Setup.DebugMode, true);
                Assert.AreEqual(runtime.Setup.PrivateBinding, true);
            }

            Assert.AreEqual(Python.GetSysModule(runtime).GetVariable<string>("platform"), "cli");
            Assert.AreEqual(Python.GetBuiltinModule(runtime).GetVariable<bool>("True"), true);
            if(System.Environment.OSVersion.Platform == System.PlatformID.Unix) {
                Assert.AreEqual(Python.ImportModule(runtime, "posix").GetVariable<int>("F_OK"), 0);
            } else {
                Assert.AreEqual(Python.ImportModule(runtime, "nt").GetVariable<int>("F_OK"), 0);
            }

            Assert.Throws<ImportException>(() => {
                Python.ImportModule(runtime, "non_existant_module");
            });
        }
#endif

        public class ScopeDynamicObject : DynamicObject {
            internal readonly Dictionary<string, object> _members = new Dictionary<string, object>();

            public override bool TryGetMember(GetMemberBinder binder, out object result) {
                return _members.TryGetValue(binder.Name, out result);
            }

            public override bool TrySetMember(SetMemberBinder binder, object value) {
                _members[binder.Name] = value;
                return true;
            }

            public override bool TryDeleteMember(DeleteMemberBinder binder) {
                return _members.Remove(binder.Name);
            }
        }

        public class ScopeDynamicObject2 : ScopeDynamicObject {
            public readonly object __doc__ = null;
        }

        public class ScopeDynamicObject3 : ScopeDynamicObject {
            public object __doc__ {
                get {
                    return null;
                }
            }
        }

        public class ScopeDynamicObject4 : ScopeDynamicObject {
            private object _doc;
            public object __doc__ {
                get {
                    return _doc;
                }
                set {
                    _doc = value;
                }
            }
        }

        public class ScopeDynamicObject5 : ScopeDynamicObject {
            public object __doc__;
        }

        public class ScopeDynamicObject6 : ScopeDynamicObject {
            public void __doc__() {
            }
        }

        public class ScopeDynamicObject7 : ScopeDynamicObject {
            public class __doc__ {
            }
        }

        public class ScopeDynamicObject8 : ScopeDynamicObject {
#pragma warning disable 67
            public event EventHandler __doc__;
#pragma warning restore 67
        }

        [Test]
        public void ScenarioDynamicObjectAsScope() {
            var engine = Python.CreateEngine();

            // tests where __doc__ gets assigned into the members dictionary
            foreach (var myScope in new ScopeDynamicObject[] { new ScopeDynamicObject(), new ScopeDynamicObject2(), new ScopeDynamicObject3(), new ScopeDynamicObject6(), new ScopeDynamicObject7(), new ScopeDynamicObject8() }) {
                var scope = engine.CreateScope(myScope);
                engine.Execute(@"
x = 42", scope);

                var source = engine.CreateScriptSourceFromString("x = 42", SourceCodeKind.File);
                source.Compile().Execute(scope);

                Assert.AreEqual(myScope._members.ContainsKey("__doc__"), true);
                Assert.AreEqual(myScope._members.ContainsKey("x"), true);
                Assert.AreEqual(myScope._members.ContainsKey("__file__"), true);

                source = engine.CreateScriptSourceFromString("'hello world'", SourceCodeKind.File);
                source.Compile().Execute(scope);

                Assert.AreEqual(myScope._members["__doc__"], "hello world");
            }

            // tests where __doc__ gets assigned into a field/property
            {
                ScopeDynamicObject myScope = new ScopeDynamicObject4();
                var scope = engine.CreateScope(myScope);

                var source = engine.CreateScriptSourceFromString("'hello world'\nx=42\n", SourceCodeKind.File);
                source.Compile().Execute(scope);

                Assert.AreEqual(((ScopeDynamicObject4)myScope).__doc__, "hello world");

                myScope = new ScopeDynamicObject5();
                scope = engine.CreateScope(myScope);

                source.Compile().Execute(scope);
                Assert.AreEqual(((ScopeDynamicObject5)myScope).__doc__, "hello world");
            }
        }

        [Test]
        public void ScenarioCodePlex20472() {
#if NETCOREAPP
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
#endif
            // This test file is encoded in Windows codepage 1251 (Cyrilic) but lacks a magic comment (PEP-263)
            string fileName = Path.Combine(Path.Combine(Common.ScriptTestDirectory, "encoded_files"), "cp20472.py");
            try {
                _pe.CreateScriptSourceFromFile(fileName).Compile();

                // The line above should have thrown a syntax exception or an import error
                // because the default source file encoding is UTF-8 and there are decoding errors

                throw new Exception("ScenarioCodePlex20472");
            }
            catch (SyntaxErrorException) { }

            // Opening the file with explicitly specifying the correct encoding should work
            CompiledCode prog = _pe.CreateScriptSourceFromFile(fileName, Encoding.GetEncoding(1251)).Compile();
            prog.Execute();
            Assert.AreEqual(prog.DefaultScope.GetVariable("s"), "\u041F\u0436\u0451");
        }

        [Test]
        public void ScenarioInterpreterNestedVariables() {
            ParameterExpression arg = Expression.Parameter(typeof(object), "tmp");
            var argBody = Expression.Lambda<Func<object, IRuntimeVariables>>(
                Expression.RuntimeVariables(
                    arg
                ),
                arg
            );

            var vars = CompilerHelpers.LightCompile(argBody)(42);
            Assert.AreEqual(vars[0], 42);

            ParameterExpression tmp = Expression.Parameter(typeof(object), "tmp");
            var body = Expression.Lambda<Func<object>>(
                Expression.Block(
                    Expression.Block(
                        new[] { tmp },
                        Expression.Assign(tmp, Expression.Constant(42, typeof(object)))
                    ),
                    Expression.Block(
                        new[] { tmp },
                        tmp
                    )
                )
            );

            Assert.AreEqual(body.Compile()(), ClrModule.IsNetCoreApp || ClrModule.IsMono ? (object)42 : null); // https://github.com/IronLanguages/ironpython3/issues/908
            Assert.AreEqual(CompilerHelpers.LightCompile(body)(), null);

            body = Expression.Lambda<Func<object>>(
                Expression.Block(
                    Expression.Block(
                        new[] { tmp },
                        Expression.Block(
                            Expression.Assign(tmp, Expression.Constant(42, typeof(object))),
                            Expression.Block(
                                new[] { tmp },
                                tmp
                            )
                        )
                    )
                )
            );
            Assert.AreEqual(CompilerHelpers.LightCompile(body)(), null);
            Assert.AreEqual(body.Compile()(), null);
        }

        public class TestCodePlex23562 {
            public bool MethodCalled = false;
            public TestCodePlex23562() {
            }
            public void TestMethod() {
                MethodCalled = true;
            }
        }

        [Test]
        public void ScenarioCodePlex23562()
        {
            string pyCode = @"
test = TestCodePlex23562()
test.TestMethod()
";
            var scope = _pe.CreateScope();
            scope.SetVariable("TestCodePlex23562", typeof(TestCodePlex23562));
            _pe.Execute(pyCode, scope);

            TestCodePlex23562 temp = scope.GetVariable<TestCodePlex23562>("test");
            Assert.True(temp.MethodCalled);
        }

        [Test]
        public void ScenarioCodePlex18595() {
            string pyCode = @"
str_tuple = ('ab', 'cd')
str_list  = ['abc', 'def', 'xyz']

py_func_called = False
def py_func():
    global py_func_called
    py_func_called = True
";
            var scope = _pe.CreateScope();
            _pe.Execute(pyCode, scope);

            IList<string> str_tuple = scope.GetVariable<IList<string>>("str_tuple");
            Assert.AreEqual(str_tuple.Count, 2);
            IList<string> str_list  = scope.GetVariable<IList<string>>("str_list");
            Assert.AreEqual(str_list.Count, 3);
            VoidDelegate py_func = scope.GetVariable<VoidDelegate>("py_func");
            py_func();
            Assert.AreEqual(scope.GetVariable<bool>("py_func_called"), true);
        }

        [Test]
        public void ScenarioCodePlex24077()
        {
            string pyCode = @"
class K(object):
    def __init__(self, a, b, c):
        global A, B, C
        A = a
        B = b
        C = c
";
            var scope = _pe.CreateScope();
            _pe.Execute(pyCode, scope);
            object KKlass = scope.GetVariable("K");
            object[] Kparams = new object[] { 1, 3.14, "abc"};
            _pe.Operations.CreateInstance(KKlass, Kparams);
            Assert.AreEqual(scope.GetVariable<int>("A"), 1);
        }

        // Execute
        [Test]
        public void ScenarioExecute() {
            ClsPart clsPart = new ClsPart();

            ScriptScope scope = _env.CreateScope();

            scope.SetVariable(clspartName, clsPart);

            // field: assign and get back
            _pe.Execute("clsPart.Field = 100", scope);
            _pe.Execute("if 100 != clsPart.Field: raise AssertionError('test failed')", scope);
            Assert.AreEqual(100, clsPart.Field);

            // property: assign and get back
            _pe.Execute("clsPart.Property = clsPart.Field", scope);
            _pe.Execute("if 100 != clsPart.Property: raise AssertionError('test failed')", scope);
            Assert.AreEqual(100, clsPart.Property);

            // method: Event not set yet
            _pe.Execute("a = clsPart.Method(2)", scope);
            _pe.Execute("if -1 != a: raise AssertionError('test failed')", scope);

            // method: add python func as event handler
            _pe.Execute("def f(x) : return x * x", scope);
            _pe.Execute("clsPart.Event += f", scope);
            _pe.Execute("a = clsPart.Method(2)", scope);
            _pe.Execute("if 4 != a: raise AssertionError('test failed')", scope);

            // ===============================================

            // reset the same variable with instance of the same type
            scope.SetVariable(clspartName, new ClsPart());
            _pe.Execute("if 0 != clsPart.Field: raise AssertionError('test failed')", scope);

            // add cls method as event handler
            scope.SetVariable("clsMethod", new IntIntDelegate(Negate));
            _pe.Execute("clsPart.Event += clsMethod", scope);
            _pe.Execute("a = clsPart.Method(2)", scope);
            _pe.Execute("if -2 != a: raise AssertionError('test failed')", scope);

            // ===============================================

            // reset the same variable with integer
            scope.SetVariable(clspartName, 1);
            _pe.Execute("if 1 != clsPart: raise AssertionError('test failed')", scope);
            Assert.AreEqual((int)(object)scope.GetVariable(clspartName), 1);

            ScriptSource su = _pe.CreateScriptSourceFromString("");
            Assert.Throws<ArgumentNullException>(() => {
                su.Execute(null);
            });
        }

        [Test]
        public static void ScenarioTryGetMember() {
            var engine = Python.CreateEngine();
            var str = ClrModule.GetPythonType(typeof(string));
            object result;
            Assert.AreEqual(engine.Operations.TryGetMember(str, "Equals", out result), true);
            Assert.AreEqual(result.ToString(), "IronPython.Runtime.Types.BuiltinFunction");
        }

        [Test]
        public static void ScenarioComprehensionScope() {
            var engine = Python.CreateEngine();
            var scope = engine.CreateScope();
            var source = engine.CreateScriptSourceFromString("assert [lambda: i for i in [1,2]][0]() == 2");
            var compiledCode = source.Compile();
            compiledCode.Execute(scope);
        }

        [Test]
        public static void ScenarioComprehensionScopeGlobal() {
            var engine = Python.CreateEngine();
            var source = engine.CreateScriptSourceFromString("assert [lambda: i for i in global_list][0]() == res");
            var compiledCode = source.Compile();
            var scope = engine.CreateScope();
            scope.SetVariable("global_list", new[] { 1, 2 });
            scope.SetVariable("res", 2);
            compiledCode.Execute(scope);
        }

        [Test]
        public static void ScenarioInterfaceExtensions() {
            var engine = Python.CreateEngine();
            engine.Runtime.LoadAssembly(typeof(Fooable).Assembly);
            ScriptSource src = engine.CreateScriptSourceFromString("x.Bar()");
            ScriptScope scope = engine.CreateScope();
            scope.SetVariable("x", new Fooable());
            Assert.AreEqual((object)src.Execute(scope), "Bar Called");
        }

        private class MyInvokeMemberBinder : InvokeMemberBinder {
            public MyInvokeMemberBinder(string name, CallInfo callInfo)
                : base(name, false, callInfo) {
            }

            public override DynamicMetaObject FallbackInvokeMember(DynamicMetaObject target, DynamicMetaObject[] args, DynamicMetaObject errorSuggestion) {
                return errorSuggestion ?? new DynamicMetaObject(
                    Expression.Constant("FallbackInvokeMember"),
                    target.Restrictions.Merge(BindingRestrictions.Combine(args)).Merge(target.Restrict(target.LimitType).Restrictions)
                );
            }

            public override DynamicMetaObject FallbackInvoke(DynamicMetaObject target, DynamicMetaObject[] args, DynamicMetaObject errorSuggestion) {
                return new DynamicMetaObject(
                    Expression.Dynamic(new MyInvokeBinder(CallInfo), typeof(object), Microsoft.Scripting.Utils.DynamicUtils.GetExpressions(Microsoft.Scripting.Utils.ArrayUtils.Insert(target, args))),
                    target.Restrictions.Merge(BindingRestrictions.Combine(args))
                );
            }
        }

        private class MyInvokeBinder : InvokeBinder {
            public MyInvokeBinder(CallInfo callInfo)
                : base(callInfo) {
            }

            public override DynamicMetaObject FallbackInvoke(DynamicMetaObject target, DynamicMetaObject[] args, DynamicMetaObject errorSuggestion) {
                return new DynamicMetaObject(
                    Expression.Call(
                        typeof(String).GetMethod("Concat", new Type[] { typeof(object), typeof(object) }),
                        Expression.Constant("FallbackInvoke"),
                        target.Expression
                    ),
                    BindingRestrictionsHelpers.GetRuntimeTypeRestriction(target)
                );
            }
        }

        private class MyGetIndexBinder : GetIndexBinder {
            public MyGetIndexBinder(CallInfo args)
                : base(args) {
            }

            public override DynamicMetaObject FallbackGetIndex(DynamicMetaObject target, DynamicMetaObject[] indexes, DynamicMetaObject errorSuggestion) {
                return new DynamicMetaObject(
                    Expression.Call(
                        typeof(String).GetMethod("Concat", new Type[] { typeof(object), typeof(object) }),
                        Expression.Constant("FallbackGetIndex"),
                        indexes[0].Expression
                    ),
                    BindingRestrictionsHelpers.GetRuntimeTypeRestriction(target)
                );
            }
        }

        private class MySetIndexBinder : SetIndexBinder {
            public MySetIndexBinder(CallInfo args)
                : base(args) {
            }

            public override DynamicMetaObject FallbackSetIndex(DynamicMetaObject target, DynamicMetaObject[] indexes, DynamicMetaObject value, DynamicMetaObject errorSuggestion) {
                return new DynamicMetaObject(
                    Expression.Call(
                        typeof(String).GetMethod("Concat", new Type[] { typeof(object), typeof(object), typeof(object) }),
                        Expression.Constant("FallbackSetIndex"),
                        indexes[0].Expression,
                        value.Expression
                    ),
                    BindingRestrictionsHelpers.GetRuntimeTypeRestriction(target)
                );
            }
        }

        private class MyGetMemberBinder : GetMemberBinder {
            public MyGetMemberBinder(string name)
                : base(name, false) {
            }

            public override DynamicMetaObject FallbackGetMember(DynamicMetaObject target, DynamicMetaObject errorSuggestion) {
                return new DynamicMetaObject(
                    Expression.Constant("FallbackGetMember"),
                    BindingRestrictionsHelpers.GetRuntimeTypeRestriction(target)
                );
            }
        }

        private class MyInvokeBinder2 : InvokeBinder {
            public MyInvokeBinder2(CallInfo args)
                : base(args) {
            }

            public override DynamicMetaObject FallbackInvoke(DynamicMetaObject target, DynamicMetaObject[] args, DynamicMetaObject errorSuggestion) {
                Expression[] exprs = new Expression[args.Length + 1];
                exprs[0] = Expression.Constant("FallbackInvoke");
                for (int i = 0; i < args.Length; i++) {
                    exprs[i + 1] = args[i].Expression;
                }

                return new DynamicMetaObject(
                    Expression.Call(
                        typeof(String).GetMethod("Concat", new Type[] { typeof(object[]) }),
                        Expression.NewArrayInit(
                            typeof(object),
                            exprs
                        )
                    ),
                    BindingRestrictionsHelpers.GetRuntimeTypeRestriction(target)
                );
            }
        }

        private class MyConvertBinder : ConvertBinder {
            private object _result;
            public MyConvertBinder(Type type) : this(type, "Converted") {
            }
            public MyConvertBinder(Type type, object result)
                : base(type, true) {
                _result = result;
            }

            public override DynamicMetaObject FallbackConvert(DynamicMetaObject target, DynamicMetaObject errorSuggestion) {
                return new DynamicMetaObject(
                    Expression.Constant(_result),
                    BindingRestrictionsHelpers.GetRuntimeTypeRestriction(target)
                );

            }
        }

        private class MyUnaryBinder : UnaryOperationBinder {
            public MyUnaryBinder(ExpressionType et)
                : base(et) {
            }

            public override DynamicMetaObject FallbackUnaryOperation(DynamicMetaObject target, DynamicMetaObject errorSuggestion) {
                return new DynamicMetaObject(
                    Expression.Constant("UnaryFallback"),
                    BindingRestrictionsHelpers.GetRuntimeTypeRestriction(target)
                );
            }
        }

        private void TestTarget(object sender, EventArgs args) {
        }

        [Test]
        public void ScenarioDocumentation() {
            ScriptScope scope = _pe.CreateScope();
            ScriptSource src = _pe.CreateScriptSourceFromString(@"
import System
import clr

def f0(a, b): pass
def f1(a, *b): pass
def f2(a, **b): pass
def f3(a, *b, **c): pass

class C:
    m0 = f0
    m1 = f1
    m2 = f2
    m3 = f3
    def __init__(self):
        self.foo = 42

class SC(C): pass

inst = C()

class NC(object):
    m0 = f0
    m1 = f1
    m2 = f2
    m3 = f3
    def __init__(self):
        self.foo = 42

class SNC(NC): pass

ncinst = C()

class EmptyNC(object): pass

enc = EmptyNC()

m0 = C.m0
m1 = C.m1
m2 = C.m2
m3 = C.m3

i = int

", SourceCodeKind.File);

            var doc = _pe.GetService<DocumentationOperations>();
            src.Execute(scope);
            scope.SetVariable("dlg", new EventHandler(TestTarget));

            object f0 = scope.GetVariable("f0");
            object f1 = scope.GetVariable("f1");
            object f2 = scope.GetVariable("f2");
            object f3 = scope.GetVariable("f3");
            object dlg = scope.GetVariable("dlg");
            object m0 = scope.GetVariable("m0");
            object m1 = scope.GetVariable("m1");
            object m2 = scope.GetVariable("m2");
            object m3 = scope.GetVariable("m3");

            var tests = new [] {
                new {
                    Obj=f0,
                    Result = new [] {
                        new[] {
                            new { ParamName="a", ParamAttrs = ParameterFlags.None },
                            new { ParamName="b", ParamAttrs = ParameterFlags.None }
                        }
                    }
                },
                new {
                    Obj=f1,
                    Result = new [] {
                        new[] {
                            new { ParamName="a", ParamAttrs = ParameterFlags.None },
                            new { ParamName="b", ParamAttrs = ParameterFlags.ParamsArray }
                        }
                    }
                },
                new {
                    Obj=f2,
                    Result = new [] {
                        new[] {
                            new { ParamName="a", ParamAttrs = ParameterFlags.None },
                            new { ParamName="b", ParamAttrs = ParameterFlags.ParamsDict}
                        }
                    }
                },
                new {
                    Obj=f3,
                    Result = new [] {
                        new [] {
                            new { ParamName="a", ParamAttrs = ParameterFlags.None},
                            new { ParamName="b", ParamAttrs = ParameterFlags.ParamsArray},
                            new { ParamName="c", ParamAttrs = ParameterFlags.ParamsDict}
                        }
                    }
                },
                new {
                    Obj=dlg,
                    Result = new [] {
                        new [] {
                            new { ParamName="sender", ParamAttrs = ParameterFlags.None},
                            new { ParamName="e", ParamAttrs = ParameterFlags.None},
                        }
                    }
                },
                new {
                    Obj=m0,
                    Result = new [] {
                        new[] {
                            new { ParamName="a", ParamAttrs = ParameterFlags.None },
                            new { ParamName="b", ParamAttrs = ParameterFlags.None }
                        }
                    }
                },
                new {
                    Obj=m1,
                    Result = new [] {
                        new[] {
                            new { ParamName="a", ParamAttrs = ParameterFlags.None },
                            new { ParamName="b", ParamAttrs = ParameterFlags.ParamsArray }
                        }
                    }
                },
                new {
                    Obj=m2,
                    Result = new [] {
                        new[] {
                            new { ParamName="a", ParamAttrs = ParameterFlags.None },
                            new { ParamName="b", ParamAttrs = ParameterFlags.ParamsDict}
                        }
                    }
                },
                new {
                    Obj=m3,
                    Result = new [] {
                        new [] {
                            new { ParamName="a", ParamAttrs = ParameterFlags.None},
                            new { ParamName="b", ParamAttrs = ParameterFlags.ParamsArray},
                            new { ParamName="c", ParamAttrs = ParameterFlags.ParamsDict}
                        }
                    }
                },

            };

            foreach (var test in tests) {
                var result = new List<OverloadDoc>(doc.GetOverloads(test.Obj));
                Assert.AreEqual(result.Count, test.Result.Length);

                for (int i = 0; i < result.Count; i++) {
                    var received = result[i]; ;
                    var expected = test.Result[i];
                    Assert.AreEqual(received.Parameters.Count, expected.Length);
                    var recvParams = new List<ParameterDoc>(received.Parameters);

                    for (int j = 0; j < expected.Length; j++) {
                        var receivedParam = recvParams[j];
                        var expectedParam = expected[j];

                        Assert.AreEqual(receivedParam.Flags, expectedParam.ParamAttrs);
                        Assert.AreEqual(receivedParam.Name, expectedParam.ParamName);
                    }
                }
            }

            object inst = scope.GetVariable("inst");
            object ncinst = scope.GetVariable("ncinst");
            object klass = scope.GetVariable("C");
            object newklass = scope.GetVariable("NC");
            object subklass = scope.GetVariable("SC");
            object subnewklass = scope.GetVariable("SNC");
            object System = scope.GetVariable("System");
            object clr = scope.GetVariable("clr");

            foreach (object o in new[] { inst, ncinst }) {
                var members = doc.GetMembers(o);
                ContainsMemberName(members, "m0", MemberKind.Method);
                ContainsMemberName(members, "foo", MemberKind.None);
            }

            ContainsMemberName(doc.GetMembers(klass), "m0", MemberKind.Method);
            ContainsMemberName(doc.GetMembers(newklass), "m0", MemberKind.Method);
            ContainsMemberName(doc.GetMembers(subklass), "m0", MemberKind.Method);
            ContainsMemberName(doc.GetMembers(subnewklass), "m0", MemberKind.Method);
            ContainsMemberName(doc.GetMembers(System), "Collections", MemberKind.Namespace);
            ContainsMemberName(doc.GetMembers(clr), "AddReference", MemberKind.Function);

            object intType = scope.GetVariable("i");
            foreach (object o in new object[] { intType, 42 }) {
                var members = doc.GetMembers(o);
                ContainsMemberName(members, "__add__", MemberKind.Method);
                ContainsMemberName(members, "conjugate", MemberKind.Method);
                ContainsMemberName(members, "real", MemberKind.Property);
            }

            ContainsMemberName(doc.GetMembers(new List<object>()), "Count", MemberKind.Property);
            ContainsMemberName(doc.GetMembers(DynamicHelpers.GetPythonTypeFromType(typeof(DateTime))), "MaxValue", MemberKind.Field);

            doc.GetMembers(scope.GetVariable("enc"));
        }

        private void ContainsMemberName(ICollection<MemberDoc> members, string name, MemberKind kind) {
            foreach (var member in members) {
                if (member.Name == name) {
                    Assert.AreEqual(member.Kind, kind);
                    return;
                }
            }

            Assert.Fail("didn't find member " + name);
        }

        [Test]
        public void ScenarioDlrInterop() {
            string actionOfT = typeof(Action<>).FullName.Split('`')[0];

            ScriptScope scope = _env.CreateScope();
            ScriptSource src = _pe.CreateScriptSourceFromString(@"
import clr
if clr.IsNetCoreApp:
    clr.AddReference('System.Collections.NonGeneric')
elif not clr.IsMono:
    clr.AddReference('System.Windows.Forms')
    from System.Windows.Forms import Control

import System
from System.Collections import ArrayList

long = type(1<<64)

somecallable = " + actionOfT + @"[object](lambda : 'Delegate')

if not clr.IsNetCoreApp and not clr.IsMono:
    class control(Control):
        pass

    class control_setattr(Control):
        def __init__(self):
            object.__setattr__(self, 'lastset', None)

        def __setattr__(self, name, value):
            object.__setattr__(self, 'lastset', (name, value))

    class control_override_prop(Control):
        def __setattr__(self, name, value):
            pass

        def get_AllowDrop(self):
            return 'abc'

        def set_AllowDrop(self, value):
            super(control_setattr, self).AllowDrop.SetValue(value)

class ns(object):
    ClassVal = 'ClassVal'

    def __init__(self):
        self.InstVal = 'InstVal'
        self.InstCallable = somecallable
        self.LastSetItem = None

    def __add__(self, other):
        return 'add' + str(other)

    def TestFunc(self):
        return 'TestFunc'

    def ToString(self):
        return 'MyToString'

    def NsMethod(self, *args, **kwargs):
        return args, kwargs

    @staticmethod
    def StaticMethod():
        return 'Static'

    @classmethod
    def StaticMethod(cls):
        return cls

    def __call__(self, *args, **kwargs):
        return args, kwargs

    def __int__(self): return 42
    def __float__(self): return 42.0
    def __str__(self): return 'Python'
    def __long__(self): return long(42)
    def __complex__(self): return 42j
    def __bool__(self): return False

    def __getitem__(self, index):
        return index

    def __setitem__(self, index, value):
        self.LastSetItem = (index, value)

    SomeDelegate = somecallable

class ns_getattr(object):
    ClassVal = 'ClassVal'

    def __init__(self):
        self.InstVal = 'InstVal'

    def TestFunc(self):
        return 'TestFunc'

    def __getattr__(self, name):
        if name == 'SomeDelegate':
            return somecallable
        elif name == 'something':
            return 'getattrsomething'
        return name

class ns_getattribute(object):
    ClassVal = 'ClassVal'

    def __init__(self):
        self.InstVal = 'InstVal'

    def TestFunc(self):
        return 'TestFunc'

    def __getattribute__(self, name):
        if name == 'SomeDelegate':
            return somecallable
        return name

class MyArrayList(ArrayList):
    ClassVal = 'ClassVal'

    def __init__(self):
        self.InstVal = 'InstVal'

    def TestFunc(self):
        return 'TestFunc'


class MyArrayList_getattr(ArrayList):
    ClassVal = 'ClassVal'

    def __init__(self):
        self.InstVal = 'InstVal'

    def TestFunc(self):
        return 'TestFunc'

    def __getattr__(self, name):
        return name

class MyArrayList_getattribute(ArrayList):
    ClassVal = 'ClassVal'

    def __init__(self):
        self.InstVal = 'InstVal'

    def TestFunc(self):
        return 'TestFunc'

    def __getattribute__(self, name):
        return name

class IterableObject(object):
    def __iter__(self):
        yield 1
        yield 2
        yield 3

class IterableObjectOs:
    def __iter__(self):
        yield 1
        yield 2
        yield 3

class os:
    ClassVal = 'ClassVal'

    def __init__(self):
        self.InstVal = 'InstVal'
        self.InstCallable = somecallable
        self.LastSetItem = None

    def TestFunc(self):
        return 'TestFunc'

    def __call__(self, *args, **kwargs):
        return args, kwargs

    def __int__(self): return 42
    def __float__(self): return 42.0
    def __str__(self): return 'Python'
    def __long__(self): return long(42)
    def __bool__(self): return False
    def __complex__(self): return 42j

    def __getitem__(self, index):
        return index

    def __setitem__(self, index, value):
        self.LastSetItem = (index, value)

    SomeDelegate = somecallable

class plain_os:
    pass

class plain_ns(object): pass

class os_getattr:
    ClassVal = 'ClassVal'

    def __init__(self):
        self.InstVal = 'InstVal'

    def __getattr__(self, name):
        if name == 'SomeDelegate':
            return somecallable
        return name

    def TestFunc(self):
        return 'TestFunc'

class ns_nonzero(object):
    def __bool__(self):
        return True

ns_nonzero_inst = ns_nonzero()

class ns_len1(object):
    def __len__(self): return 1

ns_len1_inst = ns_len1()

class ns_len0(object):
    def __len__(self): return 0

ns_len0_inst = ns_len0()

def TestFunc():
    return 'TestFunc'

TestFunc.SubFunc = TestFunc

def Invokable(*args, **kwargs):
    return args, kwargs

TestFunc.TestFunc = TestFunc
TestFunc.InstVal = 'InstVal'
TestFunc.ClassVal = 'ClassVal'  # just here to simplify tests

if not clr.IsNetCoreApp and not clr.IsMono:
    controlinst = control()
nsinst = ns()
iterable = IterableObject()
iterableos = IterableObjectOs()
plainnsinst = plain_ns()
nsmethod = nsinst.NsMethod
alinst = MyArrayList()
osinst = os()
plainosinst = plain_os()
os_getattrinst = os_getattr()
ns_getattrinst = ns_getattr()
al_getattrinst = MyArrayList_getattr()

ns_getattributeinst = ns_getattribute()
al_getattributeinst = MyArrayList_getattribute()

range = range
", SourceCodeKind.Statements);

            src.Execute(scope);

            // InvokeMember tests

            var allObjects = new object[] { scope.GetVariable("nsinst"), scope.GetVariable("osinst"), scope.GetVariable("alinst"), scope.GetVariable("TestFunc") };
            var getattrObjects = new object[] { scope.GetVariable("ns_getattrinst"), scope.GetVariable("os_getattrinst"), scope.GetVariable("al_getattrinst") };
            var getattributeObjects = new object[] { scope.GetVariable("ns_getattributeinst"), scope.GetVariable("al_getattributeinst") };
            var indexableObjects = new object[] { scope.GetVariable("nsinst"), scope.GetVariable("osinst") };
            var unindexableObjects = new object[] { scope.GetVariable("TestFunc"), scope.GetVariable("ns_getattrinst"), scope.GetVariable("somecallable") }; // scope.GetVariable("plainosinst"),
            var invokableObjects = new object[] { scope.GetVariable("Invokable"), scope.GetVariable("nsinst"), scope.GetVariable("osinst"), scope.GetVariable("nsmethod"), };
            var convertableObjects = new object[] { scope.GetVariable("nsinst"), scope.GetVariable("osinst") };
            var unconvertableObjects = new object[] { scope.GetVariable("plainnsinst"), scope.GetVariable("plainosinst") };
            var iterableObjects = new object[] { scope.GetVariable("iterable"), scope.GetVariable("iterableos") };

            // if it lives on a system type we should do a fallback invoke member
            var site = CallSite<Func<CallSite, object, object>>.Create(new MyInvokeMemberBinder("Count", new CallInfo(0)));
            Assert.AreEqual(site.Target(site, (object)scope.GetVariable("alinst")), "FallbackInvokeMember");

            // invoke a function that's a member on an object
            foreach (object inst in allObjects) {
                site = CallSite<Func<CallSite, object, object>>.Create(new MyInvokeMemberBinder("TestFunc", new CallInfo(0)));
                Assert.AreEqual(site.Target(site, inst), "TestFunc");
            }

            // invoke a field / property that's on an object
            foreach (object inst in allObjects) {
                site = CallSite<Func<CallSite, object, object>>.Create(new MyInvokeMemberBinder("InstVal", new CallInfo(0)));
                Assert.AreEqual(site.Target(site, inst), "FallbackInvokeInstVal");

                site = CallSite<Func<CallSite, object, object>>.Create(new MyInvokeMemberBinder("ClassVal", new CallInfo(0)));
                Assert.AreEqual(site.Target(site, inst), "FallbackInvokeClassVal");


                if (!(inst is PythonFunction)) {
                    site = CallSite<Func<CallSite, object, object>>.Create(new MyInvokeMemberBinder("SomeMethodThatNeverExists", new CallInfo(0)));
                    Assert.AreEqual(site.Target(site, inst), "FallbackInvokeMember");
                }
            }

            // invoke a field / property that's not defined on objects w/ __getattr__
            foreach (object inst in getattrObjects) {
                site = CallSite<Func<CallSite, object, object>>.Create(new MyInvokeMemberBinder("DoesNotExist", new CallInfo(0)));
                Assert.AreEqual(site.Target(site, inst), "FallbackInvokeDoesNotExist");
            }

            // invoke a field / property that's not defined on objects w/ __getattribute__
            foreach (object inst in getattributeObjects) {
                site = CallSite<Func<CallSite, object, object>>.Create(new MyInvokeMemberBinder("DoesNotExist", new CallInfo(0)));
                Assert.AreEqual(site.Target(site, inst), "FallbackInvokeDoesNotExist");

                site = CallSite<Func<CallSite, object, object>>.Create(new MyInvokeMemberBinder("Count", new CallInfo(0)));
                Assert.AreEqual(site.Target(site, inst), "FallbackInvokeCount");

                site = CallSite<Func<CallSite, object, object>>.Create(new MyInvokeMemberBinder("TestFunc", new CallInfo(0)));
                Assert.AreEqual(site.Target(site, inst), "FallbackInvokeTestFunc");

                site = CallSite<Func<CallSite, object, object>>.Create(new MyInvokeMemberBinder("InstVal", new CallInfo(0)));
                Assert.AreEqual(site.Target(site, inst), "FallbackInvokeInstVal");

                site = CallSite<Func<CallSite, object, object>>.Create(new MyInvokeMemberBinder("ClassVal", new CallInfo(0)));
                Assert.AreEqual(site.Target(site, inst), "FallbackInvokeClassVal");
            }

            foreach (object inst in indexableObjects) {
                var site2 = CallSite<Func<CallSite, object, object, object>>.Create(new MyGetIndexBinder(new CallInfo(1)));
                Assert.AreEqual(site2.Target(site2, inst, "index"), "index");

                var site3 = CallSite<Func<CallSite, object, object, object, object>>.Create(new MySetIndexBinder(new CallInfo(1)));
                Assert.AreEqual(site3.Target(site3, inst, "index", "value"), "value");

                site = CallSite<Func<CallSite, object, object>>.Create(new MyGetMemberBinder("LastSetItem"));
                IList<object> res = (IList<object>)site.Target(site, inst);
                Assert.AreEqual(res.Count, 2);
                Assert.AreEqual(res[0], "index");
                Assert.AreEqual(res[1], "value");
            }

            foreach (object inst in unindexableObjects) {
                var site2 = CallSite<Func<CallSite, object, object, object>>.Create(new MyGetIndexBinder(new CallInfo(1)));
                //Console.WriteLine(inst);
                Assert.AreEqual(site2.Target(site2, inst, "index"), "FallbackGetIndexindex");

                var site3 = CallSite<Func<CallSite, object, object, object, object>>.Create(new MySetIndexBinder(new CallInfo(1)));
                Assert.AreEqual(site3.Target(site3, inst, "index", "value"), "FallbackSetIndexindexvalue");
            }

            foreach (object inst in invokableObjects) {
                var site2 = CallSite<Func<CallSite, object, object, object>>.Create(new MyInvokeBinder2(new CallInfo(1)));
                VerifyFunction(new[] { "foo"}, new string[0], site2.Target(site2, inst, "foo"));

                site2 = CallSite<Func<CallSite, object, object, object>>.Create(new MyInvokeBinder2(new CallInfo(1, "bar")));
                VerifyFunction(new[] { "foo" }, new[] { "bar" }, site2.Target(site2, inst, "foo"));

                var site3 = CallSite<Func<CallSite, object, object, object, object>>.Create(new MyInvokeBinder2(new CallInfo(2)));
                VerifyFunction(new[] { "foo", "bar" }, new string[0], site3.Target(site3, inst, "foo", "bar"));

                site3 = CallSite<Func<CallSite, object, object, object, object>>.Create(new MyInvokeBinder2(new CallInfo(2, "bar")));
                VerifyFunction(new[] { "foo", "bar" }, new[] { "bar" }, site3.Target(site3, inst, "foo", "bar"));

                site3 = CallSite<Func<CallSite, object, object, object, object>>.Create(new MyInvokeBinder2(new CallInfo(2, "foo", "bar")));
                VerifyFunction(new[] { "foo", "bar" }, new[] { "foo", "bar" }, site3.Target(site3, inst, "foo", "bar"));
            }

            foreach (object inst in convertableObjects) {
                // These may be invalid according to the DLR (wrong ret type) but currently work today.
                site = CallSite<Func<CallSite, object, object>>.Create(new MyConvertBinder(typeof(string)));
                Assert.AreEqual(site.Target(site, inst), "Python");

                var dlgsiteo = CallSite<Func<CallSite, object, object>>.Create(new MyConvertBinder(typeof(Func<object, object>), null));
                VerifyFunction(new[] { "foo" }, new string[0], ((Func<object, object>)(dlgsiteo.Target(dlgsiteo, inst)))("foo"));

                var dlgsite2o = CallSite<Func<CallSite, object, object>>.Create(new MyConvertBinder(typeof(Func<object, object, object>), null));
                VerifyFunction(new[] { "foo", "bar" }, new string[0], ((Func<object, object, object>)dlgsite2o.Target(dlgsite2o, inst))("foo", "bar"));

                // strongly typed return versions
                var ssite = CallSite<Func<CallSite, object, string>>.Create(new MyConvertBinder(typeof(string)));
                Assert.AreEqual(ssite.Target(ssite, inst), "Python");

                var isite = CallSite<Func<CallSite, object, int>>.Create(new MyConvertBinder(typeof(int), 23));
                Assert.AreEqual(isite.Target(isite, inst), 42);

                var dsite = CallSite<Func<CallSite, object, double>>.Create(new MyConvertBinder(typeof(double), 23.0));
                Assert.AreEqual(dsite.Target(dsite, inst), 42.0);

                var csite = CallSite<Func<CallSite, object, Complex>>.Create(new MyConvertBinder(typeof(Complex), new Complex(0, 23)));
                Assert.AreEqual(csite.Target(csite, inst), new Complex(0, 42));

                var bsite = CallSite<Func<CallSite, object, bool>>.Create(new MyConvertBinder(typeof(bool), true));
                Assert.AreEqual(bsite.Target(bsite, inst), false);

                var bisite = CallSite<Func<CallSite, object, BigInteger>>.Create(new MyConvertBinder(typeof(BigInteger), (BigInteger)23));
                Assert.AreEqual(bisite.Target(bisite, inst), (BigInteger)42);

                var dlgsite = CallSite<Func<CallSite, object, Func<object, object>>>.Create(new MyConvertBinder(typeof(Func<object, object>), null));
                VerifyFunction(new[] { "foo" }, new string[0], dlgsite.Target(dlgsite, inst)("foo"));

                var dlgsite2 = CallSite<Func<CallSite, object, Func<object, object, object>>>.Create(new MyConvertBinder(typeof(Func<object, object, object>), null));
                VerifyFunction(new[] { "foo", "bar" }, new string[0], dlgsite2.Target(dlgsite2, inst)("foo", "bar"));
            }

            foreach (object inst in unconvertableObjects) {
                // These may be invalid according to the DLR (wrong ret type) but currently work today.
                site = CallSite<Func<CallSite, object, object>>.Create(new MyConvertBinder(typeof(string)));
                Assert.AreEqual(site.Target(site, inst), "Converted");

                // strongly typed return versions
                var ssite = CallSite<Func<CallSite, object, string>>.Create(new MyConvertBinder(typeof(string)));
                Assert.AreEqual(ssite.Target(ssite, inst), "Converted");

                var isite = CallSite<Func<CallSite, object, int>>.Create(new MyConvertBinder(typeof(int), 23));
                Assert.AreEqual(isite.Target(isite, inst), 23);

                var dsite = CallSite<Func<CallSite, object, double>>.Create(new MyConvertBinder(typeof(double), 23.0));
                Assert.AreEqual(dsite.Target(dsite, inst), 23.0);

                var csite = CallSite<Func<CallSite, object, Complex>>.Create(new MyConvertBinder(typeof(Complex), new Complex(0, 23.0)));
                Assert.AreEqual(csite.Target(csite, inst), new Complex(0, 23.0));

                var bsite = CallSite<Func<CallSite, object, bool>>.Create(new MyConvertBinder(typeof(bool), true));
                Assert.AreEqual(bsite.Target(bsite, inst), true);

                var bisite = CallSite<Func<CallSite, object, BigInteger>>.Create(new MyConvertBinder(typeof(BigInteger), (BigInteger)23));
                Assert.AreEqual(bisite.Target(bisite, inst), (BigInteger)23);
            }

            // get on .NET member should fallback

            if (!ClrModule.IsMono && !ClrModule.IsNetCoreApp) {
                // property
                site = CallSite<Func<CallSite, object, object>>.Create(new MyGetMemberBinder("AllowDrop"));
                Assert.AreEqual(site.Target(site, (object)scope.GetVariable("controlinst")), "FallbackGetMember");

                // method
                site = CallSite<Func<CallSite, object, object>>.Create(new MyGetMemberBinder("BringToFront"));
                Assert.AreEqual(site.Target(site, (object)scope.GetVariable("controlinst")), "FallbackGetMember");

                // protected method
                site = CallSite<Func<CallSite, object, object>>.Create(new MyGetMemberBinder("OnParentChanged"));
                Assert.AreEqual(site.Target(site, (object)scope.GetVariable("controlinst")), "FallbackGetMember");

                // event
                site = CallSite<Func<CallSite, object, object>>.Create(new MyGetMemberBinder("DoubleClick"));
                Assert.AreEqual(site.Target(site, (object)scope.GetVariable("controlinst")), "FallbackGetMember");
            }

            site = CallSite<Func<CallSite, object, object>>.Create(new MyInvokeMemberBinder("something", new CallInfo(0)));
            Assert.AreEqual(site.Target(site, (object)scope.GetVariable("ns_getattrinst")), "FallbackInvokegetattrsomething");

            foreach (object inst in iterableObjects) {
                // converting a type which implements __iter__
                var enumsite = CallSite<Func<CallSite, object, IEnumerable>>.Create(new MyConvertBinder(typeof(IEnumerable)));
                IEnumerable ie = enumsite.Target(enumsite, inst);
                IEnumerator ator = ie.GetEnumerator();
                Assert.AreEqual(ator.MoveNext(), true);
                Assert.AreEqual(ator.Current, 1);
                Assert.AreEqual(ator.MoveNext(), true);
                Assert.AreEqual(ator.Current, 2);
                Assert.AreEqual(ator.MoveNext(), true);
                Assert.AreEqual(ator.Current, 3);
                Assert.AreEqual(ator.MoveNext(), false);

                var enumobjsite = CallSite<Func<CallSite, object, object>>.Create(new MyConvertBinder(typeof(IEnumerable)));
                ie = (IEnumerable)enumobjsite.Target(enumobjsite, inst);
                ator = ie.GetEnumerator();
                Assert.AreEqual(ator.MoveNext(), true);
                Assert.AreEqual(ator.Current, 1);
                Assert.AreEqual(ator.MoveNext(), true);
                Assert.AreEqual(ator.Current, 2);
                Assert.AreEqual(ator.MoveNext(), true);
                Assert.AreEqual(ator.Current, 3);
                Assert.AreEqual(ator.MoveNext(), false);

                var enumatorsite = CallSite<Func<CallSite, object, IEnumerator>>.Create(new MyConvertBinder(typeof(IEnumerator)));
                ator = enumatorsite.Target(enumatorsite, inst);
                Assert.AreEqual(ator.MoveNext(), true);
                Assert.AreEqual(ator.Current, 1);
                Assert.AreEqual(ator.MoveNext(), true);
                Assert.AreEqual(ator.Current, 2);
                Assert.AreEqual(ator.MoveNext(), true);
                Assert.AreEqual(ator.Current, 3);
                Assert.AreEqual(ator.MoveNext(), false);

                var enumatorobjsite = CallSite<Func<CallSite, object, object>>.Create(new MyConvertBinder(typeof(IEnumerator)));
                ator = (IEnumerator)enumatorobjsite.Target(enumatorobjsite, inst);
                Assert.AreEqual(ator.MoveNext(), true);
                Assert.AreEqual(ator.Current, 1);
                Assert.AreEqual(ator.MoveNext(), true);
                Assert.AreEqual(ator.Current, 2);
                Assert.AreEqual(ator.MoveNext(), true);
                Assert.AreEqual(ator.Current, 3);
                Assert.AreEqual(ator.MoveNext(), false);
            }

            site = CallSite<Func<CallSite, object, object>>.Create(new MyUnaryBinder(ExpressionType.Not));
            Assert.AreEqual(site.Target(site, (object)scope.GetVariable("nsinst")), true);
            Assert.AreEqual(site.Target(site, (object)scope.GetVariable("ns_nonzero_inst")), false);
            Assert.AreEqual(site.Target(site, (object)scope.GetVariable("ns_len0_inst")), true);
            Assert.AreEqual(site.Target(site, (object)scope.GetVariable("ns_len1_inst")), false);

            site = CallSite<Func<CallSite, object, object>>.Create(new MyInvokeMemberBinder("ToString", new CallInfo(0)));
            Assert.AreEqual(site.Target(site, (object)scope.GetVariable("range")), "FallbackInvokeMember");

            // invoke a function defined as a member of a function
            site = CallSite<Func<CallSite, object, object>>.Create(new MyInvokeMemberBinder("SubFunc", new CallInfo(0)));
            Assert.AreEqual(site.Target(site, (object)scope.GetVariable("TestFunc")), "TestFunc");

            site = CallSite<Func<CallSite, object, object>>.Create(new MyInvokeMemberBinder("DoesNotExist", new CallInfo(0)));
            Assert.AreEqual(site.Target(site, (object)scope.GetVariable("TestFunc")), "FallbackInvokeMember");
        }

        private class MyDynamicObject : DynamicObject {
            public object Last;

            public override bool TryBinaryOperation(BinaryOperationBinder binder, object arg, out object result) {
                result = binder.Operation;
                return true;
            }

            public override bool TryUnaryOperation(UnaryOperationBinder binder, out object result) {
                result = binder.Operation;
                return true;
            }

            public override bool TryGetMember(GetMemberBinder binder, out object result) {
                result = binder.Name;
                return true;
            }

            public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object result) {
                result = indexes[0];
                return true;
            }

            public override bool TryDeleteMember(DeleteMemberBinder binder) {
                Last = binder.Name;
                return true;
            }

            public override bool TryDeleteIndex(DeleteIndexBinder binder, object[] indexes) {
                Last = indexes[0];
                return true;
            }

            public override bool TrySetIndex(SetIndexBinder binder, object[] indexes, object value) {
                Last = indexes[0];
                return true;
            }

            public override bool TrySetMember(SetMemberBinder binder, object value) {
                Last = value;
                return true;
            }

            public override bool TryInvoke(InvokeBinder binder, object[] args, out object result) {
                result = binder.CallInfo;
                return true;
            }

            public override bool TryConvert(ConvertBinder binder, out object result) {
                result = 1000;
                return true;
            }
        }

        [Test]
        public void ScenarioDlrInterop_ConsumeDynamicObject() {
            var scope = _pe.CreateScope();
            var dynObj = new MyDynamicObject();
            scope.SetVariable("x", dynObj);
            _pe.Execute("import clr", scope);
            var tests = new[] {
                new {TestCase = "clr.Convert(x, int)", Result=(object)1000},

                new {TestCase = "x(2,3,4)", Result=(object)new CallInfo(3)},
                new {TestCase = "x(2,3,4, z = 4)", Result=(object)new CallInfo(4, "z")},

                new {TestCase = "not x", Result=(object)ExpressionType.Not},
                new {TestCase = "+x", Result=(object)ExpressionType.UnaryPlus},
                new {TestCase = "-x", Result=(object)ExpressionType.Negate},
                new {TestCase = "~x", Result=(object)ExpressionType.OnesComplement},

                new {TestCase = "x + 42", Result=(object)ExpressionType.Add},
                new {TestCase = "x - 42", Result=(object)ExpressionType.Subtract},
                new {TestCase = "x / 42", Result=(object)ExpressionType.Divide},
                new {TestCase = "x * 42", Result=(object)ExpressionType.Multiply},
                new {TestCase = "x % 42", Result=(object)ExpressionType.Modulo},
                new {TestCase = "x ^ 42", Result=(object)ExpressionType.ExclusiveOr},
                new {TestCase = "x << 42", Result=(object)ExpressionType.LeftShift},
                new {TestCase = "x >> 42", Result=(object)ExpressionType.RightShift},
                new {TestCase = "x | 42", Result=(object)ExpressionType.Or},
                new {TestCase = "x & 42", Result=(object)ExpressionType.And},
                new {TestCase = "x ** 42", Result=(object)ExpressionType.Power},

                new {TestCase = "x > 42", Result=(object)ExpressionType.GreaterThan},
                new {TestCase = "x < 42", Result=(object)ExpressionType.LessThan},
                new {TestCase = "x >= 42", Result=(object)ExpressionType.GreaterThanOrEqual},
                new {TestCase = "x <= 42", Result=(object)ExpressionType.LessThanOrEqual},
                new {TestCase = "x == 42", Result=(object)ExpressionType.Equal},
                new {TestCase = "x != 42", Result=(object)ExpressionType.NotEqual},

                new {TestCase = "x.abc", Result=(object)"abc"},
                new {TestCase = "x.foo", Result=(object)"foo"},

                new {TestCase = "x[0]", Result=(object)0},
                new {TestCase = "x[1]", Result=(object)1},

            };
            foreach(var test in tests) {
                Assert.AreEqual(test.Result, (object)_pe.Execute(test.TestCase, scope));
            }

            var tests2 = new[] {
                new {TestCase = "x.foo = 42", Last=(object)42},
                new {TestCase = "x[23] = 5", Last=(object)23},
                new {TestCase = "del x[5]", Last=(object)5},
                new {TestCase = "del x.abc", Last=(object)"abc"},
            };

            foreach (var test in tests2) {
                _pe.Execute(test.TestCase, scope);
                Assert.AreEqual(test.Last, dynObj.Last);
            }
        }

        private void VerifyFunction(object[] results, string[] names, object value) {
            IList<object> res = (IList<object>)value;
            Assert.AreEqual(res.Count, 2);
            IList<object> positional = (IList<object>)res[0];
            IDictionary<object, object> kwargs = (IDictionary<object, object>)res[1];

            for (int i = 0; i < positional.Count; i++) {
                Assert.AreEqual(positional[i], results[i]);
            }

            for (int i = positional.Count; i < results.Length; i++) {
                Assert.AreEqual(kwargs[names[i - positional.Count]], results[i]);
            }

        }

        [Test]
        public void ScenarioEvaluateInAnonymousEngineModule() {
            ScriptScope scope1 = _env.CreateScope();
            ScriptScope scope2 = _env.CreateScope();
            ScriptScope scope3 = _env.CreateScope();

            _pe.Execute("x = 0", scope1);
            _pe.Execute("x = 1", scope2);

            scope3.SetVariable("x", 2);

            Assert.AreEqual(0, _pe.Execute<int>("x", scope1));
            Assert.AreEqual(0, (int)(object)scope1.GetVariable("x"));

            Assert.AreEqual(1, _pe.Execute<int>("x", scope2));
            Assert.AreEqual(1, (int)(object)scope2.GetVariable("x"));

            Assert.AreEqual(2, _pe.Execute<int>("x", scope3));
            Assert.AreEqual(2, (int)(object)scope3.GetVariable("x"));
        }

        [Test]
        public void ScenarioObjectOperations() {
            var ops = _pe.Operations;
            Assert.AreEqual("(1, 2, 3)", ops.Format(new PythonTuple(new object[] { 1, 2, 3 })));

            var scope = _pe.CreateScope();
            scope.SetVariable("ops", ops);
            Assert.AreEqual("[1, 2, 3]", _pe.Execute<string>("ops.Format([1,2,3])", scope));

            ScriptSource src = _pe.CreateScriptSourceFromString("def f(*args): return args", SourceCodeKind.Statements);
            src.Execute(scope);
            object f = (object)scope.GetVariable("f");

            for (int i = 0; i < 20; i++) {
                object[] inp = new object[i];
                for (int j = 0; j < inp.Length; j++) {
                    inp[j] = j;
                }

                Assert.AreEqual((object)ops.Invoke(f, inp), PythonTuple.MakeTuple(inp));
            }

            ScriptScope mod = _env.CreateScope();

            _pe.CreateScriptSourceFromString(@"
class foo(object):
    abc = 3
    def __init__(self, x, y):
        self.x = x
        self.y = y
", SourceCodeKind.Statements).Execute(mod);

            object klass = mod.GetVariable("foo");

            // create instance of foo, verify members

            object foo = ops.CreateInstance(klass , 123, 444);

            Assert.AreEqual(ops.GetMember<int>(foo, "abc"), 3);
            Assert.AreEqual(ops.GetMember<int>(foo, "x"), 123);
            Assert.AreEqual(ops.GetMember<int>(foo, "y"), 444);
        }

        [Test]
        public void ScenarioCP712() {
            ScriptScope scope1 = _env.CreateScope();
            _pe.CreateScriptSourceFromString("max(3, 4)", SourceCodeKind.InteractiveCode).Execute(scope1);
            //CompiledCode compiledCode = _pe.CreateScriptSourceFromString("max(3,4)", SourceCodeKind.InteractiveCode).Compile();
            //compiledCode.Execute(scope1);
            // Assert.AreEqual(4, scope1.GetVariable<int>("__builtins__._"));
            //TODO - this currently fails.
            // Assert.AreEqual(4, scope1.GetVariable<int>("_"));
        }

        [Test]
        public void ScenarioCP24784() {
            var code = _pe.CreateScriptSourceFromString("def f():\r\n \r\n print(1)", SourceCodeKind.InteractiveCode);

            Assert.AreEqual(code.GetCodeProperties(), ScriptCodeParseResult.IncompleteStatement);
        }

        public delegate int CP19724Delegate(double p1);

        [Test]
        public void ScenarioCP19724()
        {
            ScriptScope scope1 = _env.CreateScope();
            ScriptSource src = _pe.CreateScriptSourceFromString(@"
class KNew(object):
    def __call__(self, p1):
        global X
        X = 42
        return 7

k = KNew()", SourceCodeKind.Statements);
            src.Execute(scope1);

            CP19724Delegate tDelegate = scope1.GetVariable<CP19724Delegate>("k");
            Assert.AreEqual(7, tDelegate(3.14));
            Assert.AreEqual(42, scope1.GetVariable<int>("X"));
        }

        [Test]
        public void ScenarioEvaluateInPublishedEngineModule() {
            PythonContext pc = DefaultContext.DefaultPythonContext;

            PythonModule publishedModule = new PythonModule();
            PythonModule otherModule = new PythonModule();
            pc.PublishModule("published_context_test", publishedModule);

            pc.CreateSnippet("x = 0", SourceCodeKind.Statements).Execute(otherModule.Scope);
            pc.CreateSnippet("x = 1", SourceCodeKind.Statements).Execute(publishedModule.Scope);

            object x;

            // Ensure that the default EngineModule is not affected
            x = pc.CreateSnippet("x", SourceCodeKind.Expression).Execute(otherModule.Scope);
            Assert.AreEqual(0, (int)x);
            // Ensure that the published context has been updated as expected
            x = pc.CreateSnippet("x", SourceCodeKind.Expression).Execute(publishedModule.Scope);
            Assert.AreEqual(1, (int)x);

            // Ensure that the published context is accessible from other contexts using sys.modules
            // TODO: do better:
            // pe.Import("sys", ScriptDomainManager.CurrentManager.DefaultModule);
            pc.CreateSnippet("from published_context_test import x", SourceCodeKind.Statements).Execute(otherModule.Scope);
            x = pc.CreateSnippet("x", SourceCodeKind.Expression).Execute(otherModule.Scope);
            Assert.AreEqual(1, (int)x);
        }

        private class CustomDictionary : IDictionary<string, object> {
            // Make "customSymbol" always be accessible. This could have been accomplished just by
            // doing SetGlobal. However, this mechanism could be used for other extensibility
            // purposes like getting a callback whenever the symbol is read
            internal static readonly string customSymbol = "customSymbol";
            internal const int customSymbolValue = 100;

            private Dictionary<string, object> dict = new Dictionary<string, object>();

            #region IDictionary<string,object> Members

            public void Add(string key, object value) {
                if (key.Equals(customSymbol))
                    throw new UnboundNameException("Cannot assign to customSymbol");
                dict.Add(key, value);
            }

            public bool ContainsKey(string key) {
                if (key.Equals(customSymbol))
                    return true;
                return dict.ContainsKey(key);
            }

            public ICollection<string> Keys {
                get { throw new NotImplementedException("The method or operation is not implemented."); }
            }

            public bool Remove(string key) {
                if (key.Equals(customSymbol))
                    throw new UnboundNameException("Cannot delete customSymbol");
                return dict.Remove(key);
            }

            public bool TryGetValue(string key, out object value) {
                if (key.Equals(customSymbol)) {
                    value = customSymbolValue;
                    return true;
                }

                return dict.TryGetValue(key, out value);
            }

            public ICollection<object> Values {
                get { throw new NotImplementedException("The method or operation is not implemented."); }
            }

            public object this[string key] {
                get {
                    if (key.Equals(customSymbol))
                        return customSymbolValue;
                    return dict[key];
                }
                set {
                    if (key.Equals(customSymbol))
                        throw new UnboundNameException("Cannot assign to customSymbol");
                    dict[key] = value;
                }
            }

            #endregion

            #region ICollection<KeyValuePair<string,object>> Members

            public void Add(KeyValuePair<string, object> item) {
                throw new NotImplementedException("The method or operation is not implemented.");
            }

            public void Clear() {
                throw new NotImplementedException("The method or operation is not implemented.");
            }

            public bool Contains(KeyValuePair<string, object> item) {
                throw new NotImplementedException("The method or operation is not implemented.");
            }

            public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex) {
                throw new NotImplementedException("The method or operation is not implemented.");
            }

            public int Count {
                get { throw new NotImplementedException("The method or operation is not implemented."); }
            }

            public bool IsReadOnly {
                get { throw new NotImplementedException("The method or operation is not implemented."); }
            }

            public bool Remove(KeyValuePair<string, object> item) {
                throw new NotImplementedException("The method or operation is not implemented.");
            }

            #endregion

            #region IEnumerable<KeyValuePair<string,object>> Members

            public IEnumerator<KeyValuePair<string, object>> GetEnumerator() {
                foreach (var keyValue in dict) {
                    yield return keyValue;
                }

                yield return new KeyValuePair<string, object>("customSymbol", customSymbolValue);
            }

            #endregion

            #region IEnumerable Members

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
                throw new NotImplementedException("The method or operation is not implemented.");
            }

            #endregion
        }

        [Test]
        public void ScenarioCustomDictionary() {
            PythonDictionary customGlobals = new PythonDictionary(new StringDictionaryStorage(new CustomDictionary()));

            ScriptScope customModule = _pe.Runtime.CreateScope(new ObjectDictionaryExpando(customGlobals));

            // Evaluate
            Assert.AreEqual(_pe.Execute<int>("customSymbol + 1", customModule), CustomDictionary.customSymbolValue + 1);

            // Execute
            _pe.Execute("customSymbolPlusOne = customSymbol + 1", customModule);
            Assert.AreEqual(_pe.Execute<int>("customSymbolPlusOne", customModule), CustomDictionary.customSymbolValue + 1);
            Assert.AreEqual(customModule.GetVariable<int>("customSymbolPlusOne"), CustomDictionary.customSymbolValue + 1);

            // Compile
            CompiledCode compiledCode = _pe.CreateScriptSourceFromString("customSymbolPlusTwo = customSymbol + 2").Compile();

            compiledCode.Execute(customModule);
            Assert.AreEqual(_pe.Execute<int>("customSymbolPlusTwo", customModule), CustomDictionary.customSymbolValue + 2);
            Assert.AreEqual(customModule.GetVariable<int>("customSymbolPlusTwo"), CustomDictionary.customSymbolValue + 2);

            // check overriding of Add
            try {
                _pe.Execute("customSymbol = 1", customModule);
                throw new Exception("We should not reach here");
            } catch (UnboundNameException) { }

            try {
                _pe.Execute(@"global customSymbol
customSymbol = 1", customModule);
                throw new Exception("We should not reach here");
            } catch (UnboundNameException) { }

            // check overriding of Remove
            try {
                _pe.Execute("del customSymbol", customModule);
                throw new Exception("We should not reach here");
            } catch (UnboundNameException) { }

            try {
                _pe.Execute(@"global customSymbol
del customSymbol", customModule);
                throw new Exception("We should not reach here");
            } catch (UnboundNameException) { }

            // vars()
            IDictionary vars = _pe.Execute<IDictionary>("vars()", customModule);
            Assert.AreEqual(true, vars.Contains("customSymbol"));

            // Miscellaneous APIs
            //IntIntDelegate d = pe.CreateLambda<IntIntDelegate>("customSymbol + arg", customModule);
            // Assert.AreEqual(d(1), CustomDictionary.customSymbolValue + 1);
        }

        [Test]
        public void ScenarioCallClassInstance() {
            ScriptScope scope = _env.CreateScope();
            _pe.CreateScriptSourceFromString(@"
class X(object):
    def __call__(self, arg):
        return arg

a = X()

class Y:
    def __call__(self, arg):
        return arg

b = Y()", SourceCodeKind.Statements).Execute(scope);
            var a = scope.GetVariable<Func<object, int>>("a");
            var b = scope.GetVariable<Func<object, int>>("b");
            Assert.AreEqual(a(42), 42);
            Assert.AreEqual(b(42), 42);
        }

        // Evaluate
        [Test]
        public void ScenarioEvaluate() {
            ScriptScope scope = _env.CreateScope();

            Assert.AreEqual(10, _pe.CreateScriptSourceFromString("4+6").Execute<int>(scope));
            Assert.AreEqual(null, (object)_pe.CreateScriptSourceFromString("if True: pass").Execute(scope));

            Assert.AreEqual(10, _pe.CreateScriptSourceFromString("4+6", SourceCodeKind.AutoDetect).Execute<int>(scope));
            Assert.AreEqual(null, (object)_pe.CreateScriptSourceFromString("if True: pass", SourceCodeKind.AutoDetect).Execute(scope));

            Assert.AreEqual(10, _pe.CreateScriptSourceFromString("4+6", SourceCodeKind.Expression).Execute<int>(scope));
            Assert.Throws<SyntaxErrorException>(() => _pe.CreateScriptSourceFromString("if True: pass", SourceCodeKind.Expression).Execute(scope));

            Assert.AreEqual(null, (object)_pe.CreateScriptSourceFromString("4+6", SourceCodeKind.File).Execute(scope));
            Assert.AreEqual(null, (object)_pe.CreateScriptSourceFromString("if True: pass", SourceCodeKind.File).Execute(scope));

            Assert.AreEqual(null, (object)_pe.CreateScriptSourceFromString("4+6", SourceCodeKind.SingleStatement).Execute(scope));
            Assert.AreEqual(null, (object)_pe.CreateScriptSourceFromString("if True: pass", SourceCodeKind.SingleStatement).Execute(scope));

            Assert.AreEqual(null, (object)_pe.CreateScriptSourceFromString("4+6", SourceCodeKind.Statements).Execute(scope));
            Assert.AreEqual(null, (object)_pe.CreateScriptSourceFromString("if True: pass", SourceCodeKind.Statements).Execute(scope));

            Assert.AreEqual(10, (int)(object)_pe.Execute("4+6", scope));
            Assert.AreEqual(10, _pe.Execute<int>("4+6", scope));

            Assert.AreEqual("abab", (string)(object)_pe.Execute("'ab' * 2", scope));
            Assert.AreEqual("abab", _pe.Execute<string>("'ab' * 2", scope));

            ClsPart clsPart = new ClsPart();
            scope.SetVariable(clspartName, clsPart);
            Assert.AreEqual(clsPart, ((object)_pe.Execute("clsPart", scope)) as ClsPart);
            Assert.AreEqual(clsPart, _pe.Execute<ClsPart>("clsPart", scope));

            _pe.Execute("clsPart.Field = 100", scope);
            Assert.AreEqual(100, (int)(object)_pe.Execute("clsPart.Field", scope));
            Assert.AreEqual(100, _pe.Execute<int>("clsPart.Field", scope));

            // Ensure that we can get back a delegate to a Python method
            _pe.Execute("def IntIntMethod(a): return a * 100", scope);
            IntIntDelegate d = _pe.Execute<IntIntDelegate>("IntIntMethod", scope);
            Assert.AreEqual(d(2), 2 * 100);
        }

        [Test]
        public void ScenarioMemberNames() {
            ScriptScope scope = _env.CreateScope();

            _pe.CreateScriptSourceFromString(@"
class nc(object):
    def __init__(self):
        self.baz = 5
    foo = 3
    def abc(self): pass
    @staticmethod
    def staticfunc(arg1): pass
    @classmethod
    def classmethod(cls): pass

ncinst = nc()

def f(): pass

f.foo = 3
", SourceCodeKind.Statements).Execute(scope);

            ParameterExpression parameter = Expression.Parameter(typeof(object), "");

            DynamicMetaObject nc = Microsoft.Scripting.Utils.DynamicUtils.ObjectToMetaObject((object)scope.GetVariable("nc"), parameter);
            DynamicMetaObject ncinst = Microsoft.Scripting.Utils.DynamicUtils.ObjectToMetaObject((object)scope.GetVariable("ncinst"), parameter); ;
            DynamicMetaObject f = Microsoft.Scripting.Utils.DynamicUtils.ObjectToMetaObject((object)scope.GetVariable("f"), parameter); ;

            List<string> ncnames = new List<string>(nc.GetDynamicMemberNames());
            List<string> ncinstnames = new List<string>(ncinst.GetDynamicMemberNames());
            List<string> fnames = new List<string>(f.GetDynamicMemberNames());

            ncnames.Sort();
            ncinstnames.Sort();
            fnames.Sort();

            Assert.AreEqual(ncnames.ToArray(), new[] { "__class__", "__delattr__", "__dict__", "__doc__", "__eq__", "__format__", "__ge__", "__getattribute__", "__gt__", "__hash__", "__init__", "__le__", "__lt__", "__module__", "__ne__", "__new__", "__reduce__", "__reduce_ex__", "__repr__", "__setattr__", "__sizeof__", "__str__", "__subclasshook__", "__weakref__", "abc", "classmethod", "foo", "staticfunc" });
            Assert.AreEqual(ncinstnames.ToArray(), new[] { "__class__", "__delattr__", "__dict__", "__doc__", "__eq__", "__format__", "__ge__", "__getattribute__", "__gt__", "__hash__", "__init__", "__le__", "__lt__", "__module__", "__ne__", "__new__", "__reduce__", "__reduce_ex__", "__repr__", "__setattr__", "__sizeof__", "__str__", "__subclasshook__", "__weakref__", "abc", "baz", "classmethod", "foo", "staticfunc" });

            Assert.AreEqual(fnames.ToArray(), new[] { "foo" });
        }

        [Test]
        public void ScenarioTokenCategorizer() {
            var categorizer = _pe.GetService<TokenCategorizer>();
            var source = _pe.CreateScriptSourceFromString("sys", SourceCodeKind.Statements);
            categorizer.Initialize(null, source, SourceLocation.MinValue);

            TokenInfo token = categorizer.ReadToken();
            Assert.AreEqual(token.Category, TokenCategory.Identifier);

            token = categorizer.ReadToken();
            Assert.AreEqual(token.Category, TokenCategory.EndOfStream);

            source = _pe.CreateScriptSourceFromString("\"sys\"", SourceCodeKind.Statements);
            categorizer.Initialize(null, source, SourceLocation.MinValue);

            token = categorizer.ReadToken();
            Assert.AreEqual(token.Category, TokenCategory.StringLiteral);

            token = categorizer.ReadToken();
            Assert.AreEqual(token.Category, TokenCategory.EndOfStream);
        }

        private static string ListsToString<T>(IList<T> left, IList<T> right) {
            string res = "    ";

            foreach (object o in left) {
                res += "\"" + o + "\", ";
            }

            res += Environment.NewLine + "    ";
            foreach (object o in right) {
                res += "\"" + o + "\", ";
            }
            return res;
        }

        [Test]
        public void ScenarioCallableClassToDelegate() {
            ScriptSource src = _pe.CreateScriptSourceFromString(@"
class Test(object):
    def __call__(self):
        return 42

inst = Test()

class TestOC:
    def __call__(self):
        return 42

instOC = TestOC()
", SourceCodeKind.Statements);
            ScriptScope scope = _pe.CreateScope();
            src.Execute(scope);

            Func<int> t = scope.GetVariable<Func<int>>("inst");
            Assert.AreEqual(42, t());

            t = scope.GetVariable<Func<int>>("instOC");
            Assert.AreEqual(42, t());
        }

        // ExecuteFile
        [Test]
        public void ScenarioExecuteFile() {
            ScriptSource tempFile1, tempFile2;

            ScriptScope scope = _env.CreateScope();

            using (StringWriter sw = new StringWriter()) {
                sw.WriteLine("var1 = (10, 'z')");
                sw.WriteLine("");
                sw.WriteLine("clsPart.Field = 100");
                sw.WriteLine("clsPart.Property = clsPart.Field * 5");
                sw.WriteLine("clsPart.Event += (lambda x: x*x)");

                tempFile1 = _pe.CreateScriptSourceFromString(sw.ToString(), SourceCodeKind.File);
            }

            ClsPart clsPart = new ClsPart();
            scope.SetVariable(clspartName, clsPart);
            tempFile1.Execute(scope);

            using (StringWriter sw = new StringWriter()) {
                sw.WriteLine("if var1[0] != 10: raise AssertionError('test failed')");
                sw.WriteLine("if var1[1] != 'z': raise AssertionError('test failed')");
                sw.WriteLine("");
                sw.WriteLine("if clsPart.Property != clsPart.Field * 5: raise AssertionError('test failed')");
                sw.WriteLine("var2 = clsPart.Method(var1[0])");
                sw.WriteLine("if var2 != 10 * 10: raise AssertionError('test failed')");

                tempFile2 = _pe.CreateScriptSourceFromString(sw.ToString(), SourceCodeKind.File);
            }

            tempFile2.Execute(scope);
        }

        // Bug: 542
        [Test]
        public void Scenario542() {
            ScriptSource tempFile1;

            ScriptScope scope = _env.CreateScope();

            using (StringWriter sw = new StringWriter()) {
                sw.WriteLine("def M1(): return -1");
                sw.WriteLine("def M2(): return +1");

                sw.WriteLine("class C:");
                sw.WriteLine("    def M1(self): return -1");
                sw.WriteLine("    def M2(self): return +1");

                sw.WriteLine("class C1:");
                sw.WriteLine("    def M(): return -1");
                sw.WriteLine("class C2:");
                sw.WriteLine("    def M(): return +1");

                tempFile1 = _pe.CreateScriptSourceFromString(sw.ToString(), SourceCodeKind.File);
            }

            tempFile1.Execute(scope);

            Assert.AreEqual(-1, _pe.CreateScriptSourceFromString("M1()").Execute<int>(scope));
            Assert.AreEqual(+1, _pe.CreateScriptSourceFromString("M2()").Execute<int>(scope));

            Assert.AreEqual(-1, (int)(object)_pe.CreateScriptSourceFromString("M1()").Execute(scope));
            Assert.AreEqual(+1, (int)(object)_pe.CreateScriptSourceFromString("M2()").Execute(scope));

            _pe.CreateScriptSourceFromString("if M1() != -1: raise AssertionError('test failed')", SourceCodeKind.SingleStatement).Execute(scope);
            _pe.CreateScriptSourceFromString("if M2() != +1: raise AssertionError('test failed')", SourceCodeKind.SingleStatement).Execute(scope);


            _pe.CreateScriptSourceFromString("c = C()", SourceCodeKind.SingleStatement).Execute(scope);
            Assert.AreEqual(-1, _pe.CreateScriptSourceFromString("c.M1()").Execute<int>(scope));
            Assert.AreEqual(+1, _pe.CreateScriptSourceFromString("c.M2()").Execute<int>(scope));

            Assert.AreEqual(-1, (int)(object)_pe.CreateScriptSourceFromString("c.M1()").Execute(scope));
            Assert.AreEqual(+1, (int)(object)_pe.CreateScriptSourceFromString("c.M2()").Execute(scope));

            _pe.CreateScriptSourceFromString("if c.M1() != -1: raise AssertionError('test failed')", SourceCodeKind.SingleStatement).Execute(scope);
            _pe.CreateScriptSourceFromString("if c.M2() != +1: raise AssertionError('test failed')", SourceCodeKind.SingleStatement).Execute(scope);


            // Assert.AreEqual(-1, pe.EvaluateAs<int>("C1.M()"));
            // Assert.AreEqual(+1, pe.EvaluateAs<int>("C2.M()"));

            // Assert.AreEqual(-1, (int)pe.Evaluate("C1.M()"));
            // Assert.AreEqual(+1, (int)pe.Evaluate("C2.M()"));

            //pe.Execute(pe.CreateScriptSourceFromString("if C1.M() != -1: raise AssertionError('test failed')");
            //pe.Execute(pe.CreateScriptSourceFromString("if C2.M() != +1: raise AssertionError('test failed')");
        }

        // Bug: 167
        [Test]
        public void Scenario167() {
            ScriptScope scope = _env.CreateScope();
            _pe.CreateScriptSourceFromString("a=1\r\nb=-1", SourceCodeKind.Statements).Execute(scope);
            Assert.AreEqual(1, _pe.CreateScriptSourceFromString("a").Execute<int>(scope));
            Assert.AreEqual(-1, _pe.CreateScriptSourceFromString("b").Execute<int>(scope));
        }

        // AddToPath
        [Test]
        public void ScenarioAddToPath() { // runs first to avoid path-order issues
            //pe.InitializeModules(ipc_path, ipc_path + "\\ipy.exe", pe.VersionString);
            string tempFile1 = Path.GetTempFileName();

            try {
                File.WriteAllText(tempFile1, "from testpkg1.does_not_exist import *");
                ScriptScope scope = _pe.Runtime.CreateScope();

                try {
                    _pe.CreateScriptSourceFromFile(tempFile1).Execute(scope);
                    throw new Exception("Scenario7");
                } catch (IronPython.Runtime.Exceptions.ImportException) { }

                File.WriteAllText(tempFile1, "from testpkg1.mod1 import *");
                _pe.SetSearchPaths(new string[] { Common.ScriptTestDirectory });

                _pe.CreateScriptSourceFromFile(tempFile1).Execute(scope);
                _pe.CreateScriptSourceFromString("give_back(eval('2 + 3'))", SourceCodeKind.Statements).Execute(scope);
            } finally {
                File.Delete(tempFile1);
            }
        }

        // Options.DebugMode

#if FEATURE_REMOTING
        // [Test] // https://github.com/IronLanguages/ironpython3/issues/898
        public void ScenarioPartialTrust() {
            // Mono doesn't implement partial trust
            if(System.Environment.OSVersion.Platform == System.PlatformID.Unix)
                return;

            // basic check of running a host in partial trust

            AppDomainSetup info = new AppDomainSetup();
            info.ApplicationBase = AppDomain.CurrentDomain.BaseDirectory;
            info.ApplicationName = "Test";

            Evidence evidence = new Evidence();
            evidence.AddHostEvidence(new Zone(SecurityZone.Internet));

            PermissionSet permSet = SecurityManager.GetStandardSandbox(evidence);
            AppDomain newDomain = AppDomain.CreateDomain("test", evidence, info, permSet, null);

            // create runtime in partial trust...
            ScriptRuntime runtime = Python.CreateRuntime(newDomain);

            // get the Python engine...
            ScriptEngine engine = runtime.GetEngine("py");

            // execute some simple code
            ScriptScope scope = engine.CreateScope();
            ScriptSource source = engine.CreateScriptSourceFromString("2 + 2");
            Assert.AreEqual(source.Execute<int>(scope), 4);

            // import all of the built-in modules & make sure we can reflect on them...
            source = engine.CreateScriptSourceFromString(@"
import sys
for mod in sys.builtin_module_names:
    if mod.startswith('_ctypes'):
        continue
    elif mod.startswith('signal'):
        continue
    elif mod=='mmap':
        print 'https://github.com/IronLanguages/main/issues/835'
        continue
    x = __import__(mod)
    dir(x)
", SourceCodeKind.Statements);

            source.Execute(scope);

            // define some classes & use the methods...
            source = engine.CreateScriptSourceFromString(@"
class x(object):
    def f(self): return 1 + 2

a = x()
a.f()


class y:
    def f(self): return 1 + 2

b = y()
b.f()
", SourceCodeKind.Statements);


            source.Execute(scope);

            // call a protected method on a derived class...
            source = engine.CreateScriptSourceFromString(@"
import clr
class x(object):
    def f(self): return 1 + 2

a = x()
b = a.MemberwiseClone()

if id(a) == id(b):
    raise Exception
", SourceCodeKind.Statements);


            source.Execute(scope);

            AppDomain.Unload(newDomain);
        }

        [Test]
        public void ScenarioStackFrameLineInfo() {
            const string lineNumber = "raise.py:line";
            // TODO: Should this work on Mono?
            if(Environment.OSVersion.Platform == PlatformID.Unix)
            {
                Console.WriteLine("Skipping StackFrameLineInfo test on Mono");
                return;
            }

            // TODO: clone setup?
            var scope = Python.CreateRuntime().CreateScope("py");
            var debugSetup = Python.CreateRuntimeSetup(null);
            debugSetup.DebugMode = true;
            var debugScope = new ScriptRuntime(debugSetup).CreateScope("py");

            TestLineInfo(scope, lineNumber);
            TestLineInfo(debugScope, lineNumber);
            TestLineInfo(scope, lineNumber);

            // Ensure that all APIs work
            Assert.AreEqual(scope.GetVariable<int>("x"), 1);

            //IntIntDelegate d = pe.CreateLambda<IntIntDelegate>("arg + x");
            // Assert.AreEqual(d(100), 101);
            //d = pe.CreateMethod<IntIntDelegate>("var = arg + x\nreturn var");
            // Assert.AreEqual(d(100), 101);
        }

        private void TestLineInfo(ScriptScope/*!*/ scope, string lineNumber) {
            try {
                scope.Engine.ExecuteFile(Path.Combine(Common.InputTestDirectory, "raise.py"), scope);
                throw new Exception("We should not get here");
            } catch (StopIterationException e2) {
                if (scope.Engine.Runtime.Setup.DebugMode != e2.StackTrace.Contains(lineNumber))
                    throw new Exception("Debugging is enabled even though Options.DebugMode is not specified");
            }
        }

#endif

        // Compile and Run
        [Test]
        public void ScenarioCompileAndRun() {
            ClsPart clsPart = new ClsPart();

            ScriptScope scope = _env.CreateScope();

            scope.SetVariable(clspartName, clsPart);
            CompiledCode compiledCode = _pe.CreateScriptSourceFromString("def f(): clsPart.Field += 10", SourceCodeKind.Statements).Compile();
            compiledCode.Execute(scope);

            compiledCode = _pe.CreateScriptSourceFromString("f()").Compile();
            compiledCode.Execute(scope);
            Assert.AreEqual(10, clsPart.Field);
            compiledCode.Execute(scope);
            Assert.AreEqual(20, clsPart.Field);
        }

        // [Test] // https://github.com/IronLanguages/ironpython3/issues/906
        public void ScenarioStreamRedirect() {
            MemoryStream stdout = new MemoryStream();
            MemoryStream stdin = new MemoryStream();
            MemoryStream stderr = new MemoryStream();
            Encoding encoding = Encoding.UTF8;

            _pe.Runtime.IO.SetInput(stdin, encoding);
            _pe.Runtime.IO.SetOutput(stdout, encoding);
            _pe.Runtime.IO.SetErrorOutput(stderr, encoding);

            const string str = "This is stdout";
            byte[] bytes = encoding.GetBytes(str);

            try {
                ScriptScope scope = _pe.Runtime.CreateScope();
                _pe.CreateScriptSourceFromString("import sys", SourceCodeKind.Statements).Execute(scope);

                stdin.Write(bytes, 0, bytes.Length);
                stdin.Position = 0;
                _pe.CreateScriptSourceFromString("output = sys.__stdin__.readline()", SourceCodeKind.Statements).Execute(scope);
                Assert.AreEqual(str, _pe.CreateScriptSourceFromString("output").Execute<string>(scope));

                _pe.CreateScriptSourceFromString("sys.__stdout__.write(output)", SourceCodeKind.Statements).Execute(scope);
                stdout.Flush();
                stdout.Position = 0;

                // deals with BOM:
                using (StreamReader reader = new StreamReader(stdout, true)) {
                    string s = reader.ReadToEnd();
                    Assert.AreEqual(str, s);
                }

                _pe.CreateScriptSourceFromString("sys.__stderr__.write(\"This is stderr\")", SourceCodeKind.Statements).Execute(scope);

                stderr.Flush();
                stderr.Position = 0;

                // deals with BOM:
                using (StreamReader reader = new StreamReader(stderr, true)) {
                    string s = reader.ReadToEnd();
                    Assert.AreEqual("This is stderr", s);
                }
            } finally {
                _pe.Runtime.IO.RedirectToConsole();
            }
        }

        private class DictThreadGlobalState {
            public volatile int DoneCount;
            public bool IsDone;
            public ManualResetEvent Event;
            public ManualResetEvent DoneEvent;
            public PythonDictionary Dict;
            public List<DictThreadTestState> Tests = new List<DictThreadTestState>();
            public List<Thread> Threads = new List<Thread>();
            public Func<PythonDictionary> DictMaker;
        }

        private class DictThreadTestState {
            public Action<PythonDictionary> Action;
            public Action<PythonDictionary> Verify;
            public DictThreadGlobalState GlobalState;
        }

        [Test]
        public static void ScenarioDictionaryThreadSafety() {
            const int ThreadCount = 10;

            // add new keys to an empty dictionary concurrently
            RunThreadTest(
                MakeThreadTest(
                    ThreadCount,
                    AddStringKey,
                    VerifyStringKey,
                    () => new PythonDictionary()
                )
            );

            // add new keys to a constant dictionary concurrently
            var constantStorage = MakeConstantStringDictionary(ThreadCount);
            RunThreadTest(
                MakeThreadTest(
                    ThreadCount,
                    AddStringKey,
                    VerifyStringKey,
                    () => new PythonDictionary(constantStorage)
                )
            );

            // remove keys from a constant dictionary concurrently
            var emptyStorage = MakeConstantStringDictionary(ThreadCount);
            RunThreadTest(
                MakeThreadTest(
                    ThreadCount,
                    RemoveStringKey,
                    VerifyNoKeys,
                    () => new PythonDictionary(emptyStorage)
                )
            );
        }

        private static ConstantDictionaryStorage MakeConstantStringDictionary(int ThreadCount) {
            object[] storage = new object[ThreadCount * 2];
            for (int i = 0; i < ThreadCount; i++) {
                storage[i * 2] = StringKey(i);
                storage[i * 2 + 1] = StringKey(i);
            }

            var emptyStorage = new ConstantDictionaryStorage(new CommonDictionaryStorage(storage, true));
            return emptyStorage;
        }

        private static ConstantDictionaryStorage MakeConstantStringDictionary() {
            object[] storage = new object[2];
            storage[0] = storage[1] = "SomeValueWhichIsNeverUsedDuringTheTest";
            var emptyStorage = new ConstantDictionaryStorage(new CommonDictionaryStorage(storage, true));
            return emptyStorage;
        }

        private static void RunThreadTest(DictThreadGlobalState globalState) {
            for (int i = 0; i < globalState.Threads.Count; i++) {
                globalState.Threads[i].Start(globalState.Tests[i]);
            }
            for (int i = 0; i < 10000; i++) {
                globalState.Dict = globalState.DictMaker();

                while (globalState.DoneCount != globalState.Threads.Count) {
                    // wait for threads to get back to start point
                }

                globalState.DoneEvent.Reset();
                globalState.Event.Set();

                while (globalState.DoneCount != 0) {
                    // wait for threads to get back to finish
                }

                foreach (var test in globalState.Tests) {
                    test.Verify(globalState.Dict);
                }

                globalState.Event.Reset();
                globalState.DoneEvent.Set();
            }

            globalState.IsDone = true;
            globalState.Event.Set();
        }

        private static DictThreadGlobalState MakeThreadTest(int threadCount,
            Func<int, Action<PythonDictionary>> actionMaker,
            Func<int, Action<PythonDictionary>> verifyMaker,
            Func<PythonDictionary> dictMaker) {
            DictThreadGlobalState globalState = new DictThreadGlobalState();

            globalState.DictMaker = dictMaker;
            globalState.Event = new ManualResetEvent(false);
            globalState.DoneEvent = new ManualResetEvent(false);
            globalState.Threads = new List<Thread>();
            globalState.Tests = new List<DictThreadTestState>();

            for (int i = 0; i < threadCount; i++) {
                var curTestCase = new DictThreadTestState();
                curTestCase.GlobalState = globalState;
                curTestCase.Action = actionMaker(i);
                curTestCase.Verify = verifyMaker(i);
                globalState.Tests.Add(curTestCase);

                Thread thread = new Thread(new ParameterizedThreadStart((x) => {
                    var state = (DictThreadTestState)x;

#pragma warning disable 0420 // "a reference to a volatile field will not be treated as volatile"
                    for (; ; ) {
                        Interlocked.Increment(ref state.GlobalState.DoneCount);
                        state.GlobalState.Event.WaitOne();
                        if (globalState.IsDone) {
                            break;
                        }

                        state.Action(state.GlobalState.Dict);

                        Interlocked.Decrement(ref state.GlobalState.DoneCount);

                        state.GlobalState.DoneEvent.WaitOne();
                    }
#pragma warning restore 0420
                }));

                thread.IsBackground = true;
                globalState.Threads.Add(thread);
            }
            return globalState;
        }

        private static Action<PythonDictionary> AddStringKey(int value) {
            string key = StringKey(value);

            return (dict) => {
                dict[key] = key;
            };
        }

        private static string StringKey(int value) {
            return new string((char)(65 + value), 1);
        }

        private static Action<PythonDictionary> RemoveStringKey(int value) {
            string key = StringKey(value);

            return (dict) => {
                dict.Remove(key);
            };
        }

        private static Action<PythonDictionary> VerifyStringKey(int value) {
            string key = StringKey(value);

            return (dict) => {
                if (!dict.Contains(key)) {
                    Console.WriteLine(PythonOps.Repr(DefaultContext.Default, dict));
                }
                Assert.True(dict.Contains(key));
                Assert.AreEqual((string)dict[key], key);
            };
        }

        private static Action<PythonDictionary> VerifyNoKeys(int value) {
            return (dict) => {
                Assert.AreEqual(dict.Count, 0);
            };
        }

        [Test]
        public void Scenario12() {
            ScriptScope scope = _env.CreateScope();

            _pe.CreateScriptSourceFromString(@"
class R(object):
    def __init__(self, a, b):
        self.a = a
        self.b = b

    def M(self):
        return self.a + self.b

    sum = property(M, None, None, None)

r = R(10, 100)
if r.sum != 110:
    raise AssertionError('Scenario 12 failed')
", SourceCodeKind.Statements).Execute(scope);
        }

// TODO: rewrite
#if FALSE
        [Test]
        public void ScenarioTrueDivision1() {
            TestOldDivision(pe, DefaultModule);
            ScriptScope module = pe.CreateModule("anonymous", ModuleOptions.TrueDivision);
            TestNewDivision(pe, module);
        }

        [Test]
        public void ScenarioTrueDivision2() {
            TestOldDivision(pe, DefaultModule);
            ScriptScope module = pe.CreateModule("__future__", ModuleOptions.PublishModule);
            module.SetVariable("division", 1);
            pe.Execute(pe.CreateScriptSourceFromString("from __future__ import division", module));
            TestNewDivision(pe, module);
        }

        [Test]
        public void ScenarioTrueDivision3() {
            TestOldDivision(pe, DefaultModule);
            ScriptScope future = pe.CreateModule("__future__", ModuleOptions.PublishModule);
            future.SetVariable("division", 1);
            ScriptScope td = pe.CreateModule("truediv", ModuleOptions.None);
            ScriptCode cc = ScriptCode.FromCompiledCode((CompiledCode)pe.CompileCode("from __future__ import division"));
            cc.Run(td);
            TestNewDivision(pe, td);  // we've polluted the DefaultModule by executing the code
        }

        [Test]
        public void ScenarioTrueDivision4() {
            pe.AddToPath(Common.ScriptTestDirectory);

            string modName = GetTemporaryModuleName();
            string file = System.IO.Path.Combine(Common.ScriptTestDirectory, modName + ".py");
            System.IO.File.WriteAllText(file, "result = 1/2");

            PythonDivisionOptions old = PythonEngine.CurrentEngine.Options.DivisionOptions;

            try {
                PythonEngine.CurrentEngine.Options.DivisionOptions = PythonDivisionOptions.Old;
                ScriptScope module = pe.CreateModule("anonymous", ModuleOptions.TrueDivision);
                pe.Execute(pe.CreateScriptSourceFromString("import " + modName, module));
                int res = pe.EvaluateAs<int>(modName + ".result", module);
                Assert.AreEqual(res, 0);
            } finally {
                PythonEngine.CurrentEngine.Options.DivisionOptions = old;
                try {
                    System.IO.File.Delete(file);
                } catch { }
            }
        }

        private string GetTemporaryModuleName() {
            return "tempmod" + Path.GetRandomFileName().Replace('-', '_').Replace('.', '_');
        }

        [Test]
        public void ScenarioTrueDivision5() {
            pe.AddToPath(Common.ScriptTestDirectory);

            string modName = GetTemporaryModuleName();
            string file = System.IO.Path.Combine(Common.ScriptTestDirectory, modName + ".py");
            System.IO.File.WriteAllText(file, "from __future__ import division; result = 1/2");

            try {
                ScriptScope module = ScriptDomainManager.CurrentManager.CreateModule(modName);
                pe.Execute(pe.CreateScriptSourceFromString("import " + modName, module));
                double res = pe.EvaluateAs<double>(modName + ".result", module);
                Assert.AreEqual(res, 0.5);
                Assert.AreEqual((bool)((PythonContext)DefaultContext.Default.LanguageContext).TrueDivision, false);
            } finally {
                try {
                    System.IO.File.Delete(file);
                } catch { }
            }
        }

        [Test]
        public void ScenarioSystemStatePrefix() {
            Assert.AreEqual(IronPythonTest.Common.RuntimeDirectory, pe.SystemState.prefix);
        }


        private static void TestOldDivision(ScriptEngine pe, ScriptScope module) {
            pe.Execute(pe.CreateScriptSourceFromString("result = 1/2", module));
            Assert.AreEqual((int)module.Scope.LookupName(DefaultContext.Default.LanguageContext, SymbolTable.StringToId("result")), 0);
            Assert.AreEqual(pe.EvaluateAs<int>("1/2", module), 0);
            pe.Execute(pe.CreateScriptSourceFromString("exec 'result = 3/2'", module));
            Assert.AreEqual((int)module.Scope.LookupName(DefaultContext.Default.LanguageContext, SymbolTable.StringToId("result")), 1);
            Assert.AreEqual(pe.EvaluateAs<int>("eval('3/2')", module), 1);
        }

        private static void TestNewDivision(ScriptEngine pe, ScriptScope module) {
            pe.Execute(pe.CreateScriptSourceFromString("result = 1/2", module));
            Assert.AreEqual((double)module.Scope.LookupName(DefaultContext.Default.LanguageContext, SymbolTable.StringToId("result")), 0.5);
            Assert.AreEqual(pe.EvaluateAs<double>("1/2", module), 0.5);
            pe.Execute(pe.CreateScriptSourceFromString("exec 'result = 3/2'", module));
            Assert.AreEqual((double)module.Scope.LookupName(DefaultContext.Default.LanguageContext, SymbolTable.StringToId("result")), 1.5);
            Assert.AreEqual(pe.EvaluateAs<double>("eval('3/2')", module), 1.5);
        }
#endif
        // More to come: exception related...

        public static int Negate(int arg) { return -1 * arg; }

    }


    public interface IFooable {
    }

    public class Fooable : IFooable {

    }

    public static class FooableExtensions {
        public static string Bar(IFooable self) {
            return "Bar Called";
        }
    }

    [PythonHiddenBaseClass]
    public class HiddenBase {
        public void Inaccessible() {
        }
    }

    public class DerivedFromHiddenBase : HiddenBase {
        public int Accessible() {
            return 42;
        }
    }

    public class GenericProperty<T> {
        private T _value;
        public T Value {
            get {
                return this._value;
            }
            set {
                this._value = value;
            }
        }
    }

}
