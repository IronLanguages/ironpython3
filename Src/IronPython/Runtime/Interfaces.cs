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
        WeakRefTracker GetWeakRef();

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
        IList<object> GetMemberNames(CodeContext context);
    }
}
