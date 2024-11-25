// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

using Microsoft.Scripting.Runtime;

namespace IronPython.Runtime {
    [PythonType("super")]
    public class Super : PythonTypeSlot, ICodeFormattable {
        [DisallowNull]
        private PythonType? _thisClass; // set during __init__
        private object? _self;
        private object? _selfClass;

        public Super() {
        }

        #region Python Public API Surface

        public void __init__(CodeContext context) {
            var vars = context.Dict._storage as RuntimeVariablesDictionaryStorage;

            // Step 1: access arg[0]
            if (vars is null || vars.Arg0Idx < 0) {
                throw PythonOps.RuntimeError("super(): no arguments");
            }

            object? arg0 = vars.GetCell(vars.Arg0Idx).Value;
            if (arg0 == Uninitialized.Instance) {
                throw PythonOps.RuntimeError("super(): arg[0] deleted");
            }

            // Step 2: access __class__ cell
            int idx = Array.IndexOf(vars.Names, "__class__");
            if (idx < 0 || idx >= vars.NumFreeVars) {
                throw PythonOps.RuntimeError("super(): __class__ cell not found");
            }

            object? cls = vars.GetCell(idx).Value;
            if (cls == Uninitialized.Instance) {
                throw PythonOps.RuntimeError("super(): empty __class__ cell");
            }
            if (cls is not PythonType type) {
                throw PythonOps.RuntimeError("super(): __class__ is not a type ({0})", PythonOps.GetPythonTypeName(cls));
            }

            __init__(type, arg0);
        }

        public void __init__([NotNone] PythonType type) {
            __init__(type, null);
        }

        public void __init__([NotNone] PythonType type, object? obj) {
            if (type is null) throw new ArgumentNullException(nameof(type));

            if (obj is not null) {
                if (PythonOps.IsInstance(obj, type)) {
                    _thisClass = type;
                    _self = obj;
                    _selfClass = DynamicHelpers.GetPythonType(obj);
                } else if (obj is PythonType dt && dt.IsSubclassOf(type)) {
                    _thisClass = type;
                    _selfClass = obj;
                    _self = obj;
                } else {
                    throw PythonOps.TypeError("super(type, obj): obj must be an instance or subtype of type {1}, not {0}", PythonOps.GetPythonTypeName(obj), type.Name);
                }
            } else {
                _thisClass = type;
                _self = null;
                _selfClass = null;
            }
        }

        public PythonType? __thisclass__ {
            get { return _thisClass; }
        }

        public object? __self__ {
            get { return _self; }
        }

        public object? __self_class__ {
            get { return _selfClass; }
        }

        public new object? __get__(CodeContext/*!*/ context, object? instance, object? owner = null) {
            if (instance is null && owner is null) {
                throw PythonOps.TypeError("__get__(None, None) is invalid");
            }

            PythonType selfType = PythonType;

            if (selfType == TypeCache.Super) {
                if (_thisClass is null) {
                    throw PythonOps.TypeError("super(): __init__ not called");
                }
                Super res = new Super();
                res.__init__(_thisClass, instance);
                return res;
            }

            return PythonCalls.Call(context, selfType, _thisClass, instance);
        }

        #endregion

        #region Custom member access

        [SpecialName]
        public object GetCustomMember(CodeContext context, [NotNone] string name) {
            // first find where we are in the mro...
            object? value;
            if (_selfClass is PythonType mroType && _thisClass is not null) { // can be null if the user does super.__new__
                IList<PythonType> mro = mroType.ResolutionOrder;

                int lookupType;
                bool foundThis = false;
                for (lookupType = 0; lookupType < mro.Count; lookupType++) {
                    if (mro[lookupType] == _thisClass) {
                        foundThis = true;
                        break;
                    }
                }

                if (!foundThis) {
                    // __self__ is not a subclass of __thisclass__, we need to
                    // search __thisclass__'s mro and return a method from one
                    // of it's bases.
                    lookupType = 0;
                    mro = _thisClass.ResolutionOrder;
                }

                // if we're super on a class then we have no self.
                object? self = _self == _selfClass ? null : _self;

                // then skip our class, and lookup in everything
                // above us until we get a hit.
                lookupType++;
                while (lookupType < mro.Count) {

                    if (TryLookupInBase(context, mro[lookupType], name, self, out value))
                        return value;

                    lookupType++;
                }
            }

            if (PythonType.TryGetBoundMember(context, this, name, out value)) {
                return value;
            }

            return OperationFailed.Value;
        }

        [SpecialName]
        public void SetMember(CodeContext context, [NotNone] string name, object? value) {
            PythonType.SetMember(context, this, name, value);
        }

        [SpecialName]
        public void DeleteCustomMember(CodeContext context, [NotNone] string name) {
            PythonType.DeleteMember(context, this, name);
        }

        private bool TryLookupInBase(CodeContext context, PythonType pt, string name, object? self, [NotNullWhen(true)] out object? value) {
            // new-style class, or reflected type, lookup slot
            if (pt.TryLookupSlot(context, name, out PythonTypeSlot dts) &&
                dts.TryGetValue(context, self, DescriptorContext, out value)) {
                return true;
            }
            value = null;
            return false;
        }

        private PythonType? DescriptorContext {
            get {
                if (!DynamicHelpers.GetPythonType(_self).IsSubclassOf(_thisClass)) {
                    if (_self == _selfClass) // Using @classmethod
                        return _selfClass as PythonType ?? _thisClass;
                    return _thisClass;
                }

                return _selfClass as PythonType;
            }
        }

        // TODO needed because ICustomMembers is too hard to implement otherwise.  Let's fix that and get rid of this.
        private PythonType PythonType {
            get {
                if (GetType() == typeof(Super))
                    return TypeCache.Super;

                Debug.Assert(this is IPythonObject);
                IPythonObject sdo = (IPythonObject)this;

                return sdo.PythonType;
            }
        }

        #endregion

        internal override bool TryGetValue(CodeContext context, object? instance, PythonType? owner, out object? value) {
            value = __get__(context, instance, owner);
            return true;
        }

        internal override bool GetAlwaysSucceeds {
            get {
                return true;
            }
        }

        #region ICodeFormattable Members

        public string __repr__(CodeContext context) {
            string selfRepr;
            if (_self == this)
                selfRepr = "<super object>";
            else
                selfRepr = PythonOps.Repr(context, _self);
            return string.Format("<{0}: {1}, {2}>", PythonOps.GetPythonTypeName(this), PythonOps.Repr(context, _thisClass), selfRepr);
        }

        #endregion
    }
}
