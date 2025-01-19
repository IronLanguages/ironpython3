// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.
//
// Copyright (c) Jeff Hardy 2010-2012.
//

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
            private PythonTuple data;
            private PythonTuple description;

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
                        if(s.Equals((string)col_desc[0], StringComparison.OrdinalIgnoreCase))
                            return data[i];
                    }

                    throw CreateThrowable(PythonExceptions.IndexError, "No item with that key");
                }
            }

            public PythonList keys()
            {
                PythonList list = new PythonList();

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
