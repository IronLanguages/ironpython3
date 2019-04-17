using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using IronPython.Runtime;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;
using IronPython.Zlib;

using Microsoft.Scripting;
using Microsoft.Scripting.Utils;
using Microsoft.Scripting.Runtime;

[assembly: PythonModule("zipimport", typeof(IronPython.Runtime.ZipImportModule))]
namespace IronPython.Runtime {
    public static class ZipImportModule {
        public const string __doc__ = @"zipimport provides support for importing Python modules from Zip archives.

This module exports three objects:
- zipimporter: a class; its constructor takes a path to a Zip archive.
- ZipImportError: exception raised by zipimporter objects. It's a
subclass of ImportError, so it can be caught as ImportError, too.
- _zip_directory_cache: a dict, mapping archive paths to zip directory
info dicts, as used in zipimporter._files.

It is usually not needed to use the zipimport module explicitly; it is
used by the builtin import mechanism for sys.path items that are paths
to Zip archives.";

        private static readonly object _zip_directory_cache_key = new object();

        [SpecialName]
        public static void PerformModuleReload(PythonContext context, PythonDictionary dict) {
            if (!context.HasModuleState(_zip_directory_cache_key))
                context.SetModuleState(_zip_directory_cache_key, new PythonDictionary());

            dict["_zip_directory_cache"] = context.GetModuleState(_zip_directory_cache_key);
            InitModuleExceptions(context, dict);
        }

        [PythonType]
        public class zipimporter {
            private const int MAXPATHLEN = 256;

            private string _archive;
            private string _prefix;
            private PythonDictionary __files;

            [Flags]
            private enum ModuleCodeType {
                Source = 0,
                ByteCode,
                Package,
            }

            private enum ModuleStatus {
                Error,
                NotFound,
                Module,
                Package
            };

            /*  */
            /// <summary>
            /// zip_searchorder defines how we search for a module in the Zip
            /// archive: we first search for a package __init__, then for
            /// non-package .pyc, .pyo and .py entries. The .pyc and .pyo entries
            /// are swapped by initzipimport() if we run in optimized mode. Also,
            /// '/' is replaced by SEP there.
            /// </summary>
            private static readonly Dictionary<string, ModuleCodeType> _search_order;

            static zipimporter() {
                // we currently don't support bytecode, so just include the source versions.
                _search_order = new Dictionary<string, ModuleCodeType>() {
                        //{ Path.DirectorySeparatorChar + "__init__.pyc" , ModuleType.Package | ModuleType.ByteCode },
                        //{ Path.DirectorySeparatorChar + "__init__.pyo", ModuleType.Package | ModuleType.ByteCode },
                        { Path.DirectorySeparatorChar + "__init__.py", ModuleCodeType.Package | ModuleCodeType.Source },
                        //{ ".pyc", ModuleType.ByteCode },
                        //{ ".pyo", ModuleType.ByteCode },
                        { ".py", ModuleCodeType.Source },
                };
            }

            #region Public API
            public zipimporter(CodeContext/*!*/ context, object pathObj, [ParamDictionary] IDictionary<object, object> kwArgs) {
                PlatformAdaptationLayer pal = context.LanguageContext.DomainManager.Platform;
                string prefix, input, path;

                if (pathObj == null) {
                    throw PythonOps.TypeError("must be string, not None");
                }

                if (!(pathObj is string)) {
                    throw PythonOps.TypeError("must be string, not {0}", pathObj.GetType());
                }

                if (kwArgs.Count > 0) {
                    throw PythonOps.TypeError("zipimporter() does not take keyword arguments");
                }

                path = pathObj as string;

                if (path.Length == 0)
                    throw MakeError(context, "archive path is empty");

                if (path.Length > MAXPATHLEN)
                    throw MakeError(context, "archive path too long");

                string buf = path.Replace(Path.AltDirectorySeparatorChar,
                    Path.DirectorySeparatorChar);
                input = buf;

                path = string.Empty;
                prefix = string.Empty;

                while (!string.IsNullOrEmpty(buf)) {
                    if (pal.FileExists(buf)) {
                        path = buf;
                        break;
                    }
                    buf = Path.GetDirectoryName(buf);
                }

                if (!string.IsNullOrEmpty(path)) {
                    PythonDictionary zip_directory_cache =
                        context.LanguageContext.GetModuleState(
                        _zip_directory_cache_key) as PythonDictionary;

                    if (zip_directory_cache != null && zip_directory_cache.ContainsKey(path)) {
                        _files = zip_directory_cache[path] as PythonDictionary;
                    } else {
                        _files = ReadDirectory(context, path);
                        zip_directory_cache.Add(path, _files);
                    }
                } else {
                    throw MakeError(context, "not a Zip file");
                }

                _prefix = input.Replace(path, string.Empty);
                // add trailing SEP
                if (!string.IsNullOrEmpty(_prefix) && !_prefix.EndsWith (Path.DirectorySeparatorChar.ToString())) {
                    _prefix = _prefix.Substring (1);
                    _prefix += Path.DirectorySeparatorChar;
                }
                _archive = path;
            }

            public PythonDictionary _files {
                get {
                    return __files;
                }
                private set {
                    __files = value;
                }
            }

            public string archive {
                get {
                    return _archive;
                }
            }

            public string prefix {
                get {
                    return _prefix;
                }
            }

            public string __repr__() {
                string archive = string.IsNullOrEmpty(_archive) ? "???" : _archive;
                string prefix = string.IsNullOrEmpty(_prefix) ? string.Empty : _prefix;
                string res = string.Empty;

                if (!string.IsNullOrEmpty(prefix)) {
                    res = string.Format("<zipimporter object \"{0}{1}{2}\">", archive, Path.DirectorySeparatorChar, prefix);
                } else {
                    res = string.Format("<zipimporter object \"{0}\">", archive);
                }
                return res;
            }

            [Documentation(@"find_module(fullname, path=None) -> self or None.

Search for a module specified by 'fullname'. 'fullname' must be the
fully qualified (dotted) module name. It returns the zipimporter
instance itself if the module was found, or None if it wasn't.
The optional 'path' argument is ignored -- it's there for compatibility
with the importer protocol.")]
            public object find_module(CodeContext/*!*/ context, string fullname,
                params object[] args) {
                // there could be a path item in the args, but it is not used
                ModuleStatus info = GetModuleInfo(context, fullname);

                if (info == ModuleStatus.Error || info == ModuleStatus.NotFound)
                    return null;

                return this;
            }

            [Documentation(@"load_module(fullname) -> module.

Load the module specified by 'fullname'. 'fullname' must be the
fully qualified (dotted) module name. It returns the imported
module, or raises ZipImportError if it wasn't found.")]
            public object load_module(CodeContext/*!*/ context, string fullname) {
                bool ispackage;
                string modpath;
                PythonModule mod;
                PythonContext pythonContext = context.LanguageContext;
                PythonDictionary dict;
                ScriptCode script = null;
                byte[] code = GetModuleCode(context, fullname, out ispackage, out modpath);
                if (code == null) {
                    return null;
                }

                mod = pythonContext.CompileModule(modpath, fullname,
                    new SourceUnit(pythonContext, new MemoryStreamContentProvider(pythonContext, code, modpath), modpath, SourceCodeKind.File),
                    ModuleOptions.None, out script);

                dict = mod.__dict__;
                // we do these here because we don't want CompileModule to initialize the module until we've set 
                // up some additional stuff
                dict.Add("__name__", fullname);
                dict.Add("__loader__", this);
                dict.Add("__package__", null);

                if (ispackage) {
                    // add __path__ to the module *before* the code
                    // gets executed
                    string subname = GetSubName(fullname);
                    string fullpath = string.Format("{0}{1}{2}{3}",
                        _archive,
                        Path.DirectorySeparatorChar,
                        _prefix.Length > 0 ? _prefix : string.Empty,
                        subname);

                    PythonList pkgpath = PythonOps.MakeList(fullpath);
                    dict.Add("__path__", pkgpath);
                }

                script.Run(mod.Scope);
                return mod;
            }

            [Documentation(@"get_filename(fullname) -> filename string.

Return the filename for the specified module.")]
            public string get_filename(CodeContext/*!*/ context, string fullname) {
                // Deciding the filename requires working out where the code
                // would come from if the module was actually loaded
                bool ispackage;
                string modpath;
                byte[] code = GetModuleCode(context, fullname, out ispackage, out modpath);
                if (code == null)
                    return null;

                return modpath;
            }

            [Documentation(@"is_package(fullname) -> bool.

Return True if the module specified by fullname is a package.
Raise ZipImportError if the module couldn't be found.")]
            public bool is_package(CodeContext/*!*/ context, string fullname) {
                ModuleStatus info = GetModuleInfo(context, fullname);
                if (info == ModuleStatus.NotFound) {
                    throw MakeError(context, "can't find module '{0}'", fullname);
                }
                return info == ModuleStatus.Package;
            }

            [Documentation(@"get_data(pathname) -> string with file data.

Return the data associated with 'pathname'. Raise IOError if
the file wasn't found.")]
            public Bytes get_data(CodeContext/*!*/ context, string path) {
                if (path.Length >= MAXPATHLEN) {
                    throw MakeError(context, "path too long");
                }

                path = path.Replace(_archive, string.Empty).TrimStart(Path.DirectorySeparatorChar);                
                if (!__files.ContainsKey(path)) {
                    throw PythonOps.IOError(path);
                }

                var data = GetData(context, _archive, __files[path] as PythonTuple);
                return Bytes.Make(data);
            }

            [Documentation(@"get_code(fullname) -> code object.

Return the code object for the specified module. Raise ZipImportError
if the module couldn't be found.")]
            public string get_code(CodeContext/*!*/ context, string fullname) {
                return string.Empty;
            }

            [Documentation(@"get_source(fullname) -> source string.

Return the source code for the specified module. Raise ZipImportError
if the module couldn't be found, return None if the archive does
contain the module, but has no source for it.")]
            public string get_source(CodeContext/*!*/ context, string fullname) {
                ModuleStatus mi = GetModuleInfo(context, fullname);
                string res = null;
                PythonContext pythonContext = context.LanguageContext;
                if (mi == ModuleStatus.Error) {
                    return null;
                }

                if (mi == ModuleStatus.NotFound) {
                    throw MakeError(context, "can't find module '{0}'", fullname);
                }

                string subname = GetSubName(fullname);
                string path = MakeFilename(context, _prefix, subname);
                if (string.IsNullOrEmpty(path)) {
                    return null;
                }

                if (mi == ModuleStatus.Package) {
                    path += Path.DirectorySeparatorChar + "__init__.py";
                } else {
                    path += ".py";
                }

                if (__files.ContainsKey(path)) {
                    var data = GetData(context, _archive, __files[path] as PythonTuple);
                    res = pythonContext.DefaultEncoding.GetString(data, 0, data.Length);
                }

                return res;
            }

            #endregion

            private byte[] GetModuleCode(CodeContext/*!*/ context, string fullname, out bool ispackage, out string modpath) {
                PythonTuple toc_entry = null;
                string subname = GetSubName(fullname);
                string path = MakeFilename(context, _prefix, subname);
                byte[] code = null;
                ispackage = false;
                modpath = string.Empty;

                if (string.IsNullOrEmpty(path)) {
                    return null;
                }

                foreach (KeyValuePair<string, ModuleCodeType> entry in _search_order) {
                    string temp = path + entry.Key;
                    if (__files.ContainsKey(temp)) {
                        toc_entry = (PythonTuple)__files[temp];
                        ispackage = (entry.Value & ModuleCodeType.Package) == ModuleCodeType.Package;
                        bool isbc = (entry.Value & ModuleCodeType.ByteCode) == ModuleCodeType.ByteCode;

                        // we currently don't support bytecode modules, so we don't check
                        // the time of the bytecode file vs. the time of the source file.
                        code = GetCodeFromData(context, ispackage, false, 0, toc_entry);
                        if (code == null) {
                            continue;
                        }
                        modpath = (string)toc_entry[0];
                        return code;
                    }
                }
                throw MakeError(context, "can't find module '{0}'", fullname);
            }

            /// <summary>
            /// Given a path to a Zip file and a toc_entry, return the (uncompressed)
            /// data as a new reference.
            /// </summary>
            /// <param name="archive"></param>
            /// <param name="toc_entry"></param>
            /// <returns></returns>
            private byte[] GetData(CodeContext context, string archive, PythonTuple toc_entry) {
                int l, file_offset = (int)toc_entry[4], data_size = (int)toc_entry[2], compress = (int)toc_entry[1];
                BinaryReader fp = null;
                byte[] raw_data;
                byte[] data = null;
                try {
                    try {
                        fp = new BinaryReader(new FileStream(archive, FileMode.Open, FileAccess.Read));
                    } catch {
                        throw PythonOps.IOError("zipimport: can not open file {0}", archive);
                    }

                    // Check to make sure the local file header is correct
                    fp.BaseStream.Seek(file_offset, SeekOrigin.Begin);
                    l = fp.ReadInt32();
                    if (l != 0x04034B50) {
                        // Bad: Local File Header
                        throw MakeError(context, "bad local file header in {0}", archive);
                    }
                    fp.BaseStream.Seek(file_offset + 26, SeekOrigin.Begin);
                    l = 30 + fp.ReadInt16() + fp.ReadInt16(); // local header size
                    file_offset += l;   // start of file data

                    fp.BaseStream.Seek(file_offset, SeekOrigin.Begin);
                    try {
                        raw_data = fp.ReadBytes(compress == 0 ? data_size : data_size + 1);
                    } catch {
                        throw PythonOps.IOError("zipimport: can't read data");
                    }

                    if (compress != 0) {
                        raw_data[data_size] = (byte)'Z';
                        data_size++;
                    }

                    if (compress == 0) { // data is not compressed
                        data = raw_data;
                    } else {
                        // decompress with zlib
                        data = ZlibModule.Decompress(raw_data, -15);
                    }

                } finally {
                    if (fp != null) {
                        fp.Close();
                    }
                }

                return data;
            }

            /// <summary>
            /// Return the code object for the module named by 'fullname' from the
            /// Zip archive as a new reference.
            /// </summary>
            /// <param name="context"></param>
            /// <param name="ispackage"></param>
            /// <param name="isbytecode"></param>
            /// <param name="mtime"></param>
            /// <param name="toc_entry"></param>
            /// <returns></returns>
            private byte[] GetCodeFromData(CodeContext/*!*/ context, bool ispackage, bool isbytecode, int mtime, PythonTuple toc_entry) {
                byte[] data = GetData(context, _archive, toc_entry);
                string modpath = (string)toc_entry[0];
                byte[] code = null;

                if (data != null) {
                    if (isbytecode) {
                        // would put in code to unmarshal the bytecode here...                                     
                    } else {
                        code = data;
                    }
                }
                return code;
            }

            /// <summary>
            /// Given a path to a Zip archive, build a dict, mapping file names
            /// (local to the archive, using SEP as a separator) to toc entries.
            /// 
            /// A toc_entry is a tuple:
            /// (__file__,      # value to use for __file__, available for all files
            ///  compress,      # compression kind; 0 for uncompressed
            ///  data_size,     # size of compressed data on disk
            ///  file_size,     # size of decompressed data
            ///  file_offset,   # offset of file header from start of archive
            ///  time,          # mod time of file (in dos format)
            ///  date,          # mod data of file (in dos format)
            ///  crc,           # crc checksum of the data
            ///  )
            /// Directories can be recognized by the trailing SEP in the name,
            /// data_size and file_offset are 0.
            /// </summary>
            /// <param name="archive"></param>
            /// <returns></returns>
            private PythonDictionary ReadDirectory(CodeContext context, string archive) {
                string path, name = string.Empty;
                BinaryReader fp = null;
                int header_position, header_size, header_offset, count, compress;
                int time, date, crc, data_size, name_size, file_size, file_offset;
                int arc_offset; // offset from beginning of file to start of zip-archive 
                PythonDictionary files = null;
                byte[] endof_central_dir = new byte[22];

                if (archive.Length > MAXPATHLEN) {
                    throw PythonOps.OverflowError("Zip path name is too long");
                }

                path = archive;
                try {
                    try {
                        fp = new BinaryReader(new FileStream(archive, FileMode.Open, FileAccess.Read));
                    } catch {
                        throw MakeError(context, "can't open Zip file: '{0}'", archive);
                    }

                    if (fp.BaseStream.Length < 22) {
                        throw MakeError(context, "can't read Zip file: '{0}'", archive);
                    }

                    fp.BaseStream.Seek(-22, SeekOrigin.End);
                    header_position = (int)fp.BaseStream.Position;
                    if (fp.Read(endof_central_dir, 0, 22) != 22) {
                        throw MakeError(context, "can't read Zip file: '{0}'", archive);
                    }

                    if (BitConverter.ToUInt32(endof_central_dir, 0) != 0x06054B50) {
                        // Bad: End of Central Dir signature
                        fp.Close();
                        throw MakeError(context, "not a Zip file: '{0}'", archive);
                    }

                    header_size = BitConverter.ToInt32(endof_central_dir, 12);
                    header_offset = BitConverter.ToInt32(endof_central_dir, 16);
                    arc_offset = header_position - header_offset - header_size;
                    header_offset += arc_offset;

                    files = new PythonDictionary();
                    path += Path.DirectorySeparatorChar;

                    // Start of Central Directory
                    count = 0;
                    while (true) {
                        name = string.Empty;
                        fp.BaseStream.Seek(header_offset, SeekOrigin.Begin); // Start of file header
                        int l = fp.ReadInt32();
                        if (l != 0x02014B50) {
                            break; // Bad: Central Dir File Header
                        }
                        fp.BaseStream.Seek(header_offset + 10, SeekOrigin.Begin);
                        compress = fp.ReadInt16();
                        time = fp.ReadInt16();
                        date = fp.ReadInt16();
                        crc = fp.ReadInt32();
                        data_size = fp.ReadInt32();
                        file_size = fp.ReadInt32();
                        name_size = fp.ReadInt16();
                        header_size = 46 + name_size +
                            fp.ReadInt16() +
                            fp.ReadInt16();

                        fp.BaseStream.Seek(header_offset + 42, SeekOrigin.Begin);
                        file_offset = fp.ReadInt32() + arc_offset;
                        if (name_size > MAXPATHLEN)
                            name_size = MAXPATHLEN;

                        for (int i = 0; i < name_size; i++) {
                            char c = fp.ReadChar();
                            if (c == '/')
                                c = Path.DirectorySeparatorChar;
                            name += c;
                        }
                        header_offset += header_size;

                        PythonTuple t = PythonOps.MakeTuple(path + name, compress, data_size, file_size, file_offset, time, date, crc);
                        files.Add(name, t);
                        count++;
                    }
                } finally {
                    fp?.Close();
                }

                return files;
            }

            private string GetSubName(string fullname) {
                string[] items = fullname.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
                return items[items.Length - 1];
            }

            /// <summary>
            /// Given a (sub)modulename, write the potential file path in the
            /// archive (without extension) to the path buffer. 
            /// </summary>
            /// <param name="prefix"></param>
            /// <param name="name"></param>
            /// <returns></returns>
            private string MakeFilename(CodeContext context, string prefix, string name) {
                // self.prefix + name [+ SEP + "__init__"] + ".py[co]" 
                if ((prefix.Length + name.Length + 13) >= MAXPATHLEN) {
                    throw MakeError(context, "path to long");
                }
                return (prefix + name).Replace('.', Path.DirectorySeparatorChar);
            }

            /// <summary>
            /// Determines the type of module we have (package or module, or not found).
            /// </summary>
            /// <param name="context"></param>
            /// <param name="fullname"></param>
            /// <returns></returns>
            private ModuleStatus GetModuleInfo(CodeContext/*!*/ context, string fullname) {
                string subname = GetSubName(fullname);
                string path = MakeFilename(context, _prefix, subname);
                if (string.IsNullOrEmpty(path))
                    return ModuleStatus.Error;

                foreach (KeyValuePair<string, ModuleCodeType> entry in _search_order) {
                    string temp = path + entry.Key;
                    if (_files.ContainsKey(temp)) {
                        if ((entry.Value & ModuleCodeType.Package) == ModuleCodeType.Package) {
                            return ModuleStatus.Package;
                        } else {
                            return ModuleStatus.Module;
                        }
                    }
                }
                return ModuleStatus.NotFound;
            }
        }

        public static PythonType get_ZipImportError(CodeContext context) {
            PythonContext pyContext = context.LanguageContext;
            PythonType zipImportError = (PythonType)pyContext.GetModuleState("zipimport.ZipImportError");
            return zipImportError;
        }

        internal static Exception MakeError(CodeContext context, params object[] args) {
            return PythonOps.CreateThrowable(get_ZipImportError(context), args);
        }

        private static void InitModuleExceptions(PythonContext context,
            PythonDictionary dict) {
            context.EnsureModuleException(
                "zipimport.ZipImportError",
                PythonExceptions.ImportError,
                typeof(PythonExceptions.BaseException),
                dict, "ZipImportError", "zipimport",
                (msg, innerException) => new ImportException(msg, innerException));
        }
    }
}
