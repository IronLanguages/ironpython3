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

namespace IronPython.Runtime {
    /// <summary>
    /// Represents a sequence which may have been provided as a set of parameters to an indexer.
    /// 
    /// TODO: This should be removed, and all uses of this should go to [SpecialName]object GetItem(..., params object[] keys)
    /// and [SpecialName]void SetItem(..., params object [] keys) or this[params object[]xyz] which is also legal.  
    /// 
    /// currently this exists for backwards compatibility w/ IronPython's "expandable tuples".
    /// </summary>
    public interface IParameterSequence {
        object[] Expand(object initial);
        object this[int index] {
            get;
        }
        int Count {
            get;
        }
    }
}
