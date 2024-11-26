// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.
//
// Copyright (c) Jeff Hardy 2010-2012.
//

using Community.CsharpSqlite;

namespace IronPython.SQLite
{
    internal static class Util
    {
        public static int Step(Sqlite3.Vdbe statement)
        {
            if(statement == null)
                return Sqlite3.SQLITE_OK;
            else
                return Sqlite3.sqlite3_step(statement);
        }
    }
}
