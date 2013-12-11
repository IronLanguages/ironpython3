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

using System.Diagnostics;
using System.Dynamic;
using Microsoft.Scripting.Actions;

namespace IronPython.Runtime.Types {
    /// <summary>
    /// Couples a MemberGroup and the name which produces the member group together
    /// </summary>
    class ResolvedMember {
        public readonly string/*!*/ Name;
        public readonly MemberGroup/*!*/ Member;
        public static readonly ResolvedMember[]/*!*/ Empty = new ResolvedMember[0];

        public ResolvedMember(string/*!*/ name, MemberGroup/*!*/ member) {
            Debug.Assert(name != null);
            Debug.Assert(member != null);

            Name = name;
            Member = member;
        }
    }
}
