// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Linq.Expressions;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Runtime.CompilerServices;
using System.Threading;

using Microsoft.Scripting;
using Microsoft.Scripting.Ast;
using Microsoft.Scripting.Runtime;

using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;
using IronPython.Runtime.Binding;

namespace IronPython.Runtime {
    /// <summary>
    /// Python module.  Stores classes, functions, and data.  Usually a module
    /// is created by importing a file or package from disk.  But a module can also
    /// be directly created by calling the module type and providing a name or
    /// optionally a documentation string.
    /// </summary>
    [PythonType("module"), DebuggerTypeProxy(typeof(PythonModule.DebugProxy)), DebuggerDisplay("module: {GetName()}")]
    public class PythonModule : IDynamicMetaObjectProvider, IPythonMembersList {
        private readonly PythonDictionary _dict;
        private Scope _scope;

        public PythonModule() {
            _dict = new PythonDictionary();
            if (GetType() != typeof(PythonModule) && this is IPythonObject) {
                // we should share the user dict w/ our dict.
                ((IPythonObject)this).ReplaceDict(_dict);
            }
        }

        /// <summary>
        /// Creates a new module backed by a Scope.  Used for creating modules for foreign Scope's.
        /// </summary>
        internal PythonModule(PythonContext context, Scope scope) {
            _dict = new PythonDictionary(new ScopeDictionaryStorage(context, scope));
            _scope = scope;
        }

        /// <summary>
        /// Creates a new PythonModule with the specified dictionary.
        /// 
        /// Used for creating modules for builtin modules which don't have any code associated with them.
        /// </summary>
        internal PythonModule(PythonDictionary dict) {
            _dict = dict;
        }

        public static PythonModule/*!*/ __new__(CodeContext/*!*/ context, PythonType/*!*/ cls, params object[]/*!*/ args\u00F8) {
            PythonModule res;
            if (cls == TypeCache.Module) {
                res = new PythonModule();
            } else if (cls.IsSubclassOf(TypeCache.Module)) {
                res = (PythonModule)cls.CreateInstance(context);
            } else {
                throw PythonOps.TypeError("{0} is not a subtype of module", cls.Name);
            }

            return res;
        }

        [StaticExtensionMethod]
        public static PythonModule/*!*/ __new__(CodeContext/*!*/ context, PythonType/*!*/ cls, [ParamDictionary]IDictionary<object, object> kwDict0, params object[]/*!*/ args\u00F8) {
            return __new__(context, cls, args\u00F8);
        }

        public void __init__(string name) {
            __init__(name, null);
        }

        public void __init__(string name, string doc) {
            _dict["__name__"] = name;
            _dict["__doc__"] = doc;
        }

        public object __getattribute__(CodeContext/*!*/ context, string name) {
            PythonTypeSlot slot;
            object res;
            if (GetType() != typeof(PythonModule) &&
                DynamicHelpers.GetPythonType(this).TryResolveSlot(context, name, out slot) &&
                slot.TryGetValue(context, this, DynamicHelpers.GetPythonType(this), out res)) {
                return res;
            }

            switch (name) {
                // never look in the dict for these...
                case "__dict__": return __dict__;
                case "__class__": return DynamicHelpers.GetPythonType(this);
            }

            if (_dict.TryGetValue(name, out res)) {
                return res;
            }

            // fall back to object to provide all of our other attributes (e.g. __setattr__, etc...)
            return ObjectOps.__getattribute__(context, this, name);
        }

        internal object GetAttributeNoThrow(CodeContext/*!*/ context, string name) {
            PythonTypeSlot slot;
            object res;
            if (GetType() != typeof(PythonModule) &&
                DynamicHelpers.GetPythonType(this).TryResolveSlot(context, name, out slot) &&
                slot.TryGetValue(context, this, DynamicHelpers.GetPythonType(this), out res)) {
                return res;
            }

            switch (name) {
                // never look in the dict for these...
                case "__dict__": return __dict__;
                case "__class__": return DynamicHelpers.GetPythonType(this);
            }

            if (_dict.TryGetValue(name, out res)) {
                return res;
            } else if (DynamicHelpers.GetPythonType(this).TryGetNonCustomMember(context, this, name, out res)) {
                return res;
            } else if (DynamicHelpers.GetPythonType(this).TryResolveSlot(context, "__getattr__", out slot) &&
                slot.TryGetValue(context, this, DynamicHelpers.GetPythonType(this), out res)) {
                return PythonCalls.Call(context, res, name);
            }

            return OperationFailed.Value;
        }

        public void __setattr__(CodeContext/*!*/ context, string name, object value) {
            PythonTypeSlot slot;
            if (GetType() != typeof(PythonModule) &&
                DynamicHelpers.GetPythonType(this).TryResolveSlot(context, name, out slot) &&
                slot.TrySetValue(context, this, DynamicHelpers.GetPythonType(this), value)) {
                return;
            }

            switch (name) {
                case "__dict__": throw PythonOps.AttributeError("readonly attribute");
                case "__class__": throw PythonOps.TypeError("__class__ assignment: only for heap types");
            }

            Debug.Assert(value != Uninitialized.Instance);

            _dict[name] = value;
        }

        public void __delattr__(CodeContext/*!*/ context, string name) {
            PythonTypeSlot slot;
            if (GetType() != typeof(PythonModule) &&
                DynamicHelpers.GetPythonType(this).TryResolveSlot(context, name, out slot) &&
                slot.TryDeleteValue(context, this, DynamicHelpers.GetPythonType(this))) {
                return;
            }

            switch (name) {
                case "__dict__": throw PythonOps.AttributeError("readonly attribute");
                case "__class__": throw PythonOps.TypeError("can't delete __class__ attribute");
            }

            object value;
            if (!_dict.TryRemoveValue(name, out value)) {
                throw PythonOps.AttributeErrorForMissingAttribute("module", name);
            }
        }

        public string/*!*/ __repr__() {
            return __str__();
        }

        public string/*!*/ __str__() {
            object fileObj, nameObj;
            if (!_dict.TryGetValue("__file__", out fileObj)) {
                fileObj = null;
            }
            if (!_dict._storage.TryGetName(out nameObj)) {
                nameObj = null;
            }

            string file = fileObj as string;
            string name = nameObj as string ?? "?";

            if (file == null) {
                return String.Format("<module '{0}' (built-in)>", name);
            }
            return String.Format("<module '{0}' from '{1}'>", name, file);
        }

        internal PythonDictionary __dict__ {
            get {
                return _dict;
            }
        }

        [SpecialName, PropertyMethod]
        public PythonDictionary Get__dict__() {
            return _dict;
        }

        [SpecialName, PropertyMethod]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic")]
        public void Set__dict__(object value) {
            throw PythonOps.AttributeError("readonly attribute");
        }

        [SpecialName, PropertyMethod]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic")]
        public void Delete__dict__() {
            throw PythonOps.AttributeError("readonly attribute");
        }

        public Scope Scope {
            get {
                if (_scope == null) {
                    Interlocked.CompareExchange(ref _scope, new Scope(new ObjectDictionaryExpando(_dict)), null);
                }

                return _scope;
            }
        }

        #region IDynamicMetaObjectProvider Members

        [PythonHidden] // needs to be public so that we can override it.
        public DynamicMetaObject GetMetaObject(Expression parameter) {
            return new MetaModule(this, parameter);
        }

        #endregion

        private class MetaModule : MetaPythonObject, IPythonGetable {
            public MetaModule(PythonModule module, Expression self)
                : base(self, BindingRestrictions.Empty, module) {
            }

            public override DynamicMetaObject BindGetMember(GetMemberBinder binder) {
                return GetMemberWorker(binder, PythonContext.GetCodeContextMO(binder));
            }

            #region IPythonGetable Members

            public DynamicMetaObject GetMember(PythonGetMemberBinder member, DynamicMetaObject codeContext) {
                return GetMemberWorker(member, codeContext);
            }

            #endregion

            private DynamicMetaObject GetMemberWorker(DynamicMetaObjectBinder binder, DynamicMetaObject codeContext) {
                string name = GetGetMemberName(binder);
                var tmp = Expression.Variable(typeof(object), "res");                

                return new DynamicMetaObject(
                    Expression.Block(
                        new[] { tmp },
                        Expression.Condition(
                            Expression.Call(
                                typeof(PythonOps).GetMethod(nameof(PythonOps.ModuleTryGetMember)),
                                PythonContext.GetCodeContext(binder),
                                Utils.Convert(Expression, typeof(PythonModule)),
                                Expression.Constant(name),
                                tmp
                            ),
                            tmp,
                            Expression.Convert(GetMemberFallback(this, binder, codeContext).Expression, typeof(object))
                        )
                    ),
                    BindingRestrictions.GetTypeRestriction(Expression, Value.GetType())
                );
            }

            public override DynamicMetaObject/*!*/ BindInvokeMember(InvokeMemberBinder/*!*/ action, DynamicMetaObject/*!*/[]/*!*/ args) {
                return BindingHelpers.GenericInvokeMember(action, null, this, args);
            }

            public override DynamicMetaObject BindSetMember(SetMemberBinder binder, DynamicMetaObject value) {
                Debug.Assert(value.Value != Uninitialized.Instance);

                return new DynamicMetaObject(
                    Expression.Block(
                        Expression.Call(
                            Utils.Convert(Expression, typeof(PythonModule)),
                            typeof(PythonModule).GetMethod(nameof(PythonModule.__setattr__)),
                            PythonContext.GetCodeContext(binder),
                            Expression.Constant(binder.Name),
                            Expression.Convert(value.Expression, typeof(object))
                        ),
                        Expression.Convert(value.Expression, typeof(object))
                    ),
                    BindingRestrictions.GetTypeRestriction(Expression, Value.GetType())
                );
            }

            public override DynamicMetaObject BindDeleteMember(DeleteMemberBinder binder) {
                return new DynamicMetaObject(
                    Expression.Call(
                        Utils.Convert(Expression, typeof(PythonModule)),
                        typeof(PythonModule).GetMethod(nameof(PythonModule.__delattr__)),
                        PythonContext.GetCodeContext(binder),
                        Expression.Constant(binder.Name)
                    ),
                    BindingRestrictions.GetTypeRestriction(Expression, Value.GetType())
                );
            }

            public override IEnumerable<string> GetDynamicMemberNames() {
                foreach (object o in ((PythonModule)Value).__dict__.Keys) {
                    if (o is string str) {
                        yield return str;
                    }
                }
            }
        }

        internal bool IsBuiltin => GetFile() is null;

        internal string GetFile() {
            object res;
            if (_dict.TryGetValue("__file__", out res)) {
                return res as string;
            }
            return null;
        }

        internal string GetName() {
            object res;
            if (_dict._storage.TryGetName(out res)) {
                return res as string;
            }
            return null;
        }

        #region IPythonMembersList Members

        IList<object> IPythonMembersList.GetMemberNames(CodeContext context) {
            return new List<object>(__dict__.Keys);
        }

        #endregion

        #region IMembersList Members

        IList<string> IMembersList.GetMemberNames() {
            List<string> res = new List<string>(__dict__.Keys.Count);
            foreach (object o in __dict__.Keys) {
                if (o is string strKey) {
                    res.Add(strKey);
                }
            }

            return res;
        }

        #endregion
        
        internal class DebugProxy {
            private readonly PythonModule _module;

            public DebugProxy(PythonModule module) {
                _module = module;
            }

            [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
            public List<ObjectDebugView> Members {
                get {
                    var res = new List<ObjectDebugView>();
                    foreach (var v in _module._dict) {
                        res.Add(new ObjectDebugView(v.Key, v.Value));
                    }
                    return res;
                }
            }
        }

        
    }
}
