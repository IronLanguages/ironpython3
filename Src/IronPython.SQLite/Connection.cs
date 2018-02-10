/* ****************************************************************************
 *
 * Copyright (c) Jeff Hardy 2010-2012.
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
using System.Runtime.InteropServices;
using System.Threading;
using Community.CsharpSqlite;
using IronPython.Runtime;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;
using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using sqlite3_stmt = Community.CsharpSqlite.Sqlite3.Vdbe;
using sqlite3_value = Community.CsharpSqlite.Sqlite3.Mem;

namespace IronPython.SQLite
{
    public static partial class PythonSQLite
    {
        [PythonType]
        public class Connection
        {
            public bool autocommit = false;
            public int total_changes { get { return Sqlite3.sqlite3_total_changes(this.db); } }
            
            public int detect_types { get; set; }
            public bool check_same_thread { get; set; }

            private double _timeout;
            public double timeout
            {
                get { return _timeout; }
                set { _timeout = value; Sqlite3.sqlite3_busy_timeout(this.db, (int)(_timeout * 1000)); }
            }

            private string _isolation_level;
            public string isolation_level
            {
                get { return _isolation_level; }
                set { setIsolationLevel(value); }
            }

            public string begin_statement { get; private set; }

            public object row_factory { get; set; }
            public object text_factory { get; set; }

            public IDictionary collations = new PythonDictionary();

            private List<WeakReference> statements = new List<WeakReference>();
            private int created_statements = 0;

            private Dictionary<object, object> function_pinboard = new Dictionary<object, object>();

            internal Sqlite3.sqlite3 db;

            internal bool inTransaction = false;
            internal int thread_ident = Thread.CurrentThread.ManagedThreadId;

            private static readonly Dictionary<object, object> emptyKwargs= new Dictionary<object, object>();

            public PythonType Warning = PythonSQLite.Warning;
            public PythonType Error = PythonSQLite.Error;
            public PythonType InterfaceError = PythonSQLite.InterfaceError;
            public PythonType DataError = PythonSQLite.DataError;
            public PythonType DatabaseError = PythonSQLite.DatabaseError;
            public PythonType OperationalError = PythonSQLite.OperationalError;
            public PythonType InternalError = PythonSQLite.InternalError;
            public PythonType IntegrityError = PythonSQLite.IntegrityError;
            public PythonType ProgrammingError = PythonSQLite.ProgrammingError;
            public PythonType NotSupportedError = PythonSQLite.NotSupportedError;

            private enum AllStatmentsAction
            {
                Reset, Finalize
            }

            public Connection(string database,
                double timeout=0.0,
                int detect_types=0,
                string isolation_level=null,
                bool check_same_thread=true,
                object factory=null,
                int cached_statements=0)
            {
                this.text_factory = typeof(string);

                int rc = Sqlite3.sqlite3_open(database, out this.db);
                if(rc != Sqlite3.SQLITE_OK)
                    throw GetSqliteError(this.db, null);

                setIsolationLevel(isolation_level ?? "");

                this.detect_types = detect_types;
                this.timeout = timeout;
                this.check_same_thread = check_same_thread;
            }

            ~Connection()
            {
                if(this.db != null)
                    Sqlite3.sqlite3_close(this.db);
            }

            [Documentation("Closes the connection.")]
            public void close()
            {
                checkThread();

                doAllStatements(AllStatmentsAction.Finalize);

                if(this.db != null)
                {
                    int rc = Sqlite3.SQLITE_OK, i = 0;
                    
                    do
                    {
                        rc = Sqlite3.sqlite3_close(this.db);
                        if(rc == Sqlite3.SQLITE_BUSY)
                        {
                            GC.Collect();
                            GC.WaitForPendingFinalizers();
                        }
                    } while(rc == Sqlite3.SQLITE_BUSY && i++ < 3);

                    if(rc != Sqlite3.SQLITE_OK)
                        throw GetSqliteError(this.db, null);

                    this.db = null;
                }
            }

            internal void begin()
            {
                sqlite3_stmt statement = null;
                string tail = null;
                int rc = Sqlite3.sqlite3_prepare(this.db, this.begin_statement, -1, ref statement, ref tail);
                if(rc != Sqlite3.SQLITE_OK)
                    throw GetSqliteError(this.db, statement);

                rc = Util.Step(statement);
                if(rc == Sqlite3.SQLITE_DONE)
                    this.inTransaction = true;
                else
                    throw GetSqliteError(this.db, statement);

                rc = Sqlite3.sqlite3_finalize(statement);
                if(rc != Sqlite3.SQLITE_OK)
                    GetSqliteError(this.db, null);
            }

            [Documentation("Commit the current transaction.")]
            public void commit()
            {
                checkThread(); checkConnection();

                if(inTransaction)
                {
                    sqlite3_stmt statement = null;
                    string tail = null;
                    int rc = Sqlite3.sqlite3_prepare(this.db, "COMMIT", -1, ref statement, ref tail);
                    if(rc != Sqlite3.SQLITE_OK)
                        throw GetSqliteError(this.db, null);

                    rc = Util.Step(statement);
                    if(rc == Sqlite3.SQLITE_DONE)
                        this.inTransaction = false;
                    else
                        throw GetSqliteError(this.db, statement);

                    rc = Sqlite3.sqlite3_finalize(statement);
                    if(rc != Sqlite3.SQLITE_OK)
                        GetSqliteError(this.db, null);
                }
            }

            [Documentation("Roll back the current transaction.")]
            public void rollback()
            {
                checkThread(); checkConnection();

                if(inTransaction)
                {
                    doAllStatements(AllStatmentsAction.Reset);

                    sqlite3_stmt statement = null;
                    string tail = null;
                    int rc = Sqlite3.sqlite3_prepare(this.db, "ROLLBACK", -1, ref statement, ref tail);
                    if(rc != Sqlite3.SQLITE_OK)
                        throw GetSqliteError(this.db, null);

                    rc = Util.Step(statement);
                    if(rc == Sqlite3.SQLITE_DONE)
                        this.inTransaction = false;
                    else
                        throw GetSqliteError(this.db, statement);

                    rc = Sqlite3.sqlite3_finalize(statement);
                    if(rc != Sqlite3.SQLITE_OK)
                        GetSqliteError(this.db, null);
                }
            }

            [Documentation("Return a cursor for the connection.")]
            public object cursor(CodeContext context, object factory=null)
            {
                checkThread(); checkConnection();

                object cursor = factory == null ? new Cursor(context, this) : PythonCalls.Call(context, factory, this);

                if(this.row_factory != null)
                    context.LanguageContext.Operations.SetMember(cursor, "row_factory", this.row_factory);

                return cursor;
            }

            [Documentation("Executes a SQL statement. Non-standard.")]
            public object execute(CodeContext context, [ParamDictionary]IDictionary<object, object> kwargs, params object[] args)
            {
                object c = cursor(context, null);
                object execute = context.LanguageContext.Operations.GetMember(c, "execute");
                return PythonCalls.CallWithKeywordArgs(context, execute, args, kwargs);
            }

            [Documentation("Repeatedly executes a SQL statement. Non-standard.")]
            public object executemany(CodeContext context, [ParamDictionary]IDictionary<object, object> kwargs, params object[] args)
            {
                object c = cursor(context, null);
                object executemany = context.LanguageContext.Operations.GetMember(c, "executemany");
                return PythonCalls.CallWithKeywordArgs(context, executemany, args, kwargs);
            }

            [Documentation("Executes a multiple SQL statements at once. Non-standard.")]
            public object executescript(CodeContext context, [ParamDictionary]IDictionary<object, object> kwargs, params object[] args)
            {
                object c = cursor(context, null);
                object executescript = context.LanguageContext.Operations.GetMember(c, "executescript");
                return PythonCalls.CallWithKeywordArgs(context, executescript, args, kwargs);
            }

            public object __call__(string sql)
            {
                dropUnusedStatementReferences();

                Statement statement = new Statement(this, sql);
                this.statements.Add(new WeakReference(statement));
                return statement;
            }

            private void dropUnusedStatementReferences()
            {
                if(this.created_statements++ < 200)
                    return;

                this.created_statements = 0;

                List<WeakReference> new_list = new List<WeakReference>();

                foreach(WeakReference weakref in this.statements)
                    if(weakref.IsAlive)
                        new_list.Add(weakref);

                this.statements = new_list;
            }

            #region User functions

            [Documentation("Creates a new function. Non-standard.")]
            public void create_function(CodeContext context, string name, int narg, object func)
            {
                int rc = Sqlite3.sqlite3_create_function(this.db, 
                    name, narg, 
                    Sqlite3.SQLITE_ANY, 
                    new object[] { context, func }, 
                    callUserFunction, 
                    null, null);
                
                if(rc != Sqlite3.SQLITE_OK)
                    throw MakeOperationalError("Error creating function");
                else
                    this.function_pinboard[func] = null;
            }

            private static void callUserFunction(Sqlite3.sqlite3_context ctx, int argc, sqlite3_value[] argv)
            {
                object[] data = (object[])Sqlite3.sqlite3_user_data(ctx);
                CodeContext context = (CodeContext)data[0];
                object func = data[1];

                object[] args = buildPyParams(context, ctx, argc, argv);

                try
                {
                    object result = PythonCalls.CallWithKeywordArgs(context, func, args, emptyKwargs);
                    setResult(ctx, result);
                }
                catch(Exception)
                {
                    Sqlite3.sqlite3_result_error(ctx, "user-defined function raised exception", -1);
                }
            }

            private static object[] buildPyParams(CodeContext context, Sqlite3.sqlite3_context ctx, int argc, sqlite3_value[] argv)
            {
                object[] args = new object[argc];

                for(int i = 0; i < argc; ++i)
                {
                    sqlite3_value cur_value = argv[i];
                    object cur_py_value = null;

                    switch(Sqlite3.sqlite3_value_type(cur_value))
                    {
                        case Sqlite3.SQLITE_INTEGER:
                            cur_py_value = (int)Sqlite3.sqlite3_value_int64(cur_value);
                            break;

                        case Sqlite3.SQLITE_FLOAT:
                            cur_py_value = Sqlite3.sqlite3_value_double(cur_value);
                            break;

                        case Sqlite3.SQLITE_TEXT:
                            cur_py_value = Sqlite3.sqlite3_value_text(cur_value);
                            break;

                        case Sqlite3.SQLITE_BLOB:
                            byte[] result = Sqlite3.sqlite3_value_blob(cur_value);
                            PythonBuffer buffer = new PythonBuffer(context, result);
                            cur_py_value = buffer;
                            break;

                        case Sqlite3.SQLITE_NULL:
                        default:
                            cur_py_value = null;
                            break;
                    }

                    args[i] = cur_py_value;
                }

                return args;
            }

            private static void setResult(Sqlite3.sqlite3_context ctx, object result)
            {
                if(result == null)
                    Sqlite3.sqlite3_result_null(ctx);
                else if(result is bool)
                    Sqlite3.sqlite3_result_int64(ctx, ((bool)result) ? 1 : 0);
                else if(result is int)
                    Sqlite3.sqlite3_result_int64(ctx, (int)result);
                else if(result is long)
                    Sqlite3.sqlite3_result_int64(ctx, (long)result);
                else if (result is System.Numerics.BigInteger)
                    Sqlite3.sqlite3_result_int64(ctx, (long)(System.Numerics.BigInteger)result);
                else if(result is float)
                    Sqlite3.sqlite3_result_double(ctx, (float)result);
                else if(result is double)
                    Sqlite3.sqlite3_result_double(ctx, (double)result);
                else if(result is string)
                    Sqlite3.sqlite3_result_text(ctx, (string)result, -1, Sqlite3.SQLITE_TRANSIENT);
                else if(result is byte[])
                {
                    byte[] b = (byte[])result;
                    string s = Latin1.GetString(b, 0, b.Length);
                    Sqlite3.sqlite3_result_blob(ctx, s, s.Length, Sqlite3.SQLITE_TRANSIENT);
                }
                else if(result is PythonBuffer)
                {
                    PythonBuffer buffer = (PythonBuffer)result;
                    string s = buffer[new Slice(0, null)].ToString();
                    Sqlite3.sqlite3_result_blob(ctx, s, s.Length, Sqlite3.SQLITE_TRANSIENT);
                }
                else
                {
                    // TODO raise error
                }
            }

            #endregion

            #region User aggregates

            private class UserAggregateThunk
            {
                public UserAggregateThunk(CodeContext context, string name, object aggregate_class)
                {
                    this.context = context;
                    this.aggregate_class = aggregate_class;
                    this.name = name;
                }

                public void stepCallback(Sqlite3.sqlite3_context ctx, int argc, sqlite3_value[] param)
                {
                    if(instance == null)
                    {
                        try
                        {
                            instance = PythonCalls.Call(context, aggregate_class);
                        }
                        catch(Exception)
                        {
                            Sqlite3.sqlite3_result_error(ctx, "user-defined aggregate's '__init__' method raised error", -1);
                            return;
                        }
                    }

                    try
                    {
                        object step = context.LanguageContext.Operations.GetMember(instance, "step");
                        object[] args = buildPyParams(context, ctx, argc, param);

                        PythonCalls.CallWithKeywordArgs(context, step, args, new Dictionary<object, object>());
                    }
                    catch(Exception e)
                    {
                        if(e is MissingMemberException)
                            throw;

                        Sqlite3.sqlite3_result_error(ctx, "user-defined aggregate's 'step' method raised error", -1);
                    }
                }

                public void finalCallback(Sqlite3.sqlite3_context ctx)
                {
                    if(instance == null)
                        return;

                    try
                    {
                        object function_result = context.LanguageContext.Operations.InvokeMember(instance, "finalize");
                        setResult(ctx, function_result);
                    }
                    catch(Exception)
                    {
                        Sqlite3.sqlite3_result_error(ctx, "user-defined aggregate's 'finalize' method raised error", -1);
                    }
                }

                CodeContext context;
                string name;
                object aggregate_class;
                object instance;
            }

            [Documentation("Creates a new aggregate. Non-standard.")]
            public void create_aggregate(CodeContext context, string name, int n_arg, object aggregate_class)
            {
                UserAggregateThunk thunk = new UserAggregateThunk(context, name, aggregate_class);

                int rc = Sqlite3.sqlite3_create_function(this.db,
                    name, n_arg,
                    Sqlite3.SQLITE_ANY,
                    thunk,
                    null,
                    thunk.stepCallback,
                    thunk.finalCallback);

                if(rc != Sqlite3.SQLITE_OK)
                    throw MakeOperationalError("Error creating aggregate");
                else
                    this.function_pinboard[aggregate_class] = null;
            }

            #endregion

            [Documentation("Creates a collation function. Non-standard.")]
            public void create_collation(params object[] args)
            {
                throw new NotImplementedException();
            }

            [Documentation("Sets progress handler callback. Non-standard.")]
            public void set_progress_handler(params object[] args)
            {
                throw new NotImplementedException();
            }

            [Documentation("Sets authorizer callback. Non-standard.")]
            public void set_authorizer(params object[] args)
            {
                throw new NotImplementedException();
            }

            [Documentation("For context manager. Non-standard.")]
            public object __enter__()
            {
                return this;
            }

            [Documentation("For context manager. Non-standard.")]
            public object __exit__(CodeContext context, object exc_type, object exc_value, object exc_tb)
            {
                DynamicOperations ops = context.LanguageContext.Operations;
                if(exc_type == null && exc_value == null && exc_tb == null)
                {
                    object commitfn;
                    if(ops.TryGetMember(this, "commit", out commitfn))
                        ops.Invoke(commitfn);
                    else
                        commit();
                }
                else
                {
                    object rollbackfn;
                    if(ops.TryGetMember(this, "rollback", out rollbackfn))
                        ops.Invoke(rollbackfn);
                    else
                        rollback();
                }

                return false;
            }

            public object iterdump(CodeContext context)
            {
                throw new NotImplementedException("Not supported with C#-sqlite for unknown reasons.");

                //var ops = context.LanguageContext.Operations;

                //PythonModule sqlite3 = Importer.ImportModule(context, context.GlobalScope, "sqlite3", false, -1) as PythonModule;
                //PythonModule dump = Importer.ImportFrom(context, sqlite3, "dump") as PythonModule;
                
                //object _iterdump = ops.GetMember(dump, "_iterdump");
                //object result = ops.Invoke(_iterdump, this);

                //return result;
            }

            internal void checkConnection()
            {
                if(this.db == null)
                    throw MakeProgrammingError("Cannot operate on a closed database.");
            }

            internal void checkThread()
            {
                if(this.check_same_thread)
                    if(this.thread_ident != System.Threading.Thread.CurrentThread.ManagedThreadId)
                        throw MakeProgrammingError("SQLite objects created in a thread can only be used in that same thread." +
                            "The object was created in thread id {0} and this is thread id {1}".Format(
                            this.thread_ident, System.Threading.Thread.CurrentThread.ManagedThreadId));
            }

            internal static void verify(Connection connection)
            {
                verify(connection, false);
            }

            internal static void verify(Connection connection, bool closed)
            {
                if(!closed && (connection == null || connection.db == null))
                    throw MakeProgrammingError("Cannot operate on a closed database.");

                connection.checkThread();
            }

            private void setIsolationLevel(string isolation_level)
            {
                this.begin_statement = null;

                if(isolation_level == null)
                {
                    this._isolation_level = null;
                    this.commit();
                    this.inTransaction = false;
                }
                else
                {
                    this._isolation_level = isolation_level;
                    this.begin_statement = "BEGIN " + isolation_level;
                }
            }

            private void doAllStatements(AllStatmentsAction action)
            {
                foreach(WeakReference weakref in this.statements)
                {
                    if(weakref.IsAlive)
                    {
                        Statement statement = weakref.Target as Statement;

                        if(statement != null)
                        {
                            if(action == AllStatmentsAction.Reset)
                                statement.Reset();
                            else
                                statement.SqliteFinalize();
                        }
                    }
                }
            }
        }
    }
}
