// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

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
