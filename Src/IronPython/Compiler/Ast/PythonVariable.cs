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
            Name = name;
            Kind = kind;
            Scope = scope;
        }

        public string Name { get; }

        public bool IsGlobal {
            get {
                return Kind == VariableKind.Global || Scope.IsGlobal;
            }
        }

        public ScopeStatement Scope { get; }

        public VariableKind Kind { get; set; }

        public virtual PythonVariable LimitVariable => this;

        /// <summary>
        /// Gets or sets a value indicating whether the variable gets deleted.
        /// </summary>
        internal bool Deleted { get; set; } // TODO: rename to: PotentiallyDeleted

        /// <summary>
        /// Gets the index used for tracking in the flow checker.
        /// </summary>
        internal int Index { get; set; }

        /// <summary>
        /// True iff there is a path in control flow graph of a single scope
        /// on which the variable is used before explicitly initialized (assigned or deleted)
        /// in that scope.
        /// </summary>
        public bool ReadBeforeInitialized { get; set; }

        /// <summary>
        /// True iff the variable is referred to from the inner scope.
        /// </summary>
        public bool AccessedInNestedScope { get; set; }
    }

    // TODO: can be abolished?
    internal class PythonReferenceVariable : PythonVariable {

        internal PythonReferenceVariable(PythonReference reference, ScopeStatement scope)
            : base(reference.Name, VariableKind.Nonlocal, scope) {
            Reference = reference;
        }

        internal PythonReference Reference { get; }

        public override PythonVariable LimitVariable => Reference.PythonVariable?.LimitVariable
            // TODO: choose one of:
            // ?? this;
            ?? throw new InvalidOperationException($"unbound reference variable {Name} in scope {Scope.Name}");
            // ; // may return null
    }
}
