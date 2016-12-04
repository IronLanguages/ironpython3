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
using Microsoft.Scripting.Runtime;
using IronPython.Runtime.Operations;

namespace IronPython.Runtime.Types {
    [PythonType("getset_descriptor")]
    public sealed class PythonTypeWeakRefSlot : PythonTypeSlot, ICodeFormattable {
        PythonType _type;

        public PythonTypeWeakRefSlot(PythonType parent) {
            this._type = parent;
        }

        internal override bool TryGetValue(CodeContext context, object instance, PythonType owner, out object value) {
            if (instance == null) {
                value = this;
                return true;
            }

            IWeakReferenceable reference;
            if (context.GetPythonContext().TryConvertToWeakReferenceable(instance, out reference)) {
                WeakRefTracker tracker = reference.GetWeakRef();
                if (tracker == null || tracker.HandlerCount == 0) {
                    value = null;
                } else {
                    value = tracker.GetHandlerCallback(0);
                }
                return true;
            }

            value = null;
            return false;
        }

        internal override bool TrySetValue(CodeContext context, object instance, PythonType owner, object value) {
            IWeakReferenceable reference;
            if (context.GetPythonContext().TryConvertToWeakReferenceable(instance, out reference)) {
                return reference.SetWeakRef(new WeakRefTracker(reference, value, instance));
            }
            return false;
        }

        internal override bool TryDeleteValue(CodeContext context, object instance, PythonType owner) {
            throw PythonOps.TypeError("__weakref__ attribute cannot be deleted");
        }
       
        public override string ToString() {
            return String.Format("<attribute '__weakref__' of '{0}' objects>", _type.Name);
        }

        #region ICodeFormattable Members

        public string/*!*/ __repr__(CodeContext/*!*/ context) {
            return String.Format("<attribute '__weakref__' of {0} objects",
                PythonOps.Repr(context, _type));
        }

        #endregion
    }
}
