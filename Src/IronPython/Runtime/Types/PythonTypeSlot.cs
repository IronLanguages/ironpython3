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
using System.Linq.Expressions;
#else
using Microsoft.Scripting.Ast;
#endif

using System.Dynamic;
using System.Runtime.InteropServices;

using IronPython.Runtime.Binding;
using IronPython.Runtime.Operations;

using Microsoft.Scripting.Utils;
using AstUtils = Microsoft.Scripting.Ast.Utils;

namespace IronPython.Runtime.Types {
    using Ast = Expression;

    /// <summary>
    /// A TypeSlot is an item that gets stored in a type's dictionary.  Slots provide an 
    /// opportunity to customize access at runtime when a value is get or set from a dictionary.
    /// </summary>
    [PythonType]
    public class PythonTypeSlot {
        /// <summary>
        /// Gets the value stored in the slot for the given instance binding it to an instance if one is provided and
        /// the slot binds to instances.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1007:UseGenericsWhereAppropriate")]
        internal virtual bool TryGetValue(CodeContext context, object instance, PythonType owner, out object value) {
            value = null;
            return false;
        }

        /// <summary>
        /// Sets the value of the slot for the given instance.
        /// </summary>
        /// <returns>true if the value was set, false if it can't be set</returns>
        internal virtual bool TrySetValue(CodeContext context, object instance, PythonType owner, object value) {
            return false;
        }

        /// <summary>
        /// Deletes the value stored in the slot from the instance.
        /// </summary>
        /// <returns>true if the value was deleted, false if it can't be deleted</returns>
        internal virtual bool TryDeleteValue(CodeContext context, object instance, PythonType owner) {            
            return false;
        }

        internal virtual bool IsAlwaysVisible {
            get {
                return true;
            }
        }

        /// <summary>
        /// True if generating code for gets can result in more optimal accesses.
        /// </summary>
        internal virtual bool CanOptimizeGets {
            get {
                return false;
            }
        }

        /// <summary>
        /// Gets an expression which is used for accessing this slot.  If the slot lookup fails the error expression
        /// is used again.
        /// 
        /// The default implementation just calls the TryGetValue method.  Subtypes of PythonTypeSlot can override
        /// this and provide a more optimal implementation.
        /// </summary>
        internal virtual void MakeGetExpression(PythonBinder/*!*/ binder, Expression/*!*/ codeContext, DynamicMetaObject instance, DynamicMetaObject/*!*/ owner, ConditionalBuilder/*!*/ builder) {
            ParameterExpression tmp = Ast.Variable(typeof(object), "slotTmp");
            Expression call = Ast.Call(
                 typeof(PythonOps).GetMethod("SlotTryGetValue"),
                 codeContext,
                 AstUtils.Convert(AstUtils.WeakConstant(this), typeof(PythonTypeSlot)),
                 instance != null ? instance.Expression : AstUtils.Constant(null),
                 owner.Expression,
                 tmp
            );

            builder.AddVariable(tmp);
            if (!GetAlwaysSucceeds) {
                builder.AddCondition(
                    call,
                    tmp
                );
            } else {
                builder.FinishCondition(Ast.Block(call, tmp));
            }
        }
        
        /// <summary>
        /// True if TryGetValue will always succeed, false if it may fail.
        /// 
        /// This is used to optimize away error generation code.
        /// </summary>
        internal virtual bool GetAlwaysSucceeds {
            get {
                return false;
            }
        }

        internal virtual bool IsSetDescriptor(CodeContext context, PythonType owner) {
            return false;
        }

        public virtual object __get__(CodeContext/*!*/ context, object instance, [DefaultParameterValue(null)]object typeContext) {
            PythonType dt = typeContext as PythonType;

            object res;
            if (TryGetValue(context, instance, dt, out res))
                return res;

            throw PythonOps.AttributeErrorForMissingAttribute(dt == null ? "?" : dt.Name, "__get__");
        }
    }
}
