// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.
//
// Copyright (c) Jeff Hardy 2010-2012.
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Community.CsharpSqlite;
using IronPython.Runtime;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using sqlite3_stmt = Community.CsharpSqlite.Sqlite3.Vdbe;

namespace IronPython.SQLite
{
    public static partial class PythonSQLite
    {
        [PythonType]
        public class Cursor : IEnumerable
        {
            public const string __doc__ = "SQLite database cursor class.";

            private Statement statement;
            private object next_row;
            private bool resultsDone;
            private int last_step_rc;

            private List<object> row_cast_map = new List<object>();

            public PythonTuple description { get; private set; }

            public int rowcount { get; private set; }

            public int? rownumber
            {
                get { return null; } 
            }

            public long? lastrowid { get; private set; }

            public object row_factory { get; set; }

            public int arraysize { get; set; }

            public Connection connection { get; private set; }

            public object callproc(string procname)
            {
                throw PythonSQLite.MakeNotSupportedError();
            }

            private CodeContext context;

            public Cursor(CodeContext context, Connection connection)
            {
                this.context = context;
                this.connection = connection;

                this.arraysize = 1;
                this.rowcount = -1;

                if(this.connection != null)
                    this.connection.checkThread();
            }

            ~Cursor()
            {
                if(this.statement != null)
                    this.statement.Reset();
            }

            [Documentation("Closes the cursor.")]
            public void close()
            {
                connection.checkThread(); connection.checkConnection();

                if(this.statement != null)
                {
                    this.statement.Reset();
                }
            }

            [Documentation("Executes a SQL statement.")]
            public object execute(CodeContext context, object operation, object args=null)
            {
                return queryExecute(context, false, operation, args);
            }

            [Documentation("Repeatedly executes a SQL statement.")]
            public object executemany(CodeContext context, object operation, object args)
            {
                return queryExecute(context, true, operation, args);
            }

            private object queryExecute(CodeContext context, bool multiple, object operation_obj, object args)
            {
                if(!(operation_obj is string))
                    throw CreateThrowable(PythonExceptions.ValueError, "operation parameter must be str or unicode");

                string operation = (string)operation_obj;

                if(string.IsNullOrEmpty(operation))
                    return null;

                int rc;

                connection.checkThread(); connection.checkConnection();

                this.next_row = null;

                IEnumerator parameters_iter = null;
                if(multiple)
                {
                    if(args != null)
                        parameters_iter = PythonOps.CreatePythonEnumerator(args);
                }
                else
                {
                    object[] parameters_list = { args };
                    if(parameters_list[0] == null)
                        parameters_list[0] = new PythonTuple();

                    parameters_iter = parameters_list.GetEnumerator();
                }

                if(this.statement != null)
                    rc = this.statement.Reset();

                this.description = null;
                this.rowcount = -1;

                // TODO: use stmt cache instead ?
                this.statement = (Statement)this.connection.__call__(operation);

                if(this.statement.in_use)
                    this.statement = new Statement(connection, operation);

                this.statement.Reset();
                this.statement.MarkDirty();

                if(!string.IsNullOrEmpty(connection.begin_statement))
                {
                    switch(statement.StatementType)
                    {
                        case StatementType.Update:
                        case StatementType.Insert:
                        case StatementType.Delete:
                        case StatementType.Replace:
                            if(!connection.inTransaction)
                                connection.begin();
                            
                            break;

                        case StatementType.Other:
                            // it's a DDL statement or something similar
                            // we better COMMIT first so it works for all cases
                            if(connection.inTransaction)
                                connection.commit();

                            break;

                        case StatementType.Select:
                            if(multiple)
                                throw MakeProgrammingError("You cannot execute SELECT statements in executemany().");
                            break;

                        default:
                            break;
                    }
                }

                while(true)
                {
                    if(!parameters_iter.MoveNext())
                        break;

                    object parameters = parameters_iter.Current;

                    this.statement.MarkDirty();
                    this.statement.BindParameters(context, parameters);

                    while(true)
                    {
                        rc = this.statement.RawStep();
                        if(rc == Sqlite3.SQLITE_DONE || rc == Sqlite3.SQLITE_ROW)
                            break;

                        rc = this.statement.Reset();
                        if(rc == Sqlite3.SQLITE_SCHEMA)
                        {
                            rc = this.statement.Recompile(context, parameters);
                            if(rc == Sqlite3.SQLITE_OK)
                            {
                                continue;
                            }
                            else
                            {
                                this.statement.Reset();
                                throw GetSqliteError(this.connection.db, null);
                            }
                        }
                        else
                        {
                            this.statement.Reset();
                            throw GetSqliteError(this.connection.db, null);
                        }
                    }

                    if(!buildRowCastMap())
                        throw MakeOperationalError("Error while building row_cast_map");

                    if(rc == Sqlite3.SQLITE_ROW || (rc == Sqlite3.SQLITE_DONE && this.statement.StatementType == StatementType.Select))
                    {
                        if(this.description == null)
                        {
                            int numcols = Sqlite3.sqlite3_column_count(this.statement.st);
                            object[] new_description = new object[numcols];

                            for(int i = 0; i < numcols; ++i)
                            {
                                string name = buildColumnName(Sqlite3.sqlite3_column_name(this.statement.st, i));

                                object descriptor = new object[] {
                                    name,
                                    (object)null,
                                    (int?)null,
                                    (int?)null,
                                    (int?)null,
                                    (int?)null,
                                    (bool?)null
                                };

                                new_description[i] = new PythonTuple(descriptor);
                            }

                            this.description = new PythonTuple(new_description);
                        }
                    }

                    if(rc == Sqlite3.SQLITE_ROW)
                    {
                        if(multiple)
                            throw MakeProgrammingError("executemany() can only execute DML statements.");

                        this.next_row = fetchOneRow(context);
                    }
                    else if(rc == Sqlite3.SQLITE_DONE && !multiple)
                    {
                        this.statement.Reset();
                    }

                    switch(this.statement.StatementType)
                    {
                        case StatementType.Update:
                        case StatementType.Delete:
                        case StatementType.Insert:
                        case StatementType.Replace:
                            if(this.rowcount == -1)
                                this.rowcount = 0;

                            this.rowcount += Sqlite3.sqlite3_changes(this.connection.db);
                            break;
                    }

                    if(!multiple && this.statement.StatementType == StatementType.Insert)
                    {
                        this.lastrowid = Sqlite3.sqlite3_last_insert_rowid(this.connection.db);
                    }
                    else
                    {
                        this.lastrowid = null;
                    }

                    if(multiple)
                        rc = this.statement.Reset();
                }

                return this;
            }

            private string buildColumnName(string colname)
            {
                int n = colname.IndexOf('[');
                return n < 0 ? colname : colname.Substring(0, n).Trim();
            }

            private object fetchOneRow(CodeContext context)
            {
                int numcols = Sqlite3.sqlite3_data_count(this.statement.st);
                object[] row = new object[numcols];
                object converter = null;

                for(int i = 0; i < numcols; ++i)
                {
                    object converted = null;

                    if(this.connection.detect_types != 0)
                    {
                        converter = row_cast_map[i];
                    }
                    else
                    {
                        converter = null;
                    }

                    if(converter != null)
                    {
                        byte[] val = Sqlite3.sqlite3_column_blob(this.statement.st, i);
                        if(val == null)
                        {
                            converted = null;
                        }
                        else
                        {
                            string item = Latin1.GetString(val, 0, val.Length);
                            converted = PythonCalls.Call(context, converter, item);
                        }
                    }
                    else
                    {
                        int coltype = Sqlite3.sqlite3_column_type(this.statement.st, i);

                        switch(coltype)
                        {
                            case Sqlite3.SQLITE_NULL:
                                converted = null;
                                break;

                            case Sqlite3.SQLITE_INTEGER:
                                long l = Sqlite3.sqlite3_column_int64(this.statement.st, i);
                                if(l < int.MinValue || l > int.MaxValue)
                                    converted = l;
                                else
                                    converted = (int)l;

                                break;

                            case Sqlite3.SQLITE_FLOAT:
                                converted = Sqlite3.sqlite3_column_double(this.statement.st, i);
                                break;

                            case Sqlite3.SQLITE_TEXT:
                                converted = Sqlite3.sqlite3_column_text(this.statement.st, i);
                                break;

                            case Sqlite3.SQLITE_BLOB:
                            default:
                                converted = new Bytes(Sqlite3.sqlite3_column_blob(this.statement.st, i) ?? new byte[0]); // TODO: avoid creating a copy
                                break;
                        }
                    }

                    row[i] = converted;
                }

                return new PythonTuple(row);
            }

            public Cursor executescript(string operation)
            {
                connection.checkThread(); connection.checkConnection();

                this.connection.commit();

                sqlite3_stmt statement = null;
                string script = operation;
                bool statement_completed = false;
                while(true)
                {
                    if(Sqlite3.sqlite3_complete(operation) == 0)
                        break;
                    statement_completed = true;

                    int rc = Sqlite3.sqlite3_prepare(this.connection.db,
                                                     operation,
                                                     -1,
                                                     ref statement,
                                                     ref script);

                    if(rc != Sqlite3.SQLITE_OK)
                        throw GetSqliteError(this.connection.db, null);

                    /* execute statement, and ignore results of SELECT statements */
                    rc = Sqlite3.SQLITE_ROW;
                    while(rc == Sqlite3.SQLITE_ROW)
                        rc = Sqlite3.sqlite3_step(statement);

                    if(rc != Sqlite3.SQLITE_DONE)
                    {
                        Sqlite3.sqlite3_finalize(statement);
                        throw GetSqliteError(this.connection.db, null);
                    }

                    rc = Sqlite3.sqlite3_finalize(statement);
                    if(rc != Sqlite3.SQLITE_OK)
                        throw GetSqliteError(this.connection.db, null);
                }

                if(!statement_completed)
                    throw MakeProgrammingError("you did not provide a complete SQL statement");

                return this;
            }

            public object __iter__()
            {
                return this;
            }

            public object __next__(CodeContext context)
            {
                object next_row_tuple, next_row;

                connection.checkThread(); connection.checkConnection();

                if(this.next_row == null)
                {
                    if(this.statement != null)
                    {
                        this.statement.Reset();
                        this.statement = null;
                    }

                    throw new StopIterationException();
                }

                next_row_tuple = this.next_row;
                this.next_row = null;

                if(this.row_factory != null)
                {
                    next_row = PythonCalls.Call(context, this.row_factory, this, next_row_tuple);
                }
                else
                {
                    next_row = next_row_tuple;
                }

                if(this.statement != null)
                {
                    int rc = this.statement.RawStep();
                    if(rc != Sqlite3.SQLITE_DONE && rc != Sqlite3.SQLITE_ROW)
                    {
                        this.statement.Reset();
                        throw GetSqliteError(this.connection.db, this.statement.st);
                    }

                    if(rc == Sqlite3.SQLITE_ROW)
                        this.next_row = fetchOneRow(context);
                }

                return next_row;
            }

            [Documentation("Fetches one row from the resultset.")]
            public object fetchone(CodeContext context)
            {
                try
                {
                    return this.__next__(context);
                }
                catch(StopIterationException)
                {
                    return null;
                }
            }

            public object fetchmany(CodeContext context)
            {
                return fetchmany(context, this.arraysize);
            }

            [Documentation("Fetches several rows from the resultset.")]
            public object fetchmany(CodeContext context, int size)
            {
                PythonList result = new PythonList();
                object item = fetchone(context);
                for(int i = 0; i < size && item != null; ++i, item = fetchone(context))
                    result.Add(item);

                return result;
            }

            [Documentation("Fetches all rows from the resultset.")]
            public object fetchall(CodeContext context)
            {
                PythonList result = new PythonList();
                object item = fetchone(context);
                while(item != null)
                {
                    result.Add(item);
                    item = fetchone(context);
                }

                return result;
            }

            public object nextset()
            {
                return null;
            }

            [Documentation("Required by DB-API. Does nothing in IronPython.Sqlite3.")]
            public void setinputsizes(object sizes) { }

            [Documentation("Required by DB-API. Does nothing in IronPython.Sqlite3.")]
            public void setoutputsize(params object[] args) { }

            private bool buildRowCastMap()
            {
                if(this.connection.detect_types == 0)
                    return true;

                row_cast_map = new List<object>();

                object converter = null;
                for(int i = 0; i < Sqlite3.sqlite3_column_count(this.statement.st); ++i)
                {
                    converter = null;

                    if((this.connection.detect_types & PARSE_COLNAMES) != 0)
                    {
                        string colname = Sqlite3.sqlite3_column_name(this.statement.st, i);
                        if(colname != null)
                        {
                            Regex matchColname = new Regex(@"\[(\w+)\]");
                            Match match = matchColname.Match(colname);
                            if(match.Success)
                            {
                                string key = match.Groups[1].ToString();
                                converter = getConverter(key);
                            }
                        }
                    }

                    if((converter == null) && ((this.connection.detect_types & PARSE_DECLTYPES) != 0))
                    {
                        string decltype = Sqlite3.sqlite3_column_decltype(this.statement.st, i);
                        if(decltype != null)
                        {
                            Regex matchDecltype = new Regex(@"\b(\w+)\b");
                            Match match = matchDecltype.Match(decltype);
                            if(match.Success)
                            {
                                string py_decltype = match.Groups[1].ToString();
                                converter = getConverter(py_decltype);
                            }
                        }
                    }

                    row_cast_map.Add(converter);
                }

                return true;
            }

            private object getConverter(string key)
            {
                object converter;
                return converters.TryGetValue(key.ToUpperInvariant(), out converter) ? converter : null;
            }

            #region IEnumerable Members

            public IEnumerator GetEnumerator()
            {
                PythonList results = new PythonList();
                try
                {
                    while(true)
                        results.append(this.__next__(this.context));
                }
                catch(StopIterationException) { }

                return results.GetEnumerator();
            }

            #endregion
        }
    }
}
