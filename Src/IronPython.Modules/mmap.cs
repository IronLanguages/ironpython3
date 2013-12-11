/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
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

#if FEATURE_MMAP

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

using IronPython.Runtime;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

[assembly: PythonModule("mmap", typeof(IronPython.Modules.MmapModule))]
namespace IronPython.Modules {
    public static class MmapModule {
        public const int ACCESS_READ = 1;
        public const int ACCESS_WRITE = 2;
        public const int ACCESS_COPY = 3;

        // Constants that are set in os.py
        private const int SEEK_SET = 0;
        private const int SEEK_CUR = 1;
        private const int SEEK_END = 2;

        public static readonly int ALLOCATIONGRANULARITY = GetAllocationGranularity();
        public static readonly int PAGESIZE = System.Environment.SystemPageSize;

        private static readonly object _mmapErrorKey = new object();

        [SpecialName]
        public static void PerformModuleReload(PythonContext/*!*/ context, PythonDictionary/*!*/ dict) {
            context.EnsureModuleException(_mmapErrorKey, PythonExceptions.EnvironmentError, dict, "error", "mmap");
        }

        private static Exception Error(CodeContext/*!*/ context, string/*!*/ message) {
            return PythonExceptions.CreateThrowable(
                (PythonType)PythonContext.GetContext(context).GetModuleState(_mmapErrorKey),
                message
            );
        }

        private static Exception Error(CodeContext/*!*/ context, int errno, string/*!*/ message) {
            return PythonExceptions.CreateThrowable(
                (PythonType)PythonContext.GetContext(context).GetModuleState(_mmapErrorKey),
                errno,
                message
            );
        }

        private static Exception WindowsError(int code) {
            string message = CTypes.FormatError(code);
            return PythonExceptions.CreateThrowable(PythonExceptions.WindowsError, code, message);
        }

        [PythonType]
        public class mmap {
            private MemoryMappedFile _file;
            private MemoryMappedViewAccessor _view;
            private long _position;
            private FileStream _sourceStream;

            private readonly long _offset;
            private readonly string _mapName;
            private readonly MemoryMappedFileAccess _fileAccess;

            private volatile bool _isClosed;
            private int _refCount = 1;
            
            public mmap(
                CodeContext/*!*/ context,
                int fileno,
                long length,
                [DefaultParameterValue(null)]string tagname,
                [DefaultParameterValue(ACCESS_WRITE)]int access,
                [DefaultParameterValue(0L)]long offset
            ) {
                switch (access) {
                    case ACCESS_READ:
                        _fileAccess = MemoryMappedFileAccess.Read;
                        break;
                    case ACCESS_WRITE:
                        _fileAccess = MemoryMappedFileAccess.ReadWrite;
                        break;
                    case ACCESS_COPY:
                        _fileAccess = MemoryMappedFileAccess.CopyOnWrite;
                        break;
                    default:
                        throw PythonOps.ValueError("mmap invalid access parameter");
                }

                if (length < 0) {
                    throw PythonOps.OverflowError("memory mapped size must be positive");
                }
                if (offset < 0) {
                    throw PythonOps.OverflowError("memory mapped offset must be positive");
                }

                // CPython only allows offsets that are a multiple of ALLOCATIONGRANULARITY
                if (offset % ALLOCATIONGRANULARITY != 0) {
                    throw WindowsError(PythonExceptions._WindowsError.ERROR_MAPPED_ALIGNMENT);
                }

                // .NET throws on an empty tagname, but CPython treats it as null.
                _mapName = tagname == "" ? null : tagname;

                if (fileno == -1 || fileno == 0) {
                    // Map anonymous memory that is not tied to a file.
                    // Note: CPython seems to allow 0 as a file descriptor even though it represents stdin.
                    _offset = 0; // offset is ignored without an underlying file
                    _sourceStream = null;

                    // work around the .NET bug whereby CreateOrOpen throws on a null mapName
                    if (_mapName == null) {
                        _mapName = Guid.NewGuid().ToString();
                    }

                    _file = MemoryMappedFile.CreateOrOpen(_mapName, length, _fileAccess);
                } else {
                    // Memory-map an actual file
                    long capacity = checked(_offset + length);
                    _offset = offset;
                    
                    PythonFile file;
                    PythonContext pContext = PythonContext.GetContext(context);
                    if (!pContext.FileManager.TryGetFileFromId(pContext, fileno, out file)) {
                        throw Error(context, PythonExceptions._WindowsError.ERROR_INVALID_BLOCK, "Bad file descriptor");
                    }

                    if ((_sourceStream = file._stream as FileStream) == null) {
                        throw WindowsError(PythonExceptions._WindowsError.ERROR_INVALID_HANDLE);
                    }

                    if (_fileAccess == MemoryMappedFileAccess.ReadWrite && !_sourceStream.CanWrite) {
                        throw WindowsError(PythonExceptions._WindowsError.ERROR_ACCESS_DENIED);
                    }

                    // Enlarge the file as needed.
                    if (capacity > _sourceStream.Length) {
                        if (_sourceStream.CanWrite) {
                            _sourceStream.SetLength(capacity);
                        } else {
                            throw WindowsError(PythonExceptions._WindowsError.ERROR_NOT_ENOUGH_MEMORY);
                        }
                    }

                    _file = MemoryMappedFile.CreateFromFile(
                        _sourceStream, _mapName, _sourceStream.Length, _fileAccess, null, HandleInheritability.None, true
                    );
                }

                _view = _file.CreateViewAccessor(_offset, length, _fileAccess);
                _position = 0L;
            }

            public object __len__() {
                using (new MmapLocker(this)) {
                    return ReturnLong(_view.Capacity);
                }
            }

            public string this[long index] {
                get {
                    using (new MmapLocker(this)) {
                        CheckIndex(index);

                        return ((char)_view.ReadByte(index)).ToString();
                    }
                }

                set {
                    using (new MmapLocker(this)) {
                        if (value == null || value.Length != 1) {
                            throw PythonOps.IndexError("mmap assignment must be a single-character string");
                        }
                        EnsureWritable();
                        CheckIndex(index);

                        _view.Write(index, (byte)value[0]);
                    }
                }
            }

            public string this[Slice slice] {
                get {
                    using (new MmapLocker(this)) {
                        long start, stop, step, longCount;
                        PythonOps.FixSlice(
                            _view.Capacity,
                            GetLong(slice.start), GetLong(slice.stop), GetLong(slice.step),
                            out start, out stop, out step, out longCount
                        );

                        int count = (int)longCount;
                        if (count == 0) {
                            return "";
                        }

                        StringBuilder sb = new StringBuilder(count);

                        for (; count > 0; count--) {
                            sb.Append((char)_view.ReadByte(start));
                            start += step;
                        }

                        return sb.ToString();
                    }
                }

                set {
                    using (new MmapLocker(this)) {
                        if (value == null) {
                            throw PythonOps.TypeError("mmap slice assignment must be a string");
                        }
                        EnsureWritable();

                        long start, stop, step, longCount;
                        PythonOps.FixSlice(
                            _view.Capacity,
                            GetLong(slice.start), GetLong(slice.stop), GetLong(slice.step),
                            out start, out stop, out step, out longCount
                        );

                        int count = (int)longCount;
                        if (value.Length != count) {
                            throw PythonOps.IndexError("mmap slice assignment is wrong size");
                        } else if (count == 0) {
                            return;
                        }

                        byte[] data = value.MakeByteArray();

                        if (step == 1) {
                            _view.WriteArray(start, data, 0, value.Length);
                        } else {
                            foreach (byte b in data) {
                                _view.Write(start, b);
                                start += step;
                            }
                        }
                    }
                }
            }

            public void __delitem__(long index) {
                using (new MmapLocker(this)) {
                    CheckIndex(index);
                    throw PythonOps.TypeError("mmap object doesn't support item deletion");
                }
            }

            
            public void __delslice__(Slice slice) {
                using (new MmapLocker(this)) {
                    throw PythonOps.TypeError("mmap object doesn't support slice deletion");
                }
            }

            public void close() {
                if (!_isClosed) {
                    lock (this) {
                        if (!_isClosed) {
                            _isClosed = true;
                            CloseWorker();
                        }
                    }
                }
            }

            private void CloseWorker() {
                if (Interlocked.Decrement(ref _refCount) == 0) {
                    _view.Flush();
                    _view.Dispose();
                    _file.Dispose();
                    _sourceStream = null;
                    _view = null;
                    _file = null;
                }
            }

            public object find([NotNull]string/*!*/ s) {
                using (new MmapLocker(this)) {
                    return FindWorker(s, Position, _view.Capacity);
                }
            }

            public object find([NotNull]string/*!*/ s, long start) {
                using (new MmapLocker(this)) {
                    return FindWorker(s, start, _view.Capacity);
                }
            }

            public object find([NotNull]string/*!*/ s, long start, long end) {
                using (new MmapLocker(this)) {
                    return FindWorker(s, start, end);
                }
            }

            private object FindWorker(string/*!*/ s, long start, long end) {
                ContractUtils.RequiresNotNull(s, "s");

                start = PythonOps.FixSliceIndex(start, _view.Capacity);
                end = PythonOps.FixSliceIndex(end, _view.Capacity);

                if (s == "") {
                    return start <= end ? ReturnLong(start) : -1;
                }

                long findLength = end - start;
                if (s.Length > findLength) {
                    return -1;
                }
                
                int index = -1;
                int bufferLength = Math.Max(s.Length, PAGESIZE);
                CompareInfo c = CultureInfo.InvariantCulture.CompareInfo;
                
                if (findLength <= bufferLength * 2) {
                    // In this case, the search area is not significantly larger than s, so we only need to
                    // allocate a single string to search through.
                    byte[] buffer = new byte[findLength];
                    _view.ReadArray(start, buffer, 0, (int)findLength);

                    string findString = PythonOps.MakeString(buffer);
                    index = c.IndexOf(findString, s, CompareOptions.Ordinal);
                } else {
                    // We're matching s against a significantly larger file, so we partition the stream into
                    // sections twice the length of s and search each segment. Because a match could exist on a
                    // boundary, sections must overlap by s.Length. Data is saved in 2 buffers to avoid
                    // reading the same parts of the stream twice.
                    byte[] buffer0 = new byte[bufferLength];
                    byte[] buffer1 = new byte[bufferLength];
                    
                    _view.ReadArray(start, buffer0, 0, bufferLength);
                    int bytesRead = _view.ReadArray(start + bufferLength, buffer1, 0, bufferLength);

                    start += bufferLength * 2;
                    findLength -= bufferLength * 2;

                    while (findLength > 0 && bytesRead > 0) {
                        string findString = GetString(buffer0, buffer1, bytesRead);
                        index = c.IndexOf(findString, s, CompareOptions.Ordinal);

                        if (index != -1) {
                            return ReturnLong(start - 2 * bufferLength + index);
                        }

                        byte[] temp = buffer0;
                        buffer0 = buffer1;
                        buffer1 = temp;

                        int readLength = findLength < bufferLength ? (int)findLength : bufferLength;
                        findLength -= bytesRead;

                        bytesRead = _view.ReadArray(start, buffer1, 0, readLength);
                        start += bytesRead;
                    }
                }

                return index == -1 ? -1 : ReturnLong(start + index);
            }

            public int flush() {
                using (new MmapLocker(this)) {
                    _view.Flush();
                    return 1;
                }
            }

            public int flush(long offset, long size) {
                using (new MmapLocker(this)) {
                    CheckIndex(offset, false);
                    CheckIndex(checked(offset + size), false);

                    _view.Flush();
                    return 1;
                }
            }

            public void move(long dest, long src, long count) {
                using (new MmapLocker(this)) {
                    EnsureWritable();
                    if (dest < 0 || src < 0 || count < 0 ||
                        checked(Math.Max(src, dest) + count) > _view.Capacity) {
                        throw PythonOps.ValueError("source or destination out of range");
                    }

                    if (src == dest || count == 0) {
                        return;
                    }

                    if (count <= PAGESIZE) {
                        byte[] buffer = new byte[count];
                        
                        MoveWorker(buffer, src, dest, (int)count);
                    } else if (src < dest) {
                        byte[] buffer = new byte[PAGESIZE];

                        while (count >= PAGESIZE) {
                            MoveWorker(buffer, src, dest, PAGESIZE);
                            src += PAGESIZE;
                            dest += PAGESIZE;
                            count -= PAGESIZE;
                        }

                        if (count > 0) {
                            MoveWorker(buffer, src, dest, (int)count);
                        }
                    } else {
                        byte[] buffer = new byte[PAGESIZE];

                        src += count;
                        dest += count;

                        int len = (int)(count % PAGESIZE);
                        if (len != 0) {
                            src -= len;
                            dest -= len;
                            count -= len;
                            MoveWorker(buffer, src, dest, len);
                        }

                        while (count > 0) {
                            src -= PAGESIZE;
                            dest -= PAGESIZE;
                            count -= PAGESIZE;
                            MoveWorker(buffer, src, dest, PAGESIZE);
                        }
                    }
                }
            }

            private void MoveWorker(byte[] buffer, long src, long dest, int count) {
                _view.ReadArray(src, buffer, 0, count);
                _view.WriteArray(dest, buffer, 0, count);
            }

            public string read(int len) {
                using (new MmapLocker(this)) {
                    long pos = Position;

                    if (len < 0) {
                        len = checked((int)(_view.Capacity - pos));
                    } else if (len > _view.Capacity - pos) {
                        len = checked((int)(_view.Capacity - pos));
                    }

                    if (len == 0) {
                        return "";
                    }

                    byte[] buffer = new byte[len];
                    len = _view.ReadArray(pos, buffer, 0, len);
                    Position = pos + len;

                    return buffer.MakeString(len);
                }
            }

            public string read_byte() {
                using (new MmapLocker(this)) {
                    long pos = Position;

                    if (pos >= _view.Capacity) {
                        throw PythonOps.ValueError("read byte out of range");
                    }

                    byte res = _view.ReadByte(pos);
                    Position = pos + 1;

                    return ((char)res).ToString();
                }
            }

            public string readline() {
                using (new MmapLocker(this)) {
                    StringBuilder res = new StringBuilder();

                    long pos = Position;
                    
                    char cur = '\0';
                    while (cur != '\n' && pos < _view.Capacity) {
                        cur = (char)_view.ReadByte(pos);
                        res.Append(cur);
                        pos++;
                    }

                    Position = pos;
                    return res.ToString();
                }
            }

            public void resize(long newsize) {
                using (new MmapLocker(this)) {
                    if (_fileAccess != MemoryMappedFileAccess.ReadWrite) {
                        throw PythonOps.TypeError("mmap can't resize a readonly or copy-on-write memory map.");
                    }

                    if (_sourceStream == null) {
                        // resizing is not supported without an underlying file
                        throw WindowsError(PythonExceptions._WindowsError.ERROR_INVALID_PARAMETER);
                    }

                    if (newsize == 0) {
                        // resizing to an empty mapped region is not allowed
                        throw WindowsError(_offset == 0
                            ? PythonExceptions._WindowsError.ERROR_ACCESS_DENIED
                            : PythonExceptions._WindowsError.ERROR_FILE_INVALID
                        );
                    }

                    if (_view.Capacity == newsize) {
                        // resizing to the same size
                        return;
                    }

                    long capacity = checked(_offset + newsize);

                    try {
                        _view.Flush();
                        _view.Dispose();
                        _file.Dispose();

                        if (!_sourceStream.CanWrite) {
                            _sourceStream = new FileStream(_sourceStream.Name, FileMode.OpenOrCreate, FileAccess.ReadWrite);
                        }

                        // Resize the file as needed.
                        if (capacity != _sourceStream.Length) {
                            _sourceStream.SetLength(capacity);
                        }

                        _file = MemoryMappedFile.CreateFromFile(
                            _sourceStream, _mapName, _sourceStream.Length, _fileAccess, null, HandleInheritability.None, true
                        );

                        _view = _file.CreateViewAccessor(_offset, newsize, _fileAccess);
                    } catch {
                        close();
                        throw;
                    }
                }
            }

            public object rfind([NotNull]string/*!*/ s) {
                using (new MmapLocker(this)) {
                    return RFindWorker(s, Position, _view.Capacity);
                }
            }

            public object rfind([NotNull]string/*!*/ s, long start) {
                using (new MmapLocker(this)) {
                    return RFindWorker(s, start, _view.Capacity);
                }
            }

            public object rfind([NotNull]string/*!*/ s, long start, long end) {
                using (new MmapLocker(this)) {
                    return RFindWorker(s, start, end);
                }
            }

            private object RFindWorker(string/*!*/ s, long start, long end) {
                ContractUtils.RequiresNotNull(s, "s");

                start = PythonOps.FixSliceIndex(start, _view.Capacity);
                end = PythonOps.FixSliceIndex(end, _view.Capacity);

                if (s == "") {
                    return start <= end ? ReturnLong(start) : -1;
                }

                long findLength = end - start;
                if (s.Length > findLength) {
                    return -1;
                }

                int index = -1;
                int bufferLength = Math.Max(s.Length, PAGESIZE);
                CompareInfo c = CultureInfo.InvariantCulture.CompareInfo;

                if (findLength <= bufferLength * 2) {
                    // In this case, the search area is not significantly larger than s, so we only need to
                    // allocate a single string to search through.
                    byte[] buffer = new byte[findLength];

                    findLength = _view.ReadArray(start, buffer, 0, (int)findLength);

                    string findString = PythonOps.MakeString(buffer);
                    index = c.LastIndexOf(findString, s, CompareOptions.Ordinal);
                } else {
                    // We're matching s against a significantly larger file, so we partition the stream into
                    // sections twice the length of s and search each segment. Because a match could exist on a
                    // boundary, sections must overlap by s.Length. Data is saved in 2 buffers to avoid
                    // reading the same parts of the stream twice.
                    byte[] buffer0 = new byte[bufferLength];
                    byte[] buffer1 = new byte[bufferLength];

                    int remainder = (int)((end - start) % bufferLength);
                    if (remainder == 0) {
                        remainder = bufferLength;
                    }

                    start = end - bufferLength - remainder;
                    findLength -= bufferLength + remainder;

                    _view.ReadArray(start, buffer0, 0, bufferLength);
                    int bytesRead = _view.ReadArray(start + bufferLength, buffer1, 0, remainder);
                    
                    while (findLength >= 0) {
                        string findString = GetString(buffer0, buffer1, bytesRead);
                        index = c.LastIndexOf(findString, s, CompareOptions.Ordinal);

                        if (index != -1) {
                            return ReturnLong(index + start);
                        }

                        byte[] temp = buffer0;
                        buffer0 = buffer1;
                        buffer1 = temp;

                        start -= bufferLength;
                        bytesRead = _view.ReadArray(start, buffer0, 0, bufferLength);
                        findLength -= bytesRead;
                    }
                }

                return index == -1 ? -1 : ReturnLong(index + start);
            }

            public void seek(long pos, [DefaultParameterValue(SEEK_SET)]int whence) {
                using (new MmapLocker(this)) {
                    switch (whence) {
                        case SEEK_SET:
                            break;
                        case SEEK_CUR:
                            pos = checked(pos + Position);
                            break;
                        case SEEK_END:
                            pos = checked(pos + _view.Capacity);
                            break;
                        default:
                            throw PythonOps.ValueError("unknown seek type");
                    }

                    CheckSeekIndex(pos);
                    Position = pos;
                }
            }

            public object size() {
                using (new MmapLocker(this)) {
                    return ReturnLong(_offset + _view.Capacity);
                }
            }

            public object tell() {
                using (new MmapLocker(this)) {
                    return ReturnLong(Position);
                }
            }

            public void write(string s) {
                using (new MmapLocker(this)) {
                    EnsureWritable();

                    long pos = Position;

                    if (_view.Capacity - pos < s.Length) {
                        throw PythonOps.ValueError("data out of range");
                    }

                    byte[] data = s.MakeByteArray();
                    _view.WriteArray(pos, data, 0, s.Length);

                    Position = pos + s.Length;
                }
            }

            public void write_byte(string s) {
                using (new MmapLocker(this)) {
                    if (s.Length != 1) {
                        throw PythonOps.TypeError("write_byte() argument 1 must be char, not str");
                    }
                    EnsureWritable();

                    long pos = Position;
                    if (Position >= _view.Capacity) {
                        throw PythonOps.ValueError("write byte out of range");
                    }

                    _view.Write(pos, (byte)s[0]);
                    Position = pos + 1;
                }
            }

#region Private implementation details

            private long Position {
                get {
                    return Interlocked.Read(ref _position);
                }
                set {
                    Interlocked.Exchange(ref _position, value);
                }
            }

            private void EnsureWritable() {
                if (_fileAccess == MemoryMappedFileAccess.Read) {
                    throw PythonOps.TypeError("mmap can't modify a read-only memory map.");
                }
            }

            private void CheckIndex(long index) {
                CheckIndex(index, true);
            }

            private void CheckIndex(long index, bool inclusive) {
                if (index > _view.Capacity || index < 0 || (inclusive && index == _view.Capacity)) {
                    throw PythonOps.IndexError("mmap index out of range");
                }
            }

            private void CheckSeekIndex(long index) {
                if (index > _view.Capacity || index < 0) {
                    throw PythonOps.ValueError("seek out of range");
                }
            }

            private static long? GetLong(object o) {
                if (o == null) {
                    return null;
                } else if (o is int) {
                    return (long)(int)o;
                } else if (o is BigInteger) {
                    return (long)(BigInteger)o;
                } else if (o is long) {
                    return (long)o;
                }
                return (long)Converter.ConvertToBigInteger(o);
            }

            private static object ReturnLong(long l) {
                if (l <= int.MaxValue && l >= int.MinValue) {
                    return (int)l;
                }
                return (BigInteger)l;
            }

            private static string GetString(byte[] buffer0, byte[] buffer1, int length1) {
                StringBuilder sb = new StringBuilder(buffer0.Length + length1);
                foreach (byte b in buffer0) {
                    sb.Append((char)b);
                }
                for (int i = 0; i < length1; i++) {
                    sb.Append((char)buffer1[i]);
                }
                return sb.ToString();
            }

            internal string GetSearchString() {
                using (new MmapLocker(this)) {
                    return this[new Slice(0, null)];
                }
            }

            #endregion

#region Synchronization
            
            private void EnsureOpen() {
                if (_isClosed) {
                    throw PythonOps.ValueError("mmap closed or invalid");
                }
            }

            private struct MmapLocker : IDisposable {
                private readonly mmap _mmap;

                public MmapLocker(mmap mmap) {
                    _mmap = mmap;
                    Interlocked.Increment(ref _mmap._refCount);
                    _mmap.EnsureOpen();
                }

#region IDisposable Members

                public void Dispose() {
                    _mmap.CloseWorker();
                }

                #endregion
            }

            #endregion
        }

#region P/Invoke for allocation granularity

        [StructLayout(LayoutKind.Sequential)]
        private struct SYSTEM_INFO {
            internal int dwOemId; // This is a union of a DWORD and a struct containing 2 WORDs.
            internal int dwPageSize;
            internal IntPtr lpMinimumApplicationAddress;
            internal IntPtr lpMaximumApplicationAddress;
            internal IntPtr dwActiveProcessorMask;
            internal int dwNumberOfProcessors;
            internal int dwProcessorType;
            internal int dwAllocationGranularity;
            internal short wProcessorLevel;
            internal short wProcessorRevision;
        }

        [DllImport("kernel32", SetLastError=true)]
        private static extern void GetSystemInfo(ref SYSTEM_INFO lpSystemInfo);

        private static int GetAllocationGranularity() {
            try {
                return GetAllocationGranularityWorker();
            } catch {
                return System.Environment.SystemPageSize;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int GetAllocationGranularityWorker() {
            SYSTEM_INFO info = new SYSTEM_INFO();
            GetSystemInfo(ref info);
            return info.dwAllocationGranularity;
        }

        #endregion
    }
}

#endif
