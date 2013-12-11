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
        public static bool __eq__(char self, char other) {
            return self == other;
        }

        [SpecialName]
        public static bool __ne__(char self, char other) {
            return self != other;
        }

        public static int __hash__(char self) {
            return new String(self, 1).GetHashCode();
        }

        [return: MaybeNotImplemented]
        public static object __cmp__(char self, object other) {
            string strOther;

            if (other is char) {
                int diff = self - (char)other;
                return diff > 0 ? 1 : diff < 0 ? -1 : 0;
            } else if ((strOther = other as string) != null && strOther.Length == 1) {
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
            return new string(self, 1);
        }

        [SpecialName, ExplicitConversionMethod]
        public static char ConvertToChar(int value) {
            if (value < 0 || value > Char.MaxValue) throw new OverflowException();

            return (char)value;
        }
    }
}
