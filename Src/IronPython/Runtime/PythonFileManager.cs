// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.IO;

using Microsoft.Scripting.Utils;

using IronPython.Runtime.Exceptions;

namespace IronPython.Runtime {
    internal class PythonFileManager {
        private HybridMapping<object> mapping = new HybridMapping<object>(3);

        public int AddToStrongMapping(Modules.PythonIOModule.FileIO file, int pos = -1) {
            return mapping.StrongAdd(file, pos);
        }

        public int AddToStrongMapping(Stream stream, int pos = -1) {
            return mapping.StrongAdd(stream, pos);
        }

        public void Remove(object o) {
            mapping.RemoveOnObject(o);
        }

        public void RemoveObjectOnId(int id) {
            mapping.RemoveOnId(id);
        }

        public Modules.PythonIOModule.FileIO GetFileFromId(PythonContext context, int id) {
            if (TryGetFileFromId(context, id, out Modules.PythonIOModule.FileIO pf)) {
                return pf;
            }

            throw PythonExceptions.CreateThrowable(PythonExceptions.OSError, 9, "Bad file descriptor");
        }

        public bool TryGetFileFromId(PythonContext context, int id, out Modules.PythonIOModule.FileIO pf) {
            pf = mapping.GetObjectFromId(id) as Modules.PythonIOModule.FileIO;
            return pf != null;
        }

        public bool TryGetObjectFromId(PythonContext context, int id, out object o) {
            o = mapping.GetObjectFromId(id);
            return o != null;
        }

        public object GetObjectFromId(int id) {
            object o = mapping.GetObjectFromId(id);

            if (o == null) {
                throw PythonExceptions.CreateThrowable(PythonExceptions.OSError, 9, "Bad file descriptor");
            }
            return o;
        }

        public int GetIdFromFile(Modules.PythonIOModule.FileIO pf) {
            return mapping.GetIdFromObject(pf);
        }

        public void CloseIfLast(CodeContext context, int fd, Modules.PythonIOModule.FileIO pf) {
            mapping.RemoveOnId(fd);
            if (-1 == mapping.GetIdFromObject(pf)) {
                pf.close(context);
            }
        }

        public void CloseIfLast(int fd, Stream stream) {
            mapping.RemoveOnId(fd);
            if (-1 == mapping.GetIdFromObject(stream)) {
                stream.Close();
            }
        }

        public int GetOrAssignIdForFile(Modules.PythonIOModule.FileIO pf) {
            int res = mapping.GetIdFromObject(pf);
            if (res == -1) {
                // lazily created weak mapping
                res = mapping.WeakAdd(pf);
            }
            return res;
        }

        public int GetIdFromObject(object o) {
            return mapping.GetIdFromObject(o);
        }


        public int GetOrAssignIdForObject(object o) {
            int res = mapping.GetIdFromObject(o);
            if (res == -1) {
                // lazily created weak mapping
                res = mapping.WeakAdd(o);
            }
            return res;
        }

        public bool ValidateFdRange(int fd) {
            return fd >= 0 && fd < HybridMapping<object>.SIZE;
        }
    }
}
