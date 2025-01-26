// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

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
