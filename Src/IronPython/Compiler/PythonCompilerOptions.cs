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

using Microsoft.Scripting;

using IronPython.Runtime;

namespace IronPython.Compiler {
    [Serializable]
    public sealed class PythonCompilerOptions : CompilerOptions {
        private ModuleOptions _module;
        private bool _skipFirstLine, _dontImplyIndent;
        private string _moduleName;
        private int[] _initialIndentation;
        private CompilationMode _compilationMode;

        /// <summary>
        /// Creates a new PythonCompilerOptions with the default language features enabled.
        /// </summary>
        public PythonCompilerOptions()
            : this(ModuleOptions.None) {
        }

        /// <summary>
        /// Creates a new PythonCompilerOptions with the specified language features enabled.
        /// </summary>
        public PythonCompilerOptions(ModuleOptions features) {
            _module = features;
        }

        public bool DontImplyDedent {
            get { return _dontImplyIndent; }
            set { _dontImplyIndent = value; }
        }

        /// <summary>
        /// Gets or sets the initial indentation.  This can be set to allow parsing
        /// partial blocks of code that are already indented.
        /// 
        /// For each element of the array there is an additional level of indentation.
        /// Each integer value represents the number of spaces used for the indentation.
        /// 
        /// If this value is null then no indentation level is specified.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1819:PropertiesShouldNotReturnArrays")]
        public int[] InitialIndent {
            get {
                return _initialIndentation;
            }
            set {
                _initialIndentation = value;
            }
        }

        public bool AllowWithStatement {
            get {
                return (_module & ModuleOptions.WithStatement) != 0;
            }
            set {
                if (value) _module |= ModuleOptions.WithStatement;
                else _module &= ~ModuleOptions.WithStatement;
            }
        }

        public bool AbsoluteImports {
            get {
                return (_module & ModuleOptions.AbsoluteImports) != 0;
            }
            set {
                if (value) _module |= ModuleOptions.AbsoluteImports;
                else _module &= ~ModuleOptions.AbsoluteImports;
            }
        }

        public bool Verbatim {
            get {
                return (_module & ModuleOptions.Verbatim) != 0;
            }
            set {
                if (value) _module |= ModuleOptions.Verbatim;
                else _module &= ~ModuleOptions.Verbatim;
            }
        }

        public bool UnicodeLiterals {
            get {
                return (_module & ModuleOptions.UnicodeLiterals) != 0;
            }
            set {
                if (value) _module |= ModuleOptions.UnicodeLiterals;
                else _module &= ~ModuleOptions.UnicodeLiterals;
            }
        }

        public bool Interpreted {
            get {
                return (_module & ModuleOptions.Interpret) != 0;
            }
            set {
                if (value) _module |= ModuleOptions.Interpret;
                else _module &= ~ModuleOptions.Interpret;
            }
        }

        public bool Optimized {
            get {
                return (_module & ModuleOptions.Optimized) != 0;
            }
            set {
                if (value) _module |= ModuleOptions.Optimized;
                else _module &= ~ModuleOptions.Optimized;
            }
        }

        public ModuleOptions Module {
            get {
                return _module;
            }
            set {
                _module = value;
            }
        }

        public string ModuleName {
            get {
                return _moduleName;
            }
            set {
                _moduleName = value;
            }
        }

        public bool SkipFirstLine {
            get { return _skipFirstLine; }
            set { _skipFirstLine = value; }
        }

        internal CompilationMode CompilationMode {
            get {
                return _compilationMode;
            }
            set {
                _compilationMode = value;
            }
        }
    }
}
