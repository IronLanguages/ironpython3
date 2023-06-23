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

        [StaticExtensionMethod]
        public static object __new__(PythonType cls) => __new__(cls, default(char));

        [StaticExtensionMethod]
        public static object __new__(PythonType cls, ushort value) => __new__(cls, (char)value);

        [StaticExtensionMethod]
        public static object __new__(PythonType cls, char value) {
            if (cls != DynamicHelpers.GetPythonTypeFromType(typeof(char))) {
                throw PythonOps.TypeError("Char.__new__: first argument must be Char type.");
            }
            return value;
        }

        public static string __repr__(char self) => StringOps.__repr__(char.ToString(self));

        public static int __hash__(char self) => char.ToString(self).GetHashCode();

        public static int __index__(char self) => self;

        [SpecialName]
        public static bool LessThan(char x, char y) => x < y;
        [SpecialName]
        public static bool LessThanOrEqual(char x, char y) => x <= y;
        [SpecialName]
        public static bool GreaterThan(char x, char y) => x > y;
        [SpecialName]
        public static bool GreaterThanOrEqual(char x, char y) => x >= y;
        [SpecialName]
        public static bool Equals(char x, char y) => x == y;
        [SpecialName]
        public static bool NotEquals(char x, char y) => x != y;

        [SpecialName]
        public static bool LessThan(char x, [NotNone] string y) => StringOps.LessThan(char.ToString(x), y);
        [SpecialName]
        public static bool LessThanOrEqual(char x, [NotNone] string y) => StringOps.LessThanOrEqual(char.ToString(x), y);
        [SpecialName]
        public static bool GreaterThan(char x, [NotNone] string y) => StringOps.GreaterThan(char.ToString(x), y);
        [SpecialName]
        public static bool GreaterThanOrEqual(char x, [NotNone] string y) => StringOps.GreaterThanOrEqual(char.ToString(x), y);
        [SpecialName]
        public static bool Equals(char x, [NotNone] string y) => StringOps.Equals(char.ToString(x), y);
        [SpecialName]
        public static bool NotEquals(char x, [NotNone] string y) => StringOps.NotEquals(char.ToString(x), y);

        public static bool __contains__(char self, char other) => self == other;

        public static bool __contains__(char self, string? other) => StringOps.__contains__(char.ToString(self), other);

        [SpecialName, ImplicitConversionMethod]
        public static string ConvertToString(char self) => char.ToString(self);

        [SpecialName, ExplicitConversionMethod]
        public static char ConvertToChar(int value) => checked((char)value);
    }
}
