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
using Microsoft.Scripting;

namespace IronPython.Runtime.Types {
    /// <summary>
    /// This interface is used for implementing parts of the IronPython type system.  It
    /// is not intended for consumption from user programs.
    /// </summary>
    public interface IPythonObject {
        PythonDictionary Dict {
            get;
        }

        /// <summary>
        /// Thread-safe dictionary set.  Returns the dictionary set or the previous value if already set or 
        /// null if the dictionary set isn't supported.
        /// </summary>
        /// <param name="dict"></param>
        /// <returns></returns>
        PythonDictionary SetDict(PythonDictionary dict);
        /// <summary>
        /// Dictionary replacement.  Returns true if replaced, false if the dictionary set isn't supported.
        /// </summary>
        /// <param name="dict"></param>
        /// <returns></returns>
        bool ReplaceDict(PythonDictionary dict);

        PythonType PythonType {
            get;
        }

        void SetPythonType(PythonType newType);

        object[] GetSlots();
        object[] GetSlotsCreate();
    }
}
