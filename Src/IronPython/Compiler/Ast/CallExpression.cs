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
        public CallExpression(Expression target, IReadOnlyList<Arg>? args, IReadOnlyList<Arg>? kwargs) {
            Target = target;
            Args = args?.ToArray() ?? Array.Empty<Arg>();
            Kwargs = kwargs?.ToArray() ?? Array.Empty<Arg>();
        }

        public Expression Target { get; }

        public IReadOnlyList<Arg> Args { get; }

        public IReadOnlyList<Arg> Kwargs { get; }

        internal IList<Arg> ImplicitArgs => _implicitArgs ??= new List<Arg>();
        private List<Arg>? _implicitArgs;

        public bool NeedsLocalsDictionary() {
            if (!(Target is NameExpression nameExpr)) return false;

            if (nameExpr.Name == "eval" || nameExpr.Name == "exec") return true;
            if (nameExpr.Name == "dir" || nameExpr.Name == "vars" || nameExpr.Name == "locals") {
                // could be splatting empty list or dict resulting in 0-param call which needs context
                return Args.All(arg => arg.Name == "*") && Kwargs.All(arg => arg.Name == "**");
            }

            return false;
        }

        public override MSAst.Expression Reduce() {
            IReadOnlyList<Arg> args = Args;
            if (Args.Count == 0 && _implicitArgs != null && _implicitArgs.Count > 0) {
                args = _implicitArgs;
            }

            // count splatted list args and find the lowest index of a list argument, if any
            ScanArgs(args, ArgumentType.List, out var numList, out int firstListPos);
            Debug.Assert(numList == 0 || firstListPos < args.Count);

            // count splatted dictionary args and find the lowest index of a dict argument, if any
            ScanArgs(Kwargs, ArgumentType.Dictionary, out var numDict, out int firstDictPos);
            Debug.Assert(numDict == 0 || firstDictPos < Kwargs.Count);

            // all list arguments and all simple arguments after the first list will be collated into a single list for the actual call
            // all dictionary arguments will be merged into a single dictionary for the actual call
            Argument[] kinds = new Argument[firstListPos + Math.Min(numList, 1) + (Kwargs.Count - numDict) + Math.Min(numDict, 1)];
            MSAst.Expression[] values = new MSAst.Expression[2 + kinds.Length];

            values[0] = Parent.LocalContext;
            values[1] = Target;

            int i = 0;

            // add simple arguments
            if (firstListPos > 0) {
                foreach (var arg in args) {
                    if (i == firstListPos) break;
                    Debug.Assert(arg.ArgumentInfo.Kind == ArgumentType.Simple);
                    kinds[i] = arg.ArgumentInfo;
                    values[i + 2] = arg.Expression;
                    i++;
                }
            }

            // unpack list arguments
            if (numList > 0) {
                var arg = args[firstListPos];
                Debug.Assert(arg.ArgumentInfo.Kind == ArgumentType.List);
                kinds[i] = arg.ArgumentInfo;
                values[i + 2] = UnpackListHelper(args, firstListPos);
                i++;
            }

            // add named arguments
            if (Kwargs.Count != numDict) {
                foreach (var arg in Kwargs) {
                    var info = arg.ArgumentInfo;
                    if (info.Kind == ArgumentType.Named) {
                        kinds[i] = info;
                        values[i + 2] = arg.Expression;
                        i++;
                    }
                }
            }

            // unpack dict arguments
            if (numDict > 0) {
                var arg = Kwargs[firstDictPos];
                Debug.Assert(arg.ArgumentInfo.Kind == ArgumentType.Dictionary);
                kinds[i] = arg.ArgumentInfo;
                values[i + 2] = UnpackDictHelper(Parent.LocalContext, Kwargs, numDict, firstDictPos);
            }

            return Parent.Invoke(
                new CallSignature(kinds),
                values
            );

            static void ScanArgs(IReadOnlyList<Arg> args, ArgumentType scanForType, out int numArgs, out int firstArgPos) {
                numArgs = 0;
                firstArgPos = args.Count;

                if (args.Count == 0) return;

                for (var i = args.Count - 1; i >= 0; i--) {
                    var arg = args[i];
                    var info = arg.ArgumentInfo;
                    if (info.Kind == scanForType) {
                        firstArgPos = i;
                        numArgs++;
                    }
                }
            }

            static MSAst.Expression UnpackListHelper(IReadOnlyList<Arg> args, int firstListPos) {
                Debug.Assert(args.Count > 0);
                Debug.Assert(args[firstListPos].ArgumentInfo.Kind == ArgumentType.List);

                if (args.Count - firstListPos == 1) return args[firstListPos].Expression;

                var expressions = new ReadOnlyCollectionBuilder<MSAst.Expression>(args.Count - firstListPos + 2);
                var varExpr = Expression.Variable(typeof(PythonList), "$coll");
                expressions.Add(Expression.Assign(varExpr, Expression.Call(AstMethods.MakeEmptyList)));
                for (int i = firstListPos; i < args.Count; i++) {
                    var arg = args[i];
                    if (arg.ArgumentInfo.Kind == ArgumentType.List) {
                        expressions.Add(Expression.Call(AstMethods.ListExtend, varExpr, AstUtils.Convert(arg.Expression, typeof(object))));
                    } else {
                        expressions.Add(Expression.Call(AstMethods.ListAppend, varExpr, AstUtils.Convert(arg.Expression, typeof(object))));
                    }
                }
                expressions.Add(varExpr);
                return Expression.Block(typeof(PythonList), new MSAst.ParameterExpression[] { varExpr }, expressions);
            }

            static MSAst.Expression UnpackDictHelper(MSAst.Expression context, IReadOnlyList<Arg> kwargs, int numDict, int firstDictPos) {
                Debug.Assert(kwargs.Count > 0);
                Debug.Assert(0 < numDict && numDict <= kwargs.Count);
                Debug.Assert(kwargs[firstDictPos].ArgumentInfo.Kind == ArgumentType.Dictionary);

                if (numDict == 1) return kwargs[firstDictPos].Expression;

                var expressions = new ReadOnlyCollectionBuilder<MSAst.Expression>(numDict + 2);
                var varExpr = Expression.Variable(typeof(PythonDictionary), "$dict");
                expressions.Add(Expression.Assign(varExpr, Expression.Call(AstMethods.MakeEmptyDict)));
                for (int i = firstDictPos; i < kwargs.Count; i++) {
                    var arg = kwargs[i];
                    if (arg.ArgumentInfo.Kind == ArgumentType.Dictionary) {
                        expressions.Add(Expression.Call(AstMethods.DictMerge, context, varExpr, arg.Expression));
                    }
                }
                expressions.Add(varExpr);
                return Expression.Block(typeof(PythonDictionary), new MSAst.ParameterExpression[] { varExpr }, expressions);
            }
        }

        #region IInstructionProvider Members

        void IInstructionProvider.AddInstructions(LightCompiler compiler) {
            IReadOnlyList<Arg> args = Args;
            if (args.Count == 0 && _implicitArgs != null && _implicitArgs.Count > 0) {
                args = _implicitArgs;
            }

            if (Kwargs.Count > 0) {
                compiler.Compile(Reduce());
                return;
            }

            for (int i = 0; i < args.Count; i++) {
                if (!args[i].ArgumentInfo.IsSimple) {
                    compiler.Compile(Reduce());
                    return;
                }
            }

            switch (args.Count) {
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
                if (_implicitArgs != null && _implicitArgs.Count > 0) {
                    foreach (Arg arg in ImplicitArgs) {
                        arg.Walk(walker);
                    }
                }
                foreach (Arg arg in Kwargs) {
                    arg.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }
    }
}
