// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Linq.Expressions;

using System;
using System.Collections.Generic;
using System.Dynamic;

using Microsoft.Scripting;
using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Utils;

using IronPython.Runtime.Types;
    
namespace IronPython.Runtime.Binding {
    using Ast = Expression;
    using AstUtils = Microsoft.Scripting.Ast.Utils;

    partial class MetaPythonType : MetaPythonObject, IPythonConvertible {
        public MetaPythonType(Expression/*!*/ expression, BindingRestrictions/*!*/ restrictions, PythonType/*!*/ value)
            : base(expression, BindingRestrictions.Empty, value) {
            Assert.NotNull(value);
        }

        public override DynamicMetaObject BindCreateInstance(CreateInstanceBinder create, params DynamicMetaObject[] args) {
            return InvokeWorker(create, args, AstUtils.Constant(PythonContext.GetPythonContext(create).SharedContext));
        }

        public override DynamicMetaObject BindConvert(ConvertBinder/*!*/ conversion) {
            return ConvertWorker(conversion, conversion.Type, conversion.Explicit ? ConversionResultKind.ExplicitCast : ConversionResultKind.ImplicitCast);
        }

        public DynamicMetaObject BindConvert(PythonConversionBinder binder) {
            return ConvertWorker(binder, binder.Type, binder.ResultKind);
        }

        public DynamicMetaObject ConvertWorker(DynamicMetaObjectBinder binder, Type type, ConversionResultKind kind) {
            if (type.IsSubclassOf(typeof(Delegate))) {
                return MakeDelegateTarget(binder, type, Restrict(Value.GetType()));
            }
            return FallbackConvert(binder);
        }

        public override System.Collections.Generic.IEnumerable<string> GetDynamicMemberNames() {
            PythonContext pc = Value.PythonContext ?? DefaultContext.DefaultPythonContext;

            foreach (object o in Value.GetMemberNames(pc.SharedContext)) {
                if (o is string) {
                    yield return (string)o;
                }
            }
        }

        public new PythonType/*!*/ Value {
            get {
                return (PythonType)base.Value;
            }
        }
    }
}
