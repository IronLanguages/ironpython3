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

using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;

using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

namespace IronPython.Runtime {
    [PythonType("super")]
    public class Super : PythonTypeSlot, ICodeFormattable {
        private PythonType _thisClass;
        private object _self;
        private object _selfClass;

        public Super() {
        }

        #region Python Public API Surface

        public void __init__(PythonType type) {
            __init__(type, null);
        }

        public void __init__(PythonType type, object obj) {
            if (obj != null) {
                PythonType dt = obj as PythonType;
                if (PythonOps.IsInstance(obj, type)) {
                    this._thisClass = type;
                    this._self = obj;
                    this._selfClass = DynamicHelpers.GetPythonType(obj);
                } else if (dt != null && dt.IsSubclassOf(type)) {
                    this._thisClass = type;
                    this._selfClass = obj;
                    this._self = obj;
                } else {
                    throw PythonOps.TypeError("super(type, obj): obj must be an instance or subtype of type {1}, not {0}", PythonTypeOps.GetName(obj), type.Name);
                }
            } else {
                this._thisClass = type;
                this._self = null;
                this._selfClass = null;
            }
        }

        public PythonType __thisclass__ {
            get { return _thisClass; }
        }

        public object __self__ {
            get { return _self; }
        }

        public object __self_class__ {
            get { return _selfClass; }
        }

        public new object __get__(CodeContext/*!*/ context, object instance, object owner) {
            PythonType selfType = PythonType;

            if (selfType == TypeCache.Super) {
                Super res = new Super();
                res.__init__(_thisClass, instance);
                return res;
            }

            return PythonCalls.Call(context, selfType, _thisClass, instance);
        }

        #endregion
        
        #region Custom member access

        [SpecialName]
        public object GetCustomMember(CodeContext context, string name) {
            // first find where we are in the mro...
            PythonType mroType = _selfClass as PythonType;

            object value;
            if (mroType != null) { // can be null if the user does super.__new__
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
                object self = _self == _selfClass ? null : _self;

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
        public void SetMember(CodeContext context, string name, object value) {
            PythonType.SetMember(context, this, name, value);
        }

        [SpecialName]
        public void DeleteCustomMember(CodeContext context, string name) {
            PythonType.DeleteMember(context, this, name);
        }

        private bool TryLookupInBase(CodeContext context, PythonType pt, string name, object self, out object value) {
            PythonTypeSlot dts;

            if (pt.OldClass == null) {
                // new-style class, or reflected type, lookup slot
                if (pt.TryLookupSlot(context, name, out dts) && 
                    dts.TryGetValue(context, self, DescriptorContext, out value)) {
                    return true;
                }
            } else {
                // old-style class, lookup attribute                
                OldClass dt = pt.OldClass;

                if (PythonOps.TryGetBoundAttr(context, dt, name, out value)) {
                    value = OldClass.GetOldStyleDescriptor(context, value, self, DescriptorContext);
                    return true;
                }
            }
            value = null;
            return false;
        }

        private PythonType DescriptorContext {
            get {
                if (!DynamicHelpers.GetPythonType(_self).IsSubclassOf(_thisClass)) {
                    if(_self == _selfClass) // Using @classmethod
                        return _selfClass as PythonType ?? _thisClass;
                    return _thisClass;
                }

                PythonType dt = _selfClass as PythonType;
                if (dt != null) return dt;

                return ((OldClass)_selfClass).TypeObject;
            }
        }

        // TODO needed because ICustomMembers is too hard to implement otherwise.  Let's fix that and get rid of this.
        private PythonType PythonType {
            get {
                if (GetType() == typeof(Super))
                    return TypeCache.Super;

                IPythonObject sdo = this as IPythonObject;
                Debug.Assert(sdo != null);

                return sdo.PythonType;
            }
        }

        #endregion

        internal override bool TryGetValue(CodeContext context, object instance, PythonType owner, out object value) {
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
            return string.Format("<{0}: {1}, {2}>", PythonTypeOps.GetName(this), PythonOps.Repr(context, _thisClass), selfRepr);
        }

        #endregion
    }
}
