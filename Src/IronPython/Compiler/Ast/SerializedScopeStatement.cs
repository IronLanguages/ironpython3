﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.Scripting;

using IronPython.Runtime;

using MSAst = System.Linq.Expressions;

namespace IronPython.Compiler.Ast {
    /// <summary>
    /// Fake ScopeStatement for FunctionCode's to hold on to after we have deserialized pre-compiled code.
    /// </summary>
    class SerializedScopeStatement : ScopeStatement {
        private readonly string _name;
        private readonly string _filename;
        private readonly FunctionAttributes _flags;
        private readonly string[] _parameterNames;

        internal SerializedScopeStatement(string name, string[] argNames, FunctionAttributes flags, int startIndex, int endIndex, string path, string[] freeVars, string[] names, string[] cellVars, string[] varNames) {
            _name = name;
            _filename = path;
            _flags = flags;
            SetLoc(null, startIndex, endIndex);
            _parameterNames = argNames;
            if (freeVars != null) {
                foreach (string freeVar in freeVars) {
                    AddFreeVariable(new PythonVariable(freeVar, VariableKind.Local, this), false);
                }
            }
            if (names != null) {
                foreach (string globalName in names) {
                    AddGlobalVariable(new PythonVariable(globalName, VariableKind.Global, this));
                }
            }
            if (varNames != null) {
                foreach (string variable in varNames) {
                    EnsureVariable(variable);
                }
            }
            if (cellVars != null) {
                foreach (string cellVar in cellVars) {
                    AddCellVariable(new PythonVariable(cellVar, VariableKind.Local, this));
                }
            }
        }

        internal override Microsoft.Scripting.Ast.LightLambdaExpression GetLambda() {
            throw new InvalidOperationException();
        }

        internal override bool ExposesLocalVariable(PythonVariable variable) {
            throw new InvalidOperationException();
        }

        internal override PythonVariable BindReference(PythonNameBinder binder, PythonReference reference) {
            throw new InvalidOperationException();
        }

        public override void Walk(PythonWalker walker) {
            throw new InvalidOperationException();
        }

        public override string Name {
            get {
                return _name;
            }
        }

        internal override string Filename {
            get {
                return _filename;
            }
        }

        internal override FunctionAttributes Flags {
            get {
                return _flags;
            }
        }

        internal override string[] ParameterNames {
            get {
                return _parameterNames;
            }
        }

        internal override int ArgCount {
            get {
                return _parameterNames.Length;
            }
        }
    }
}
