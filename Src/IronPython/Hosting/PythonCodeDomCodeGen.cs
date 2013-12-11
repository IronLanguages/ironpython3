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

using System.CodeDom;
using System.Collections.Generic;
using Microsoft.Scripting.Runtime;

#if FEATURE_CODEDOM

namespace IronPython.Hosting {
    internal class PythonCodeDomCodeGen : CodeDomCodeGen {

        // Tracks the indents found in user provided snippets
        private Stack<int> _indents = new Stack<int>(new int[] { 0 });

        // Tracks the indent level that is required by generated
        // nesting levels (that the user can't see)
        private int _generatedIndent = 0;

        protected override void WriteExpressionStatement(CodeExpressionStatement s) {
            Writer.Write(new string(' ', _generatedIndent + _indents.Peek()));
            WriteExpression(s.Expression);
            Writer.Write("\n");
        }

        protected override void WriteSnippetExpression(CodeSnippetExpression e) {
            Writer.Write(IndentSnippet(e.Value));
        }

        protected override void WriteSnippetStatement(CodeSnippetStatement s) {
            string snippet = s.Value;

            // Finally, append the snippet. Make sure that it is indented properly if
            // it has nested newlines
            Writer.Write(IndentSnippetStatement(snippet));
            Writer.Write('\n');

            // See if the snippet changes our indent level
            string lastLine = snippet.Substring(snippet.LastIndexOf('\n') + 1);
            // If the last line is only whitespace, then we have a new indent level
            if (string.IsNullOrEmpty(lastLine.Trim('\t', ' '))) {
                lastLine = lastLine.Replace("\t", "        ");
                int indentLen = lastLine.Length;
                if (indentLen > _indents.Peek()) {
                    _indents.Push(indentLen);
                }
                else {
                    while (indentLen < _indents.Peek()) {
                        _indents.Pop();
                    }
                }
            }
        }

        protected override void WriteFunctionDefinition(CodeMemberMethod func) {
            Writer.Write("def ");
            Writer.Write(func.Name);
            Writer.Write("(");
            for (int i = 0; i < func.Parameters.Count; ++i) {
                if (i != 0) {
                    Writer.Write(",");
                }
                Writer.Write(func.Parameters[i].Name);
            }
            Writer.Write("):\n");

            int baseIndent = _indents.Peek();

            _generatedIndent += 4;

            foreach (CodeStatement stmt in func.Statements) {
                WriteStatement(stmt);
            }

            _generatedIndent -= 4;

            while (_indents.Peek() > baseIndent) {
                _indents.Pop();
            }
        }

        protected override string QuoteString(string val) {
            return "'''" + val.Replace(@"\", @"\\").Replace("'''", @"\'''") + "'''";
        }

        private string IndentSnippet(string block) {
            return block.Replace("\n", "\n" + new string(' ', _generatedIndent));
        }

        private string IndentSnippetStatement(string block) {
            return new string(' ', _generatedIndent) + IndentSnippet(block);
        }
    }
}

#endif