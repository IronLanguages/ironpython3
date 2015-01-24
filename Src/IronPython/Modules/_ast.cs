/* ****************************************************************************
 *
 * Copyright (c) Jeff Hardy 2010. 
 * Copyright (c) Dan Eloff 2008-2009. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the  Apache License, Version 2.0, please send an email to 
 * ironpy@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 *
 * ***************************************************************************/

using System;
using System.Collections;
using System.Collections.Generic;
using Generic = System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using IronPython.Compiler;
using IronPython.Compiler.Ast;
using IronPython.Runtime;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;
using IronPython.Runtime.Exceptions;
using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using PyOperator = IronPython.Compiler.PythonOperator;
using PythonList = IronPython.Runtime.List;
using System.Runtime.InteropServices;
using AstExpression = IronPython.Compiler.Ast.Expression;

#if FEATURE_NUMERICS
using System.Numerics;
#else
using Microsoft.Scripting.Math;
using Complex = Microsoft.Scripting.Math.Complex64;
#endif

[assembly: PythonModule("_ast", typeof(IronPython.Modules._ast))]
namespace IronPython.Modules
{
    public static class _ast
    {
        public const string __version__ = "62047";
        public const int PyCF_ONLY_AST = 0x400;

        private class ThrowingErrorSink : ErrorSink
        {
            public static new readonly ThrowingErrorSink/*!*/ Default = new ThrowingErrorSink();

            private ThrowingErrorSink() {
            }

            public override void Add(SourceUnit sourceUnit, string message, SourceSpan span, int errorCode, Severity severity) {
                if (severity == Severity.Warning) {
                    PythonOps.SyntaxWarning(message, sourceUnit, span, errorCode);
                } else {
                    throw PythonOps.SyntaxError(message, sourceUnit, span, errorCode);
                }
            }
        }

        internal static PythonAst ConvertToPythonAst(CodeContext codeContext, AST source ) {
            Statement stmt;
            PythonCompilerOptions options = new PythonCompilerOptions(ModuleOptions.ExecOrEvalCode);
            SourceUnit unit = new SourceUnit(codeContext.LanguageContext, NullTextContentProvider.Null, "", SourceCodeKind.AutoDetect);
            CompilerContext compilerContext = new CompilerContext(unit, options, ErrorSink.Default);
            bool printExpression = false;

            if (source is Expression) {
                Expression exp = (Expression)source;
                stmt = new ReturnStatement(expr.Revert(exp.body));
            } else if (source is Module) {
                Module module = (Module)source;
                stmt = _ast.stmt.RevertStmts(module.body);
            } else if (source is Interactive) {
                Interactive interactive = (Interactive)source;
                stmt = _ast.stmt.RevertStmts(interactive.body);
                printExpression = true;
            } else 
                throw PythonOps.TypeError("unsupported type of AST: {0}",(source.GetType()));

            return new PythonAst(stmt, false, ModuleOptions.ExecOrEvalCode, printExpression, compilerContext, new int[] {} );
        }

        internal static AST BuildAst(CodeContext context, SourceUnit sourceUnit, PythonCompilerOptions opts, string mode) {
            Parser parser = Parser.CreateParser(
                new CompilerContext(sourceUnit, opts, ThrowingErrorSink.Default),
                (PythonOptions)context.LanguageContext.Options);

            PythonAst ast = parser.ParseFile(true);
            return ConvertToAST(ast, mode);
        }

        private static mod ConvertToAST(PythonAst pythonAst, string kind) {
            ContractUtils.RequiresNotNull(pythonAst, "pythonAst");
            ContractUtils.RequiresNotNull(kind, "kind");
            return ConvertToAST((SuiteStatement)pythonAst.Body, kind);
        }

        private static mod ConvertToAST(SuiteStatement suite, string kind) {
            ContractUtils.RequiresNotNull(suite, "suite");
            ContractUtils.RequiresNotNull(kind, "kind");
            switch (kind) {
                case "exec":
                    return new Module(suite);
                case "eval":
                    return new Expression(suite);
                case "single":
                    return new Interactive(suite);
                default:
                    throw new ArgumentException("kind must be 'exec' or 'eval' or 'single'");
            }
        }

        [PythonType]
        public abstract class AST
        {
            private PythonTuple __fields = new PythonTuple();   // Genshi assumes _fields in not None
            private PythonTuple __attributes = new PythonTuple();   // Genshi assumes _fields in not None
            protected int? _lineno; // both lineno and col_offset are expected to be int, in cpython anything is accepted
            protected int? _col_offset;

            public PythonTuple _fields {
                get { return __fields; }
                protected set { __fields = value; }
            }

            public PythonTuple _attributes {
                get { return __attributes; }
                protected set { __attributes = value; }
            }

            public int lineno {
                get { 
                    if (_lineno != null) return (int)_lineno;
                    throw PythonOps.AttributeErrorForMissingAttribute(PythonTypeOps.GetName(this), "lineno");
                }
                set { _lineno = value; }
            }

            public int col_offset {
                get { 
                    if (_col_offset != null) return (int)_col_offset;
                    throw PythonOps.AttributeErrorForMissingAttribute(PythonTypeOps.GetName(this), "col_offset");
                }
                set { _col_offset = value; }
            }

            public void __setstate__(PythonDictionary state) {
                restoreProperties(__attributes, state);
                restoreProperties(__fields, state);
            }

            internal void restoreProperties(IEnumerable<object> names, IDictionary source) {
                foreach (object name in names) {
                    if (name is string) {
                        try {
                            string key = (string)name;
                            this.GetType().GetProperty(key).SetValue(this, source[key], null);
                        } catch (Generic.KeyNotFoundException) {
                            // ignore missing
                        }
                    }
                }
            }

            internal void storeProperties(IEnumerable<object> names, IDictionary target) {
                foreach (object name in names) {
                    if (name is string) {
                        string key = (string)name;
                        object val;
                        try {
                            val = this.GetType().GetProperty(key).GetValue(this, null);
                            target.Add(key, val);
                        } catch (System.Reflection.TargetInvocationException) {
                            // field not set
                        }
                    }
                }
            }

            internal PythonDictionary getstate() {
                PythonDictionary d = new PythonDictionary(10);
                storeProperties(__fields, d);
                storeProperties(__attributes, d);
                return d;
            }

            public virtual object/*!*/ __reduce__() {
                return PythonTuple.MakeTuple(DynamicHelpers.GetPythonType(this), new PythonTuple(), getstate());
            }

            public virtual object/*!*/ __reduce_ex__(int protocol) {
                return __reduce__();
            }

            protected void GetSourceLocation(Node node) {
                _lineno = node.Start.Line;

                // IronPython counts from 1; CPython counts from 0
                _col_offset = node.Start.Column - 1;
            }

            internal static PythonList ConvertStatements(Statement stmt) {
                return ConvertStatements(stmt, false);
            }

            internal static PythonList ConvertStatements(Statement stmt, bool allowNull) {
                if (stmt == null)
                    if (allowNull)
                        return PythonOps.MakeEmptyList(0);
                    else
                        throw new ArgumentNullException("stmt");

                if (stmt is SuiteStatement) {
                    SuiteStatement suite = (SuiteStatement)stmt;
                    PythonList list = PythonOps.MakeEmptyList(suite.Statements.Count);
                    foreach (Statement s in suite.Statements) 
                        if (s is SuiteStatement)  // multiple stmt in a line
                            foreach (Statement s2 in ((SuiteStatement)s).Statements)
                                list.Add(Convert(s2));
                        else 
                            list.Add(Convert(s));
                    
                    return list;
                }

                return PythonOps.MakeListNoCopy(Convert(stmt));
            }


            internal static stmt Convert(Statement stmt) {
                stmt ast;

                if (stmt is FunctionDefinition)
                    ast = new FunctionDef((FunctionDefinition)stmt);
                else if (stmt is ReturnStatement)
                    ast = new Return((ReturnStatement)stmt);
                else if (stmt is AssignmentStatement)
                    ast = new Assign((AssignmentStatement)stmt);
                else if (stmt is AugmentedAssignStatement)
                    ast = new AugAssign((AugmentedAssignStatement)stmt);
                else if (stmt is DelStatement)
                    ast = new Delete((DelStatement)stmt);
                else if (stmt is ExpressionStatement)
                    ast = new Expr((ExpressionStatement)stmt);
                else if (stmt is ForStatement)
                    ast = new For((ForStatement)stmt);
                else if (stmt is WhileStatement)
                    ast = new While((WhileStatement)stmt);
                else if (stmt is IfStatement)
                    ast = new If((IfStatement)stmt);
                else if (stmt is WithStatement)
                    ast = new With((WithStatement)stmt);
                else if (stmt is RaiseStatement)
                    ast = new Raise((RaiseStatement)stmt);
                else if (stmt is TryStatement)
                    ast = Convert((TryStatement)stmt);
                else if (stmt is AssertStatement)
                    ast = new Assert((AssertStatement)stmt);
                else if (stmt is ImportStatement)
                    ast = new Import((ImportStatement)stmt);
                else if (stmt is FromImportStatement)
                    ast = new ImportFrom((FromImportStatement)stmt);
                else if (stmt is ExecStatement)
                    ast = new Exec((ExecStatement)stmt);
                else if (stmt is GlobalStatement)
                    ast = new Global((GlobalStatement)stmt);
                else if (stmt is ClassDefinition)
                    ast = new ClassDef((ClassDefinition)stmt);
                else if (stmt is BreakStatement)
                    ast = new Break();
                else if (stmt is ContinueStatement)
                    ast = new Continue();
                else if (stmt is EmptyStatement)
                    ast = new Pass();
                else
                    throw new ArgumentTypeException("Unexpected statement type: " + stmt.GetType());

                ast.GetSourceLocation(stmt);
                return ast;
            }

            internal static stmt Convert(TryStatement stmt) {
                if (stmt.Finally != null) {
                    PythonList body;
                    if (stmt.Handlers != null && stmt.Handlers.Count != 0) {
                        stmt tryExcept = new TryExcept(stmt);
                        tryExcept.GetSourceLocation(stmt);
                        body = PythonOps.MakeListNoCopy(tryExcept);
                    } else
                        body = ConvertStatements(stmt.Body);

                    return new TryFinally(body, ConvertStatements(stmt.Finally));
                }

                return new TryExcept(stmt);
            }

            internal static PythonList ConvertAliases(IList<DottedName> names, IList<string> asnames) {
                PythonList list = PythonOps.MakeEmptyList(names.Count);

                if (names == FromImportStatement.Star) // does it ever happen?
                    list.Add(new alias("*", null));
                else
                    for (int i = 0; i < names.Count; i++)
                        list.Add(new alias(names[i].MakeString(), asnames[i]));

                return list;
            }

            internal static PythonList ConvertAliases(IList<string> names, IList<string> asnames) {
                PythonList list = PythonOps.MakeEmptyList(names.Count);

                if (names == FromImportStatement.Star)
                    list.Add(new alias("*", null));
                else
                    for (int i = 0; i < names.Count; i++)
                        list.Add(new alias(names[i], asnames[i]));

                return list;
            }

            internal static slice TrySliceConvert(AstExpression expr) {
                if (expr is SliceExpression)
                    return new Slice((SliceExpression)expr);
                if (expr is ConstantExpression && ((ConstantExpression)expr).Value == PythonOps.Ellipsis)
                    return Ellipsis.Instance;
                if (expr is TupleExpression && ((TupleExpression)expr).IsExpandable)
                    return new ExtSlice(((Tuple)Convert(expr)).elts);
                return null;
            }

            internal static expr Convert(AstExpression expr) {
                return Convert(expr, Load.Instance);
            }

            internal static expr Convert(AstExpression expr, expr_context ctx) {
                expr ast;

                if (expr is ConstantExpression)
                    ast = Convert((ConstantExpression)expr);
                else if (expr is NameExpression)
                    ast = new Name((NameExpression)expr, ctx);
                else if (expr is UnaryExpression) {
                    var unaryOp = new UnaryOp((UnaryExpression)expr);
                    ast = unaryOp.TryTrimTrivialUnaryOp();
                } else if (expr is BinaryExpression)
                    ast = Convert((BinaryExpression)expr);
                else if (expr is AndExpression)
                    ast = new BoolOp((AndExpression)expr);
                else if (expr is OrExpression)
                    ast = new BoolOp((OrExpression)expr);
                else if (expr is CallExpression)
                    ast = new Call((CallExpression)expr);
                else if (expr is ParenthesisExpression)
                    return Convert(((ParenthesisExpression)expr).Expression);
                else if (expr is LambdaExpression)
                    ast = new Lambda((LambdaExpression)expr);
                else if (expr is ListExpression)
                    ast = new List((ListExpression)expr, ctx);
                else if (expr is TupleExpression)
                    ast = new Tuple((TupleExpression)expr, ctx);
                else if (expr is DictionaryExpression)
                    ast = new Dict((DictionaryExpression)expr);
                else if (expr is ListComprehension)
                    ast = new ListComp((ListComprehension)expr);
                else if (expr is GeneratorExpression)
                    ast = new GeneratorExp((GeneratorExpression)expr);
                else if (expr is MemberExpression)
                    ast = new Attribute((MemberExpression)expr, ctx);
                else if (expr is YieldExpression)
                    ast = new Yield((YieldExpression)expr);
                else if (expr is ConditionalExpression)
                    ast = new IfExp((ConditionalExpression)expr);
                else if (expr is IndexExpression)
                    ast = new Subscript((IndexExpression)expr, ctx);
                else if (expr is BackQuoteExpression)
                    ast = new Repr((BackQuoteExpression)expr);
                else if (expr is SetExpression)
                    ast = new Set((SetExpression)expr);
                else if (expr is DictionaryComprehension)
                    ast = new DictComp((DictionaryComprehension)expr);
                else if (expr is SetComprehension)
                    ast = new SetComp((SetComprehension)expr);
                else
                    throw new ArgumentTypeException("Unexpected expression type: " + expr.GetType());

                ast.GetSourceLocation(expr);
                return ast;
            }

            internal static expr Convert(ConstantExpression expr) {
                expr ast;

                if (expr.Value == null)
                    return new Name("None", Load.Instance);

                if (expr.Value is int || expr.Value is double || expr.Value is Int64 || expr.Value is BigInteger || expr.Value is Complex)
                    ast = new Num(expr.Value);
                else if (expr.Value is string)
                    ast = new Str((string)expr.Value);
                else if (expr.Value is IronPython.Runtime.Bytes)
                    ast = new Str(Converter.ConvertToString(expr.Value));

                else
                    throw new ArgumentTypeException("Unexpected constant type: " + expr.Value.GetType());

                return ast;
            }

            internal static expr Convert(BinaryExpression expr) {
                AST op = Convert(expr.Operator);
                if (BinaryExpression.IsComparison(expr)) {
                    return new Compare(expr);
                } else {
                    if (op is @operator) {
                        return new BinOp(expr, (@operator)op);
                    }
                }

                throw new ArgumentTypeException("Unexpected operator type: " + op.GetType());
            }

            internal static AST Convert(Node node) {
                AST ast;

                if (node is TryStatementHandler)
                    ast = new ExceptHandler((TryStatementHandler)node);
                else
                    throw new ArgumentTypeException("Unexpected node type: " + node.GetType());

                ast.GetSourceLocation(node);
                return ast;
            }

            internal static PythonList Convert(IList<ComprehensionIterator> iterators) {
                ComprehensionIterator[] iters = new ComprehensionIterator[iterators.Count];
                iterators.CopyTo(iters, 0);

                PythonList comps = new PythonList();
                int start = 1;
                for (int i = 0; i < iters.Length; i++) {
                    if (i == 0 || iters[i] is ComprehensionIf)
                        if (i == iters.Length - 1)
                            i++;
                        else
                            continue;

                    ComprehensionIf[] ifs = new ComprehensionIf[i - start];
                    Array.Copy(iters, start, ifs, 0, ifs.Length);
                    comps.Add(new comprehension((ComprehensionFor)iters[start - 1], ifs));
                    start = i + 1;
                }
                return comps;
            }

            internal static PythonList Convert(ComprehensionIterator[] iters) {
                Generic.List<ComprehensionFor> cfCollector =
                    new Generic.List<ComprehensionFor>();
                Generic.List<Generic.List<ComprehensionIf>> cifCollector =
                    new Generic.List<Generic.List<ComprehensionIf>>();
                Generic.List<ComprehensionIf> cif = null;
                for (int i = 0; i < iters.Length; i++) {
                    if (iters[i] is ComprehensionFor) {
                        ComprehensionFor cf = (ComprehensionFor)iters[i];
                        cfCollector.Add(cf);
                        cif = new Generic.List<ComprehensionIf>();
                        cifCollector.Add(cif);
                    } else {
                        ComprehensionIf ci = (ComprehensionIf)iters[i];
                        cif.Add(ci);
                    }
                }

                PythonList comps = new PythonList();
                for (int i = 0; i < cfCollector.Count; i++)
                    comps.Add(new comprehension(cfCollector[i], cifCollector[i].ToArray()));
                return comps;
            }



            internal static AST Convert(PyOperator op) {
                // We treat operator classes as singletons here to keep overhead down
                // But we cannot fully make them singletons if we wish to keep compatibility wity CPython
                switch (op) {
                    case PyOperator.Add:
                        return Add.Instance;
                    case PyOperator.BitwiseAnd:
                        return BitAnd.Instance;
                    case PyOperator.BitwiseOr:
                        return BitOr.Instance;
                    case PyOperator.Divide:
                        return Div.Instance;
                    case PyOperator.Equal:
                        return Eq.Instance;
                    case PyOperator.FloorDivide:
                        return FloorDiv.Instance;
                    case PyOperator.GreaterThan:
                        return Gt.Instance;
                    case PyOperator.GreaterThanOrEqual:
                        return GtE.Instance;
                    case PyOperator.In:
                        return In.Instance;
                    case PyOperator.Invert:
                        return Invert.Instance;
                    case PyOperator.Is:
                        return Is.Instance;
                    case PyOperator.IsNot:
                        return IsNot.Instance;
                    case PyOperator.LeftShift:
                        return LShift.Instance;
                    case PyOperator.LessThan:
                        return Lt.Instance;
                    case PyOperator.LessThanOrEqual:
                        return LtE.Instance;
                    case PyOperator.Mod:
                        return Mod.Instance;
                    case PyOperator.Multiply:
                        return Mult.Instance;
                    case PyOperator.Negate:
                        return USub.Instance;
                    case PyOperator.Not:
                        return Not.Instance;
                    case PyOperator.NotEqual:
                        return NotEq.Instance;
                    case PyOperator.NotIn:
                        return NotIn.Instance;
                    case PyOperator.Pos:
                        return UAdd.Instance;
                    case PyOperator.Power:
                        return Pow.Instance;
                    case PyOperator.RightShift:
                        return RShift.Instance;
                    case PyOperator.Subtract:
                        return Sub.Instance;
                    case PyOperator.Xor:
                        return BitXor.Instance;
                    default:
                        throw new ArgumentException("Unexpected PyOperator: " + op, "op");
                }
            }
        }

        [PythonType]
        public class alias : AST
        {
            private string _name;
            private string _asname; // Optional

            public alias() {
                _fields = new PythonTuple(new[] { "name", "asname" });
            }

            internal alias(string name, [Optional]string asname)
                : this() {
                _name = name;
                _asname = asname;
            }

            public string name {
                get { return _name; }
                set { _name = value; }
            }

            public string asname {
                get { return _asname; }
                set { _asname = value; }
            }
        }

        [PythonType]
        public class arguments : AST
        {
            private PythonList _args;
            private string _vararg; // Optional
            private string _kwarg; // Optional
            private PythonList _defaults;

            public arguments() {
                _fields = new PythonTuple(new[] { "args", "vararg", "kwarg", "defaults" });
            }

            public arguments(PythonList args, [Optional]string vararg, [Optional]string kwarg, PythonList defaults)
                :this() {
                _args = args;
                _vararg = vararg;
                _kwarg = kwarg;
                _kwarg = kwarg;
                _defaults = defaults;
            }

            internal arguments(IList<Parameter> parameters)
                : this() {
                _args = PythonOps.MakeEmptyList(parameters.Count);
                _defaults = PythonOps.MakeEmptyList(parameters.Count);
                foreach (Parameter param in parameters) {
                    if (param.IsList)
                        _vararg = param.Name;
                    else if (param.IsDictionary)
                        _kwarg = param.Name;
                    else {
                        args.Add(new Name(param));
                        if (param.DefaultValue != null)
                            defaults.Add(Convert(param.DefaultValue));
                    }
                }
            }


            internal arguments(Parameter[] parameters)
                : this(parameters as IList<Parameter>) {
            }

            internal Parameter[] Revert() {
                List<Parameter> parameters = new List<Parameter>();
                int argIdx = args.Count - 1;
                for (int defIdx = defaults.Count - 1; defIdx >= 0; defIdx--, argIdx--) {
                    Name name = (Name)args[argIdx];
                    Parameter p = new Parameter(name.id);
                    p.DefaultValue = expr.Revert(defaults[defIdx]);
                    parameters.Add(p);
                }
                while (argIdx >= 0) {
                    Name name = (Name)args[argIdx--];
                    parameters.Add(new Parameter(name.id));
                }
                parameters.Reverse();
                if (vararg != null)
                    parameters.Add(new Parameter(vararg, ParameterKind.List));
                if (kwarg != null)
                    parameters.Add(new Parameter(kwarg, ParameterKind.Dictionary));
                return parameters.ToArray();
            }

            public PythonList args {
                get { return _args; }
                set { _args = value; }
            }

            public string vararg {
                get { return _vararg; }
                set { _vararg = value; }
            }

            public string kwarg {
                get { return _kwarg; }
                set { _kwarg = value; }
            }

            public PythonList defaults {
                get { return _defaults; }
                set { _defaults = value; }
            }
        }

        [PythonType]
        public abstract class boolop : AST
        {
        }

        [PythonType]
        public abstract class cmpop : AST
        {
            internal PythonOperator Revert() {
                if (this == Eq.Instance)
                    return PyOperator.Equal;
                if (this == Gt.Instance)
                    return PyOperator.GreaterThan;
                if (this == GtE.Instance)
                    return PyOperator.GreaterThanOrEqual;
                if (this == In.Instance)
                    return PyOperator.In;
                if (this == Is.Instance)
                    return PyOperator.Is;
                if (this == IsNot.Instance)
                    return PyOperator.IsNot;
                if (this == Lt.Instance)
                    return PyOperator.LessThan;
                if (this == LtE.Instance)
                    return PyOperator.LessThanOrEqual;
                if (this == NotEq.Instance)
                    return PythonOperator.NotEqual;
                if (this == NotIn.Instance)
                    return PythonOperator.NotIn;
                throw PythonOps.TypeError("Unexpected compare operator: {0}", GetType());
            }

        }

        [PythonType]
        public class comprehension : AST
        {
            private expr _target;
            private expr _iter;
            private PythonList _ifs;

            public comprehension() {
                _fields = new PythonTuple(new[] { "target", "iter", "ifs" });
            }

            public comprehension(expr target, expr iter, PythonList ifs)
                : this() {
                _target = target;
                _iter = iter;
                _ifs = ifs;
            }

            internal comprehension(ComprehensionFor listFor, ComprehensionIf[] listIfs)
                : this() {
                _target = Convert(listFor.Left, Store.Instance);
                _iter = Convert(listFor.List);
                _ifs = PythonOps.MakeEmptyList(listIfs.Length);
                foreach (ComprehensionIf listIf in listIfs)
                    _ifs.Add(Convert(listIf.Test));
            }
            
            internal static ComprehensionIterator[] RevertComprehensions(PythonList comprehensions) {
                Generic.List<ComprehensionIterator> comprehensionIterators =
                    new Generic.List<ComprehensionIterator>();
                foreach (comprehension comp in comprehensions) {
                    ComprehensionFor cf = new ComprehensionFor(expr.Revert(comp.target), expr.Revert(comp.iter));
                    comprehensionIterators.Add(cf);
                    foreach (expr ifs in comp.ifs) {
                        comprehensionIterators.Add(new ComprehensionIf(expr.Revert(ifs)));
                    }
                }
                return comprehensionIterators.ToArray();
            }

            public expr target {
                get { return _target; }
                set { _target = value; }
            }

            public expr iter {
                get { return _iter; }
                set { _iter = value; }
            }

            public PythonList ifs {
                get { return _ifs; }
                set { _ifs = value; }
            }
        }

        [PythonType]
        public class excepthandler : AST
        {
            public excepthandler() {
                _attributes = new PythonTuple(new[] { "lineno", "col_offset" });
            }
        }

        [PythonType]
        public abstract class expr : AST
        {
            protected expr() {
                _attributes = new PythonTuple(new[] { "lineno", "col_offset" });
            }

            internal virtual AstExpression Revert() {
                throw PythonOps.TypeError("Unexpected expr type: {0}", GetType());
            }

            internal static AstExpression Revert(expr ex) {
                if (ex == null)
                    return null;
                return ex.Revert();
            }

            internal static AstExpression Revert(object ex) {
                if (ex == null)
                    return null;
                Debug.Assert(ex is expr);
                return ((expr)ex).Revert();
            }

            internal static AstExpression[] RevertExprs(PythonList exprs) {
                // it is assumed that list elements are expr
                AstExpression[] ret = new AstExpression[exprs.Count];
                for (int i = 0; i < exprs.Count; i++)
                    ret[i] = ((expr)exprs[i]).Revert();
                return ret;
            }

        }

        [PythonType]
        public abstract class expr_context : AST
        {
        }

        [PythonType]
        public class keyword : AST
        {
            private string _arg;
            private expr _value;

            public keyword() {
                _fields = new PythonTuple(new[] { "arg", "value" });
            }

            public keyword(string arg, expr value)
                : this() {
                _arg = arg;
                _value = value;
            }

            internal keyword(IronPython.Compiler.Ast.Arg arg)
                : this() {
                _arg = arg.Name;
                _value = Convert(arg.Expression);
            }

            public string arg {
                get { return _arg; }
                set { _arg = value; }
            }

            public expr value {
                get { return _value; }
                set { _value = value; }
            }
        }

        [PythonType]
        public abstract class mod : AST
        {
            internal abstract PythonList GetStatements();
        }

        [PythonType]
        public abstract class @operator : AST 
        {
            internal PythonOperator Revert() {
                if (this == Add.Instance)
                    return PythonOperator.Add;
                if (this == BitAnd.Instance)
                    return PyOperator.BitwiseAnd;
                if (this == BitOr.Instance)
                    return PyOperator.BitwiseOr;
                if (this == Div.Instance)
                    return PyOperator.Divide;
                if (this == FloorDiv.Instance)
                    return PyOperator.FloorDivide;
                if (this == LShift.Instance)
                    return PyOperator.LeftShift;
                if (this == Mod.Instance)
                    return PythonOperator.Mod;
                if (this == Mult.Instance)
                    return PythonOperator.Multiply;
                if (this == Pow.Instance)
                    return PythonOperator.Power;
                if (this == RShift.Instance)
                    return PyOperator.RightShift;
                if (this == Sub.Instance)
                    return PythonOperator.Subtract;
                if (this == BitXor.Instance)
                    return PythonOperator.Xor;
                throw PythonOps.TypeError("Unexpected unary operator: {0}", GetType());
            }

        }

        [PythonType]
        public abstract class slice : AST
        {
        }

        [PythonType]
        public abstract class stmt : AST
        {
            protected stmt() {
                _attributes = new PythonTuple(new[] { "lineno", "col_offset" });
            }

            internal virtual Statement Revert() {
                throw PythonOps.TypeError("Unexpected statement type: {0}", GetType());
            }

            internal static Statement RevertStmts(PythonList stmts) {
                if (stmts.Count == 1)
                    return ((stmt)stmts[0]).Revert();
                Statement[] statements = new Statement[stmts.Count];
                for (int i = 0; i < stmts.Count; i++)
                    statements[i] = ((stmt)stmts[i]).Revert();
                return new SuiteStatement(statements);
            }
        }

        [PythonType]
        public abstract class unaryop : AST
        {
            internal PyOperator Revert() {
                if (this == Invert.Instance)
                    return PyOperator.Invert;
                if (this == USub.Instance)
                    return PythonOperator.Negate;
                if (this == Not.Instance)
                    return PythonOperator.Not;
                if (this == UAdd.Instance)
                    return PythonOperator.Pos;
                throw PythonOps.TypeError("Unexpected unary operator: {0}", GetType());
            }
        }

        [PythonType]
        public class Add : @operator
        {
            internal static Add Instance = new Add();
        }

        [PythonType]
        public class And : boolop
        {
            internal static And Instance = new And();
        }

        [PythonType]
        public class Assert : stmt
        {
            private expr _test;
            private expr _msg; // Optional

            public Assert() {
                _fields = new PythonTuple(new[] { "test", "msg" });
            }

            public Assert(expr test, expr msg, [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                _test = test;
                _msg = msg;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal Assert(AssertStatement stmt)
                : this() {
                _test = Convert(stmt.Test);
                if (stmt.Message != null)
                    _msg = Convert(stmt.Message);
            }

            internal override Statement Revert() {
                return new AssertStatement(expr.Revert(test), expr.Revert(msg));
            }

            public expr test {
                get { return _test; }
                set { _test = value; }
            }

            public expr msg {
                get { return _msg; }
                set { _msg = value; }
            }
        }

        [PythonType]
        public class Assign : stmt
        {
            private PythonList _targets;
            private expr _value;

            public Assign() {
                _fields = new PythonTuple(new[] { "targets", "value" });
            }

            public Assign(PythonList targets, expr value, [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                _targets = targets;
                _value = value;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal Assign(AssignmentStatement stmt)
                : this() {
                _targets = PythonOps.MakeEmptyList(stmt.Left.Count);
                foreach (AstExpression expr in stmt.Left)
                    _targets.Add(Convert(expr, Store.Instance));

                _value = Convert(stmt.Right);
            }
            
            internal override Statement Revert() {
                return new AssignmentStatement(expr.RevertExprs(targets), expr.Revert(value));
            }

            public PythonList targets {
                get { return _targets; }
                set { _targets = value; }
            }

            public expr value {
                get { return _value; }
                set { _value = value; }
            }
        }

        [PythonType]
        public class Attribute : expr
        {
            private expr _value;
            private string _attr;
            private expr_context _ctx;

            public Attribute() {
                _fields = new PythonTuple(new[] { "value", "attr", "ctx" });
            }

            public Attribute(expr value, string attr, expr_context ctx,
                [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                _value = value;
                _attr = attr;
                _ctx = ctx;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal Attribute(MemberExpression attr, expr_context ctx)
                : this() {
                _value = Convert(attr.Target);
                _attr = attr.Name;
                _ctx = ctx;
            }
         
            internal override AstExpression Revert() {
                return new MemberExpression(expr.Revert(value), attr);
            }

            public expr value {
                get { return _value; }
                set { _value = value; }
            }

            public string attr {
                get { return _attr; }
                set { _attr = value; }
            }

            public expr_context ctx {
                get { return _ctx; }
                set { _ctx = value; }
            }
        }

        [PythonType]
        public class AugAssign : stmt
        {
            private expr _target;
            private @operator _op;
            private expr _value;

            public AugAssign() {
                _fields = new PythonTuple(new[] { "target", "op", "value" });
            }

            public AugAssign(expr target, @operator op, expr value,
                [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                _target = target;
                _op = op;
                _value = value;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal AugAssign(AugmentedAssignStatement stmt)
                : this() {
                _target = Convert(stmt.Left, Store.Instance);
                _value = Convert(stmt.Right);
                _op = (@operator)Convert(stmt.Operator);
            }

            internal override Statement Revert() {
                return new AugmentedAssignStatement(op.Revert(), expr.Revert(target), expr.Revert(value));
            }

            public expr target {
                get { return _target; }
                set { _target = value; }
            }

            public @operator op {
                get { return _op; }
                set { _op = value; }
            }

            public expr value {
                get { return _value; }
                set { _value = value; }
            }
        }

        /// <summary>
        /// Not used.
        /// </summary>
        [PythonType]
        public class AugLoad : expr_context
        {
        }

        /// <summary>
        /// Not used.
        /// </summary>
        [PythonType]
        public class AugStore : expr_context
        {
        }

        [PythonType]
        public class BinOp : expr
        {
            private expr _left;
            private expr _right;
            private @operator _op;

            public BinOp() {
                _fields = new PythonTuple(new[] { "left", "op", "right" });
            }

            public BinOp(expr left, @operator op, expr right, [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                _left = left;
                _op = op;
                _right = right;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal BinOp(BinaryExpression expr, @operator op)
                : this() {
                _left = Convert(expr.Left);
                _right = Convert(expr.Right);
                _op = op;
            }

            internal override AstExpression Revert() {
                return new BinaryExpression(op.Revert(), expr.Revert(left), expr.Revert(right));
            }

            public expr left {
                get { return _left; }
                set { _left = value; }
            }

            public expr right {
                get { return _right; }
                set { _right = value; }
            }

            public @operator op {
                get { return _op; }
                set { _op = value; }
            }
        }

        [PythonType]
        public class BitAnd : @operator
        {
            internal static BitAnd Instance = new BitAnd();
        }

        [PythonType]
        public class BitOr : @operator
        {
            internal static BitOr Instance = new BitOr();
        }

        [PythonType]
        public class BitXor : @operator
        {
            internal static BitXor Instance = new BitXor();
        }

        [PythonType]
        public class BoolOp : expr
        {
            private boolop _op;
            private PythonList _values;

            public BoolOp() {
                _fields = new PythonTuple(new[] { "op", "values" });
            }

            public BoolOp(boolop op, PythonList values, [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                _op = op;
                _values = values;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal BoolOp(AndExpression and)
                : this() {
                _values = PythonOps.MakeListNoCopy(Convert(and.Left), Convert(and.Right));
                _op = And.Instance;
            }

            internal BoolOp(OrExpression or)
                : this() {
                _values = PythonOps.MakeListNoCopy(Convert(or.Left), Convert(or.Right));
                _op = Or.Instance;
            }

            internal override AstExpression Revert() {
                if (op == And.Instance) {
                    AndExpression ae = new AndExpression(
                        expr.Revert(values[0]),
                        expr.Revert(values[1]));
                    return ae;
                } else if (op == Or.Instance) {
                    OrExpression oe = new OrExpression(
                        expr.Revert(values[0]),
                        expr.Revert(values[1]));
                    return oe;
                }
                throw PythonOps.TypeError("Unexpected boolean operator: {0}", op);
            }

            public boolop op {
                get { return _op; }
                set { _op = value; }
            }

            public PythonList values {
                get { return _values; }
                set { _values = value; }
            }
        }

        [PythonType]
        public class Break : stmt
        {
            internal static Break Instance = new Break();

            internal Break()
                : this(null, null) { }

            public Break([Optional]int? lineno, [Optional]int? col_offset) {
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal override Statement Revert() {
                return new BreakStatement();
            }
        }

        [PythonType]
        public class Call : expr
        {
            private expr _func;
            private PythonList _args;
            private PythonList _keywords;
            private expr _starargs; // Optional
            private expr _kwargs; // Optional

            public Call() {
                _fields = new PythonTuple(new[] { "func", "args", "keywords", "starargs", "kwargs" });
            }

            public Call( expr func, PythonList args, PythonList keywords, 
                [Optional]expr starargs, [Optional]expr kwargs,
                [Optional]int? lineno, [Optional]int? col_offset) 
                :this() {
                _func = func;
                _args = args;
                _keywords = keywords;
                _starargs = starargs;
                _kwargs = kwargs;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal Call(CallExpression call)
                : this() {
                _args = PythonOps.MakeEmptyList(call.Args.Count);
                _keywords = new PythonList();
                _func = Convert(call.Target);
                foreach (Arg arg in call.Args) {

                    if (arg.Name == null)
                        _args.Add(Convert(arg.Expression));
                    else if (arg.Name == "*")
                        _starargs = Convert(arg.Expression);
                    else if (arg.Name == "**")
                        _kwargs = Convert(arg.Expression);
                    else
                        _keywords.Add(new keyword(arg));
                }
            }

            internal override AstExpression Revert() {
                AstExpression target = expr.Revert(func);
                List<Arg> newArgs = new List<Arg>();
                foreach (expr ex in args)
                    newArgs.Add(new Arg(expr.Revert(ex)));
                if (null != starargs)
                    newArgs.Add(new Arg("*", expr.Revert(starargs)));
                if (null != kwargs)
                    newArgs.Add(new Arg("**", expr.Revert(kwargs)));
                foreach (keyword kw in keywords)
                    newArgs.Add(new Arg(kw.arg, expr.Revert(kw.value)));
                return new CallExpression(target, newArgs.ToArray());
            }

            public expr func {
                get { return _func; }
                set { _func = value; }
            }

            public PythonList args {
                get { return _args; }
                set { _args = value; }
            }

            public PythonList keywords {
                get { return _keywords; }
                set { _keywords = value; }
            }

            public expr starargs {
                get { return _starargs; }
                set { _starargs = value; }
            }

            public expr kwargs {
                get { return _kwargs; }
                set { _kwargs = value; }
            }
        }

        [PythonType]
        public class ClassDef : stmt
        {
            private string _name;
            private PythonList _bases;
            private PythonList _body;
            private PythonList _decorator_list;

            public ClassDef() {
                _fields = new PythonTuple(new[] { "name", "bases", "body", "decorator_list" });
            }

            public ClassDef(string name, PythonList bases, PythonList body, PythonList decorator_list,
                [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                _name = name;
                _bases = bases;
                _body = body;
                _decorator_list = decorator_list;
                _lineno = lineno;
                _col_offset = col_offset;
            }


            internal ClassDef(ClassDefinition def)
                : this() {
                _name = def.Name;
                _bases = PythonOps.MakeEmptyList(def.Bases.Count);
                foreach (AstExpression expr in def.Bases)
                    _bases.Add(Convert(expr));
                _body = ConvertStatements(def.Body);
                if (def.Decorators != null) {
                    _decorator_list = PythonOps.MakeEmptyList(def.Decorators.Count);
                    foreach (AstExpression expr in def.Decorators)
                        _decorator_list.Add(Convert(expr));
                } else
                    _decorator_list = PythonOps.MakeEmptyList(0);
            }

            internal override Statement Revert() {
                ClassDefinition cd = new ClassDefinition(name, expr.RevertExprs(bases), RevertStmts(body));
                if (decorator_list.Count != 0) 
                    cd.Decorators = expr.RevertExprs(decorator_list);
                return cd;
            }

            public string name {
                get { return _name; }
                set { _name = value; }
            }

            public PythonList bases {
                get { return _bases; }
                set { _bases = value; }
            }

            public PythonList body {
                get { return _body; }
                set { _body = value; }
            }

            public PythonList decorator_list {
                get { return _decorator_list; }
                set { _decorator_list = value; }
            }
        }

        [PythonType]
        public class Compare : expr
        {
            private expr _left;
            private PythonList _ops;
            private PythonList _comparators;

            public Compare() {
                _fields = new PythonTuple(new[] { "left", "ops", "comparators" });
            }

            public Compare(expr left, PythonList ops, PythonList comparators, 
                [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                _left = left;
                _ops = ops;
                _comparators = comparators;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal Compare(BinaryExpression expr)
                : this() {
                _left = Convert(expr.Left);
                _ops = PythonOps.MakeList();
                _comparators = PythonOps.MakeList();
                while (BinaryExpression.IsComparison(expr.Right)) {
                    BinaryExpression right = (BinaryExpression)expr.Right;
                    // start accumulating ops and comparators
                    _ops.Add(Convert(expr.Operator));
                    _comparators.Add(Convert(right.Left));
                    expr = right;
                }
                _ops.Add(Convert(expr.Operator));
                _comparators.Add(Convert(expr.Right));
            }

            internal override AstExpression Revert() {
                // the most likely case first
                if (ops.Count == 1) {
                    return new BinaryExpression(
                        ((cmpop)(ops[0])).Revert(),
                        expr.Revert(left),
                        expr.Revert(comparators[0]));
                }

                // chaining of comparators is processed here (a>b>c> ...)
                Debug.Assert(ops.Count > 1, "expected 2 or more ops in chained comparator");
                int i = ops.Count - 1;
                BinaryExpression right = new BinaryExpression(
                        ((cmpop)(ops[i])).Revert(),
                        expr.Revert(comparators[i - 1]),
                        expr.Revert(comparators[i]));
                i--;
                while (i > 0) {
                    right = new BinaryExpression(
                        ((cmpop)(ops[i])).Revert(),
                        expr.Revert(comparators[i - 1]),
                        right);
                    i--;
                }
                return new BinaryExpression(
                        ((cmpop)(ops[0])).Revert(),
                        expr.Revert(left),
                        right);
            }

            public expr left {
                get { return _left; }
                set { _left = value; }
            }

            public PythonList ops {
                get { return _ops; }
                set { _ops = value; }
            }

            public PythonList comparators {
                get { return _comparators; }
                set { _comparators = value; }
            }
        }

        [PythonType]
        public class Continue : stmt
        {
            internal static Continue Instance = new Continue();

            internal Continue()
                : this(null, null) { }

            public Continue([Optional]int? lineno, [Optional]int? col_offset) {
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal override Statement Revert() {
                return new ContinueStatement();
            }
        }

        [PythonType]
        public class Del : expr_context
        {
            internal static Del Instance = new Del();
        }

        [PythonType]
        public class Delete : stmt
        {
            private PythonList _targets;

            public Delete() {
                _fields = new PythonTuple(new[] { "targets", });
            }

            public Delete(PythonList targets, [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                _targets = targets;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal Delete(DelStatement stmt)
                : this() {
                _targets = PythonOps.MakeEmptyList(stmt.Expressions.Count);
                foreach (AstExpression expr in stmt.Expressions)
                    _targets.Add(Convert(expr, Del.Instance));
            }

            internal override Statement Revert() {
                return new DelStatement(expr.RevertExprs(targets));
            }

            public PythonList targets {
                get { return _targets; }
                set { _targets = value; }
            }
        }

        [PythonType]
        public class Dict : expr
        {
            private PythonList _keys;
            private PythonList _values;

            public Dict() {
                _fields = new PythonTuple(new[] { "keys", "values" });
            }

            public Dict(PythonList keys, PythonList values, [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                _keys = keys;
                _values = values;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal Dict(DictionaryExpression expr)
                : this() {
                _keys = PythonOps.MakeEmptyList(expr.Items.Count);
                _values = PythonOps.MakeEmptyList(expr.Items.Count);
                foreach (SliceExpression item in expr.Items) {
                    _keys.Add(Convert(item.SliceStart));
                    _values.Add(Convert(item.SliceStop));
                }
            }

            internal override AstExpression Revert() {
                SliceExpression[] e = new SliceExpression[values.Count];
                for (int i = 0; i < values.Count; i++) {
                    e[i] = new SliceExpression(
                        expr.Revert(keys[i]),
                        expr.Revert(values[i]),
                        null,
                        false);
                }
                return new DictionaryExpression(e);
            }

            public PythonList keys {
                get { return _keys; }
                set { _keys = value; }
            }

            public PythonList values {
                get { return _values; }
                set { _values = value; }
            }
        }

        [PythonType]
        public class DictComp : expr 
        {
            private expr _key;
            private expr _value;
            private PythonList _generators;

            public DictComp() {
                _fields = new PythonTuple(new[] { "key", "value", "generators" });
            }

            public DictComp(expr key, expr value, PythonList generators, 
                [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                _key = key;
                _value = value;
                _generators = generators;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal DictComp(DictionaryComprehension comp)
                : this() {
                _key = Convert(comp.Key);
                _value = Convert(comp.Value);
                _generators = Convert(comp.Iterators);
            }

            internal override AstExpression Revert() {
                return new DictionaryComprehension(expr.Revert(key), expr.Revert(value), comprehension.RevertComprehensions(generators));
            }

            public expr key {
                get { return _key; }
                set { _key = value; }
            }

            public expr value {
                get { return _value; }
                set { _value = value; }
            }

            public PythonList generators {
                get { return _generators; }
                set { _generators = value; }
            }
        }


        [PythonType]
        public class Div : @operator
        {
            internal static Div Instance = new Div();
        }

        [PythonType]
        public class Ellipsis : slice
        {
            internal static Ellipsis Instance = new Ellipsis();
        }

        [PythonType]
        public class Eq : cmpop
        {
            internal static Eq Instance = new Eq();
        }

        [PythonType]
        public class ExceptHandler : excepthandler
        {
            private expr _type;
            private expr _name;
            private PythonList _body;

            public ExceptHandler() {
                _fields = new PythonTuple(new[] { "type", "name", "body" });
            }

            public ExceptHandler([Optional]expr type, [Optional]expr name, PythonList body,
                [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                _type = type;
                _name = name;
                _body = body;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal ExceptHandler(TryStatementHandler stmt)
                : this() {
                if (stmt.Test != null)
                    _type = Convert(stmt.Test);
                if (stmt.Target != null)
                    _name = Convert(stmt.Target, Store.Instance);

                _body = ConvertStatements(stmt.Body);
            }

            internal TryStatementHandler RevertHandler() {
                return new TryStatementHandler(expr.Revert(type), expr.Revert(name), stmt.RevertStmts(body));
            }

            public expr type {
                get { return _type; }
                set { _type = value; }
            }

            public expr name {
                get { return _name; }
                set { _name = value; }
            }

            public PythonList body {
                get { return _body; }
                set { _body = value; }
            }
        }

        [PythonType]
        public class Exec : stmt
        {
            private expr _body;
            private expr _globals; // Optional
            private expr _locals; // Optional

            public Exec() {
                _fields = new PythonTuple(new[] { "body", "globals", "locals" });
            }

            public Exec(expr body, [Optional]expr globals, [Optional]expr locals,
               [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                _body = body;
                _globals = globals;
                _locals = locals;
                _lineno = lineno;
                _col_offset = col_offset;
            }
            
            public Exec(ExecStatement stmt)
                : this() {
                _body = Convert(stmt.Code);
                if (stmt.Globals != null)
                    _globals = Convert(stmt.Globals);
                if (stmt.Locals != null)
                    _locals = Convert(stmt.Locals);
            }

            internal override Statement Revert() {
                return new ExecStatement(expr.Revert(body), expr.Revert(locals), expr.Revert(globals));
            }

            public expr body {
                get { return _body; }
                set { _body = value; }
            }

            public expr globals {
                get { return _globals; }
                set { _globals = value; }
            }

            public expr locals {
                get { return _locals; }
                set { _locals = value; }
            }
        }

        [PythonType]
        public class Expr : stmt
        {
            private expr _value;

            public Expr() {
                _fields = new PythonTuple(new[] { "value", });
            }

            public Expr(expr value,  [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                _value = value;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal Expr(ExpressionStatement stmt)
                : this() {
                _value = Convert(stmt.Expression);
            }

            internal override Statement Revert() {
                return new ExpressionStatement(expr.Revert(value));
            }

            public expr value {
                get { return _value; }
                set { _value = value; }
            }
        }

        [PythonType]
        public class Expression : mod
        {
            private expr _body;

            public Expression() {
                _fields = new PythonTuple(new[] { "body", });
            }

            public Expression(expr body)
                : this() {
                _body = body;
            }

            internal Expression(SuiteStatement suite)
                : this() {
                _body = Convert(((ExpressionStatement)suite.Statements[0]).Expression);
            }

            internal override PythonList GetStatements() {
                return PythonOps.MakeListNoCopy(_body);
            }

            public expr body {
                get { return _body; }
                set { _body = value; }
            }
        }

        [PythonType]
        public class ExtSlice : slice
        {
            private PythonList _dims;

            public ExtSlice() {
                _fields = new PythonTuple(new[] { "dims", });
            }

            public ExtSlice(PythonList dims)
                : this() {
                _dims = dims;
            }

            internal AstExpression[] Revert() {
                List<AstExpression> ret = new List<AstExpression>(dims.Count);
                foreach (expr ex in dims)
                    ret.Add(expr.Revert(ex));
                return ret.ToArray();
            }

            public PythonList dims {
                get { return _dims; }
                set { _dims = value; }
            }
        }

        [PythonType]
        public class FloorDiv : @operator
        {
            internal static FloorDiv Instance = new FloorDiv();
        }

        [PythonType]
        public class For : stmt
        {
            private expr _target;
            private expr _iter;
            private PythonList _body;
            private PythonList _orelse; // Optional, default []

            public For() {
                _fields = new PythonTuple(new[] { "target", "iter", "body", "orelse" });
            }

            public For(expr target, expr iter, PythonList body, [Optional]PythonList orelse,
               [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                _target = target;
                _iter = iter;
                _body = body;
                if (null == orelse)
                    _orelse = new PythonList();
                else
                    _orelse = orelse;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal For(ForStatement stmt)
                : this() {
                _target = Convert(stmt.Left, Store.Instance);
                _iter = Convert(stmt.List);
                _body = ConvertStatements(stmt.Body);
                _orelse = ConvertStatements(stmt.Else, true);
            }

            internal override Statement Revert() {
                return new ForStatement(expr.Revert(target), expr.Revert(iter), RevertStmts(body), RevertStmts(orelse));
            }

            public expr target {
                get { return _target; }
                set { _target = value; }
            }

            public expr iter {
                get { return _iter; }
                set { _iter = value; }
            }

            public PythonList body {
                get { return _body; }
                set { _body = value; }
            }

            public PythonList orelse {
                get { return _orelse; }
                set { _orelse = value; }
            }
        }

        [PythonType]
        public class FunctionDef : stmt
        {
            private string _name;
            private arguments _args;
            private PythonList _body;
            private PythonList _decorator_list;

            public FunctionDef() {
                _fields = new PythonTuple(new[] { "name", "args", "body", "decorator_list" });
            }

            public FunctionDef(string name, arguments args, PythonList body, PythonList decorator_list,
                [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                _name = name;
                _args = args;
                _body = body;
                _decorator_list = decorator_list;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal FunctionDef(FunctionDefinition def)
                : this() {
                _name = def.Name;
                _args = new arguments(def.Parameters);
                _body = ConvertStatements(def.Body);

                if (def.Decorators != null) {
                    _decorator_list = PythonOps.MakeEmptyList(def.Decorators.Count);
                    foreach (AstExpression expr in def.Decorators)
                        _decorator_list.Add(Convert(expr));
                } else
                    _decorator_list = PythonOps.MakeEmptyList(0);
            }

            internal override Statement Revert() {
                FunctionDefinition fd = new FunctionDefinition(name, args.Revert(), RevertStmts(body));
                fd.IsGenerator = _containsYield;
                _containsYield = false;
                if (decorator_list.Count != 0)
                    fd.Decorators = expr.RevertExprs(decorator_list);
                return fd;
            }

            public string name {
                get { return _name; }
                set { _name = value; }
            }

            public arguments args {
                get { return _args; }
                set { _args = value; }
            }

            public PythonList body {
                get { return _body; }
                set { _body = value; }
            }

            public PythonList decorator_list {
                get { return _decorator_list; }
                set { _decorator_list = value; }
            }
        }



        [PythonType]
        public class GeneratorExp : expr
        {
            private expr _elt;
            private PythonList _generators;

            public GeneratorExp() {
                _fields = new PythonTuple(new[] { "elt", "generators" });
            }

            public GeneratorExp(expr elt, PythonList generators, [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                _elt = elt;
                _generators = generators;
                _lineno = lineno;
                _col_offset = col_offset;
            }
           
            // given following generator: (a for b in c for d in e for f in g)
            // PythonAST (Iron) Representation looks like this:
            //             
            // Iterable
            //  NameExpression c
            //
            // Function
            //  FunctionDefinition<genexpr>
            //    Parameter__gen_$_parm__
            //    ForStatement
            //      NameExpression b
            //      NameExpression __gen_$_parm__
            //      ForStatement
            //        NameExpression d
            //        NameExpression e
            //        ForStatement
            //          NameExpression f
            //          NameExpression g
            //          ExpressionStatement
            //            YieldExpression
            //            NameExpression a
            //
            // and corresponding ast (implementation agnostic) looks like this:
            //        ('Expression', ('GeneratorExp', (1, 1), 
            //            ('Name', (1, 1), 'a', ('Load',)), 
            //            [('comprehension', ('Name', (1, 7), 'b', ('Store',)), ('Name', (1, 12), 'c', ('Load',)), []),
            //             ('comprehension', ('Name', (1, 18), 'd', ('Store',)), ('Name', (1, 23), 'e', ('Load',)), []),
            //             ('comprehension', ('Name', (1, 29), 'f', ('Store',)), ('Name', (1, 34), 'g', ('Load',)), [])
            //            ]))

            internal GeneratorExp(GeneratorExpression expr)
                : this() {
                ExtractListComprehensionIterators walker = new ExtractListComprehensionIterators();
                expr.Function.Body.Walk(walker);
                ComprehensionIterator[] iters = walker.Iterators;
                Debug.Assert(iters.Length != 0, "A generator expression cannot have zero iterators.");
                iters[0] = new ComprehensionFor(((ComprehensionFor)iters[0]).Left, expr.Iterable);
                _elt = Convert(walker.Yield.Expression);
                _generators = Convert(iters);
            }

            internal class ExtractListComprehensionIterators : PythonWalker
            {
                private readonly List<ComprehensionIterator> _iterators = new List<ComprehensionIterator>();
                public YieldExpression Yield;

                public ComprehensionIterator[] Iterators {
                    get { return _iterators.ToArray(); }
                }

                public override bool Walk(ForStatement node) {
                    _iterators.Add(new ComprehensionFor(node.Left, node.List));
                    node.Body.Walk(this);
                    return false;
                }

                public override bool Walk(IfStatement node) {
                    _iterators.Add(new ComprehensionIf(node.Tests[0].Test));
                    node.Tests[0].Body.Walk(this);
                    return false;
                }

                public override bool Walk(YieldExpression node) {
                    Yield = node;
                    return false;
                }
            }

            // TODO: following 2 names are copy paste from Parser.cs
            //       it would be better to have them in one place
            private const string generatorFnName = "<genexpr>";
            private const string generatorFnArgName = "__gen_$_parm__";

            internal override AstExpression Revert() {
                Statement stmt = new ExpressionStatement(new YieldExpression(expr.Revert(elt)));
                int comprehensionIdx = generators.Count - 1;
                AstExpression list;
                do {
                    comprehension c = (comprehension)generators[comprehensionIdx];
                    if (c.ifs != null && c.ifs.Count != 0) {
                        int ifIdx = c.ifs.Count - 1;
                        while (ifIdx >= 0) {
                            IfStatementTest ist = new IfStatementTest(expr.Revert(c.ifs[ifIdx]), stmt);
                            stmt = new IfStatement(new IfStatementTest[] { ist }, null);
                            ifIdx--;
                        }
                    }
                    list = expr.Revert(c.iter);
                    stmt = new ForStatement(expr.Revert(c.target), list, stmt, null);
                    comprehensionIdx--;
                } while (comprehensionIdx >= 0);
                ((ForStatement)stmt).List = new NameExpression(generatorFnArgName);
                Parameter parameter = new Parameter(generatorFnArgName, 0);
                FunctionDefinition functionDefinition = new FunctionDefinition(generatorFnName, new Parameter[] { parameter }, stmt);
                functionDefinition.IsGenerator = true;
                return new GeneratorExpression(functionDefinition, list);
            }

            public expr elt {
                get { return _elt; }
                set { _elt = value; }
            }

            public PythonList generators {
                get { return _generators; }
                set { _generators = value; }
            }
        }

        [PythonType]
        public class Global : stmt
        {
            private PythonList _names;

            public Global() {
                _fields = new PythonTuple(new[] { "names", });
            }

            public Global(PythonList names, [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                _names = names;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal Global(GlobalStatement stmt)
                : this() {
                _names = new PythonList(stmt.Names);
            }

            internal override Statement Revert() {
                string[] newNames = new string[names.Count];
                for (int i = 0; i < names.Count; i++)
                    newNames[i] = (string)names[i];
                return new GlobalStatement(newNames);
            }

            public PythonList names {
                get { return _names; }
                set { _names = value; }
            }
        }

        [PythonType]
        public class Gt : cmpop
        {
            internal static Gt Instance = new Gt();
        }

        [PythonType]
        public class GtE : cmpop
        {
            internal static GtE Instance = new GtE();
        }

        [PythonType]
        public class If : stmt
        {
            private expr _test;
            private PythonList _body;
            private PythonList _orelse; // Optional, default []

            public If() {
                _fields = new PythonTuple(new[] { "test", "body", "orelse" });
            }

            public If(expr test, PythonList body, [Optional]PythonList orelse, 
                [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                _test = test;
                _body = body;
                if (null == orelse)
                    _orelse = new PythonList();
                else
                    _orelse = orelse;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal If(IfStatement stmt)
                : this() {
                If current = this;
                If parent = null;
                foreach (IfStatementTest ifTest in stmt.Tests) {
                    if (parent != null) {
                        current = new If();
                        parent._orelse = PythonOps.MakeListNoCopy(current);
                    }

                    current.Initialize(ifTest);
                    parent = current;
                }

                current._orelse = ConvertStatements(stmt.ElseStatement, true);
            }

            internal void Initialize(IfStatementTest ifTest) {
                _test = Convert(ifTest.Test);
                _body = ConvertStatements(ifTest.Body);
                GetSourceLocation(ifTest);
            }

            internal override Statement Revert() {
                List<IfStatementTest> tests = new List<IfStatementTest>();
                tests.Add(new IfStatementTest(expr.Revert(test), RevertStmts(body)));
                If currIf = this;
                while (currIf.orelse != null && currIf.orelse.Count == 1 && currIf.orelse[0] is If) {
                    If orelse = (If)currIf.orelse[0];
                    tests.Add(new IfStatementTest(expr.Revert(orelse.test), RevertStmts(orelse.body)));
                    currIf = orelse;
                }
                return new IfStatement(tests.ToArray(), RevertStmts(currIf.orelse));
            }

            public expr test {
                get { return _test; }
                set { _test = value; }
            }

            public PythonList body {
                get { return _body; }
                set { _body = value; }
            }

            public PythonList orelse {
                get { return _orelse; }
                set { _orelse = value; }
            }
        }

        [PythonType]
        public class IfExp : expr
        {
            private expr _test;
            private expr _body;
            private expr _orelse;

            public IfExp() {
                _fields = new PythonTuple(new[] { "test", "body", "orelse" });
            }

            public IfExp(expr test, expr body, expr orelse, [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                _test = test;
                _body = body;
                _orelse = orelse;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal IfExp(ConditionalExpression cond)
                : this() {
                _test = Convert(cond.Test);
                _body = Convert(cond.TrueExpression);
                _orelse = Convert(cond.FalseExpression);
            }

            internal override AstExpression Revert() {
                return new ConditionalExpression(expr.Revert(test), expr.Revert(body), expr.Revert(orelse));
            }

            public expr test {
                get { return _test; }
                set { _test = value; }
            }

            public expr body {
                get { return _body; }
                set { _body = value; }
            }

            public expr orelse {
                get { return _orelse; }
                set { _orelse = value; }
            }
        }

        private static char[] MODULE_NAME_SPLITTER = new char[1] { '.' };


        [PythonType]
        public class Import : stmt
        {
            private PythonList _names;

            public Import() {
                _fields = new PythonTuple(new[] { "names", });
            }

            public Import(PythonList names, [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                _names = names;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal Import(ImportStatement stmt)
                : this() {
                _names = ConvertAliases(stmt.Names, stmt.AsNames);
            }

            internal override Statement Revert() {
                ModuleName[] moduleNames = new ModuleName[names.Count];
                String[] asNames = new String[names.Count];
                for (int i = 0; i < names.Count; i++) {
                    alias alias = (alias)names[i];
                    moduleNames[i] = new ModuleName(alias.name.Split(MODULE_NAME_SPLITTER));
                    asNames[i] = alias.asname;
                }
                return new ImportStatement(moduleNames, asNames, false);  // TODO: not so sure about the relative/absolute argument here
            }

            public PythonList names {
                get { return _names; }
                set { _names = value; }
            }
        }

        [PythonType]
        public class ImportFrom : stmt
        {
            private string _module; // Optional
            private PythonList _names;
            private int _level; // Optional, default 0

            public ImportFrom() {
                _fields = new PythonTuple(new[] { "module", "names", "level" });
            }

            public ImportFrom([Optional]string module, PythonList names, [Optional]int level,
                [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                _module = module;
                _names = names;
                _level = level;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            public ImportFrom(FromImportStatement stmt)
                : this() {
                _module = stmt.Root.MakeString();
                _module = string.IsNullOrEmpty(_module) ? null : _module;
                _names = ConvertAliases(stmt.Names, stmt.AsNames);
                if (stmt.Root is RelativeModuleName)
                    _level = ((RelativeModuleName)stmt.Root).DotCount;
            }

            internal override Statement Revert() {
                ModuleName root = null;
                bool absolute = false; // TODO: absolute import appears in ModuleOptions, not sure how it should work together
                if (module != null)
                    if (module[0] == '.') // relative module
                        root = new RelativeModuleName(module.Split(MODULE_NAME_SPLITTER), level);
                    else {
                        root = new ModuleName(module.Split(MODULE_NAME_SPLITTER));
                        absolute = true;
                    }

                if (names.Count == 1 && ((alias)names[0]).name == "*")
                    return new FromImportStatement(root, (string[])FromImportStatement.Star, null, false, absolute);

                String[] newNames = new String[names.Count];
                String[] asNames = new String[names.Count];
                for (int i = 0; i < names.Count; i++) {
                    alias alias = (alias)names[i];
                    newNames[i] = alias.name;
                    asNames[i] = alias.asname;
                }
                return new FromImportStatement(root, newNames, asNames, false, absolute);
            }

            public string module {
                get { return _module; }
                set { _module = value; }
            }

            public PythonList names {
                get { return _names; }
                set { _names = value; }
            }

            public int level {
                get { return _level; }
                set { _level = value; }
            }
        }

        [PythonType]
        public class In : cmpop
        {
            internal static In Instance = new In();
        }

        [PythonType]
        public class Index : slice
        {
            private expr _value;

            public Index() {
                _fields = new PythonTuple(new[] { "value", });
            }

            public Index(expr value)
                : this() {
                _value = value;
            }

            public expr value {
                get { return _value; }
                set { _value = value; }
            }
        }

        [PythonType]
        public class Interactive : mod
        {
            private PythonList _body;

            public Interactive() {
                _fields = new PythonTuple(new[] { "body", });
            }

            public Interactive(PythonList body)
                : this() {
                _body = body;
            }

            internal Interactive(SuiteStatement suite)
                : this() {
                _body = ConvertStatements(suite);
            }

            internal override PythonList GetStatements() {
                return _body;
            }

            public PythonList body {
                get { return _body; }
                set { _body = value; }
            }
        }

        [PythonType]
        public class Invert : unaryop
        {
            internal static Invert Instance = new Invert();
        }

        [PythonType]
        public class Is : cmpop
        {
            internal static Is Instance = new Is();
        }

        [PythonType]
        public class IsNot : cmpop
        {
            internal static IsNot Instance = new IsNot();
        }

        [PythonType]
        public class Lambda : expr
        {
            private arguments _args;
            private expr _body;

            public Lambda() {
                _fields = new PythonTuple(new[] { "args", "body" });
            }

            public Lambda(arguments args, expr body, [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                _args = args;
                _body = body;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal Lambda(LambdaExpression lambda)
                : this() {
                FunctionDef def = (FunctionDef)Convert(lambda.Function);
                _args = def.args;
                Debug.Assert(def.body.Count == 1, "LambdaExpression body should be one statement.");
                stmt statement = (stmt)def.body[0];
                if (statement is Return)
                    _body = ((Return)statement).value;
                else if (statement is Expr) {
                    // What should be sufficient is:
                    // _body = ((Expr)statement).value;
                    // but, AST comes with trees containing twice YieldExpression.
                    // For: 
                    //   lamba x: (yield x) 
                    // it comes back with:
                    //
                    //IronPython.Compiler.Ast.LambdaExpression
                    //IronPython.Compiler.Ast.FunctionDefinition<lambda$334>generator
                    //  IronPython.Compiler.Ast.Parameter  x
                    //  IronPython.Compiler.Ast.ExpressionStatement
                    //    IronPython.Compiler.Ast.YieldExpression     <<<<<<<<
                    //      IronPython.Compiler.Ast.YieldExpression   <<<<<<<< why twice?
                    //        IronPython.Compiler.Ast.NameExpression x

                    _body = ((Yield)((Expr)statement).value).value;
                }  else
                    throw PythonOps.TypeError("Unexpected statement type: {0}, expected Return or Expr", statement.GetType());
            }

            internal override AstExpression Revert() {
                Statement newBody;
                AstExpression exp = expr.Revert(body);
                if (!_containsYield)
                    newBody = new ReturnStatement(exp);
                else
                    newBody = new ExpressionStatement(exp);
                Parameter[] para = args.Revert();
                FunctionDefinition fd = new FunctionDefinition(null, para, newBody);
                fd.IsGenerator = _containsYield;
                _containsYield = false;
                return new LambdaExpression(fd);
            }

            public arguments args {
                get { return _args; }
                set { _args = value; }
            }

            public expr body {
                get { return _body; }
                set { _body = value; }
            }
        }

        [PythonType]
        public class List : expr
        {
            private PythonList _elts;
            private expr_context _ctx;

            public List() {
                _fields = new PythonTuple(new[] { "elts", "ctx" });
            }

            public List(PythonList elts, expr_context ctx, [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                _elts = elts;
                _ctx = ctx;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal List(ListExpression list, expr_context ctx)
                : this() {
                _elts = PythonOps.MakeEmptyList(list.Items.Count);
                foreach (AstExpression expr in list.Items)
                    _elts.Add(Convert(expr, ctx));

                _ctx = ctx;
            }

            internal override AstExpression Revert() {
                AstExpression[] e = new AstExpression[elts.Count];
                int i = 0;
                foreach (expr el in elts)
                    e[i++] = expr.Revert(el);
                return new ListExpression(e);
            }

            public PythonList elts {
                get { return _elts; }
                set { _elts = value; }
            }

            public expr_context ctx {
                get { return _ctx; }
                set { _ctx = value; }
            }
        }

        [PythonType]
        public class ListComp : expr
        {
            private expr _elt;
            private PythonList _generators;

            public ListComp() {
                _fields = new PythonTuple(new[] { "elt", "generators" });
            }

            public ListComp(expr elt, PythonList generators, [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                _elt = elt;
                _generators = generators;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal ListComp(ListComprehension comp)
                : this() {
                _elt = Convert(comp.Item);
                _generators = Convert(comp.Iterators);
            }

            internal override AstExpression Revert() {
                AstExpression item = expr.Revert(elt);
                ComprehensionIterator[] iters = comprehension.RevertComprehensions(generators);
                return new ListComprehension(item, iters);
            }

            public expr elt {
                get { return _elt; }
                set { _elt = value; }
            }

            public PythonList generators {
                get { return _generators; }
                set { _generators = value; }
            }
        }

        [PythonType]
        public class Load : expr_context
        {
            internal static Load Instance = new Load();
        }

        [PythonType]
        public class Lt : cmpop
        {
            internal static Lt Instance = new Lt();
        }

        [PythonType]
        public class LtE : cmpop
        {
            internal static LtE Instance = new LtE();
        }

        [PythonType]
        public class LShift : @operator
        {
            internal static LShift Instance = new LShift();
        }

        [PythonType]
        public class Mod : @operator
        {
            internal static Mod Instance = new Mod();
        }

        [PythonType]
        public class Module : mod
        {
            private PythonList _body;

            public Module() {
                _fields = new PythonTuple(new[] { "body", });
            }

            public Module(PythonList body)
                : this() {
                _body = body;
            }

            internal Module(SuiteStatement suite)
                : this() {
                _body = ConvertStatements(suite);
            }

            public PythonList body {
                get { return _body; }
                set { _body = value; }
            }

            internal override PythonList GetStatements() {
                return _body;
            }
        }

        [PythonType]
        public class Mult : @operator
        {
            internal static Mult Instance = new Mult();
        }

        [PythonType]
        public class Name : expr
        {
            private string _id;
            private expr_context _ctx;

            public Name() {
                _fields = new PythonTuple(new[] { "id", "ctx" });
            }

            public Name(string id, expr_context ctx, [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                _id = id;
                _ctx = ctx;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            public Name(String id, expr_context ctx)
                : this(id, ctx, null, null) { }

            internal Name(Parameter para)
                : this(para.Name, Param.Instance) {
                GetSourceLocation(para);
            }

            internal Name(NameExpression expr, expr_context ctx)
                : this(expr.Name, ctx) {
                GetSourceLocation(expr);
            }

            internal override AstExpression Revert() {
                return new NameExpression(id);
            }

            public expr_context ctx {
                get { return _ctx; }
                set { _ctx = value; }
            }

            public string id {
                get { return _id; }
                set { _id = value; }
            }
        }

        [PythonType]
        public class Not : unaryop
        {
            internal static Not Instance = new Not();
        }

        [PythonType]
        public class NotEq : cmpop
        {
            internal static NotEq Instance = new NotEq();
        }

        [PythonType]
        public class NotIn : cmpop
        {
            internal static NotIn Instance = new NotIn();
        }

        [PythonType]
        public class Num : expr
        {
            private object _n;

            public Num() {
                _fields = new PythonTuple(new[] { "n", });
            }

            internal Num(object n)
                : this(n, null, null) { }

            public Num(object n, [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                _n = n;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal override AstExpression Revert() {
                return new ConstantExpression(n);
            }

            public object n {
                get { return _n; }
                set { _n = value; }
            }
        }

        [PythonType]
        public class Or : boolop
        {
            internal static Or Instance = new Or();
        }

        [PythonType]
        public class Param : expr_context
        {
            internal static Param Instance = new Param();
        }

        [PythonType]
        public class Pass : stmt
        {
            internal static Pass Instance = new Pass();

            internal Pass()
                : this(null, null) { }

            public Pass([Optional]int? lineno, [Optional]int? col_offset) {
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal override Statement Revert() {
                return new EmptyStatement();
            }
        }

        [PythonType]
        public class Pow : @operator
        {
            internal static Pow Instance = new Pow();
        }

        [PythonType]
        public class Print : stmt
        {
            private expr _dest; // optional
            private PythonList _values;
            private bool _nl;

            public Print() {
                _fields = new PythonTuple(new[] { "dest", "values", "nl" });
            }

            public Print([Optional]expr dest, PythonList values, bool nl,
               [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                _dest = dest;
                _values = values;
                _nl = nl;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            public expr dest {
                get { return _dest; }
                set { _dest = value; }
            }

            public PythonList values {
                get { return _values; }
                set { _values = value; }
            }

            public bool nl {
                get { return _nl; }
                set { _nl = value; }
            }
        }

        [PythonType]
        public class Raise : stmt
        {
            private expr _type; // Optional
            private expr _inst; // Optional
            private expr _tback; // Optional

            public Raise() {
                _fields = new PythonTuple(new[] { "type", "inst", "tback" });
            }

            public Raise([Optional]expr type, [Optional]expr inst, [Optional]expr tback,
                [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                _type = type;
                _inst = inst;
                _tback = tback;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal Raise(RaiseStatement stmt)
                : this() {
                if (stmt.ExceptType != null)
                    _type = Convert(stmt.ExceptType);
                if (stmt.Value != null)
                    _inst = Convert(stmt.Value);
                if (stmt.Traceback != null)
                    _tback = Convert(stmt.Traceback);
            }

            internal override Statement Revert() {
                return new RaiseStatement(expr.Revert(type), expr.Revert(inst), expr.Revert(tback));
            }

            public expr type {
                get { return _type; }
                set { _type = value; }
            }

            public expr inst {
                get { return _inst; }
                set { _inst = value; }
            }

            public expr tback {
                get { return _tback; }
                set { _tback = value; }
            }
        }

        [PythonType]
        public class Repr : expr
        {
            private expr _value;

            public Repr() {
                _fields = new PythonTuple(new[] { "value", });
            }

            public Repr(expr value, [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                _value = value;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal Repr(BackQuoteExpression expr)
                : this() {
                _value = Convert(expr.Expression);
            }

            internal override AstExpression Revert() {
                return new BackQuoteExpression(expr.Revert(value));
            }

            public expr value {
                get { return _value; }
                set { _value = value; }
            }
        }

        [PythonType]
        public class Return : stmt
        {
            private expr _value; // Optional

            public Return() {
                _fields = new PythonTuple(new[] { "value", });
            }

            public Return([Optional]expr value, [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                _value = value;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            public Return(ReturnStatement statement)
                : this() {
                // statement.Expression is never null
                //or is it?
                if (statement.Expression == null)
                    _value = null;
                else
                    _value = Convert(statement.Expression);
            }

            internal override Statement Revert() {
                return new ReturnStatement(expr.Revert(value));
            }

            public expr value {
                get { return _value; }
                set { _value = value; }
            }
        }

        [PythonType]
        public class RShift : @operator
        {
            internal static RShift Instance = new RShift();
        }

        [PythonType]
        public class Set : expr 
        {
            private PythonList _elts;

            public Set() {
                _fields = new PythonTuple(new[] { "elts" });
            }

            public Set(PythonList elts, [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                _elts = elts;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal Set(SetExpression setExpression)
                : this() {
                _elts = new PythonList(setExpression.Items.Count);
                foreach (AstExpression item in setExpression.Items) {
                    _elts.Add(Convert(item));
                }
            }

            internal override AstExpression Revert() {
                AstExpression[] e = new AstExpression[elts.Count];
                int i = 0;
                foreach (expr el in elts)
                    e[i++] = expr.Revert(el);
                return new SetExpression(e);
            }

            public PythonList elts {
                get { return _elts; }
                set { _elts = value; }
            }
        }

        [PythonType]
        public class SetComp : expr 
        {
            private expr _elt;
            private PythonList _generators;

            public SetComp() {
                _fields = new PythonTuple(new[] { "elt", "generators" });
            }

            public SetComp(expr elt, PythonList generators, [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                _elt = elt;
                _generators = generators;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal SetComp(SetComprehension comp)
                : this() {  
                _elt = Convert(comp.Item);
                _generators = Convert(comp.Iterators);
            }

            internal override AstExpression Revert() {
                AstExpression item = expr.Revert(elt);
                ComprehensionIterator[] iters = comprehension.RevertComprehensions(generators);
                return new SetComprehension(item, iters);
            }

            public expr elt {
                get { return _elt; }
                set { _elt = value; }
            }

            public PythonList generators {
                get { return _generators; }
                set { _generators = value; }
            }
        }


        [PythonType]
        public class Slice : slice
        {
            private expr _lower; // Optional
            private expr _upper; // Optional
            private expr _step; // Optional

            public Slice() {
                _fields = new PythonTuple(new[] { "lower", "upper", "step" });
            }


            public Slice([Optional]expr lower, [Optional]expr upper, [Optional]expr step)
                // default interpretation of missing step is [:]
                // in order to get [::], please provide explicit Name('None',Load.Instance)
                : this() {
                _lower = lower;
                _upper = upper;
                _step = step;
            }

            internal Slice(SliceExpression expr)
                : this() {
                if (expr.SliceStart != null)
                    _lower = Convert(expr.SliceStart);
                if (expr.SliceStop != null)
                    _upper = Convert(expr.SliceStop);
                if (expr.StepProvided)
                    if (expr.SliceStep != null)
                        _step = Convert(expr.SliceStep); // [x:y:z]
                    else
                        _step = new Name("None", Load.Instance); // [x:y:]
            }

            public expr lower {
                get { return _lower; }
                set { _lower = value; }
            }

            public expr upper {
                get { return _upper; }
                set { _upper = value; }
            }

            public expr step {
                get { return _step; }
                set { _step = value; }
            }
        }

        [PythonType]
        public class Store : expr_context
        {
            internal static Store Instance = new Store();
        }

        [PythonType]
        public class Str : expr
        {
            private string _s;

            public Str() {
                _fields = new PythonTuple(new[] { "s", });
            }

            internal Str(String s)
                : this(s, null, null) { }

            public Str(string s, [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                _s = s;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal override AstExpression Revert() {
                return new ConstantExpression(s);
            }

            public string s {
                get { return _s; }
                set { _s = value; }
            }
        }

        [PythonType]
        public class Sub : @operator
        {
            internal static Sub Instance = new Sub();
        }

        [PythonType]
        public class Subscript : expr
        {
            private expr _value;
            private slice _slice;
            private expr_context _ctx;

            public Subscript() {
                _fields = new PythonTuple(new[] { "value", "slice", "ctx" });
            }

            public Subscript( expr value, slice slice, expr_context ctx, 
                [Optional]int? lineno, [Optional]int? col_offset )
                : this() {
                _value = value;
                _slice = slice;
                _ctx = ctx;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal Subscript(IndexExpression expr, expr_context ctx)
                : this() {
                _value = Convert(expr.Target);
                _ctx = ctx;
                _slice = TrySliceConvert(expr.Index);
                if (_slice == null)
                    _slice = new Index(Convert(expr.Index));
            }

            internal override AstExpression Revert() {
                AstExpression index = null;
                if (slice is Index)
                    index = expr.Revert(((Index)slice).value);
                else if (slice is Slice) {
                    Slice concreteSlice = (Slice)slice;
                    AstExpression start = null;
                    if (concreteSlice.lower != null)
                        start = expr.Revert(concreteSlice.lower);
                    AstExpression stop = null;
                    if (concreteSlice.upper != null)
                        stop = expr.Revert(concreteSlice.upper);
                    AstExpression step = null;
                    bool stepProvided = false;
                    if (concreteSlice.step != null) {
                        stepProvided = true;
                        if (concreteSlice.step is Name && ((Name)concreteSlice.step).id == "None") {
                            // pass
                        } else {
                            step = expr.Revert(concreteSlice.step);
                        }
                    }
                    index = new SliceExpression(start, stop, step, stepProvided);
                } else if (slice is Ellipsis) {
                    index = new ConstantExpression(PythonOps.Ellipsis);
                } else if (slice is ExtSlice) {
                    index = new TupleExpression(true, ((ExtSlice)slice).Revert());
                } else {
                    Debug.Assert(false, "Unexpected type when converting Subscript: " + slice.GetType());
                }
                return new IndexExpression(expr.Revert(value), index);
            }

            public expr value {
                get { return _value; }
                set { _value = value; }
            }

            public slice slice {
                get { return _slice; }
                set { _slice = value; }
            }

            public expr_context ctx {
                get { return _ctx; }
                set { _ctx = value; }
            }
        }

        /// <summary>
        /// Not an actual node. We don't create this, but it's here for compatibility.
        /// </summary>
        [PythonType]
        public class Suite : mod
        {
            private PythonList _body;

            public Suite() {
                _fields = new PythonTuple(new[] { "body", });
            }

            public Suite(PythonList body)
                : this() {
                _body = body;
            }

            public PythonList body {
                get { return _body; }
                set { _body = value; }
            }

            internal override PythonList GetStatements() {
                return _body;
            }
        }

        [PythonType]
        public class TryExcept : stmt
        {
            private PythonList _body;
            private PythonList _handlers;
            private PythonList _orelse; // Optional, default []

            public TryExcept() {
                _fields = new PythonTuple(new[] { "body", "handlers", "orelse" });
            }

            public TryExcept(PythonList body, PythonList handlers, [Optional]PythonList orelse,
                [Optional]int? lineno, [Optional]int? col_offset ) 
                : this() {
                _body = body;
                _handlers = handlers;
                if (null == orelse)
                    _orelse = new PythonList();
                else
                    _orelse = orelse;
                _lineno = lineno;
                _col_offset = col_offset;
            }


            internal TryExcept(TryStatement stmt)
                : this() {
                _body = ConvertStatements(stmt.Body);

                _handlers = PythonOps.MakeEmptyList(stmt.Handlers.Count);
                foreach (TryStatementHandler tryStmt in stmt.Handlers)
                    _handlers.Add(Convert(tryStmt));

                _orelse = ConvertStatements(stmt.Else, true);
            }

            internal override Statement Revert() {
                TryStatementHandler[] tshs = new TryStatementHandler[handlers.Count];
                for (int i = 0; i < handlers.Count; i++) {
                    tshs[i] = ((ExceptHandler)handlers[i]).RevertHandler();
                }
                return new TryStatement(RevertStmts(body), tshs, RevertStmts(orelse), null);
            }

            public PythonList body {
                get { return _body; }
                set { _body = value; }
            }

            public PythonList handlers {
                get { return _handlers; }
                set { _handlers = value; }
            }

            public PythonList orelse {
                get { return _orelse; }
                set { _orelse = value; }
            }
        }

        [PythonType]
        public class TryFinally : stmt
        {
            private PythonList _body;
            private PythonList _finalbody;

            public TryFinally() {
                _fields = new PythonTuple(new[] { "body", "finalbody" });
            }

            public TryFinally(PythonList body, PythonList finalBody, 
                [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                _body = body;
                _finalbody = finalbody;
                _lineno = lineno;
                _col_offset = col_offset;
            }


            internal TryFinally(PythonList body, PythonList finalbody)
                : this() {
                _body = body;
                _finalbody = finalbody;
            }

            internal override Statement Revert() {
                if (body.Count == 1 && body[0] is TryExcept) {
                    TryExcept te = (TryExcept)body[0];
                    TryStatementHandler[] tshs = new TryStatementHandler[te.handlers.Count];
                    for (int i = 0; i < te.handlers.Count; i++) {
                        tshs[i] = ((ExceptHandler)te.handlers[i]).RevertHandler();
                    }
                    return new TryStatement(RevertStmts(te.body), tshs, RevertStmts(te.orelse), RevertStmts(finalbody));
                }
                return new TryStatement(RevertStmts(body), null, null, RevertStmts(finalbody));
            }

            public PythonList body {
                get { return _body; }
                set { _body = value; }
            }

            public PythonList finalbody {
                get { return _finalbody; }
                set { _finalbody = value; }
            }
        }

        [PythonType]
        public class Tuple : expr
        {
            private PythonList _elts;
            private expr_context _ctx;

            public Tuple() {
                _fields = new PythonTuple(new[] { "elts", "ctx" });
            }

            public Tuple(PythonList elts, expr_context ctx, [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                _elts = elts;
                _ctx = ctx;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal Tuple(TupleExpression list, expr_context ctx)
                : this() {
                _elts = PythonOps.MakeEmptyList(list.Items.Count);
                foreach (AstExpression expr in list.Items)
                    _elts.Add(Convert(expr, ctx));

                _ctx = ctx;
            }

            internal override AstExpression Revert() {
                AstExpression[] e = new AstExpression[elts.Count];
                int i = 0;
                foreach (expr el in elts)
                    e[i++] = expr.Revert(el);
                return new TupleExpression(false, e);
            }

            public PythonList elts {
                get { return _elts; }
                set { _elts = value; }
            }

            public expr_context ctx {
                get { return _ctx; }
                set { _ctx = value; }
            }
        }

        [PythonType]
        public class UnaryOp : expr
        {
            private unaryop _op;
            private expr _operand;

            public UnaryOp() {
                _fields = new PythonTuple(new[] { "op", "operand" });
            }

            internal UnaryOp(UnaryExpression expression)
                : this() {
                _op = (unaryop)Convert(expression.Op);
                _operand = Convert(expression.Expression);
            }

            public UnaryOp(unaryop op, expr operand, [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                _op = op;
                _operand = operand;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal override AstExpression Revert() {
                return new UnaryExpression(op.Revert(), expr.Revert(operand));
            }

            public unaryop op {
                get { return _op; }
                set { _op = value; }
            }

            public expr operand {
                get { return _operand; }
                set { _operand = value; }
            }

            internal expr TryTrimTrivialUnaryOp() {
                // in case of +constant or -constant returns underlying Num
                // representation, otherwise unmodified itself
                var num = _operand as Num;
                if (null == num) {
                    return this;
                }
                if (_op is UAdd) {
                    return num;
                }
                if (!(_op is USub)) {
                    return this;
                }
                // list of possible types can be found in:
                // class AST {
                //     internal static expr Convert(ConstantExpression expr);
                // }
                if (num.n is int) {
                    num.n = -(int)num.n;
                } else if (num.n is double) {
                    num.n = -(double)num.n;
                } else if (num.n is Int64) {
                    num.n = -(Int64)num.n;
                } else if (num.n is BigInteger) {
                    num.n = -(BigInteger)num.n;
                } else if (num.n is Complex) {
                    num.n = -(Complex)num.n;
                } else {
                    return this;
                }
                return num;
            }
        }

        [PythonType]
        public class UAdd : unaryop
        {
            internal static UAdd Instance = new UAdd();
        }

        [PythonType]
        public class USub : unaryop
        {
            internal static USub Instance = new USub();
        }

        [PythonType]
        public class While : stmt
        {
            private expr _test;
            private PythonList _body;
            private PythonList _orelse; // Optional, default []

            public While() {
                _fields = new PythonTuple(new[] { "test", "body", "orelse" });
            }

            public While(expr test, PythonList body, [Optional]PythonList orelse,
                [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                _test = test;
                _body = body;
                if (null == orelse)
                    _orelse = new PythonList();
                else
                    _orelse = orelse;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal While(WhileStatement stmt)
                : this() {
                _test = Convert(stmt.Test);
                _body = ConvertStatements(stmt.Body);
                _orelse = ConvertStatements(stmt.ElseStatement, true);
            }

            internal override Statement Revert() {
                return new WhileStatement(expr.Revert(test), RevertStmts(body), RevertStmts(orelse));
            }

            public expr test {
                get { return _test; }
                set { _test = value; }
            }

            public PythonList body {
                get { return _body; }
                set { _body = value; }
            }

            public PythonList orelse {
                get { return _orelse; }
                set { _orelse = value; }
            }
        }

        [PythonType]
        public class With : stmt
        {
            private expr _context_expr;
            private expr _optional_vars; // Optional
            private PythonList _body;

            public With() {
                _fields = new PythonTuple(new[] { "context_expr", "optional_vars", "body" });
            }

            public With(expr context_expr, [Optional]expr optional_vars, PythonList body,
                [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                _context_expr = context_expr;
                _optional_vars = optional_vars;
                _body = body;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal With(WithStatement with)
                : this() {
                _context_expr = Convert(with.ContextManager);
                if (with.Variable != null)
                    _optional_vars = Convert(with.Variable);

                _body = ConvertStatements(with.Body);
            }

            internal override Statement Revert() {
                return new WithStatement(expr.Revert(context_expr), expr.Revert(optional_vars), RevertStmts(body));
            }

            public expr context_expr {
                get { return _context_expr; }
                set { _context_expr = value; }
            }

            public expr optional_vars {
                get { return _optional_vars; }
                set { _optional_vars = value; }
            }

            public PythonList body {
                get { return _body; }
                set { _body = value; }
            }
        }

        // if yield is detected, the containing function has to be marked as generator
        private static bool _containsYield = false;

        [PythonType]
        public class Yield : expr
        {
            private expr _value; // Optional

            public Yield() {
                _fields = new PythonTuple(new[] { "value", });
            }

            public Yield([Optional]expr value, [Optional]int? lineno, [Optional]int? col_offset) 
                : this() {
                _value = value;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal Yield(YieldExpression expr)
                : this() {
                // expr.Expression is never null
                _value = Convert(expr.Expression);
            }

            internal override AstExpression Revert() {
                _containsYield = true;
                return new YieldExpression(expr.Revert(value));
            }

            public expr value {
                get { return _value; }
                set { _value = value; }
            }
        }
    }
}
