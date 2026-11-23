// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Linq.Expressions;

using Microsoft.Scripting.Interpreter;
using Microsoft.Scripting.Runtime;

namespace IronPython.Compiler {
    /// <summary>
    /// Represents code which can be lazily compiled.
    ///
    /// The code is created in an AST which provides the Expression of T and
    /// whether or not the code should be interpreted.  For non-pre compiled
    /// scenarios the code will not be compiled until the 1st time it is run.
    ///
    /// For pre-compiled scenarios the code is IExpressionSerializable and will
    /// turn into a normal pre-compiled method.
    /// </summary>
    internal sealed class LazyCode<T> : IExpressionSerializable where T : class {
        public Expression<T> Code;
        private T Delegate;
        private readonly bool _shouldInterpret;
        private readonly int _compilationThreshold;

        public LazyCode(Expression<T> code, bool shouldInterpret, int compilationThreshold) {
            Code = code;
            _shouldInterpret = shouldInterpret;
            _compilationThreshold = compilationThreshold;
        }

        public T EnsureDelegate() {
            if (Delegate == null) {
                lock (this) {
                    if (Delegate == null) {
                        Delegate = Compile();
                        Code = null;
                    }
                }
            }

            return Delegate;
        }

        private T Compile() {
            if (_shouldInterpret) {
                var result = (System.Delegate)(object)Microsoft.Scripting.Generation.CompilerHelpers.LightCompile(Code, _compilationThreshold);

                // If the adaptive compiler decides to compile this code, we
                // want to store the new compiled target. This saves us from going
                // through the interpreter stub every call.
                if (result.Target is LightLambda lightLambda) {
                    lightLambda.Compile += (_, e) => Delegate = (T)(object)e.Compiled;
                }

                return (T)(object)result;
            }

            return Code.Compile();
        }

        #region IExpressionSerializable Members

        public Expression CreateExpression() {
            return Code;
        }

        #endregion
    }
}
