// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;

namespace IronPython.Runtime.Types {
    /// <summary>
    /// Represents an ops-extension which adds a new slot.  The slot can have arbitrary
    /// get/set behavior above and beyond normal .NET methods or properties.  This is
    /// typically in regards to how it processes access from instances or subtypes.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    internal sealed class SlotFieldAttribute : Attribute {
        public SlotFieldAttribute() { }
    }
}
