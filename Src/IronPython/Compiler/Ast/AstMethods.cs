﻿/* ****************************************************************************
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
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

namespace IronPython.Compiler.Ast {
    static class AstMethods {
        public static readonly MethodInfo IsTrue = GetMethod((Func<object, bool>)PythonOps.IsTrue);
        public static readonly MethodInfo RaiseAssertionError = GetMethod((Action<object>)PythonOps.RaiseAssertionError);
        public static readonly MethodInfo Repr = GetMethod((Func<CodeContext, object, string>)PythonOps.Repr);
        public static readonly MethodInfo WarnDivision = GetMethod((Action<CodeContext, PythonDivisionOptions, object, object>)PythonOps.WarnDivision);
        public static readonly MethodInfo MakeClass = GetMethod((Func<FunctionCode, Func<CodeContext, CodeContext>, CodeContext, string, object[], string, object>)PythonOps.MakeClass);
        public static readonly MethodInfo UnqualifiedExec = GetMethod((Action<CodeContext, object>)PythonOps.UnqualifiedExec);
        public static readonly MethodInfo QualifiedExec = GetMethod((Action<CodeContext, object, PythonDictionary, object>)PythonOps.QualifiedExec);
        public static readonly MethodInfo PrintExpressionValue = GetMethod((Action<CodeContext, object>)PythonOps.PrintExpressionValue);
        public static readonly MethodInfo PrintCommaWithDest = GetMethod((Action<CodeContext, object, object>)PythonOps.PrintCommaWithDest);
        public static readonly MethodInfo PrintWithDest = GetMethod((Action<CodeContext, object, object>)PythonOps.PrintWithDest);
        public static readonly MethodInfo PrintComma = GetMethod((Action<CodeContext, object>)PythonOps.PrintComma);
        public static readonly MethodInfo Print = GetMethod((Action<CodeContext, object>)PythonOps.Print);
        public static readonly MethodInfo ImportWithNames = GetMethod((Func<CodeContext, string, string[], int, object>)PythonOps.ImportWithNames);
        public static readonly MethodInfo ImportFrom = GetMethod((Func<CodeContext, object, string, object>)PythonOps.ImportFrom);
        public static readonly MethodInfo ImportStar = GetMethod((Action<CodeContext, string, int>)PythonOps.ImportStar);
        public static readonly MethodInfo SaveCurrentException = GetMethod((Func<Exception>)PythonOps.SaveCurrentException);
        public static readonly MethodInfo RestoreCurrentException = GetMethod((Action<Exception>)PythonOps.RestoreCurrentException);
        public static readonly MethodInfo MakeGeneratorExpression = GetMethod((Func<object, object, object>)PythonOps.MakeGeneratorExpression);
        public static readonly MethodInfo ListAddForComprehension = GetMethod((Action<List, object>)PythonOps.ListAddForComprehension);
        public static readonly MethodInfo SetAddForComprehension = GetMethod((Action<SetCollection, object>)PythonOps.SetAddForComprehension);
        public static readonly MethodInfo DictAddForComprehension = GetMethod((Action<PythonDictionary, object, object>)PythonOps.DictAddForComprehension);
        public static readonly MethodInfo MakeEmptyListFromCode = GetMethod((Func<List>)PythonOps.MakeEmptyListFromCode);
        public static readonly MethodInfo CheckUninitialized = GetMethod((Func<object, string, object>)PythonOps.CheckUninitialized);
        public static readonly MethodInfo PrintNewlineWithDest = GetMethod((Action<CodeContext, object>)PythonOps.PrintNewlineWithDest);
        public static readonly MethodInfo PrintNewline = GetMethod((Action<CodeContext>)PythonOps.PrintNewline);
        public static readonly MethodInfo PublishModule = GetMethod((Func<CodeContext, string, object>)PythonOps.PublishModule);
        public static readonly MethodInfo RemoveModule = GetMethod((Action<CodeContext, string, object>)PythonOps.RemoveModule);
        public static readonly MethodInfo ModuleStarted = GetMethod((Action<CodeContext, ModuleOptions>)PythonOps.ModuleStarted);
        public static readonly MethodInfo MakeRethrownException = GetMethod((Func<CodeContext, Exception>)PythonOps.MakeRethrownException);
        public static readonly MethodInfo MakeRethrowExceptionWorker = GetMethod((Func<Exception, Exception>)PythonOps.MakeRethrowExceptionWorker);
        public static readonly MethodInfo MakeException = GetMethod((Func<CodeContext, object, object, object, Exception>)PythonOps.MakeException);
        public static readonly MethodInfo MakeSlice = GetMethod((Func<object, object, object, Slice>)PythonOps.MakeSlice);
        public static readonly MethodInfo ExceptionHandled = GetMethod((Action<CodeContext>)PythonOps.ExceptionHandled);
        public static readonly MethodInfo GetExceptionInfoLocal = GetMethod((Func<CodeContext, Exception, PythonTuple>)PythonOps.GetExceptionInfoLocal);
        public static readonly MethodInfo CheckException = GetMethod((Func<CodeContext, object, object, object>)PythonOps.CheckException);
        public static readonly MethodInfo SetCurrentException = GetMethod((Func<CodeContext, Exception, object>)PythonOps.SetCurrentException);
        public static readonly MethodInfo BuildExceptionInfo = GetMethod((Action<CodeContext, Exception>)PythonOps.BuildExceptionInfo);
        public static readonly MethodInfo MakeTuple = GetMethod((Func<object[], PythonTuple>)PythonOps.MakeTuple);
        public static readonly MethodInfo IsNot = GetMethod((Func<object, object, object>)PythonOps.IsNot);
        public static readonly MethodInfo Is = GetMethod((Func<object, object, object>)PythonOps.Is);
        public static readonly MethodInfo ImportTop = GetMethod((Func<CodeContext, string, int, object>)PythonOps.ImportTop);
        public static readonly MethodInfo ImportBottom = GetMethod((Func<CodeContext, string, int, object>)PythonOps.ImportBottom);
        public static readonly MethodInfo MakeList = GetMethod((Func<List>)PythonOps.MakeList);
        public static readonly MethodInfo MakeListNoCopy = GetMethod((Func<object[], List>)PythonOps.MakeListNoCopy);
        public static readonly MethodInfo GetEnumeratorValues = GetMethod((Func<CodeContext, object, int, object>)PythonOps.GetEnumeratorValues);
        public static readonly MethodInfo GetEnumeratorValuesNoComplexSets = GetMethod((Func<CodeContext, object, int, object>)PythonOps.GetEnumeratorValuesNoComplexSets);
        public static readonly MethodInfo GetGlobalContext = GetMethod((Func<CodeContext, CodeContext>)PythonOps.GetGlobalContext);
        public static readonly MethodInfo GetParentContextFromFunction = GetMethod((Func<PythonFunction, CodeContext>)PythonOps.GetParentContextFromFunction);
        public static readonly MethodInfo MakeFunction = GetMethod((Func<CodeContext, FunctionCode, object, object[], object>)PythonOps.MakeFunction);
        public static readonly MethodInfo MakeFunctionDebug = GetMethod((Func<CodeContext/*!*/, FunctionCode, object, object[], Delegate, object>)PythonOps.MakeFunctionDebug);
        public static readonly MethodInfo MakeClosureCell = GetMethod((Func<ClosureCell>)PythonOps.MakeClosureCell);
        public static readonly MethodInfo MakeClosureCellWithValue = GetMethod((Func<object, ClosureCell>)PythonOps.MakeClosureCellWithValue);
        public static readonly MethodInfo LookupName = GetMethod((Func<CodeContext, string, object>)PythonOps.LookupName);
        public static readonly MethodInfo RemoveName = GetMethod((Action<CodeContext, string>)PythonOps.RemoveName);
        public static readonly MethodInfo SetName = GetMethod((Func<CodeContext, string, object, object>)PythonOps.SetName);
        public static readonly MethodInfo KeepAlive = GetMethod((Action<object>)GC.KeepAlive);
        public static readonly MethodInfo MakeDict = GetMethod((Func<int, PythonDictionary>)PythonOps.MakeDict);
        public static readonly MethodInfo MakeEmptyDict = GetMethod((Func<PythonDictionary>)PythonOps.MakeEmptyDict);
        public static readonly MethodInfo MakeDictFromItems = GetMethod((Func<object[], PythonDictionary>)PythonOps.MakeDictFromItems);
        public static readonly MethodInfo MakeConstantDict = GetMethod((Func<object, PythonDictionary>)PythonOps.MakeConstantDict);
        public static readonly MethodInfo MakeSet = GetMethod((Func<object[], SetCollection>)PythonOps.MakeSet);
        public static readonly MethodInfo MakeEmptySet = GetMethod((Func<SetCollection>)PythonOps.MakeEmptySet);
        public static readonly MethodInfo MakeHomogeneousDictFromItems = GetMethod((Func<object[], PythonDictionary>)PythonOps.MakeHomogeneousDictFromItems);
        public static readonly MethodInfo CreateLocalContext = GetMethod((Func<CodeContext, MutableTuple, string[], CodeContext>)PythonOps.CreateLocalContext);
        public static readonly MethodInfo UpdateStackTrace = GetMethod((Action<Exception, CodeContext, FunctionCode, int>)PythonOps.UpdateStackTrace);
        public static readonly MethodInfo ForLoopDispose = GetMethod((Action<KeyValuePair<IEnumerator, IDisposable>>)PythonOps.ForLoopDispose);
        public static readonly MethodInfo GetClosureTupleFromContext = GetMethod((Func<CodeContext, MutableTuple>)PythonOps.GetClosureTupleFromContext);
        public static readonly MethodInfo IsUnicode = GetMethod((Func<object, bool>)PythonOps.IsUnicode);
        public static readonly MethodInfo PushFrame = GetMethod((Func<CodeContext, FunctionCode, List<FunctionStack>>)PythonOps.PushFrame);
        public static readonly MethodInfo FormatUnicode = GetMethod((Func<CodeContext, string, object, string>)PythonOps.FormatUnicode);
        public static readonly MethodInfo FormatString = GetMethod((Func<CodeContext, string, object, string>)PythonOps.FormatString);
        public static readonly MethodInfo GetUnicodeFunction = GetMethod((Func<BuiltinFunction>)PythonOps.GetUnicodeFuntion);
        public static readonly MethodInfo GeneratorCheckThrowableAndReturnSendValue = GetMethod((Func<object, object>)PythonOps.GeneratorCheckThrowableAndReturnSendValue);
        
        private static MethodInfo GetMethod(Delegate x) {
            return x.GetMethodInfo();
        }
    }
}
