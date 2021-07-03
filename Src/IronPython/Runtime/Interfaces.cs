// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Collections;
using System.Collections.Generic;

using Microsoft.Scripting.Runtime;

namespace IronPython.Runtime {

    public interface ICodeFormattable {
        string/*!*/ __repr__(CodeContext/*!*/ context);
    }    

    /// <summary>
    /// Defines the internal interface used for accessing weak references and adding finalizers
    /// to user-defined types.
    /// </summary>
    public interface IWeakReferenceable {
        /// <summary>
        /// Gets the current WeakRefTracker for an object that can be used to
        /// append additional weak references.
        /// </summary>
        WeakRefTracker? GetWeakRef();

        /// <summary>
        /// Attempts to set the WeakRefTracker for an object.  Used on the first
        /// addition of a weak ref tracker to an object.  If the object doesn't
        /// support adding weak references then it returns false.
        /// </summary>
        bool SetWeakRef(WeakRefTracker value);

        /// <summary>
        /// Sets a WeakRefTracker on an object for the purposes of supporting finalization.
        /// All user types (new-style and old-style) support finalization even if they don't
        /// support weak-references, and therefore this function always succeeds.  Note the
        /// slot used to store the WeakRefTracker is still shared between SetWeakRef and 
        /// SetFinalizer if a type supports both.
        /// </summary>
        /// <param name="value"></param>
        void SetFinalizer(WeakRefTracker value);
    }

    /// <summary>
    /// Allow types to implement weakreference tracking by returning a proxy.
    /// 
    /// The proxy can refer to the current Python context, which is the main purpose.
    /// </summary>
    public interface IWeakReferenceableByProxy {
        /// <summary>
        /// 
        /// </summary>
        IWeakReferenceable GetWeakRefProxy(PythonContext context);
    }

    public interface IProxyObject {
        object Target { get; }
    }

    public interface IReversible {
        IEnumerator __reversed__();
    }

    /// <summary>
    /// Provides a list of all the members of an instance.  ie. all the keys in the 
    /// dictionary of the object. Note that it can contain objects that are not strings. 
    /// 
    /// Such keys can be added in IronPython using syntax like:
    ///     obj.__dict__[100] = someOtherObject
    ///     
    /// This Python specific version also supports filtering based upon the show cls 
    /// flag by flowing in the code context.
    /// </summary>
    public interface IPythonMembersList : IMembersList {
        IList<object?> GetMemberNames(CodeContext context);
    }
}
