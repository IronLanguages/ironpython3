// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using MSAst = System.Linq.Expressions;

using System;
using System.Diagnostics;

using Microsoft.Scripting;
using Microsoft.Scripting.Ast;
using Microsoft.Scripting.Utils;

using IronPython.Runtime;

namespace IronPython.Compiler.Ast {
    using Ast = MSAst.Expression;

    [DebuggerDisplay("{Kind} {Name} from {Scope.Name}")]
    public class PythonVariable {

        public PythonVariable(string name, VariableKind kind, ScopeStatement/*!*/ scope) {
            Assert.NotNull(scope);
            Debug.Assert(kind != VariableKind.Nonlocal || !scope.IsGlobal);
            Name = name;
            Kind = kind;
            Scope = scope;
        }

        /// <summary>
        /// The name of the variable as used in Python code.
        /// </summary>
        public string Name { get; }

        public bool IsGlobal {
            get {
                return Kind == VariableKind.Global || Scope.IsGlobal;
            }
        }

        /// <summary>
        /// The original scope in which the variable is defined.
        /// </summary>
        public ScopeStatement Scope { get; }

        public VariableKind Kind { get; set; } // TODO: make readonly

        /// <summary>
        /// The actual variable represented by this variable instance.
        /// For reference variables this may be null if the reference is not yet resolved.
        /// </summary>
        public virtual PythonVariable LimitVariable => this;

        /// <summary>
        /// Gets a value indicating whether the variable gets deleted by a <c>del</c> statement in any scope.
        /// </summary>
        internal bool MaybeDeleted { get; private set; }

        /// <summary>
        /// Mark the variable as argument to a del statement in some scope.
        /// </summary>
        internal void RegisterDeletion() => MaybeDeleted = true;

        /// <summary>
        /// Gets the index used for tracking in the flow checker.
        /// </summary>
        internal int Index { get; set; }

        /// <summary>
        /// True iff there is a path in the control flow graph of a single scope
        /// on which the variable is used before explicitly initialized (assigned or deleted)
        /// in that scope.
        /// </summary>
        public bool ReadBeforeInitialized { get; set; }

        /// <summary>
        /// True iff the variable is referred to from an inner scope.
        /// </summary>
        public bool AccessedInNestedScope { get; set; }
    }

    internal class PythonReferenceVariable : PythonVariable {

        internal PythonReferenceVariable(PythonReference reference, ScopeStatement scope)
            : base(reference.Name, VariableKind.Nonlocal, scope) {
            Reference = reference;
        }

        internal PythonReference Reference { get; }

        public override PythonVariable LimitVariable => Reference.PythonVariable?.LimitVariable;
    }
}
