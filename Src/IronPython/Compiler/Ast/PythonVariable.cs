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
using System.Diagnostics;

using Microsoft.Scripting;
using Microsoft.Scripting.Ast;
using Microsoft.Scripting.Utils;

using IronPython.Runtime;

namespace IronPython.Compiler.Ast {
    using Ast = MSAst.Expression;

    internal class PythonVariable {
        private readonly string _name;
        private readonly ScopeStatement/*!*/ _scope;
        private VariableKind _kind;    // the type of variable, 

        // variables used during the flow analysis to determine required initialization & checks
        private bool _deleted;                  // del x, the variable gets deleted at some point
        private bool _readBeforeInitialized;    // the variable is read before it's initialized and therefore needs an init check
        private bool _accessedInNestedScope;    // the variable is accessed in a nested scope and therefore needs to be a closure var
        private int _index;                     // Index used for tracking in the flow checker

        public PythonVariable(string name, VariableKind kind, ScopeStatement/*!*/ scope) {
            Assert.NotNull(scope);
            _name = name;
            _kind = kind;
            _scope = scope;
        }

        public string Name {
            get { return _name; }
        }

        public bool IsGlobal {
            get {
                return Kind == VariableKind.Global || Scope.IsGlobal;
            }
        }

        public ScopeStatement Scope {
            get { return _scope; }
        }

        public VariableKind Kind {
            get { return _kind; }
            set { _kind = value; }
        }

        internal bool Deleted {
            get { return _deleted; }
            set { _deleted = value; }
        }

        internal int Index {
            get { return _index; }
            set { _index = value; }
        }

        /// <summary>
        /// True iff there is a path in control flow graph on which the variable is used before initialized (assigned or deleted).
        /// </summary>
        public bool ReadBeforeInitialized {
            get { return _readBeforeInitialized; }
            set { _readBeforeInitialized = value; }
        }

        /// <summary>
        /// True iff the variable is referred to from the inner scope.
        /// </summary>
        public bool AccessedInNestedScope {
            get { return _accessedInNestedScope; }
            set { _accessedInNestedScope = value; }
        }
    }
}
