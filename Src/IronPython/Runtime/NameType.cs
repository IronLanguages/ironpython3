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
