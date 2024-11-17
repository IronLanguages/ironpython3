// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq.Expressions;
using System.Threading;

using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Types;

using Microsoft.Scripting.Actions.Calls;
using Microsoft.Scripting.Utils;

using AstUtils = Microsoft.Scripting.Ast.Utils;

namespace IronPython.Runtime.Binding {
    /// <summary>
    /// Provides support for emitting warnings when built in methods are invoked at runtime.
    /// </summary>
    internal static class BindingWarnings {
        public static bool ShouldWarn(PythonContext/*!*/ context, OverloadInfo/*!*/ method, out WarningInfo info) {
            Assert.NotNull(method);

            ObsoleteAttribute[] os = (ObsoleteAttribute[])method.ReflectionInfo.GetCustomAttributes(typeof(ObsoleteAttribute), true);
            if (os.Length > 0) {
                info = new WarningInfo(
                    PythonExceptions.DeprecationWarning,
                    string.Format("{0}.{1} has been obsoleted.  {2}",
                        NameConverter.GetTypeName(method.DeclaringType),
                        method.Name,
                        os[0].Message
                    )
                );

                return true;
            }

#if FEATURE_APARTMENTSTATE
            // no apartment states on Silverlight
            if (method.DeclaringType == typeof(Thread)) {
                if (method.Name == "Sleep") {
                    info = new WarningInfo(
                        PythonExceptions.RuntimeWarning,
                        "Calling Thread.Sleep on an STA thread doesn't pump messages.  Use Thread.CurrentThread.Join instead.",
                        Expression.Equal(
                            Expression.Call(
                                Expression.Property(
                                    null,
                                    typeof(Thread).GetProperty("CurrentThread")
                                ),
                                typeof(Thread).GetMethod("GetApartmentState")
                            ),
                            AstUtils.Constant(ApartmentState.STA)
                        )
                    );

                    return true;
                }
            }
#endif

            info = null;
            return false;
        }
    }
}
