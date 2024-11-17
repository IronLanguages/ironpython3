// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.
//
// Copyright (c) Jeff Hardy 2010-2012.
//

using System;
using System.Collections;
using System.Diagnostics;

using Community.CsharpSqlite;

using IronPython.Runtime;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

using sqlite3_stmt = Community.CsharpSqlite.Sqlite3.Vdbe;

namespace IronPython.SQLite {
    internal enum StatementType {
        Unknown,
        Select,
        Insert,
        Update,
        Delete,
        Replace,
        Other
    }

    [DebuggerDisplay("{sql}")]
    internal class Statement {
        private readonly Guid uniqueid;
        private Sqlite3.sqlite3 db;
        internal sqlite3_stmt st;

        private object current = null, nextRow = null;
        private bool started = false;
        private string sql;
        private bool bound = false;
        internal bool in_use = false;

        public string Tail { get; private set; }

        public Statement(PythonSQLite.Connection connection, string operation) {
            this.uniqueid = Guid.NewGuid();

            this.db = connection.db;
            this.sql = operation;

            this.st = null;
            string tail = null;
            if (Sqlite3.sqlite3_prepare(this.db, this.sql, -1, ref this.st, ref tail) != Sqlite3.SQLITE_OK /*TODO: || too much sql */) {
                Sqlite3.sqlite3_finalize(st);
                this.st = null;
                throw PythonSQLite.GetSqliteError(this.db, null);
            }

            this.Tail = tail;
        }

        private Statement(Sqlite3.sqlite3 db, sqlite3_stmt stmt, string operation, string tail) {
            this.uniqueid = Guid.NewGuid();

            this.db = db;
            this.sql = operation;

            this.st = stmt;
            this.Tail = tail;
        }

        ~Statement() {
            if (this.st != null) {
                Sqlite3.sqlite3_finalize(this.st);
            }

            this.st = null;
        }

        private StatementType _type = StatementType.Unknown;
        public StatementType StatementType {
            get {
                if (this._type != StatementType.Unknown)
                    return _type;

                string s = this.sql.TrimStart();

                if (s.StartsWith("select", StringComparison.InvariantCultureIgnoreCase))
                    this._type = StatementType.Select;
                else if (s.StartsWith("insert", StringComparison.InvariantCultureIgnoreCase))
                    this._type = StatementType.Insert;
                else if (s.StartsWith("update", StringComparison.InvariantCultureIgnoreCase))
                    this._type = StatementType.Update;
                else if (s.StartsWith("delete", StringComparison.InvariantCultureIgnoreCase))
                    this._type = StatementType.Delete;
                else if (s.StartsWith("replace", StringComparison.InvariantCultureIgnoreCase))
                    this._type = StatementType.Replace;
                else
                    this._type = StatementType.Other;

                return this._type;
            }
        }

        public void BindParameters(CodeContext context, object parameters) {
            if (bound)
                this.ClearParameters();

            int num_params_needed = Sqlite3.sqlite3_bind_parameter_count(this.st);

            if (parameters == null) {
                if (num_params_needed > 0)
                    throw PythonSQLite.MakeProgrammingError("parameters are required but not specified.");
                else
                    return;
            }

            if (parameters is IDictionary)
                BindParameters(context, (IDictionary)parameters, num_params_needed);
            else if (parameters is IList)
                BindParameters(context, (IList)parameters, num_params_needed);
            else
                throw PythonSQLite.MakeProgrammingError("unknown parameter type");

            bound = true;
        }

        private void BindParameters(CodeContext context, IDictionary args, int num_params_needed) {
            for (int i = 1; i <= num_params_needed; ++i) {
                string binding_name = Sqlite3.sqlite3_bind_parameter_name(this.st, i);
                if (string.IsNullOrEmpty(binding_name))
                    throw PythonSQLite.MakeProgrammingError("Binding {0} has no name, but you supplied a dictionary (which has only names).".Format(i));

                // remove the leading colon
                binding_name = binding_name.Substring(1);

                if (args.Contains(binding_name))
                    BindParameter(context, i, maybeAdapt(context, args[binding_name]));
                else
                    throw PythonSQLite.MakeProgrammingError("You did not supply a value for binding {0}.".Format(i));
            }
        }

        private void BindParameters(CodeContext context, IList args, int num_params_needed) {
            if (num_params_needed != args.Count)
                throw PythonSQLite.MakeProgrammingError("Incorrect number of bindings supplied.");

            for (int i = 0; i < args.Count; ++i) {
                BindParameter(context, i + 1, maybeAdapt(context, args[i]));
            }
        }

        private void BindParameter(CodeContext context, int index, object arg) {
            int rc;
            if (arg == null)
                rc = Sqlite3.sqlite3_bind_null(st, index);
            else if (arg is int)
                rc = Sqlite3.sqlite3_bind_int(st, index, (int)arg);
            else if (arg is bool)
                rc = Sqlite3.sqlite3_bind_int(st, index, (bool)arg ? 1 : 0);
            else if (arg is long)
                rc = Sqlite3.sqlite3_bind_int64(st, index, (long)arg);
            else if (arg is System.Numerics.BigInteger)
                rc = Sqlite3.sqlite3_bind_int64(st, index, (long)((System.Numerics.BigInteger)arg));
            else if (arg is float)
                rc = Sqlite3.sqlite3_bind_double(st, index, (float)arg);
            else if (arg is double)
                rc = Sqlite3.sqlite3_bind_double(st, index, (double)arg);
            else if (arg is string)
                rc = Sqlite3.sqlite3_bind_text(st, index, (string)arg, -1, Sqlite3.SQLITE_TRANSIENT);
            else if (arg is byte[])
                rc = Sqlite3.sqlite3_bind_blob(this.st, index, (byte[])arg, -1, Sqlite3.SQLITE_TRANSIENT);
            else
                throw PythonSQLite.MakeInterfaceError("Unable to bind parameter {0} - unsupported type {1}".Format(index, arg.GetType()));

            if (rc != Sqlite3.SQLITE_OK)
                throw PythonSQLite.MakeInterfaceError("Unable to bind parameter {0}: {1}".Format(index, Sqlite3.sqlite3_errmsg(db)));
        }

        private object maybeAdapt(CodeContext context, object value) {
            return needsAdaptation(context, value) ? adaptValue(context, value) : value;
        }

        private bool needsAdaptation(CodeContext context, object value) {
            // TODO The check for primitive types could probably be cached like pysqlite does
            if (value == null ||
                value is int ||
                value is bool ||
                value is long ||
                value is System.Numerics.BigInteger ||
                value is float ||
                value is double ||
                value is string ||
                value is byte[]) {
                object proto = DynamicHelpers.GetPythonTypeFromType(typeof(PythonSQLite.PrepareProtocol));
                object type = DynamicHelpers.GetPythonType(value);

                object key = new PythonTuple(new[] { type, proto });

                return PythonSQLite.adapters.ContainsKey(key);
            } else {
                return true;
            }
        }

        private object adaptValue(CodeContext context, object value) {
            object proto = DynamicHelpers.GetPythonTypeFromType(typeof(PythonSQLite.PrepareProtocol));
            object type = DynamicHelpers.GetPythonType(value);

            object key = new PythonTuple(new[] { type, proto });

            object adapter;
            if (PythonSQLite.adapters.TryGetValue(key, out adapter)) {
                object adapted = PythonCalls.Call(context, adapter, value);
                return adapted;
            }

            // TODO: Use proto? Any value whatsoever?

            object conform;
            if (context.LanguageContext.Operations.TryGetMember(value, "__conform__", out conform)) {
                object adapted = PythonCalls.Call(context, conform, proto);
                if (adapted != null) {
                    return adapted;
                }
            }

            return value;
        }

        public int RawStep() {
            return Util.Step(st);
        }

        public int SqliteFinalize() {
            int rc = Sqlite3.SQLITE_OK;

            if (this.st != null) {
                rc = Sqlite3.sqlite3_finalize(this.st);
                this.st = null;
            }

            this.in_use = false;

            return rc;
        }

        public int Reset() {
            int rc = Sqlite3.SQLITE_OK;

            if (this.in_use && this.st != null) {
                rc = Sqlite3.sqlite3_reset(this.st);

                if (rc == Sqlite3.SQLITE_OK)
                    this.in_use = false;
            }

            return rc;
        }

        private void ClearParameters() {
            if (Sqlite3.sqlite3_clear_bindings(this.st) != Sqlite3.SQLITE_OK)
                throw PythonSQLite.GetSqliteError(this.db, null);
        }

        internal void MarkDirty() {
            this.in_use = true;
        }

        internal int Recompile(CodeContext context, object parameters) {
            sqlite3_stmt new_st = null;
            string tail = null;

            int rc = Sqlite3.sqlite3_prepare(this.db, this.sql, -1, ref new_st, ref tail);
            if (rc == Sqlite3.SQLITE_OK) {
                Statement new_stmt = new Statement(this.st.db, new_st, this.sql, tail);
                new_stmt.BindParameters(context, parameters);

                Sqlite3.sqlite3_finalize(this.st);
                this.st = new_st;
            }

            return rc;
        }
    }
}

