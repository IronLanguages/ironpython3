// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Linq.Expressions;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Dynamic;

using Microsoft.Scripting;
using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Actions.Calls;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

namespace IronPython.Runtime.Binding {
    using Ast = Expression;
    using AstUtils = Microsoft.Scripting.Ast.Utils;

    internal class MetaPythonFunction : MetaPythonObject, IPythonInvokable, IPythonOperable, IPythonConvertible, IInferableInvokable, IConvertibleMetaObject, IPythonGetable {
        public MetaPythonFunction(Expression/*!*/ expression, BindingRestrictions/*!*/ restrictions, PythonFunction/*!*/ value)
            : base(expression, BindingRestrictions.Empty, value) {
            Assert.NotNull(value);
        }

        #region IPythonInvokable Members

        public DynamicMetaObject/*!*/ Invoke(PythonInvokeBinder/*!*/ pythonInvoke, Expression/*!*/ codeContext, DynamicMetaObject/*!*/ target, DynamicMetaObject/*!*/[]/*!*/ args) {
            return new FunctionBinderHelper(pythonInvoke, this, codeContext, args).MakeMetaObject();
        }

        #endregion


        #region IPythonGetable Members

        public DynamicMetaObject GetMember(PythonGetMemberBinder member, DynamicMetaObject codeContext) {
            return BindGetMemberWorker(member, member.Name, codeContext);
        }

        #endregion

        #region MetaObject Overrides

        public override DynamicMetaObject/*!*/ BindInvokeMember(InvokeMemberBinder/*!*/ action, DynamicMetaObject/*!*/[]/*!*/ args) {
            ParameterExpression tmp = Expression.Parameter(typeof(object));

            // first get the default binder value
            DynamicMetaObject fallback = action.FallbackInvokeMember(this, args);

            // then fallback w/ an error suggestion that does a late bound lookup.
            return action.FallbackInvokeMember(
                this,
                args,
                new DynamicMetaObject(
                    Ast.Block(
                        new[] { tmp },
                        Ast.Condition(
                            Ast.NotEqual(
                                Ast.Assign(
                                    tmp,
                                    Ast.Call(
                                        typeof(PythonOps).GetMethod(nameof(PythonOps.PythonFunctionGetMember)),
                                        AstUtils.Convert(
                                            Expression,
                                            typeof(PythonFunction)
                                        ),
                                        Expression.Constant(action.Name)
                                    )
                                ),
                                Ast.Constant(OperationFailed.Value)
                            ),
                            action.FallbackInvoke(
                                new DynamicMetaObject(tmp, BindingRestrictions.Empty),
                                args,
                                null
                            ).Expression,
                            AstUtils.Convert(fallback.Expression, typeof(object))
                        )
                    ),
                    BindingRestrictions.GetTypeRestriction(Expression, typeof(PythonFunction)).Merge(fallback.Restrictions)
                )
            );
        }

        public override DynamicMetaObject/*!*/ BindInvoke(InvokeBinder/*!*/ call, params DynamicMetaObject/*!*/[]/*!*/ args) {
            return new FunctionBinderHelper(call, this, null, args).MakeMetaObject();
        }

        public override DynamicMetaObject BindConvert(ConvertBinder/*!*/ conversion) {
            return ConvertWorker(conversion, conversion.Type, conversion.Explicit ? ConversionResultKind.ExplicitCast : ConversionResultKind.ImplicitCast);
        }

        public DynamicMetaObject BindConvert(PythonConversionBinder binder) {
            return ConvertWorker(binder, binder.Type, binder.ResultKind);
        }

        public DynamicMetaObject ConvertWorker(DynamicMetaObjectBinder binder, Type type, ConversionResultKind kind) {
            if (type.IsSubclassOf(typeof(Delegate))) {
                return MakeDelegateTarget(binder, type, Restrict(typeof(PythonFunction)));
            }
            return FallbackConvert(binder);
        }

        public override System.Collections.Generic.IEnumerable<string> GetDynamicMemberNames() {
            foreach (object o in Value.__dict__.Keys) {
                if (o is string) {
                    yield return (string)o;
                }
            }
        }

        public override DynamicMetaObject BindGetMember(GetMemberBinder binder) {
            return BindGetMemberWorker(binder, binder.Name, PythonContext.GetCodeContextMO(binder));
        }

        private DynamicMetaObject BindGetMemberWorker(DynamicMetaObjectBinder binder, string name, DynamicMetaObject codeContext) {
            ParameterExpression tmp = Expression.Parameter(typeof(object));

            // first get the default binder value
            DynamicMetaObject fallback = FallbackGetMember(binder, this, codeContext);

            // then fallback w/ an error suggestion that does a late bound lookup.
            return FallbackGetMember(
                binder,
                this,
                codeContext,
                new DynamicMetaObject(
                    Ast.Block(
                        new[] { tmp },
                        Ast.Condition(
                            Ast.NotEqual(
                                Ast.Assign(
                                    tmp,
                                    Ast.Call(
                                        typeof(PythonOps).GetMethod(nameof(PythonOps.PythonFunctionGetMember)),
                                        AstUtils.Convert(
                                            Expression,
                                            typeof(PythonFunction)
                                        ),
                                        Expression.Constant(name)
                                    )
                                ),
                                Ast.Constant(OperationFailed.Value)
                            ),
                            tmp,
                            AstUtils.Convert(fallback.Expression, typeof(object))
                        )
                    ),
                    BindingRestrictions.GetTypeRestriction(Expression, typeof(PythonFunction)).Merge(fallback.Restrictions)
                )
            );
        }

        private static DynamicMetaObject FallbackGetMember(DynamicMetaObjectBinder binder, DynamicMetaObject self, DynamicMetaObject codeContext) {
            return FallbackGetMember(binder, self, codeContext, null);
        }

        private static DynamicMetaObject FallbackGetMember(DynamicMetaObjectBinder binder, DynamicMetaObject self, DynamicMetaObject codeContext, DynamicMetaObject errorSuggestion) {
            if (binder is PythonGetMemberBinder pyGetMem) {
                return pyGetMem.Fallback(self, codeContext, errorSuggestion);
            }

            return ((GetMemberBinder)binder).FallbackGetMember(self, errorSuggestion);
        }

        public override DynamicMetaObject BindSetMember(SetMemberBinder binder, DynamicMetaObject value) {
            // fallback w/ an error suggestion that does a late bound set
            return binder.FallbackSetMember(
                this,
                value,
                new DynamicMetaObject(
                    Ast.Call(
                        typeof(PythonOps).GetMethod(nameof(PythonOps.PythonFunctionSetMember)),
                        AstUtils.Convert(
                            Expression,
                            typeof(PythonFunction)
                        ),
                        Expression.Constant(binder.Name),
                        AstUtils.Convert(
                            value.Expression,
                            typeof(object)
                        )
                    ),
                    BindingRestrictions.GetTypeRestriction(Expression, typeof(PythonFunction))
                )
            );
        }

        public override DynamicMetaObject BindDeleteMember(DeleteMemberBinder binder) {
            switch (binder.Name) {
                case "__dict__":
                    return new DynamicMetaObject(
                        Expression.Call(
                            typeof(PythonOps).GetMethod(nameof(PythonOps.PythonFunctionDeleteDict))
                        ),
                        BindingRestrictions.GetTypeRestriction(Expression, typeof(PythonFunction))
                    );
                case "__doc__":
                    return new DynamicMetaObject(
                        Expression.Call(
                            typeof(PythonOps).GetMethod(nameof(PythonOps.PythonFunctionDeleteDoc)),
                            Expression.Convert(Expression, typeof(PythonFunction))
                        ),
                        BindingRestrictions.GetTypeRestriction(Expression, typeof(PythonFunction))
                    );
                case "__defaults__":
                    return new DynamicMetaObject(
                        Expression.Call(
                            typeof(PythonOps).GetMethod(nameof(PythonOps.PythonFunctionDeleteDefaults)),
                            Expression.Convert(Expression, typeof(PythonFunction))
                        ),
                        BindingRestrictions.GetTypeRestriction(Expression, typeof(PythonFunction))
                    ); 
            }

            // first get the default binder value
            DynamicMetaObject fallback = binder.FallbackDeleteMember(this);

            // then fallback w/ an error suggestion that does a late bound delete
            return binder.FallbackDeleteMember(
                this,
                new DynamicMetaObject(
                    Expression.Condition(
                        Ast.Call(
                            typeof(PythonOps).GetMethod(nameof(PythonOps.PythonFunctionDeleteMember)),
                            AstUtils.Convert(
                                Expression,
                                typeof(PythonFunction)
                            ),
                            Expression.Constant(binder.Name)
                        ),
                        Expression.Default(typeof(void)),       // we deleted the member
                        AstUtils.Convert(
                            fallback.Expression,                // report language specific error,
                            typeof(void)
                        )
                    ),
                    BindingRestrictions.GetTypeRestriction(Expression, typeof(PythonFunction)).Merge(fallback.Restrictions)
                )
            );
        }

        #endregion

        #region Calls

        /// <summary>
        /// Performs the actual work of binding to the function.
        /// 
        /// Overall this works by going through the arguments and attempting to bind all the outstanding known
        /// arguments - position arguments and named arguments which map to parameters are easy and handled
        /// in the 1st pass for GetArgumentsForRule.  We also pick up any extra named or position arguments which
        /// will need to be passed off to a kw argument or a params array.
        /// 
        /// After all the normal args have been assigned to do a 2nd pass in FinishArguments.  Here we assign
        /// a value to either a value from the params list, kw-dict, or defaults.  If there is ambiguity between
        /// this (e.g. we have a splatted params list, kw-dict, and defaults) we call a helper which extracts them
        /// in the proper order (first try the list, then the dict, then the defaults).
        /// </summary>
        private class FunctionBinderHelper {
            private readonly MetaPythonFunction/*!*/ _func;         // the meta object for the function we're calling
            private readonly DynamicMetaObject/*!*/[]/*!*/ _args;          // the arguments for the function
            private readonly DynamicMetaObject/*!*/[]/*!*/ _originalArgs;  // the original arguments for the function
            private readonly DynamicMetaObjectBinder/*!*/ _call;               // the signature for the method call
            private readonly Expression _codeContext;               // the code context expression if one is available.

            private readonly List<ParameterExpression>/*!*/ _temps; // temporary variables allocated to create the rule
            private ParameterExpression _dict, _params, _paramsLen; // splatted dictionary & params + the initial length of the params array, null if not provided.
            private List<Expression> _init;                         // a set of initialization code (e.g. creating a list for the params array)
            private Expression _error;                              // a custom error expression if the default needs to be overridden.
            private bool _extractedParams;                          // true if we needed to extract a parameter from the parameter list.
            private bool _extractedDefault;                         // true if we needed to extract a parameter from the kw list.
            private bool _needCodeTest;                             // true if we need to test the code object
            private Expression _deferTest;                          // non-null if we have a test which could fail at runtime and we need to fallback to deferal
            private Expression _userProvidedParams;                 // expression the user provided that should be expanded for params.
            private Expression _paramlessCheck;                     // tests when we have no parameters

            public FunctionBinderHelper(DynamicMetaObjectBinder/*!*/ call, MetaPythonFunction/*!*/ function, Expression codeContext, DynamicMetaObject/*!*/[]/*!*/ args) {
                PerfTrack.NoteEvent(PerfTrack.Categories.Binding, "PythonFunction Invoke " + function.Value.FunctionCompatibility + " w/ " + args.Length + " args");
                PerfTrack.NoteEvent(PerfTrack.Categories.BindingTarget, "PythonFunction");

                _call = call;
                _func = function;
                _args = args;
                _originalArgs = args;
                _temps = new List<ParameterExpression>();
                _codeContext = codeContext;

                // Remove the passed in instance argument if present
                int instanceIndex = Signature.IndexOf(ArgumentType.Instance);
                if (instanceIndex > -1) {
                    _args = ArrayUtils.RemoveAt(_args, instanceIndex);
                }
            }

            public DynamicMetaObject/*!*/ MakeMetaObject() {
                Expression[] invokeArgs = GetArgumentsForRule();
                BindingRestrictions restrict = _func.Restrictions.Merge(GetRestrictions().Merge(BindingRestrictions.Combine(_args)));
                DynamicMetaObject res;

                if (invokeArgs != null) {
                    // successful call
                    Expression target = AddInitialization(MakeFunctionInvoke(invokeArgs));

                    if (_temps.Count > 0) {
                        target = Ast.Block(
                            _temps,
                            target
                        );
                    }

                    res = new DynamicMetaObject(
                        target,
                        restrict
                    );
                } else if (_error != null) {
                    // custom error generated while figuring out the call
                    res = new DynamicMetaObject(_error, restrict);
                } else {
                    // generic error
                    res = new DynamicMetaObject(
                        _call.Throw(
                            Ast.Call(
                                typeof(PythonOps).GetMethod(Signature.HasKeywordArgument() ? nameof(PythonOps.BadKeywordArgumentError) : nameof(PythonOps.FunctionBadArgumentError)),
                                AstUtils.Convert(GetFunctionParam(), typeof(PythonFunction)),
                                AstUtils.Constant(Signature.GetProvidedPositionalArgumentCount())
                            ),
                            typeof(object)
                        ),
                        restrict
                    );
                }

                DynamicMetaObject[] deferArgs = ArrayUtils.Insert(_func, _originalArgs);

                if (_codeContext != null) {
                    deferArgs = ArrayUtils.Insert(new DynamicMetaObject(_codeContext, BindingRestrictions.Empty), deferArgs);
                }

                return BindingHelpers.AddDynamicTestAndDefer(
                    _call, 
                    res, 
                    deferArgs, 
                    new ValidationInfo(_deferTest),
                    res.Expression.Type     // force defer to our return type, our restrictions guarantee this to be true (only defaults can change, and we restrict to the delegate type)
                );
            }


            private CallSignature Signature {
                get {
                    return BindingHelpers.GetCallSignature(_call);
                }
            }

            /// <summary>
            /// Makes the test for our rule.
            /// </summary>
            private BindingRestrictions/*!*/ GetRestrictions() {
                if (_extractedDefault) {
                    return BindingRestrictions.GetInstanceRestriction(_func.Expression, _func.Value);
                } else if (_needCodeTest) {
                    return GetSimpleRestriction().Merge(
                        BindingRestrictions.GetInstanceRestriction(
                            Expression.Property(
                                GetFunctionParam(),
                                "__code__"
                            ),
                            _func.Value.__code__
                        )
                    );
                }

                return GetSimpleRestriction();
            }

            /// <summary>
            /// Makes the test when we just have simple positional arguments.
            /// </summary>
            private BindingRestrictions/*!*/ GetSimpleRestriction() {
                _deferTest = Ast.Equal(
                    Ast.Call(
                        typeof(PythonOps).GetMethod(nameof(PythonOps.FunctionGetCompatibility)),
                        Ast.Convert(_func.Expression, typeof(PythonFunction))
                    ),
                    AstUtils.Constant(_func.Value.FunctionCompatibility)
                );

                return BindingRestrictionsHelpers.GetRuntimeTypeRestriction(
                    _func.Expression, typeof(PythonFunction)
                );
            }

            /// <summary>
            /// Gets the array of expressions which correspond to each argument for the function.  These
            /// correspond with the function as it's defined in Python and must be transformed for our
            /// delegate type before being used.
            /// </summary>
            private Expression/*!*/[]/*!*/ GetArgumentsForRule() {
                int normalArgumentCount = _func.Value.NormalArgumentCount;
                int keywordOnlyArgumentCount = _func.Value.KeywordOnlyArgumentCount;

                Expression[] exprArgs = new Expression[normalArgumentCount + keywordOnlyArgumentCount + _func.Value.ExtraArguments];
                List<Expression> extraArgs = null;
                Dictionary<string, Expression> namedArgs = null;
                int instanceIndex = Signature.IndexOf(ArgumentType.Instance);

                // walk all the provided args and find out where they go...
                for (int i = 0; i < _args.Length; i++) {
                    int parameterIndex = (instanceIndex == -1 || i < instanceIndex) ? i : i + 1;

                    switch (Signature.GetArgumentKind(i)) {
                        case ArgumentType.Dictionary:
                            _args[parameterIndex] = MakeDictionaryCopy(_args[parameterIndex]);
                            continue;

                        case ArgumentType.List:
                            _userProvidedParams = _args[parameterIndex].Expression;
                            continue;

                        case ArgumentType.Named:
                            _needCodeTest = true;
                            bool foundName = false;
                            for (int j = 0; j < normalArgumentCount + keywordOnlyArgumentCount; j++) {
                                if (_func.Value.ArgNames[j] == Signature.GetArgumentName(i)) {
                                    if (exprArgs[j] != null) {
                                        // kw-argument provided for already provided normal argument.
                                        // Since the repeated kwargs is caught by a SyntaxError, this
                                        // error can only occur with a positional-keyword arg conflict
                                        if (_error == null) {
                                            _error = _call.Throw(
                                                Expression.Call(
                                                    typeof(PythonOps).GetMethod(nameof(PythonOps.MultipleArgumentError)),
                                                    GetFunctionParam(),
                                                    Expression.Constant(_func.Value.ArgNames[j])
                                                ),
                                                typeof(object)
                                            );
                                        }
                                        return null;
                                    }

                                    exprArgs[j] = _args[parameterIndex].Expression;
                                    foundName = true;
                                    break;
                                }
                            }

                            if (!foundName) {
                                if (namedArgs == null) {
                                    namedArgs = new Dictionary<string, Expression>();
                                }
                                namedArgs[Signature.GetArgumentName(i)] = _args[parameterIndex].Expression;
                            }
                            continue;
                    }

                    if (i < normalArgumentCount) {
                        exprArgs[i] = _args[parameterIndex].Expression;
                    } else {
                        if (extraArgs == null) {
                            extraArgs = new List<Expression>();
                        }
                        extraArgs.Add(_args[parameterIndex].Expression);
                    }
                }

                if (!FinishArguments(exprArgs, extraArgs, namedArgs)) {
                    if (namedArgs != null && _func.Value.ExpandDictPosition == -1) {
                        MakeUnexpectedKeywordError(namedArgs);
                    }

                    return null;
                }

                return GetArgumentsForTargetType(exprArgs);
            }

            /// <summary>
            /// Binds any missing arguments to values from params array, kw dictionary, or default values.
            /// </summary>
            private bool FinishArguments(Expression[] exprArgs, List<Expression> paramsArgs, Dictionary<string, Expression> namedArgs) {
                var normalArgumentCount = _func.Value.NormalArgumentCount;
                var normalNoDefaults = normalArgumentCount - _func.Value.Defaults.Length;

                for (int i = 0; i < normalArgumentCount; i++) {
                    var argName = _func.Value.ArgNames[i];

                    if (exprArgs[i] != null) {
                        exprArgs[i] = ValidateNotDuplicate(exprArgs[i], argName, i);
                        continue;
                    }

                    if (i < normalNoDefaults) {
                        exprArgs[i] = ExtractNonDefaultValue(argName);
                        if (exprArgs[i] == null) {
                            // can't get a value, this is an invalid call.
                            return false;
                        }
                    } else {
                        exprArgs[i] = ExtractDefaultValue(argName, i - normalNoDefaults);
                    }
                }

                var keywordOnlyArgumentCount = _func.Value.KeywordOnlyArgumentCount;

                if (_func.Value.NeedsCodeTest) _needCodeTest = true;

                for (int i = normalArgumentCount; i < normalArgumentCount + keywordOnlyArgumentCount; i++) {
                    var argName = _func.Value.ArgNames[i];

                    if (exprArgs[i] is null) {
                        if (_func.Value.__kwdefaults__ != null && _func.Value.__kwdefaults__.ContainsKey(argName)) {
                            exprArgs[i] = ExtractKeywordOnlyDefault(argName, i);
                        } else if (_dict != null) {
                            exprArgs[i] = ExtractDictionaryArgument(argName);
                        }
                        if (exprArgs[i] == null) {
                            // can't get a value, this is an invalid call.
                            return false;
                        }
                    }
                }

                if (!TryFinishList(exprArgs, paramsArgs) ||
                    !TryFinishDictionary(exprArgs, namedArgs))
                    return false;

                // add check for extra parameters.
                AddCheckForNoExtraParameters(exprArgs);

                return true;
            }

            /// <summary>
            /// Creates the argument for the list expansion parameter.
            /// </summary>
            private bool TryFinishList(Expression[] exprArgs, List<Expression> paramsArgs) {
                if (_func.Value.ExpandListPosition != -1) {
                    if (_userProvidedParams != null) {
                        if (_params == null && paramsArgs == null) {
                            // we didn't extract any params, we can re-use a Tuple or
                            // make a single copy.
                            exprArgs[_func.Value.ExpandListPosition] = Ast.Call(
                                typeof(PythonOps).GetMethod(nameof(PythonOps.GetOrCopyParamsTuple)),
                                _codeContext ?? AstUtils.Constant(DefaultContext.Default),
                                GetFunctionParam(),
                                AstUtils.Convert(_userProvidedParams, typeof(object))
                            );
                        } else {
                            // user provided a sequence to be expanded, and we may have used it,
                            // or we have extra args.
                            EnsureParams();

                            exprArgs[_func.Value.ExpandListPosition] = Ast.Call(
                                typeof(PythonOps).GetMethod(nameof(PythonOps.MakeTupleFromSequence)),
                                AstUtils.Convert(_params, typeof(object))
                            );

                            if (paramsArgs != null) {
                                MakeParamsAddition(paramsArgs);
                            }
                        }
                    } else {
                        exprArgs[_func.Value.ExpandListPosition] = MakeParamsTuple(paramsArgs);
                    }
                } else if (paramsArgs != null) {
                    // extra position args which are unused and no where to put them.
                    return false;
                }
                return true;
            }

            /// <summary>
            /// Adds extra positional arguments to the start of the expanded list.
            /// </summary>
            private void MakeParamsAddition(List<Expression> paramsArgs) {
                _extractedParams = true;

                List<Expression> args = new List<Expression>(paramsArgs.Count + 1);
                args.Add(_params);
                args.AddRange(paramsArgs);

                EnsureInit();

                _init.Add(
                    AstUtils.ComplexCallHelper(
                        typeof(PythonOps).GetMethod(nameof(PythonOps.AddParamsArguments)),
                        args.ToArray()
                    )
                );
            }

            /// <summary>
            /// Creates the argument for the dictionary expansion parameter.
            /// </summary>
            private bool TryFinishDictionary(Expression[] exprArgs, Dictionary<string, Expression> namedArgs) {
                if (_func.Value.ExpandDictPosition != -1) {
                    if (_dict != null) {
                        // used provided a dictionary to be expanded
                        exprArgs[_func.Value.ExpandDictPosition] = _dict;
                        if (namedArgs != null) {
                            foreach (KeyValuePair<string, Expression> kvp in namedArgs) {
                                MakeDictionaryAddition(kvp);
                            }
                        }
                    } else {
                        exprArgs[_func.Value.ExpandDictPosition] = MakeDictionary(namedArgs);
                    }
                } else if (namedArgs != null) {
                    // extra named args which are unused and no where to put them.
                    return false;
                }
                return true;
            }

            /// <summary>
            /// Adds an unbound keyword argument into the dictionary.
            /// </summary>
            /// <param name="kvp"></param>
            private void MakeDictionaryAddition(KeyValuePair<string, Expression> kvp) {
                _init.Add(
                    Ast.Call(
                        typeof(PythonOps).GetMethod(nameof(PythonOps.AddDictionaryArgument)),
                        AstUtils.Convert(GetFunctionParam(), typeof(PythonFunction)),
                        AstUtils.Constant(kvp.Key),
                        AstUtils.Convert(kvp.Value, typeof(object)),
                        AstUtils.Convert(_dict, typeof(PythonDictionary))
                    )
                );
            }

            /// <summary>
            /// Adds a check to the last parameter (so it's evaluated after we've extracted
            /// all the parameters) to ensure that we don't have any extra params or kw-params
            /// when we don't have a params array or params dict to expand them into.
            /// </summary>
            private void AddCheckForNoExtraParameters(Expression[] exprArgs) {
                List<Expression> tests = new List<Expression>(3);

                // test we've used all of the extra parameters
                if (_func.Value.ExpandListPosition == -1) {
                    if (_params != null) {
                        // we used some params, they should have gone down to zero...
                        tests.Add(
                            Ast.Call(
                                typeof(PythonOps).GetMethod(nameof(PythonOps.CheckParamsZero)),
                                AstUtils.Convert(GetFunctionParam(), typeof(PythonFunction)),
                                _params // Queue<object>
                            )
                        );
                    } else if (_userProvidedParams != null) {
                        // the user provided params, we didn't need any, and they should be zero
                        tests.Add(
                            Ast.Call(
                                typeof(PythonOps).GetMethod(nameof(PythonOps.CheckUserParamsZero)),
                                AstUtils.Convert(GetFunctionParam(), typeof(PythonFunction)),
                                AstUtils.Convert(_userProvidedParams, typeof(object))
                            )
                        );
                    }
                }

                // test that we've used all the extra named arguments
                if (_func.Value.ExpandDictPosition == -1 && _dict != null) {
                    tests.Add(
                        Ast.Call(
                            typeof(PythonOps).GetMethod(nameof(PythonOps.CheckDictionaryZero)),
                            AstUtils.Convert(GetFunctionParam(), typeof(PythonFunction)),
                            AstUtils.Convert(_dict, typeof(IDictionary))
                        )
                    );
                }

                if (tests.Count != 0) {
                    if (exprArgs.Length != 0) {
                        // if we have arguments run the tests after the last arg is evaluated.
                        Expression last = exprArgs[exprArgs.Length - 1];

                        ParameterExpression temp;

                        _temps.Add(temp = Ast.Variable(last.Type, "$temp"));

                        tests.Insert(0, Ast.Assign(temp, last));
                        tests.Add(temp);
                        exprArgs[exprArgs.Length - 1] = Ast.Block(tests.ToArray());
                    } else {
                        // otherwise run them right before the method call
                        _paramlessCheck = Ast.Block(tests.ToArray());
                    }
                }
            }

            /// <summary>
            /// Helper function to validate that a named arg isn't duplicated with by
            /// a params list or the dictionary (or both).
            /// </summary>
            private Expression ValidateNotDuplicate(Expression value, string name, int position) {
                List<Ast> statements = new List<Ast>();

                if (_userProvidedParams != null && position >= Signature.GetProvidedPositionalArgumentCount()) {
                    EnsureParams();

                    statements.Add(
                        Ast.Call(
                            typeof(PythonOps).GetMethod(nameof(PythonOps.VerifyUnduplicatedByPosition)),
                            AstUtils.Convert(GetFunctionParam(), typeof(PythonFunction)),    // function
                            AstUtils.Constant(name, typeof(string)),                         // name
                            AstUtils.Constant(position),                                     // position
                            _paramsLen                                                       // params list length
                        )
                    );
                }
                if (_dict != null) {
                    // A code test is needed because the kwargs can only conflict
                    // with arguments on the basis of their names
                    _needCodeTest = true;
                    statements.Add(
                        Ast.Call(
                            typeof(PythonOps).GetMethod(nameof(PythonOps.VerifyUnduplicatedByName)),
                            AstUtils.Convert(GetFunctionParam(), typeof(PythonFunction)), // function
                            AstUtils.Constant(name, typeof(string)),                      // name
                            AstUtils.Convert(_dict, typeof(PythonDictionary)),            // params dict
                            AstUtils.Constant(                                            // keywordArg
                                position >= Signature.GetProvidedPositionalArgumentCount(), typeof(bool))
                        )
                    );
                }

                // Return the value of the argument once we know that we don't have
                // a duplicate
                if (statements.Count == 0) {
                    return value;
                }

                statements.Add(value);
                return Ast.Block(statements);
            }

            /// <summary>
            /// Helper function to get a value (which has no default) from either the 
            /// params list or the dictionary (or both).
            /// </summary>
            private Expression ExtractNonDefaultValue(string name) {
                if (_userProvidedParams != null) {
                    // expanded params
                    if (_dict != null) {
                        // expanded params & dict
                        return ExtractFromListOrDictionary(name);
                    } else {
                        return ExtractNextParamsArg();
                    }
                } else if (_dict != null) {
                    // expanded dict
                    return ExtractDictionaryArgument(name);
                }

                // missing argument, no default, no expanded params or dict.
                return null;
            }

            /// <summary>
            /// Helper function to get the specified variable from the dictionary.
            /// </summary>
            private Expression ExtractDictionaryArgument(string name) {
                _needCodeTest = true;

                return Ast.Call(
                    typeof(PythonOps).GetMethod(nameof(PythonOps.ExtractDictionaryArgument)),
                        AstUtils.Convert(GetFunctionParam(), typeof(PythonFunction)),   // function
                        AstUtils.Constant(name, typeof(string)),                        // name
                        AstUtils.Constant(Signature.ArgumentCount),                     // arg count
                        AstUtils.Convert(_dict, typeof(PythonDictionary))               // dictionary
                    );
            }

            /// <summary>
            /// Helper function to extract the variable from defaults, or to call a helper
            /// to check params / kw-dict / defaults to see which one contains the actual value.
            /// </summary>
            private Expression ExtractDefaultValue(string argName, int dfltIndex) {
                if (_dict == null && _userProvidedParams == null) {
                    // we can pull the default directly
                    return Ast.Call(
                        typeof(PythonOps).GetMethod(nameof(PythonOps.FunctionGetDefaultValue)),
                        AstUtils.Convert(GetFunctionParam(), typeof(PythonFunction)),
                        AstUtils.Constant(dfltIndex)
                    );
                }

                // We might have a conflict, check the default last.
                if (_userProvidedParams != null) {
                    EnsureParams();
                }
                _extractedDefault = true;
                return Ast.Call(
                    typeof(PythonOps).GetMethod(nameof(PythonOps.GetFunctionParameterValue)),
                    AstUtils.Convert(GetFunctionParam(), typeof(PythonFunction)),
                    AstUtils.Constant(dfltIndex),
                    AstUtils.Constant(argName, typeof(string)),
                    VariableOrNull(_params, typeof(Queue<object>)),
                    VariableOrNull(_dict, typeof(PythonDictionary))
                );
            }

            private Expression ExtractKeywordOnlyDefault(string name, int index) {
                if (_dict is null) {
                    // we can pull the default directly
                    return Ast.Call(
                        typeof(PythonOps).GetMethod(nameof(PythonOps.FunctionGetKeywordOnlyDefaultValue)),
                        AstUtils.Convert(GetFunctionParam(), typeof(PythonFunction)),
                        AstUtils.Constant(index)
                    );
                }

                _extractedDefault = true;
                return Ast.Call(
                    typeof(PythonOps).GetMethod(nameof(PythonOps.GetFunctionKeywordOnlyParameterValue)),
                    AstUtils.Convert(GetFunctionParam(), typeof(PythonFunction)),
                    AstUtils.Constant(name),
                    VariableOrNull(_dict, typeof(PythonDictionary))
                );
            }

            /// <summary>
            /// Helper function to extract from the params list or dictionary depending upon
            /// which one has an available value.
            /// </summary>
            private Expression ExtractFromListOrDictionary(string name) {
                EnsureParams();

                _needCodeTest = true;

                return Ast.Call(
                    typeof(PythonOps).GetMethod(nameof(PythonOps.ExtractAnyArgument)),
                    AstUtils.Convert(GetFunctionParam(), typeof(PythonFunction)),  // function
                    AstUtils.Constant(name, typeof(string)),                             // name
                    _paramsLen,                                    // arg count
                    _params,                                       // Queue<object>
                    AstUtils.Convert(_dict, typeof(IDictionary))  // dictionary
                );
            }

            private void EnsureParams() {
                if (!_extractedParams) {
                    Debug.Assert(_userProvidedParams != null);
                    MakeParamsCopy(_userProvidedParams);
                    _extractedParams = true;
                }
            }

            /// <summary>
            /// Helper function to extract the next argument from the params list.
            /// </summary>
            private Expression ExtractNextParamsArg() {
                EnsureParams();

                return Ast.Call(
                    typeof(PythonOps).GetMethod(nameof(PythonOps.ExtractParamsArgument)),
                    AstUtils.Convert(GetFunctionParam(), typeof(PythonFunction)),  // function
                    AstUtils.Constant(Signature.ArgumentCount),                   // arg count
                    _params                                        // Queue<object>
                );
            }

            private static Expression VariableOrNull(ParameterExpression var, Type type) {
                if (var != null) {
                    return AstUtils.Convert(
                        var,
                        type
                    );
                }
                return AstUtils.Constant(null, type);
            }

            /// <summary>
            /// Fixes up the argument list for the appropriate target delegate type.
            /// </summary>
            private Expression/*!*/[]/*!*/ GetArgumentsForTargetType(Expression[] exprArgs) {
                Type target = _func.Value.__code__.Target.GetType();
                if (target == typeof(Func<PythonFunction, object[], object>)) {
                    exprArgs = new Expression[] {
                        AstUtils.NewArrayHelper(typeof(object), exprArgs) 
                    };
                }

                return exprArgs;
            }

            /// <summary>
            /// Helper function to get the function argument strongly typed.
            /// </summary>
            private UnaryExpression/*!*/ GetFunctionParam() {
                return Ast.Convert(_func.Expression, typeof(PythonFunction));
            }

            /// <summary>
            /// Called when the user is expanding a dictionary - we copy the user
            /// dictionary and verify that it contains only valid string names.
            /// </summary>
            private DynamicMetaObject/*!*/ MakeDictionaryCopy(DynamicMetaObject/*!*/ userDict) {
                Debug.Assert(_dict == null);

                userDict = userDict.Restrict(userDict.GetLimitType());
                _temps.Add(_dict = Ast.Variable(typeof(PythonDictionary), "$dict"));                

                EnsureInit();

                string methodName;
                if (typeof(PythonDictionary).IsAssignableFrom(userDict.GetLimitType())) {
                    methodName = nameof(PythonOps.CopyAndVerifyPythonDictionary);
                } else if (typeof(IDictionary).IsAssignableFrom(userDict.GetLimitType())) {
                    methodName = nameof(PythonOps.CopyAndVerifyDictionary);
                } else {
                    methodName = nameof(PythonOps.CopyAndVerifyUserMapping);
                }

                _init.Add(
                    Ast.Assign(
                        _dict,
                        Ast.Call(
                            typeof(PythonOps).GetMethod(methodName),
                            GetFunctionParam(),
                            AstUtils.Convert(userDict.Expression, userDict.GetLimitType())
                        )
                    )
                );
                return userDict;
            }

            /// <summary>
            /// Called when the user is expanding a params argument
            /// </summary>
            private void MakeParamsCopy(Expression/*!*/ userList) {
                Debug.Assert(_params == null);

                _temps.Add(_params = Ast.Variable(typeof(Queue<object>), "$list"));
                _temps.Add(_paramsLen = Ast.Variable(typeof(int), "$paramsLen"));

                EnsureInit();

                _init.Add(
                    Ast.Assign(
                        _params,
                        Ast.Call(
                            typeof(PythonOps).GetMethod(nameof(PythonOps.CopyAndVerifyParamsList)),
                            _codeContext ?? AstUtils.Constant(DefaultContext.Default),
                            AstUtils.Convert(GetFunctionParam(), typeof(PythonFunction)),
                            AstUtils.Convert(userList, typeof(object))
                        )
                    )
                );

                _init.Add(
                    Ast.Assign(_paramsLen,
                        Ast.Add(
                            Ast.Property(_params, nameof(Queue<object>.Count)),
                            AstUtils.Constant(Signature.GetProvidedPositionalArgumentCount())
                        )
                    )
                );
            }

            /// <summary>
            /// Called when the user hasn't supplied a dictionary to be expanded but the
            /// function takes a dictionary to be expanded.
            /// </summary>
            private Expression MakeDictionary(Dictionary<string, Expression/*!*/> namedArgs) {
                Debug.Assert(_dict == null);
                _temps.Add(_dict = Ast.Variable(typeof(PythonDictionary), "$dict"));

                Expression dictCreator;
                if (namedArgs != null) {
                    Debug.Assert(namedArgs.Count > 0);

                    Expression[] items = new Expression[namedArgs.Count * 2];
                    int itemIndex = 0;
                    foreach (KeyValuePair<string, Expression> kvp in namedArgs) {
                        items[itemIndex++] = AstUtils.Convert(kvp.Value, typeof(object));
                        items[itemIndex++] = AstUtils.Constant(kvp.Key, typeof(object));
                    }

                    dictCreator = Ast.Assign(
                        _dict,
                        Ast.Call(
                            typeof(PythonOps).GetMethod(nameof(PythonOps.MakeHomogeneousDictFromItems)),
                            Ast.NewArrayInit(typeof(object), items)
                        )
                    );

                } else {
                    dictCreator = Ast.Assign(
                        _dict,
                        Ast.Call(
                            typeof(PythonOps).GetMethod(nameof(PythonOps.MakeEmptyDict))
                        )
                    );
                }

                return dictCreator;
            }

            /// <summary>
            /// Helper function to create the expression for creating the actual tuple passed through.
            /// </summary>
            private static Expression/*!*/ MakeParamsTuple(List<Expression> extraArgs) {
                if (extraArgs != null) {
                    return AstUtils.ComplexCallHelper(
                        typeof(PythonOps).GetMethod(nameof(PythonOps.MakeTuple)),
                        extraArgs.ToArray()
                    );
                }
                return Ast.Call(
                    typeof(PythonOps).GetMethod(nameof(PythonOps.MakeTuple)),
                    Ast.NewArrayInit(typeof(object[]))
                );
            }

            /// <summary>
            /// Creates the code to invoke the target delegate function w/ the specified arguments.
            /// </summary>
            private Expression/*!*/ MakeFunctionInvoke(Expression[] invokeArgs) {
                Type targetType = _func.Value.__code__.Target.GetType();
                MethodInfo method = targetType.GetMethod("Invoke");

                // If calling generator, create the instance of PythonGenerator first
                // and add it into the list of arguments
                invokeArgs = ArrayUtils.Insert(GetFunctionParam(), invokeArgs);
                
                Expression invoke = AstUtils.SimpleCallHelper(
                    Ast.Convert(
                        Ast.Call(
                            _call.SupportsLightThrow() ?
                                typeof(PythonOps).GetMethod(nameof(PythonOps.FunctionGetLightThrowTarget))
                                : typeof(PythonOps).GetMethod(nameof(PythonOps.FunctionGetTarget)),
                            GetFunctionParam()
                        ),
                        targetType
                    ),
                    method,
                    invokeArgs
                );

                if (_paramlessCheck != null) {
                    invoke = Expression.Block(_paramlessCheck, invoke);
                }

                return invoke;
            }

            /// <summary>
            /// Appends the initialization code for the call to the function if any exists.
            /// </summary>
            private Expression/*!*/ AddInitialization(Expression body) {
                if (_init == null) return body;

                List<Expression> res = new List<Expression>(_init);
                res.Add(body);
                return Ast.Block(res);
            }

            private void MakeUnexpectedKeywordError(Dictionary<string, Expression> namedArgs) {
                string name = null;
                foreach (string id in namedArgs.Keys) {
                    name = id;
                    break;
                }

                _error = _call.Throw(
                    Ast.Call(
                        typeof(PythonOps).GetMethod(nameof(PythonOps.UnexpectedKeywordArgumentError)),
                        AstUtils.Convert(GetFunctionParam(), typeof(PythonFunction)),
                        AstUtils.Constant(name, typeof(string))
                    ),
                    typeof(PythonOps)
                );
            }            

            private void EnsureInit() {
                if (_init == null) _init = new List<Expression>();
            }
        }

        #endregion

        #region Operations

        private static DynamicMetaObject/*!*/ MakeCallSignatureRule(DynamicMetaObject self) {
            return new DynamicMetaObject(
                Ast.Call(
                    typeof(PythonOps).GetMethod(nameof(PythonOps.GetFunctionSignature)),
                    AstUtils.Convert(
                        self.Expression,
                        typeof(PythonFunction)
                    )
                ),
                BindingRestrictionsHelpers.GetRuntimeTypeRestriction(self.Expression, typeof(PythonFunction))
            );
        }

        private static DynamicMetaObject MakeIsCallableRule(DynamicMetaObject/*!*/ self) {
            return new DynamicMetaObject(
                AstUtils.Constant(true),
                BindingRestrictionsHelpers.GetRuntimeTypeRestriction(self.Expression, typeof(PythonFunction))
            );
        }

        #endregion

        #region Helpers

        public new PythonFunction/*!*/ Value {
            get {
                return (PythonFunction)base.Value;
            }
        }

        #endregion

        #region IPythonOperable Members

        DynamicMetaObject IPythonOperable.BindOperation(PythonOperationBinder action, DynamicMetaObject[] args) {
            switch (action.Operation) {
                case PythonOperationKind.CallSignatures:
                    return MakeCallSignatureRule(this);
                case PythonOperationKind.IsCallable:
                    return MakeIsCallableRule(this);
            }

            return null;
        }

        #endregion

        #region IInvokableInferable Members

        InferenceResult IInferableInvokable.GetInferredType(Type delegateType, Type parameterType) {
            if (!delegateType.IsSubclassOf(typeof(Delegate))) {
                throw new InvalidOperationException();
            }

            MethodInfo invoke = delegateType.GetMethod("Invoke");
            ParameterInfo[] pis = invoke.GetParameters();
            if (pis.Length == Value.NormalArgumentCount) {
                // our signatures are compatible
                return new InferenceResult(
                    typeof(object),
                    Restrictions.Merge(
                        BindingRestrictions.GetTypeRestriction(
                            Expression,
                            typeof(PythonFunction)
                        ).Merge(
                            BindingRestrictions.GetExpressionRestriction(
                                Expression.Equal(
                                    Expression.Call(
                                        typeof(PythonOps).GetMethod(nameof(PythonOps.FunctionGetCompatibility)),
                                        Expression.Convert(Expression, typeof(PythonFunction))
                                    ),
                                    Expression.Constant(Value.FunctionCompatibility)
                                )
                            )
                        )
                    )
                );

            }

            return null;
        }

        #endregion

        #region IConvertibleMetaObject Members

        bool IConvertibleMetaObject.CanConvertTo(Type/*!*/ type, bool @explicit) {
            return type.IsSubclassOf(typeof(Delegate));
        }

        #endregion
    }
}
