// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;

using Microsoft.Scripting.Utils;

using IronPython.Runtime.Exceptions;
using System.Diagnostics.CodeAnalysis;
using IronPython.Runtime.Operations;

namespace IronPython.Runtime {
    /// <summary>
    /// Emulates a file descriptor table for <see cref="Modules.PythonIOModule.FileIO"/> and <see cref="System.IO.Stream"/> objects.
    /// </summary>
    /// <remarks>
    /// PythonFileManager emulates a file descriptor table. On Windows, .NET uses Win32 API which uses file handles
    /// rather than file descriptors. The emulation is necesary to support Python API, which in some places uses file descriptors.
    ///
    /// The manager maintains a mapping between open files (or system file-like objects) and a "descriptor", being a small non-negative integer.
    /// Unlike in CPython, the descriptors are allocated lazily, meaning they are allocated only when they become exposed (requested)
    /// through relevant API calls. Therefore the ordering of assigned descriptors in IronPython may be different than in CPython.
    /// This should not be a problem since assumptions on descriptor ordering are not good programming practice.
    ///
    /// Open filesystem files are represented in the manager as lower level Stream objects, and higher level FileIO objects.
    /// FileIO basically encapsulates one of two Streams (one for reading and one for writing, which may be the same).
    ///
    /// There are two levels of sharing that are supported. On the higher level, several different FileIO objects may share the same
    /// file descriptor. In such case, all of those FileIO objects maintain references to the same underlying Stream object(s)
    /// and the manager maintains a mapping between the descriptor and only one of those FileIO objects (the first one opened).
    /// In such situations, only one of the FileIO may be opened with flag `closefd` (CPython rule).
    ///
    /// If a filesystem file is opened without a corresponding FileIO (i.e. a bare file descriptor), the manager maintains a hidden FileIO
    /// for itself to manage the mapping. This object is not exposed to Python and only used for opening/closing/dup etc. purposes.
    ///
    /// The second lever of sharing of open files is below the file descriptor level. A file descriptor can be duplicated using dup/dup2,
    /// but the duplicated descriptor is still refering to the same open file in the filesystem. In such case, the manager maintains
    /// a separate FileIO for the duplicated descriptor, but the FileIOs for both descriptors share the underlying Streams.
    /// Both such descriptors have to be closed independently by the user code (either explicitly by os.close(fd) or through close()
    /// on the FileIO objects), but the underlying shared streams are closed only when all such duplicated descriptors are closed.
    /// To facilitate that, the manager uses rudimentary reference counting. This ref-counting is only used for streams
    /// that are shared across file descriptors.
    /// </remarks>
    internal class PythonFileManager {
        /// <summary>
        /// Maximum number of open file descriptors.
        /// </summary>
        public const int LIMIT_OFILE = 0x100000; // hard limit on Linux

        private readonly object _synchObject = new();
        private readonly Dictionary<int, object> _objects = new();
        private const int _offset = 3; // number of lowest keys that are not automatically allocated
        private int _current = _offset; // lowest potentially unused key in _objects at or above _offset
        private readonly ConcurrentDictionary<Stream, int> _refs = new();

        public int AddFile(int id, Modules.PythonIOModule.FileIO file) {
            file._fd = Add(id, file);
            return file._fd;
        }

        public int AddStream(int id, Stream stream) => Add(id, stream);

        // Public API controls which types are allowed to be added
        private int Add(int id, object obj) {
            ContractUtils.RequiresNotNull(obj, nameof(obj));
            ContractUtils.Requires(id >= 0, nameof(id));
            lock (_synchObject) {
                if (_objects.ContainsKey(id)) {
                    throw PythonOps.OSError(9, "Bad file descriptor", id.ToString());
                }
                _objects.Add(id, obj);
                return id;
            }
        }

        public int AddFile(Modules.PythonIOModule.FileIO file) {
            file._fd = Add(file);
            return file._fd;
        }

        public int AddStream(Stream stream) => Add(stream);

        // Public API controls which types are allowed to be added
        private int Add(object obj) {
            ContractUtils.RequiresNotNull(obj, nameof(obj));
            lock (_synchObject) {
                while (_objects.ContainsKey(_current)) {
                    _current++;
                    if (_current >= LIMIT_OFILE)
                        throw PythonOps.OSError(24, "Too many open files");
                }
                _objects.Add(_current, obj);
                return _current++;
            }
        }

        public bool Remove(object obj) {
            lock (_synchObject) {
                int id = GetIdFromObject(obj);
                if (id >= 0) {
                    return RemoveObjectOnId(id);
                }
                return false;
            }
        }

        public bool RemoveObjectOnId(int id) {
            lock (_synchObject) {
                bool removed = _objects.Remove(id);
                if (id < _current && id >= _offset) {
                    _current = id;
                }
                return removed;
            }
        }

        public bool TryGetObjectFromId(int id, [NotNullWhen(true)] out object? obj) {
            lock (_synchObject) {
                return _objects.TryGetValue(id, out obj);
            }
        }

        public bool TryGetFileFromId(int id, [NotNullWhen(true)] out Modules.PythonIOModule.FileIO? file) {
            TryGetObjectFromId(id, out object? obj);
            file = obj as Modules.PythonIOModule.FileIO;
            return file is not null;
        }

        public bool TryGetStreamFromId(int id, [NotNullWhen(true)] out Stream? stream) {
            TryGetObjectFromId(id, out object? obj);
            stream = obj as Stream;
            return stream is not null;
        }

        public object GetObjectFromId(int id) {
            if (TryGetObjectFromId(id, out object? obj)) {
               return obj;
            }
            throw PythonOps.OSError(9, "Bad file descriptor");
        }

        public Modules.PythonIOModule.FileIO GetFileFromId(int id) {
            if (TryGetFileFromId(id, out Modules.PythonIOModule.FileIO? file)) {
                return file;
            }
            throw PythonOps.OSError(9, "Bad file descriptor");
        }

        public Stream GetStreamFromId(int id) {
            if (TryGetStreamFromId(id, out Stream? stream)) {
                return stream;
            }
            throw PythonOps.OSError(9, "Bad file descriptor");
        }

        public void EnsureRef(Stream stream) {
            _refs.TryAdd(stream, 1);
        }

        public void AddRef(Stream stream) {
            _refs.AddOrUpdate(stream, 1, (_,  v) => v + 1);
        }

        public bool DerefAndCloseIfLast(Stream stream) {
            int newref = _refs.AddOrUpdate(stream, 0, (_, v) => v - 1);
            if (newref <= 0) {
                stream.Close(); // equivalent of Dispose()
                _refs.TryRemove(stream, out _);
                return true;
            }
            return false;
        }

        public int GetOrAssignId(object obj) {
            lock (_synchObject) {
                int res = GetIdFromObject(obj);
                if (res == -1) {
                    res = Add(obj);
                }
                return res;
            }
        }

        public int GetIdFromObject(object obj) {
            lock (_synchObject) {
                foreach (KeyValuePair<int, object> kvp in _objects) {
                    if (ReferenceEquals(kvp.Value, obj)) {
                        return kvp.Key;
                    }
                }
            }
            return -1;
        }

        public bool ValidateFdRange(int fd) {
            return fd >= 0 && fd < LIMIT_OFILE;
        }
    }
}
