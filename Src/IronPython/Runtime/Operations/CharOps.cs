// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Runtime.CompilerServices;

using Microsoft.Scripting.Runtime;

using IronPython.Runtime.Types;

namespace IronPython.Runtime.Operations {
    /// <summary>
    /// We override the behavior of equals, compare and hashcode to make
    /// chars seem as much like strings as possible.  In Python there is no
    /// difference between these types.
    /// </summary>

    public static class CharOps {
        public static string/*!*/ __repr__(char self) {
            return char.ToString(self);
        }

        public static bool __eq__(char self, char other) {
            return self == other;
        }

        [SpecialName]
        public static bool __ne__(char self, char other) {
            return self != other;
        }

        public static int __hash__(char self) {
            return char.ToString(self).GetHashCode();
        }

        public static int __index__(char self) {
            return self;
        }

        [return: MaybeNotImplemented]
        public static object __cmp__(char self, object other) {
            if (other is char c) {
                int diff = self - c;
                return diff > 0 ? 1 : diff < 0 ? -1 : 0;
            } else if (other is string strOther && strOther.Length == 1) {
                int diff = self - strOther[0];
                return diff > 0 ? 1 : diff < 0 ? -1 : 0;
            }

            return NotImplementedType.Value;
        }

        public static bool __contains__(char self, char other) {
            return self == other;
        }

        public static bool __contains__(char self, string other) {
            return other.Length == 1 && other[0] == self;
        }

        [SpecialName, ImplicitConversionMethod]
        public static string ConvertToString(char self) {
            return char.ToString(self);
        }

        [SpecialName, ExplicitConversionMethod]
        public static char ConvertToChar(int value) {
            return checked((char)value);
        }
    }
}
