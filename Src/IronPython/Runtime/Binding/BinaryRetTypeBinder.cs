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
using System.Dynamic;
using System.Runtime.CompilerServices;

#if FEATURE_CORE_DLR
using System.Linq.Expressions;
#else
using Microsoft.Scripting.Ast;
#endif

using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Generation;
using Microsoft.Scripting.Runtime;
using IronPython.Runtime.Operations;

namespace IronPython.Runtime.Binding {

    partial class BinaryRetTypeBinder : ComboBinder, IExpressionSerializable {
        private readonly DynamicMetaObjectBinder _opBinder;
        private readonly PythonConversionBinder _convBinder;

        public BinaryRetTypeBinder(DynamicMetaObjectBinder operationBinder, PythonConversionBinder conversionBinder) :
            base(new BinderMappingInfo(
                    operationBinder,
                    ParameterMappingInfo.Parameter(0),
                    ParameterMappingInfo.Parameter(1)
                ),
                new BinderMappingInfo(
                    conversionBinder,
                    ParameterMappingInfo.Action(0)
                )
            ) {
            _opBinder = operationBinder;
            _convBinder = conversionBinder;
        }

        public override Type ReturnType {
            get {
                return _convBinder.Type;
            }
        }

        #region IExpressionSerializable Members

        public Expression CreateExpression() {
            
            return Expression.Call(
                typeof(PythonOps).GetMethod("MakeComboAction"),
                BindingHelpers.CreateBinderStateExpression(),
                ((IExpressionSerializable)_opBinder).CreateExpression(),
                _convBinder.CreateExpression()
            );
        }

        #endregion
    }
}
