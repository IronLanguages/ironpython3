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
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using IronPython.Runtime.Operations;
using Microsoft.Scripting;
using Microsoft.Scripting.Generation;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

namespace IronPython.Runtime.Types {
    /// <summary>
    /// Provides access to non-default .NET indexers (aka properties w/ parameters).
    /// 
    /// C# doesn't support these, but both COM and VB.NET do.  The types dictionary
    /// gets populated w/a ReflectedGetterSetter indexer which is a descriptor.  Getting
    /// the descriptor returns a bound indexer.  The bound indexer supports indexing.
    /// We support multiple indexer parameters via expandable tuples.
    /// </summary>
    [PythonType("indexer#")]
    public sealed class ReflectedIndexer : ReflectedGetterSetter {
        private readonly object _instance;
        private readonly PropertyInfo/*!*/ _info;

        public ReflectedIndexer(PropertyInfo/*!*/ info, NameType nt, bool privateBinding)
            : base(new MethodInfo[] { info.GetGetMethod(privateBinding) }, new MethodInfo[] { info.GetSetMethod(privateBinding) }, nt) {
            Debug.Assert(info != null);
            
            _info = info;
        }

        public ReflectedIndexer(ReflectedIndexer from, object instance)
            : base(from) {
            _instance = instance;
            _info = from._info;
        }

        internal override bool TryGetValue(CodeContext context, object instance, PythonType owner, out object value) {
            value = new ReflectedIndexer(this, instance);
            return true;
        }

        internal override bool GetAlwaysSucceeds {
            get {
                return true;
            }
        }

        internal override Type DeclaringType {
            get { return _info.DeclaringType; }
        }

        public override PythonType PropertyType {
            [PythonHidden]
            get {
                return DynamicHelpers.GetPythonTypeFromType(_info.PropertyType);
            }
        }

        public override string __name__ {
            get { return _info.Name; }
        }

        #region Public APIs

        public bool SetValue(CodeContext context, SiteLocalStorage<CallSite<Func<CallSite, CodeContext, object, object[], object>>> storage, object[] keys, object value) {
            return CallSetter(context, storage, _instance, keys, value);
        }

        public object GetValue(CodeContext context, SiteLocalStorage<CallSite<Func<CallSite, CodeContext, object, object[], object>>> storage, object[] keys) {
            return CallGetter(context, storage, _instance, keys);
        }

        public new object __get__(CodeContext context, object instance, object owner) {
            object val;
            TryGetValue(context, instance, owner as PythonType, out val);
            return val;
        }

        public object this[SiteLocalStorage<CallSite<Func<CallSite, CodeContext, object, object[], object>>> storage, params object[] key] {
            get {
                return GetValue(DefaultContext.Default, storage, key);
            }
            set {
                if (!SetValue(DefaultContext.Default, storage, key, value)) {
                    throw PythonOps.AttributeErrorForReadonlyAttribute(DeclaringType.Name, __name__);
                }
            }
        }

        #endregion
    }

}
