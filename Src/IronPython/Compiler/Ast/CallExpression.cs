// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

using IronPython.Runtime;

using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Interpreter;
using Microsoft.Scripting.Utils;

using AstUtils = Microsoft.Scripting.Ast.Utils;
using MSAst = System.Linq.Expressions;

namespace IronPython.Compiler.Ast {

    public class CallExpression : Expression, IInstructionProvider {
        public CallExpression(Expression target, Arg[] args) {
            // TODO: use two arrays (args/keywords) instead
            if (args == null) throw new ArgumentNullException(nameof(args));
            Target = target;
            Args = args;
        }

        public Expression Target { get; }

        public Arg[] Args { get; }

        internal IList<Arg> ImplicitArgs { get; } = new List<Arg>();

        public bool NeedsLocalsDictionary() {
            if (!(Target is NameExpression nameExpr)) return false;

            if (nameExpr.Name == "eval" || nameExpr.Name == "exec") return true;
            if (nameExpr.Name == "dir" || nameExpr.Name == "vars" || nameExpr.Name == "locals") {
                // could be splatting empty list or dict resulting in 0-param call which needs context
                return Args.All(arg => arg.Name == "*" || arg.Name == "**");
            }

            return false;
        }

        public override MSAst.Expression Reduce() {
            Arg[] args = Args;
            if (Args.Length == 0 && ImplicitArgs.Count > 0) {
                args = ImplicitArgs.ToArray();
            }

            SplitArgs(args, out var simpleArgs, out var listArgs, out var namedArgs, out var dictArgs, out var numDict);

            Argument[] kinds = new Argument[simpleArgs.Length + Math.Min(listArgs.Length, 1) + namedArgs.Length + (dictArgs.Length - numDict) + Math.Min(numDict, 1)];
            MSAst.Expression[] values = new MSAst.Expression[2 + kinds.Length];

            values[0] = Parent.LocalContext;
            values[1] = Target;

            int i = 0;

            // add simple arguments
            foreach (var arg in simpleArgs) {
                kinds[i] = arg.GetArgumentInfo();
                values[i + 2] = arg.Expression;
                i++;
            }

            // unpack list arguments
            if (listArgs.Length > 0) {
                var arg = listArgs[0];
                Debug.Assert(arg.GetArgumentInfo().Kind == ArgumentType.List);
                kinds[i] = arg.GetArgumentInfo();
                values[i + 2] = UnpackListHelper(listArgs);
                i++;
            }

            // add named arguments
            foreach (var arg in namedArgs) {
                kinds[i] = arg.GetArgumentInfo();
                values[i + 2] = arg.Expression;
                i++;
            }

            // add named arguments specified after a dict unpack
            if (dictArgs.Length != numDict) {
                foreach (var arg in dictArgs) {
                    var info = arg.GetArgumentInfo();
                    if (info.Kind == ArgumentType.Named) {
                        kinds[i] = info;
                        values[i + 2] = arg.Expression;
                        i++;
                    }
                }
            }

            // unpack dict arguments
            if (dictArgs.Length > 0) {
                var arg = dictArgs[0];
                Debug.Assert(arg.GetArgumentInfo().Kind == ArgumentType.Dictionary);
                kinds[i] = arg.GetArgumentInfo();
                values[i + 2] = UnpackDictHelper(Parent.LocalContext, dictArgs);
            }

            return Parent.Invoke(
                new CallSignature(kinds),
                values
            );

            static void SplitArgs(Arg[] args, out ReadOnlySpan<Arg> simpleArgs, out ReadOnlySpan<Arg> listArgs, out ReadOnlySpan<Arg> namedArgs, out ReadOnlySpan<Arg> dictArgs, out int numDict) {
                if (args.Length == 0) {
                    simpleArgs = default;
                    listArgs = default;
                    namedArgs = default;
                    dictArgs = default;
                    numDict = 0;
                    return;
                }

                int idxSimple = args.Length;
                int idxList = args.Length;
                int idxNamed = args.Length;
                int idxDict = args.Length;
                numDict = 0;

                // we want idxSimple <= idxList <= idxNamed <= idxDict
                for (var i = args.Length - 1; i >= 0; i--) {
                    var arg = args[i];
                    var info = arg.GetArgumentInfo();
                    switch (info.Kind) {
                        case ArgumentType.Simple:
                            idxSimple = i;
                            break;
                        case ArgumentType.List:
                            idxList = i;
                            break;
                        case ArgumentType.Named:
                            idxNamed = i;
                            break;
                        case ArgumentType.Dictionary:
                            idxDict = i;
                            numDict++;
                            break;
                        default:
                            throw new InvalidOperationException();
                    }
                }
                dictArgs = args.AsSpan(idxDict);
                if (idxNamed > idxDict) idxNamed = idxDict;
                namedArgs = args.AsSpan(idxNamed, idxDict - idxNamed);
                if (idxList > idxNamed) idxList = idxNamed;
                listArgs = args.AsSpan(idxList, idxNamed - idxList);
                if (idxSimple > idxList) idxSimple = idxList;
                simpleArgs = args.AsSpan(idxSimple, idxList - idxSimple);
            }

            static MSAst.Expression UnpackListHelper(ReadOnlySpan<Arg> args) {
                Debug.Assert(args.Length > 0);
                Debug.Assert(args[0].GetArgumentInfo().Kind == ArgumentType.List);
                if (args.Length == 1) return args[0].Expression;

                var expressions = new ReadOnlyCollectionBuilder<MSAst.Expression>(args.Length + 2);
                var varExpr = Expression.Variable(typeof(PythonList), "$coll");
                expressions.Add(Expression.Assign(varExpr, Expression.Call(AstMethods.MakeEmptyList)));
                foreach (var arg in args) {
                    if (arg.GetArgumentInfo().Kind == ArgumentType.List) {
                        expressions.Add(Expression.Call(AstMethods.ListExtend, varExpr, AstUtils.Convert(arg.Expression, typeof(object))));
                    } else {
                        expressions.Add(Expression.Call(AstMethods.ListAppend, varExpr, AstUtils.Convert(arg.Expression, typeof(object))));
                    }
                }
                expressions.Add(varExpr);
                return Expression.Block(typeof(PythonList), new MSAst.ParameterExpression[] { varExpr }, expressions);
            }

            static MSAst.Expression UnpackDictHelper(MSAst.Expression context, ReadOnlySpan<Arg> args) {
                Debug.Assert(args.Length > 0);
                Debug.Assert(args[0].GetArgumentInfo().Kind == ArgumentType.Dictionary);
                if (args.Length == 1) return args[0].Expression;

                var expressions = new List<MSAst.Expression>(args.Length + 2);
                var varExpr = Expression.Variable(typeof(PythonDictionary), "$dict");
                expressions.Add(Expression.Assign(varExpr, Expression.Call(AstMethods.MakeEmptyDict)));
                foreach (var arg in args) {
                    if (arg.GetArgumentInfo().Kind == ArgumentType.Dictionary) {
                        expressions.Add(Expression.Call(AstMethods.DictMerge, context, varExpr, arg.Expression));
                    }
                }
                expressions.Add(varExpr);
                return Expression.Block(typeof(PythonDictionary), new MSAst.ParameterExpression[] { varExpr }, expressions);
            }
        }

        #region IInstructionProvider Members

        void IInstructionProvider.AddInstructions(LightCompiler compiler) {
            Arg[] args = Args;
            if (args.Length == 0 && ImplicitArgs.Count > 0) {
                args = ImplicitArgs.ToArray();
            }

            for (int i = 0; i < args.Length; i++) {
                if (!args[i].GetArgumentInfo().IsSimple) {
                    compiler.Compile(Reduce());
                    return;
                }
            }

            switch (args.Length) {
                #region Generated Python Call Expression Instruction Switch

                // *** BEGIN GENERATED CODE ***
                // generated by function: gen_call_expression_instruction_switch from: generate_calls.py

                case 0:
                    compiler.Compile(Parent.LocalContext);
                    compiler.Compile(Target);
                    compiler.Instructions.Emit(new Invoke0Instruction(Parent.PyContext));
                    return;
                case 1:
                    compiler.Compile(Parent.LocalContext);
                    compiler.Compile(Target);
                    compiler.Compile(args[0].Expression);
                    compiler.Instructions.Emit(new Invoke1Instruction(Parent.PyContext));
                    return;
                case 2:
                    compiler.Compile(Parent.LocalContext);
                    compiler.Compile(Target);
                    compiler.Compile(args[0].Expression);
                    compiler.Compile(args[1].Expression);
                    compiler.Instructions.Emit(new Invoke2Instruction(Parent.PyContext));
                    return;
                case 3:
                    compiler.Compile(Parent.LocalContext);
                    compiler.Compile(Target);
                    compiler.Compile(args[0].Expression);
                    compiler.Compile(args[1].Expression);
                    compiler.Compile(args[2].Expression);
                    compiler.Instructions.Emit(new Invoke3Instruction(Parent.PyContext));
                    return;
                case 4:
                    compiler.Compile(Parent.LocalContext);
                    compiler.Compile(Target);
                    compiler.Compile(args[0].Expression);
                    compiler.Compile(args[1].Expression);
                    compiler.Compile(args[2].Expression);
                    compiler.Compile(args[3].Expression);
                    compiler.Instructions.Emit(new Invoke4Instruction(Parent.PyContext));
                    return;
                case 5:
                    compiler.Compile(Parent.LocalContext);
                    compiler.Compile(Target);
                    compiler.Compile(args[0].Expression);
                    compiler.Compile(args[1].Expression);
                    compiler.Compile(args[2].Expression);
                    compiler.Compile(args[3].Expression);
                    compiler.Compile(args[4].Expression);
                    compiler.Instructions.Emit(new Invoke5Instruction(Parent.PyContext));
                    return;
                case 6:
                    compiler.Compile(Parent.LocalContext);
                    compiler.Compile(Target);
                    compiler.Compile(args[0].Expression);
                    compiler.Compile(args[1].Expression);
                    compiler.Compile(args[2].Expression);
                    compiler.Compile(args[3].Expression);
                    compiler.Compile(args[4].Expression);
                    compiler.Compile(args[5].Expression);
                    compiler.Instructions.Emit(new Invoke6Instruction(Parent.PyContext));
                    return;

                    // *** END GENERATED CODE ***

                    #endregion
            }
            compiler.Compile(Reduce());
        }

        #endregion

        private abstract class InvokeInstruction : Instruction {
            public override int ProducedStack => 1;

            public override string InstructionName => "Python Invoke" + (ConsumedStack - 1);
        }

        #region Generated Python Call Expression Instructions

        // *** BEGIN GENERATED CODE ***
        // generated by function: gen_call_expression_instructions from: generate_calls.py

        private class Invoke0Instruction : InvokeInstruction {
            private readonly CallSite<Func<CallSite, CodeContext, object, object>> _site;

            public Invoke0Instruction(PythonContext context) {
                _site = context.CallSite0;
            }

            public override int ConsumedStack => 2;

            public override int Run(InterpretedFrame frame) {

                var target = frame.Pop();
                frame.Push(_site.Target(_site, (CodeContext)frame.Pop(), target));
                return +1;
            }
        }

        private class Invoke1Instruction : InvokeInstruction {
            private readonly CallSite<Func<CallSite, CodeContext, object, object, object>> _site;

            public Invoke1Instruction(PythonContext context) {
                _site = context.CallSite1;
            }

            public override int ConsumedStack => 3;

            public override int Run(InterpretedFrame frame) {
                var arg0 = frame.Pop();
                var target = frame.Pop();
                frame.Push(_site.Target(_site, (CodeContext)frame.Pop(), target, arg0));
                return +1;
            }
        }

        private class Invoke2Instruction : InvokeInstruction {
            private readonly CallSite<Func<CallSite, CodeContext, object, object, object, object>> _site;

            public Invoke2Instruction(PythonContext context) {
                _site = context.CallSite2;
            }

            public override int ConsumedStack => 4;

            public override int Run(InterpretedFrame frame) {
                var arg1 = frame.Pop();
                var arg0 = frame.Pop();
                var target = frame.Pop();
                frame.Push(_site.Target(_site, (CodeContext)frame.Pop(), target, arg0, arg1));
                return +1;
            }
        }

        private class Invoke3Instruction : InvokeInstruction {
            private readonly CallSite<Func<CallSite, CodeContext, object, object, object, object, object>> _site;

            public Invoke3Instruction(PythonContext context) {
                _site = context.CallSite3;
            }

            public override int ConsumedStack => 5;

            public override int Run(InterpretedFrame frame) {
                var arg2 = frame.Pop();
                var arg1 = frame.Pop();
                var arg0 = frame.Pop();
                var target = frame.Pop();
                frame.Push(_site.Target(_site, (CodeContext)frame.Pop(), target, arg0, arg1, arg2));
                return +1;
            }
        }

        private class Invoke4Instruction : InvokeInstruction {
            private readonly CallSite<Func<CallSite, CodeContext, object, object, object, object, object, object>> _site;

            public Invoke4Instruction(PythonContext context) {
                _site = context.CallSite4;
            }

            public override int ConsumedStack => 6;

            public override int Run(InterpretedFrame frame) {
                var arg3 = frame.Pop();
                var arg2 = frame.Pop();
                var arg1 = frame.Pop();
                var arg0 = frame.Pop();
                var target = frame.Pop();
                frame.Push(_site.Target(_site, (CodeContext)frame.Pop(), target, arg0, arg1, arg2, arg3));
                return +1;
            }
        }

        private class Invoke5Instruction : InvokeInstruction {
            private readonly CallSite<Func<CallSite, CodeContext, object, object, object, object, object, object, object>> _site;

            public Invoke5Instruction(PythonContext context) {
                _site = context.CallSite5;
            }

            public override int ConsumedStack => 7;

            public override int Run(InterpretedFrame frame) {
                var arg4 = frame.Pop();
                var arg3 = frame.Pop();
                var arg2 = frame.Pop();
                var arg1 = frame.Pop();
                var arg0 = frame.Pop();
                var target = frame.Pop();
                frame.Push(_site.Target(_site, (CodeContext)frame.Pop(), target, arg0, arg1, arg2, arg3, arg4));
                return +1;
            }
        }

        private class Invoke6Instruction : InvokeInstruction {
            private readonly CallSite<Func<CallSite, CodeContext, object, object, object, object, object, object, object, object>> _site;

            public Invoke6Instruction(PythonContext context) {
                _site = context.CallSite6;
            }

            public override int ConsumedStack => 8;

            public override int Run(InterpretedFrame frame) {
                var arg5 = frame.Pop();
                var arg4 = frame.Pop();
                var arg3 = frame.Pop();
                var arg2 = frame.Pop();
                var arg1 = frame.Pop();
                var arg0 = frame.Pop();
                var target = frame.Pop();
                frame.Push(_site.Target(_site, (CodeContext)frame.Pop(), target, arg0, arg1, arg2, arg3, arg4, arg5));
                return +1;
            }
        }

        // *** END GENERATED CODE ***

        #endregion

        public override string NodeName => "function call";

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                Target?.Walk(walker);
                if (Args != null) {
                    foreach (Arg arg in Args) {
                        arg.Walk(walker);
                    }
                }
                if (ImplicitArgs.Count > 0) {
                    foreach (Arg arg in ImplicitArgs) {
                        arg.Walk(walker);
                    }
                }
            }
            walker.PostWalk(this);
        }
    }
}
