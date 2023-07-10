// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.Versioning;

using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime.Operations;
using System.Diagnostics;

namespace IronPython.Runtime {

    /// <summary>
    /// Lightweight version of FileIO.
    /// </summary>
    internal sealed class StreamBox {
        private int _id = -1;
        private Stream _readStream;
        private Stream _writeStream;

        public StreamBox(Stream readStream, Stream writeStream) {
            _readStream = readStream;
            _writeStream = writeStream;
        }

        public StreamBox(Stream stream) : this(stream, stream) { }

        public StreamBox(StreamBox streams) : this(streams._readStream, streams._writeStream) {
            StreamType = streams.StreamType;
        }

        public StreamBox(Stream stream, ConsoleStreamType streamType) : this(stream) {
            StreamType = streamType;
        }

        public Stream ReadStream => _readStream;
        public Stream WriteStream => _writeStream;

        public int Id {
            get => _id;
            set {
                // Only PythonFileManager should set Id, and only once
                if (value < 0) throw new ArgumentException("File descriptor must be non-negative.");
                if (_id >= 0) throw new InvalidOperationException("File descriptor already set");
                _id = value;
            }
        }

        public bool IsSingleStream => ReferenceEquals(_readStream, _writeStream);

        public ConsoleStreamType? StreamType { get; private set; }

        /// <summary>
        /// Is this stdin, stdout, or stderr?
        /// </summary>
        /// <returns></returns>
        public bool IsStandardIOStream() {
            return SharedIO.IsConsoleStream(_readStream);
        }

        /// <summary>
        /// Is this stdin, stdout, or stderr, connected to a console?
        /// </summary>
        /// <returns></returns>
        public bool IsConsoleStream() {
            if (Environment.OSVersion.Platform == PlatformID.Unix) {
                return isattyUnix();
            }

            if (!SharedIO.IsConsoleStream(_readStream)) return false;
            return StreamType switch {
                ConsoleStreamType.Input => !Console.IsInputRedirected,
                ConsoleStreamType.Output => !Console.IsOutputRedirected,
                ConsoleStreamType.ErrorOutput => !Console.IsErrorRedirected,
                _ => false
            };

            // Isolate Mono.Unix from the rest of the method so that we don't try to load the Mono.Unix assembly on Windows.
            bool isattyUnix() {
                // TODO: console streams may be dupped to differend FD numbers, do not use hard-coded 0, 1, 2
                return StreamType switch {
                    ConsoleStreamType.Input => Mono.Unix.Native.Syscall.isatty(0),
                    ConsoleStreamType.Output => Mono.Unix.Native.Syscall.isatty(1),
                    ConsoleStreamType.ErrorOutput => Mono.Unix.Native.Syscall.isatty(2),
                    _ => false
                };
            }
        }

        public long Truncate(long  size) {
            long pos = _readStream.Position;
            _writeStream.SetLength(size);
            _readStream.Seek(pos, SeekOrigin.Begin);
            return size;
        }

        public byte[] Read(int count) {
            byte[] buffer = new byte[count];
            int bytesRead = _readStream.Read(buffer, 0, count);
            Array.Resize(ref buffer, bytesRead);
            return buffer;
        }

        public int ReadInto(IPythonBuffer buffer) {
#if NETCOREAPP
            return _readStream.Read(buffer.AsSpan());
#else
            byte[]? bytes = buffer.AsUnsafeWritableArray();
            if (bytes is not null) {
                return _readStream.Read(bytes, 0, buffer.NumBytes());
            }

            var span = buffer.AsSpan();
            const int chunkSize = 0x1000; // 4 KiB, default buffer size of FileSteam
            bytes = ArrayPool<byte>.Shared.Rent(chunkSize);
            try {
                for (int pos = 0; pos < span.Length; pos += chunkSize) {
                    int toRead = Math.Min(chunkSize, span.Length - pos);
                    int hasRead = _readStream.Read(bytes, 0, toRead);
                    bytes.AsSpan(0, hasRead).CopyTo(span.Slice(pos));
                    if (hasRead < toRead) return pos + hasRead;
                }
            } finally {
                ArrayPool<byte>.Shared.Return(bytes);
            }
            return span.Length;
#endif
        }

        public int Write(IPythonBuffer buffer) {
            int count;
#if NETCOREAPP
            ReadOnlySpan<byte> bytes = buffer.AsReadOnlySpan();
            count = bytes.Length;
            _writeStream.Write(bytes);
#else
            byte[] bytes = buffer.AsUnsafeArray() ?? buffer.AsUnsafeWritableArray() ?? buffer.ToArray();
            count = buffer.NumBytes();
            _writeStream.Write(bytes, 0, count);
#endif
            _writeStream.Flush(); // IO at this level is not supposed to buffer so we need to call Flush.
            if (!IsSingleStream) {
                _readStream.Seek(_writeStream.Position, SeekOrigin.Begin);
            }
            return count;
        }

        public void Flush() {
            if (_writeStream.CanWrite) {
                _writeStream.Flush();
            }
        }

        public void CloseStreams(PythonFileManager? manager) {
            if (manager is not null && Id >= 0) {
                manager.Remove(this);
                manager.DerefStreamsAndCloseIfLast(this);
            } else {
                _readStream.Close();
                if (!IsSingleStream) {
                    _writeStream.Close();
                }
            }
        }
    }

    /// <summary>
    /// Emulates a file descriptor table.
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
    /// Open filesystem files are represented in the manager as StreamBox objects, which
    /// basically encapsulate one or two Streams (one for reading and one for writing, which may be the same).
    ///
    /// There are two levels of sharing that are supported. On the higher level, several different FileIO objects may share the same
    /// file descriptor. In such case, all of those FileIO objects maintain references to the same underlying StreamBox object.
    /// In such situations, only one of the FileIO may be opened with flag `closefd` (CPython rule).
    ///
    /// The second lever of sharing of open files is below the file descriptor level. A file descriptor can be duplicated using dup/dup2,
    /// but the duplicated descriptor is still refering to the same open file in the filesystem. In such case, the manager maintains
    /// a separate StreamBox for the duplicated descriptor, but the StreamBoxes for both descriptors share the underlying Streams.
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
        private readonly Dictionary<int, StreamBox> _table = new();
        private const int _offset = 3; // number of lowest keys that are not automatically allocated
        private int _current = _offset; // lowest potentially unused key in _objects at or above _offset
        private readonly ConcurrentDictionary<Stream, int> _refs = new();

        // Mandatory Add for Unix, on Windows only for dup2 case
        public int Add(int id, StreamBox streams) {
            ContractUtils.RequiresNotNull(streams, nameof(streams));
            ContractUtils.Requires(streams.Id < 0, nameof(streams));
            ContractUtils.Requires(id >= 0, nameof(id));
            lock (_synchObject) {
                if (_table.ContainsKey(id)) {
                    throw PythonOps.OSError(9, "Bad file descriptor", id.ToString());
                }
                streams.Id = id;
                _table.Add(id, streams);
                return id;
            }
        }

        [SupportedOSPlatform("windows")]
        public int Add(StreamBox streams) {
            ContractUtils.RequiresNotNull(streams, nameof(streams));
            ContractUtils.Requires(streams.Id < 0, nameof(streams));
            lock (_synchObject) {
                while (_table.ContainsKey(_current)) {
                    _current++;
                    if (_current >= LIMIT_OFILE)
                        throw PythonOps.OSError(24, "Too many open files");
                }
                streams.Id = _current;
                _table.Add(_current, streams);
                return _current++;
            }
        }

        public bool Remove(StreamBox streams) {
            lock (_synchObject) {
                if (streams.Id >= 0) {
                    return Remove(streams.Id);
                }
                return false;
            }
        }

        public bool Remove(int id) {
            lock (_synchObject) {
                bool removed = _table.Remove(id);
                if (id < _current && id >= _offset) {
                    _current = id;
                }
                return removed;
            }
        }

        public bool TryGetStreams(int id, [NotNullWhen(true)] out StreamBox? streams) {
            lock (_synchObject) {
                return _table.TryGetValue(id, out streams);
            }
        }

        public StreamBox GetStreams(int id) {
            if (TryGetStreams(id, out StreamBox? streams)) {
               return streams;
            }
            throw PythonOps.OSError(9, "Bad file descriptor");
        }

        public int GetOrAssignId(StreamBox streams) {
            lock (_synchObject) {
                int res = streams.Id;
                if (res == -1) {
                    res = Add(streams);
                }
                return res;
            }
        }

        public void EnsureRefStreams(StreamBox streams) {
            Debug.Assert(streams.Id >= 0);
            _refs.TryAdd(streams.ReadStream, 1);
        }

        public void AddRefStreams(StreamBox streams) {
            Debug.Assert(streams.Id >= 0);
            _refs.AddOrUpdate(streams.ReadStream, 1, (_,  v) => v + 1);
        }

        public bool DerefStreamsAndCloseIfLast(StreamBox streams) {
            Debug.Assert(streams.Id >= 0);
            int newref = _refs.AddOrUpdate(streams.ReadStream, 0, (_, v) => v - 1);
            if (newref <= 0) {
                streams.ReadStream.Close(); // equivalent of Dispose()
                _refs.TryRemove(streams.ReadStream, out _);
                if (!streams.IsSingleStream) {
                    streams.WriteStream.Close();
                }
                return true;
            }
            return false;
        }

        public bool ValidateFdRange(int fd) {
            return fd >= 0 && fd < LIMIT_OFILE;
        }
    }
}
