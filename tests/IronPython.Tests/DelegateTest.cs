// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;

namespace IronPythonTest {
    public delegate void ParamsDelegate(object sender, params object[] args);
    public delegate void BigParamsDelegate(object sender, object a, object b, object c, object d, params object[] args);
    public delegate void OutDelegate(object sender, out object res);
    public delegate void RefDelegate(object sender, ref object res);
    public delegate string OutReturnDelegate(object sender, out object res);
    public delegate int RefReturnDelegate(object sender, ref object res);
    public delegate void SimpleDelegate();
    public delegate void SimpleDelegateWithOneArg(object arg1);
    public delegate object SimpleReturnDelegate();
    public delegate object SimpleReturnDelegateArg1(object arg1);
    public delegate object SimpleReturnDelegateArg2(object arg1, object arg2);
    public delegate object IntArgDelegate(int arg1, int arg2);
    public delegate object StringArgDelegate(string arg1, string arg2);

    public class DelegateTest {
        // custom delegates used for various event handlers...        
        public static event EventHandler StaticEvent;
        public static event EventHandler<EventArgs> StaticGenericEvent;
        public static event ParamsDelegate StaticParamsEvent;
        public static event OutDelegate StaticOutEvent;
        public static event RefDelegate StaticRefEvent;
        public static event BigParamsDelegate StaticBigParamsEvent;
        public static event OutReturnDelegate StaticOutReturnEvent;
        public static event RefReturnDelegate StaticRefReturnEvent;

        public event EventHandler Event;
        public event EventHandler<EventArgs> GenericEvent;
        public event ParamsDelegate ParamsEvent;
        public event OutDelegate OutEvent;
        public event RefDelegate RefEvent;
        public event BigParamsDelegate BigParamsEvent;
        public event OutReturnDelegate OutReturnEvent;
        public event RefReturnDelegate RefReturnEvent;

        public static SimpleDelegate Simple = new SimpleDelegate(SimpleMethod);

        public static void SimpleMethod() {
        }

        public static void InvokeUntypedDelegate(Delegate d, params object[] args) {
            d.DynamicInvoke(args);
        }

        public static void FireStatic(object sender, EventArgs e) {
            StaticEvent(sender, e);
        }

        public static void FireGenericStatic(object sender, EventArgs e) {
            StaticGenericEvent(sender, e);
        }

        public static void FireParamsStatic(object sender, params object[] args) {
            StaticParamsEvent(sender, args);
        }

        public static void FireOutStatic(object sender, out object res) {
            StaticOutEvent(sender, out res);
        }

        public static void FireBigParamsStatic(object sender, object a, object b, object c, object d, params object[] args) {
            StaticBigParamsEvent(sender, a, b, c, d, args);
        }

        public static void FireRefStatic(object sender, ref object res) {
            StaticRefEvent(sender, ref res);
        }

        public static string FireOutReturnStatic(object sender, out object res) {
            return StaticOutReturnEvent(sender, out res);
        }

        public static int FireRefReturnStatic(object sender, ref object res) {
            return StaticRefReturnEvent(sender, ref res);
        }

        public void FireInstance(object sender, EventArgs e) {
            Event(sender, e);
        }

        public void FireGeneric(object sender, EventArgs e) {
            GenericEvent(sender, e);
        }

        public void FireParams(object sender, params object[] args) {
            ParamsEvent(sender, args);
        }

        public void FireOut(object sender, out object res) {
            OutEvent(sender, out res);
        }

        public void FireRef(object sender, ref object res) {
            RefEvent(sender, ref res);
        }

        public void FireBigParams(object sender, object a, object b, object c, object d, params object[] args) {
            BigParamsEvent(sender, a, b, c, d, args);
        }

        public string FireOutReturn(object sender, out object res) {
            return OutReturnEvent(sender, out res);
        }

        public int FireRefReturn(object sender, ref object res) {
            return RefReturnEvent(sender, ref res);
        }
    }
}
