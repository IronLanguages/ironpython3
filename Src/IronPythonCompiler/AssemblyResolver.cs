using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using IKVM.Reflection;

namespace IronPythonCompiler {
    internal class AssemblyResolver {
        private Universe _universe;
        private readonly List<string> _libpaths = new List<string>();
        private Version _mscorlibVersion;
        private Dictionary<string, string> _hintpaths = new Dictionary<string, string>();

        public AssemblyResolver(Universe universe, bool nostdlib, IEnumerable<string> references, IEnumerable<string> libpaths) {
            _universe = universe;

            _libpaths.Add(Environment.CurrentDirectory);
            _libpaths.AddRange(libpaths);
            if (!nostdlib) {
                _libpaths.Add(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory());
            }

            // items passed in via /lib:<path>
            foreach (string path in libpaths) {
                foreach (string dir in path.Split(new char[] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries)) {
                    if (Directory.Exists(dir)) {
                        _libpaths.Add(dir);
                    } else {
                        ConsoleOps.Warning("Directory specified ('{0}') by /lib: does not exist", dir);
                    }
                }
            }

            string envLib = Environment.GetEnvironmentVariable("LIB");
            if (!string.IsNullOrEmpty(envLib)) {
                foreach (string dir in envLib.Split(new char[] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries)) {
                    if (Directory.Exists(dir)) {
                        _libpaths.Add(dir);
                    } else {
                        ConsoleOps.Warning("Directory specified ('{0}') in LIB does not exist", dir);
                    }
                }
            }

            if (nostdlib) {
                _mscorlibVersion = LoadMscorlib(references).GetName().Version;
            } else {
                _mscorlibVersion = universe.Load("mscorlib").GetName().Version;
            }
            _universe.AssemblyResolve += _universe_AssemblyResolve;
        }

        private Assembly _universe_AssemblyResolve(object sender, IKVM.Reflection.ResolveEventArgs args) {
            AssemblyName name = new AssemblyName(args.Name);
            AssemblyName previousMatch = null;
            int previousMatchLevel = 0;
            foreach (Assembly asm in _universe.GetAssemblies()) {
                if (Match(asm.GetName(), name, ref previousMatch, ref previousMatchLevel)) {
                    return asm;
                }
            }
            foreach (string file in FindAssemblyPath(name.Name + ".dll")) {
                if (Match(AssemblyName.GetAssemblyName(file), name, ref previousMatch, ref previousMatchLevel)) {
                    return LoadFile(file);
                }
            }
            if (args.RequestingAssembly != null) {
                string path = Path.Combine(Path.GetDirectoryName(args.RequestingAssembly.Location), name.Name + ".dll");
                if (File.Exists(path) && Match(AssemblyName.GetAssemblyName(path), name, ref previousMatch, ref previousMatchLevel)) {
                    return LoadFile(path);
                }
            }
            string hintpath;
            if (_hintpaths.TryGetValue(name.FullName, out hintpath)) {
                string path = Path.Combine(hintpath, name.Name + ".dll");
                if (File.Exists(path) && Match(AssemblyName.GetAssemblyName(path), name, ref previousMatch, ref previousMatchLevel)) {
                    return LoadFile(path);
                }
            }
            if (previousMatch != null) {
                if (previousMatchLevel == 2) {
                    ConsoleOps.Warning("assuming assembly reference '{0}' matches '{1}', you may need to supply runtime policy", previousMatch.FullName, name.FullName);
                    return LoadFile(new Uri(previousMatch.CodeBase).LocalPath);
                } else if (args.RequestingAssembly != null) {
                    ConsoleOps.Error(true, "Assembly '{0}' uses '{1}' which has a higher version than referenced assembly '{2}'", args.RequestingAssembly.FullName, name.FullName, previousMatch.FullName);
                } else {
                    ConsoleOps.Error(true, "Assembly '{0}' was requested which is a higher version than referenced assembly '{1}'", name.FullName, previousMatch.FullName);
                }
            } else {
                ConsoleOps.Error(true, "unable to find assembly '{0}' {1}", args.Name, args.RequestingAssembly != null ? string.Format("    (a dependency of '{0}')", args.RequestingAssembly.FullName) : string.Empty);
            }
            return null;
        }

        private bool Match(AssemblyName assemblyDef, AssemblyName assemblyRef, ref AssemblyName bestMatch, ref int bestMatchLevel) {
            // Match levels:
            //   0 = no match
            //   1 = lower version match (i.e. not a suitable match, but used in error reporting: something was found but the version was too low)
            //   2 = higher version potential match (i.e. we can use this version, but if it is available the exact match will be preferred)
            //
            // If we find a perfect match, bestMatch is not changed but we return true to signal that the search can end right now.
            AssemblyComparisonResult result;
            _universe.CompareAssemblyIdentity(assemblyRef.FullName, false, assemblyDef.FullName, true, out result);
            switch (result) {
                case AssemblyComparisonResult.EquivalentFullMatch:
                case AssemblyComparisonResult.EquivalentPartialMatch:
                case AssemblyComparisonResult.EquivalentFXUnified:
                case AssemblyComparisonResult.EquivalentPartialFXUnified:
                case AssemblyComparisonResult.EquivalentPartialWeakNamed:
                case AssemblyComparisonResult.EquivalentWeakNamed:
                    return true;
                case AssemblyComparisonResult.NonEquivalentPartialVersion:
                case AssemblyComparisonResult.NonEquivalentVersion:
                    if (bestMatchLevel < 1) {
                        bestMatchLevel = 1;
                        bestMatch = assemblyDef;
                    }
                    return false;
                case AssemblyComparisonResult.EquivalentUnified:
                case AssemblyComparisonResult.EquivalentPartialUnified:
                    if (bestMatchLevel < 2) {
                        bestMatchLevel = 2;
                        bestMatch = assemblyDef;
                    }
                    return false;
                case AssemblyComparisonResult.NonEquivalent:
                case AssemblyComparisonResult.Unknown:
                    return false;
                default:
                    throw new NotImplementedException();
            }
        }

        public bool ResolveReference(Dictionary<string, Assembly> cache, List<Assembly> references, string reference) {
            string[] files = new string[0];
            try {
                string path = Path.GetDirectoryName(reference);
                files = Directory.GetFiles(path == "" ? "." : path, Path.GetFileName(reference));
            } catch (ArgumentException) {
            } catch (IOException) {
            }

            if (files.Length == 0) {
                Assembly asm = null;
                cache.TryGetValue(reference, out asm);
                if (asm == null) {
                    foreach (string found in FindAssemblyPath(reference)) {
                        asm = LoadFile(found);
                        cache.Add(reference, asm);
                        break;
                    }
                }
                if (asm == null) {
                    Console.Error.WriteLine("Error: reference not found: {0}", reference);
                    return false;
                }

                references.Add(asm);
            } else {
                foreach (string file in files) {
                    Assembly asm;
                    if (!cache.TryGetValue(file, out asm)) {
                        asm = LoadFile(file);
                    }
                    references.Add(asm);
                }
            }
            return true;
        }

        private Assembly LoadMscorlib(IEnumerable<string> references) {
            if (references != null) {
                foreach (string r in references) {
                    try {
                        if (AssemblyName.GetAssemblyName(r).Name == "mscorlib") {
                            return LoadFile(r);
                        }
                    } catch {
                    }
                }
            }
            foreach (string mscorlib in FindAssemblyPath("mscorlib.dll")) {
                return LoadFile(mscorlib);
            }
            ConsoleOps.Error(true, "unable to find mscorlib.dll");
            return null;
        }

        private void AddHintPath(string assemblyName, string path) {
            _hintpaths.Add(assemblyName, path);
        }

        private IEnumerable<string> FindAssemblyPath(string file) {
            if (Path.IsPathRooted(file)) {
                if (File.Exists(file)) {
                    yield return file;
                }
            } else {
                foreach (string dir in _libpaths) {
                    string path = Path.Combine(dir, file);
                    if (File.Exists(path)) {
                        yield return path;
                    }
                    // for legacy compat, we try again after appending .dll
                    path = Path.Combine(dir, file + ".dll");
                    if (File.Exists(path)) {
                        ConsoleOps.Warning("Found assembly '{0}' using legacy search rule, please append '.dll' to the reference", file);
                        yield return path;
                    }
                }
            }
        }

        private Assembly LoadFile(string path) {
            string ex = null;
            try {
                using (RawModule module = _universe.OpenRawModule(path)) {
                    if (_mscorlibVersion != null) {
                        // to avoid problems (i.e. weird exceptions), we don't allow assemblies to load that reference a newer version of mscorlib
                        foreach (AssemblyName asmref in module.GetReferencedAssemblies()) {
                            if (asmref.Name == "mscorlib" && asmref.Version > _mscorlibVersion) {
                                ConsoleOps.Error(true, "unable to load assembly '{0}' as it depends on a higher version of mscorlib than the one currently loaded", path);
                            }
                        }
                    }
                    Assembly asm = _universe.LoadAssembly(module);
                    if (asm.Location != module.Location && CanonicalizePath(asm.Location) != CanonicalizePath(module.Location)) {
                        ConsoleOps.Warning("assembly '{0}' is ignored as previously loaded assembly '{1}' has the same identity '{2}'", path, asm.Location, asm.FullName);
                    }
                    return asm;
                }
            } catch (IOException x) {
                ex = x.Message;
            } catch (UnauthorizedAccessException x) {
                ex = x.Message;
            } catch (IKVM.Reflection.BadImageFormatException x) {
                ex = x.Message;
            }

            ConsoleOps.Error(true, "unable to load assembly '{0}'" + Environment.NewLine + "    ({1})", path, ex);
            return null;
        }

        private static string CanonicalizePath(string path) {
            try {
                FileInfo fi = new FileInfo(path);
                if (fi.DirectoryName == null) {
                    return path.Length > 1 && path[1] == ':' ? path.ToUpperInvariant() : path;
                }
                string dir = CanonicalizePath(fi.DirectoryName);
                string name = fi.Name;
                try {
                    string[] arr = System.IO.Directory.GetFileSystemEntries(dir, name);
                    if (arr.Length == 1) {
                        name = arr[0];
                    }
                } catch (UnauthorizedAccessException) {
                } catch (IOException) {
                }
                return Path.Combine(dir, name);
            } catch (UnauthorizedAccessException) {
            } catch (IOException) {
            } catch (System.Security.SecurityException) {
            } catch (NotSupportedException) {
            }
            return path;
        }
    }
}
