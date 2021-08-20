using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using IronPython.Runtime;
using IronPython.Runtime.Exceptions;
using IronPython.Zlib;
using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;

namespace IronPython.Modules {
    /// <summary>
    /// Implementes a resource-based meta_path importer as described in PEP 302.
    /// </summary>
    [PythonType]
    public class ResourceMetaPathImporter {
        private readonly PackedResourceLoader _loader;
        private readonly IDictionary<string, PackedResourceInfo> _unpackedLibrary;
        private readonly IDictionary<string, PackedResourceInfo[]> _unpackedModules;
        private readonly string _unpackingError;

        private static readonly Dictionary<string, ModuleCodeType> SearchOrder;

        [Flags]
        private enum ModuleCodeType {
            Source = 0,
            //ByteCode,
            Package,
        }

        static ResourceMetaPathImporter() {
            // we currently don't support bytecode, so just include the source versions.
            SearchOrder = new Dictionary<string, ModuleCodeType> {
                                                                     {
                                                                         Path.DirectorySeparatorChar + "__init__.py",
                                                                         ModuleCodeType.Package |
                                                                         ModuleCodeType.Source
                                                                         },
                                                                     {".py", ModuleCodeType.Source},
                                                                 };
        }

        /// <summary>
        /// Instantiates a new meta_path importer using an embedded ZIP resource file.
        /// </summary>
        /// <param name="fromAssembly"></param>
        /// <param name="resourceName"></param>
        public ResourceMetaPathImporter(Assembly fromAssembly, string resourceName) {
            _loader = new PackedResourceLoader(fromAssembly, resourceName);

            if (_loader.LoadZipDirectory(out _unpackedLibrary, out _unpackedModules,
                                         out _unpackingError))
                return;

            _unpackedLibrary = new Dictionary<string, PackedResourceInfo>();
            _unpackedModules = new Dictionary<string, PackedResourceInfo[]>();
            if (!String.IsNullOrEmpty(_unpackingError))
                throw MakeError("meta_path importer initialization error: {0}", _unpackingError);
        }

        [Documentation(
            @"find_module(fullname, path=None) -> self or None.

Search for a module specified by 'fullname'. 'fullname' must be the
fully qualified (dotted) module name. It returns the importer
instance itself if the module was found, or None if it wasn't.
The optional 'path' argument is ignored -- it's there for compatibility
with the importer protocol."
            )]
        public object find_module(CodeContext /*!*/ context, string fullname, params object[] args) {
            var packedName = MakeFilename(fullname);

            foreach (var entry in SearchOrder) {
                var temp = packedName + entry.Key;
                if (_unpackedLibrary.ContainsKey(temp))
                    return this;
            }
            return null;
        }

        [Documentation(
            @"load_module(fullname) -> module.

Load the module specified by 'fullname'. 'fullname' must be the
fully qualified (dotted) module name. It returns the imported
module, or raises ResourceImportError if it wasn't found."
            )]
        public object load_module(CodeContext /*!*/ context, string fullname) {
            var modules = context.LanguageContext.SystemStateModules;
            if (modules.ContainsKey(fullname))
                return modules[fullname];

            bool ispackage;
            string modpath;
            var code = GetModuleCode(context, fullname, out ispackage, out modpath);
            if (code == null)
                return null;

            var pythonContext = context.LanguageContext;
            ScriptCode script;
            var mod = pythonContext.CompileModule(modpath, fullname,
                                                  new SourceUnit(pythonContext,
                                                                 new MemoryStreamContentProvider(pythonContext, code, modpath),
                                                                 modpath, SourceCodeKind.File),
                                                  ModuleOptions.None, out script);

            var dict = mod.__dict__;
            // we do these here because we don't want CompileModule to initialize the module until we've set 
            // up some additional stuff
            dict.Add("__name__", fullname);
            dict.Add("__loader__", this);
            // ReSharper disable AssignNullToNotNullAttribute
            dict.Add("__package__", null);
            // ReSharper restore AssignNullToNotNullAttribute
            dict.Add("__file__", "<resource>");

            if (ispackage) {
                //// add __path__ to the module *before* the code
                //// gets executed
                //var subname = GetSubName(fullname);
                //var fullpath = string.Format("{0}{1}",
                //    Path.DirectorySeparatorChar,
                //    subname);

                //var pkgpath = PythonOps.MakeList(fullpath);
                var pkgpath = new PythonList();
                dict.Add("__path__", pkgpath);
            }

            modules.Add(fullname, mod);
            try {
                script.Run(mod.Scope);
            }
            catch (Exception) {
                modules.Remove(fullname);
                throw;
            }
            return mod;
        }

        private byte[] GetModuleCode(CodeContext /*!*/ context, string fullname, out bool ispackage,
                                     out string modpath) {
            var path = MakeFilename(fullname);
            ispackage = false;
            modpath = string.Empty;

            if (String.IsNullOrEmpty(path))
                return null;

            foreach (var entry in SearchOrder) {
                var temp = path + entry.Key;
                if (!_unpackedLibrary.ContainsKey(temp))
                    continue;

                var tocEntry = _unpackedLibrary[temp];
                ispackage = (entry.Value & ModuleCodeType.Package) == ModuleCodeType.Package;

                // we currently don't support bytecode modules, so we don't check
                // the time of the bytecode file vs. the time of the source file.
                byte[] code = GetCodeFromData(context, false, tocEntry);
                if (code == null) {
                    continue;
                }
                modpath = tocEntry.FullName;
                return code;
            }
            throw MakeError("can't find module '{0}'", fullname);
        }

        private byte[] GetCodeFromData(CodeContext /*!*/ context, bool isbytecode, PackedResourceInfo tocEntry) {
            byte[] data = GetData(tocEntry);
            byte[] code = null;

            if (data != null) {
                if (isbytecode) {
                    // would put in code to unmarshal the bytecode here...                                     
                }
                else {
                    code = data;
                }
            }
            return code;
        }

        private byte[] GetData(PackedResourceInfo tocEntry) {
            string unpackingError;
            byte[] result;
            if (!_loader.GetData(tocEntry, out result, out unpackingError))
                throw MakeError(unpackingError);
            return result;
        }

        private static Exception MakeError(params object[] args) {
            return IronPython.Runtime.Operations.PythonOps.CreateThrowable(PythonExceptions.ImportError, args);
        }

        private static string MakeFilename(string name) {
            return name.Replace('.', Path.DirectorySeparatorChar);
        }

        private struct PackedResourceInfo {
            private int _fileSize;

            public string FullName;
            public int Compress;
            public int DataSize;
            public int FileOffset;

            public static PackedResourceInfo Create(string fullName, int compress,
                                                    int dataSize, int fileSize, int fileOffset) {
                PackedResourceInfo result;
                result.FullName = fullName;
                result.Compress = compress;
                result.DataSize = dataSize;
                result._fileSize = fileSize;
                result.FileOffset = fileOffset;
                return result;
            }

#if DEBUG
            public override string ToString() {
                var sizeDesc = String.Format("{0} bytes", _fileSize);
                if (Convert.ToDouble(_fileSize)/1024.0 > 1.0)
                    sizeDesc = String.Format("{0} KB", Math.Round(Convert.ToDouble(_fileSize)/1024.0, 1));
                return String.Format("{0} ({1})", FullName, sizeDesc);
            }
#endif
        }

        private class PackedResourceLoader {
            private readonly Assembly _fromAssembly;
            private readonly string _resourceNameBase;
            private const int MaxPathLen = 256;

            public PackedResourceLoader(Assembly fromAssembly, string resourceName) {
                _fromAssembly = fromAssembly;
                _resourceNameBase = resourceName;
            }

            public bool LoadZipDirectory(
                out IDictionary<string, PackedResourceInfo> files,
                out IDictionary<string, PackedResourceInfo[]> modules,
                out string unpackingError) {
                if (!ReadZipDirectory(out files, out unpackingError)) {
                    modules = null;
                    return false;
                }

                try {
                    var parsedSources =
                        from entry in files.Values
                        let isPyFile = entry.FullName.EndsWith(".py", StringComparison.OrdinalIgnoreCase)
                        where isPyFile
                        let name = entry.FullName.Substring(0, entry.FullName.Length - 3)
                        let dottedName = name.Replace('\\', '.').Replace('/', '.')
                        let lineage = dottedName.Split('.')
                        let fileName = lineage.Last()
                        let path = lineage.Take(lineage.Length - 1).ToArray()
                        orderby fileName
                        select new {
                                       name = fileName,
                                       path,
                                       dottedPath = String.Join(".", path),
                                       entry
                                   };
                    var moduleContents =
                        from source in parsedSources
                        orderby source.dottedPath
                        group source by source.dottedPath
                        into moduleGroup
                        select new {
                                       moduleGroup.Key,
                                       Items = moduleGroup.Select(item => item.entry).ToArray()
                                   };
                    modules = moduleContents.ToDictionary(
                        moduleGroup => moduleGroup.Key,
                        moduleGroup => moduleGroup.Items);
                    return true;
                }
                catch (Exception exception) {
                    files = null;
                    modules = null;
                    unpackingError = String.Format("{0}: {1}", exception.GetType().Name, exception.Message);
                    return false;
                }
            }

            private Stream GetZipArchive() {
                var compareName = _resourceNameBase.ToLowerInvariant();
                var fullResourceNameQuery =
                    from name in _fromAssembly.GetManifestResourceNames()
                    where name.ToLowerInvariant().EndsWith(compareName, StringComparison.Ordinal)
                    select name;
                var fullResourceName = fullResourceNameQuery.FirstOrDefault();
                return string.IsNullOrEmpty(fullResourceName)
                           ? null
                           : _fromAssembly.GetManifestResourceStream(fullResourceName);
            }

            private bool ReadZipDirectory(out IDictionary<string, PackedResourceInfo> result,
                                          out string unpackingError) {
                unpackingError = null;
                result = null;
                try {
                    var stream = GetZipArchive();
                    if (stream == null) {
                        unpackingError = "Resource not found.";
                        return false;
                    }
                    using (var reader = new BinaryReader(stream)) {
                        if (reader.BaseStream.Length < 2) {
                            unpackingError = "Can't read ZIP resource: Empty Resource.";
                            return false;
                        }

                        var endofCentralDir = new byte[22];

                        reader.BaseStream.Seek(-22, SeekOrigin.End);
                        var headerPosition = (int) reader.BaseStream.Position;
                        if (reader.Read(endofCentralDir, 0, 22) != 22) {
                            unpackingError = "Can't read ZIP resource: Invalid ZIP Directory.";
                            return false;
                        }

                        if (BitConverter.ToUInt32(endofCentralDir, 0) != 0x06054B50) {
                            // Bad: End of Central Dir signature
                            unpackingError = "Can't read ZIP resource: Not a ZIP file.";
                            return false;
                        }

                        var headerSize = BitConverter.ToInt32(endofCentralDir, 12);
                        var headerOffset = BitConverter.ToInt32(endofCentralDir, 16);
                        var arcOffset = headerPosition - headerOffset - headerSize;
                        headerOffset += arcOffset;
                        var directoryEntries = ReadZipDirectory(reader, headerOffset, arcOffset);
                        result = directoryEntries
                            .OrderBy(entry => entry.FullName)
                            .ToDictionary(entry => entry.FullName);
                        return true;
                    }
                }
                catch (Exception exception) {
                    unpackingError = String.Format("{0}: {1}", exception.GetType().Name, exception.Message);
                    return false;
                }
            }

            private static IEnumerable<PackedResourceInfo> ReadZipDirectory(BinaryReader reader, int headerOffset,
                                                                            int arcoffset) {
                while (true) {
                    var name = string.Empty;
                    reader.BaseStream.Seek(headerOffset, SeekOrigin.Begin); // Start of file header
                    int l = reader.ReadInt32();
                    if (l != 0x02014B50) {
                        break; // Bad: Central Dir File Header
                    }
                    reader.BaseStream.Seek(headerOffset + 10, SeekOrigin.Begin);
                    var compress = reader.ReadInt16();
                    /*var time =*/
                    reader.ReadInt16();
                    /*var date =*/
                    reader.ReadInt16();
                    /*var crc =*/
                    reader.ReadInt32();
                    var dataSize = reader.ReadInt32();
                    var fileSize = reader.ReadInt32();
                    var nameSize = reader.ReadInt16();
                    var headerSize = 46 + nameSize +
                                     reader.ReadInt16() +
                                     reader.ReadInt16();

                    reader.BaseStream.Seek(headerOffset + 42, SeekOrigin.Begin);
                    var fileOffset = reader.ReadInt32() + arcoffset;
                    if (nameSize > MaxPathLen)
                        nameSize = MaxPathLen;

                    for (int i = 0; i < nameSize; i++) {
                        char c = reader.ReadChar();
                        if (c == '/')
                            c = Path.DirectorySeparatorChar;
                        name += c;
                    }
                    headerOffset += headerSize;

                    yield return
                        PackedResourceInfo.Create(name, compress, dataSize, fileSize, fileOffset);
                }
            }

            public bool GetData(PackedResourceInfo tocEntry, out byte[] result, out string unpackingError) {
                unpackingError = null;
                result = null;
                var fileOffset = tocEntry.FileOffset;
                var dataSize = tocEntry.DataSize;
                var compress = tocEntry.Compress;
                try {
                    var stream = GetZipArchive();
                    if (stream == null) {
                        unpackingError = "Resource not found.";
                        return false;
                    }
                    using (var reader = new BinaryReader(stream)) {
                        // Check to make sure the local file header is correct
                        reader.BaseStream.Seek(fileOffset, SeekOrigin.Begin);
                        var l = reader.ReadInt32();
                        if (l != 0x04034B50) {
                            // Bad: Local File Header
                            unpackingError = "Bad local file header in ZIP resource.";
                            return false;
                        }
                        reader.BaseStream.Seek(fileOffset + 26, SeekOrigin.Begin);
                        l = 30 + reader.ReadInt16() + reader.ReadInt16(); // local header size
                        fileOffset += l; // start of file data

                        reader.BaseStream.Seek(fileOffset, SeekOrigin.Begin);
                        byte[] rawData;
                        try {
                            rawData = reader.ReadBytes(compress == 0 ? dataSize : dataSize + 1);
                        }
                        catch {
                            unpackingError = "Can't read data";
                            return false;
                        }

                        if (compress != 0) {
                            rawData[dataSize] = (byte) 'Z';
                        }

                        result = compress == 0 ? rawData : ZlibModule.Decompress(rawData, -15);
                        return true;
                    }
                }
                catch (Exception exception) {
                    unpackingError = String.Format("{0}: {1}", exception.GetType().Name, exception.Message);
                    return false;
                }
            }
        }

    }
}
