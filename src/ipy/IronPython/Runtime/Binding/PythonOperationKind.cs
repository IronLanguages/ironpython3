using System;
using System.Collections.Generic;
using System.Text;

namespace IronPython.Runtime.Binding {
    /// <summary>
    /// Custom dynamic site kinds for simple sites that just take a fixed set of parameters.
    /// </summary>
    internal enum PythonOperationKind {
        None,
        /// <summary>
        /// Unary operator.
        /// 
        /// Gets various documentation about the object returned as a string
        /// </summary>
        Documentation,
        /// <summary>
        /// Unary operator.
        /// 
        /// Gets information about the type of parameters, returned as a string.
        /// </summary>
        CallSignatures,
        /// <summary>
        /// Unary operator.
        /// 
        /// Checks whether the object is callable or not, returns true if it is.
        /// </summary>
        IsCallable,

        Hash,

        /// <summary>
        /// Binary operator.
        /// 
        /// Checks to see if the instance contains another object.  Returns true or false.
        /// </summary>
        Contains,
        /// <summary>
        /// Unary operator.
        /// 
        /// Returns the number of items stored in the object.
        /// </summary>      
        Length,
        /// <summary>
        /// Binary operator.
        /// 
        /// Compares two instances returning an integer indicating the relationship between them.  May
        /// throw if the object types are uncomparable.
        /// </summary>
        Compare,
        /// <summary>
        /// Binary operator.
        /// 
        /// Returns both the dividend and quotioent of x / y.
        /// </summary>
        DivMod,
        /// <summary>
        /// Unary operator.
        /// 
        /// Get the absolute value of the instance.
        /// </summary>
        AbsoluteValue,
        /// <summary>
        /// Unary operator.
        /// 
        /// Gets the positive value of the instance.
        /// </summary>
        Positive,
        /// <summary>
        /// Unary operator.
        /// 
        /// Negates the instance and return the new value.
        /// </summary>        
        Negate,
        /// <summary>
        /// Unary operator.
        /// 
        /// Returns the ones complement of the instance.
        /// </summary>
        OnesComplement,

        GetItem,
        SetItem,
        DeleteItem,

        /// <summary>
        /// Unary operator.
        /// 
        /// Boolean negation
        /// </summary>
        IsFalse,
        /// <summary>
        /// Unary operator.
        /// 
        /// Negation, returns object
        /// </summary>
        Not,

        /// <summary>
        /// Get enumerator for iteration binder.  Returns a KeyValuePair&lt;IEnumerator, IDisposable&gt;
        /// 
        /// The IEnumerator is used for iteration.  The IDisposable is provided if the object was an
        /// IEnumerable or IEnumerable&lt;T&gt; and is a disposable object.
        /// </summary>
        GetEnumeratorForIteration,

        ///<summary>Operator for performing add</summary>
        Add,
        ///<summary>Operator for performing sub</summary>
        Subtract,
        ///<summary>Operator for performing pow</summary>
        Power,
        ///<summary>Operator for performing mul</summary>
        Multiply,
        ///<summary>Operator for performing matmul</summary>
        MatMult,
        ///<summary>Operator for performing floordiv</summary>
        FloorDivide,
        ///<summary>Operator for performing truediv</summary>
        TrueDivide,
        ///<summary>Operator for performing mod</summary>
        Mod,
        ///<summary>Operator for performing lshift</summary>
        LeftShift,
        ///<summary>Operator for performing rshift</summary>
        RightShift,
        ///<summary>Operator for performing and</summary>
        BitwiseAnd,
        ///<summary>Operator for performing or</summary>
        BitwiseOr,
        ///<summary>Operator for performing xor</summary>
        ExclusiveOr,


        ///<summary>Operator for performing lt</summary>
        LessThan = ExclusiveOr + 1 | Comparison,
        ///<summary>Operator for performing gt</summary>
        GreaterThan = LessThan + 1 | Comparison,
        ///<summary>Operator for performing le</summary>
        LessThanOrEqual = GreaterThan + 1 | Comparison,
        ///<summary>Operator for performing ge</summary>
        GreaterThanOrEqual = LessThanOrEqual + 1 | Comparison,
        ///<summary>Operator for performing eq</summary>
        Equal = GreaterThanOrEqual + 1 | Comparison,
        ///<summary>Operator for performing ne</summary>
        NotEqual = Equal + 1 | Comparison,


        ///<summary>Operator for performing in-place add</summary>
        InPlaceAdd = Add | InPlace,
        ///<summary>Operator for performing in-place sub</summary>
        InPlaceSubtract = Subtract | InPlace,
        ///<summary>Operator for performing in-place pow</summary>
        InPlacePower = Power | InPlace,
        ///<summary>Operator for performing in-place mul</summary>
        InPlaceMultiply = Multiply | InPlace,
        ///<summary>Operator for performing in-place matmul</summary>
        InPlaceMatMult = MatMult | InPlace,
        ///<summary>Operator for performing in-place floordiv</summary>
        InPlaceFloorDivide = FloorDivide | InPlace,
        ///<summary>Operator for performing in-place truediv</summary>
        InPlaceTrueDivide = TrueDivide | InPlace,
        ///<summary>Operator for performing in-place mod</summary>
        InPlaceMod = Mod | InPlace,
        ///<summary>Operator for performing in-place lshift</summary>
        InPlaceLeftShift = LeftShift | InPlace,
        ///<summary>Operator for performing in-place rshift</summary>
        InPlaceRightShift = RightShift | InPlace,
        ///<summary>Operator for performing in-place and</summary>
        InPlaceBitwiseAnd = BitwiseAnd | InPlace,
        ///<summary>Operator for performing in-place or</summary>
        InPlaceBitwiseOr = BitwiseOr | InPlace,
        ///<summary>Operator for performing in-place xor</summary>
        InPlaceExclusiveOr = ExclusiveOr | InPlace,
        ///<summary>Operator for performing reverse add</summary>
        ReverseAdd = Add | Reversed,
        ///<summary>Operator for performing reverse sub</summary>
        ReverseSubtract = Subtract | Reversed,
        ///<summary>Operator for performing reverse pow</summary>
        ReversePower = Power | Reversed,
        ///<summary>Operator for performing reverse mul</summary>
        ReverseMultiply = Multiply | Reversed,
        ///<summary>Operator for performing reverse matmul</summary>
        ReverseMatMult = MatMult | Reversed,
        ///<summary>Operator for performing reverse floordiv</summary>
        ReverseFloorDivide = FloorDivide | Reversed,
        ///<summary>Operator for performing reverse truediv</summary>
        ReverseTrueDivide = TrueDivide | Reversed,
        ///<summary>Operator for performing reverse mod</summary>
        ReverseMod = Mod | Reversed,
        ///<summary>Operator for performing reverse lshift</summary>
        ReverseLeftShift = LeftShift | Reversed,
        ///<summary>Operator for performing reverse rshift</summary>
        ReverseRightShift = RightShift | Reversed,
        ///<summary>Operator for performing reverse and</summary>
        ReverseBitwiseAnd = BitwiseAnd | Reversed,
        ///<summary>Operator for performing reverse or</summary>
        ReverseBitwiseOr = BitwiseOr | Reversed,
        ///<summary>Operator for performing reverse xor</summary>
        ReverseExclusiveOr = ExclusiveOr | Reversed,
        ///<summary>Operator for performing reverse divmod</summary>
        ReverseDivMod = DivMod | Reversed,

        InPlace       = 0x20000000,
        Reversed      = 0x10000000,
        Comparison    = 0x08000000,
    }
}
