// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#if FEATURE_MMAP

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;

using IronPython.Runtime;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

using Microsoft.Scripting.Utils;
using Microsoft.Win32.SafeHandles;

/*
MemoryMappedFile — Rules of Engagement on .NET
==============================================

In .NET, there are the following fields of `MemoryMappedFile` related to the lifetime management of
resources.
* `private readonly SafeMemoryMappedFileHandle _handle;` created in the constructor; necessary to
  operate on the mmap, always disposed.
* `private readonly bool _leaveOpen;` initialized to a constructor parameter value; it pertains to
  `_fileHandle` not `_handle`
* `private readonly SafeFileHandle? _fileHandle;` may be provided to the constructor, created by the
  constructor, or null.

Note that there is no field that captures `FileStream`. If a `FileStream` instance is provided to
the factory method, it will only be used once to get its file handle, which fate is controlled by
`_leaveOpen`. The `FileStream` instance itself is not disposed by `MemoryMappedFile`. A bit strange,
since `FileStream` has a destructor and may be lingering around. However, when its `Dispose` is
called from within the finalizer, it will not try to dispose the file handle, which is the whole
point.

`MemoryMappedFile` itself is `IDisposable` and its `Dispose` does:
* dispose `_handle`, unless `_handle.IsClosed` already.
* if not `_leaveOpen` and `_fileHandle` is not null, dispose `_fileHandle`.

There are several factory/constructor groups of `MemoryMappedFile`:

## Factory Method Group #1 (Windows only)

Opens an existing named memory mapped file by name. In this case, only `_handle` is initialized;
there is no underlying `_fileHandle`. It delegates opening to `OpenCore(mapName, inheritability,
desiredAccessRights, false);` **This group functions only on Windows.**

## Factory Method Group #2

Creates a new memory mapped file where the content is taken from an existing file on disk.

If the factory method is given a file path, it creates its own `FileStream`, stores its handle in
`_fileHandle`, and ensures that the file handle gets closed on dispose (`_leaveOpen` is false).

If the factory method is given a file handle, it is stored in `_fileHandle` and its lifetime is
controlled by parameter `leaveOpen` given to the same method. If `leaveOpen` is true, the caller is
responsible of disposing the file handle.

If the factory method is given a `fileStream`, it is used to get the file length, flush the stream,
and to extract the file handle into `_fileHandle`. Whether the extracted file handle is disposed
depend on parameter `leaveOpen`. `FileStream` itself is never disposed.

It delegates the opening to `CreateCore(fileHandle, mapName, HandleInheritability.None, access,
MemoryMappedFileOptions.None, capacity, fileSize);` (see below for mode details on POSIX).

**On POSIX, mapName must be null.**

## Factory Method Group #3 (not POSIX)

Creates a new empty memory mapped file. It only accepts a map name, and never creates/uses an
existing file from the file system. It delegates the creation to `CreateCore(fileHandle: null,
mapName, inheritability, access, options, capacity, -1);`

**On POSIX, mapName must be null so practically this group cannot be used on POSIX.**

## Factory Method Group #4 (Windows only)

Creates a new empty memory mapped file or opens an existing memory mapped file if one exists with
the same name. In this factory method/constructor, there is no file stream or file handle involved;
If the map of the requested name exists, it is like opening from group #1, if it doesn't, it is like
group #3 **This group functions only on Windows.**

## Behaviour on POSIX

Only Group #2 can be used so it means that the factory/constructor must be given one of:
* file path (`string`)
* file handle (`SafeFileHandle`), may be null for an anonymous empty map
* file stream (`FileStream`)

The actual work is done by `CreateCore` (POSIX-specific). If given a null file handle (from factory
method `CreateNew`), `CreateCore` may create its own file stream if needed. This file stream/handle
is not saved in a field `_fileHandle` of the map itself, but when the handle is passed to the
constructor of `SafeMemoryMappedFileHandle`, it is also marked as `ownsFileStream`, so disposing the
map will dispose the file handle. In all normal cases, i.e. `filehandle` is not null but passed by
the factory method, the lifetime of the file handle is controlled by `_leaveOpen` of
`MemoryMappedFile` and `SafeMemoryMappedFileHandle` is created with the argument `ownsFileStream`
set to false. The constructor to `SafeMemoryMappedFileHandle` does `DangerousAddRef` to the given
file stream handle (if any), so that when the original file is disposed, the handle is still valid.
On POSIX, the mmap handle value will be set to the same value as file handle value, which is the
file descriptor. For mmaps without the underlying file stream, the handle is set originally to
`IntPtr.MaxValue` so that it is valid but does not collide with any existing file descriptor. When
the mmap handle is released, it also does `DangerousRelease` of the underlying file stream handle
(if any), plus `Dispose` of it if it owned the file stream.

`SafeFileHandle` closes the underlying file descriptor on dispose and sets it to invalid. It is OK
to close the handle several times, or even if it add-reffed and in use somewhere else. The
descriptor will be closed ass soon as the refcount is released, and in the meantime, it will prevent
future addrefs.

MmapDefault - Rules of Engagement
=================================

`MmapDefault` is the workhorse for Python's `mmap`. It contains all the code necessary to run on all
supported platforms. The two subclasses `MmapWindows` and `MmapPosix` only contain platform specific
constructors, to adhere to Python API.

The relevant lifetime-sensitive (disposable) fields are:
* `MemoryMappedFile _file;` created in the constructor, may be recreated on resize
* `MemoryMappedViewAccessor _view;` created in the constructor, may be recreated on resize
* `FileStream _sourceStream;` the underlying file object, may be null
* `SafeFileHandle _handle;` the handle of the underlying file object, only used on some POSIX
  platforms, null otherwise

## .NET 8.0+/POSIX

When the constructor is given a file descriptor, it is duplicated, saved in `_handle` and used to
create the memory-mapped `_file`. The duplication of the file descriptor is CPython's behaviour;
since Python 3.13 the constructor accepts a keyword-only argument `trackfd` prevents the duplication
but it is not implemented here. `_handle` always owns the (duplicated) file descriptor, so it has to
be disposed appropriately, if created. `_file` is created instructed to leave the file handle open,
so it is possible to dispose it and recreate again on `resize`.

## .NET 6.0/POSIX

The factory method to create a memory-mapped file from a file descriptor is not available. The
descriptor is still duplicated and saved in `_handle` like on .NET 8.0 but it is used to create a
`FileStream` which is then used to create the memory-mapped file. The created `FileStream` is saved
in `_fileStream` since it is useful to perform various file operations. However, it does not own the
file descriptor, so the rules of engagement for `_handle` from .NET 8.0 still apply. Because of
that, it is not essential to dispose `_sourceStream` in this case, but a good practice since it will
suppress its finalizer. Also the memory-mapped `_file` is created instructed to leave the file
handle open to prevent the closure of the file descriptor when the memory-mapped file is re-created.

## Windows, all frameworks

On Windows, the file descriptor is emulated by `PythonFileManager`, the file handle is not
duplicated and field `_handle` is always null. The associated file stream is retrieved from
`PythonFileManager` and used to create the memory-mapped file. The file stream is saved in
`_sourceStream`, but since it comes from somewhere else, it must not be disposed here. Therefore the
memory-mapped file is created instructed to leave the file handle open and there is no
`_sourceStream.Dispose` call on disposing `MmapDefault`. The `MemoryMappedFile` constructor will
addref the actual file handle internally, so it is safe to keep using `mmap` even if the original
file stream is closed prematurely. Of course, it is still important to dispose the `mmap` object to
release the reference to the file handle.

## Mono

Mono uses genuine file descriptors, however due to bugs and limitations, it cannot use the
.NET/POSIX mechanics. Therefore, to prevent regressions, it follows the Windows way (to the extent
that it is feasible), but more advanced scenarios will not behave correctly.

*/


[assembly: PythonModule("mmap", typeof(IronPython.Modules.MmapModule))]
namespace IronPython.Modules {
    public static class MmapModule {
        public const int ACCESS_DEFAULT = 0;  // Since Python 3.7
        public const int ACCESS_READ = 1;
        public const int ACCESS_WRITE = 2;
        public const int ACCESS_COPY = 3;

        // Constants that are set in os.py
        private const int SEEK_SET = 0;
        private const int SEEK_CUR = 1;
        private const int SEEK_END = 2;

        [PythonHidden(PlatformsAttribute.PlatformFamily.Windows)]
        public const int MAP_SHARED = 1;
        [PythonHidden(PlatformsAttribute.PlatformFamily.Windows)]
        public const int MAP_PRIVATE = 2;

        [PythonHidden(PlatformsAttribute.PlatformFamily.Windows)]
        public const int PROT_READ = 1;
        [PythonHidden(PlatformsAttribute.PlatformFamily.Windows)]
        public const int PROT_WRITE = 2;
        [PythonHidden(PlatformsAttribute.PlatformFamily.Windows)]
        public const int PROT_EXEC = 4;

        public static readonly int ALLOCATIONGRANULARITY = GetAllocationGranularity();
        public static readonly int PAGESIZE = System.Environment.SystemPageSize;

        public static readonly string __doc__ = null;

        private static string FormatError(int errorCode) {
            return new Win32Exception(errorCode).Message;
        }

        private static Exception WindowsError(int code) {
            return PythonExceptions.CreateThrowable(PythonExceptions.OSError, code, FormatError(code));
        }

        public static PythonType error => PythonExceptions.OSError;

        public static PythonType mmap {
            get {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                    return DynamicHelpers.GetPythonTypeFromType(typeof(MmapWindows));
                }

                return DynamicHelpers.GetPythonTypeFromType(typeof(MmapUnix));
            }
        }


        [PythonType("mmap"), PythonHidden]
        public class MmapUnix : MmapDefault {
            public MmapUnix(CodeContext/*!*/ context, int fileno, long length, int flags = MAP_SHARED, int prot = PROT_WRITE | PROT_READ, int access = ACCESS_DEFAULT, long offset = 0)
                : base(context, fileno, length, null, ToMmapFileAccess(flags, prot, access), offset) { }

            private static MemoryMappedFileAccess ToMmapFileAccess(int flags, int prot, int access) {
                if (access == ACCESS_DEFAULT) {
                    if ((flags & (MAP_PRIVATE | MAP_SHARED)) == 0) {
                        throw PythonOps.OSError(PythonErrorNumber.EINVAL, "Invalid argument");
                    }
                    if ((prot & PROT_WRITE) != 0) {
                        prot |= PROT_READ;
                    }
                    return (prot & (PROT_READ | PROT_WRITE | PROT_EXEC)) switch {
                        PROT_READ => MemoryMappedFileAccess.Read,
                        PROT_READ | PROT_WRITE => (flags & MAP_PRIVATE) == 0 ? MemoryMappedFileAccess.ReadWrite : MemoryMappedFileAccess.CopyOnWrite,
                        PROT_READ | PROT_EXEC => MemoryMappedFileAccess.ReadExecute,
                        PROT_READ | PROT_WRITE | PROT_EXEC when (flags & MAP_PRIVATE) == 0 => MemoryMappedFileAccess.ReadWriteExecute,
                        _ => throw PythonOps.NotImplementedError("this combination of prot is not supported"),
                    };
                } else if (flags != MAP_SHARED || prot != (PROT_WRITE | PROT_READ)) {
                    throw PythonOps.ValueError("mmap can't specify both access and flags, prot.");
                } else {
                    return access switch {
                        ACCESS_READ => MemoryMappedFileAccess.Read,
                        ACCESS_WRITE => MemoryMappedFileAccess.ReadWrite,
                        ACCESS_COPY => MemoryMappedFileAccess.CopyOnWrite,
                        _ => throw PythonOps.ValueError("mmap invalid access parameter"),
                    };
                }
            }
        }


        [PythonType("mmap"), PythonHidden]
        public class MmapWindows : MmapDefault {
            public MmapWindows(CodeContext context, int fileno, long length, string tagname = null, int access = ACCESS_DEFAULT, long offset = 0)
                : base(context, fileno, length, tagname, ToMmapFileAccess(access), offset) { }

            private static MemoryMappedFileAccess ToMmapFileAccess(int access) {
                return access switch {
                    ACCESS_READ => MemoryMappedFileAccess.Read,
                    // On Windows, default access is write-through
                    ACCESS_DEFAULT or ACCESS_WRITE => MemoryMappedFileAccess.ReadWrite,
                    ACCESS_COPY => MemoryMappedFileAccess.CopyOnWrite,
                    _ => throw PythonOps.ValueError("mmap invalid access parameter"),
                };
            }
        }

        [PythonHidden]
        public class MmapDefault : IWeakReferenceable {
            private MemoryMappedFile _file;
            private MemoryMappedViewAccessor _view;
            private long _position;
            private FileStream _sourceStream;

            private readonly long _offset;
            private readonly string _mapName;
            private readonly MemoryMappedFileAccess _fileAccess;
            private readonly SafeFileHandle _handle;

            private volatile bool _isClosed;
            private int _refCount = 1;

            public MmapDefault(CodeContext/*!*/ context, int fileno, long length, string tagname, MemoryMappedFileAccess fileAccess, long offset) {
                _fileAccess = fileAccess;

                if (length < 0) {
                    throw PythonOps.OverflowError("memory mapped size must be positive");
                }
                if (offset < 0) {
                    throw PythonOps.OverflowError("memory mapped offset must be positive");
                }
                if (IntPtr.Size == 4 && length > int.MaxValue) {
                    throw PythonOps.OverflowError("cannot fit 'long' into an index-sized integer");
                }

                // CPython only allows offsets that are a multiple of ALLOCATIONGRANULARITY
                if (offset % ALLOCATIONGRANULARITY != 0) {
                    throw WindowsError(PythonExceptions._OSError.ERROR_MAPPED_ALIGNMENT);
                }

                // .NET throws on an empty tagname, but CPython treats it as null.
                _mapName = tagname == "" ? null : tagname;

                if (fileno == -1 || fileno == 0) {
                    // Map anonymous memory that is not tied to a file.
                    // Note: CPython seems to allow 0 as a file descriptor even though it represents stdin.
                    _offset = 0; // offset is ignored without an underlying file
                    _sourceStream = null;

                    // work around the .NET bug whereby CreateOrOpen throws on a null mapName
                    if (_mapName is null) {
                        _file = MemoryMappedFile.CreateNew(null, length, _fileAccess);
                    } else {
                        Debug.Assert(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));
                        _file = MemoryMappedFile.CreateOrOpen(_mapName, length, _fileAccess);
                    }
                } else {
                    // Memory-map an actual file
                    _offset = offset;

                    PythonContext pContext = context.LanguageContext;
                    if (pContext.FileManager.TryGetStreams(fileno, out StreamBox streams)) {
                        Stream stream = streams.ReadStream;
                        if (stream is FileStream fs) {
                            _sourceStream = fs;
                        } else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
                            // use file descriptor
#if NET8_0_OR_GREATER
                            // On .NET 8.0+ we can create a MemoryMappedFile directly from a file descriptor
                            stream.Flush();
                            CheckFileAccessAndSize(stream);
                            fileno = Dup(fileno);
                            _handle = new SafeFileHandle((IntPtr)fileno, ownsHandle: true);
                            _file = MemoryMappedFile.CreateFromFile(_handle, _mapName, stream.Length, _fileAccess, HandleInheritability.None, leaveOpen: true);
#else
                            // On .NET 6.0 on POSIX we need to create a FileStream from the file descriptor
                            fileno = Dup(fileno);
                            _handle = new SafeFileHandle((IntPtr)fileno, ownsHandle: true);
                            FileAccess fa = stream.CanWrite ? stream.CanRead ? FileAccess.ReadWrite : FileAccess.Write : FileAccess.Read;
                            // This FileStream constructor may or may not work on Mono, but on Mono streams.ReadStream is FileStream
                            // (unless dupped in some cases, which are unsupported anyway)
                            // so Mono should not be in this else-branch
                            _sourceStream = new FileStream(new SafeFileHandle((IntPtr)fileno, ownsHandle: false), access: fa);
#endif
                        }
                        // otherwise leaves _file as null and _sourceStream as null
                    } else {
                        throw PythonOps.OSError(PythonExceptions._OSError.ERROR_INVALID_BLOCK, "Bad file descriptor");
                    }

                    if (_file is null) {
                        // create _file form _sourceStream
                        if (_sourceStream is null) {
                            throw WindowsError(PythonExceptions._OSError.ERROR_INVALID_HANDLE);
                        }

                        if (length == 0) {
                            length = _sourceStream.Length - _offset;
                        }

                        CheckFileAccessAndSize(_sourceStream);

                        long capacity = checked(_offset + length);

                        // Enlarge the file as needed.
                        if (capacity > _sourceStream.Length) {
                            if (_sourceStream.CanWrite) {
                                _sourceStream.SetLength(capacity);
                            } else {
                                throw WindowsError(PythonExceptions._OSError.ERROR_NOT_ENOUGH_MEMORY);
                            }
                        }

                        _file = CreateFromFile(
                            _sourceStream,
                            _mapName,
                            _sourceStream.Length,
                            _fileAccess,
                            HandleInheritability.None,
                            leaveOpen: true);
                    }
                }

                try {
                    _view = _file.CreateViewAccessor(_offset, length, _fileAccess);
                } catch {
                    _file.Dispose();
                    _file = null;
                    CloseFileHandle();
                    throw;
                }
                _position = 0L;

                void CheckFileAccessAndSize(Stream stream) {
                    bool isValid = _fileAccess switch {
                        MemoryMappedFileAccess.Read => stream.CanRead,
                        MemoryMappedFileAccess.ReadWrite => stream.CanRead && stream.CanWrite,
                        MemoryMappedFileAccess.CopyOnWrite => stream.CanRead,
                        MemoryMappedFileAccess.ReadExecute => stream.CanRead,
                        MemoryMappedFileAccess.ReadWriteExecute => stream.CanRead && stream.CanWrite,
                        _ => false
                    };

                    try {
                        if (!isValid) {
                            throw PythonOps.OSError(PythonExceptions._OSError.ERROR_ACCESS_DENIED, "Invalid access mode");
                        }

                        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                            // Unix map does not support increasing size on open
                            if (length != 0 && _offset + length > stream.Length) {
                                throw PythonOps.ValueError("mmap length is greater than file size");
                            }
                        }
                        if (length == 0 && stream.Length == 0) {
                            throw PythonOps.ValueError("cannot mmap an empty file");
                        }
                        if (_offset >= stream.Length) {
                            throw PythonOps.ValueError("mmap offset is greater than file size");
                        }
                    } catch {
                        CloseFileHandle();
                        throw;
                    }
                }
            }  // end of constructor


            // TODO: Move to PythonNT - POSIX
            private static int Dup(int fd) {
                int fd2 = Mono.Unix.Native.Syscall.dup(fd);
                if (fd2 == -1) throw PythonNT.GetLastUnixError();

                try {
                    // set close-on-exec flag
                    int flags = Mono.Unix.Native.Syscall.fcntl(fd2, Mono.Unix.Native.FcntlCommand.F_GETFD);
                    if (flags == -1) throw PythonNT.GetLastUnixError();
    
                    const int FD_CLOEXEC = 1;  // TODO: Move to module fcntl
                    flags |= FD_CLOEXEC;
                    flags = Mono.Unix.Native.Syscall.fcntl(fd2, Mono.Unix.Native.FcntlCommand.F_SETFD, flags);
                    if (flags == -1) throw PythonNT.GetLastUnixError();
                } catch {
                    Mono.Unix.Native.Syscall.close(fd2);
                    throw;
                }

                return fd2;
            }


            public object __len__() {
                using (new MmapLocker(this)) {
                    return ReturnLong(_view.Capacity);
                }
            }

            public int this[long index] {
                get {
                    using (new MmapLocker(this)) {
                        CheckIndex(index);

                        return _view.ReadByte(index);
                    }
                }

                set {
                    using (new MmapLocker(this)) {
                        EnsureWritable();
                        CheckIndex(index);

                        _view.Write(index, (byte)value);
                    }
                }
            }

            public Bytes this[Slice slice] {
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
                            return Bytes.Empty;
                        }

                        var bytes = new byte[count];

                        for (var i = 0; i < count; i++) {
                            bytes[i] = _view.ReadByte(start);
                            start += step;
                        }

                        return Bytes.Make(bytes);
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
                        if (value.Count != count) {
                            throw PythonOps.IndexError("mmap slice assignment is wrong size");
                        } else if (count == 0) {
                            return;
                        }

                        byte[] data = value.UnsafeByteArray;

                        if (step == 1) {
                            _view.WriteArray(start, data, 0, value.Count);
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

            public void __delitem__(Slice slice) {
                using (new MmapLocker(this)) {
                    throw PythonOps.TypeError("mmap object doesn't support slice deletion");
                }
            }

            public object __enter__() {
                return this;
            }

            public void __exit__(CodeContext/*!*/ context, params object[] excinfo) {
                close();
            }

            public bool closed => _isClosed;

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
                    CloseFileHandle();
                    _sourceStream = null;
                    _view = null;
                    _file = null;
                }
            }

            private void CloseFileHandle() {
                if (_handle is not null) {
                    // mmap owns _sourceStream too (if any) in this case
                    _sourceStream?.Dispose();
                    _handle.Dispose();
                }
            }

            public object find([NotNone] IBufferProtocol s) {
                using (new MmapLocker(this)) {
                    return FindWorker(s, Position, _view.Capacity);
                }
            }

            public object find([NotNone] IBufferProtocol s, long start) {
                using (new MmapLocker(this)) {
                    return FindWorker(s, start, _view.Capacity);
                }
            }

            public object find([NotNone] IBufferProtocol s, long start, long end) {
                using (new MmapLocker(this)) {
                    return FindWorker(s, start, end);
                }
            }

            private object FindWorker(IBufferProtocol data, long start, long end) {
                using var pythonBuffer = data.GetBuffer();
                var s = pythonBuffer.AsReadOnlySpan();

                start = PythonOps.FixSliceIndex(start, _view.Capacity);
                end = PythonOps.FixSliceIndex(end, _view.Capacity);

                if (s.Length == 0) {
                    return start <= end ? ReturnLong(start) : -1;
                }

                long findLength = end - start;
                if (s.Length > findLength) {
                    return -1;
                }

                int index = -1;
                int bufferLength = Math.Max(s.Length, PAGESIZE);

                if (findLength <= bufferLength * 2) {
                    // In this case, the search area is not significantly larger than s, so we only need to
                    // allocate a single string to search through.
                    byte[] buffer = new byte[findLength];
                    _view.ReadArray(start, buffer, 0, (int)findLength);

                    index = buffer.AsSpan().IndexOf(s);
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
                        var combinedBuffer = CombineBytes(buffer0, buffer1, bytesRead);
                        index = combinedBuffer.AsSpan().IndexOf(s);

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

            public Bytes read() => read(-1);

            public Bytes read(int len) {
                using (new MmapLocker(this)) {
                    long pos = Position;

                    if (len < 0) {
                        len = checked((int)(_view.Capacity - pos));
                    } else if (len > _view.Capacity - pos) {
                        len = checked((int)(_view.Capacity - pos));
                    }

                    if (len == 0) {
                        return Bytes.Empty;
                    }

                    byte[] buffer = new byte[len];
                    len = _view.ReadArray(pos, buffer, 0, len);
                    Position = pos + len;

                    return Bytes.Make(buffer);
                }
            }

            public Bytes read(object n) {
                // this overload is needed to prevent cast of double to int - https://github.com/IronLanguages/ironpython2/issues/547
                if (n is null) return read(-1);
                throw PythonOps.TypeError($"integer argument expected, got {PythonOps.GetPythonTypeName(n)}");
            }

            public int read_byte() {
                using (new MmapLocker(this)) {
                    long pos = Position;

                    if (pos >= _view.Capacity) {
                        throw PythonOps.ValueError("read byte out of range");
                    }

                    byte res = _view.ReadByte(pos);
                    Position = pos + 1;

                    return res;
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
                    if (_fileAccess is not MemoryMappedFileAccess.ReadWrite and not MemoryMappedFileAccess.ReadWriteExecute) {
                        throw PythonOps.TypeError("mmap can't resize a readonly or copy-on-write memory map.");
                    }

                    if (_sourceStream == null) {
                        if (_handle is not null && !_handle.IsInvalid
                          && (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || RuntimeInformation.IsOSPlatform(OSPlatform.Linux))) {
                            // resize on Posix platforms
                            PythonNT.ftruncateUnix(unchecked((int)_handle.DangerousGetHandle()), newsize);
                        }
                        // resizing is not supported without an underlying file
                        throw WindowsError(PythonExceptions._OSError.ERROR_INVALID_PARAMETER);
                    }

                    if (newsize == 0) {
                        // resizing to an empty mapped region is not allowed
                        throw WindowsError(_offset == 0
                            ? PythonExceptions._OSError.ERROR_ACCESS_DENIED
                            : PythonExceptions._OSError.ERROR_FILE_INVALID
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

                        var leaveOpen = true;
                        if (!_sourceStream.CanWrite) {
                            _sourceStream = new FileStream(_sourceStream.Name, FileMode.OpenOrCreate, FileAccess.ReadWrite);
                            leaveOpen = false;
                        }

                        // Resize the file as needed.
                        if (capacity != _sourceStream.Length) {
                            _sourceStream.SetLength(capacity);
                        }

                        _file = CreateFromFile(
                            _sourceStream,
                            _mapName,
                            _sourceStream.Length,
                            _fileAccess,
                            HandleInheritability.None,
                            leaveOpen);

                        _view = _file.CreateViewAccessor(_offset, newsize, _fileAccess);
                    } catch {
                        close();
                        throw;
                    }
                }
            }

            public object rfind([NotNone] IBufferProtocol s) {
                using (new MmapLocker(this)) {
                    return RFindWorker(s, Position, _view.Capacity);
                }
            }

            public object rfind([NotNone] IBufferProtocol s, long start) {
                using (new MmapLocker(this)) {
                    return RFindWorker(s, start, _view.Capacity);
                }
            }

            public object rfind([NotNone] IBufferProtocol s, long start, long end) {
                using (new MmapLocker(this)) {
                    return RFindWorker(s, start, end);
                }
            }

            private object RFindWorker(IBufferProtocol bufferProtocol, long start, long end) {
                using var pythonBuffer = bufferProtocol.GetBuffer();
                var s = pythonBuffer.AsReadOnlySpan();

                start = PythonOps.FixSliceIndex(start, _view.Capacity);
                end = PythonOps.FixSliceIndex(end, _view.Capacity);

                if (s.Length == 0) {
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

                    index = buffer.AsSpan().LastIndexOf(s);
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
                        var combinedBuffer = CombineBytes(buffer0, buffer1, bytesRead);
                        index = combinedBuffer.AsSpan().LastIndexOf(s);

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

            public void seek(long pos, int whence = SEEK_SET) {
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
                    if (_handle is not null && (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || RuntimeInformation.IsOSPlatform(OSPlatform.Linux))) {
                        return GetFileSizeUnix(_handle);
                    }
                    if (_sourceStream == null) return ReturnLong(_view.Capacity);
                    return ReturnLong(new FileInfo(_sourceStream.Name).Length);
                }
            }

            public object tell() {
                using (new MmapLocker(this)) {
                    return ReturnLong(Position);
                }
            }

            public int write([NotNone] IBufferProtocol s) {
                using var buffer = s.GetBuffer();
                using (new MmapLocker(this)) {
                    EnsureWritable();

                    long pos = Position;

                    if (_view.Capacity - pos < buffer.AsReadOnlySpan().Length) {
                        throw PythonOps.ValueError("data out of range");
                    }

                    byte[] data = buffer.AsUnsafeArray() ?? buffer.ToArray();
                    _view.WriteArray(pos, data, 0, data.Length);

                    Position = pos + data.Length;

                    return data.Length;
                }
            }

            public void write_byte(int s) {
                if (s < byte.MinValue || s > byte.MaxValue) throw PythonOps.OverflowError("unsigned byte integer is less than minimum");

                using (new MmapLocker(this)) {
                    EnsureWritable();

                    long pos = Position;
                    if (Position >= _view.Capacity) {
                        throw PythonOps.ValueError("write byte out of range");
                    }

                    _view.Write(pos, (byte)s);
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
                if (_fileAccess is MemoryMappedFileAccess.Read or MemoryMappedFileAccess.ReadExecute) {
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

            private static byte[] CombineBytes(byte[] buffer0, byte[] buffer1, int length1) {
                if (length1 == 0) return buffer0;
                var res = new byte[buffer0.Length + length1];
                buffer0.CopyTo(res, 0);
                Array.Copy(buffer1, 0, res, buffer0.Length, length1);
                return res;
            }

            internal Bytes GetSearchString() {
                using (new MmapLocker(this)) {
                    return this[new Slice(0, null)];
                }
            }

            [SupportedOSPlatform("linux"), SupportedOSPlatform("macos")]
            private static long GetFileSizeUnix(SafeFileHandle handle) {
                long size;
                if (handle.IsInvalid) {
                    throw PythonOps.OSError(PythonExceptions._OSError.ERROR_INVALID_HANDLE, "Invalid file handle");
                }

                if (Mono.Unix.Native.Syscall.fstat((int)handle.DangerousGetHandle(), out Mono.Unix.Native.Stat status) == 0) {
                    size = status.st_size;
                } else {
                    Mono.Unix.Native.Errno errno = Mono.Unix.Native.Stdlib.GetLastError();
                    string msg = Mono.Unix.UnixMarshal.GetErrorDescription(errno);
                    int error = Mono.Unix.Native.NativeConvert.FromErrno(errno);
                    throw PythonOps.OSError(error, msg);
                }

                return size;
            }

            #endregion

            #region Synchronization

            private void EnsureOpen() {
                if (_isClosed) {
                    throw PythonOps.ValueError("mmap closed or invalid");
                }
            }

            private struct MmapLocker : IDisposable {
                private readonly MmapDefault _mmap;

                public MmapLocker(MmapDefault mmap) {
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

            #region IWeakReferenceable Members

            private WeakRefTracker _tracker;

            WeakRefTracker IWeakReferenceable.GetWeakRef() {
                return _tracker;
            }

            bool IWeakReferenceable.SetWeakRef(WeakRefTracker value) {
                return Interlocked.CompareExchange(ref _tracker, value, null) == null;
            }

            void IWeakReferenceable.SetFinalizer(WeakRefTracker value) {
                _tracker = value;
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

        [DllImport("kernel32", SetLastError = true)]
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

        private static MemoryMappedFile CreateFromFile(System.IO.FileStream fileStream, string mapName, long capacity, System.IO.MemoryMappedFiles.MemoryMappedFileAccess access, System.IO.HandleInheritability inheritability, bool leaveOpen) {
            return MemoryMappedFile.CreateFromFile(fileStream, mapName, capacity, access, inheritability, leaveOpen);
        }
    }
}

#endif
