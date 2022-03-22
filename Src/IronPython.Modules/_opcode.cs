// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Text;

using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

[assembly: PythonModule("_opcode", typeof(IronPython.Modules.PythonOpcode))]
namespace IronPython.Modules {
    public static class PythonOpcode {

        /* Instruction opcodes for compiled code */
        private const int POP_TOP = 1;
        private const int ROT_TWO = 2;
        private const int ROT_THREE = 3;
        private const int DUP_TOP = 4;
        private const int DUP_TOP_TWO = 5;
        private const int ROT_FOUR = 6;
        private const int NOP = 9;
        private const int UNARY_POSITIVE = 10;
        private const int UNARY_NEGATIVE = 11;
        private const int UNARY_NOT = 12;
        private const int UNARY_INVERT = 15;
        private const int BINARY_MATRIX_MULTIPLY = 16;
        private const int INPLACE_MATRIX_MULTIPLY = 17;
        private const int BINARY_POWER = 19;
        private const int BINARY_MULTIPLY = 20;
        private const int BINARY_MODULO = 22;
        private const int BINARY_ADD = 23;
        private const int BINARY_SUBTRACT = 24;
        private const int BINARY_SUBSCR = 25;
        private const int BINARY_FLOOR_DIVIDE = 26;
        private const int BINARY_TRUE_DIVIDE = 27;
        private const int INPLACE_FLOOR_DIVIDE = 28;
        private const int INPLACE_TRUE_DIVIDE = 29;
        private const int GET_AITER = 50;
        private const int GET_ANEXT = 51;
        private const int BEFORE_ASYNC_WITH = 52;
        private const int BEGIN_FINALLY = 53;
        private const int END_ASYNC_FOR = 54;
        private const int INPLACE_ADD = 55;
        private const int INPLACE_SUBTRACT = 56;
        private const int INPLACE_MULTIPLY = 57;
        private const int INPLACE_MODULO = 59;
        private const int STORE_SUBSCR = 60;
        private const int DELETE_SUBSCR = 61;
        private const int BINARY_LSHIFT = 62;
        private const int BINARY_RSHIFT = 63;
        private const int BINARY_AND = 64;
        private const int BINARY_XOR = 65;
        private const int BINARY_OR = 66;
        private const int INPLACE_POWER = 67;
        private const int GET_ITER = 68;
        private const int GET_YIELD_FROM_ITER = 69;
        private const int PRINT_EXPR = 70;
        private const int LOAD_BUILD_CLASS = 71;
        private const int YIELD_FROM = 72;
        private const int GET_AWAITABLE = 73;
        private const int INPLACE_LSHIFT = 75;
        private const int INPLACE_RSHIFT = 76;
        private const int INPLACE_AND = 77;
        private const int INPLACE_XOR = 78;
        private const int INPLACE_OR = 79;
        private const int WITH_CLEANUP_START = 81;
        private const int WITH_CLEANUP_FINISH = 82;
        private const int RETURN_VALUE = 83;
        private const int IMPORT_STAR = 84;
        private const int SETUP_ANNOTATIONS = 85;
        private const int YIELD_VALUE = 86;
        private const int POP_BLOCK = 87;
        private const int END_FINALLY = 88;
        private const int POP_EXCEPT = 89;
        private const int HAVE_ARGUMENT = 90;
        private const int STORE_NAME = 90;
        private const int DELETE_NAME = 91;
        private const int UNPACK_SEQUENCE = 92;
        private const int FOR_ITER = 93;
        private const int UNPACK_EX = 94;
        private const int STORE_ATTR = 95;
        private const int DELETE_ATTR = 96;
        private const int STORE_GLOBAL = 97;
        private const int DELETE_GLOBAL = 98;
        private const int LOAD_CONST = 100;
        private const int LOAD_NAME = 101;
        private const int BUILD_TUPLE = 102;
        private const int BUILD_LIST = 103;
        private const int BUILD_SET = 104;
        private const int BUILD_MAP = 105;
        private const int LOAD_ATTR = 106;
        private const int COMPARE_OP = 107;
        private const int IMPORT_NAME = 108;
        private const int IMPORT_FROM = 109;
        private const int JUMP_FORWARD = 110;
        private const int JUMP_IF_FALSE_OR_POP = 111;
        private const int JUMP_IF_TRUE_OR_POP = 112;
        private const int JUMP_ABSOLUTE = 113;
        private const int POP_JUMP_IF_FALSE = 114;
        private const int POP_JUMP_IF_TRUE = 115;
        private const int LOAD_GLOBAL = 116;
        private const int SETUP_FINALLY = 122;
        private const int LOAD_FAST = 124;
        private const int STORE_FAST = 125;
        private const int DELETE_FAST = 126;
        private const int RAISE_VARARGS = 130;
        private const int CALL_FUNCTION = 131;
        private const int MAKE_FUNCTION = 132;
        private const int BUILD_SLICE = 133;
        private const int LOAD_CLOSURE = 135;
        private const int LOAD_DEREF = 136;
        private const int STORE_DEREF = 137;
        private const int DELETE_DEREF = 138;
        private const int CALL_FUNCTION_KW = 141;
        private const int CALL_FUNCTION_EX = 142;
        private const int SETUP_WITH = 143;
        private const int EXTENDED_ARG = 144;
        private const int LIST_APPEND = 145;
        private const int SET_ADD = 146;
        private const int MAP_ADD = 147;
        private const int LOAD_CLASSDEREF = 148;
        private const int BUILD_LIST_UNPACK = 149;
        private const int BUILD_MAP_UNPACK = 150;
        private const int BUILD_MAP_UNPACK_WITH_CALL = 151;
        private const int BUILD_TUPLE_UNPACK = 152;
        private const int BUILD_SET_UNPACK = 153;
        private const int SETUP_ASYNC_WITH = 154;
        private const int FORMAT_VALUE = 155;
        private const int BUILD_CONST_KEY_MAP = 156;
        private const int BUILD_STRING = 157;
        private const int BUILD_TUPLE_UNPACK_WITH_CALL = 158;
        private const int LOAD_METHOD = 160;
        private const int CALL_METHOD = 161;
        private const int CALL_FINALLY = 162;
        private const int POP_FINALLY = 163;


        private static readonly int PY_INVALID_STACK_EFFECT = int.MaxValue;


        /* Masks and values used by FORMAT_VALUE opcode. */
        private const int FVC_MASK = 0x3;
        private const int FVC_NONE = 0x0;
        private const int FVC_STR = 0x1;
        private const int FVC_REPR = 0x2;
        private const int FVC_ASCII = 0x3;
        private const int FVS_MASK = 0x4;
        private const int FVS_HAVE_SPEC = 0x4;

        public static int stack_effect(CodeContext context, int opcode, object oparg=null) {
            int ioparg = 0;

            if (opcode >= HAVE_ARGUMENT) {
                if (oparg == null) {
                    throw PythonOps.ValueError("stack_effect: opcode requires oparg but oparg was not specified");
                }

                if (!Converter.TryConvertToIndex(oparg, out ioparg)) { // supported since CPython 3.8
                    ioparg = Converter.ImplicitConvertToInt32(oparg) ?? // warning since CPython 3.8, unsupported in 3.10
                        throw PythonOps.TypeError($"an integer is required (got type {PythonOps.GetPythonTypeName(oparg)})");
                }
            } else if (oparg != null) {
                throw PythonOps.ValueError("stack_effect: opcode does not permit oparg but oparg was specified");
            }

            int effect = stack_effect(opcode, ioparg);
            if (effect == PY_INVALID_STACK_EFFECT) {
                throw PythonOps.ValueError("invalid opcode or oparg");
            }
            return effect;
        }

        private static int stack_effect(int opcode, int oparg, int jump = -1) {
            switch (opcode) {
                case NOP:
                case EXTENDED_ARG:
                    return 0;

                /* Stack manipulation */
                case POP_TOP:
                    return -1;
                case ROT_TWO:
                case ROT_THREE:
                case ROT_FOUR:
                    return 0;
                case DUP_TOP:
                    return 1;
                case DUP_TOP_TWO:
                    return 2;

                /* Unary operators */
                case UNARY_POSITIVE:
                case UNARY_NEGATIVE:
                case UNARY_NOT:
                case UNARY_INVERT:
                    return 0;

                case SET_ADD:
                case LIST_APPEND:
                    return -1;
                case MAP_ADD:
                    return -2;

                /* Binary operators */
                case BINARY_POWER:
                case BINARY_MULTIPLY:
                case BINARY_MATRIX_MULTIPLY:
                case BINARY_MODULO:
                case BINARY_ADD:
                case BINARY_SUBTRACT:
                case BINARY_SUBSCR:
                case BINARY_FLOOR_DIVIDE:
                case BINARY_TRUE_DIVIDE:
                    return -1;
                case INPLACE_FLOOR_DIVIDE:
                case INPLACE_TRUE_DIVIDE:
                    return -1;

                case INPLACE_ADD:
                case INPLACE_SUBTRACT:
                case INPLACE_MULTIPLY:
                case INPLACE_MATRIX_MULTIPLY:
                case INPLACE_MODULO:
                    return -1;
                case STORE_SUBSCR:
                    return -3;
                case DELETE_SUBSCR:
                    return -2;

                case BINARY_LSHIFT:
                case BINARY_RSHIFT:
                case BINARY_AND:
                case BINARY_XOR:
                case BINARY_OR:
                    return -1;
                case INPLACE_POWER:
                    return -1;
                case GET_ITER:
                    return 0;

                case PRINT_EXPR:
                    return -1;
                case LOAD_BUILD_CLASS:
                    return 1;
                case INPLACE_LSHIFT:
                case INPLACE_RSHIFT:
                case INPLACE_AND:
                case INPLACE_XOR:
                case INPLACE_OR:
                    return -1;

                case SETUP_WITH:
                    /* 1 in the normal flow.
                     * Restore the stack position and push 6 values before jumping to
                     * the handler if an exception be raised. */
                    return jump != 0 ? 6 : 1;
                case WITH_CLEANUP_START:
                    return 2; /* or 1, depending on TOS */
                case WITH_CLEANUP_FINISH:
                    /* Pop a variable number of values pushed by WITH_CLEANUP_START
                     * + __exit__ or __aexit__. */
                    return -3;
                case RETURN_VALUE:
                    return -1;
                case IMPORT_STAR:
                    return -1;
                case SETUP_ANNOTATIONS:
                    return 0;
                case YIELD_VALUE:
                    return 0;
                case YIELD_FROM:
                    return -1;
                case POP_BLOCK:
                    return 0;
                case POP_EXCEPT:
                    return -3;
                case END_FINALLY:
                case POP_FINALLY:
                    /* Pop 6 values when an exception was raised. */
                    return -6;

                case STORE_NAME:
                    return -1;
                case DELETE_NAME:
                    return 0;
                case UNPACK_SEQUENCE:
                    return oparg - 1;
                case UNPACK_EX:
                    return (oparg & 0xFF) + (oparg >> 8);
                case FOR_ITER:
                    /* -1 at end of iterator, 1 if continue iterating. */
                    return jump > 0 ? -1 : 1;

                case STORE_ATTR:
                    return -2;
                case DELETE_ATTR:
                    return -1;
                case STORE_GLOBAL:
                    return -1;
                case DELETE_GLOBAL:
                    return 0;
                case LOAD_CONST:
                    return 1;
                case LOAD_NAME:
                    return 1;
                case BUILD_TUPLE:
                case BUILD_LIST:
                case BUILD_SET:
                case BUILD_STRING:
                    return 1 - oparg;
                case BUILD_LIST_UNPACK:
                case BUILD_TUPLE_UNPACK:
                case BUILD_TUPLE_UNPACK_WITH_CALL:
                case BUILD_SET_UNPACK:
                case BUILD_MAP_UNPACK:
                case BUILD_MAP_UNPACK_WITH_CALL:
                    return 1 - oparg;
                case BUILD_MAP:
                    return 1 - 2 * oparg;
                case BUILD_CONST_KEY_MAP:
                    return -oparg;
                case LOAD_ATTR:
                    return 0;
                case COMPARE_OP:
                    return -1;
                case IMPORT_NAME:
                    return -1;
                case IMPORT_FROM:
                    return 1;

                /* Jumps */
                case JUMP_FORWARD:
                case JUMP_ABSOLUTE:
                    return 0;

                case JUMP_IF_TRUE_OR_POP:
                case JUMP_IF_FALSE_OR_POP:
                    return jump != 0 ? 0 : -1;

                case POP_JUMP_IF_FALSE:
                case POP_JUMP_IF_TRUE:
                    return -1;

                case LOAD_GLOBAL:
                    return 1;

                /* Exception handling */
                case SETUP_FINALLY:
                    /* 0 in the normal flow.
                     * Restore the stack position and push 6 values before jumping to
                     * the handler if an exception be raised. */
                    return jump != 0 ? 6 : 0;
                case BEGIN_FINALLY:
                    /* Actually pushes 1 value, but count 6 for balancing with
                     * END_FINALLY and POP_FINALLY.
                     * This is the main reason of using this opcode instead of
                     * "LOAD_CONST None". */
                    return 6;
                case CALL_FINALLY:
                    return jump != 0 ? 1 : 0;

                case LOAD_FAST:
                    return 1;
                case STORE_FAST:
                    return -1;
                case DELETE_FAST:
                    return 0;

                case RAISE_VARARGS:
                    return -oparg;

                /* Functions and calls */
                case CALL_FUNCTION:
                    return -oparg;
                case CALL_METHOD:
                    return -oparg - 1;
                case CALL_FUNCTION_KW:
                    return -oparg - 1;
                case CALL_FUNCTION_EX:
                    return -1 - (((oparg & 0x01) != 0) ? 1 : 0);
                case MAKE_FUNCTION:
                    return -1 - (((oparg & 0x01) != 0) ? 1 : 0) - (((oparg & 0x02) != 0) ? 1 : 0) -
                        (((oparg & 0x04) != 0) ? 1 : 0) - (((oparg & 0x08) != 0) ? 1 : 0);
                case BUILD_SLICE:
                    if (oparg == 3)
                        return -2;
                    else
                        return -1;

                /* Closures */
                case LOAD_CLOSURE:
                    return 1;
                case LOAD_DEREF:
                case LOAD_CLASSDEREF:
                    return 1;
                case STORE_DEREF:
                    return -1;
                case DELETE_DEREF:
                    return 0;

                /* Iterators and generators */
                case GET_AWAITABLE:
                    return 0;
                case SETUP_ASYNC_WITH:
                    /* 0 in the normal flow.
                     * Restore the stack position to the position before the result
                     * of __aenter__ and push 6 values before jumping to the handler
                     * if an exception be raised. */
                    return jump != 0 ? -1 + 6 : 0;
                case BEFORE_ASYNC_WITH:
                    return 1;
                case GET_AITER:
                    return 0;
                case GET_ANEXT:
                    return 1;
                case GET_YIELD_FROM_ITER:
                    return 0;
                case END_ASYNC_FOR:
                    return -7;
                case FORMAT_VALUE:
                    /* If there's a fmt_spec on the stack, we go from 2->1,
                       else 1->1. */
                    return (oparg & FVS_MASK) == FVS_HAVE_SPEC ? -1 : 0;
                case LOAD_METHOD:
                    return 1;
                default:
                    return PY_INVALID_STACK_EFFECT;
            }
        }
    }
}
