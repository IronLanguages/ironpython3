using System;
using System.Collections.Generic;
using System.Text;
using IronPython.Runtime.Binding;

namespace IronPython.Runtime.Types {
    /// <summary>
    /// OperatorMapping provides a mapping from DLR operators to their associated .NET methods.
    /// </summary>
    internal class OperatorMapping {
        private static readonly OperatorMapping[] _infos = MakeOperatorTable(); // table of Operators, names, and alt names for looking up methods.

        private readonly PythonOperationKind _operator;
        private readonly string _name;

        private OperatorMapping(PythonOperationKind op, string name, string altName) {
            _operator = op;
            _name = name;
            AlternateName = altName;
        }

        private OperatorMapping(PythonOperationKind op, string name, string altName, Type alternateExpectedType) {
            _operator = op;
            _name = name;
            AlternateName = altName;
            AlternateExpectedType = alternateExpectedType;
        }

        /// <summary>
        /// Given an operator returns the OperatorMapping associated with the operator or null
        /// </summary>
        public static OperatorMapping GetOperatorMapping(PythonOperationKind op) {
            foreach (OperatorMapping info in _infos) {
                if (info._operator == op) return info;
            }
            return null;
        }

        public static OperatorMapping GetOperatorMapping(string name) {
            foreach (OperatorMapping info in _infos) {
                if (info.Name == name || info.AlternateName == name) {
                    return info;
                }
            }
            return null;
        }

        /// <summary>
        /// The operator the OperatorMapping provides info for.
        /// </summary>
        public PythonOperationKind Operator {
            get { return _operator; }
        }

        /// <summary>
        /// The primary method name associated with the method.  This method name is
        /// usally in the form of op_Operator (e.g. op_Addition).
        /// </summary>
        public string Name {
            get { return _name; }
        }

        /// <summary>
        /// Gets the secondary method name associated with the method. This method name is
        /// usually a standard .NET method name with pascal casing (e.g. Add).
        /// </summary>
        public string AlternateName { get; }

        /// <summary>
        /// Gets the return type that must match for the alternate operator to be valid.
        /// This is available alternate operators don't have special names and therefore
        /// could be confused for a normal method which isn't fulfilling the contract.
        /// </summary>
        public Type AlternateExpectedType { get; }

        private static OperatorMapping[] MakeOperatorTable() {
            List<OperatorMapping> res = new List<OperatorMapping>();

            // alternate names from: http://msdn2.microsoft.com/en-us/library/2sk3x8a7(vs.71).aspx
            //   different in:
            //    comparisons all support alternative names, Xor is "ExclusiveOr" not "Xor"

            // unary operators as defined in Partition I Architecture 9.3.1:
            res.Add(new OperatorMapping(PythonOperationKind.Negate, "op_UnaryNegation", "Negate"));         // - (unary)
            res.Add(new OperatorMapping(PythonOperationKind.Positive, "op_UnaryPlus", "Plus"));           // + (unary)
            res.Add(new OperatorMapping(PythonOperationKind.Not, "op_LogicalNot", null));             // !
            //res.Add(new OperatorMapping(PythonOperationKind.AddressOf,           "op_AddressOf",                 null));             // & (unary)
            res.Add(new OperatorMapping(PythonOperationKind.OnesComplement, "op_OnesComplement", "OnesComplement")); // ~
            //res.Add(new OperatorMapping(PythonOperationKind.PointerDereference,  "op_PointerDereference",        null));             // * (unary)

            // binary operators as defined in Partition I Architecture 9.3.2:
            res.Add(new OperatorMapping(PythonOperationKind.Add, "op_Addition", "Add"));            // +
            res.Add(new OperatorMapping(PythonOperationKind.Subtract, "op_Subtraction", "Subtract"));       // -
            res.Add(new OperatorMapping(PythonOperationKind.Multiply, "op_Multiply", "Multiply"));       // *
            res.Add(new OperatorMapping(PythonOperationKind.TrueDivide, "op_Division", "Divide"));         // /
            res.Add(new OperatorMapping(PythonOperationKind.Mod, "op_Modulus", "Mod"));            // %
            res.Add(new OperatorMapping(PythonOperationKind.ExclusiveOr, "op_ExclusiveOr", "ExclusiveOr"));    // ^
            res.Add(new OperatorMapping(PythonOperationKind.BitwiseAnd, "op_BitwiseAnd", "BitwiseAnd"));     // &
            res.Add(new OperatorMapping(PythonOperationKind.BitwiseOr, "op_BitwiseOr", "BitwiseOr"));      // |
            res.Add(new OperatorMapping(PythonOperationKind.LeftShift, "op_LeftShift", "LeftShift"));      // <<
            res.Add(new OperatorMapping(PythonOperationKind.RightShift, "op_RightShift", "RightShift"));     // >>
            res.Add(new OperatorMapping(PythonOperationKind.Equal, "op_Equality", "Equals"));         // ==   
            res.Add(new OperatorMapping(PythonOperationKind.GreaterThan, "op_GreaterThan", "GreaterThan"));    // >
            res.Add(new OperatorMapping(PythonOperationKind.LessThan, "op_LessThan", "LessThan"));       // <
            res.Add(new OperatorMapping(PythonOperationKind.NotEqual, "op_Inequality", "NotEquals"));      // != 
            res.Add(new OperatorMapping(PythonOperationKind.GreaterThanOrEqual, "op_GreaterThanOrEqual", "GreaterThanOrEqual"));        // >=
            res.Add(new OperatorMapping(PythonOperationKind.LessThanOrEqual, "op_LessThanOrEqual", "LessThanOrEqual"));        // <=
            res.Add(new OperatorMapping(PythonOperationKind.InPlaceMultiply, "op_MultiplicationAssignment", "InPlaceMultiply"));       // *=
            res.Add(new OperatorMapping(PythonOperationKind.InPlaceSubtract, "op_SubtractionAssignment", "InPlaceSubtract"));       // -=
            res.Add(new OperatorMapping(PythonOperationKind.InPlaceExclusiveOr, "op_ExclusiveOrAssignment", "InPlaceExclusiveOr"));            // ^=
            res.Add(new OperatorMapping(PythonOperationKind.InPlaceLeftShift, "op_LeftShiftAssignment", "InPlaceLeftShift"));      // <<=
            res.Add(new OperatorMapping(PythonOperationKind.InPlaceRightShift, "op_RightShiftAssignment", "InPlaceRightShift"));     // >>=
            res.Add(new OperatorMapping(PythonOperationKind.InPlaceMod, "op_ModulusAssignment", "InPlaceMod"));            // %=
            res.Add(new OperatorMapping(PythonOperationKind.InPlaceAdd, "op_AdditionAssignment", "InPlaceAdd"));            // += 
            res.Add(new OperatorMapping(PythonOperationKind.InPlaceBitwiseAnd, "op_BitwiseAndAssignment", "InPlaceBitwiseAnd"));     // &=
            res.Add(new OperatorMapping(PythonOperationKind.InPlaceBitwiseOr, "op_BitwiseOrAssignment", "InPlaceBitwiseOr"));      // |=
            res.Add(new OperatorMapping(PythonOperationKind.InPlaceTrueDivide, "op_DivisionAssignment", "InPlaceDivide"));         // /=
            
            // these exist just for TypeInfo to map by name
            res.Add(new OperatorMapping(PythonOperationKind.GetItem, "get_Item", "GetItem"));        // not defined
            res.Add(new OperatorMapping(PythonOperationKind.SetItem, "set_Item", "SetItem"));        // not defined
            res.Add(new OperatorMapping(PythonOperationKind.DeleteItem, "del_Item", "DeleteItem"));        // not defined

            // DLR Extended operators:
            res.Add(new OperatorMapping(PythonOperationKind.CallSignatures, "GetCallSignatures", null));
            res.Add(new OperatorMapping(PythonOperationKind.Documentation, "GetDocumentation", null));
            res.Add(new OperatorMapping(PythonOperationKind.IsCallable, "IsCallable", null));

            return res.ToArray();
        }
    }
}
