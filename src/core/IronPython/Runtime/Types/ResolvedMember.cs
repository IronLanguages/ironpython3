// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using Microsoft.Scripting.Actions;

namespace IronPython.Runtime.Types {
    /// <summary>
    /// Couples a MemberGroup and the name which produces the member group together
    /// </summary>
    internal class ResolvedMember {
        public readonly string/*!*/ Name;
        public readonly MemberGroup/*!*/ Member;
        public static readonly ResolvedMember[]/*!*/ Empty = System.Array.Empty<ResolvedMember>();

        public ResolvedMember(string/*!*/ name, MemberGroup/*!*/ member) {
            Name = name;
            Member = member;
        }
    }
}
