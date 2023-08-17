// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using Microsoft.Win32.SafeHandles;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

using NotNullWhenAttribute = System.Diagnostics.CodeAnalysis.NotNullWhenAttribute;
using NotNullAttribute = System.Diagnostics.CodeAnalysis.NotNullAttribute;

#if FEATURE_PIPES
using System.IO.Pipes;
#endif

[assembly: PythonModule("nt", typeof(IronPython.Modules.PythonNT))]
namespace IronPython.Modules {
    public static class PythonNT {
        public const string __doc__ = "Provides low-level operating system access for files, the environment, etc...";

        /* TODO: missing functions/classes:
         * Windows:
         * {'execve', '_isdir', 'getlogin', 'get_inheritable', 'statvfs_result', 'readlink', 'stat_float_times', 'getppid',
         * '_getdiskusage', 'execv', 'set_inheritable', 'device_encoding', 'isatty', '_getvolumepathname',
         * 'times_result', 'cpu_count', 'get_handle_inheritable', 'set_handle_inheritable'}
         */

#if FEATURE_PROCESS
        private static readonly Dictionary<int, Process> _processToIdMapping = new Dictionary<int, Process>();
        private static readonly List<int> _freeProcessIds = new List<int>();
        private static int _processCount;
#endif

        private static readonly object _keyFields = new object();
        private static readonly string _keyHaveFunctions = "_have_functions";
        private static readonly Encoding _utf8Encoding;
        private static readonly Encoding _mbcsEncoding;

        static PythonNT() {
            // TODO: Python 3.6: use sys.getfilesystemencodeerrors()

            _mbcsEncoding = Encoding.GetEncoding(0); // on errors does diacritics stripping if possible else replace

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                _utf8Encoding = new PythonSurrogatePassEncoding(Encoding.UTF8);
            } else {
                // TODO: Verify: CPython uses surrogateescape, but .NET will handle using replace
                // so paths produced as output will never have surrogates, but have errors replaced by U+FFFD
                // and paths provided as input will have any surrogates replaced by U+FFFD or ?
                // Using surrogateescape here properly validates bytes input but does not guarantee safe roundtrip
                _utf8Encoding = new PythonSurrogateEscapeEncoding(Encoding.UTF8);
            }
        }

        [SpecialName]
        public static void PerformModuleReload([NotNone] PythonContext context, [NotNone] PythonDictionary dict) {
            var have_functions = new PythonList();
            if (Environment.OSVersion.Platform == PlatformID.Win32NT) {
                have_functions.Add("MS_WINDOWS");
            }

            context.GetOrCreateModuleState(_keyFields, () => {
                dict.Add(_keyHaveFunctions, have_functions);
                return dict;
            });
        }

        #region Public API Surface

        [PythonHidden(PlatformsAttribute.PlatformFamily.Unix)]
        public static PythonTuple _getdiskusage([NotNone] string path) {
            var driveInfo = new DriveInfo(path);
            return PythonTuple.MakeTuple((BigInteger)driveInfo.TotalSize, (BigInteger)driveInfo.AvailableFreeSpace);
        }

        [DllImport("kernel32.dll", EntryPoint = "GetFinalPathNameByHandleW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetFinalPathNameByHandle([In] SafeFileHandle hFile, [Out] StringBuilder lpszFilePath, [In] int cchFilePath, [In] int dwFlags);

        [SupportedOSPlatform("windows"), PythonHidden(PlatformsAttribute.PlatformFamily.Unix)]
        public static string _getfinalpathname([NotNone] string path) {
            var hFile = CreateFile(path, 0, 0, IntPtr.Zero, OPEN_EXISTING, FILE_FLAG_BACKUP_SEMANTICS, IntPtr.Zero);
            if (hFile.IsInvalid) {
                throw GetLastWin32Error(path);
            }

            const int MAX_PATH_LEN = 10000;
            StringBuilder sb = new StringBuilder(MAX_PATH_LEN);
            if (GetFinalPathNameByHandle(hFile, sb, MAX_PATH_LEN, 0) == 0) {
                throw GetLastWin32Error(path);
            }
            return sb.ToString();
        }

        public static string _getfullpathname(CodeContext/*!*/ context, [NotNone] string/*!*/ path) {
            PlatformAdaptationLayer pal = context.LanguageContext.DomainManager.Platform;

            try {
                return pal.GetFullPath(path);
            } catch (ArgumentException) {
                // .NET validates the path, CPython doesn't... so we replace invalid chars with
                // Char.Maxvalue, get the full path, and then replace the Char.Maxvalue's back w/
                // their original value.
                string newdir = path;

                if (IsWindows()) {
                    if (newdir.Length >= 2 && newdir[1] == ':' &&
                        (newdir[0] < 'a' || newdir[0] > 'z') && (newdir[0] < 'A' || newdir[0] > 'Z')) {
                        // invalid drive, .NET will reject this
                        if (newdir.Length == 2) {
                            return newdir + Path.DirectorySeparatorChar;
                        } else if (newdir[2] == Path.DirectorySeparatorChar) {
                            return newdir;
                        } else {
                            return newdir.Substring(0, 2) + Path.DirectorySeparatorChar + newdir.Substring(2);
                        }
                    }
                    if (newdir.Length > 2 && newdir.IndexOf(':', 2) != -1) {
                        // : is an invalid char if it's not in the 2nd position
                        newdir = newdir.Substring(0, 2) + newdir.Substring(2).Replace(':', Char.MaxValue);
                    }

                    if (newdir.Length > 0 && newdir[0] == ':') {
                        newdir = Char.MaxValue + newdir.Substring(1);
                    }
                }

                foreach (char c in Path.GetInvalidPathChars()) {
                    newdir = newdir.Replace(c, Char.MaxValue);
                }

#if NETCOREAPP || NETSTANDARD
                foreach (char c in invalidPathChars) {
                    newdir = newdir.Replace(c, Char.MaxValue);
                }
#endif

                foreach (char c in Path.GetInvalidFileNameChars()) {
                    // don't replace the volume or directory separators
                    if (c == Path.VolumeSeparatorChar || c == Path.DirectorySeparatorChar) continue;
                    newdir = newdir.Replace(c, Char.MaxValue);
                }

                // walk backwards through the path replacing the same characters.  We should have
                // only updated the directory leaving the filename which we're fixing.
                string res = pal.GetFullPath(newdir);
                int curDir = path.Length;
                for (int curRes = res.Length - 1; curRes >= 0; curRes--) {
                    if (res[curRes] == Char.MaxValue) {
                        for (curDir--; curDir >= 0; curDir--) {
                            if (newdir[curDir] == Char.MaxValue) {
                                res = res.Substring(0, curRes) + path[curDir] + res.Substring(curRes + 1);
                                break;
                            }
                        }
                    }
                }

                return res;
            }

            static bool IsWindows() {
                return Environment.OSVersion.Platform == PlatformID.Win32NT ||
                    Environment.OSVersion.Platform == PlatformID.Win32S ||
                    Environment.OSVersion.Platform == PlatformID.Win32Windows;
            }
        }

        public static Bytes _getfullpathname(CodeContext/*!*/ context, [NotNone] Bytes path)
            => _getfullpathname(context, path.ToFsString(context)).ToFsBytes(context);

        public static Bytes _getfullpathname(CodeContext/*!*/ context, object? path)
            => _getfullpathname(context, ConvertToFsString(context, path, nameof(path))).ToFsBytes(context);

#if FEATURE_PROCESS
        public static void abort() {
            System.Environment.FailFast("IronPython os.abort");
        }
#endif

        /// <summary>
        /// Checks for the specific permissions, provided by the mode parameter, are available for the provided path.  Permissions can be:
        ///
        /// F_OK: Check to see if the file exists
        /// R_OK | W_OK | X_OK: Check for the specific permissions.  Only W_OK is respected.
        /// </summary>
        [Documentation("access(path, mode, *, dir_fd=None, effective_ids=False, follow_symlinks=True)")]
        public static bool access(CodeContext/*!*/ context, [NotNone] string path, int mode, [ParamDictionary, NotNone] IDictionary<string, object> kwargs) {
            if (path == null) throw PythonOps.TypeError("expected string, got None");

            foreach (var pair in kwargs) {
                switch (pair.Key) {
                    case "dir_fd":
                    case "follow_symlinks":
                        // TODO: implement these!
                        break;
                    case "effective_ids":
                        if (PythonOps.IsTrue(pair.Value))
                            throw PythonOps.NotImplementedError("access: effective_ids unavailable on this platform");
                        break;
                    default:
                        throw PythonOps.TypeError("'{0}' is an invalid keyword argument for this function", pair.Key);
                }
            }

#if FEATURE_FILESYSTEM
            try {
                FileAttributes fa = File.GetAttributes(path);
                if (mode == F_OK) {
                    return true;
                }
                // match the behavior of the VC C Runtime
                if ((fa & FileAttributes.Directory) != 0) {
                    // directories have read & write access
                    return true;
                }
                if ((fa & FileAttributes.ReadOnly) != 0 && (mode & W_OK) != 0) {
                    // want to write but file is read-only
                    return false;
                }
                return true;
            } catch (ArgumentException) {
            } catch (PathTooLongException) {
            } catch (NotSupportedException) {
            } catch (FileNotFoundException) {
            } catch (DirectoryNotFoundException) {
            } catch (IOException) {
            } catch (UnauthorizedAccessException) {
            }
            return false;
#else
            throw new NotImplementedException();
#endif
        }

        [Documentation("")]
        public static bool access(CodeContext context, [NotNone] Bytes path, int mode, [ParamDictionary, NotNone] IDictionary<string, object> kwargs)
            => access(context, path.ToFsString(context), mode, kwargs);

        [Documentation("")]
        public static bool access(CodeContext context, object? path, int mode, [ParamDictionary, NotNone] IDictionary<string, object> kwargs)
            => access(context, ConvertToFsString(context, path, nameof(path)), mode, kwargs);

#if FEATURE_FILESYSTEM

        public static void chdir([NotNone] string path) {
            if (String.IsNullOrEmpty(path)) {
                throw PythonOps.OSError(PythonExceptions._OSError.ERROR_INVALID_NAME, "Path cannot be an empty string", path, PythonExceptions._OSError.ERROR_INVALID_NAME);
            }

            try {
                Directory.SetCurrentDirectory(path);
            } catch (Exception e) {
                throw ToPythonException(e, path);
            }
        }

        public static void chdir(CodeContext context, [NotNone] Bytes path)
            => chdir(path.ToFsString(context));

        public static void chdir(CodeContext context, object? path)
            => chdir(ConvertToFsString(context, path, nameof(path)));

        // Isolate Mono.Unix from the rest of the method so that we don't try to load the Mono.Unix assembly on Windows.
        private static void chmodUnix(string path, int mode) {
            if (Mono.Unix.Native.Syscall.chmod(path, Mono.Unix.Native.NativeConvert.ToFilePermissions((uint)mode)) == 0) return;
            throw GetLastUnixError(path);
        }

        [Documentation("chmod(path, mode, *, dir_fd=None, follow_symlinks=True)")]
        public static void chmod([NotNone] string path, int mode, [ParamDictionary, NotNone] IDictionary<string, object> kwargs) {
            foreach (var key in kwargs.Keys) {
                switch (key) {
                    case "dir_fd":
                    case "follow_symlinks":
                        // TODO: implement these!
                        break;
                    default:
                        throw PythonOps.TypeError("'{0}' is an invalid keyword argument for this function", key);
                }
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                try {
                    FileInfo fi = new FileInfo(path);
                    if ((mode & S_IWRITE) != 0) {
                        fi.Attributes &= ~(FileAttributes.ReadOnly);
                    } else {
                        fi.Attributes |= FileAttributes.ReadOnly;
                    }
                } catch (Exception e) {
                    throw ToPythonException(e, path);
                }
            } else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                chmodUnix(path, mode);
            } else {
                throw new PlatformNotSupportedException();
            }
        }

        [Documentation("")]
        public static void chmod(CodeContext context, [NotNone] Bytes path, int mode, [ParamDictionary, NotNone] IDictionary<string, object> kwargs)
            => chmod(path.ToFsString(context), mode, kwargs);

        [Documentation("")]
        public static void chmod(CodeContext context, object? path, int mode, [ParamDictionary, NotNone] IDictionary<string, object> kwargs)
            => chmod(ConvertToFsString(context, path, nameof(path)), mode, kwargs);

#endif

        public static void close(CodeContext/*!*/ context, int fd) {
            PythonFileManager fileManager = context.LanguageContext.FileManager;
            if (fileManager.TryGetFileFromId(fd, out PythonIOModule.FileIO? file)) {
                file.CloseStreams(fileManager);
                fileManager.RemoveObjectOnId(fd);
            } else {
                Stream stream = fileManager.GetStreamFromId(fd);
                fileManager.RemoveObjectOnId(fd);
                fileManager.DerefAndCloseIfLast(stream);
            }
        }

        public static void closerange(CodeContext/*!*/ context, int fd_low, int fd_high) {
            for (var fd = fd_low; fd <= fd_high; fd++) {
                try {
                    close(context, fd);
                } catch (OSException) {
                    // ignore errors on close
                }
            }
        }

        public static int dup(CodeContext/*!*/ context, int fd) {
            PythonFileManager fileManager = context.LanguageContext.FileManager;

            object obj = fileManager.GetObjectFromId(fd); // OSError if fd not valid
            if (obj is PythonIOModule.FileIO file) {
                var file2 = new PythonIOModule.FileIO(context, file.fileno(context)) { closefd = false };
                int fd2 = fileManager.AddFile(file2);
                fileManager.EnsureRef(file._readStream);
                fileManager.AddRef(file2._readStream);
                return fd2;
            } else {
                var stream = (Stream)obj;
                fileManager.EnsureRef(stream);
                fileManager.AddRef(stream);
                return fileManager.AddStream(stream);
            }
        }


        public static int dup2(CodeContext/*!*/ context, int fd, int fd2) {
            PythonFileManager fileManager = context.LanguageContext.FileManager;

            object obj = fileManager.GetObjectFromId(fd); // OSError if fd not valid
            if (fd == fd2) {
                return fd2;
            }

            if (!fileManager.ValidateFdRange(fd2)) {
                throw PythonOps.OSError(9, "Bad file descriptor");
            }

            if (fileManager.TryGetObjectFromId(fd2, out _)) {
                close(context, fd2);
            }

            // TODO: race condition: `open` or `dup` on another thread may occupy fd2 

            if (obj is PythonIOModule.FileIO file) {
                var file2 = new PythonIOModule.FileIO(context, file.fileno(context)) { closefd = false };
                fileManager.AddFile(fd2, file2);
                fileManager.EnsureRef(file._readStream);
                fileManager.AddRef(file2._readStream);
                return fd2;
            } else {
                var stream = (Stream)obj;
                fileManager.EnsureRef(stream);
                fileManager.AddRef(stream);
                return fileManager.AddStream(fd2, stream);
            }
        }

#if FEATURE_PROCESS
        /// <summary>
        /// single instance of environment dictionary is shared between multiple runtimes because the environment
        /// is shared by multiple runtimes.
        /// </summary>
        public static readonly object environ = new PythonDictionary(new EnvironmentDictionaryStorage());
#endif

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
        public static readonly PythonType error = Builtin.OSError;

        public static void _exit(CodeContext/*!*/ context, int status) {
            context.LanguageContext.DomainManager.Platform.TerminateScriptExecution(status);
        }

        public static object fspath(CodeContext context, [AllowNull] object path)
            => PythonOps.FsPath(path);

        [LightThrowing]
        public static object fstat(CodeContext/*!*/ context, int fd) {
            PythonFileManager fileManager = context.LanguageContext.FileManager;

            fileManager.TryGetObjectFromId(fd, out object? obj);
            if (obj is PythonIOModule.FileIO file) {
                if (file.IsConsole) return new stat_result(0x2000);
                if (StatStream(file._readStream) is not null and var res) return res;
            } else if (obj is Stream stream && StatStream(stream) is not null and var res) {
                return res;
            }
            return LightExceptions.Throw(PythonOps.OSError(9, "Bad file descriptor"));

            static object? StatStream(Stream stream) {
                if (stream is FileStream fs) return lstat(fs.Name, new Dictionary<string, object>(1));
                if (stream is PipeStream) return new stat_result(0x1000);
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                    if (ReferenceEquals(stream, Stream.Null)) return new stat_result(0x2000);
                } else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                    if (IsUnixStream(stream)) return new stat_result(0x1000);
                }
                return null;
            }

            static bool IsUnixStream(Stream stream) {
                return stream is Mono.Unix.UnixStream;
            }
        }

        public static void fsync(CodeContext context, int fd) {
            PythonFileManager fileManager = context.LanguageContext.FileManager;
            var pf = fileManager.GetFileFromId(fd);
            try {
                pf.flush(context);
            } catch (Exception ex) when (ex is ValueErrorException || ex is IOException) {
                throw PythonOps.OSError(9, "Bad file descriptor");
            }
        }

        public static string getcwd(CodeContext/*!*/ context) {
            return context.LanguageContext.DomainManager.Platform.CurrentDirectory;
        }

        public static Bytes getcwdb(CodeContext/*!*/ context)
            => getcwd(context).ToFsBytes(context);

#if NETCOREAPP || NETSTANDARD
        private static readonly char[] invalidPathChars = new char[] { '\"', '<', '>' };
#endif

#if FEATURE_PROCESS
        public static int getpid() {
            return System.Diagnostics.Process.GetCurrentProcess().Id;
        }
#endif

        [Documentation("link(src, dst, *, src_dir_fd=None, dst_dir_fd=None, follow_symlinks=True)")]
        public static void link([NotNone] string src, [NotNone] string dst, [ParamDictionary, NotNone] IDictionary<string, object> kwargs) {
            foreach (var key in kwargs.Keys) {
                switch (key) {
                    case "src_dir_fd":
                    case "dst_dir_fd":
                    case "follow_symlinks":
                        // TODO: implement these!
                        break;
                    default:
                        throw PythonOps.TypeError("'{0}' is an invalid keyword argument for this function", key);
                }
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                linkWindows(src, dst);
            } else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                linkUnix(src, dst);
            } else {
                throw new NotImplementedException();
            }

            static void linkWindows(string src, string dst) {
                if (!CreateHardLink(dst, src, IntPtr.Zero))
                    throw GetLastWin32Error(src, dst);
            }

            static void linkUnix(string src, string dst) {
                if (Mono.Unix.Native.Syscall.link(src, dst) == 0) return;
                throw GetLastUnixError(src, dst);
            }
        }

        public static bool isatty(CodeContext context, int fd) {
            if (context.LanguageContext.FileManager.TryGetFileFromId(fd, out var file))
                return file.isatty(context);
            return false;
        }

        [Documentation("")]
        public static void link(CodeContext context, [NotNone] Bytes src, [NotNone] Bytes dst, [ParamDictionary, NotNone] IDictionary<string, object> kwargs)
            => link(src.ToFsString(context), dst.ToFsString(context), kwargs);

        [Documentation("")]
        public static void link(CodeContext context, object? src, object? dst, [ParamDictionary, NotNone] IDictionary<string, object> kwargs)
            => link(ConvertToFsString(context, src, nameof(src)), ConvertToFsString(context, dst, nameof(dst)), kwargs);


        [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CreateHardLink(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

        public static PythonList listdir(CodeContext/*!*/ context, string? path = null) {
            if (path == null) {
                path = getcwd(context);
            }

            if (path == string.Empty) {
                throw PythonOps.OSError(PythonExceptions._OSError.ERROR_PATH_NOT_FOUND, "The system cannot find the path specified", path, PythonExceptions._OSError.ERROR_PATH_NOT_FOUND);
            }

#if !NETFRAMEWORK
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                // .NET Core throws an unhelpful "The parameter is incorrect" error when trying to listdir a file
                if (File.Exists(path)) {
                    throw GetWin32Error(PythonExceptions._OSError.ERROR_DIRECTORY, path);
                }
            }
#endif

            PythonList ret = new PythonList();
            try {
                addBase(context.LanguageContext.DomainManager.Platform.GetFileSystemEntries(path, "*"), ret);
                return ret;
            } catch (Exception e) {
                throw ToPythonException(e, path);
            }
        }

        public static PythonList listdir(CodeContext context, [NotNone] Bytes path) {
            PythonList ret = new PythonList();
            foreach (object? item in listdir(context, path.ToFsString(context))) {
                ret.AddNoLock(((string)item!).ToFsBytes(context));
            }
            return ret;
        }

        public static PythonList listdir(CodeContext context, object? path)
            => listdir(context, ConvertToFsString(context, path, nameof(path)));

        public static BigInteger lseek(CodeContext context, int fd, long offset, int whence) {
            var file = context.LanguageContext.FileManager.GetFileFromId(fd);

            return file.seek(context, offset, whence);
        }

        [Documentation("lstat(path, *, dir_fd=None) -> stat_result\n\n" +
            "Like stat(), but do not follow symbolic links.\n" +
            "Equivalent to calling stat(...) with follow_symlinks=False.")]
        [LightThrowing]
        public static object lstat([NotNone] string path, [ParamDictionary, NotNone] IDictionary<string, object> kwargs) {
            if (kwargs.ContainsKey("follow_symlinks"))
                throw PythonOps.TypeError("'follow_symlinks' is an invalid keyword argument for lstat(...)");

            kwargs["follow_symlinks"] = false;
            return stat(path, kwargs);
        }

        [LightThrowing, Documentation("")]
        public static object lstat(CodeContext context, [NotNone] Bytes path, [ParamDictionary, NotNone] IDictionary<string, object> kwargs)
            => lstat(path.ToFsString(context), kwargs);


        [LightThrowing, Documentation("")]
        public static object lstat(CodeContext context, object? path, [ParamDictionary, NotNone] IDictionary<string, object> kwargs)
            => lstat(ConvertToFsString(context, path, nameof(path)), kwargs);

        [PythonType]
        public sealed class DirEntry {
            private readonly CodeContext context;
            private readonly FileSystemInfo info;
            private readonly bool asBytes;

            internal DirEntry(CodeContext context, FileSystemInfo info, bool asBytes) {
                this.context = context;
                this.info = info;
                this.asBytes = asBytes;
            }

            public object path => asBytes ? info.FullName.ToFsBytes(context) : info.FullName;
            public object name => asBytes ? info.Name.ToFsBytes(context) : info.Name;

            [LightThrowing]
            public object? inode() {
                var obj = stat(follow_symlinks: false);
                if (obj is stat_result res) return res.st_ino;
                return obj;
            }

            public bool is_dir(bool follow_symlinks = true) => info.Attributes.HasFlag(FileAttributes.Directory);

            public bool is_file(bool follow_symlinks = true) => !is_dir();

            public bool is_symlink() => throw new NotImplementedException();

            [LightThrowing]
            public object? stat(bool follow_symlinks = true) => PythonNT.stat(info.FullName, new Dictionary<string, object>());

            public string __repr__(CodeContext context) => $"<DirEntry {PythonOps.Repr(context, name)}>";
        }

        [PythonType, PythonHidden]
        public sealed class ScandirIterator : IEnumerable<DirEntry>, IEnumerator<DirEntry> {
            private readonly CodeContext context;
            private readonly IEnumerator<FileSystemInfo> enumerator;
            private readonly bool asBytes;

            internal ScandirIterator(CodeContext context, IEnumerable<FileSystemInfo> list, bool asBytes) {
                this.context = context;
                enumerator = list.GetEnumerator();
                this.asBytes = asBytes;
            }

            [PythonHidden]
            public DirEntry Current => new DirEntry(context, enumerator.Current, asBytes);

            object IEnumerator.Current => Current;

            [PythonHidden]
            public void Dispose() => enumerator.Dispose();

            [PythonHidden]
            public IEnumerator<DirEntry> GetEnumerator() => this;

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            [PythonHidden]
            public bool MoveNext() => enumerator.MoveNext();

            [PythonHidden]
            public void Reset() => enumerator.Reset();
        }

        public static ScandirIterator scandir(CodeContext context, string? path = null)
            => new ScandirIterator(context, ScandirHelper(context, path), asBytes: false);

        public static ScandirIterator scandir(CodeContext context, [NotNone] IBufferProtocol path)
            => new ScandirIterator(context, ScandirHelper(context, ConvertToFsString(context, path, nameof(path))), asBytes: true);

        private static IEnumerable<FileSystemInfo> ScandirHelper(CodeContext context, string? path) {
            if (path == null) {
                path = getcwd(context);
            }

            if (path == string.Empty) {
                throw PythonOps.OSError(PythonExceptions._OSError.ERROR_PATH_NOT_FOUND, "The system cannot find the path specified", path, PythonExceptions._OSError.ERROR_PATH_NOT_FOUND);
            }

#if !NETFRAMEWORK
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                // .NET Core throws an unhelpful "The parameter is incorrect" error when trying to listdir a file
                if (File.Exists(path)) {
                    throw GetWin32Error(PythonExceptions._OSError.ERROR_DIRECTORY, path);
                }
            }
#endif

            try {
                return new DirectoryInfo(path).EnumerateFileSystemInfos();
            } catch (Exception e) {
                throw ToPythonException(e, path);
            }
        }

#if FEATURE_NATIVE

        [Documentation("symlink(src, dst, target_is_directory=False, *, dir_fd=None)")]
        public static void symlink([NotNone] string src, [NotNone] string dst, [ParamDictionary, NotNone] IDictionary<string, object> kwargs, [NotNone] params object[] args) {
            var numArgs = args.Length;
            CheckOptionalArgsCount(numRegParms: 2, numOptPosParms: 1, numKwParms: 1, numArgs, kwargs.Count);

            bool target_is_directory = numArgs > 0 ? Converter.ConvertToBoolean(args[0]) : false;

            foreach (var kvp in kwargs) {
                switch (kvp.Key) {
                    case nameof(target_is_directory):
                        if (numArgs > 0) throw PythonOps.TypeError("argument for {0}() given by name ('{1}') and position ({2})", nameof(symlink), nameof(target_is_directory), 3);
                        target_is_directory = Converter.ConvertToBoolean(kvp.Value);
                        break;
                    case "dir_fd":
                        throw PythonOps.NotImplementedError("{0} unavailable on this platform", kvp.Key);
                    default:
                        throw PythonOps.TypeError("'{0}' is an invalid keyword argument for {1}()", kvp.Key, nameof(symlink));
                }
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                // TODO: implement this
                throw new NotImplementedException();
            } else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                symlinkUnix(src, dst);
            } else {
                throw new NotImplementedException();
            }

            static void symlinkUnix(string src, string dst) {
                if (Mono.Unix.Native.Syscall.symlink(src, dst) == 0) return;
                throw GetLastUnixError(src, dst);
            }
        }

        [Documentation("")]
        public static void symlink(CodeContext context, [NotNone] Bytes src, [NotNone] Bytes dst, [ParamDictionary, NotNone] IDictionary<string, object> kwargs, [NotNone] params object[] args)
            => symlink(src.ToFsString(context), dst.ToFsString(context), kwargs, args);

        [Documentation("")]
        public static void symlink(CodeContext context, object? src, object? dst, [ParamDictionary, NotNone] IDictionary<string, object> kwargs, [NotNone] params object[] args)
            => symlink(ConvertToFsString(context, src, nameof(src)), ConvertToFsString(context, dst, nameof(dst)), kwargs, args);

        public class uname_result : PythonTuple {
            // TODO: posix: support constructor with a sequence, see construction of stat_result
            public uname_result(string? sysname, string? nodename, string? release, string? version, string? machine) :
                base(new object?[] { sysname, nodename, release, version, machine }) { }

            public string? sysname => (string?)this[0];

            public string? nodename => (string?)this[1];

            public string? release => (string?)this[2];

            public string? version => (string?)this[3];

            public string? machine => (string?)this[4];

            public override string ToString() {
                // TODO: posix: handle null values, see terminal_size.__repr__()
                return $"posix.uname_result(sysname='{sysname}', nodename='{nodename}', release='{release}', version='{version}', machine='{machine}')";
            }
        }

        [PythonHidden(PlatformsAttribute.PlatformFamily.Windows)]
        public static uname_result uname() {
            Mono.Unix.Native.Utsname info;
            Mono.Unix.Native.Syscall.uname(out info);
            return new uname_result(info.sysname, info.nodename, info.release, info.version, info.machine);
        }

        [PythonHidden(PlatformsAttribute.PlatformFamily.Windows)]
        public static BigInteger getuid() {
            return Mono.Unix.Native.Syscall.getuid();
        }

        [PythonHidden(PlatformsAttribute.PlatformFamily.Windows)]
        public static BigInteger geteuid() {
            return Mono.Unix.Native.Syscall.geteuid();
        }

#endif

#if FEATURE_FILESYSTEM
        [Documentation("mkdir(path, mode=511, *, dir_fd=None)")]
        public static void mkdir([NotNone] string path, [ParamDictionary, NotNone] IDictionary<string, object> kwargs, [NotNone] params object[] args) {
            var numArgs = args.Length;
            CheckOptionalArgsCount(numRegParms: 1, numOptPosParms: 1, numKwParms: 1, numArgs, kwargs.Count);

            int mode = numArgs > 0 ? Converter.ConvertToIndex(args[0], throwOverflowError: true) : 511; // 0o777

            foreach (var kvp in kwargs) {
                switch (kvp.Key) {
                    case nameof(mode):
                        if (numArgs > 0) throw PythonOps.TypeError("argument for {0}() given by name ('{1}') and position ({2})", nameof(mkdir), nameof(mode), 2);
                        mode = Converter.ConvertToIndex(kvp.Value, throwOverflowError: true);
                        break;
                    case "dir_fd":
                        throw PythonOps.NotImplementedError("{0} unavailable on this platform", kvp.Key);
                    default:
                        throw PythonOps.TypeError("'{0}' is an invalid keyword argument for {1}()", kvp.Key, nameof(mkdir));
                }
            }

            if (Directory.Exists(path)) throw DirectoryExists();
            // we ignore mode

            try {
                Directory.CreateDirectory(path);
            } catch (Exception e) {
                throw ToPythonException(e, path);
            }
        }

        [Documentation("")]
        public static void mkdir(CodeContext context, [NotNone] Bytes path, [ParamDictionary, NotNone] IDictionary<string, object> kwargs, [NotNone] params object[] args)
            => mkdir(path.ToFsString(context), kwargs, args);

        [Documentation("")]
        public static void mkdir(CodeContext context, object? path, [ParamDictionary, NotNone] IDictionary<string, object> kwargs, [NotNone] params object[] args)
            => mkdir(ConvertToFsString(context, path, nameof(path)), kwargs, args);

        private const int DefaultBufferSize = 4096;

        [Documentation("open(path, flags, mode=511, *, dir_fd=None)")]
        public static object open(CodeContext/*!*/ context, [NotNone] string path, int flags, [ParamDictionary, NotNone] IDictionary<string, object> kwargs, [NotNone] params object[] args) {
            var numArgs = args.Length;
            CheckOptionalArgsCount(numRegParms: 2, numOptPosParms: 1, numKwParms: 1, numArgs, kwargs.Count);

            int mode = numArgs > 0 ? Converter.ConvertToIndex(args[0], throwOverflowError: true) : 511; // 0o777

            foreach (var kvp in kwargs) {
                switch (kvp.Key) {
                    case nameof(mode):
                        if (numArgs > 0) throw PythonOps.TypeError("argument for {0}() given by name ('{1}') and position ({2})", nameof(open), nameof(mode), 3);
                        mode = Converter.ConvertToIndex(kvp.Value, throwOverflowError: true);
                        break;
                    case "dir_fd":
                        throw PythonOps.NotImplementedError("{0} unavailable on this platform", kvp.Key);
                    default:
                        throw PythonOps.TypeError("'{0}' is an invalid keyword argument for {1}()", kvp.Key, nameof(open));
                }
            }

            try {
                FileMode fileMode = FileModeFromFlags(flags);
                FileAccess access = FileAccessFromFlags(flags);
                FileOptions options = FileOptionsFromFlags(flags);
                Stream fs;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && IsNulFile(path)) {
                    fs = Stream.Null;
                } else if (access == FileAccess.Read && (fileMode == FileMode.CreateNew || fileMode == FileMode.Create || fileMode == FileMode.Append)) {
                    // .NET doesn't allow Create/CreateNew w/ access == Read, so create the file, then close it, then
                    // open it again w/ just read access.
                    fs = new FileStream(path, fileMode, FileAccess.Write, FileShare.None);
                    fs.Close();
                    fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, DefaultBufferSize, options);
                } else if (access == FileAccess.ReadWrite && fileMode == FileMode.Append) {
                    fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite, DefaultBufferSize, options);
                } else {
                    fs = new FileStream(path, fileMode, access, FileShare.ReadWrite, DefaultBufferSize, options);
                }

                return context.LanguageContext.FileManager.AddFile(new PythonIOModule.FileIO(context, fs) { name = path, closefd = false });
            } catch (Exception e) {
                throw ToPythonException(e, path);
            }
        }

        [Documentation("")]
        public static object open(CodeContext context, [NotNone] Bytes path, int flags, [ParamDictionary, NotNone] IDictionary<string, object> kwargs, [NotNone] params object[] args)
            => open(context, path.ToFsString(context), flags, kwargs, args);

        [Documentation("")]
        public static object open(CodeContext context, object? path, int flags, [ParamDictionary, NotNone] IDictionary<string, object> kwargs, [NotNone] params object[] args)
            => open(context, ConvertToFsString(context, path, nameof(path)), flags, kwargs, args);

        private static FileOptions FileOptionsFromFlags(int flag) {
            FileOptions res = FileOptions.None;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                if ((flag & O_TEMPORARY) != 0) {
                    res |= FileOptions.DeleteOnClose;
                }
                if ((flag & O_RANDOM) != 0) {
                    res |= FileOptions.RandomAccess;
                }
                if ((flag & O_SEQUENTIAL) != 0) {
                    res |= FileOptions.SequentialScan;
                }
            }

            return res;
        }
#endif

#if FEATURE_PIPES

        private static Tuple<Stream, Stream> CreatePipeStreams() {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                return CreatePipeStreamsUnix();
            } else {
                var inPipe = new AnonymousPipeServerStream(PipeDirection.In);
                var outPipe = new AnonymousPipeClientStream(PipeDirection.Out, inPipe.ClientSafePipeHandle);
                return Tuple.Create<Stream, Stream>(inPipe, outPipe);
            }

            static Tuple<Stream, Stream> CreatePipeStreamsUnix() {
                Mono.Unix.UnixPipes pipes = Mono.Unix.UnixPipes.CreatePipes();
                return Tuple.Create<Stream, Stream>(pipes.Reading, pipes.Writing);
            }
        }

        public static PythonTuple pipe(CodeContext context) {
            var pipeStreams = CreatePipeStreams();

            var inFile = new PythonIOModule.FileIO(context, pipeStreams.Item1) { closefd = false };
            var outFile = new PythonIOModule.FileIO(context, pipeStreams.Item2) { closefd = false };

            return PythonTuple.MakeTuple(
                context.LanguageContext.FileManager.AddFile(inFile),
                context.LanguageContext.FileManager.AddFile(outFile)
            );
        }
#endif

#if FEATURE_PROCESS
        public static void putenv([NotNone] string name, [NotNone] string value) {
            try {
                System.Environment.SetEnvironmentVariable(name, value);
            } catch (Exception e) {
                throw ToPythonException(e);
            }
        }
#endif

        public static Bytes read(CodeContext/*!*/ context, int fd, int buffersize) {
            if (buffersize < 0) {
                throw PythonOps.OSError(PythonErrorNumber.EINVAL, "Invalid argument");
            }

            try {
                PythonContext pythonContext = context.LanguageContext;
                var pf = pythonContext.FileManager.GetFileFromId(fd);
                return (Bytes)pf.read(context, buffersize);
            } catch (Exception e) {
                throw ToPythonException(e);
            }
        }

        [Documentation("rename(src, dst, *, src_dir_fd=None, dst_dir_fd=None)")]
        public static void rename([NotNone] string src, [NotNone] string dst, [ParamDictionary, NotNone] IDictionary<string, object> kwargs) {
            foreach (var key in kwargs.Keys) {
                switch (key) {
                    case "src_dir_fd":
                    case "dst_dir_fd":
                        // TODO: posix: implement these!
                        break;
                    default:
                        throw PythonOps.TypeError("'{0}' is an invalid keyword argument for this function", key);
                }
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                if (!MoveFileEx(src, dst, 0)) {
                    throw GetLastWin32Error(src, dst);
                }
            } else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                renameUnix(src, dst);
            } else {
                try {
                    Directory.Move(src, dst);
                } catch (Exception e) {
                    throw ToPythonException(e);
                }
            }
        }

        [Documentation("")]
        public static void rename(CodeContext context, [NotNone] Bytes src, [NotNone] Bytes dst, [ParamDictionary, NotNone] IDictionary<string, object> kwargs)
            => rename(src.ToFsString(context), dst.ToFsString(context), kwargs);

        [Documentation("")]
        public static void rename(CodeContext context, object? src, object? dst, [ParamDictionary, NotNone] IDictionary<string, object> kwargs)
            => rename(ConvertToFsString(context, src, nameof(src)), ConvertToFsString(context, dst, nameof(dst)), kwargs);

        private const uint MOVEFILE_REPLACE_EXISTING = 0x01;

        [DllImport("kernel32.dll", EntryPoint = "MoveFileExW", SetLastError = true, CharSet = CharSet.Unicode, BestFitMapping = false)]
        private static extern bool MoveFileEx(string src, string dst, uint flags);

        private static void renameUnix(string src, string dst) {
            if (Mono.Unix.Native.Syscall.rename(src, dst) == 0) return;
            throw GetLastUnixError(src, dst);
        }

        [Documentation("replace(src, dst, *, src_dir_fd=None, dst_dir_fd=None)")]
        public static void replace([NotNone] string src, [NotNone] string dst, [ParamDictionary, NotNone] IDictionary<string, object> kwargs) {
            foreach (var key in kwargs.Keys) {
                switch (key) {
                    case "src_dir_fd":
                    case "dst_dir_fd":
                        // TODO: posix: implement these!
                        break;
                    default:
                        throw PythonOps.TypeError("'{0}' is an invalid keyword argument for this function", key);
                }
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                if (!MoveFileEx(src, dst, MOVEFILE_REPLACE_EXISTING)) {
                    throw GetLastWin32Error(src, dst);
                }
            } else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                renameUnix(src, dst);
            } else {
                throw new NotImplementedException();
            }
        }

        [Documentation("")]
        public static void replace(CodeContext context, [NotNone] Bytes src, [NotNone] Bytes dst, [ParamDictionary, NotNone] IDictionary<string, object> kwargs)
            => replace(src.ToFsString(context), dst.ToFsString(context), kwargs);

        [Documentation("")]
        public static void replace(CodeContext context, object? src, object? dst, [ParamDictionary, NotNone] IDictionary<string, object> kwargs)
            => replace(ConvertToFsString(context, src, nameof(src)), ConvertToFsString(context, dst, nameof(dst)), kwargs);


        [Documentation("rmdir(path, *, dir_fd=None)")]
        public static void rmdir([NotNone] string path, [ParamDictionary, NotNone] IDictionary<string, object> kwargs) {
            foreach (var key in kwargs.Keys) {
                switch (key) {
                    case "dir_fd":
                        // TODO: posix: implement this
                        break;
                    default:
                        throw PythonOps.TypeError("'{0}' is an invalid keyword argument for this function", key);
                }
            }
            try {
                Directory.Delete(path);
            } catch (Exception e) {
                throw ToPythonException(e, path);
            }
        }

        [Documentation("")]
        public static void rmdir(CodeContext context, [NotNone] Bytes path, [ParamDictionary, NotNone] IDictionary<string, object> kwargs)
            => rmdir(path.ToFsString(context), kwargs);

        [Documentation("")]
        public static void rmdir(CodeContext context, object? path, [ParamDictionary, NotNone] IDictionary<string, object> kwargs)
            => rmdir(ConvertToFsString(context, path, nameof(path)), kwargs);

#if FEATURE_PROCESS

        /// <summary>
        /// spawns a new process.
        ///
        /// If mode is nt.P_WAIT then then the call blocks until the process exits and the return value
        /// is the exit code.
        ///
        /// Otherwise the call returns a handle to the process.  The caller must then call nt.waitpid(pid, options)
        /// to free the handle and get the exit code of the process.  Failure to call nt.waitpid will result
        /// in a handle leak.
        /// </summary>
        public static object spawnv(CodeContext/*!*/ context, int mode, [NotNone] string path, object? args) {
            return SpawnProcessImpl(context, MakeProcess(), mode, path, args);
        }

        public static object spawnv(CodeContext context, int mode, [NotNone] Bytes path, object? args)
            => spawnv(context, mode, path.ToFsString(context), args);

        public static object spawnv(CodeContext context, int mode, object? path, object? args)
            => spawnv(context, mode, ConvertToFsString(context, path, nameof(path)), args);

        /// <summary>
        /// spawns a new process.
        ///
        /// If mode is nt.P_WAIT then then the call blocks until the process exits and the return value
        /// is the exit code.
        ///
        /// Otherwise the call returns a handle to the process.  The caller must then call nt.waitpid(pid, options)
        /// to free the handle and get the exit code of the process.  Failure to call nt.waitpid will result
        /// in a handle leak.
        /// </summary>
        public static object spawnve(CodeContext/*!*/ context, int mode, [NotNone] string path, object? args, object? env) {
            Process process = MakeProcess();
            SetEnvironment(context, process.StartInfo.EnvironmentVariables, env);

            return SpawnProcessImpl(context, process, mode, path, args);
        }

        public static object spawnve(CodeContext context, int mode, [NotNone] Bytes path, object? args, object? env)
            => spawnve(context, mode, path.ToFsString(context), args, env);

        public static object spawnve(CodeContext context, int mode, object? path, object? args, object? env)
            => spawnve(context, mode, ConvertToFsString(context, path, nameof(path)), args, env);

        private static Process MakeProcess() {
            try {
                return new Process();
            } catch (Exception e) {
                throw ToPythonException(e);
            }
        }

        private static object SpawnProcessImpl(CodeContext/*!*/ context, Process process, int mode, string path, object? args, [CallerMemberName] string? methodname = null) {
            try {
                process.StartInfo.Arguments = ArgumentsToString(context, args, methodname);
                process.StartInfo.FileName = path;
                process.StartInfo.UseShellExecute = false;
            } catch (Exception e) {
                throw ToPythonException(e, path);
            }

            if (!process.Start()) {
                throw PythonOps.OSError("Cannot start process: {0}", path);
            }
            if (mode == P_WAIT) {
                process.WaitForExit();
                int exitCode = process.ExitCode;
                process.Close();
                return exitCode;
            }

            lock (_processToIdMapping) {
                int id;
                if (_freeProcessIds.Count > 0) {
                    id = _freeProcessIds[_freeProcessIds.Count - 1];
                    _freeProcessIds.RemoveAt(_freeProcessIds.Count - 1);
                } else {
                    // process IDs are handles on CPython/Win32 so we match
                    // that behavior and return something that is handle like.  Handles
                    // on NT are guaranteed to have the low 2 bits not set and users
                    // could use these for their own purposes.  We therefore match that
                    // behavior here.
                    _processCount += 4;
                    id = _processCount;
                }

                _processToIdMapping[id] = process;
                return ScriptingRuntimeHelpers.Int32ToObject(id);
            }
        }

        /// <summary>
        /// Copy elements from a Python mapping of dict environment variables to a StringDictionary.
        /// </summary>
        private static void SetEnvironment(CodeContext context, System.Collections.Specialized.StringDictionary currentEnvironment, object? newEnvironment) {
            var env = newEnvironment as IEnumerable<KeyValuePair<object?, object?>>; // TODO: as IMappingProtocol (https://docs.python.org/3.4/c-api/mapping.html) ?
            if (env == null) {
                throw PythonOps.TypeError("env argument must be a mapping object");
            }

            currentEnvironment.Clear();

            string strKey, strValue;
            foreach (var kvp in env) {
                strKey = ConvertToFsString(context, kvp.Key, "environment variable name", "spawnve");
                strValue = ConvertToFsString(context, kvp.Value, "environment variable value", "spawnve");
                currentEnvironment[strKey] = strValue;
            }
        }
#endif

        /// <summary>
        /// Convert a sequence of args to a string suitable for using to spawn a process.
        /// </summary>
        private static string ArgumentsToString(CodeContext/*!*/ context, object? args, string? methodname) {
            IEnumerator? argsEnumerator;
            StringBuilder? sb = null;
            if (!PythonOps.TryGetEnumerator(context, args, out argsEnumerator)) {
                throw PythonOps.TypeErrorForBadInstance("args parameter must be sequence, not {0}", args);
            }

            bool space = false;
            try {
                // skip the first element, which is the name of the command being run
                argsEnumerator.MoveNext();
                while (argsEnumerator.MoveNext()) {
                    if (sb == null) sb = new StringBuilder(); // lazy creation
                    string strarg = ConvertToFsString(context, argsEnumerator.Current, "elements of 'args'", methodname);
                    if (space) {
                        sb.Append(' ');
                    }
                    if (strarg.IndexOf(' ') != -1) {
                        sb.Append('"');
                        // double quote any existing quotes
                        sb.Append(strarg.Replace("\"", "\"\""));
                        sb.Append('"');
                    } else {
                        sb.Append(strarg);
                    }
                    space = true;
                }
            } finally {
                IDisposable? disposable = argsEnumerator as IDisposable;
                if (disposable != null) disposable.Dispose();
            }

            if (sb == null) return "";
            return sb.ToString();
        }

#if FEATURE_PROCESS
        [SupportedOSPlatform("windows"), PythonHidden(PlatformsAttribute.PlatformFamily.Unix)]
        public static void startfile([NotNone] string filepath, string operation = "open") {
            System.Diagnostics.Process process = new System.Diagnostics.Process();
            process.StartInfo.FileName = filepath;
            process.StartInfo.UseShellExecute = true;
            process.StartInfo.Verb = operation;
            try {
                process.Start();
            } catch (Exception e) {
                throw ToPythonException(e, filepath);
            }
        }

        [SupportedOSPlatform("windows"), PythonHidden(PlatformsAttribute.PlatformFamily.Unix)]
        public static void startfile(CodeContext context, [NotNone] Bytes filepath, string operation = "open")
            => startfile(filepath.ToFsString(context), operation);

        [SupportedOSPlatform("windows"), PythonHidden(PlatformsAttribute.PlatformFamily.Unix)]
        public static void startfile(CodeContext context, object? filepath, string operation = "open")
            => startfile(ConvertToFsString(context, filepath, nameof(filepath)), operation);

#endif

        [PythonType]
        public sealed class stat_result : PythonTuple {
            public const int n_fields = 16;
            public const int n_sequence_fields = 10;
            public const int n_unnamed_fields = 3;

            private const long nanosecondsPerSeconds = 1_000_000_000;

            private static object ToInt(BigInteger x) => x.AsInt32(out int i) ? i : (object)x;

            internal stat_result(int mode) : this(new object[10] { mode, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, null) { }

            internal stat_result(Mono.Unix.Native.Stat stat)
                : this(new object[16] {Mono.Unix.Native.NativeConvert.FromFilePermissions(stat.st_mode), ToInt(stat.st_ino), ToInt(stat.st_dev), ToInt(stat.st_nlink), ToInt(stat.st_uid), ToInt(stat.st_gid), ToInt(stat.st_size),
                      ToInt(stat.st_atime), ToInt(stat.st_mtime), ToInt(stat.st_ctime),
                      stat.st_atime + stat.st_atime_nsec / (double)nanosecondsPerSeconds, stat.st_mtime + stat.st_mtime_nsec / (double)nanosecondsPerSeconds, stat.st_ctime + stat.st_ctime_nsec / (double)nanosecondsPerSeconds,
                      ToInt(stat.st_atime * nanosecondsPerSeconds + stat.st_atime_nsec), ToInt(stat.st_mtime * nanosecondsPerSeconds + stat.st_mtime_nsec), ToInt(stat.st_ctime * nanosecondsPerSeconds + stat.st_ctime_nsec) }, null) { }

            internal stat_result(int mode, ulong fileidx, long size, long st_atime_ns, long st_mtime_ns, long st_ctime_ns)
                : this(new object[16] { mode, ToInt(fileidx), 0, 0, 0, 0, ToInt(size),
                      ToInt(st_atime_ns / nanosecondsPerSeconds), ToInt(st_mtime_ns / nanosecondsPerSeconds), ToInt(st_ctime_ns / nanosecondsPerSeconds),
                      st_atime_ns / (double)nanosecondsPerSeconds, st_mtime_ns / (double)nanosecondsPerSeconds, st_ctime_ns / (double)nanosecondsPerSeconds,
                      ToInt(st_atime_ns), ToInt(st_mtime_ns), ToInt(st_ctime_ns) }, null) { }

            private stat_result(object?[] statResult, PythonDictionary? dict) : base(statResult.Take(n_sequence_fields).ToArray()) {
                if (statResult.Length < n_sequence_fields) {
                    throw PythonOps.TypeError($"os.stat_result() takes an at least {n_sequence_fields}-sequence ({statResult.Length}-sequence given)");
                } else if (statResult.Length > n_fields) {
                    throw PythonOps.TypeError($"os.stat_result() takes an at least {n_sequence_fields}-sequence ({statResult.Length}-sequence given)");
                }

                object? obj;
                if (statResult.Length >= 11) {
                    _atime = statResult[10];
                } else if (TryGetDictValue(dict, "st_atime", out obj)) {
                    _atime = obj;
                }

                if (statResult.Length >= 12) {
                    _mtime = statResult[11];
                } else if (TryGetDictValue(dict, "st_mtime", out obj)) {
                    _mtime = obj;
                }

                if (statResult.Length >= 13) {
                    _ctime = statResult[12];
                } else if (TryGetDictValue(dict, "st_ctime", out obj)) {
                    _ctime = obj;
                }

                if (statResult.Length >= 14) {
                    st_atime_ns = statResult[13];
                } else if (TryGetDictValue(dict, "st_atime_ns", out obj)) {
                    st_atime_ns = obj;
                }

                if (statResult.Length >= 15) {
                    st_mtime_ns = statResult[14];
                } else if (TryGetDictValue(dict, "st_mtime_ns", out obj)) {
                    st_mtime_ns = obj;
                }

                if (statResult.Length >= 16) {
                    st_ctime_ns = statResult[15];
                } else if (TryGetDictValue(dict, "st_ctime_ns", out obj)) {
                    st_ctime_ns = obj;
                }
            }

            public static stat_result __new__(CodeContext context, [NotNone] PythonType cls, [NotNone] IEnumerable<object?> sequence, PythonDictionary? dict = null) {
                return new stat_result(sequence, dict);
            }

            public stat_result([NotNone] IEnumerable<object?> sequence, PythonDictionary? dict = null)
                : this(sequence.ToArray(), dict) { }

            private static bool TryGetDictValue(PythonDictionary? dict, string name, out object? value) {
                value = null;
                return dict != null && dict.TryGetValue(name, out value);
            }

            private readonly object? _atime;
            private readonly object? _mtime;
            private readonly object? _ctime;

            public object? st_mode => this[0];
            public object? st_ino => this[1];
            public object? st_dev => this[2];
            public object? st_nlink => this[3];
            public object? st_uid => this[4];
            public object? st_gid => this[5];
            public object? st_size => this[6];
            public object? st_atime => _atime ?? this[7];
            public object? st_mtime => _mtime ?? this[8];
            public object? st_ctime => _ctime ?? this[9];
            public object? st_atime_ns { get; }
            public object? st_mtime_ns { get; }
            public object? st_ctime_ns { get; }

            public override string/*!*/ __repr__(CodeContext/*!*/ context) {
                return string.Format("os.stat_result("
                    + "st_mode={0}, "
                    + "st_ino={1}, "
                    + "st_dev={2}, "
                    + "st_nlink={3}, "
                    + "st_uid={4}, "
                    + "st_gid={5}, "
                    + "st_size={6}, "
                    + "st_atime={7}, "
                    + "st_mtime={8}, "
                    + "st_ctime={9})", this.Select(v => PythonOps.Repr(context, v)).ToArray());
            }

            public PythonTuple __reduce__() {
                PythonDictionary timeDict = new PythonDictionary(3);
                timeDict["st_atime"] = _atime;
                timeDict["st_mtime"] = _mtime;
                timeDict["st_ctime"] = _ctime;
                timeDict["st_atime_ns"] = st_atime_ns;
                timeDict["st_mtime_ns"] = st_mtime_ns;
                timeDict["st_ctime_ns"] = st_ctime_ns;

                return MakeTuple(
                    DynamicHelpers.GetPythonTypeFromType(typeof(stat_result)),
                    MakeTuple(new PythonTuple(this), timeDict)
                );
            }
        }

        private static bool HasExecutableExtension(string path) {
            string extension = Path.GetExtension(path).ToLower(CultureInfo.InvariantCulture);
            return (extension == ".exe" || extension == ".dll" || extension == ".com" || extension == ".bat");
        }

        // Isolate Mono.Unix from the rest of the method so that we don't try to load the Mono.Unix assembly on Windows.
        private static object statUnix(string path) {
            if (Mono.Unix.Native.Syscall.stat(path, out Mono.Unix.Native.Stat buf) == 0) {
                return new stat_result(buf);
            }
            return LightExceptions.Throw(GetLastUnixError(path));
        }

        private const int OPEN_EXISTING = 3;
        private const int FILE_ATTRIBUTE_NORMAL = 0x00000080;
        private const int FILE_READ_ATTRIBUTES = 0x0080;
        private const int FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;
        private const int FILE_FLAG_OPEN_REPARSE_POINT = 0x00200000;

        [DllImport("kernel32.dll", EntryPoint = "CreateFileW", SetLastError = true, CharSet = CharSet.Unicode, BestFitMapping = false)]
        private static extern SafeFileHandle CreateFile(
            string lpFileName,
            int dwDesiredAccess,
            int dwShareMode,
            IntPtr securityAttrs,
            int dwCreationDisposition,
            int dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetFileInformationByHandle(SafeFileHandle hFile, out BY_HANDLE_FILE_INFORMATION lpFileInformation);

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct BY_HANDLE_FILE_INFORMATION {
            public uint FileAttributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
            public uint VolumeSerialNumber;
            public uint FileSizeHigh;
            public uint FileSizeLow;
            public uint NumberOfLinks;
            public uint FileIndexHigh;
            public uint FileIndexLow;
        }

        [Documentation("stat(path, *, dir_fd=None, follow_symlinks=True) -> stat_result\n\n" +
            "Gathers statistics about the specified file or directory")]
        [LightThrowing]
        public static object stat([NotNone] string path, [ParamDictionary, NotNone] IDictionary<string, object> kwargs) {
            if (path == null) {
                return LightExceptions.Throw(PythonOps.TypeError("expected string, got NoneType"));
            }

            foreach (var key in kwargs.Keys) {
                switch (key) {
                    case "dir_fd":
                    case "follow_symlinks":
                        // TODO: implement these!
                        break;
                    default:
                        return LightExceptions.Throw(PythonOps.TypeError("'{0}' is an invalid keyword argument for this function", key));
                }
            }

            VerifyPath(path, functionName: nameof(stat), argName: nameof(path));

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                try {
                    FileInfo fi = new FileInfo(path);
                    int mode = 0;
                    long size;

                    if (IsNulFile(path)) {
                        return new stat_result(0x2000);
                    } else if (Directory.Exists(path)) {
                        size = 0;
                        mode = 0x4000 | S_IEXEC;
                    } else if (File.Exists(path)) {
                        size = fi.Length;
                        mode = 0x8000;
                        if (HasExecutableExtension(path)) {
                            mode |= S_IEXEC;
                        }
                    } else {
                        return LightExceptions.Throw(PythonOps.OSError(0, "file does not exist", path, PythonExceptions._OSError.ERROR_PATH_NOT_FOUND));
                    }

                    mode |= S_IREAD;
                    if ((fi.Attributes & FileAttributes.ReadOnly) == 0) {
                        mode |= S_IWRITE;
                    }

                    const long epochDifferenceLong = 62135596800 * TimeSpan.TicksPerSecond;

                    // 1 tick = 100 nanoseconds
                    long st_atime_ns = (fi.LastAccessTime.ToUniversalTime().Ticks - epochDifferenceLong) * 100;
                    long st_mtime_ns = (fi.LastWriteTime.ToUniversalTime().Ticks - epochDifferenceLong) * 100;
                    long st_ctime_ns = (fi.CreationTime.ToUniversalTime().Ticks - epochDifferenceLong) * 100;

                    ulong fileIdx = 0;
                    var handle = CreateFile(path, FILE_READ_ATTRIBUTES, 0, IntPtr.Zero, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL | FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OPEN_REPARSE_POINT, IntPtr.Zero);
                    if (!handle.IsInvalid) {
                        if (GetFileInformationByHandle(handle, out BY_HANDLE_FILE_INFORMATION fileInfo)) {
                            fileIdx = (((ulong)fileInfo.FileIndexHigh) << 32) + fileInfo.FileIndexLow;
                        }
                        handle.Close();
                    }

                    return new stat_result(mode, fileIdx, size, st_atime_ns, st_mtime_ns, st_ctime_ns);
                } catch (ArgumentException) {
                    return LightExceptions.Throw(PythonOps.OSError(0, "The path is invalid", path, PythonExceptions._OSError.ERROR_INVALID_NAME));
                } catch (Exception e) {
                    return LightExceptions.Throw(ToPythonException(e, path));
                }
            } else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                return statUnix(path);
            } else {
                throw new PlatformNotSupportedException();
            }
        }

        [LightThrowing, Documentation("")]
        public static object stat(CodeContext context, [NotNone] Bytes path, [ParamDictionary, NotNone] IDictionary<string, object> dict)
            => stat(path.ToFsString(context), dict);

        [LightThrowing, Documentation("")]
        public static object stat(CodeContext context, [NotNone] IBufferProtocol path, [ParamDictionary, NotNone] IDictionary<string, object> dict) {
            // TODO: accept object? path to get nicer error message?
            // TODO: Python 3.6: os.PathLike
            PythonOps.Warn(context, PythonExceptions.DeprecationWarning, $"{nameof(stat)}: {nameof(path)} should be string, bytes or integer, not {PythonOps.GetPythonTypeName(path)}"); // deprecated in 3.6
            return stat(path.ToFsBytes(context).ToFsString(context), dict);
        }

        [LightThrowing, Documentation("")]
        public static object stat(CodeContext context, int fd)
            => fstat(context, fd);

        public static string strerror(int code) {
#if FEATURE_NATIVE
            const int bufsize = 0x1FF;
            var buffer = new StringBuilder(bufsize);

            int result = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ?
                Interop.Ucrtbase.strerror(code, buffer) :
                strerror_r(code, buffer);

            if (result == 0) {
                var msg = buffer.ToString();
                if (msg.Length > 0) {
                    return msg;
                }
            }
#endif
            return "Unknown error " + code;
        }

#if FEATURE_NATIVE
        // Isolate Mono.Unix from the rest of the method so that we don't try to load the Mono.Unix assembly on Windows.
        private static int strerror_r(int code, StringBuilder buffer)
            => Mono.Unix.Native.Syscall.strerror_r(Mono.Unix.Native.NativeConvert.ToErrno(code), buffer);
#endif

#if FEATURE_PROCESS
        [Documentation("system(command) -> int\nExecute the command (a string) in a subshell.")]
        public static int system([NotNone] string command) {
            ProcessStartInfo? psi = GetProcessInfo(command, false);

            if (psi == null) {
                return -1;
            }

            psi.CreateNoWindow = false;

            try {
                Process? process = Process.Start(psi);
                if (process == null) {
                    return -1;
                }
                process.WaitForExit();
                return process.ExitCode;
            } catch (Win32Exception) {
                return 1;
            }
        }
#endif

        public static terminal_size get_terminal_size(CodeContext context)
            => get_terminal_size(1); // stdout

        public static terminal_size get_terminal_size(int fd)
            => new terminal_size(new object[2] { Console.WindowWidth, Console.WindowHeight }); // TODO: use the fd

        [PythonType]
        public sealed class terminal_size : PythonTuple {
            public const int n_fields = 2;
            public const int n_sequence_fields = 2;
            public const int n_unnamed_fields = 0;

            internal terminal_size(object?[] sequence) : base(sequence) {
                if (sequence.Length != n_sequence_fields) {
                    throw PythonOps.TypeError($"os.{nameof(terminal_size)}() takes a {n_sequence_fields}-sequence ({sequence.Length}-sequence given)");
                }
            }

            public static terminal_size __new__(CodeContext context, [NotNone] PythonType cls, [NotNone] IEnumerable<object?> sequence) {
                return new terminal_size(sequence.ToArray());
            }

            public object? columns => this[0];
            public object? lines => this[1];

            public override string/*!*/ __repr__(CodeContext/*!*/ context) {
                return $"os.{nameof(terminal_size)}(columns={PythonOps.Repr(context, columns)}, lines={PythonOps.Repr(context, lines)})";
            }
        }

        public static void truncate(CodeContext context, [NotNone] string path, BigInteger length) {
            using var stream = new FileStream(path, FileMode.Open);
            stream.SetLength((long)length);
        }

        [Documentation("")]
        public static void truncate(CodeContext context, [NotNone] Bytes path, BigInteger length)
            => truncate(context, path.ToFsString(context), length);

        [Documentation("")]
        public static void truncate(CodeContext context, [NotNone] IBufferProtocol path, BigInteger length) {
            // TODO: accept object? path to get nicer error message?
            // TODO: Python 3.6: os.PathLike
            PythonOps.Warn(context, PythonExceptions.DeprecationWarning, $"{nameof(truncate)}: {nameof(path)} should be string, bytes or integer, not {PythonOps.GetPythonTypeName(path)}"); // deprecated in 3.6
            truncate(context, path.ToFsBytes(context).ToFsString(context), length);
        }

        public static void truncate(CodeContext context, int fd, BigInteger length)
            => ftruncate(context, fd, length);

        public static void ftruncate(CodeContext context, int fd, BigInteger length)
            => context.LanguageContext.FileManager.GetFileFromId(fd).truncate(context, length);

#if FEATURE_FILESYSTEM
        public static object times() {
            System.Diagnostics.Process p = System.Diagnostics.Process.GetCurrentProcess();

            return PythonTuple.MakeTuple(p.UserProcessorTime.TotalSeconds,
                p.PrivilegedProcessorTime.TotalSeconds,
                0,  // child process system time
                0,  // child process os time
                DateTime.Now.Subtract(p.StartTime).TotalSeconds);
        }

        [Documentation("remove(path, *, dir_fd=None)")]
        public static void remove([NotNone] string path, [ParamDictionary, NotNone] IDictionary<string, object> kwargs) {
            foreach (var key in kwargs.Keys) {
                switch (key) {
                    case "dir_fd":
                        // TODO: posix: implement this
                        break;
                    default:
                        throw PythonOps.TypeError("'{0}' is an invalid keyword argument for this function", key);
                }
            }
            UnlinkWorker(path);
        }

        [Documentation("")]
        public static void remove(CodeContext context, [NotNone] Bytes path, [ParamDictionary, NotNone] IDictionary<string, object> kwargs)
            => remove(path.ToFsString(context), kwargs);

        [Documentation("")]
        public static void remove(CodeContext context, object? path, [ParamDictionary, NotNone] IDictionary<string, object> kwargs)
            => remove(ConvertToFsString(context, path, nameof(path)), kwargs);

        [Documentation("unlink(path, *, dir_fd=None)")]
        public static void unlink([NotNone] string path, [ParamDictionary, NotNone] IDictionary<string, object> kwargs)
            => remove(path, kwargs);

        [Documentation("")]
        public static void unlink(CodeContext context, [NotNone] Bytes path, [ParamDictionary, NotNone] IDictionary<string, object> kwargs)
            => unlink(path.ToFsString(context), kwargs);

        [Documentation("")]
        public static void unlink(CodeContext context, object? path, [ParamDictionary, NotNone] IDictionary<string, object> kwargs)
            => unlink(ConvertToFsString(context, path, nameof(path)), kwargs);

        private static void UnlinkWorker(string path) {
            if (path == null) {
                throw new ArgumentNullException(nameof(path));
            } else if (path.IndexOfAny(Path.GetInvalidPathChars()) != -1 || Path.GetFileName(path).IndexOfAny(Path.GetInvalidFileNameChars()) != -1) {
                throw PythonOps.OSError(PythonExceptions._OSError.ERROR_INVALID_NAME, "The filename, directory name, or volume label syntax is incorrect", path, PythonExceptions._OSError.ERROR_INVALID_NAME);
            }

            bool existing = File.Exists(path); // will return false also on access denied
            try {
                File.Delete(path); // will throw an exception on access denied, no exception on file not existing
            } catch (Exception e) {
                throw ToPythonException(e, path);
            }
            if (!existing) { // file was not existing in the first place
                throw PythonOps.OSError(PythonExceptions._OSError.ERROR_FILE_NOT_FOUND, "The system cannot find the file specified", path, PythonExceptions._OSError.ERROR_FILE_NOT_FOUND);
            }
        }
#endif

#if FEATURE_PROCESS
        public static void unsetenv([NotNone] string varname) {
            System.Environment.SetEnvironmentVariable(varname, null);
        }
#endif

        public static object urandom(int n) {
            if (n < 0) throw PythonOps.ValueError("negative argument not allowed");

#if NET6_0_OR_GREATER
            var data = RandomNumberGenerator.GetBytes(n);
#else
            RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
            byte[] data = new byte[n];
            rng.GetBytes(data);
#endif

            return Bytes.Make(data);
        }

        public static object urandom(object? n)
            => urandom(Converter.ConvertToIndex(n, throwOverflowError: true));

        private static readonly object _umaskKey = new object();

        public static int umask(CodeContext/*!*/ context, int mask) {
            mask &= 0x180;
            object oldMask = context.LanguageContext.GetSetModuleState(_umaskKey, mask);
            if (oldMask == null) {
                return 0;
            } else {
                return (int)oldMask;
            }
        }

        public static int umask(CodeContext/*!*/ context, object? mask)
            => umask(context, Converter.ConvertToIndex(mask, throwOverflowError: true));

#if FEATURE_FILESYSTEM

        private static void utimeUnix(string path, long atime_ns, long utime_ns) {
            var atime = new Mono.Unix.Native.Timespec();
            atime.tv_sec = atime_ns / 1_000_000_000;
            atime.tv_nsec = atime_ns % 1_000_000_000;
            var utime = new Mono.Unix.Native.Timespec();
            utime.tv_sec = utime_ns / 1_000_000_000;
            utime.tv_nsec = utime_ns % 1_000_000_000;

            if (Mono.Unix.Native.Syscall.utimensat(Mono.Unix.Native.Syscall.AT_FDCWD, path, new[] { atime, utime }, 0) == 0) return;
            throw GetLastUnixError(path);
        }

        [Documentation("utime(path, times=None, *[, ns], dir_fd=None, follow_symlinks=True)")]
        public static void utime([NotNone] string path, [ParamDictionary, NotNone] IDictionary<string, object> kwargs, [NotNone] params object[] args) {
            var numArgs = args.Length;
            CheckOptionalArgsCount(numRegParms: 1, numOptPosParms: 1, numKwParms: 3, numArgs, kwargs.Count);

            PythonTuple? times = numArgs > 0 ? convertTimesToTuple(args[0]) : null;

            PythonTuple? ns = null;
            foreach (var kvp in kwargs) {
                switch (kvp.Key) {
                    case nameof(times):
                        if (numArgs > 0) throw PythonOps.TypeError("argument for {0}() given by name ('{1}') and position ({2})", nameof(utime), kvp.Key, 2);
                        if (ns != null) throw PythonOps.ValueError("utime: you may specify either 'times' or 'ns' but not both");

                        times = convertTimesToTuple(kvp.Value);
                        break;

                    case nameof(ns):
                        if (times != null) throw PythonOps.ValueError("utime: you may specify either 'times' or 'ns' but not both");

                        if (kvp.Value is PythonTuple pt3 && pt3.Count == 2) {
                            ns = pt3;
                        } else {
                            throw PythonOps.TypeError("utime: 'ns' must be a tuple of two ints");
                        }
                        break;

                    case "dir_fd":
                    case "follow_symlinks":
                        // TODO: implement these!
                        break;

                    default:
                        throw PythonOps.TypeError("'{0}' is an invalid keyword argument for this function", kvp.Key);
                }
            }

            if (times != null && times.__len__() != 2) {
                throw PythonOps.TypeError("utime: 'times' must be either a 2-value tuple (atime, mtime) or None");
            }

            // precision is lost when using FileInfo on Linux, use a syscall instead
            if (ns != null && (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || !ClrModule.IsMono && RuntimeInformation.IsOSPlatform(OSPlatform.OSX))) {
                utimeUnix(path, Converter.ConvertToInt64(ns[0]), Converter.ConvertToInt64(ns[1]));
                return;
            }

            try {
                // Create a DirectoryInfo or FileInfo depending on what it is
                // Changing the times of a directory does not work with a FileInfo and v.v.
                var fi = Directory.Exists(path) ? (FileSystemInfo)new DirectoryInfo(path) : new FileInfo(path);
                var atime = DateTime.Now;
                var mtime = DateTime.Now;
                if (times != null) {
                    atime = new DateTime(PythonTime.TimestampToTicks(Converter.ConvertToDouble(times[0])), DateTimeKind.Utc);
                    mtime = new DateTime(PythonTime.TimestampToTicks(Converter.ConvertToDouble(times[1])), DateTimeKind.Utc);
                } else if (ns != null) {
                    const long epochDifferenceLong = 62135596800 * TimeSpan.TicksPerSecond;
                    atime = new DateTime(Converter.ConvertToInt64(ns[0]) / 100 + epochDifferenceLong, DateTimeKind.Utc);
                    mtime = new DateTime(Converter.ConvertToInt64(ns[1]) / 100 + epochDifferenceLong, DateTimeKind.Utc);
                }
                fi.LastAccessTime = atime;
                fi.LastWriteTime = mtime;
            } catch (Exception e) {
                throw ToPythonException(e, path);
            }

            static PythonTuple? convertTimesToTuple(object? val) {
                return val == null ? null
                        : val is PythonTuple pt2 && pt2.Count == 2 ? pt2
                        : throw PythonOps.TypeError("utime: 'times' must be either a 2-value tuple (atime, mtime) or None");
            }
        }

        [Documentation("")]
        public static void utime(CodeContext context, [NotNone] Bytes path, [ParamDictionary, NotNone] IDictionary<string, object> kwargs, [NotNone] params object[] args)
            => utime(path.ToFsString(context), kwargs, args);

        [Documentation("")]
        public static void utime(CodeContext context, object? path, [ParamDictionary, NotNone] IDictionary<string, object> kwargs, [NotNone] params object[] args)
            => utime(ConvertToFsString(context, path, nameof(path)), kwargs, args);

#endif

#if FEATURE_PROCESS
        public static PythonTuple waitpid(int pid, int options) {
            Process? process;
            lock (_processToIdMapping) {
                if (!_processToIdMapping.TryGetValue(pid, out process)) {
                    throw GetOsError(PythonErrorNumber.ECHILD);
                }
            }

            process.WaitForExit();
            PythonTuple res = PythonTuple.MakeTuple(pid, process.ExitCode);

            lock (_processToIdMapping) {
                // lower 3 bits are user defined and ignored (matching NT's handle semantics)
                _processToIdMapping.Remove(pid & ~0x03);
                _freeProcessIds.Add(pid & ~0x03);
            }

            return res;
        }
#endif

        public static int write(CodeContext/*!*/ context, int fd, [NotNone] IBufferProtocol data) {
            try {
                PythonContext pythonContext = context.LanguageContext;
                var pf = pythonContext.FileManager.GetFileFromId(fd);
                return (int)pf.write(context, data);
            } catch (Exception e) {
                throw ToPythonException(e);
            }
        }

#if FEATURE_PROCESS

        [Documentation(@"Send signal sig to the process pid. Constants for the specific signals available on the host platform
are defined in the signal module.")]
        public static void kill(CodeContext/*!*/ context, int pid, int sig) {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
                RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                if (Mono.Unix.Native.Syscall.kill(pid, Mono.Unix.Native.NativeConvert.ToSignum(sig)) == 0) return;
                throw GetLastUnixError();
            } else {
                if (PythonSignal.NativeSignal.GenerateConsoleCtrlEvent((uint)sig, (uint)pid)) return;

                // If the calls to GenerateConsoleCtrlEvent didn't work, simply
                // forcefully kill the process.
                Process toKill = Process.GetProcessById(pid);
                toKill.Kill();
            }
        }

#endif

        #region Generated O_Flags

        // *** BEGIN GENERATED CODE ***
        // generated by function: generate_all_O_flags from: generate_os_codes.py


        [PythonHidden(PlatformsAttribute.PlatformFamily.Windows)]
        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("macos")]
        public static int O_ACCMODE => 0x3;

        public static int O_APPEND => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? 0x8 : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? 0x8 : 0x400;

        [PythonHidden(PlatformsAttribute.PlatformFamily.Unix)]
        [SupportedOSPlatform("windows")]
        public static int O_BINARY => 0x8000;

        [PythonHidden(PlatformsAttribute.PlatformFamily.Windows)]
        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("macos")]
        public static int O_CLOEXEC => RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? 0x1000000 : 0x80000;

        public static int O_CREAT => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? 0x100 : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? 0x200 : 0x40;

        [PythonHidden(PlatformsAttribute.PlatformFamily.Windows)]
        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("macos")]
        public static int O_DSYNC => RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? 0x400000 : 0x1000;

        public static int O_EXCL => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? 0x400 : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? 0x800 : 0x80;

        [PythonHidden(PlatformsAttribute.PlatformFamily.Windows, PlatformID.Unix)]
        [SupportedOSPlatform("macos")]
        public static int O_EXEC => 0x40000000;

        [PythonHidden(PlatformsAttribute.PlatformFamily.Windows, PlatformID.MacOSX)]
        [SupportedOSPlatform("linux")]
        public static int O_LARGEFILE => 0x0;

        [PythonHidden(PlatformsAttribute.PlatformFamily.Windows)]
        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("macos")]
        public static int O_NDELAY => RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? 0x4 : 0x800;

        [PythonHidden(PlatformsAttribute.PlatformFamily.Windows)]
        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("macos")]
        public static int O_NOCTTY => RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? 0x20000 : 0x100;

        [PythonHidden(PlatformsAttribute.PlatformFamily.Unix)]
        [SupportedOSPlatform("windows")]
        public static int O_NOINHERIT => 0x80;

        [PythonHidden(PlatformsAttribute.PlatformFamily.Windows)]
        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("macos")]
        public static int O_NONBLOCK => RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? 0x4 : 0x800;

        [PythonHidden(PlatformsAttribute.PlatformFamily.Unix)]
        [SupportedOSPlatform("windows")]
        public static int O_RANDOM => 0x10;

        public static int O_RDONLY => 0x0;

        public static int O_RDWR => 0x2;

        [PythonHidden(PlatformsAttribute.PlatformFamily.Windows, PlatformID.MacOSX)]
        [SupportedOSPlatform("linux")]
        public static int O_RSYNC => 0x101000;

        [PythonHidden(PlatformsAttribute.PlatformFamily.Windows, PlatformID.Unix)]
        [SupportedOSPlatform("macos")]
        public static int O_SEARCH => 0x40100000;

        [PythonHidden(PlatformsAttribute.PlatformFamily.Unix)]
        [SupportedOSPlatform("windows")]
        public static int O_SEQUENTIAL => 0x20;

        [PythonHidden(PlatformsAttribute.PlatformFamily.Unix)]
        [SupportedOSPlatform("windows")]
        public static int O_SHORT_LIVED => 0x1000;

        [PythonHidden(PlatformsAttribute.PlatformFamily.Windows)]
        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("macos")]
        public static int O_SYNC => RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? 0x80 : 0x101000;

        [PythonHidden(PlatformsAttribute.PlatformFamily.Unix)]
        [SupportedOSPlatform("windows")]
        public static int O_TEMPORARY => 0x40;

        [PythonHidden(PlatformsAttribute.PlatformFamily.Unix)]
        [SupportedOSPlatform("windows")]
        public static int O_TEXT => 0x4000;

        public static int O_TRUNC => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? 0x200 : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? 0x400 : 0x200;

        public static int O_WRONLY => 0x1;

        // *** END GENERATED CODE ***

        #endregion

        public const int P_WAIT = 0;
        public const int P_NOWAIT = 1;
        public const int P_NOWAITO = 3;

        // Not used by IronPython
        public const int P_OVERLAY = 2;
        public const int P_DETACH = 4;

        public const int TMP_MAX = 32767;

        [PythonHidden(PlatformsAttribute.PlatformFamily.Windows)]
        public const int WNOHANG = 1;

        [PythonHidden(PlatformsAttribute.PlatformFamily.Windows)]
        public const int WUNTRACED = 2;

        [PythonHidden(PlatformsAttribute.PlatformFamily.Windows)]
        public const int WCONTINUED = 8;

        [PythonHidden(PlatformsAttribute.PlatformFamily.Windows)]
        public const int WSTOPPED = 2;

        [PythonHidden(PlatformsAttribute.PlatformFamily.Windows)]
        public const int WEXITED = 4;

        [PythonHidden(PlatformsAttribute.PlatformFamily.Windows)]
        public const int WNOWAIT = 0x1000000;

        [PythonHidden(PlatformsAttribute.PlatformFamily.Windows)]
        public const int P_ALL = 0;

        [PythonHidden(PlatformsAttribute.PlatformFamily.Windows)]
        public const int P_PID = 1;

        [PythonHidden(PlatformsAttribute.PlatformFamily.Windows)]
        public const int P_PGID = 2;


        [Documentation(@"WCOREDUMP(status) -> bool

Return True if the process returning 'status' was dumped to a core file."),
            PythonHidden(PlatformsAttribute.PlatformFamily.Windows)]
        public static bool WCOREDUMP(int status) {
            //#define WCOREDUMP(s) ((s) & 0x80)
            return (status & 0x80) != 0;
        }

        [Documentation(@"WIFCONTINUED(status) -> bool

Return True if the process returning 'status' was continued from a
job control stop."),
            PythonHidden(PlatformsAttribute.PlatformFamily.Windows)]
        public static bool WIFCONTINUED(int status) {
            //#define WIFCONTINUED(s) ((s) == 0xffff)
            return status == 0xffff;
        }

        [Documentation(@"WIFSTOPPED(status) -> bool

Return True if the process returning 'status' was stopped."),
            PythonHidden(PlatformsAttribute.PlatformFamily.Windows)]
        public static bool WIFSTOPPED(int status) {
            //#define WIFSTOPPED(s) (((s) & 0xff) == 0x7f)
            return (status & 0xff) == 0x7f;
        }

        [Documentation(@"WIFSIGNALED(status) -> bool

Return True if the process returning 'status' was terminated by a signal."),
            PythonHidden(PlatformsAttribute.PlatformFamily.Windows)]
        public static bool WIFSIGNALED(int status) {
            return ((byte)((status & 0x7f) >> 1)) > 0;
        }

        [Documentation(@"WIFEXITED(status) -> bool

Return true if the process returning 'status' exited using the exit()
system call."),
            PythonHidden(PlatformsAttribute.PlatformFamily.Windows)]
        public static bool WIFEXITED(int status) {
            return WTERMSIG(status) == 0;
        }

        [Documentation(@"WEXITSTATUS(status) -> integer

Return the process return code from 'status'."),
            PythonHidden(PlatformsAttribute.PlatformFamily.Windows)]
        public static int WEXITSTATUS(int status) {
            // #define WEXITSTATUS(s) (((s) & 0xff00) >> 8)
            return (status & 0xff00) >> 8;
        }

        [Documentation(@"WTERMSIG(status) -> integer

Return the signal that terminated the process that provided the 'status'
value."),
            PythonHidden(PlatformsAttribute.PlatformFamily.Windows)]
        public static int WTERMSIG(int status) {
            //#define WTERMSIG(s) ((s) & 0x7f)
            return (status & 0x7f);
        }

        [Documentation(@"WSTOPSIG(status) -> integer

Return the signal that stopped the process that provided
the 'status' value."),
            PythonHidden(PlatformsAttribute.PlatformFamily.Windows)]
        public static int WSTOPSIG(int status) {
            // #define WSTOPSIG(s) WEXITSTATUS(s)
            return WEXITSTATUS(status);
        }

        #endregion

        #region Private implementation details

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Interoperability", "CA1404:CallGetLastErrorImmediatelyAfterPInvoke")]
        private static Exception ToPythonException(Exception e, string? filename = null) {
            // already a Python Exception
            if (e.GetPythonException() is not null)
                return e;

            if (e is ArgumentException || e is ArgumentNullException || e is ArgumentTypeException) {
                // rethrow reasonable exceptions
                return ExceptionHelpers.UpdateForRethrow(e);
            }

            int error = Marshal.GetLastWin32Error();

            string message = e.Message;
            int errorCode = 0;

            bool isWindowsError = false;
            if (e is Win32Exception winExcep) {
                errorCode = winExcep.NativeErrorCode;
                message = e.Message;
                isWindowsError = true;
            } else if (e is UnauthorizedAccessException unauth) {
                errorCode = PythonExceptions._OSError.ERROR_ACCESS_DENIED;
                return PythonOps.OSError(errorCode, "Access is denied", filename, errorCode);
            } else {
                var ioe = e as IOException;
                Exception? pe = IOExceptionToPythonException(ioe, error, filename);
                if (pe != null) return pe;

                errorCode = System.Runtime.InteropServices.Marshal.GetHRForException(e);

                if ((errorCode & ~0xfff) == (unchecked((int)0x80070000))) {
                    // Win32 HR, translate HR to Python error code if possible, otherwise
                    // report the HR.
                    errorCode = errorCode & 0xfff;

                    pe = IOExceptionToPythonException(ioe, errorCode, filename);
                    if (pe != null) return pe;

                    message = e.Message;
                    isWindowsError = true;
                }
            }

            if (isWindowsError) {
                return PythonOps.OSError(errorCode, message, filename, errorCode);
            }

            return PythonOps.OSError(errorCode, message, filename);
        }

        private static Exception? IOExceptionToPythonException(IOException? ioe, int error, string? filename) {
            if (ioe == null) return null;

            switch (error) {
                case PythonExceptions._OSError.ERROR_DIR_NOT_EMPTY:
                    return PythonExceptions.CreateThrowable(PythonExceptions.OSError, error, "The directory is not empty", filename, error);
                case PythonExceptions._OSError.ERROR_ACCESS_DENIED:
                    return PythonExceptions.CreateThrowable(PythonExceptions.PermissionError, error, "Access is denied", filename, error);
                case PythonExceptions._OSError.ERROR_SHARING_VIOLATION:
                    return PythonExceptions.CreateThrowable(PythonExceptions.PermissionError, error, "The process cannot access the file because it is being used by another process", null, error);
                case PythonExceptions._OSError.ERROR_PATH_NOT_FOUND:
                    return PythonExceptions.CreateThrowable(PythonExceptions.FileNotFoundError, error, "The system cannot find the path specified", filename, error);
                default:
                    return null;
            }
        }

        // Win32 error codes

        private const int S_IWRITE = 0x80 + 0x10 + 0x02; // owner / group / world
        private const int S_IREAD = 0x100 + 0x20 + 0x04; // owner / group / world
        private const int S_IEXEC = 0x40 + 0x08 + 0x01; // owner / group / world

        public const int F_OK = 0;
        public const int X_OK = 1;
        public const int W_OK = 2;
        public const int R_OK = 4;

        private static void addBase(IEnumerable<string> files, PythonList ret) {
            foreach (string file in files) {
                ret.AddNoLock(Path.GetFileName(file));
            }
        }

        private static FileMode FileModeFromFlags(int flags) {
            if ((flags & O_APPEND) != 0) return FileMode.Append;
            if ((flags & O_EXCL) != 0) {
                if ((flags & O_CREAT) != 0) {
                    return FileMode.CreateNew;
                }

                return FileMode.Open;
            }
            if ((flags & O_CREAT) != 0) return FileMode.Create;
            if ((flags & O_TRUNC) != 0) return FileMode.Truncate;

            return FileMode.Open;
        }

        private static FileAccess FileAccessFromFlags(int flags) {
            if ((flags & O_RDWR) != 0) return FileAccess.ReadWrite;
            if ((flags & O_WRONLY) != 0) return FileAccess.Write;

            return FileAccess.Read;
        }

#if FEATURE_PROCESS

        private static ProcessStartInfo? GetProcessInfo(string command, bool throwException) {
            // TODO: always run through cmd.exe ?
            command = command.Trim();
            string? baseCommand, args;
            if (!TryGetExecutableCommand(command, out baseCommand, out args)) {
                if (!TryGetShellCommand(command, out baseCommand, out args)) {
                    if (throwException) {
                        throw PythonOps.WindowsError("The system can not find command '{0}'", command);
                    } else {
                        return null;
                    }
                }
            }

            ProcessStartInfo psi = new ProcessStartInfo(baseCommand, args);
            psi.UseShellExecute = false;

            return psi;
        }

        private static bool TryGetExecutableCommand(string command, out string baseCommand, out string args) {
            baseCommand = command;
            args = String.Empty;
            int pos;

            if (command[0] == '\"') {
                for (pos = 1; pos < command.Length; pos++) {
                    if (command[pos] == '\"') {
                        baseCommand = command.Substring(1, pos - 1).Trim();
                        if (pos + 1 < command.Length) {
                            args = command.Substring(pos + 1);
                        }
                        break;
                    }
                }

                if (pos == command.Length) {
                    baseCommand = command.Substring(1).Trim();
                    command = command + "\"";
                }
            } else {
                pos = command.IndexOf(' ');
                if (pos != -1) {
                    baseCommand = command.Substring(0, pos);
                    // pos won't be the last one
                    args = command.Substring(pos + 1);
                }
            }
            string fullpath = Path.GetFullPath(baseCommand);
            if (File.Exists(fullpath)) {
                baseCommand = fullpath;
                return true;
            }

            // TODO: need revisit
            string sysdir = System.Environment.GetFolderPath(System.Environment.SpecialFolder.System);
            foreach (string suffix in new string[] { string.Empty, ".com", ".exe", "cmd", ".bat" }) {
                fullpath = Path.Combine(sysdir, baseCommand + suffix);
                if (File.Exists(fullpath)) {
                    baseCommand = fullpath;
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetShellCommand(string command, [NotNullWhen(true)] out string? baseCommand, out string args) {
            baseCommand = Environment.GetEnvironmentVariable("COMSPEC");
            args = string.Empty;
            if (baseCommand == null) {
                baseCommand = Environment.GetEnvironmentVariable("SHELL");
                if (baseCommand == null) {
                    return false;
                }
                args = string.Format("-c \"{0}\"", command);
            } else {
                args = string.Format("/c {0}", command);
            }
            return true;
        }

#endif

        private static Exception DirectoryExists() {
            return PythonOps.OSError(PythonExceptions._OSError.ERROR_ALREADY_EXISTS, "directory already exists", null, PythonExceptions._OSError.ERROR_ALREADY_EXISTS);
        }

#if FEATURE_NATIVE

        private static Exception GetLastUnixError(string? filename = null, string? filename2 = null)
            => GetOsError(Mono.Unix.Native.NativeConvert.FromErrno(Mono.Unix.Native.Syscall.GetLastError()), filename, filename2);

#endif

        private static Exception GetOsError(int error, string? filename = null, string? filename2 = null)
            => PythonOps.OSError(error, strerror(error), filename, null, filename2);

#if FEATURE_NATIVE || FEATURE_CTYPES

        // Gets an error message for a Win32 error code.
        internal static string GetMessage(int errorCode) {
            string msg = new Win32Exception(errorCode).Message;
            // error codes: https://docs.microsoft.com/en-us/windows/win32/debug/system-error-codes
            if (errorCode is not (< 0 or >= 8200 or 34 or 106 or 317 or 718)) {
                msg = msg.IndexOf('%') switch {
                    0 => "The file specified" + msg.Substring(2),
                    > 0 => msg.Replace("%1", "the file specified"),
                    _ => msg
                };
            }
            return msg.TrimEnd('\r', '\n', '.');
        }

        internal static Exception GetLastWin32Error(string? filename = null, string? filename2 = null)
            => GetWin32Error(Marshal.GetLastWin32Error(), filename, filename2);

        private static Exception GetWin32Error(int error, string? filename = null, string? filename2 = null) {
            var msg = GetMessage(error);
            return PythonOps.OSError(0, msg, filename, error, filename2);
        }

#endif

        private static Encoding _getFileSystemEncoding(CodeContext context) {
            return SysModule.getfilesystemencoding(context) switch {
                "mbcs" => _mbcsEncoding,
                "utf-8" => _utf8Encoding,
                _ => throw new InvalidImplementationException("SysModule.getfilesystemencoding() returned invalid encoding"),
            };
        }

        private static string ToFsString(this Bytes b, CodeContext context) => _getFileSystemEncoding(context).GetString(b.AsSpan());

        private static Bytes ToFsBytes(this string s, CodeContext context) => Bytes.Make(_getFileSystemEncoding(context).GetBytes(s));

        private static Bytes ToFsBytes(this IBufferProtocol bp, CodeContext context) {
            // TODO: Python 3.6: "path should be string, bytes or os.PathLike"
            PythonOps.Warn(context, PythonExceptions.DeprecationWarning, "path should be string or bytes, not {0}", PythonOps.GetPythonTypeName(bp));
            return new Bytes(bp); // accepts FULL_RO buffers in CPython
        }

        private static string ConvertToFsString(CodeContext context, [NotNull] object? o, string argname, [CallerMemberName] string? methodname = null)
            => o switch {
                string s            => s,
                ExtensibleString es => es.Value,
                Bytes b             => b.ToFsString(context),
                IBufferProtocol bp  => bp.ToFsBytes(context).ToFsString(context),
                // TODO: Python 3.6: os.PathLike
                _ => throw PythonOps.TypeError("{0}: {1} should be string or bytes, not '{2}'", methodname, argname, PythonOps.GetPythonTypeName(o))
            };

        private static void CheckOptionalArgsCount(int numRegParms, int numOptPosParms, int numKwParms, int numOptPosArgs, int numKwArgs, [CallerMemberName] string? methodname = null) {
            if (numOptPosArgs > numOptPosParms)
                throw PythonOps.TypeErrorForOptionalArgumentCountMismatch(methodname ?? "<unknown>", numRegParms + numOptPosParms, numRegParms + numOptPosArgs, positional: true);

            if (numOptPosArgs + numKwArgs > numOptPosParms + numKwParms)
                throw PythonOps.TypeErrorForOptionalArgumentCountMismatch(methodname ?? "<unknown>", numRegParms + numOptPosParms + numKwParms, numRegParms + numOptPosArgs + numKwArgs);
        }

        private static void VerifyPath(string path, string functionName, string argName) {
            if (path.IndexOf((char)0) != -1) throw PythonOps.ValueError($"{functionName}: embedded null character in {argName}");
        }

        [SupportedOSPlatform("windows")]
        private static bool IsNulFile(string path)
            => path.StartsWith("nul", StringComparison.OrdinalIgnoreCase)
                && (path.Length == 3
                 || path.Length == 4 && path[3] == ':'
                 || path.Length == 5 && path[3] == ':' && path[4] == ':');

        #endregion
    }
}
