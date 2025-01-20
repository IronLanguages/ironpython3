// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#if FEATURE_REFEMIT

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Reflection;
using System.Runtime.CompilerServices;

using Microsoft.Scripting;
using Microsoft.Scripting.Ast;
using Microsoft.Scripting.Generation;
using Microsoft.Scripting.Interpreter;
using Microsoft.Scripting.Utils;

using IronPython.Runtime;

using MSAst = System.Linq.Expressions;
using AstUtils = Microsoft.Scripting.Ast.Utils;


namespace IronPython.Compiler.Ast {
    using Ast = MSAst.Expression;

    /// <summary>
    /// Implements globals which are backed by a static type, followed by an array if the static types' slots become full.  The global
    /// variables are stored in static fields on a type for fast access.  The type also includes fields for constants and call sites
    /// so they can be accessed much fasetr.
    /// 
    /// We don't generate any code into the type though - DynamicMethod's are much faster for code gen then normal ref emit.
    /// </summary>
    internal partial class UncollectableCompilationMode : CompilationMode {
        private static readonly Dictionary<object/*!*/, ConstantInfo/*!*/>/*!*/ _allConstants = new Dictionary<object/*!*/, ConstantInfo/*!*/>();
        private static readonly Dictionary<Type/*!*/, DelegateCache/*!*/>/*!*/ _delegateCache = new Dictionary<Type/*!*/, DelegateCache/*!*/>();

        public UncollectableCompilationMode() {
        }

        public override LightLambdaExpression ReduceAst(PythonAst instance, string name) {
            return Utils.LightLambda<Func<FunctionCode, object>>(typeof(object), AstUtils.Convert(instance.ReduceWorker(), typeof(object)), name, new[] { PythonAst._functionCode });
        }

        #region Cached Constant/Symbol/Global Support

        public override MSAst.Expression/*!*/ GetConstant(object value) {
            // if we can emit the value and we won't be continiously boxing/unboxing
            // then don't bother caching the value in a static field.
            
            // TODO: Sometimes we don't want to pre-box the values, such as if it's an int
            // going to a call site which can be strongly typed.  We need to coordinate 
            // more with whoever consumes the values.
            if (CompilerHelpers.CanEmitConstant(value, CompilerHelpers.GetType(value)) &&
                !CompilerHelpers.GetType(value).IsValueType) {
                return Utils.Constant(value);
            }

            ConstantInfo ci;
            lock (_allConstants) {
                if (!_allConstants.TryGetValue(value, out ci)) {
                    _allConstants[value] = ci = NextConstant(_allConstants.Count, value);
                    PublishConstant(value, ci);
                }
            }

            return ci.Expression;
        }

        public override Type GetConstantType(object value) {
            if (value == null || value.GetType().IsValueType) {
                return typeof(object);
            }

            return value.GetType();
        }

        public override MSAst.Expression GetGlobal(MSAst.Expression globalContext, int arrayIndex, PythonVariable variable, PythonGlobal/*!*/ global) {
            Assert.NotNull(global);

            lock (StorageData.Globals) {
                ConstantInfo info = NextGlobal(0);

                StorageData.GlobalStorageType(StorageData.GlobalCount + 1);

                PublishWorker(StorageData.GlobalCount, StorageData.GlobalTypes, info, global, StorageData.Globals);

                StorageData.GlobalCount += 1;

                return new PythonGlobalVariableExpression(info.Expression, variable, global);
            }
        }

        public override Type DelegateType {
            get {
                return typeof(MSAst.Expression<Func<FunctionCode, object>>);
            }
        }

        #endregion

        #region Field Allocation and Publishing

        public override UncollectableCompilationMode.ConstantInfo GetContext() {
            lock (StorageData.Contexts) {
                int index = StorageData.ContextCount++;
                int arrIndex = index - StorageData.ContextTypes * StorageData.StaticFields;
                Type storageType = StorageData.ContextStorageType(index);

                MSAst.Expression expr;
                FieldInfo fieldInfo;
                if (arrIndex < 0) {
                    fieldInfo = storageType.GetField(string.Format("Context{0:000}", index % StorageData.StaticFields));
                    expr = Ast.Field(null, fieldInfo);
                } else {
                    fieldInfo = typeof(StorageData).GetField(nameof(StorageData.Contexts));
                    expr = Ast.ArrayIndex(
                        Ast.Field(null, fieldInfo),
                        Ast.Constant(arrIndex, typeof(int))
                    );
                }

                return new ConstantInfo(new CodeContextExpression(expr), fieldInfo, index);
            }
        }

        private class CodeContextExpression : MSAst.Expression, IInstructionProvider {
            private readonly MSAst.Expression _expression;
            public CodeContext Context;

            public CodeContextExpression(MSAst.Expression expression) {
                _expression = expression;
            }

            public override MSAst.Expression Reduce() {
                return _expression;
            }

            public override bool CanReduce {
                get {
                    return true;
                }
            }

            public override Type Type {
                get {
                    return typeof(CodeContext);
                }
            }

            public override MSAst.ExpressionType NodeType {
                get {
                    return MSAst.ExpressionType.Extension;
                }
            }

            #region IInstructionProvider Members

            public void AddInstructions(LightCompiler compiler) {
                compiler.Instructions.EmitLoad(Context);
            }

            #endregion
        }

        private static ConstantInfo/*!*/ NextConstant(int offset, object value) {
            return new ConstantInfo(new ConstantExpression(offset, value), null, offset);
        }

        private static ConstantInfo/*!*/ NextGlobal(int offset) {
            return new ConstantInfo(new GlobalExpression(offset), null, offset);
        }

        // public for accessibility via GetMethod("NextSite")
        public static SiteInfo/*!*/ NextSite<T>(DynamicMetaObjectBinder/*!*/ binder) where T : class {
            lock (StorageData.SiteLockObj) {
                int index = SiteStorage<T>.SiteCount++;
                
                int arrIndex = index - StorageData.SiteTypes * StorageData.StaticFields;
                Type storageType = SiteStorage<T>.SiteStorageType(index);

                MSAst.Expression expr;
                FieldInfo fieldInfo;
                if (arrIndex < 0) {
                    fieldInfo = storageType.GetField(string.Format("Site{0:000}", index % StorageData.StaticFields));
                    expr = Ast.Field(null, fieldInfo);
                } else {
                    fieldInfo = typeof(SiteStorage<T>).GetField("Sites");
                    expr = Ast.ArrayIndex(
                        Ast.Field(null, fieldInfo),
                        Ast.Constant(arrIndex, typeof(int))
                    );
                }

                return PublishSite(new SiteInfo<T>(binder, expr, fieldInfo, index));
            }
        }

        // Note: This should stay non-public to avoid name conflicts when accessing the
        //       generic overload via GetMethod("NextSite")
        private static SiteInfo/*!*/ NextSite(DynamicMetaObjectBinder/*!*/ binder, Type/*!*/ delegateType) {
            Type siteType = typeof(SiteStorage<>).MakeGenericType(delegateType);

            lock (StorageData.SiteLockObj) {
                int index = (int)siteType.GetField("SiteCount").GetValue(null);
                siteType.GetField("SiteCount").SetValue(null, index + 1);
                int arrIndex = index - StorageData.SiteTypes * StorageData.StaticFields;
                Type storageType = (Type)siteType.GetMethod("SiteStorageType").Invoke(null, new object[] { index });

                MSAst.Expression expr;
                FieldInfo fieldInfo;
                if (arrIndex < 0) {
                    fieldInfo = storageType.GetField(string.Format("Site{0:000}", index % StorageData.StaticFields));
                    expr = Ast.Field(null, fieldInfo);
                } else {
                    fieldInfo = siteType.GetField("Sites");
                    expr = Ast.ArrayIndex(
                        Ast.Field(null, fieldInfo),
                        Ast.Constant(arrIndex, typeof(int))
                    );
                }

                return PublishSite(new SiteInfoLarge(binder, expr, fieldInfo, index, delegateType));
            }
        }

        public override void PublishContext(CodeContext/*!*/ context, ConstantInfo/*!*/ codeContextInfo) {
            int arrIndex = codeContextInfo.Offset - StorageData.ContextTypes * StorageData.StaticFields;

            if (arrIndex < 0) {
                codeContextInfo.Field.SetValue(null, context);
            } else {
                lock (StorageData.Contexts) {
                    StorageData.Contexts[arrIndex] = context;
                }
            }

            ((CodeContextExpression)codeContextInfo.Expression).Context = context;
        }
        
        private static void PublishConstant(object constant, ConstantInfo info) {
            StorageData.ConstantStorageType(info.Offset);

            PublishWorker(0, StorageData.ConstantTypes, info, constant, StorageData.Constants);
        }

        private static SiteInfo PublishSite(SiteInfo si) {
            int arrIndex = si.Offset - StorageData.SiteTypes * StorageData.StaticFields;
            CallSite site = si.MakeSite();

            if (arrIndex < 0) {
                si.Field.SetValue(null, site);
            } else {
                lock (StorageData.SiteLockObj) {
                    ((CallSite[])si.Field.GetValue(null))[arrIndex] = site;
                }
            }

            return si;
        }

        private static void PublishWorker<T>(int start, int nTypes, ConstantInfo info, T value, T[] fallbackArray) {
            int arrIndex = start + info.Offset - nTypes * StorageData.StaticFields;
            ((ReducibleExpression)info.Expression).Start = start;

            if (arrIndex < 0) {
                ((ReducibleExpression)info.Expression).FieldInfo.SetValue(null, value);
            } else {
                fallbackArray[arrIndex] = value;
            }
        }

        #endregion

        #region Dynamic CallSite Type Information

        private sealed class DelegateCache {
            public Type/*!*/ DelegateType;
            public Type/*!*/ SiteType;

            public Func<DynamicMetaObjectBinder/*!*/, SiteInfo/*!*/>/*!*/ NextSite;
            public FieldInfo/*!*/ TargetField; // SiteType.GetField("Target")
            public MethodInfo/*!*/ InvokeMethod; // DelegateType.GetMethod("Invoke")

            public Dictionary<Type/*!*/, DelegateCache> TypeChain;

            public void MakeDelegateType(Type/*!*/ retType, params MSAst.Expression/*!*/[]/*!*/ args) {
                DelegateType = GetDelegateType(retType, args);
                SiteType = typeof(CallSite<>).MakeGenericType(DelegateType);
                NextSite = (Func<DynamicMetaObjectBinder, SiteInfo>)
                    typeof(UncollectableCompilationMode).GetMethod(nameof(UncollectableCompilationMode.NextSite)).MakeGenericMethod(DelegateType).
                        CreateDelegate(typeof(Func<DynamicMetaObjectBinder, SiteInfo>)
                );
                TargetField = SiteType.GetField("Target");
                InvokeMethod = DelegateType.GetMethod("Invoke");
            }

            public static DelegateCache FirstCacheNode(Type/*!*/ argType) {
                DelegateCache nextCacheNode;
                if (!_delegateCache.TryGetValue(argType, out nextCacheNode)) {
                    nextCacheNode = new DelegateCache();
                    _delegateCache[argType] = nextCacheNode;
                }

                return nextCacheNode;
            }

            public DelegateCache NextCacheNode(Type/*!*/ argType) {
                Assert.NotNull(argType);
                
                DelegateCache nextCacheNode;
                if (TypeChain == null) {
                    TypeChain = new Dictionary<Type, DelegateCache>();
                }

                if (!TypeChain.TryGetValue(argType, out nextCacheNode)) {
                    nextCacheNode = new DelegateCache();
                    TypeChain[argType] = nextCacheNode;
                }

                return nextCacheNode;
            }
        }

        #endregion

        #region Reducible Expressions

        internal abstract class ReducibleExpression : MSAst.Expression {
            private readonly int _offset;
            private int _start = -1;
            private FieldInfo _fieldInfo;
            
            public ReducibleExpression(int offset) {
                _offset = offset;
            }

            public abstract string/*!*/ Name { get; }
            public abstract int FieldCount { get; }
            public abstract override Type/*!*/ Type { get; }
            protected abstract Type/*!*/ GetStorageType(int index);
            
            public FieldInfo FieldInfo {
                get {
                    return _fieldInfo;
                }
            }

            // Note: Because of a call to GetStorageType, which possibly resizes a storage
            //       array, a lock must be acquired prior to setting this property.
            public int Start {
                get {
                    return _start;
                }
                set {
                    Debug.Assert(_start < 0); // setter should only be called once
                    Debug.Assert(value >= 0);

                    _start = value;
                    int index = _offset + _start;
                    Type storageType = GetStorageType(index);
                    _fieldInfo = storageType != typeof(StorageData)
                        ? storageType.GetField(
                            Name +
                            $"{index % StorageData.StaticFields:000}")
                        : typeof(StorageData).GetField(Name + "s");
                }
            }

            public override MSAst.Expression/*!*/ Reduce() {
                Debug.Assert(_start >= 0);
                Assert.NotNull(_fieldInfo);

                int index = _offset + _start;
                int arrIndex = index - FieldCount;
                if (arrIndex < 0) {
                    return Ast.Field(null, _fieldInfo);
                } else {
                    return Ast.ArrayIndex(
                        Ast.Field(null, _fieldInfo),
                        Ast.Constant(arrIndex, typeof(int))
                    );
                }
            }

            public override MSAst.ExpressionType NodeType {
                get {
                    return MSAst.ExpressionType.Extension;
                }
            }

            protected override MSAst.Expression Accept(MSAst.ExpressionVisitor visitor) {
                return this;
            }

            protected override MSAst.Expression VisitChildren(MSAst.ExpressionVisitor visitor) {
                return this;
            }

            public override bool CanReduce {
                get {
                    return true;
                }
            }
        }

        internal sealed class ConstantExpression : ReducibleExpression {
            private object _value;

            public ConstantExpression(int offset, object value) : base(offset) {
                _value = value;
            }

            public override string/*!*/ Name {
                get { return "Constant"; }
            }

            public override int FieldCount {
                get { return StorageData.ConstantTypes * StorageData.StaticFields; }
            }

            protected override Type/*!*/ GetStorageType(int index) {
                return StorageData.ConstantStorageType(index);
            }

            public override Type/*!*/ Type {
                get {
                    Type returnType = _value.GetType();
                    if (!returnType.IsValueType) {
                        return returnType;
                    } else {
                        return typeof(object);
                    }                    
                }
            }

            public object Value {
                get {
                    return _value;
                }
            }

            public override MSAst.Expression Reduce() {
                if (_value.GetType().IsValueType) {
                    return base.Reduce();
                } else {
                    return MSAst.Expression.Convert(base.Reduce(), _value.GetType());
                }
            }
        }

        internal sealed class GlobalExpression : ReducibleExpression {
            public GlobalExpression(int offset) : base(offset) { }

            public override string/*!*/ Name {
                get { return "Global"; }
            }

            public override int FieldCount {
                get { return StorageData.GlobalTypes * StorageData.StaticFields; }
            }

            protected override Type/*!*/ GetStorageType(int index) {
                return StorageData.GlobalStorageType(index);
            }

            public override Type/*!*/ Type {
                get { return typeof(PythonGlobal); }
            }
        }

        #endregion
    }
}
#endif
