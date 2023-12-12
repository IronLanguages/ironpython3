# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from sqlite3.test from StdLib
##

from iptest import is_ironpython, generate_suite, run_test, is_linux

import sqlite3.test.dbapi
import sqlite3.test.dump
import sqlite3.test.factory
import sqlite3.test.hooks
import sqlite3.test.regression
import sqlite3.test.transactions
import sqlite3.test.types
import sqlite3.test.userfunctions

import test.test_sqlite

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(test.test_sqlite, pattern=pattern)

    if is_ironpython:
        failing_tests = [
            sqlite3.test.dbapi.ConnectionTests('CheckInTransaction'),
            sqlite3.test.dbapi.CursorTests('CheckExecuteArgStringWithZeroByte'),
            sqlite3.test.dbapi.CursorTests('CheckExecuteDictMapping_Mapping'),
            sqlite3.test.dbapi.CursorTests('CheckExecuteManyIterator'),
            sqlite3.test.dbapi.CursorTests('CheckExecuteNonIterable'),
            sqlite3.test.dbapi.CursorTests('CheckExecuteParamSequence'),
            sqlite3.test.dbapi.CursorTests('CheckExecuteTooMuchSql'),
            sqlite3.test.dbapi.CursorTests('CheckLastRowIDOnReplace'),
            sqlite3.test.dbapi.ExtensionTests('CheckConnectionExecutescript'),
            sqlite3.test.dbapi.ExtensionTests('CheckCursorExecutescriptAsBytes'),
            sqlite3.test.dbapi.ExtensionTests('CheckScriptStringSql'),
            sqlite3.test.dbapi.ExtensionTests('CheckScriptSyntaxError'),
            sqlite3.test.dbapi.ClosedConTests('CheckClosedCall'),
            sqlite3.test.dbapi.ClosedConTests('CheckClosedCreateAggregate'),
            sqlite3.test.dbapi.ClosedConTests('CheckClosedCreateFunction'),
            sqlite3.test.dbapi.ClosedConTests('CheckClosedSetAuthorizer'),
            sqlite3.test.dbapi.ClosedConTests('CheckClosedSetProgressCallback'),
            sqlite3.test.dbapi.ClosedCurTests('CheckClosed'),
            sqlite3.test.dump.DumpTests('CheckTableDump'),
            sqlite3.test.dump.DumpTests('CheckUnorderableRow'),
            sqlite3.test.factory.CursorFactoryTests('CheckInvalidFactory'),
            sqlite3.test.factory.CursorFactoryTests('CheckIsInstance'),
            sqlite3.test.factory.RowFactoryTestsBackwardsCompat('CheckIsProducedByFactory'),
            sqlite3.test.factory.RowFactoryTests('CheckFakeCursorClass'),
            sqlite3.test.factory.RowFactoryTests('CheckSqliteRowAsSequence'),
            sqlite3.test.factory.RowFactoryTests('CheckSqliteRowIndex'),
            sqlite3.test.factory.RowFactoryTests('CheckSqliteRowSlice'),
            sqlite3.test.factory.TextFactoryTests('CheckString'),
            sqlite3.test.factory.TextFactoryTestsWithEmbeddedZeroBytes('CheckBytearray'),
            sqlite3.test.factory.TextFactoryTestsWithEmbeddedZeroBytes('CheckBytes'),
            sqlite3.test.factory.TextFactoryTestsWithEmbeddedZeroBytes('CheckCustom'),
            sqlite3.test.factory.TextFactoryTestsWithEmbeddedZeroBytes('CheckString'),
            sqlite3.test.hooks.CollationTests('CheckCollationIsUsed'),
            sqlite3.test.hooks.CollationTests('CheckCollationRegisterTwice'),
            sqlite3.test.hooks.CollationTests('CheckCollationReturnsLargeInteger'),
            sqlite3.test.hooks.CollationTests('CheckCreateCollationBadUpper'),
            sqlite3.test.hooks.CollationTests('CheckCreateCollationNotAscii'),
            sqlite3.test.hooks.CollationTests('CheckCreateCollationNotCallable'),
            sqlite3.test.hooks.CollationTests('CheckCreateCollationNotString'),
            sqlite3.test.hooks.CollationTests('CheckDeregisterCollation'),
            sqlite3.test.hooks.ProgressTests('CheckCancelOperation'),
            sqlite3.test.hooks.ProgressTests('CheckClearHandler'),
            sqlite3.test.hooks.ProgressTests('CheckOpcodeCount'),
            sqlite3.test.hooks.ProgressTests('CheckProgressHandlerUsed'),
            sqlite3.test.hooks.TraceCallbackTests('CheckClearTraceCallback'),
            sqlite3.test.hooks.TraceCallbackTests('CheckTraceCallbackUsed'),
            sqlite3.test.hooks.TraceCallbackTests('CheckUnicodeContent'),
            sqlite3.test.regression.RegressionTests('CheckBpo31770'),
            sqlite3.test.regression.RegressionTests('CheckCollation'),
            sqlite3.test.regression.RegressionTests('CheckCommitCursorReset'),
            sqlite3.test.regression.RegressionTests('CheckConnectionCall'),
            sqlite3.test.regression.RegressionTests('CheckConnectionConstructorCallCheck'),
            sqlite3.test.regression.RegressionTests('CheckConvertTimestampMicrosecondPadding'),
            sqlite3.test.regression.RegressionTests('CheckCursorConstructorCallCheck'),
            sqlite3.test.regression.RegressionTests('CheckCursorRegistration'),
            sqlite3.test.regression.RegressionTests('CheckErrorMsgDecodeError'),
            sqlite3.test.regression.RegressionTests('CheckNullCharacter'),
            sqlite3.test.regression.RegressionTests('CheckOnConflictRollback'),
            sqlite3.test.regression.RegressionTests('CheckRecursiveCursorUse'),
            sqlite3.test.regression.RegressionTests('CheckSetIsolationLevel'),
            sqlite3.test.regression.RegressionTests('CheckStrSubclass'),
            sqlite3.test.regression.RegressionTests('CheckTypeMapUsage'),
            sqlite3.test.regression.UnhashableCallbacksTestCase('test_progress_handler'),
            sqlite3.test.regression.UnhashableCallbacksTestCase('test_func'),
            sqlite3.test.regression.UnhashableCallbacksTestCase('test_authorizer'),
            sqlite3.test.regression.UnhashableCallbacksTestCase('test_aggr'),
            sqlite3.test.transactions.TransactionTests('CheckDMLDoesNotAutoCommitBefore'),
            sqlite3.test.transactions.TransactionTests('CheckRollbackCursorConsistency'),
            sqlite3.test.transactions.TransactionalDDL('CheckImmediateTransactionalDDL'),
            sqlite3.test.transactions.TransactionalDDL('CheckTransactionalDDL'),
            sqlite3.test.types.SqliteTypeTests('CheckBlob'),
            sqlite3.test.types.DeclTypesTests('CheckBlob'),
            sqlite3.test.types.ColNamesTests('CheckColName'),
            sqlite3.test.types.ObjectAdaptationTests('CheckCasterIsUsed'),
            sqlite3.test.types.BinaryConverterTests('CheckBinaryInputForConverter'),
            sqlite3.test.types.DateTimeTests('CheckDateTimeSubSeconds'),
            sqlite3.test.types.DateTimeTests('CheckDateTimeSubSecondsFloatingPoint'),
            sqlite3.test.types.DateTimeTests('CheckSqlTimestamp'),
            sqlite3.test.types.DateTimeTests('CheckSqliteDate'),
            sqlite3.test.types.DateTimeTests('CheckSqliteTimestamp'),
            sqlite3.test.userfunctions.FunctionTests('CheckFuncReturnBlob'),
            sqlite3.test.userfunctions.FunctionTests('CheckParamBlob'),
            sqlite3.test.userfunctions.FunctionTests('CheckParamLongLong'),
        ]
        if is_linux:
            failing_tests += [
                sqlite3.test.transactions.TransactionTests('CheckLocking'),
                sqlite3.test.transactions.TransactionTests('CheckRaiseTimeout'),
            ]

        skip_tests = [
            sqlite3.test.dbapi.ConnectionTests('CheckOpenUri'),
            sqlite3.test.factory.ConnectionFactoryTests('CheckIsInstance'),
            sqlite3.test.userfunctions.AggregateTests('CheckAggrCheckAggrSum'),
            sqlite3.test.userfunctions.AggregateTests('CheckAggrCheckParamBlob'),
            sqlite3.test.userfunctions.AggregateTests('CheckAggrCheckParamFloat'),
            sqlite3.test.userfunctions.AggregateTests('CheckAggrCheckParamInt'),
            sqlite3.test.userfunctions.AggregateTests('CheckAggrCheckParamNone'),
            sqlite3.test.userfunctions.AggregateTests('CheckAggrCheckParamStr'),
            sqlite3.test.userfunctions.AggregateTests('CheckAggrCheckParamsInt'),
            sqlite3.test.userfunctions.AggregateTests('CheckAggrErrorOnCreate'),
            sqlite3.test.userfunctions.AggregateTests('CheckAggrExceptionInFinalize'),
            sqlite3.test.userfunctions.AggregateTests('CheckAggrExceptionInInit'),
            sqlite3.test.userfunctions.AggregateTests('CheckAggrExceptionInStep'),
            sqlite3.test.userfunctions.AggregateTests('CheckAggrNoFinalize'),
            sqlite3.test.userfunctions.AggregateTests('CheckAggrNoStep'),
            sqlite3.test.userfunctions.AuthorizerTests('test_column_access'),
            sqlite3.test.userfunctions.AuthorizerTests('test_table_access'),
            sqlite3.test.userfunctions.AuthorizerRaiseExceptionTests('test_column_access'),
            sqlite3.test.userfunctions.AuthorizerRaiseExceptionTests('test_table_access'),
            sqlite3.test.userfunctions.AuthorizerIllegalTypeTests('test_column_access'),
            sqlite3.test.userfunctions.AuthorizerIllegalTypeTests('test_table_access'),
            sqlite3.test.userfunctions.AuthorizerLargeIntegerTests('test_column_access'),
            sqlite3.test.userfunctions.AuthorizerLargeIntegerTests('test_table_access'),
        ]

        return generate_suite(tests, failing_tests, skip_tests)

    else:
        return tests

run_test(__name__)
