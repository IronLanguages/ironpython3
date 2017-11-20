/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the  Apache License, Version 2.0, please send an email to 
 * dlr@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 *
 * ***************************************************************************/

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Generation;
using Microsoft.Scripting.Utils;

namespace IronPython.Runtime.Types {

    /// <summary>
    /// The unbound representation of an event property
    /// </summary>
    [PythonType("event#")]
    public sealed class ReflectedEvent : PythonTypeDataSlot, ICodeFormattable {
        private readonly bool _clsOnly;
        private readonly EventTracker/*!*/ _tracker;

        internal ReflectedEvent(EventTracker/*!*/ tracker, bool clsOnly) {
            Assert.NotNull(tracker);

            _clsOnly = clsOnly;
            _tracker = tracker;
        }

        #region Internal APIs

        internal override bool TryGetValue(CodeContext/*!*/ context, object instance, PythonType owner, out object value) {
            Assert.NotNull(context, owner);

            value = new BoundEvent(this, instance, owner);
            return true;
        }

        internal override bool GetAlwaysSucceeds {
            get {
                return true;
            }
        }

        internal override bool TrySetValue(CodeContext/*!*/ context, object instance, PythonType owner, object value) {
            Assert.NotNull(context);
            BoundEvent et = value as BoundEvent;

            if (et == null || EventInfosDiffer(et)) {
                BadEventChange bea = value as BadEventChange;

                if (bea != null) {
                    PythonType dt = bea.Owner as PythonType;
                    if (dt != null) {
                        if (bea.Instance == null) {
                            throw new MissingMemberException(String.Format("attribute '{1}' of '{0}' object is read-only", dt.Name, _tracker.Name));
                        } else {
                            throw new MissingMemberException(String.Format("'{0}' object has no attribute '{1}'", dt.Name, _tracker.Name));
                        }
                    }
                }

                throw ReadOnlyException(DynamicHelpers.GetPythonTypeFromType(Info.DeclaringType));
            }

            return true;
        }

        private bool EventInfosDiffer(BoundEvent et) {
            
            // if they're the same object they're the same...
            if (et.Event.Info == Info) {
                return false;
            }

            // otherwise compare based upon type & metadata token (they
            // differ by ReflectedType)
            if (et.Event.Info.DeclaringType != Info.DeclaringType ||
                et.Event.Info.MetadataToken != Info.MetadataToken) {
                return true;
            }

            return false;
        }

        internal override bool TryDeleteValue(CodeContext/*!*/ context, object instance, PythonType owner) {
            Assert.NotNull(context, owner);
            throw ReadOnlyException(DynamicHelpers.GetPythonTypeFromType(Info.DeclaringType));
        }

        internal override bool IsAlwaysVisible => !_clsOnly;

        #endregion

        #region Public Python APIs

        public string __doc__ {
            get {
                return DocBuilder.CreateAutoDoc(_tracker.Event);
            }
        }

        public EventInfo/*!*/ Info {
            [PythonHidden]
            get {
                return _tracker.Event;
            }
        }

        public EventTracker/*!*/ Tracker {
            [PythonHidden]
            get {
                return _tracker;
            }
        }

        /// <summary>
        /// BoundEvent is the object that gets returned when the user gets an event object.  An
        /// BoundEvent tracks where the event was received from and is used to verify we get
        /// a proper add when dealing w/ statics events.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible")] // TODO: fix
        public class BoundEvent {
            private readonly ReflectedEvent/*!*/ _event;
            private readonly PythonType/*!*/ _ownerType;
            private readonly object _instance;

            public ReflectedEvent/*!*/ Event {
                get {
                    return _event;
                }
            }

            public BoundEvent(ReflectedEvent/*!*/ reflectedEvent, object instance, PythonType/*!*/ ownerType) {
                Assert.NotNull(reflectedEvent, ownerType);

                _event = reflectedEvent;
                _instance = instance;
                _ownerType = ownerType;
            }

            // this one's correct, InPlaceAdd is wrong but we still have some dependencies on the wrong name.
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2225:OperatorOverloadsHaveNamedAlternates")] // TODO: fix
            [SpecialName]
            public object op_AdditionAssignment(CodeContext/*!*/ context, object func) {
                return InPlaceAdd(context, func);
            }

            [SpecialName]
            public object InPlaceAdd(CodeContext/*!*/ context, object func) {
                if (func == null || !PythonOps.IsCallable(context, func)) {
                    throw PythonOps.TypeError("event addition expected callable object, got {0}", PythonTypeOps.GetName(func));
                }

                if (_event.Tracker.IsStatic) {
                    if (_ownerType != DynamicHelpers.GetPythonTypeFromType(_event.Tracker.DeclaringType)) {
                        // mutating static event, only allow this from the type we're mutating, not sub-types
                        return new BadEventChange(_ownerType, _instance);
                    }
                }

                MethodInfo add = _event.Tracker.GetCallableAddMethod();

                // TODO (tomat): this used to use event.ReflectedType, is it still correct?
                if (_instance != null) {
                    add = CompilerHelpers.TryGetCallableMethod(_instance.GetType(), add);
                }

                if (CompilerHelpers.IsVisible(add)
                    || (add.IsProtected() /*todo: validate current context is in family*/ )
                    || context.LanguageContext.DomainManager.Configuration.PrivateBinding) {
                    _event.Tracker.AddHandler(_instance, func, context.LanguageContext.DelegateCreator);
                } else {
                    throw new TypeErrorException("Cannot add handler to a private event.");
                }

                return this;
            }

            [SpecialName]
            public object InPlaceSubtract(CodeContext/*!*/ context, object func) {
                Assert.NotNull(context);
                if (func == null) {
                    throw PythonOps.TypeError("event subtraction expected callable object, got None");
                }

                if (_event.Tracker.IsStatic) {
                    if (_ownerType != DynamicHelpers.GetPythonTypeFromType(_event.Tracker.DeclaringType)) {
                        // mutating static event, only allow this from the type we're mutating, not sub-types
                        return new BadEventChange(_ownerType, _instance);
                    }
                }

                MethodInfo remove = _event.Tracker.GetCallableRemoveMethod();
                if (CompilerHelpers.IsVisible(remove)
                    || (remove.IsProtected() /*todo: validate current context is in family*/ )
                    || context.LanguageContext.DomainManager.Configuration.PrivateBinding) {
                    _event.Tracker.RemoveHandler(_instance, func, context.LanguageContext.EqualityComparer);
                } else {
                    throw new TypeErrorException("Cannot remove handler from a private event.");
                }
                return this;
            }
        }

        #endregion

        #region Private Helpers

        private class BadEventChange {
            private readonly PythonType/*!*/ _ownerType;
            private readonly object _instance;

            public BadEventChange(PythonType/*!*/ ownerType, object instance) {
                _ownerType = ownerType;
                _instance = instance;
            }

            public PythonType Owner {
                get {
                    return _ownerType;
                }
            }

            public object Instance {
                get {
                    return _instance;
                }
            }
        }

        private MissingMemberException/*!*/ ReadOnlyException(PythonType/*!*/ dt) {
            Assert.NotNull(dt);
            return new MissingMemberException(String.Format("attribute '{1}' of '{0}' object is read-only", dt.Name, _tracker.Name));
        }

        #endregion

        #region ICodeFormattable Members

        public string/*!*/ __repr__(CodeContext/*!*/ context) {
            return string.Format("<event# {0} on {1}>", Info.Name, Info.DeclaringType.Name);
        }

        #endregion
    }
}
