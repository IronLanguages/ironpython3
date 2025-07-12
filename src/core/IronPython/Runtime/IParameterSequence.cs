// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

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
