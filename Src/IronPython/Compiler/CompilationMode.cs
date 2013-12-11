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

#if FEATURE_CORE_DLR
using MSAst = System.Linq.Expressions;
#else
using MSAst = Microsoft.Scripting.Ast;
#endif


using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Runtime.CompilerServices;

using Microsoft.Scripting;
using Microsoft.Scripting.Ast;
using Microsoft.Scripting.Utils;

using IronPython.Compiler.Ast;
using IronPython.Runtime;
using System.Collections;
using System.Reflection;

namespace IronPython.Compiler {
    public delegate object LookupCompilationDelegate(CodeContext context, FunctionCode code);

    /// <summary>
    /// Specifies the compilation mode which will be used during the AST transformation
    /// </summary>
    [Serializable]
    internal abstract class CompilationMode {
#if FEATURE_REFEMIT
        /// <summary>
        /// Compilation will proceed in a manner in which the resulting AST can be serialized to disk.
        /// </summary>
        public static readonly CompilationMode ToDisk = new ToDiskCompilationMode();
        
        /// <summary>
        /// Compilation will use a type and declare static fields for globals.  The resulting type
        /// is uncollectible and therefore extended use of this will cause memory leaks.
        /// </summary>
        public static readonly CompilationMode Uncollectable = new UncollectableCompilationMode();
#endif
        
        /// <summary>
        /// Compilation will use an array for globals.  The resulting code will be fully collectible
        /// and once all references are released will be collected.
        /// </summary>
        public static readonly CompilationMode Collectable = new CollectableCompilationMode();
        
        /// <summary>
        /// Compilation will force all global accesses to do a full lookup.  This will also happen for
        /// any unbound local references.  This is the slowest form of code generation and is only
        /// used for exec/eval code where we can run against an arbitrary dictionary.
        /// </summary>
        public static readonly CompilationMode Lookup = new LookupCompilationMode();

        public virtual ScriptCode MakeScriptCode(PythonAst ast) {
            return new RuntimeScriptCode(ast, ast.ModuleContext.GlobalContext);
        }

        public virtual MSAst.Expression GetConstant(object value) {
            return MSAst.Expression.Constant(value);
        }

        public virtual Type GetConstantType(object value) {
            if (value == null) {
                return typeof(object);
            }

            return value.GetType();
        }

        public virtual void PrepareScope(PythonAst ast, ReadOnlyCollectionBuilder<MSAst.ParameterExpression> locals, List<MSAst.Expression> init) {
        }

        public virtual Type DelegateType {
            get {
                return typeof(MSAst.Expression<LookupCompilationDelegate>);
            }
        }

        public virtual ConstantInfo GetContext() {
            return null;
        }

        public virtual void PublishContext(CodeContext codeContext, ConstantInfo _contextInfo) {
        }

        public MSAst.Expression/*!*/ Dynamic(DynamicMetaObjectBinder/*!*/ binder, Type/*!*/ retType, MSAst.Expression/*!*/ arg0) {
            if (retType == typeof(object)) {
                return new PythonDynamicExpression1(binder, this, arg0);
            } else if (retType == typeof(bool)) {
                return new PythonDynamicExpression1<bool>(binder, this, arg0);
            }

            return ReduceDynamic(binder, retType, arg0);
        }

        public MSAst.Expression/*!*/ Dynamic(DynamicMetaObjectBinder/*!*/ binder, Type/*!*/ retType, MSAst.Expression/*!*/ arg0, MSAst.Expression/*!*/ arg1) {
            if (retType == typeof(object)) {
                return new PythonDynamicExpression2(binder, this, arg0, arg1);
            } else if (retType == typeof(bool)) {
                return new PythonDynamicExpression2<bool>(binder, this, arg0, arg1);
            }

            return ReduceDynamic(binder, retType, arg0, arg1);
        }

        public MSAst.Expression/*!*/ Dynamic(DynamicMetaObjectBinder/*!*/ binder, Type/*!*/ retType, MSAst.Expression/*!*/ arg0, MSAst.Expression/*!*/ arg1, MSAst.Expression/*!*/ arg2) {
            if (retType == typeof(object)) {
                return new PythonDynamicExpression3(binder, this, arg0, arg1, arg2);
            }
            return ReduceDynamic(binder, retType, arg0, arg1, arg2);
        }

        public MSAst.Expression/*!*/ Dynamic(DynamicMetaObjectBinder/*!*/ binder, Type/*!*/ retType, MSAst.Expression/*!*/ arg0, MSAst.Expression/*!*/ arg1, MSAst.Expression/*!*/ arg2, MSAst.Expression/*!*/ arg3) {
            if (retType == typeof(object)) {
                return new PythonDynamicExpression4(binder, this, arg0, arg1, arg2, arg3);
            }
            return ReduceDynamic(binder, retType, arg0, arg1, arg2, arg3);
        }

        public MSAst.Expression/*!*/ Dynamic(DynamicMetaObjectBinder/*!*/ binder, Type/*!*/ retType, MSAst.Expression/*!*/[]/*!*/ args) {
            Assert.NotNull(binder, retType, args);
            Assert.NotNullItems(args);
            
            switch (args.Length) {
                case 1: return Dynamic(binder, retType, args[0]);
                case 2: return Dynamic(binder, retType, args[0], args[1]);
                case 3: return Dynamic(binder, retType, args[0], args[1], args[2]);
                case 4: return Dynamic(binder, retType, args[0], args[1], args[2], args[3]);
            }

            if (retType == typeof(object)) {
                return new PythonDynamicExpressionN(binder, this, args);
            }

            return ReduceDynamic(binder, retType, args);
        }

        public virtual MSAst.Expression/*!*/ ReduceDynamic(DynamicMetaObjectBinder/*!*/ binder, Type/*!*/ retType, MSAst.Expression/*!*/ arg0) {
            return MSAst.DynamicExpression.Dynamic(binder, retType, arg0);
        }

        public virtual MSAst.Expression/*!*/ ReduceDynamic(DynamicMetaObjectBinder/*!*/ binder, Type/*!*/ retType, MSAst.Expression/*!*/ arg0, MSAst.Expression/*!*/ arg1) {
            return MSAst.DynamicExpression.Dynamic(binder, retType, arg0, arg1);
        }

        public virtual MSAst.Expression/*!*/ ReduceDynamic(DynamicMetaObjectBinder/*!*/ binder, Type/*!*/ retType, MSAst.Expression/*!*/ arg0, MSAst.Expression/*!*/ arg1, MSAst.Expression/*!*/ arg2) {
            return MSAst.DynamicExpression.Dynamic(binder, retType, arg0, arg1, arg2);
        }

        public virtual MSAst.Expression/*!*/ ReduceDynamic(DynamicMetaObjectBinder/*!*/ binder, Type/*!*/ retType, MSAst.Expression/*!*/ arg0, MSAst.Expression/*!*/ arg1, MSAst.Expression/*!*/ arg2, MSAst.Expression/*!*/ arg3) {
            return MSAst.DynamicExpression.Dynamic(binder, retType, arg0, arg1, arg2, arg3);
        }

        public virtual MSAst.Expression/*!*/ ReduceDynamic(DynamicMetaObjectBinder/*!*/ binder, Type/*!*/ retType, MSAst.Expression/*!*/[]/*!*/ args) {
            Assert.NotNull(binder, retType, args);
            Assert.NotNullItems(args);

            return MSAst.DynamicExpression.Dynamic(binder, retType, args);
        }

        public abstract MSAst.Expression GetGlobal(MSAst.Expression globalContext, int arrayIndex, PythonVariable variable, PythonGlobal global);

        public abstract LightLambdaExpression ReduceAst(PythonAst instance, string name);

        #region ConstantInfo

        public class ConstantInfo {
            public readonly MSAst.Expression/*!*/ Expression;
            public readonly FieldInfo Field;
            public readonly int Offset;

            public ConstantInfo(MSAst.Expression/*!*/ expr, FieldInfo field, int offset) {
                Assert.NotNull(expr);

                Expression = expr;
                Field = field;
                Offset = offset;
            }
        }

        public abstract class SiteInfo : ConstantInfo {
            public readonly DynamicMetaObjectBinder/*!*/ Binder;
            public readonly Type/*!*/ DelegateType;

            protected Type/*!*/ _siteType;
            public Type/*!*/ SiteType {
                get {
                    if (_siteType != null) {
                        _siteType = typeof(CallSite<>).MakeGenericType(DelegateType);
                    }
                    return _siteType;
                }
            }

            public SiteInfo(DynamicMetaObjectBinder/*!*/ binder, MSAst.Expression/*!*/ expr, FieldInfo/*!*/ field, int index, Type/*!*/ delegateType)
                : base(expr, field, index) {
                Assert.NotNull(binder);

                Binder = binder;
                DelegateType = delegateType;
            }

            public SiteInfo(DynamicMetaObjectBinder/*!*/ binder, MSAst.Expression/*!*/ expr, FieldInfo/*!*/ field, int index, Type/*!*/ delegateType, Type/*!*/ siteType)
                : this(binder, expr, field, index, delegateType) {
                _siteType = siteType;
            }

            public abstract CallSite/*!*/ MakeSite();
        }

        public class SiteInfoLarge : SiteInfo {
            public SiteInfoLarge(DynamicMetaObjectBinder/*!*/ binder, MSAst.Expression/*!*/ expr, FieldInfo/*!*/ field, int index, Type/*!*/ delegateType)
                : base(binder, expr, field, index, delegateType) { }

            public override CallSite MakeSite() {
                return CallSite.Create(DelegateType, Binder);
            }
        }

        public class SiteInfo<T> : SiteInfo where T : class {
            public SiteInfo(DynamicMetaObjectBinder/*!*/ binder, MSAst.Expression/*!*/ expr, FieldInfo/*!*/ field, int index)
                : base(binder, expr, field, index, typeof(T), typeof(CallSite<T>)) { }

            public override CallSite MakeSite() {
                return CallSite<T>.Create(Binder);
            }
        }

        #endregion
    }
}
