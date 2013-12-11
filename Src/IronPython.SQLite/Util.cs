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
