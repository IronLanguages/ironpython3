// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;

namespace IronPythonTest {
    public delegate void EventTestDelegate();
    public delegate void OtherEvent(object sender, EventArgs args);

    public class Events {
        public Events() {
        }


        public static object GetTrue() {
            return true;
        }
        public static object GetFalse() {
            return false;
        }
        public static event EventTestDelegate StaticTest;
        public event EventTestDelegate InstanceTest;
        public event OtherEvent InstanceOther;

        public static event OtherEvent OtherStaticTest;

        public void CallInstance() => InstanceTest?.Invoke();

        public static void CallStatic() => StaticTest?.Invoke();

        public void CallOtherInstance(object sender, EventArgs args) => InstanceOther?.Invoke(sender, args);

        public static void CallOtherStatic(object sender, EventArgs args) {
            OtherStaticTest(sender, args);
        }

        private bool _marker = false;

        public bool Marker {
            get { return _marker; }
            set { _marker = value; }
        }

        public static bool StaticMarker = false;

        public void SetMarker() {
            _marker = true;
        }

        public static void StaticSetMarker() {
            StaticMarker = true;
        }

        public void AddSetMarkerDelegateToInstanceTest() {
            InstanceTest += new EventTestDelegate(this.SetMarker);
        }

        public static void AddSetMarkerDelegateToStaticTest() {
            StaticTest += new EventTestDelegate(StaticSetMarker);
        }

        public void FireProtectedTest() {
            if (OnProtectedEvent != null) OnProtectedEvent(this, EventArgs.Empty);
            if (ExplicitProtectedEvent != null) ExplicitProtectedEvent(this, EventArgs.Empty);
        }

        protected event EventHandler OnProtectedEvent;

        private EventHandler ExplicitProtectedEvent;

        public event EventHandler OnExplicitProtectedEvent {
            add { ExplicitProtectedEvent += value; }
            remove { ExplicitProtectedEvent -= value; }
        }

    }
}
