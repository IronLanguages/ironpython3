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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IronPython.Runtime;
using System.Collections;
using IronPython.Runtime.Exceptions;

namespace IronPython.SQLite
{
    public static partial class PythonSQLite
    {
        [PythonType]
        public class Row : IEnumerable
        {
            PythonTuple data;
            PythonTuple description;

            public Row(Cursor cursor, PythonTuple data)
            {
                this.data = data;
                this.description = cursor.description;
            }

            public override bool Equals(object obj)
            {
                Row other = obj as Row;

                if(other == null)
                    return false;

                if(object.ReferenceEquals(this, other))
                    return true;

                return this.description.Equals(other.description) && this.data.Equals(other.data);
            }

            public override int GetHashCode()
            {
                return description.GetHashCode() ^ data.GetHashCode();
            }

            public object __iter__()
            {
                return data;
            }

            public object this[long i]
            {
                get { return this.data[i]; }
            }

            public object this[string s]
            {
                get
                {
                    for(int i = 0; i < data.Count; ++i)
                    {
                        PythonTuple col_desc = (PythonTuple)description[i];
                        if(s.Equals((string)col_desc[0], StringComparison.InvariantCultureIgnoreCase))
                            return data[i];
                    }

                    throw CreateThrowable(PythonExceptions.IndexError, "No item with that key");
                }
            }

            public List keys()
            {
                List list = new List();

                for(int i = 0; i < data.Count; ++i)
                {
                    list.append(((PythonTuple)description[i])[0]);
                }

                return list;
            }

            #region IEnumerable Members

            public IEnumerator GetEnumerator()
            {
                return data.GetEnumerator();
            }

            #endregion
        }
    }
}
