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

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

using Microsoft.Scripting;
using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Modules;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

namespace IronPython.Runtime {

    /// <summary>
    /// Importer class - used for importing modules.  Used by Ops and __builtin__
    /// Singleton living on Python engine.
    /// </summary>
    public static class Importer {
        internal const string ModuleReloadMethod = "PerformModuleReload";

        #region Internal API Surface

        /// <summary>
        /// Gateway into importing ... called from Ops.  Performs the initial import of
        /// a module and returns the module.
        /// </summary>
        public static object Import(CodeContext/*!*/ context, string fullName, PythonTuple from, int level) {
            return LightExceptions.CheckAndThrow(ImportLightThrow(context, fullName, from, level));
        }

        /// <summary>
        /// Gateway into importing ... called from Ops.  Performs the initial import of
        /// a module and returns the module.  This version returns light exceptions instead of throwing.
        /// </summary>
        [LightThrowing]
        internal static object ImportLightThrow(CodeContext/*!*/ context, string fullName, PythonTuple from, int level) {
            PythonContext pc = PythonContext.GetContext(context);

            if (level == -1) {
                // no specific level provided, call the 4 param version so legacy code continues to work
                var site = pc.OldImportSite;
                return site.Target(
                    site,
                    context,
                    FindImportFunction(context),
                    fullName,
                    Builtin.globals(context),
                    context.Dict,
                    from
                );
            } else {

                // relative import or absolute import, in other words:
                //
                // from . import xyz
                // or 
                // from __future__ import absolute_import
                var site = pc.ImportSite;
                return site.Target(
                    site,
                    context,
                    FindImportFunction(context),
                    fullName,
                    Builtin.globals(context),
                    context.Dict,
                    from,
                    level
                );
            }
        }

        /// <summary>
        /// Gateway into importing ... called from Ops.  This is called after
        /// importing the module and is used to return individual items from
        /// the module.  The outer modules dictionary is then updated with the
        /// result.
        /// </summary>
        public static object ImportFrom(CodeContext/*!*/ context, object from, string name) {
                PythonModule scope = from as PythonModule;
                PythonType pt;
                NamespaceTracker nt;
                if (scope != null) {
                    object ret;
                    if (scope.GetType() == typeof(PythonModule)) {
                        if (scope.__dict__.TryGetValue(name, out ret)) {
                            return ret;
                        }
                    } else {
                        // subclass of module, it could have overridden __getattr__ or __getattribute__
                        if (PythonOps.TryGetBoundAttr(context, scope, name, out ret)) {
                            return ret;
                        }
                    }

                    object path;
                    List listPath;
                    string stringPath;
                	if (scope.__dict__._storage.TryGetPath(out path)) {
                    	if ((listPath = path as List) != null) {
                      		return ImportNestedModule(context, scope, new[] { name }, 0, listPath);
                    	} else if((stringPath = path as string) != null) {
                        	return ImportNestedModule(context, scope, new[] { name }, 0, List.FromArrayNoCopy(stringPath));
                    	}
                	}
                } else if ((pt = from as PythonType) != null) {
                    PythonTypeSlot pts;
                    object res;
                    if (pt.TryResolveSlot(context, name, out pts) &&
                        pts.TryGetValue(context, null, pt, out res)) {
                        return res;
                    }
                } else if ((nt = from as NamespaceTracker) != null) {
                    object res = NamespaceTrackerOps.GetCustomMember(context, nt, name);
                    if (res != OperationFailed.Value) {
                        return res;
                    }
                } else {
                    // This is too lax, for example it allows from module.class import member
                    object ret;
                    if (PythonOps.TryGetBoundAttr(context, from, name, out ret)) {
                        return ret;
                    }
                }
            
            throw PythonOps.ImportError("Cannot import name {0}", name);
        }

        private static object ImportModuleFrom(CodeContext/*!*/ context, object from, string[] parts, int current) {
            PythonModule scope = from as PythonModule;
            if (scope != null) {
                object path;
                List listPath;
                string stringPath;
                if (scope.__dict__._storage.TryGetPath(out path)) {
                    if ((listPath = path as List) != null) {
                        return ImportNestedModule(context, scope, parts, current, listPath);
                    } else if((stringPath = path as string) != null) {
                        return ImportNestedModule(context, scope, parts, current, List.FromArrayNoCopy(stringPath));
                    }
                } else {
                    PythonType t = DynamicHelpers.GetPythonType(scope);
                    if (t.TryGetMember(context, scope, "__path__", out path)) {
                        if((listPath = path as List) != null) {
                            return ImportNestedModule(context, scope, parts, current, listPath);
                        } else if((stringPath = path as string) != null) {
                            return ImportNestedModule(context, scope, parts, current, List.FromArrayNoCopy(stringPath));
                        }
                    }
                }
            }

            NamespaceTracker ns = from as NamespaceTracker;
            if (ns != null) {
                object val;
                if (ns.TryGetValue(parts[current], out val)) {
                    return MemberTrackerToPython(context, val);
                }
            }

            throw PythonOps.ImportError("No module named {0}", parts[current]);
        }

        /// <summary>
        /// Called by the __builtin__.__import__ functions (general importing) and ScriptEngine (for site.py)
        /// 
        /// level indiciates whether to perform absolute or relative imports.
        ///     -1 indicates both should be performed
        ///     0 indicates only absolute imports should be performed
        ///     Positive numbers indicate the # of parent directories to search relative to the calling module
        /// </summary>        
        public static object ImportModule(CodeContext/*!*/ context, object globals, string/*!*/ modName, bool bottom, int level) {
            if (modName.IndexOf(Path.DirectorySeparatorChar) != -1) {
                throw PythonOps.ImportError("Import by filename is not supported.", modName);
            }

            string package = null;
            object attribute;
            PythonDictionary pyGlobals = globals as PythonDictionary;
            if (pyGlobals != null) {
                if (pyGlobals._storage.TryGetPackage(out attribute)) {
                    package = attribute as string;
                    if (package == null && attribute != null) {
                        throw PythonOps.ValueError("__package__ set to non-string");
                    }
                } else {
                    package = null;
                    if (level > 0) {
                        // explicit relative import, calculate and store __package__
                        object pathAttr, nameAttr;
                        if (pyGlobals._storage.TryGetName(out nameAttr) && nameAttr is string) {
                            if (pyGlobals._storage.TryGetPath(out pathAttr)) {
                                pyGlobals["__package__"] = nameAttr;
                            } else {
                                pyGlobals["__package__"] = ((string)nameAttr).rpartition(".")[0];
                            }
                        }
                    }
                }
            }

            object newmod = null;
            string firstName;
            int firstDot = modName.IndexOf('.');
            if (firstDot == -1) {
                firstName = modName;
            } else {
                firstName = modName.Substring(0, firstDot);
            }
            string finalName = null;

            if (level != 0) {
                // try a relative import

                // if importing a.b.c, import "a" first and then import b.c from a
                string name;    // name of the module we are to import in relation to the current module
                PythonModule parentModule;
                List path;      // path to search
                if (TryGetNameAndPath(context, globals, firstName, level, package, out name, out path, out parentModule)) {
                    finalName = name;
                    // import relative
                    if (!TryGetExistingOrMetaPathModule(context, name, path, out newmod)) {
                        newmod = ImportFromPath(context, firstName, name, path);
                        if (newmod == null) {
                            // add an indirection entry saying this module does not exist
                            // see http://www.python.org/doc/essays/packages.html "Dummy Entries"
                            context.LanguageContext.SystemStateModules[name] = null;
                        } else if (parentModule != null) {
                            parentModule.__dict__[firstName] = newmod;
                        }
                        
                    } else if (firstDot == -1) {
                        // if we imported before having the assembly
                        // loaded and then loaded the assembly we want
                        // to make the assembly available now.

                        if (newmod is NamespaceTracker) {
                            context.ShowCls = true;
                        }
                    }
                }
            }

            if (level <= 0) {
                // try an absolute import
                if (newmod == null) {
                    object parentPkg;
                    if (!String.IsNullOrEmpty(package) && !PythonContext.GetContext(context).SystemStateModules.TryGetValue(package, out parentPkg)) {
                        PythonModule warnModule = new PythonModule();
                        warnModule.__dict__["__file__"] = package;
                        warnModule.__dict__["__name__"] = package;
                        ModuleContext modContext = new ModuleContext(warnModule.__dict__, context.LanguageContext);
                        PythonOps.Warn(
                            modContext.GlobalContext,
                            PythonExceptions.RuntimeWarning,
                            "Parent module '{0}' not found while handling absolute import",
                            package);
                    }
                    newmod = ImportTopAbsolute(context, firstName);
                    finalName = firstName;
                    if (newmod == null) {
                        return null;
                    }
                }
            }

            // now import the a.b.c etc.  a needs to be included here
            // because the process of importing could have modified
            // sys.modules.
            string[] parts = modName.Split('.');
            object next = newmod;
            string curName = null;
            for (int i = 0; i < parts.Length; i++) {
                curName = i == 0 ? finalName : curName + "." + parts[i];
                object tmpNext;
                if (TryGetExistingModule(context, curName, out tmpNext)) {
                    next = tmpNext;
                    if (i == 0) {
                        // need to update newmod if we pulled it out of sys.modules
                        // just in case we're in bottom mode.
                        newmod = next;
                    }
                } else if (i != 0) {
                    // child module isn't loaded yet, import it.
                    next = ImportModuleFrom(context, next, parts, i);
                } else {
                    // top-level module doesn't exist in sys.modules, probably
                    // came from some weird meta path hook.
                    newmod = next;
                }
            }

            return bottom ? next : newmod;
        }

        /// <summary>
        /// Interrogates the importing module for __name__ and __path__, which determine
        /// whether the imported module (whose name is 'name') is being imported as nested
        /// module (__path__ is present) or as sibling.
        /// 
        /// For sibling import, the full name of the imported module is parent.sibling
        /// For nested import, the full name of the imported module is parent.module.nested
        /// where parent.module is the mod.__name__
        /// </summary>
        /// <param name="context"></param>
        /// <param name="globals">the globals dictionary</param>
        /// <param name="name">Name of the module to be imported</param>
        /// <param name="full">Output - full name of the module being imported</param>
        /// <param name="path">Path to use to search for "full"</param>
        /// <param name="level">the import level for relaive imports</param>
        /// <param name="parentMod">the parent module</param>
        /// <param name="package">the global __package__ value</param>
        /// <returns></returns>
        private static bool TryGetNameAndPath(CodeContext/*!*/ context, object globals, string name, int level, string package, out string full, out List path, out PythonModule parentMod) {
            Debug.Assert(level != 0);   // shouldn't be here for absolute imports

            // Unless we can find enough information to perform relative import,
            // we are going to import the module whose name we got
            full = name;
            path = null;
            parentMod = null;

            // We need to get __name__ to find the name of the imported module.
            // If absent, fall back to absolute import
            object attribute;

            PythonDictionary pyGlobals = globals as PythonDictionary;
            if (pyGlobals == null || !pyGlobals._storage.TryGetName(out attribute)) {
                return false;
            }

            // And the __name__ needs to be string
            string modName = attribute as string;
            if (modName == null) {
                return false;
            }

            string pn;
            if (package == null) {
                // If the module has __path__ (and __path__ is list), nested module is being imported
                // otherwise, importing sibling to the importing module
                if (pyGlobals._storage.TryGetPath(out attribute) && (path = attribute as List) != null) {
                    // found __path__, importing nested module. The actual name of the nested module
                    // is the name of the mod plus the name of the imported module
                    if (level == -1) {
                        // absolute import of some module
                        full = modName + "." + name;
                        object parentModule;
                        if (PythonContext.GetContext(context).SystemStateModules.TryGetValue(modName, out parentModule)) {
                            parentMod = parentModule as PythonModule;
                        }
                    } else if (String.IsNullOrEmpty(name)) {
                        // relative import of ancestor
                        full = (StringOps.rsplit(modName, ".", level - 1)[0] as string);
                    } else {
                        // relative import of some ancestors child
                        string parentName = (StringOps.rsplit(modName, ".", level - 1)[0] as string);
                        full = parentName + "." + name;
                        object parentModule;
                        if (PythonContext.GetContext(context).SystemStateModules.TryGetValue(parentName, out parentModule)) {
                            parentMod = parentModule as PythonModule;
                        }
                    }
                    return true;
                }

                // importing sibling. The name of the imported module replaces
                // the last element in the importing module name
                int lastDot = modName.LastIndexOf('.');
                if (lastDot == -1) {
                    // name doesn't include dot, only absolute import possible
                    if (level > 0) {
                        throw PythonOps.ValueError("Attempted relative import in non-package");
                    }

                    return false;
                }

                // need to remove more than one name
                int tmpLevel = level;
                while (tmpLevel > 1 && lastDot != -1) {
                    lastDot = modName.LastIndexOf('.', lastDot - 1);
                    tmpLevel--;
                }

                if (lastDot == -1) {
                    pn = modName;
                } else {
                    pn = modName.Substring(0, lastDot);
                }
            } else {
                // __package__ doesn't include module name, so level is - 1.
                pn = GetParentPackageName(level - 1, package.Split('.'));
            }

            path = GetParentPathAndModule(context, pn, out parentMod);
            if (path != null) {
                if (String.IsNullOrEmpty(name)) {
                    full = pn;
                } else {
                    full = pn + "." + name;
                }
                return true;
            }

            if (level > 0) {
                throw PythonOps.SystemError("Parent module '{0}' not loaded, cannot perform relative import", pn);
            }
            // not enough information - absolute import
            return false;
        }

        private static string GetParentPackageName(int level, string[] names) {
            StringBuilder parentName = new StringBuilder(names[0]);

            if (level < 0) level = 1;
            for (int i = 1; i < names.Length - level; i++) {
                parentName.Append('.');
                parentName.Append(names[i]);
            }
            return parentName.ToString();
        }

        public static object ReloadModule(CodeContext/*!*/ context, PythonModule/*!*/ module) {
            return ReloadModule(context, module, null);
        }

        internal static object ReloadModule(CodeContext/*!*/ context, PythonModule/*!*/ module, PythonFile file) {
            PythonContext pc = PythonContext.GetContext(context);

            // We created the module and it only contains Python code. If the user changes
            // __file__ we'll reload from that file. 
            string fileName = module.GetFile() as string;

            // built-in module:
            if (fileName == null) {
                ReloadBuiltinModule(context, module);
                return module;
            }

            string name = module.GetName() as string;
            if (name != null) {
                List path = null;
                // find the parent module and get it's __path__ property
                int dotIndex = name.LastIndexOf('.');
                if (dotIndex != -1) {
                    PythonModule parentModule;
                    path = GetParentPathAndModule(context, name.Substring(0, dotIndex), out parentModule);
                }

                object reloaded;
                if (TryLoadMetaPathModule(context, module.GetName() as string, path, out reloaded) && reloaded != null) {
                    return module;
                }

                List sysPath;
                if (PythonContext.GetContext(context).TryGetSystemPath(out sysPath)) {
                    object ret = ImportFromPathHook(context, name, name, sysPath, null);
                    if (ret != null) {
                        return ret;
                    }
                }
            }

            SourceUnit sourceUnit;
            if (file != null) {
                sourceUnit = pc.CreateSourceUnit(new PythonFileStreamContentProvider(file), fileName, file.Encoding, SourceCodeKind.File);
            } else {
                if (!pc.DomainManager.Platform.FileExists(fileName)) {
                    throw PythonOps.SystemError("module source file not found");
                }

                sourceUnit = pc.CreateFileUnit(fileName, pc.DefaultEncoding, SourceCodeKind.File);
            }
            pc.GetScriptCode(sourceUnit, name, ModuleOptions.None, Compiler.CompilationMode.Lookup).Run(module.Scope);
            return module;
        }

        class PythonFileStreamContentProvider : StreamContentProvider {
            private readonly PythonFile _file;
            public PythonFileStreamContentProvider(PythonFile file) {
                _file = file;
            }

            public override Stream GetStream() {
                return _file._stream;
            }
        }

        /// <summary>
        /// Given the parent module name looks up the __path__ property.
        /// </summary>
        private static List GetParentPathAndModule(CodeContext/*!*/ context, string/*!*/ parentModuleName, out PythonModule parentModule) {
            List path = null;
            object parentModuleObj;
            parentModule = null;

            // Try lookup parent module in the sys.modules
            if (PythonContext.GetContext(context).SystemStateModules.TryGetValue(parentModuleName, out parentModuleObj)) {
                // see if it's a module
                parentModule = parentModuleObj as PythonModule;
                if (parentModule != null) {
                    object objPath;
                    // get its path as a List if it's there
                    if (parentModule.__dict__._storage.TryGetPath(out objPath)) {
                        path = objPath as List;
                    }
                }
            }
            return path;
        }

        private static void ReloadBuiltinModule(CodeContext/*!*/ context, PythonModule/*!*/ module) {
            Assert.NotNull(module);
            Debug.Assert(module.GetName() is string, "Module is reloadable only if its name is a non-null string");
            Type type;

            string name = (string)module.GetName();
            PythonContext pc = PythonContext.GetContext(context);

            if (!pc.BuiltinModules.TryGetValue(name, out type)) {
                throw PythonOps.ImportError("no module named {0}", module.GetName());
            }

            // should be a built-in module which we can reload.
            Debug.Assert(((PythonDictionary)module.__dict__)._storage is ModuleDictionaryStorage);

            ((ModuleDictionaryStorage)module.__dict__._storage).Reload();
        }

        /// <summary>
        /// Trys to get an existing module and if that fails fall backs to searching 
        /// </summary>
        private static bool TryGetExistingOrMetaPathModule(CodeContext/*!*/ context, string fullName, List path, out object ret) {
            if (TryGetExistingModule(context, fullName, out ret)) {
                return true;
            }

            return TryLoadMetaPathModule(context, fullName, path, out ret);
        }

        /// <summary>
        /// Attempts to load a module from sys.meta_path as defined in PEP 302.
        /// 
        /// The meta_path provides a list of importer objects which can be used to load modules before
        /// searching sys.path but after searching built-in modules.
        /// </summary>
        private static bool TryLoadMetaPathModule(CodeContext/*!*/ context, string fullName, List path, out object ret) {
            List metaPath = PythonContext.GetContext(context).GetSystemStateValue("meta_path") as List;
            if (metaPath != null) {
                foreach (object importer in (IEnumerable)metaPath) {
                    if (FindAndLoadModuleFromImporter(context, importer, fullName, path, out ret)) {
                        return true;
                    }
                }
            }

            ret = null;
            return false;
        }

        /// <summary>
        /// Given a user defined importer object as defined in PEP 302 tries to load a module.
        /// 
        /// First the find_module(fullName, path) is invoked to get a loader, then load_module(fullName) is invoked
        /// </summary>
        private static bool FindAndLoadModuleFromImporter(CodeContext/*!*/ context, object importer, string fullName, List path, out object ret) {
            object find_module = PythonOps.GetBoundAttr(context, importer, "find_module");

            PythonContext pycontext = PythonContext.GetContext(context);
            object loader = pycontext.Call(context, find_module, fullName, path);
            if (loader != null) {
                object findMod = PythonOps.GetBoundAttr(context, loader, "load_module");
                ret = pycontext.Call(context, findMod, fullName);
                return ret != null;
            }

            ret = null;
            return false;
        }

        internal static bool TryGetExistingModule(CodeContext/*!*/ context, string/*!*/ fullName, out object ret) {
            if (PythonContext.GetContext(context).SystemStateModules.TryGetValue(fullName, out ret)) {
                return true;
            }
            return false;
        }

        #endregion

        #region Private Implementation Details

        private static object ImportTopAbsolute(CodeContext/*!*/ context, string/*!*/ name) {
            object ret;
            if (TryGetExistingModule(context, name, out ret)) {
                if (IsReflected(ret)) {
                    // Even though we found something in sys.modules, we need to check if a
                    // clr.AddReference has invalidated it. So try ImportReflected again.
                    ret = ImportReflected(context, name) ?? ret;
                }

                NamespaceTracker rp = ret as NamespaceTracker;
                if (rp != null || ret == PythonContext.GetContext(context).ClrModule) {
                    context.ShowCls = true;
                }

                return ret;
            }

            if (TryLoadMetaPathModule(context, name, null, out ret)) {
                return ret;
            }

            ret = ImportBuiltin(context, name);
            if (ret != null) return ret;

            List path;
            if (PythonContext.GetContext(context).TryGetSystemPath(out path)) {
                ret = ImportFromPath(context, name, name, path);
                if (ret != null) return ret;
            }

            ret = ImportReflected(context, name);
            if (ret != null) return ret;

            return null;
        }

        private static string [] SubArray(string[] t, int len) {
            var ret = new string[len];
            Array.Copy(t, ret, len);
            return ret;
        }

        private static bool TryGetNestedModule(CodeContext/*!*/ context, PythonModule/*!*/ scope,
            string[]/*!*/ parts, int current, out object nested) {
            string name = parts[current];
            Assert.NotNull(context, scope, name);
            if (scope.__dict__.TryGetValue(name, out nested)) {
                var pm = nested as PythonModule;
                if (pm != null) {
                    var fullPath = ".".join(SubArray(parts, current));
                    // double check, some packages mess with package namespace
                    // see cp35116
                    if (pm.GetName() == fullPath) {
                        return true;
                    }
                }
                // This allows from System.Math import *
                PythonType dt = nested as PythonType;
                if (dt != null && dt.IsSystemType) {
                    return true;
                }
            }
            return false;
        }

        private static object ImportNestedModule(CodeContext/*!*/ context, PythonModule/*!*/ module,
            string[] parts, int current, List/*!*/ path) {
            object ret;
            string name = parts[current];
            string fullName = CreateFullName(module.GetName() as string, name);

            if (TryGetExistingOrMetaPathModule(context, fullName, path, out ret)) {
                module.__dict__[name] = ret;
                return ret;
            }

            if (TryGetNestedModule(context, module, parts, current, out ret)) {
                return ret;
            }

            ImportFromPath(context, name, fullName, path);
            object importedModule;
            if (PythonContext.GetContext(context).SystemStateModules.TryGetValue(fullName, out importedModule)) {
                module.__dict__[name] = importedModule;
                return importedModule;
            }

            throw PythonOps.ImportError("cannot import {0} from {1}", name, module.GetName());
        }

        private static object FindImportFunction(CodeContext/*!*/ context) {
            PythonDictionary builtins = context.GetBuiltinsDict() ?? PythonContext.GetContext(context).BuiltinModuleDict;

            object import;
            if (builtins._storage.TryGetImport(out import)) {
                return import;
            }

            throw PythonOps.ImportError("cannot find __import__");
        }

        internal static object ImportBuiltin(CodeContext/*!*/ context, string/*!*/ name) {
            Assert.NotNull(context, name);

            PythonContext pc = PythonContext.GetContext(context);
            if (name == "sys") {
                return pc.SystemState;
            } else if (name == "clr") {
                context.ShowCls = true;
                pc.SystemStateModules["clr"] = pc.ClrModule;
                return pc.ClrModule;
            }

            return pc.GetBuiltinModule(name);
        }

        private static object ImportReflected(CodeContext/*!*/ context, string/*!*/ name) {
            object ret;
            PythonContext pc = PythonContext.GetContext(context);
            if (!PythonOps.ScopeTryGetMember(context, pc.DomainManager.Globals, name, out ret) &&
                (ret = pc.TopNamespace.TryGetPackageAny(name)) == null) {
                ret = TryImportSourceFile(pc, name);
            }

            ret = MemberTrackerToPython(context, ret);
            if (ret != null) {
                PythonContext.GetContext(context).SystemStateModules[name] = ret;
            }
            return ret;
        }

        internal static object MemberTrackerToPython(CodeContext/*!*/ context, object ret) {
            MemberTracker res = ret as MemberTracker;
            if (res != null) {
                context.ShowCls = true;
                object realRes = res;

                switch (res.MemberType) {
                    case TrackerTypes.Type: realRes = DynamicHelpers.GetPythonTypeFromType(((TypeTracker)res).Type); break;
                    case TrackerTypes.Field: realRes = PythonTypeOps.GetReflectedField(((FieldTracker)res).Field); break;
                    case TrackerTypes.Event: realRes = PythonTypeOps.GetReflectedEvent((EventTracker)res); break;
                    case TrackerTypes.Method:
                        MethodTracker mt = res as MethodTracker;
                        realRes = PythonTypeOps.GetBuiltinFunction(mt.DeclaringType, mt.Name, new MemberInfo[] { mt.Method });
                        break;
                }

                ret = realRes;
            }
            return ret;
        }

        internal static PythonModule TryImportSourceFile(PythonContext/*!*/ context, string/*!*/ name) {
            var sourceUnit = TryFindSourceFile(context, name);
            PlatformAdaptationLayer pal = context.DomainManager.Platform;
            if (sourceUnit == null ||
                GetFullPathAndValidateCase(context, pal.CombinePaths(pal.GetDirectoryName(sourceUnit.Path), name + pal.GetExtension(sourceUnit.Path)), false) == null) {
                return null;
            }

            var scope = ExecuteSourceUnit(context, sourceUnit);
            if (sourceUnit.LanguageContext != context) {
                // foreign language, we should publish in sys.modules too
                context.SystemStateModules[name] = scope;
            }
            PythonOps.ScopeSetMember(context.SharedContext, sourceUnit.LanguageContext.DomainManager.Globals, name, scope);
            return scope;
        }

        internal static PythonModule ExecuteSourceUnit(PythonContext context, SourceUnit/*!*/ sourceUnit) {
            ScriptCode compiledCode = sourceUnit.Compile();
            Scope scope = compiledCode.CreateScope();
            PythonModule res = ((PythonScopeExtension)context.EnsureScopeExtension(scope)).Module;
            compiledCode.Run(scope);
            return res;
        }

        internal static SourceUnit TryFindSourceFile(PythonContext/*!*/ context, string/*!*/ name) {
            List paths;
            if (!context.TryGetSystemPath(out paths)) {
                return null;
            }

            foreach (object dirObj in paths) {
                string directory = dirObj as string;
                if (directory == null) continue;  // skip invalid entries

                string candidatePath = null;
                LanguageContext candidateLanguage = null;
                foreach (string extension in context.DomainManager.Configuration.GetFileExtensions()) {
                    string fullPath;

                    try {
                        fullPath = context.DomainManager.Platform.CombinePaths(directory, name + extension);
                    } catch (ArgumentException) {
                        // skip invalid paths
                        continue;
                    }

                    if (context.DomainManager.Platform.FileExists(fullPath)) {
                        if (candidatePath != null) {
                            throw PythonOps.ImportError(String.Format("Found multiple modules of the same name '{0}': '{1}' and '{2}'",
                                name, candidatePath, fullPath));
                        }

                        candidatePath = fullPath;
                        candidateLanguage = context.DomainManager.GetLanguageByExtension(extension);
                    }
                }

                if (candidatePath != null) {
                    return candidateLanguage.CreateFileUnit(candidatePath);
                }
            }

            return null;
        }

        private static bool IsReflected(object module) {
            // corresponds to the list of types that can be returned by ImportReflected
            return module is MemberTracker
                || module is PythonType
                || module is ReflectedEvent
                || module is ReflectedField
                || module is BuiltinFunction;
        }

        private static string CreateFullName(string/*!*/ baseName, string name) {
            if (baseName == null || baseName.Length == 0 || baseName == "__main__") {
                return name;
            }
            return baseName + "." + name;
        }

        #endregion

        private static object ImportFromPath(CodeContext/*!*/ context, string/*!*/ name, string/*!*/ fullName, List/*!*/ path) {
            return ImportFromPathHook(context, name, fullName, path, LoadFromDisk);
        }

        private static object ImportFromPathHook(CodeContext/*!*/ context, string/*!*/ name, string/*!*/ fullName, List/*!*/ path, Func<CodeContext, string, string, string, object> defaultLoader) {
            Assert.NotNull(context, name, fullName, path);

            IDictionary<object, object> importCache = PythonContext.GetContext(context).GetSystemStateValue("path_importer_cache") as IDictionary<object, object>;

            if (importCache == null) {
                return null;
            }

            foreach (object dirname in (IEnumerable)path) {
                string str = dirname as string;

                if (str != null || (Converter.TryConvertToString(dirname, out str) && str != null)) {  // ignore non-string
                    object importer;
                    if (!importCache.TryGetValue(str, out importer)) {
                        importCache[str] = importer = FindImporterForPath(context, str);
                    }

                    if (importer != null) {
                        // user defined importer object, get the loader and use it.
                        object ret;
                        if (FindAndLoadModuleFromImporter(context, importer, fullName, null, out ret)) {
                            return ret;
                        }
                    } else if (defaultLoader != null) {
                        object res = defaultLoader(context, name, fullName, str);
                        if (res != null) {
                            return res;
                        }
                    }
                }
            }

            return null;
        }

        internal static bool TryImportMainFromZip(CodeContext/*!*/ context, string/*!*/ path, out object importer) {
            Assert.NotNull(context, path);
            var importCache = PythonContext.GetContext(context).GetSystemStateValue("path_importer_cache") as IDictionary<object, object>;
            if (importCache == null) {
                importer = null;
                return false;
            }
            importCache[path] = importer = FindImporterForPath(context, path);
            if (importer == null) {
                return false;
            }
            // for consistency with cpython, insert zip as a first entry into sys.path
            var syspath = PythonContext.GetContext(context).GetSystemStateValue("path") as List;
            if (syspath != null) {
                syspath.Insert(0, path);
            }
            object dummy;
            return FindAndLoadModuleFromImporter(context, importer, "__main__", null, out dummy);
        }

        private static object LoadFromDisk(CodeContext context, string name, string fullName, string str) {
            // default behavior
            PythonModule module;
            string pathname = context.LanguageContext.DomainManager.Platform.CombinePaths(str, name);

            module = LoadPackageFromSource(context, fullName, pathname);
            if (module != null) {
                return module;
            }

            string filename = pathname + ".py";
            module = LoadModuleFromSource(context, fullName, filename);
            if (module != null) {
                return module;
            }
            return null;
        }

        /// <summary>
        /// Finds a user defined importer for the given path or returns null if no importer
        /// handles this path.
        /// </summary>
        private static object FindImporterForPath(CodeContext/*!*/ context, string dirname) {
            List pathHooks = PythonContext.GetContext(context).GetSystemStateValue("path_hooks") as List;

            foreach (object hook in (IEnumerable)pathHooks) {
                try {
                    object handler = PythonCalls.Call(context, hook, dirname);

                    if (handler != null) {
                        return handler;
                    }
                } catch (ImportException) {
                    // we can't handle the path
                }
            }

#if !SILVERLIGHT    // DirectoryExists isn't implemented on Silverlight
            if (!context.LanguageContext.DomainManager.Platform.DirectoryExists(dirname)) {
                return new PythonImport.NullImporter(dirname);
            }
#endif

            return null;
        }

        private static PythonModule LoadModuleFromSource(CodeContext/*!*/ context, string/*!*/ name, string/*!*/ path) {
            Assert.NotNull(context, name, path);

            PythonContext pc = PythonContext.GetContext(context);

            string fullPath = GetFullPathAndValidateCase(pc, path, false);
            if (fullPath == null || !pc.DomainManager.Platform.FileExists(fullPath)) {
                return null;
            }

            SourceUnit sourceUnit = pc.CreateFileUnit(fullPath, pc.DefaultEncoding, SourceCodeKind.File);
            return LoadFromSourceUnit(context, sourceUnit, name, sourceUnit.Path);
        }

        private static string GetFullPathAndValidateCase(LanguageContext/*!*/ context, string path, bool isDir) {
#if !SILVERLIGHT
            // check for a match in the case of the filename, unfortunately we can't do this
            // in Silverlight becauase there's no way to get the original filename.

            PlatformAdaptationLayer pal = context.DomainManager.Platform;
            string dir = pal.GetDirectoryName(path);
            if (!pal.DirectoryExists(dir)) {
                return null;
            }

            try {
                string file = pal.GetFileName(path);
                string[] files = pal.GetFileSystemEntries(dir, file, !isDir, isDir);

                if (files.Length != 1 || pal.GetFileName(files[0]) != file) {
                    return null;
                }

                return pal.GetFullPath(files[0]);
            } catch (IOException) {
                return null;
            }
#else
            return path;
#endif
        }

        internal static PythonModule LoadPackageFromSource(CodeContext/*!*/ context, string/*!*/ name, string/*!*/ path) {
            Assert.NotNull(context, name, path);

            path = GetFullPathAndValidateCase(PythonContext.GetContext(context), path, true);
            if (path == null) {
                return null;
            }

            if(context.LanguageContext.DomainManager.Platform.DirectoryExists(path) && !context.LanguageContext.DomainManager.Platform.FileExists(context.LanguageContext.DomainManager.Platform.CombinePaths(path, "__init__.py"))) {
                PythonOps.Warn(context, PythonExceptions.ImportWarning, "Not importing directory '{0}': missing __init__.py", path);
            }

            return LoadModuleFromSource(context, name, context.LanguageContext.DomainManager.Platform.CombinePaths(path, "__init__.py"));
        }

        private static PythonModule/*!*/ LoadFromSourceUnit(CodeContext/*!*/ context, SourceUnit/*!*/ sourceCode, string/*!*/ name, string/*!*/ path) {
            Assert.NotNull(sourceCode, name, path);
            return PythonContext.GetContext(context).CompileModule(path, name, sourceCode, ModuleOptions.Initialize | ModuleOptions.Optimized);
        }
    }
}
