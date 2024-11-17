// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Dynamic;

namespace IronPython.Runtime.Binding {
    /// <summary>
    /// An interface that is implemented on DynamicMetaObjects.
    /// 
    /// This allows objects to opt-into custom conversions when calling
    /// COM APIs.  The IronPython binders all call this interface before
    /// doing any COM binding.
    /// </summary>
    internal interface IComConvertible {
        DynamicMetaObject GetComMetaObject();
    }
}
