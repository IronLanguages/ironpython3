// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
#if FEATURE_WPF
using System.Windows.Markup;
#endif

using Microsoft.Scripting;
using Microsoft.Scripting.Hosting;
using Microsoft.Scripting.Runtime;

using IronPython.Hosting;
using IronPython.Runtime;

#if FEATURE_WPF
using DependencyObject = System.Windows.DependencyObject;
#endif

namespace IronPythonTest {
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

    // Used to test exception thrown in another domain can be shown correctly.
    public class ScriptRunner
#if FEATURE_REMOTING
        : MarshalByRefObject
#endif
    {
        private readonly ScriptEngine _engine = Python.CreateEngine();

        public void Run(string script) {
            _engine.CreateScriptSourceFromString(script, SourceCodeKind.File).Execute(_engine.CreateScope());
        }
    }
}
