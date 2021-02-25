// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.
//
// Copyright (c) Jeff Hardy 2010.
// Copyright (c) Dan Eloff 2008-2009.
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;

using IronPython.Compiler;
using IronPython.Compiler.Ast;
using IronPython.Runtime;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using AstExpression = IronPython.Compiler.Ast.Expression;
using Generic = System.Collections.Generic;

[assembly: PythonModule("_ast", typeof(IronPython.Modules._ast))]
namespace IronPython.Modules {
    public static class _ast {
        public const string __version__ = "62047";
        public const int PyCF_ONLY_AST = 0x400;

        private class ThrowingErrorSink : ErrorSink {
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

        internal static PythonAst ConvertToPythonAst(CodeContext codeContext, AST source, string filename) {
            Statement stmt;
            PythonCompilerOptions options = new PythonCompilerOptions(ModuleOptions.ExecOrEvalCode);
            SourceUnit unit = new SourceUnit(codeContext.LanguageContext, NullTextContentProvider.Null, filename, SourceCodeKind.AutoDetect);
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
                throw PythonOps.TypeError("unsupported type of AST: {0}", (source.GetType()));

            return new PythonAst(stmt, false, ModuleOptions.ExecOrEvalCode, printExpression, compilerContext, new int[] { });
        }

        internal static AST BuildAst(CodeContext context, SourceUnit sourceUnit, PythonCompilerOptions opts, string mode) {
            using (Parser parser = Parser.CreateParser(
                new CompilerContext(sourceUnit, opts, ThrowingErrorSink.Default),
                (PythonOptions)context.LanguageContext.Options)) {

                PythonAst ast = parser.ParseFile(true);
                return ConvertToAST(ast, mode);
            }
        }

        private static mod ConvertToAST(PythonAst pythonAst, string kind) {
            ContractUtils.RequiresNotNull(pythonAst, nameof(pythonAst));
            ContractUtils.RequiresNotNull(kind, nameof(kind));
            return ConvertToAST((SuiteStatement)pythonAst.Body, kind);
        }

        private static mod ConvertToAST(SuiteStatement suite, string kind) {
            ContractUtils.RequiresNotNull(suite, nameof(suite));
            ContractUtils.RequiresNotNull(kind, nameof(kind));
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
        public abstract class AST {
            protected int? _lineno; // both lineno and col_offset are expected to be int, in cpython anything is accepted
            protected int? _col_offset;

            public PythonTuple _fields { get; protected set; } = PythonTuple.EMPTY;

            public PythonTuple _attributes { get; protected set; } = PythonTuple.EMPTY;

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
                restoreProperties(_attributes, state);
                restoreProperties(_fields, state);
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
                storeProperties(_fields, d);
                storeProperties(_attributes, d);
                return d;
            }

            public virtual object/*!*/ __reduce__() {
                return PythonTuple.MakeTuple(DynamicHelpers.GetPythonType(this), PythonTuple.EMPTY, getstate());
            }

            public virtual object/*!*/ __reduce_ex__(int protocol) {
                return __reduce__();
            }

            [PythonHidden]
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
                        return new PythonList(0);
                    else
                        throw new ArgumentNullException(nameof(stmt));

                if (stmt is SuiteStatement) {
                    SuiteStatement suite = (SuiteStatement)stmt;
                    PythonList list = new PythonList(suite.Statements.Count);
                    foreach (Statement s in suite.Statements)
                        if (s is SuiteStatement)  // multiple stmt in a line
                            foreach (Statement s2 in ((SuiteStatement)s).Statements)
                                list.Add(Convert(s2));
                        else
                            list.Add(Convert(s));

                    return list;
                }

                return PythonList.FromArrayNoCopy(Convert(stmt));
            }

            internal static stmt Convert(Statement stmt) {
                stmt ast = stmt switch
                {
                    FunctionDefinition s => new FunctionDef(s),
                    ReturnStatement s => new Return(s),
                    AssignmentStatement s => new Assign(s),
                    AugmentedAssignStatement s => new AugAssign(s),
                    DelStatement s => new Delete(s),
                    ExpressionStatement s => new Expr(s),
                    ForStatement s => new For(s),
                    WhileStatement s => new While(s),
                    IfStatement s => new If(s),
                    WithStatement s => new With(s),
                    RaiseStatement s => new Raise(s),
                    TryStatement s => new Try(s),
                    AssertStatement s => new Assert(s),
                    ImportStatement s => new Import(s),
                    FromImportStatement s => new ImportFrom(s),
                    GlobalStatement s => new Global(s),
                    NonlocalStatement s => new Nonlocal(s),
                    ClassDefinition s => new ClassDef(s),
                    BreakStatement _ => new Break(),
                    ContinueStatement _ => new Continue(),
                    EmptyStatement _ => new Pass(),
                    _ => throw new ArgumentTypeException("Unexpected statement type: " + stmt.GetType()),
                };
                ast.GetSourceLocation(stmt);
                return ast;
            }

            internal static PythonList ConvertAliases(IList<DottedName> names, IList<string> asnames) {
                PythonList list = new PythonList(names.Count);

                if (names == FromImportStatement.Star) // does it ever happen?
                    list.Add(new alias("*", null));
                else
                    for (int i = 0; i < names.Count; i++)
                        list.Add(new alias(names[i].MakeString(), asnames[i]));

                return list;
            }

            internal static PythonList ConvertAliases(IList<string> names, IList<string> asnames) {
                PythonList list = new PythonList(names.Count);

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
                if (expr is TupleExpression && ((TupleExpression)expr).IsExpandable)
                    return new ExtSlice(((Tuple)Convert(expr)).elts);
                return null;
            }

            internal static expr Convert(AstExpression expr) {
                return Convert(expr, Load.Instance);
            }

            internal static expr Convert(AstExpression expr, expr_context ctx) {
                var ast = expr switch
                {
                    ConstantExpression x => Convert(x),
                    NameExpression x => new Name(x, ctx),
                    UnaryExpression x => new UnaryOp(x).TryTrimTrivialUnaryOp(),
                    BinaryExpression x => Convert(x),
                    AndExpression x => new BoolOp(x),
                    OrExpression x => new BoolOp(x),
                    CallExpression x => new Call(x),
                    ParenthesisExpression x => Convert(x.Expression),
                    LambdaExpression x => new Lambda(x),
                    ListExpression x => new List(x, ctx),
                    TupleExpression x => new Tuple(x, ctx),
                    DictionaryExpression x => new Dict(x),
                    ListComprehension x => new ListComp(x),
                    GeneratorExpression x => new GeneratorExp(x),
                    MemberExpression x => new Attribute(x, ctx),
                    YieldExpression x => new Yield(x),
                    YieldFromExpression x => new YieldFrom(x),
                    ConditionalExpression x => new IfExp(x),
                    IndexExpression x => new Subscript(x, ctx),
                    SetExpression x => new Set(x),
                    DictionaryComprehension x => new DictComp(x),
                    SetComprehension x => new SetComp(x),
                    StarredExpression x => new Starred(x, ctx),
                    _ => throw new ArgumentTypeException("Unexpected expression type: " + expr.GetType()),
                };
                ast.GetSourceLocation(expr);
                return ast;
            }

            internal static expr Convert(ConstantExpression expr) {
                expr ast;

                if (expr.Value == null || expr.Value is bool)
                    return new NameConstant(expr.Value);

                if (expr.Value is int || expr.Value is double || expr.Value is long || expr.Value is BigInteger || expr.Value is Complex)
                    ast = new Num(expr.Value);
                else if (expr.Value is string)
                    ast = new Str((string)expr.Value);
                else if (expr.Value is IronPython.Runtime.Bytes)
                    ast = new Bytes((IronPython.Runtime.Bytes)expr.Value);
                else if (expr.Value is IronPython.Runtime.Types.Ellipsis)
                    ast = Ellipsis.Instance;
                else
                    throw new ArgumentTypeException("Unexpected constant type: " + expr.Value.GetType());

                return ast;
            }

            internal static expr Convert(BinaryExpression expr) {
                AST op = Convert(expr.Operator);
                if (BinaryExpression.IsComparison(expr)) {
                    return new Compare(expr);
                }

                if (op is @operator) {
                    return new BinOp(expr, (@operator)op);
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

            internal static PythonList Convert(IList<ComprehensionIterator> iters) {
                var cfCollector = new List<ComprehensionFor>();
                var cifCollector = new List<List<ComprehensionIf>>();
                List<ComprehensionIf> cif = null;
                for (int i = 0; i < iters.Count; i++) {
                    switch(iters[i]) {
                        case ComprehensionFor cf:
                            cfCollector.Add(cf);
                            cif = new List<ComprehensionIf>();
                            cifCollector.Add(cif);
                            break;
                        case ComprehensionIf ci:
                            cif.Add(ci);
                            break;
                        default:
                            throw new InvalidOperationException();
                    }
                }

                PythonList comps = new PythonList();
                for (int i = 0; i < cfCollector.Count; i++)
                    comps.Add(new comprehension(cfCollector[i], cifCollector[i].ToArray()));
                return comps;
            }

            internal static AST Convert(PythonOperator op) {
                // We treat operator classes as singletons here to keep overhead down
                // But we cannot fully make them singletons if we wish to keep compatibility wity CPython
                return op switch
                {
                    PythonOperator.Add => Add.Instance,
                    PythonOperator.BitwiseAnd => BitAnd.Instance,
                    PythonOperator.BitwiseOr => BitOr.Instance,
                    PythonOperator.TrueDivide => Div.Instance,
                    PythonOperator.Equal => Eq.Instance,
                    PythonOperator.FloorDivide => FloorDiv.Instance,
                    PythonOperator.GreaterThan => Gt.Instance,
                    PythonOperator.GreaterThanOrEqual => GtE.Instance,
                    PythonOperator.In => In.Instance,
                    PythonOperator.Invert => Invert.Instance,
                    PythonOperator.Is => Is.Instance,
                    PythonOperator.IsNot => IsNot.Instance,
                    PythonOperator.LeftShift => LShift.Instance,
                    PythonOperator.LessThan => Lt.Instance,
                    PythonOperator.LessThanOrEqual => LtE.Instance,
                    PythonOperator.MatMult => MatMult.Instance,
                    PythonOperator.Mod => Mod.Instance,
                    PythonOperator.Multiply => Mult.Instance,
                    PythonOperator.Negate => USub.Instance,
                    PythonOperator.Not => Not.Instance,
                    PythonOperator.NotEqual => NotEq.Instance,
                    PythonOperator.NotIn => NotIn.Instance,
                    PythonOperator.Pos => UAdd.Instance,
                    PythonOperator.Power => Pow.Instance,
                    PythonOperator.RightShift => RShift.Instance,
                    PythonOperator.Subtract => Sub.Instance,
                    PythonOperator.Xor => BitXor.Instance,
                    _ => throw new ArgumentException("Unexpected PythonOperator: " + op, nameof(op)),
                };
            }
        }

        [PythonType]
        public class alias : AST {
            public alias() {
                _fields = PythonTuple.MakeTuple(new[] { nameof(name), nameof(asname) });
            }

            public alias(string name, [Optional]string asname)
                : this() {
                this.name = name;
                this.asname = asname;
            }

            public string name { get; set; }

            public string asname { get; set; }
        }

        public static PythonType arg => DynamicHelpers.GetPythonTypeFromType(typeof(ArgType));

        [PythonType("arg"), PythonHidden]
        public class ArgType : AST {
            private static PythonTuple __attributes = PythonTuple.MakeTuple(new[] { nameof(lineno), nameof(col_offset) });
            private static PythonTuple __fields = PythonTuple.MakeTuple(new[] { nameof(arg), nameof(annotation) });

            public ArgType() {
                _attributes = __attributes;
                _fields = __fields;
            }

            public ArgType(string arg, object annotation) : this() {
                this.arg = arg;
                this.annotation = annotation;
            }

            internal ArgType(Parameter parameter) : this() {
                arg = parameter.Name;
                annotation = parameter.Annotation == null ? null : Convert(parameter.Annotation);
                GetSourceLocation(parameter);
            }

            public string arg { get; set; }
            public object annotation { get; set; }
        }

        [PythonType]
        public class arguments : AST {
            public arguments() {
                _fields = PythonTuple.MakeTuple(new[] { nameof(args), nameof(vararg), nameof(kwonlyargs), nameof(kw_defaults), nameof(kwarg), nameof(defaults) });
            }

            public arguments(PythonList args, ArgType vararg, PythonList kwonlyargs, PythonList kw_defaults, ArgType kwarg, PythonList defaults)
                : this() {
                this.args = args;
                this.vararg = vararg;
                this.kwonlyargs = kwonlyargs;
                this.kw_defaults = kw_defaults;
                this.kwarg = kwarg;
                this.defaults = defaults;
            }

            internal arguments(IList<Parameter> parameters)
                : this() {
                args = new PythonList(parameters.Count);
                defaults = new PythonList(parameters.Count);
                kwonlyargs = new PythonList();
                kw_defaults = new PythonList();
                foreach (Parameter param in parameters) {
                    switch (param.Kind) {
                        case ParameterKind.List:
                            vararg = new ArgType(param);
                            break;
                        case ParameterKind.Dictionary:
                            kwarg = new ArgType(param);
                            break;
                        case ParameterKind.KeywordOnly:
                            kwonlyargs.Add(new ArgType(param));
                            kw_defaults.Add(param.DefaultValue == null ? null : Convert(param.DefaultValue));
                            break;
                        default:
                            args.Add(new ArgType(param));
                            if (param.DefaultValue != null)
                                defaults.Add(Convert(param.DefaultValue));
                            break;
                    }
                }
            }

            internal Parameter[] Revert() {
                var parameters = new List<Parameter>();
                for (var i = kwonlyargs.Count - 1; i >= 0; i--) {
                    var kwonlyarg = (ArgType)kwonlyargs[i];
                    var param = new Parameter(kwonlyarg.arg, ParameterKind.KeywordOnly) {
                        Annotation = expr.Revert(kwonlyarg.annotation),
                        DefaultValue = expr.Revert(kw_defaults[i])
                    };
                }
                int argIdx = args.Count - 1;
                for (int defIdx = defaults.Count - 1; defIdx >= 0; defIdx--, argIdx--) {
                    var arg = (ArgType)args[argIdx];
                    parameters.Add(new Parameter(arg.arg) {
                        Annotation = expr.Revert(arg.annotation),
                        DefaultValue = expr.Revert(defaults[defIdx])
                    });
                }
                while (argIdx >= 0) {
                    var arg = (ArgType)args[argIdx--];
                    parameters.Add(new Parameter(arg.arg) {
                        Annotation = expr.Revert(arg.annotation)
                    });
                }
                parameters.Reverse();
                if (vararg != null)
                    parameters.Add(new Parameter(vararg.arg, ParameterKind.List));
                if (kwarg != null)
                    parameters.Add(new Parameter(kwarg.arg, ParameterKind.Dictionary));
                return parameters.ToArray();
            }

            public PythonList args { get; set; }

            public ArgType vararg { get; set; }

            public PythonList kwonlyargs { get; set; }

            public PythonList kw_defaults { get; set; }

            public ArgType kwarg { get; set; }

            public PythonList defaults { get; set; }
        }

        [PythonType]
        public abstract class boolop : AST {
        }

        [PythonType]
        public abstract class cmpop : AST {
            internal abstract PythonOperator Revert();
        }

        [PythonType]
        public class comprehension : AST {
            public comprehension() {
                _fields = PythonTuple.MakeTuple(new[] { nameof(target), nameof(iter), nameof(ifs) });
            }

            public comprehension(expr target, expr iter, PythonList ifs)
                : this() {
                this.target = target;
                this.iter = iter;
                this.ifs = ifs;
            }

            internal comprehension(ComprehensionFor listFor, ComprehensionIf[] listIfs)
                : this() {
                target = Convert(listFor.Left, Store.Instance);
                iter = Convert(listFor.List);
                ifs = new PythonList(listIfs.Length);
                foreach (ComprehensionIf listIf in listIfs)
                    ifs.Add(Convert(listIf.Test));
            }

            internal static ComprehensionIterator[] RevertComprehensions(PythonList comprehensions) {
                var comprehensionIterators = new List<ComprehensionIterator>();
                foreach (comprehension comp in comprehensions) {
                    ComprehensionFor cf = new ComprehensionFor(expr.Revert(comp.target), expr.Revert(comp.iter));
                    comprehensionIterators.Add(cf);
                    foreach (expr ifs in comp.ifs) {
                        comprehensionIterators.Add(new ComprehensionIf(expr.Revert(ifs)));
                    }
                }
                return comprehensionIterators.ToArray();
            }

            public expr target { get; set; }

            public expr iter { get; set; }

            public PythonList ifs { get; set; }
        }

        [PythonType]
        public class excepthandler : AST {
            public excepthandler() {
                _attributes = PythonTuple.MakeTuple(new[] { nameof(lineno), nameof(col_offset) });
            }
        }

        [PythonType]
        public abstract class expr : AST {
            protected expr() {
                _attributes = PythonTuple.MakeTuple(new[] { nameof(lineno), nameof(col_offset) });
            }

            internal virtual AstExpression Revert() {
                throw PythonOps.TypeError("Unexpected expr type: {0}", GetType());
            }

            internal static AstExpression Revert(expr ex) {
                return ex?.Revert();
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
        public abstract class expr_context : AST {
        }

        [PythonType]
        public class keyword : AST {
            public keyword() {
                _fields = PythonTuple.MakeTuple(new[] { nameof(arg), nameof(value) });
            }

            public keyword(string arg, expr value)
                : this() {
                this.arg = arg;
                this.value = value;
            }

            internal keyword(IronPython.Compiler.Ast.Arg arg)
                : this() {
                this.arg = arg.Name;
                value = Convert(arg.Expression);
            }

            public string arg { get; set; }

            public expr value { get; set; }
        }

        [PythonType]
        public abstract class mod : AST {
            internal abstract PythonList GetStatements();
        }

        [PythonType]
        public abstract class @operator : AST {
            internal abstract PythonOperator Revert();
        }

        [PythonType]
        public abstract class slice : AST {
        }

        [PythonType]
        public abstract class stmt : AST {
            protected stmt() {
                _attributes = PythonTuple.MakeTuple(new[] { nameof(lineno), nameof(col_offset) });
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
        public abstract class unaryop : AST {
            internal abstract PythonOperator Revert();
        }

        [PythonType]
        public class Add : @operator {
            internal static readonly Add Instance = new Add();
            internal override PythonOperator Revert() => PythonOperator.Add;
        }

        [PythonType]
        public class And : boolop {
            internal static readonly And Instance = new And();
        }

        [PythonType]
        public class Assert : stmt {
            public Assert() {
                _fields = PythonTuple.MakeTuple(new[] { nameof(test), nameof(msg) });
            }

            public Assert(expr test, expr msg, [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                this.test = test;
                this.msg = msg;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal Assert(AssertStatement stmt)
                : this() {
                test = Convert(stmt.Test);
                if (stmt.Message != null)
                    msg = Convert(stmt.Message);
            }

            internal override Statement Revert() {
                return new AssertStatement(expr.Revert(test), expr.Revert(msg));
            }

            public expr test { get; set; }

            public expr msg { get; set; }
        }

        [PythonType]
        public class Assign : stmt {
            public Assign() {
                _fields = PythonTuple.MakeTuple(new[] { nameof(targets), nameof(value) });
            }

            public Assign(PythonList targets, expr value, [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                this.targets = targets;
                this.value = value;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal Assign(AssignmentStatement stmt)
                : this() {
                targets = new PythonList(stmt.Left.Count);
                foreach (AstExpression expr in stmt.Left)
                    targets.Add(Convert(expr, Store.Instance));

                value = Convert(stmt.Right);
            }

            internal override Statement Revert() {
                return new AssignmentStatement(expr.RevertExprs(targets), expr.Revert(value));
            }

            public PythonList targets { get; set; }

            public expr value { get; set; }
        }

        [PythonType]
        public class Attribute : expr {
            public Attribute() {
                _fields = PythonTuple.MakeTuple(new[] { nameof(value), nameof(attr), nameof(ctx) });
            }

            public Attribute(expr value, string attr, expr_context ctx,
                [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                this.value = value;
                this.attr = attr;
                this.ctx = ctx;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal Attribute(MemberExpression attr, expr_context ctx)
                : this() {
                value = Convert(attr.Target);
                this.attr = attr.Name;
                this.ctx = ctx;
            }

            internal override AstExpression Revert() {
                return new MemberExpression(expr.Revert(value), attr);
            }

            public expr value { get; set; }

            public string attr { get; set; }

            public expr_context ctx { get; set; }
        }

        [PythonType]
        public class AugAssign : stmt {
            public AugAssign() {
                _fields = PythonTuple.MakeTuple(new[] { nameof(target), nameof(op), nameof(value) });
            }

            public AugAssign(expr target, @operator op, expr value,
                [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                this.target = target;
                this.op = op;
                this.value = value;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal AugAssign(AugmentedAssignStatement stmt)
                : this() {
                target = Convert(stmt.Left, Store.Instance);
                value = Convert(stmt.Right);
                op = (@operator)Convert(stmt.Operator);
            }

            internal override Statement Revert() {
                return new AugmentedAssignStatement(op.Revert(), expr.Revert(target), expr.Revert(value));
            }

            public expr target { get; set; }

            public @operator op { get; set; }

            public expr value { get; set; }
        }

        /// <summary>
        /// Not used.
        /// </summary>
        [PythonType]
        public class AugLoad : expr_context {
        }

        /// <summary>
        /// Not used.
        /// </summary>
        [PythonType]
        public class AugStore : expr_context {
        }

        [PythonType]
        public class BinOp : expr {
            public BinOp() {
                _fields = PythonTuple.MakeTuple(new[] { nameof(left), nameof(op), nameof(right) });
            }

            public BinOp(expr left, @operator op, expr right, [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                this.left = left;
                this.op = op;
                this.right = right;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal BinOp(BinaryExpression expr, @operator op)
                : this() {
                left = Convert(expr.Left);
                right = Convert(expr.Right);
                this.op = op;
            }

            internal override AstExpression Revert() {
                return new BinaryExpression(op.Revert(), expr.Revert(left), expr.Revert(right));
            }

            public expr left { get; set; }

            public expr right { get; set; }

            public @operator op { get; set; }
        }

        [PythonType]
        public class BitAnd : @operator {
            internal static readonly BitAnd Instance = new BitAnd();
            internal override PythonOperator Revert() => PythonOperator.BitwiseAnd;
        }

        [PythonType]
        public class BitOr : @operator {
            internal static readonly BitOr Instance = new BitOr();
            internal override PythonOperator Revert() => PythonOperator.BitwiseOr;
        }

        [PythonType]
        public class BitXor : @operator {
            internal static readonly BitXor Instance = new BitXor();
            internal override PythonOperator Revert() => PythonOperator.Xor;
        }

        [PythonType]
        public class BoolOp : expr {
            public BoolOp() {
                _fields = PythonTuple.MakeTuple(new[] { nameof(op), nameof(values) });
            }

            public BoolOp(boolop op, PythonList values, [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                this.op = op;
                this.values = values;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal BoolOp(AndExpression and)
                : this() {
                values = PythonList.FromArrayNoCopy(Convert(and.Left), Convert(and.Right));
                op = And.Instance;
            }

            internal BoolOp(OrExpression or)
                : this() {
                values = PythonList.FromArrayNoCopy(Convert(or.Left), Convert(or.Right));
                op = Or.Instance;
            }

            internal override AstExpression Revert() {
                if (op == And.Instance || op is And) {
                    AndExpression ae = new AndExpression(
                        expr.Revert(values[0]),
                        expr.Revert(values[1]));
                    return ae;
                }

                if (op == Or.Instance || op is Or) {
                    OrExpression oe = new OrExpression(
                        expr.Revert(values[0]),
                        expr.Revert(values[1]));
                    return oe;
                }
                throw PythonOps.TypeError("Unexpected boolean operator: {0}", op);
            }

            public boolop op { get; set; }

            public PythonList values { get; set; }
        }

        [PythonType]
        public class Break : stmt {
            internal static readonly Break Instance = new Break();

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
        public class Bytes : expr {
            public Bytes() {
                _fields = PythonTuple.MakeTuple(new[] { nameof(s), });
            }

            internal Bytes(IronPython.Runtime.Bytes s)
                : this(s, null, null) { }

            public Bytes(IronPython.Runtime.Bytes s, [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                this.s = s;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal override AstExpression Revert() {
                return new ConstantExpression(s);
            }

            public IronPython.Runtime.Bytes s { get; set; }
        }

        [PythonType]
        public class Call : expr {
            public Call() {
                _fields = PythonTuple.MakeTuple(new[] { nameof(func), nameof(args), nameof(keywords), nameof(starargs), nameof(kwargs) });
            }

            public Call(expr func, PythonList args, PythonList keywords, expr starargs, expr kwargs,
                [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                this.func = func;
                this.args = args;
                this.keywords = keywords;
                this.starargs = starargs;
                this.kwargs = kwargs;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal Call(CallExpression call)
                : this() {
                args = new PythonList(call.Args.Length);
                keywords = new PythonList();
                func = Convert(call.Target);
                foreach (Arg arg in call.Args) {
                    if (arg.Name == null)
                        args.Add(Convert(arg.Expression));
                    else if (arg.Name == "*")
                        starargs = Convert(arg.Expression);
                    else if (arg.Name == "**")
                        kwargs = Convert(arg.Expression);
                    else
                        keywords.Add(new keyword(arg));
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

            public expr func { get; set; }

            public PythonList args { get; set; }

            public PythonList keywords { get; set; }

            public expr starargs { get; set; } // TODO: remove in 3.5

            public expr kwargs { get; set; } // TODO: remove in 3.5
        }

        [PythonType]
        public class ClassDef : stmt {
            public ClassDef() {
                _fields = PythonTuple.MakeTuple(new[] { nameof(name), nameof(bases), nameof(keywords), nameof(starargs), nameof(kwargs), nameof(body), nameof(decorator_list) });
            }

            public ClassDef(string name, PythonList bases, PythonList keywords, object starargs, object kwargs, PythonList body, PythonList decorator_list,
                [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                this.name = name;
                this.bases = bases;
                this.keywords = keywords;
                this.starargs = starargs;
                this.kwargs = kwargs;
                this.body = body;
                this.decorator_list = decorator_list;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal ClassDef(ClassDefinition def)
                : this() {
                name = def.Name;
                bases = new PythonList(def.Bases.Count);
                foreach (AstExpression expr in def.Bases)
                    bases.Add(Convert(expr));
                body = ConvertStatements(def.Body);
                if (def.Decorators != null) {
                    decorator_list = new PythonList(def.Decorators.Count);
                    foreach (AstExpression expr in def.Decorators)
                        decorator_list.Add(Convert(expr));
                } else {
                    decorator_list = new PythonList(0);
                }
                if (def.Keywords != null) {
                    keywords = new PythonList(def.Keywords.Count);
                    foreach (Arg arg in def.Keywords)
                        keywords.AddNoLock(new keyword(arg));
                } else {
                    keywords = new PythonList(0);
                }
            }

            internal override Statement Revert() {
                var newBases = expr.RevertExprs(bases);
                var newKeywords = keywords.Cast<keyword>().Select(kw => new Arg(kw.arg, expr.Revert(kw.value))).ToArray();
                ClassDefinition cd = new ClassDefinition(name, newBases, newKeywords, RevertStmts(body));
                if (decorator_list.Count != 0)
                    cd.Decorators = expr.RevertExprs(decorator_list);
                return cd;
            }

            public string name { get; set; }

            public PythonList bases { get; set; }

            public PythonList keywords { get; set; }

            public object starargs { get; set; } // TODO: remove in 3.5

            public object kwargs { get; set; } // TODO: remove in 3.5

            public PythonList body { get; set; }

            public PythonList decorator_list { get; set; }
        }

        [PythonType]
        public class Compare : expr {
            public Compare() {
                _fields = PythonTuple.MakeTuple(new[] { nameof(left), nameof(ops), nameof(comparators) });
            }

            public Compare(expr left, PythonList ops, PythonList comparators,
                [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                this.left = left;
                this.ops = ops;
                this.comparators = comparators;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal Compare(BinaryExpression expr)
                : this() {
                left = Convert(expr.Left);
                ops = new PythonList();
                comparators = new PythonList();
                while (BinaryExpression.IsComparison(expr.Right)) {
                    BinaryExpression right = (BinaryExpression)expr.Right;
                    // start accumulating ops and comparators
                    ops.Add(Convert(expr.Operator));
                    comparators.Add(Convert(right.Left));
                    expr = right;
                }
                ops.Add(Convert(expr.Operator));
                comparators.Add(Convert(expr.Right));
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

            public expr left { get; set; }

            public PythonList ops { get; set; }

            public PythonList comparators { get; set; }
        }

        [PythonType]
        public class Continue : stmt {
            internal static readonly Continue Instance = new Continue();

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
        public class Del : expr_context {
            internal static readonly Del Instance = new Del();
        }

        [PythonType]
        public class Delete : stmt {
            public Delete() {
                _fields = PythonTuple.MakeTuple(new[] { nameof(targets), });
            }

            public Delete(PythonList targets, [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                this.targets = targets;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal Delete(DelStatement stmt)
                : this() {
                targets = new PythonList(stmt.Expressions.Count);
                foreach (AstExpression expr in stmt.Expressions)
                    targets.Add(Convert(expr, Del.Instance));
            }

            internal override Statement Revert() {
                return new DelStatement(expr.RevertExprs(targets));
            }

            public PythonList targets { get; set; }
        }

        [PythonType]
        public class Dict : expr {
            public Dict() {
                _fields = PythonTuple.MakeTuple(new[] { nameof(keys), nameof(values) });
            }

            public Dict(PythonList keys, PythonList values, [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                this.keys = keys;
                this.values = values;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal Dict(DictionaryExpression expr)
                : this() {
                keys = new PythonList(expr.Items.Count);
                values = new PythonList(expr.Items.Count);
                foreach (SliceExpression item in expr.Items) {
                    keys.Add(Convert(item.SliceStart));
                    values.Add(Convert(item.SliceStop));
                }
            }

            internal override AstExpression Revert() {
                SliceExpression[] e = new SliceExpression[values.Count];
                for (int i = 0; i < values.Count; i++) {
                    e[i] = new SliceExpression(
                        expr.Revert(keys[i]),
                        expr.Revert(values[i]),
                        null);
                }
                return new DictionaryExpression(e);
            }

            public PythonList keys { get; set; }

            public PythonList values { get; set; }
        }

        [PythonType]
        public class DictComp : expr {
            public DictComp() {
                _fields = PythonTuple.MakeTuple(new[] { nameof(key), nameof(value), nameof(generators) });
            }

            public DictComp(expr key, expr value, PythonList generators,
                [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                this.key = key;
                this.value = value;
                this.generators = generators;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal DictComp(DictionaryComprehension comp)
                : this() {
                key = Convert(comp.Key);
                value = Convert(comp.Value);
                generators = Convert(comp.Iterators);
            }

            internal override AstExpression Revert() {
                return new DictionaryComprehension(expr.Revert(key), expr.Revert(value), comprehension.RevertComprehensions(generators));
            }

            public expr key { get; set; }

            public expr value { get; set; }

            public PythonList generators { get; set; }
        }

        [PythonType]
        public class Div : @operator {
            internal static readonly Div Instance = new Div();
            internal override PythonOperator Revert() => PythonOperator.TrueDivide;
        }

        [PythonType]
        public class Ellipsis : expr {
            internal static readonly Ellipsis Instance = new Ellipsis();
            internal override AstExpression Revert() => new ConstantExpression(PythonOps.Ellipsis);
        }

        [PythonType]
        public class Eq : cmpop {
            internal static readonly Eq Instance = new Eq();
            internal override PythonOperator Revert() => PythonOperator.Equal;
        }

        [PythonType]
        public class ExceptHandler : excepthandler {
            public ExceptHandler() {
                _fields = PythonTuple.MakeTuple(new[] { nameof(type), nameof(name), nameof(body) });
            }

            public ExceptHandler([Optional]expr type, [Optional]expr name, PythonList body,
                [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                this.type = type;
                this.name = name;
                this.body = body;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal ExceptHandler(TryStatementHandler stmt)
                : this() {
                if (stmt.Test != null)
                    type = Convert(stmt.Test);
                if (stmt.Target != null)
                    name = Convert(stmt.Target, Store.Instance);

                body = ConvertStatements(stmt.Body);
            }

            internal TryStatementHandler RevertHandler() {
                return new TryStatementHandler(expr.Revert(type), expr.Revert(name), stmt.RevertStmts(body));
            }

            public expr type { get; set; }

            public expr name { get; set; }

            public PythonList body { get; set; }
        }

        [PythonType]
        public class Expr : stmt {
            public Expr() {
                _fields = PythonTuple.MakeTuple(new[] { nameof(value), });
            }

            public Expr(expr value, [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                this.value = value;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal Expr(ExpressionStatement stmt)
                : this() {
                value = Convert(stmt.Expression);
            }

            internal override Statement Revert() {
                return new ExpressionStatement(expr.Revert(value));
            }

            public expr value { get; set; }
        }

        [PythonType]
        public class Expression : mod {
            public Expression() {
                _fields = PythonTuple.MakeTuple(new[] { nameof(body), });
            }

            public Expression(expr body)
                : this() {
                this.body = body;
            }

            internal Expression(SuiteStatement suite)
                : this() {
                body = Convert(((ExpressionStatement)suite.Statements[0]).Expression);
            }

            internal override PythonList GetStatements() {
                return PythonList.FromArrayNoCopy(body);
            }

            public expr body { get; set; }
        }

        [PythonType]
        public class ExtSlice : slice {
            public ExtSlice() {
                _fields = PythonTuple.MakeTuple(new[] { nameof(dims), });
            }

            public ExtSlice(PythonList dims)
                : this() {
                this.dims = dims;
            }

            internal AstExpression[] Revert() {
                List<AstExpression> ret = new List<AstExpression>(dims.Count);
                foreach (expr ex in dims)
                    ret.Add(expr.Revert(ex));
                return ret.ToArray();
            }

            public PythonList dims { get; set; }
        }

        [PythonType]
        public class FloorDiv : @operator {
            internal static readonly FloorDiv Instance = new FloorDiv();
            internal override PythonOperator Revert() => PythonOperator.FloorDivide;
        }

        [PythonType]
        public class For : stmt {
            public For() {
                _fields = PythonTuple.MakeTuple(new[] { nameof(target), nameof(iter), nameof(body), nameof(orelse) });
            }

            public For(expr target, expr iter, PythonList body, [Optional]PythonList orelse,
               [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                this.target = target;
                this.iter = iter;
                this.body = body;
                if (null == orelse)
                    this.orelse = new PythonList();
                else
                    this.orelse = orelse;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal For(ForStatement stmt)
                : this() {
                target = Convert(stmt.Left, Store.Instance);
                iter = Convert(stmt.List);
                body = ConvertStatements(stmt.Body);
                orelse = ConvertStatements(stmt.Else, true);
            }

            internal override Statement Revert() {
                return new ForStatement(expr.Revert(target), expr.Revert(iter), RevertStmts(body), RevertStmts(orelse));
            }

            public expr target { get; set; }

            public expr iter { get; set; }

            public PythonList body { get; set; }

            public PythonList orelse { get; set; }
        }

        [PythonType]
        public class FunctionDef : stmt {
            public FunctionDef() {
                _fields = PythonTuple.MakeTuple(new[] { nameof(name), nameof(args), nameof(body), nameof(decorator_list), nameof(returns) });
            }

            public FunctionDef(string name, arguments args, PythonList body, PythonList decorator_list, expr returns,
                [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                this.name = name;
                this.args = args;
                this.body = body;
                this.decorator_list = decorator_list;
                this.returns = returns;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal FunctionDef(FunctionDefinition def)
                : this() {
                name = def.Name;
                args = new arguments(def.Parameters);
                body = ConvertStatements(def.Body);

                if (def.Decorators != null) {
                    decorator_list = new PythonList(def.Decorators.Count);
                    foreach (AstExpression expr in def.Decorators)
                        decorator_list.Add(Convert(expr));
                } else {
                    decorator_list = new PythonList(0);
                }

                if (def.ReturnAnnotation != null)
                    returns = Convert(def.ReturnAnnotation);
            }

            internal override Statement Revert() {
                FunctionDefinition fd = new FunctionDefinition(name, args.Revert(), RevertStmts(body));
                fd.IsGenerator = _containsYield;
                _containsYield = false;
                if (decorator_list.Count != 0)
                    fd.Decorators = expr.RevertExprs(decorator_list);
                return fd;
            }

            public string name { get; set; }

            public arguments args { get; set; }

            public PythonList body { get; set; }

            public PythonList decorator_list { get; set; }

            public expr returns { get; set; }
        }

        [PythonType]
        public class GeneratorExp : expr {
            public GeneratorExp() {
                _fields = PythonTuple.MakeTuple(new[] { nameof(elt), nameof(generators) });
            }

            public GeneratorExp(expr elt, PythonList generators, [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                this.elt = elt;
                this.generators = generators;
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
                elt = Convert(walker.Yield.Expression);
                generators = Convert(iters);
            }

            internal class ExtractListComprehensionIterators : PythonWalker {
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

            public expr elt { get; set; }

            public PythonList generators { get; set; }
        }

        [PythonType]
        public class Global : stmt {
            public Global() {
                _fields = PythonTuple.MakeTuple(new[] { nameof(names), });
            }

            public Global(PythonList names, [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                this.names = names;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal Global(GlobalStatement stmt)
                : this() {
                names = new PythonList(stmt.Names);
            }

            internal override Statement Revert() {
                string[] newNames = new string[names.Count];
                for (int i = 0; i < names.Count; i++)
                    newNames[i] = (string)names[i];
                return new GlobalStatement(newNames);
            }

            public PythonList names { get; set; }
        }

        [PythonType]
        public class Gt : cmpop {
            internal static readonly Gt Instance = new Gt();
            internal override PythonOperator Revert() => PythonOperator.GreaterThan;
        }

        [PythonType]
        public class GtE : cmpop {
            internal static readonly GtE Instance = new GtE();
            internal override PythonOperator Revert() => PythonOperator.GreaterThanOrEqual;
        }

        [PythonType]
        public class If : stmt {
            public If() {
                _fields = PythonTuple.MakeTuple(new[] { nameof(test), nameof(body), nameof(orelse) });
            }

            public If(expr test, PythonList body, [Optional]PythonList orelse,
                [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                this.test = test;
                this.body = body;
                if (null == orelse)
                    this.orelse = new PythonList();
                else
                    this.orelse = orelse;
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
                        parent.orelse = PythonList.FromArrayNoCopy(current);
                    }

                    current.Initialize(ifTest);
                    parent = current;
                }

                current.orelse = ConvertStatements(stmt.ElseStatement, true);
            }

            internal void Initialize(IfStatementTest ifTest) {
                test = Convert(ifTest.Test);
                body = ConvertStatements(ifTest.Body);
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

            public expr test { get; set; }

            public PythonList body { get; set; }

            public PythonList orelse { get; set; }
        }

        [PythonType]
        public class IfExp : expr {
            public IfExp() {
                _fields = PythonTuple.MakeTuple(new[] { nameof(test), nameof(body), nameof(orelse) });
            }

            public IfExp(expr test, expr body, expr orelse, [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                this.test = test;
                this.body = body;
                this.orelse = orelse;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal IfExp(ConditionalExpression cond)
                : this() {
                test = Convert(cond.Test);
                body = Convert(cond.TrueExpression);
                orelse = Convert(cond.FalseExpression);
            }

            internal override AstExpression Revert() {
                return new ConditionalExpression(expr.Revert(test), expr.Revert(body), expr.Revert(orelse));
            }

            public expr test { get; set; }

            public expr body { get; set; }

            public expr orelse { get; set; }
        }

        private static char[] MODULE_NAME_SPLITTER = new char[1] { '.' };


        [PythonType]
        public class Import : stmt {
            public Import() {
                _fields = PythonTuple.MakeTuple(new[] { nameof(names), });
            }

            public Import(PythonList names, [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                this.names = names;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal Import(ImportStatement stmt)
                : this() {
                names = ConvertAliases(stmt.Names, stmt.AsNames);
            }

            internal override Statement Revert() {
                ModuleName[] moduleNames = new ModuleName[names.Count];
                String[] asNames = new String[names.Count];
                for (int i = 0; i < names.Count; i++) {
                    alias alias = (alias)names[i];
                    moduleNames[i] = new ModuleName(alias.name.Split(MODULE_NAME_SPLITTER));
                    asNames[i] = alias.asname;
                }
                return new ImportStatement(moduleNames, asNames);
            }

            public PythonList names { get; set; }
        }

        [PythonType]
        public class ImportFrom : stmt {
            public ImportFrom() {
                _fields = PythonTuple.MakeTuple(new[] { nameof(module), nameof(names), nameof(level) });
            }

            public ImportFrom([Optional]string module, PythonList names, [Optional]int level,
                [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                this.module = module;
                this.names = names;
                this.level = level;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            public ImportFrom(FromImportStatement stmt)
                : this() {
                module = stmt.Root.MakeString();
                module = string.IsNullOrEmpty(module) ? null : module;
                names = ConvertAliases(stmt.Names, stmt.AsNames);
                if (stmt.Root is RelativeModuleName)
                    level = ((RelativeModuleName)stmt.Root).DotCount;
            }

            internal override Statement Revert() {
                ModuleName root = null;
                if (module != null)
                    if (module[0] == '.') // relative module
                        root = new RelativeModuleName(module.Split(MODULE_NAME_SPLITTER), level);
                    else {
                        root = new ModuleName(module.Split(MODULE_NAME_SPLITTER));
                    }

                if (names.Count == 1 && ((alias)names[0]).name == "*")
                    return new FromImportStatement(root, (string[])FromImportStatement.Star, null, false);

                String[] newNames = new String[names.Count];
                String[] asNames = new String[names.Count];
                for (int i = 0; i < names.Count; i++) {
                    alias alias = (alias)names[i];
                    newNames[i] = alias.name;
                    asNames[i] = alias.asname;
                }
                return new FromImportStatement(root, newNames, asNames, false);
            }

            public string module { get; set; }

            public PythonList names { get; set; }

            public int level { get; set; }
        }

        [PythonType]
        public class In : cmpop {
            internal static readonly In Instance = new In();
            internal override PythonOperator Revert() => PythonOperator.In;
        }

        [PythonType]
        public class Index : slice {
            public Index() {
                _fields = PythonTuple.MakeTuple(new[] { nameof(value), });
            }

            public Index(expr value)
                : this() {
                this.value = value;
            }

            public expr value { get; set; }
        }

        [PythonType]
        public class Interactive : mod {
            public Interactive() {
                _fields = PythonTuple.MakeTuple(new[] { nameof(body), });
            }

            public Interactive(PythonList body)
                : this() {
                this.body = body;
            }

            internal Interactive(SuiteStatement suite)
                : this() {
                body = ConvertStatements(suite);
            }

            internal override PythonList GetStatements() {
                return body;
            }

            public PythonList body { get; set; }
        }

        [PythonType]
        public class Invert : unaryop {
            internal static readonly Invert Instance = new Invert();
            internal override PythonOperator Revert() => PythonOperator.Invert;
        }

        [PythonType]
        public class Is : cmpop {
            internal static readonly Is Instance = new Is();
            internal override PythonOperator Revert() => PythonOperator.Is;
        }

        [PythonType]
        public class IsNot : cmpop {
            internal static readonly IsNot Instance = new IsNot();
            internal override PythonOperator Revert() => PythonOperator.IsNot;
        }

        [PythonType]
        public class Lambda : expr {
            public Lambda() {
                _fields = PythonTuple.MakeTuple(new[] { nameof(args), nameof(body) });
            }

            public Lambda(arguments args, expr body, [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                this.args = args;
                this.body = body;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal Lambda(LambdaExpression lambda)
                : this() {
                FunctionDef def = (FunctionDef)Convert(lambda.Function);
                args = def.args;
                Debug.Assert(def.body.Count == 1, "LambdaExpression body should be one statement.");
                stmt statement = (stmt)def.body[0];
                if (statement is Return)
                    body = ((Return)statement).value;
                else if (statement is Expr) {
                    // What should be sufficient is:
                    // _body = ((Expr)statement).value;
                    // but, AST comes with trees containing twice YieldExpression.
                    // For: 
                    //   lambda x: (yield x)
                    // it comes back with:
                    //
                    //IronPython.Compiler.Ast.LambdaExpression
                    //IronPython.Compiler.Ast.FunctionDefinition<lambda$334>generator
                    //  IronPython.Compiler.Ast.Parameter  x
                    //  IronPython.Compiler.Ast.ExpressionStatement
                    //    IronPython.Compiler.Ast.YieldExpression     <<<<<<<<
                    //      IronPython.Compiler.Ast.YieldExpression   <<<<<<<< why twice?
                    //        IronPython.Compiler.Ast.NameExpression x

                    body = ((Yield)((Expr)statement).value).value;
                } else
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

            public arguments args { get; set; }

            public expr body { get; set; }
        }

        [PythonType]
        public class List : expr {
            public List() {
                _fields = PythonTuple.MakeTuple(new[] { nameof(elts), nameof(ctx) });
            }

            public List(PythonList elts, expr_context ctx, [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                this.elts = elts;
                this.ctx = ctx;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal List(ListExpression list, expr_context ctx)
                : this() {
                elts = new PythonList(list.Items.Count);
                foreach (AstExpression expr in list.Items)
                    elts.Add(Convert(expr, ctx));

                this.ctx = ctx;
            }

            internal override AstExpression Revert() {
                AstExpression[] e = new AstExpression[elts.Count];
                int i = 0;
                foreach (expr el in elts)
                    e[i++] = expr.Revert(el);
                return new ListExpression(e);
            }

            public PythonList elts { get; set; }

            public expr_context ctx { get; set; }
        }

        [PythonType]
        public class ListComp : expr {
            public ListComp() {
                _fields = PythonTuple.MakeTuple(new[] { nameof(elt), nameof(generators) });
            }

            public ListComp(expr elt, PythonList generators, [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                this.elt = elt;
                this.generators = generators;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal ListComp(ListComprehension comp)
                : this() {
                elt = Convert(comp.Item);
                generators = Convert(comp.Iterators);
            }

            internal override AstExpression Revert() {
                AstExpression item = expr.Revert(elt);
                ComprehensionIterator[] iters = comprehension.RevertComprehensions(generators);
                return new ListComprehension(item, iters);
            }

            public expr elt { get; set; }

            public PythonList generators { get; set; }
        }

        [PythonType]
        public class Load : expr_context {
            internal static readonly Load Instance = new Load();
        }

        [PythonType]
        public class Lt : cmpop {
            internal static readonly Lt Instance = new Lt();
            internal override PythonOperator Revert() => PythonOperator.LessThan;
        }

        [PythonType]
        public class LtE : cmpop {
            internal static readonly LtE Instance = new LtE();
            internal override PythonOperator Revert() => PythonOperator.LessThanOrEqual;
        }

        [PythonType]
        public class LShift : @operator {
            internal static readonly LShift Instance = new LShift();
            internal override PythonOperator Revert() => PythonOperator.LeftShift;
        }

        [PythonType]
        public class MatMult : @operator {
            internal static readonly MatMult Instance = new MatMult();
            internal override PythonOperator Revert() => PythonOperator.MatMult;
        }

        [PythonType]
        public class Mod : @operator {
            internal static readonly Mod Instance = new Mod();
            internal override PythonOperator Revert() => PythonOperator.Mod;
        }

        [PythonType]
        public class Module : mod {
            public Module() {
                _fields = PythonTuple.MakeTuple(new[] { nameof(body), });
            }

            public Module(PythonList body)
                : this() {
                this.body = body;
            }

            internal Module(SuiteStatement suite)
                : this() {
                body = ConvertStatements(suite);
            }

            public PythonList body { get; set; }

            internal override PythonList GetStatements() {
                return body;
            }
        }

        [PythonType]
        public class Mult : @operator {
            internal static readonly Mult Instance = new Mult();
            internal override PythonOperator Revert() => PythonOperator.Multiply;
        }

        [PythonType]
        public class Name : expr {
            public Name() {
                _fields = PythonTuple.MakeTuple(new[] { nameof(id), nameof(ctx) });
            }

            public Name(string id, expr_context ctx, [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                this.id = id;
                this.ctx = ctx;
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

            public expr_context ctx { get; set; }

            public string id { get; set; }
        }

        [PythonType]
        public class NameConstant : expr {
            public NameConstant() {
                _fields = PythonTuple.MakeTuple(new[] { nameof(value) });
            }

            public NameConstant(object value, [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                this.value = value;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            public NameConstant(object value)
                : this(value, null, null) { }

            internal override AstExpression Revert() {
                return new ConstantExpression(value);
            }

            public object value { get; set; }
        }

        [PythonType]
        public class Nonlocal : stmt {
            public Nonlocal() {
                _fields = PythonTuple.MakeTuple(new[] { nameof(names), });
            }

            public Nonlocal(PythonList names, [Optional] int? lineno, [Optional] int? col_offset)
                : this() {
                this.names = names;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal Nonlocal(NonlocalStatement stmt)
                : this() {
                names = new PythonList(stmt.Names);
            }

            internal override Statement Revert() {
                string[] newNames = new string[names.Count];
                for (int i = 0; i < names.Count; i++)
                    newNames[i] = (string)names[i];
                return new NonlocalStatement(newNames);
            }

            public PythonList names { get; set; }
        }

        [PythonType]
        public class Not : unaryop {
            internal static readonly Not Instance = new Not();
            internal override PythonOperator Revert() => PythonOperator.Not;
        }

        [PythonType]
        public class NotEq : cmpop {
            internal static readonly NotEq Instance = new NotEq();
            internal override PythonOperator Revert() => PythonOperator.NotEqual;
        }

        [PythonType]
        public class NotIn : cmpop {
            internal static readonly NotIn Instance = new NotIn();
            internal override PythonOperator Revert() => PythonOperator.NotIn;
        }

        [PythonType]
        public class Num : expr {
            public Num() {
                _fields = PythonTuple.MakeTuple(new[] { nameof(n), });
            }

            internal Num(object n)
                : this(n, null, null) { }

            public Num(object n, [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                this.n = n;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal override AstExpression Revert() {
                return new ConstantExpression(n);
            }

            public object n { get; set; }
        }

        [PythonType]
        public class Or : boolop {
            internal static readonly Or Instance = new Or();
        }

        [PythonType]
        public class Param : expr_context {
            internal static readonly Param Instance = new Param();
        }

        [PythonType]
        public class Pass : stmt {
            internal static readonly Pass Instance = new Pass();

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
        public class Pow : @operator {
            internal static readonly Pow Instance = new Pow();
            internal override PythonOperator Revert() => PythonOperator.Power;
        }

        [PythonType]
        public class Raise : stmt {
            public Raise() {
                _fields = PythonTuple.MakeTuple(new[] { nameof(exc), nameof(cause) });
            }

            public Raise(expr exc, expr cause, [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                this.exc = exc;
                this.cause = cause;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal Raise(RaiseStatement stmt)
                : this() {
                if (stmt.Exception != null)
                    exc = Convert(stmt.Exception);
                if (stmt.Cause != null)
                    cause = Convert(stmt.Cause);
            }

            internal override Statement Revert() {
                return new RaiseStatement(expr.Revert(exc), expr.Revert(cause));
            }

            public expr exc { get; set; }

            public expr cause { get; set; }
        }


        [PythonType]
        public class Return : stmt {
            public Return() {
                _fields = PythonTuple.MakeTuple(new[] { nameof(value), });
            }

            public Return([Optional]expr value, [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                this.value = value;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            public Return(ReturnStatement statement)
                : this() {
                // statement.Expression is never null
                //or is it?
                if (statement.Expression == null)
                    value = null;
                else
                    value = Convert(statement.Expression);
            }

            internal override Statement Revert() {
                return new ReturnStatement(expr.Revert(value));
            }

            public expr value { get; set; }
        }

        [PythonType]
        public class RShift : @operator {
            internal static readonly RShift Instance = new RShift();
            internal override PythonOperator Revert() => PythonOperator.RightShift;
        }

        [PythonType]
        public class Set : expr {
            public Set() {
                _fields = PythonTuple.MakeTuple(new[] { nameof(elts) });
            }

            public Set(PythonList elts, [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                this.elts = elts;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal Set(SetExpression setExpression)
                : this() {
                elts = new PythonList(setExpression.Items.Count);
                foreach (AstExpression item in setExpression.Items) {
                    elts.Add(Convert(item));
                }
            }

            internal override AstExpression Revert() {
                AstExpression[] e = new AstExpression[elts.Count];
                int i = 0;
                foreach (expr el in elts)
                    e[i++] = expr.Revert(el);
                return new SetExpression(e);
            }

            public PythonList elts { get; set; }
        }

        [PythonType]
        public class SetComp : expr {
            public SetComp() {
                _fields = PythonTuple.MakeTuple(new[] { nameof(elt), nameof(generators) });
            }

            public SetComp(expr elt, PythonList generators, [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                this.elt = elt;
                this.generators = generators;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal SetComp(SetComprehension comp)
                : this() {
                elt = Convert(comp.Item);
                generators = Convert(comp.Iterators);
            }

            internal override AstExpression Revert() {
                AstExpression item = expr.Revert(elt);
                ComprehensionIterator[] iters = comprehension.RevertComprehensions(generators);
                return new SetComprehension(item, iters);
            }

            public expr elt { get; set; }

            public PythonList generators { get; set; }
        }


        [PythonType]
        public class Slice : slice {
            public Slice() {
                _fields = PythonTuple.MakeTuple(new[] { nameof(lower), nameof(upper), nameof(step) });
            }

            public Slice(expr lower, expr upper, expr step)
                // default interpretation of missing step is [:]
                // in order to get [::], please provide explicit Name('None',Load.Instance)
                : this() {
                this.lower = lower;
                this.upper = upper;
                this.step = step;
            }

            internal Slice(SliceExpression expr)
                : this() {
                if (expr.SliceStart != null)
                    lower = Convert(expr.SliceStart);
                if (expr.SliceStop != null)
                    upper = Convert(expr.SliceStop);
                if (expr.SliceStep != null)
                    step = Convert(expr.SliceStep);
            }

            public expr lower { get; set; }

            public expr upper { get; set; }

            public expr step { get; set; }
        }

        [PythonType]
        public class Starred : expr {
            public Starred() {
                _fields = PythonTuple.MakeTuple(new[] { nameof(value), nameof(ctx) });
            }

            public Starred(expr value, expr_context ctx, [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                this.value = value;
                this.ctx = ctx;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            public Starred(expr value, expr_context ctx)
                : this(value, ctx, null, null) { }

            internal Starred(StarredExpression expr, expr_context ctx)
                : this(Convert(expr.Value), ctx, null, null) { }

            public expr_context ctx { get; set; }

            public expr value { get; set; }
        }

        [PythonType]
        public class Store : expr_context {
            internal static readonly Store Instance = new Store();
        }

        [PythonType]
        public class Str : expr {
            public Str() {
                _fields = PythonTuple.MakeTuple(new[] { nameof(s), });
            }

            internal Str(string s)
                : this(s, null, null) { }

            public Str(string s, [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                this.s = s;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal override AstExpression Revert() {
                return new ConstantExpression(s);
            }

            public string s { get; set; }
        }

        [PythonType]
        public class Sub : @operator {
            internal static readonly Sub Instance = new Sub();
            internal override PythonOperator Revert() => PythonOperator.Subtract;
        }

        [PythonType]
        public class Subscript : expr {
            public Subscript() {
                _fields = PythonTuple.MakeTuple(new[] { nameof(value), nameof(slice), nameof(ctx) });
            }

            public Subscript(expr value, slice slice, expr_context ctx,
                [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                this.value = value;
                this.slice = slice;
                this.ctx = ctx;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal Subscript(IndexExpression expr, expr_context ctx)
                : this() {
                value = Convert(expr.Target);
                this.ctx = ctx;
                slice = TrySliceConvert(expr.Index);
                if (slice == null)
                    slice = new Index(Convert(expr.Index));
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
                    if (concreteSlice.step != null)
                        step = expr.Revert(concreteSlice.step);
                    index = new SliceExpression(start, stop, step);
                } else if (slice is ExtSlice) {
                    index = new TupleExpression(true, ((ExtSlice)slice).Revert());
                } else {
                    Debug.Assert(false, "Unexpected type when converting Subscript: " + slice.GetType());
                }
                return new IndexExpression(expr.Revert(value), index);
            }

            public expr value { get; set; }

            public slice slice { get; set; }

            public expr_context ctx { get; set; }
        }

        /// <summary>
        /// Not an actual node. We don't create this, but it's here for compatibility.
        /// </summary>
        [PythonType]
        public class Suite : mod {
            public Suite() {
                _fields = PythonTuple.MakeTuple(new[] { nameof(body), });
            }

            public Suite(PythonList body)
                : this() {
                this.body = body;
            }

            public PythonList body { get; set; }

            internal override PythonList GetStatements() {
                return body;
            }
        }

        [PythonType]
        public class Try : stmt {
            public Try() {
                _fields = PythonTuple.MakeTuple(new[] { nameof(body), nameof(handlers), nameof(orelse), nameof(finalbody) });
            }

            public Try(PythonList body, PythonList handlers, PythonList orelse, PythonList finalbody,
                [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                this.body = body;
                this.handlers = handlers;
                this.orelse = orelse;
                this.finalbody = finalbody;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal Try(TryStatement stmt)
                : this() {
                body = ConvertStatements(stmt.Body);

                handlers = new PythonList(stmt.Handlers.Count);
                foreach (TryStatementHandler tryStmt in stmt.Handlers)
                    handlers.Add(Convert(tryStmt));

                orelse = ConvertStatements(stmt.Else, true);
                finalbody = ConvertStatements(stmt.Finally, true);
            }

            internal override Statement Revert() {
                TryStatementHandler[] tshs = new TryStatementHandler[handlers.Count];
                for (int i = 0; i < handlers.Count; i++) {
                    tshs[i] = ((ExceptHandler)handlers[i]).RevertHandler();
                }
                return new TryStatement(RevertStmts(body), tshs, orelse.Count == 0 ? null : RevertStmts(orelse), RevertStmts(finalbody));
            }

            public PythonList body { get; set; }

            public PythonList handlers { get; set; }

            public PythonList orelse { get; set; }

            public PythonList finalbody { get; set; }
        }

        [PythonType]
        public class Tuple : expr {
            public Tuple() {
                _fields = PythonTuple.MakeTuple(new[] { nameof(elts), nameof(ctx) });
            }

            public Tuple(PythonList elts, expr_context ctx, [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                this.elts = elts;
                this.ctx = ctx;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal Tuple(TupleExpression list, expr_context ctx)
                : this() {
                elts = new PythonList(list.Items.Count);
                foreach (AstExpression expr in list.Items)
                    elts.Add(Convert(expr, ctx));

                this.ctx = ctx;
            }

            internal override AstExpression Revert() {
                AstExpression[] e = new AstExpression[elts.Count];
                int i = 0;
                foreach (expr el in elts)
                    e[i++] = expr.Revert(el);
                return new TupleExpression(false, e);
            }

            public PythonList elts { get; set; }

            public expr_context ctx { get; set; }
        }

        [PythonType]
        public class UnaryOp : expr {
            public UnaryOp() {
                _fields = PythonTuple.MakeTuple(new[] { nameof(op), nameof(operand) });
            }

            internal UnaryOp(UnaryExpression expression)
                : this() {
                op = (unaryop)Convert(expression.Operator);
                operand = Convert(expression.Expression);
            }

            public UnaryOp(unaryop op, expr operand, [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                this.op = op;
                this.operand = operand;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal override AstExpression Revert() {
                return new UnaryExpression(op.Revert(), expr.Revert(operand));
            }

            public unaryop op { get; set; }

            public expr operand { get; set; }

            internal expr TryTrimTrivialUnaryOp() {
                // in case of +constant or -constant returns underlying Num
                // representation, otherwise unmodified itself
                if (!(operand is Num num)) {
                    return this;
                }
                if (op is UAdd) {
                    return num;
                }
                if (!(op is USub)) {
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
        public class UAdd : unaryop {
            internal static readonly UAdd Instance = new UAdd();
            internal override PythonOperator Revert() => PythonOperator.Pos;
        }

        [PythonType]
        public class USub : unaryop {
            internal static readonly USub Instance = new USub();
            internal override PythonOperator Revert() => PythonOperator.Negate;
        }

        [PythonType]
        public class While : stmt {
            public While() {
                _fields = PythonTuple.MakeTuple(new[] { nameof(test), nameof(body), nameof(orelse) });
            }

            public While(expr test, PythonList body, [Optional]PythonList orelse,
                [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                this.test = test;
                this.body = body;
                if (null == orelse)
                    this.orelse = new PythonList();
                else
                    this.orelse = orelse;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal While(WhileStatement stmt)
                : this() {
                test = Convert(stmt.Test);
                body = ConvertStatements(stmt.Body);
                orelse = ConvertStatements(stmt.ElseStatement, true);
            }

            internal override Statement Revert() {
                return new WhileStatement(expr.Revert(test), RevertStmts(body), RevertStmts(orelse));
            }

            public expr test { get; set; }

            public PythonList body { get; set; }

            public PythonList orelse { get; set; }
        }

        [PythonType]
        public class With : stmt {
            public With() {
                _fields = PythonTuple.MakeTuple(new[] { nameof(items), nameof(body) });
            }

            public With(PythonList items, PythonList body,
                [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                this.items = items;
                this.body = body;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal With(WithStatement with)
                : this() {
                items = new PythonList(1);
                items.AddNoLock(new withitem(Convert(with.ContextManager), with.Variable == null ? null : Convert(with.Variable, Store.Instance)));
                body = ConvertStatements(with.Body);
            }

            internal override Statement Revert() {
                Statement statement = RevertStmts(this.body);
                foreach (withitem item in items) {
                    statement = new WithStatement(expr.Revert(item.context_expr), expr.Revert(item.optional_vars), statement);
                }
                return statement;
            }

            public PythonList items { get; set; }

            public PythonList body { get; set; }
        }

        [PythonType]
        public class withitem : AST {
            public withitem() {
                _fields = PythonTuple.MakeTuple(new[] { nameof(context_expr), nameof(optional_vars) });
            }

            public withitem(expr context_expr, expr optional_vars,
                [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                this.context_expr = context_expr;
                this.optional_vars = optional_vars;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            public expr context_expr { get; set; }

            public expr optional_vars { get; set; }
        }

        // if yield is detected, the containing function has to be marked as generator
        internal static bool _containsYield = false;

        [PythonType]
        public class Yield : expr {
            public Yield() {
                _fields = PythonTuple.MakeTuple(new[] { nameof(value), });
            }

            public Yield([Optional]expr value, [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                this.value = value;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal Yield(YieldExpression expr)
                : this() {
                value = expr.Expression == null ? null : Convert(expr.Expression);
            }

            internal override AstExpression Revert() {
                _containsYield = true;
                return new YieldExpression(value == null ? null : expr.Revert(value));
            }

            public expr value { get; set; }
        }

        [PythonType]
        public class YieldFrom : expr {
            public YieldFrom() {
                _fields = PythonTuple.MakeTuple(new[] { nameof(value), });
            }

            public YieldFrom([Optional]expr value, [Optional]int? lineno, [Optional]int? col_offset)
                : this() {
                this.value = value;
                _lineno = lineno;
                _col_offset = col_offset;
            }

            internal YieldFrom(YieldFromExpression expr)
                : this() {
                // expr.Expression is never null
                value = Convert(expr.Expression);
            }

            internal override AstExpression Revert() {
                _containsYield = true;
                return new YieldFromExpression(expr.Revert(value));
            }

            public expr value { get; set; }
        }
    }
}
