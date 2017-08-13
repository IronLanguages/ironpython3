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
using System.Runtime.CompilerServices;
using System.Threading;
using IronPython.Runtime.Binding;
using IronPython.Runtime.Operations;
using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

namespace IronPython.Runtime.Types {
    /// <summary>
    /// Base class for helper which creates instances.  We have two derived types: One for user
    /// defined types which prepends the type before calling, and one for .NET types which
    /// doesn't prepend the type.
    /// </summary>
    abstract class InstanceCreator {
        protected InstanceCreator(PythonType type) {
            Assert.NotNull(type);

            Type = type;
        }

        public static InstanceCreator Make(PythonType type) {
            if (type.IsSystemType) {
                return new SystemInstanceCreator(type);
            }

            return new UserInstanceCreator(type);
        }

        protected PythonType Type { get; }

        internal abstract object CreateInstance(CodeContext/*!*/ context);
        internal abstract object CreateInstance(CodeContext/*!*/ context, object arg0);
        internal abstract object CreateInstance(CodeContext/*!*/ context, object arg0, object arg1);
        internal abstract object CreateInstance(CodeContext/*!*/ context, object arg0, object arg1, object arg2);
        internal abstract object CreateInstance(CodeContext/*!*/ context, params object[] args);
        internal abstract object CreateInstance(CodeContext context, object[] args, string[] names);
    }

    class UserInstanceCreator : InstanceCreator {
        private CallSite<Func<CallSite, CodeContext, BuiltinFunction, PythonType, object[], object>> _ctorSite;
        private CallSite<Func<CallSite, CodeContext, BuiltinFunction, PythonType, object>> _ctorSite0;
        private CallSite<Func<CallSite, CodeContext, BuiltinFunction, PythonType, object, object>> _ctorSite1;
        private CallSite<Func<CallSite, CodeContext, BuiltinFunction, PythonType, object, object, object>> _ctorSite2;
        private CallSite<Func<CallSite, CodeContext, BuiltinFunction, PythonType, object, object, object, object>> _ctorSite3;

        public UserInstanceCreator(PythonType/*!*/ type)
            : base(type) {
        }

        internal override object CreateInstance(CodeContext context) {
            if (_ctorSite0 == null) {
                Interlocked.CompareExchange(
                    ref _ctorSite0,
                    CallSite<Func<CallSite, CodeContext, BuiltinFunction, PythonType, object>>.Create(
                        context.LanguageContext.InvokeOne
                    ),
                    null
                );
            }

            return _ctorSite0.Target(_ctorSite0, context, Type.Ctor, Type);
        }

        internal override object CreateInstance(CodeContext context, object arg0) {
            if (_ctorSite1 == null) {
                Interlocked.CompareExchange(
                    ref _ctorSite1,
                    CallSite<Func<CallSite, CodeContext, BuiltinFunction, PythonType, object, object>>.Create(
                        context.LanguageContext.Invoke(
                            new CallSignature(2)
                        )
                    ),
                    null
                );
            }

            return _ctorSite1.Target(_ctorSite1, context, Type.Ctor, Type, arg0);
        }

        internal override object CreateInstance(CodeContext context, object arg0, object arg1) {
            if (_ctorSite2 == null) {
                Interlocked.CompareExchange(
                    ref _ctorSite2,
                    CallSite<Func<CallSite, CodeContext, BuiltinFunction, PythonType, object, object, object>>.Create(
                        context.LanguageContext.Invoke(
                            new CallSignature(3)
                        )
                    ),
                    null
                );
            }

            return _ctorSite2.Target(_ctorSite2, context, Type.Ctor, Type, arg0, arg1);
        }

        internal override object CreateInstance(CodeContext context, object arg0, object arg1, object arg2) {
            if (_ctorSite3 == null) {
                Interlocked.CompareExchange(
                    ref _ctorSite3,
                    CallSite<Func<CallSite, CodeContext, BuiltinFunction, PythonType, object, object, object, object>>.Create(
                        context.LanguageContext.Invoke(
                            new CallSignature(4)
                        )
                    ),
                    null
                );
            }

            return _ctorSite3.Target(_ctorSite3, context, Type.Ctor, Type, arg0, arg1, arg2);
        }

        internal override object CreateInstance(CodeContext context, params object[] args) {
            if (_ctorSite == null) {
                Interlocked.CompareExchange(
                    ref _ctorSite,
                    CallSite<Func<CallSite, CodeContext, BuiltinFunction, PythonType, object[], object>>.Create(
                        context.LanguageContext.Invoke(
                            new CallSignature(
                                new Argument(ArgumentType.Simple),
                                new Argument(ArgumentType.List)
                            )
                        )
                    ),
                    null
                );
            }

            return _ctorSite.Target(_ctorSite, context, Type.Ctor, Type, args);
        }

        internal override object CreateInstance(CodeContext context, object[] args, string[] names) {
            return PythonOps.CallWithKeywordArgs(context, Type.Ctor, ArrayUtils.Insert(Type, args), names);
        }
    }

    class SystemInstanceCreator : InstanceCreator {
        private CallSite<Func<CallSite, CodeContext, BuiltinFunction, object[], object>> _ctorSite;
        private CallSite<Func<CallSite, CodeContext, BuiltinFunction, object>> _ctorSite0;
        private CallSite<Func<CallSite, CodeContext, BuiltinFunction, object, object>> _ctorSite1;
        private CallSite<Func<CallSite, CodeContext, BuiltinFunction, object, object, object>> _ctorSite2;
        private CallSite<Func<CallSite, CodeContext, BuiltinFunction, object, object, object, object>> _ctorSite3;

        public SystemInstanceCreator(PythonType/*!*/ type)
            : base(type) {
        }

        internal override object CreateInstance(CodeContext context) {
            if (_ctorSite0 == null) {
                Interlocked.CompareExchange(
                    ref _ctorSite0,
                    CallSite<Func<CallSite, CodeContext, BuiltinFunction, object>>.Create(
                        context.LanguageContext.InvokeNone
                    ),
                    null
                );
            }

            return _ctorSite0.Target(_ctorSite0, context, Type.Ctor);
        }

        internal override object CreateInstance(CodeContext context, object arg0) {
            if (_ctorSite1 == null) {
                Interlocked.CompareExchange(
                    ref _ctorSite1,
                    CallSite<Func<CallSite, CodeContext, BuiltinFunction, object, object>>.Create(
                        context.LanguageContext.Invoke(
                            new CallSignature(1)
                        )
                    ),
                    null
                );
            }

            return _ctorSite1.Target(_ctorSite1, context, Type.Ctor, arg0);
        }

        internal override object CreateInstance(CodeContext context, object arg0, object arg1) {
            if (_ctorSite2 == null) {
                Interlocked.CompareExchange(
                    ref _ctorSite2,
                    CallSite<Func<CallSite, CodeContext, BuiltinFunction, object, object, object>>.Create(
                        context.LanguageContext.Invoke(
                            new CallSignature(2)
                        )
                    ),
                    null
                );
            }

            return _ctorSite2.Target(_ctorSite2, context, Type.Ctor, arg0, arg1);
        }

        internal override object CreateInstance(CodeContext context, object arg0, object arg1, object arg2) {
            if (_ctorSite3 == null) {
                Interlocked.CompareExchange(
                    ref _ctorSite3,
                    CallSite<Func<CallSite, CodeContext, BuiltinFunction, object, object, object, object>>.Create(
                        context.LanguageContext.Invoke(
                            new CallSignature(3)
                        )
                    ),
                    null
                );
            }

            return _ctorSite3.Target(_ctorSite3, context, Type.Ctor, arg0, arg1, arg2);
        }

        internal override object CreateInstance(CodeContext context, params object[] args) {
            if (_ctorSite == null) {
                Interlocked.CompareExchange(
                    ref _ctorSite,
                    CallSite<Func<CallSite, CodeContext, BuiltinFunction, object[], object>>.Create(
                        context.LanguageContext.Invoke(
                            new CallSignature(
                                new Argument(ArgumentType.List)
                            )
                        )
                    ),
                    null
                );
            }

            return _ctorSite.Target(_ctorSite, context, Type.Ctor, args);
        }

        internal override object CreateInstance(CodeContext context, object[] args, string[] names) {
            return PythonOps.CallWithKeywordArgs(context, Type.Ctor, args, names);
        }
    }
}
