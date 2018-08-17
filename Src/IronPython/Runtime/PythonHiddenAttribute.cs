// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;

namespace IronPython.Runtime {
    /// <summary>
    /// Marks a member as being hidden from Python code.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Field | AttributeTargets.Event | AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class PythonHiddenAttribute : PlatformsAttribute {
        public PythonHiddenAttribute(params PlatformID[] hiddenPlatforms) {
            ValidPlatforms = hiddenPlatforms;
        }

        public PythonHiddenAttribute(PlatformsAttribute.PlatformFamily hiddenPlatformFamily) {
            SetValidPlatforms(hiddenPlatformFamily);
        }

        public static bool IsHidden(MemberInfo m, bool inherit = false) {
            var hasHiddenAttribute = m.IsDefined(typeof(PythonHiddenAttribute), inherit);
            if (hasHiddenAttribute) {
                var attrs = m.GetCustomAttributes(typeof(PythonHiddenAttribute), inherit);
                return ((PythonHiddenAttribute)attrs[0]).IsPlatformValid;
            }
            return false;
        }
    }
}
