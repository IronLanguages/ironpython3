// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.
//
// Copyright (c) Jeff Hardy 2010-2012.
//

using Community.CsharpSqlite;

namespace IronPython.SQLite
{
    public static partial class PythonSQLite
    {
        public const int SQLITE_OK = Sqlite3.SQLITE_OK;

        public const int SQLITE_DENY = 1;
        public const int SQLITE_IGNORE = 2;

        public const int SQLITE_CREATE_INDEX = 1;
        public const int SQLITE_CREATE_TABLE = 2;
        public const int SQLITE_CREATE_TEMP_INDEX = 3;
        public const int SQLITE_CREATE_TEMP_TABLE = 4;
        public const int SQLITE_CREATE_TEMP_TRIGGER = 5;
        public const int SQLITE_CREATE_TEMP_VIEW = 6;
        public const int SQLITE_CREATE_TRIGGER = 7;
        public const int SQLITE_CREATE_VIEW = 8;
        public const int SQLITE_DELETE = 9;
        public const int SQLITE_DROP_INDEX = 10;
        public const int SQLITE_DROP_TABLE = 11;
        public const int SQLITE_DROP_TEMP_INDEX = 12;
        public const int SQLITE_DROP_TEMP_TABLE = 13;
        public const int SQLITE_DROP_TEMP_TRIGGER = 14;
        public const int SQLITE_DROP_TEMP_VIEW = 15;
        public const int SQLITE_DROP_TRIGGER = 16;
        public const int SQLITE_DROP_VIEW = 17;
        public const int SQLITE_INSERT = 18;
        public const int SQLITE_PRAGMA = 19;
        public const int SQLITE_READ = 20;
        public const int SQLITE_SELECT = 21;
        public const int SQLITE_TRANSACTION = 22;
        public const int SQLITE_UPDATE = 23;
        public const int SQLITE_ATTACH = 24;
        public const int SQLITE_DETACH = 25;
        public const int SQLITE_ALTER_TABLE = 26;
        public const int SQLITE_REINDEX = 27;
        public const int SQLITE_ANALYZE = 28;

        public const int PARSE_DECLTYPES = 1;
        public const int PARSE_COLNAMES = 2;
    }
}
