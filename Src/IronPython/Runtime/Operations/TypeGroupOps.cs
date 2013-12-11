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
using System.Text;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Types;
using Microsoft.Scripting;
using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Utils;

namespace IronPython.Runtime.Operations {
    public static class TypeGroupOps {
        public static string __repr__(TypeGroup self) {
            StringBuilder sb = new StringBuilder("<types ");
            bool pastFirstType = false;
            foreach(Type type in self.Types) {
                if (pastFirstType) { 
                    sb.Append(", ");
                }
                PythonType dt = DynamicHelpers.GetPythonTypeFromType(type);
                sb.Append('\'');
                sb.Append(dt.Name);
                sb.Append('\'');
                pastFirstType = true;
            }
            sb.Append(">");

            return sb.ToString();
        }

        /// <summary>
        /// Indexer for generic parameter resolution.  We bind to one of the generic versions
        /// available in this type collision.  A user can also do someType[()] to force to
        /// bind to the non-generic version, but we will always present the non-generic version
        /// when no bindings are available.
        /// </summary>
        [SpecialName]
        public static PythonType GetItem(TypeGroup self, params PythonType[] types) {
            return GetItemHelper(self, types);
        }

        [SpecialName]
        public static object Call(CodeContext/*!*/ context, TypeGroup/*!*/ self, params object[] args) {
            return PythonCalls.Call(
                context,
                DynamicHelpers.GetPythonTypeFromType(self.GetNonGenericType()),
                args ?? ArrayUtils.EmptyObjects
            );
        }

        [SpecialName]
        public static object Call(CodeContext/*!*/ context, TypeGroup/*!*/ self, [ParamDictionary]PythonDictionary kwArgs, params object[] args) {
            return PythonCalls.CallWithKeywordArgs(
                context, 
                DynamicHelpers.GetPythonTypeFromType(self.GetNonGenericType()),
                args ?? ArrayUtils.EmptyObjects,
                kwArgs ?? new PythonDictionary()
            );
        }

        [SpecialName]
        public static PythonType GetItem(TypeGroup self, params object[] types) {
            PythonType[] pythonTypes = new PythonType[types.Length];
            for(int i = 0; i < types.Length; i++) {
                object t = types[i];
                if (t is PythonType) {
                    pythonTypes[i] = (PythonType)t;
                    continue;
                } else if (t is TypeGroup) {
                    TypeGroup typeGroup = t as TypeGroup;
                    Type nonGenericType;
                    if (!typeGroup.TryGetNonGenericType(out nonGenericType)) {
                        throw PythonOps.TypeError("cannot use open generic type {0} as type argument", typeGroup.Name);
                    }
                    pythonTypes[i] = DynamicHelpers.GetPythonTypeFromType(nonGenericType);
                } else {
                    throw PythonOps.TypeErrorForTypeMismatch("type", t);
                }
            }

            return GetItemHelper(self, pythonTypes);
        }

        [SpecialName]
        public static PythonType GetItem(TypeGroup self, PythonTuple tuple) {
            if (tuple.__len__() == 0) {
                return DynamicHelpers.GetPythonTypeFromType(self.GetNonGenericType());
            }

            return GetItem(self, tuple._data);
        }

        private static PythonType GetItemHelper(TypeGroup self, PythonType[] types) {
            TypeTracker genType = self.GetTypeForArity(types.Length);
            if (genType == null) {
                throw new ValueErrorException(String.Format("could not find compatible generic type for {0} type arguments", types.Length));
            }

            Type res = genType.Type;
            if (types.Length != 0) {
                res = res.MakeGenericType(PythonTypeOps.ConvertToTypes(types));
            }

            return DynamicHelpers.GetPythonTypeFromType(res);
        }

    }
}
