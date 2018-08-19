// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

namespace IronPython.Compiler {
    public enum PythonOperator {
        None,

        // Unary
        Not,
        Pos,
        Invert,
        Negate,

        // Binary

        Add,
        Subtract,
        Multiply,
        TrueDivide,
        Mod,
        BitwiseAnd,
        BitwiseOr,
        Xor,
        LeftShift,
        RightShift,
        Power,
        FloorDivide,

        // Comparisons

        LessThan,
        LessThanOrEqual,
        GreaterThan,
        GreaterThanOrEqual,
        Equal,
        NotEqual,
        In,
        NotIn,
        IsNot,
        Is,


        // Aliases
        ExclusiveOr = Xor,
        Equals = Equal,
        NotEquals = NotEqual,
    }
}
