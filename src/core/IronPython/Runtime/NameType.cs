// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;

namespace IronPython.Runtime {
    [Flags]
    public enum NameType {
        None = 0x0000,
        Python = 0x0001,

        Method = 0x0002,
        Field = 0x0004,
        Property = 0x0008,
        Event = 0x0010,
        Type = 0x0020,
        BaseTypeMask = 0x003e,

        PythonMethod = Method | Python,
        PythonField = Field | Python,
        PythonProperty = Property | Python,
        PythonEvent = Event | Python,
        PythonType = Type | Python,

        ClassMember = 0x0040,
        ClassMethod = ClassMember | PythonMethod,
    }
}
