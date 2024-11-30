// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.
//
// Copyright (c) Jeff Hardy 2010-2012.
//

using System;
using Community.CsharpSqlite;
using IronPython.Runtime;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

namespace IronPython.SQLite
{
    public static partial class PythonSQLite
    {
        private static void InitModuleExceptions(PythonContext context, PythonDictionary dict)
        {
            Warning = context.EnsureModuleException("sqlite.Warning", PythonExceptions.Warning, dict, "Warning", "_sqlite3");
            Error = context.EnsureModuleException("sqlite.Error", PythonExceptions.Exception, dict, "Error", "_sqlite3");
            InterfaceError = context.EnsureModuleException("sqlite.InterfaceError", Error, dict, "InterfaceError", "_sqlite3");
            DatabaseError = context.EnsureModuleException("sqlite.DatabaseError", Error, dict, "DatabaseError", "_sqlite3");
            DataError = context.EnsureModuleException("sqlite.DataError", DatabaseError, dict, "DataError", "_sqlite3");
            OperationalError = context.EnsureModuleException("sqlite.OperationalError", DatabaseError, dict, "OperationalError", "_sqlite3");
            IntegrityError = context.EnsureModuleException("sqlite.IntegrityError", DatabaseError, dict, "IntegrityError", "_sqlite3");
            InternalError = context.EnsureModuleException("sqlite.InternalError", DatabaseError, dict, "InternalError", "_sqlite3");
            ProgrammingError = context.EnsureModuleException("sqlite.ProgrammingError", DatabaseError, dict, "ProgrammingError", "_sqlite3");
            NotSupportedError = context.EnsureModuleException("sqlite.NotSupportedError", DatabaseError, dict, "NotSupportedError", "_sqlite3");
        }

        public static PythonType Warning;
        internal static Exception MakeWarning(params object[] args)
        {
            return CreateThrowable(Warning, args);
        }

        public static PythonType Error;
        internal static Exception MakeError(params object[] args)
        {
            return CreateThrowable(Error, args);
        }

        public static PythonType InterfaceError;
        internal static Exception MakeInterfaceError(params object[] args)
        {
            return CreateThrowable(InterfaceError, args);
        }

        public static PythonType DatabaseError;
        internal static Exception MakeDatabaseError(params object[] args)
        {
            return CreateThrowable(DatabaseError, args);
        }

        public static PythonType DataError;
        internal static Exception MakeDataError(params object[] args)
        {
            return CreateThrowable(DataError, args);
        }

        public static PythonType OperationalError;
        internal static Exception MakeOperationalError(params object[] args)
        {
            return CreateThrowable(OperationalError, args);
        }

        public static PythonType IntegrityError;
        internal static Exception MakeIntegrityError(params object[] args)
        {
            return CreateThrowable(IntegrityError, args);
        }

        public static PythonType InternalError;
        internal static Exception MakeInternalError(params object[] args)
        {
            return CreateThrowable(InternalError, args);
        }

        public static PythonType ProgrammingError;
        internal static Exception MakeProgrammingError(params object[] args)
        {
            return CreateThrowable(ProgrammingError, args);
        }

        public static PythonType NotSupportedError;
        internal static Exception MakeNotSupportedError(params object[] args)
        {
            return CreateThrowable(NotSupportedError, args);
        }

        internal static Exception GetSqliteError(Sqlite3.sqlite3 db, Sqlite3.Vdbe st)
        {
            /* SQLite often doesn't report anything useful, unless you reset the statement first */
            if(st != null)
            {
                Sqlite3.sqlite3_reset(st);
            }

            int errorcode = Sqlite3.sqlite3_errcode(db);
            string errmsg = Sqlite3.sqlite3_errmsg(db);

            switch(errorcode)
            {
                case SQLITE_OK:
                    return null;

                case Sqlite3.SQLITE_INTERNAL:
                case Sqlite3.SQLITE_NOTFOUND:
                    return MakeInternalError(errmsg);

                case Sqlite3.SQLITE_NOMEM:
                    return new OutOfMemoryException();

                case Sqlite3.SQLITE_ERROR:
                case Sqlite3.SQLITE_PERM:
                case Sqlite3.SQLITE_ABORT:
                case Sqlite3.SQLITE_BUSY:
                case Sqlite3.SQLITE_LOCKED:
                case Sqlite3.SQLITE_READONLY:
                case Sqlite3.SQLITE_INTERRUPT:
                case Sqlite3.SQLITE_IOERR:
                case Sqlite3.SQLITE_FULL:
                case Sqlite3.SQLITE_CANTOPEN:
                case Sqlite3.SQLITE_PROTOCOL:
                case Sqlite3.SQLITE_EMPTY:
                case Sqlite3.SQLITE_SCHEMA:
                    return MakeOperationalError(errmsg);

                case Sqlite3.SQLITE_CORRUPT:
                    return MakeDatabaseError(errmsg);

                case Sqlite3.SQLITE_TOOBIG:
                    return MakeDataError(errmsg);

                case Sqlite3.SQLITE_CONSTRAINT:
                case Sqlite3.SQLITE_MISMATCH:
                    return MakeIntegrityError(errmsg);

                case Sqlite3.SQLITE_MISUSE:
                    return MakeProgrammingError(errmsg);

                default:
                    return MakeDatabaseError(errmsg);
            }
        }

        private static Exception CreateThrowable(PythonType type, params object[] args)
        {
            return PythonOps.CreateThrowable(type, args);
        }
    }
}
