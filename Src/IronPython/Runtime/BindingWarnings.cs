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

using System.Linq.Expressions;

using System;
using System.Reflection;
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
                    String.Format("{0}.{1} has been obsoleted.  {2}",
                        NameConverter.GetTypeName(method.DeclaringType),
                        method.Name,
                        os[0].Message
                    )
                );

                return true;
            }

            if (context.PythonOptions.WarnPython30) {
                Python3WarningAttribute[] py3kwarnings = (Python3WarningAttribute[])method.ReflectionInfo.GetCustomAttributes(typeof(Python3WarningAttribute), true);
                if (py3kwarnings.Length > 0) {
                    info = new WarningInfo(
                        PythonExceptions.DeprecationWarning,
                        py3kwarnings[0].Message
                    );

                    return true;
                }
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
