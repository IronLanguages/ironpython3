/***********************************************************************************
 *
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A
 * copy of the license can be found in the License.html file at the root of this distribution. If
 * you cannot locate the  Apache License, Version 2.0, please send an email to
 * ironpy@microsoft.com. By using this source code in any fashion, you are agreeing to be bound
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 *
 ***********************************************************************************/


using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using IronPython.Runtime;
using IronPython.Runtime.Operations;
using IronPython.Hosting;
using IKVM.Reflection;
using IKVM.Reflection.Emit;
using System.Resources;

using Type = IKVM.Reflection.Type;

using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Hosting;
using Microsoft.Scripting.Hosting.Providers;


namespace IronPythonCompiler {
    
    class Program {

        /// <summary>
        /// Generates the stub .exe file for starting the app
        /// </summary>
        /// <param name="config"></param>
        static void GenerateExe(Config config) {
            var u = new Universe();
            var aName = new AssemblyName(Path.GetFileNameWithoutExtension(new FileInfo(config.Output).Name));
            var ab = u.DefineDynamicAssembly(aName, AssemblyBuilderAccess.Save, Path.GetDirectoryName(config.Output));
            var mb = ab.DefineDynamicModule(config.Output, aName.Name + (aName.Name.EndsWith(".exe") ? string.Empty : ".exe"));
            var tb = mb.DefineType("PythonMain", IKVM.Reflection.TypeAttributes.Public);

            if (!string.IsNullOrEmpty(config.Win32Icon)) {
                ab.__DefineIconResource(File.ReadAllBytes(config.Win32Icon));
            }

            MethodBuilder assemblyResolveMethod = null;
            ILGenerator gen = null;

            if (config.Standalone) {
                ConsoleOps.Info("Generating stand alone executable");
                config.Embed = true;

                foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies()) {
                    var n = new AssemblyName(a.FullName);
                    if (!a.IsDynamic && a.EntryPoint == null && (n.Name.StartsWith("IronPython") || n.Name == "Microsoft.Dynamic" || n.Name == "Microsoft.Scripting")) {
                        ConsoleOps.Info("\tEmbedded {0} {1}", n.Name, n.Version);
                        var f = new FileStream(a.Location, FileMode.Open, FileAccess.Read);
                        mb.DefineManifestResource("Dll." + n.Name, f, IKVM.Reflection.ResourceAttributes.Public);
                    }
                }

                // we currently do no error checking on what is passed in to the assemblyresolve event handler
                assemblyResolveMethod = tb.DefineMethod("AssemblyResolve", MethodAttributes.Public | MethodAttributes.Static, u.Import(typeof(System.Reflection.Assembly)), new IKVM.Reflection.Type[] { u.Import(typeof(System.Object)), u.Import(typeof(System.ResolveEventArgs)) });
                gen = assemblyResolveMethod.GetILGenerator();
                var s = gen.DeclareLocal(u.Import(typeof(System.IO.Stream))); // resource stream

                gen.Emit(OpCodes.Ldnull);
                gen.Emit(OpCodes.Stloc, s);
                var d = gen.DeclareLocal(u.Import(typeof(byte[]))); // data buffer;
                gen.EmitCall(OpCodes.Call, u.Import(typeof(System.Reflection.Assembly)).GetMethod("GetEntryAssembly"), Type.EmptyTypes);
                gen.Emit(OpCodes.Ldstr, "Dll.");
                gen.Emit(OpCodes.Ldarg_1);    // The event args
                gen.EmitCall(OpCodes.Callvirt, u.Import(typeof(System.ResolveEventArgs)).GetMethod("get_Name"), Type.EmptyTypes);
                gen.Emit(OpCodes.Newobj, u.Import(typeof(System.Reflection.AssemblyName)).GetConstructor(new IKVM.Reflection.Type[] { u.Import(typeof(string)) }));
                gen.EmitCall(OpCodes.Call, u.Import(typeof(System.Reflection.AssemblyName)).GetMethod("get_Name"), Type.EmptyTypes);
                gen.EmitCall(OpCodes.Call, u.Import(typeof(string)).GetMethod("Concat", new IKVM.Reflection.Type[] { u.Import(typeof(string)), u.Import(typeof(string)) }), Type.EmptyTypes);
                gen.EmitCall(OpCodes.Callvirt, u.Import(typeof(System.Reflection.Assembly)).GetMethod("GetManifestResourceStream", new IKVM.Reflection.Type[] { u.Import(typeof(string)) }), Type.EmptyTypes);
                gen.Emit(OpCodes.Stloc, s);
                gen.Emit(OpCodes.Ldloc, s);
                gen.EmitCall(OpCodes.Callvirt, u.Import(typeof(System.IO.Stream)).GetMethod("get_Length"), Type.EmptyTypes);
                gen.Emit(OpCodes.Newarr, u.Import(typeof(System.Byte)));
                gen.Emit(OpCodes.Stloc, d);
                gen.Emit(OpCodes.Ldloc, s);
                gen.Emit(OpCodes.Ldloc, d);
                gen.Emit(OpCodes.Ldc_I4_0);
                gen.Emit(OpCodes.Ldloc, s);
                gen.EmitCall(OpCodes.Callvirt, u.Import(typeof(System.IO.Stream)).GetMethod("get_Length"), Type.EmptyTypes);
                gen.Emit(OpCodes.Conv_I4);
                gen.EmitCall(OpCodes.Callvirt, u.Import(typeof(System.IO.Stream)).GetMethod("Read", new IKVM.Reflection.Type[] { u.Import(typeof(byte[])), u.Import(typeof(int)), u.Import(typeof(int)) }), Type.EmptyTypes);
                gen.Emit(OpCodes.Pop);
                gen.Emit(OpCodes.Ldloc, d);
                gen.EmitCall(OpCodes.Call, u.Import(typeof(System.Reflection.Assembly)).GetMethod("Load", new IKVM.Reflection.Type[] { u.Import(typeof(byte[])) }), Type.EmptyTypes);
                gen.Emit(OpCodes.Ret);

                // generate a static constructor to assign the AssemblyResolve handler (otherwise it tries to use IronPython before it adds the handler)
                // the other way of handling this would be to move the call to InitializeModule into a separate method.
                var staticConstructor = tb.DefineConstructor(MethodAttributes.Public | MethodAttributes.Static, CallingConventions.Standard, Type.EmptyTypes);
                gen = staticConstructor.GetILGenerator();
                gen.EmitCall(OpCodes.Call, u.Import(typeof(System.AppDomain)).GetMethod("get_CurrentDomain"), Type.EmptyTypes);
                gen.Emit(OpCodes.Ldnull);
                gen.Emit(OpCodes.Ldftn, assemblyResolveMethod);
                gen.Emit(OpCodes.Newobj, u.Import(typeof(System.ResolveEventHandler)).GetConstructor(new IKVM.Reflection.Type[] { u.Import(typeof(object)), u.Import(typeof(System.IntPtr)) }));
                gen.EmitCall(OpCodes.Callvirt, u.Import(typeof(System.AppDomain)).GetMethod("add_AssemblyResolve"), Type.EmptyTypes);
                gen.Emit(OpCodes.Ret);
            }

            var mainMethod = tb.DefineMethod("Main", MethodAttributes.Public | MethodAttributes.Static, u.Import(typeof(int)), Type.EmptyTypes);
            if (config.Target == PEFileKinds.WindowApplication && config.UseMta) {
                mainMethod.SetCustomAttribute(u.Import(typeof(System.MTAThreadAttribute)).GetConstructor(Type.EmptyTypes), new byte[0]);
            } else if (config.Target == PEFileKinds.WindowApplication) {
                mainMethod.SetCustomAttribute(u.Import(typeof(System.STAThreadAttribute)).GetConstructor(Type.EmptyTypes), new byte[0]);
            }

            gen = mainMethod.GetILGenerator();
            
            // variables for saving original working directory and return code of script
            var strVar = gen.DeclareLocal(u.Import(typeof(string)));
            var intVar = gen.DeclareLocal(u.Import(typeof(int)));
            LocalBuilder dictVar = null;

            if (config.PythonOptions.Count > 0) {
                var True = u.Import(typeof(ScriptingRuntimeHelpers)).GetField("True");
                var False = u.Import(typeof(ScriptingRuntimeHelpers)).GetField("False");

                dictVar = gen.DeclareLocal(u.Import(typeof(Dictionary<string, object>)));
                gen.Emit(OpCodes.Newobj, u.Import(typeof(Dictionary<string, object>)).GetConstructor(Type.EmptyTypes));
                gen.Emit(OpCodes.Stloc, dictVar);

                foreach (var option in config.PythonOptions) {
                    gen.Emit(OpCodes.Ldloc, dictVar);    
                    gen.Emit(OpCodes.Ldstr, option.Key);
                    if (option.Value is int) {
                        int val = (int)option.Value;
                        if (val >= -128 && val <= 127)
                            gen.Emit(OpCodes.Ldc_I4_S, val); // this is more optimized
                        else
                            gen.Emit(OpCodes.Ldc_I4, val);
                        gen.Emit(OpCodes.Box, u.Import(typeof(System.Int32)));
                    } else if (option.Value.Equals(ScriptingRuntimeHelpers.True)) {
                        gen.Emit(OpCodes.Ldsfld, True);
                    } else if(option.Value.Equals(ScriptingRuntimeHelpers.False)) {
                        gen.Emit(OpCodes.Ldsfld, False);
                    }
                    gen.EmitCall(OpCodes.Callvirt, u.Import(typeof(Dictionary<string, object>)).GetMethod("Add", new IKVM.Reflection.Type[] { u.Import(typeof(string)), u.Import(typeof(object)) }), Type.EmptyTypes);            
                }
            }
            
            Label tryStart = gen.BeginExceptionBlock();
            
            // get the ScriptCode assembly...
            if (config.Embed) {
                // put the generated DLL into the resources for the stub exe
                var mem = new MemoryStream();
                var rw = new ResourceWriter(mem);
                rw.AddResource("IPDll." + Path.GetFileNameWithoutExtension(config.Output) + ".dll", File.ReadAllBytes(config.Output + ".dll"));
                rw.Generate();
                mem.Position = 0;
                mb.DefineManifestResource("IPDll.resources", mem, ResourceAttributes.Public);
                File.Delete(config.Output + ".dll");

                // generate code to load the resource
                gen.Emit(OpCodes.Ldstr, "IPDll");
                gen.EmitCall(OpCodes.Call, u.Import(typeof(System.Reflection.Assembly)).GetMethod("GetEntryAssembly"), Type.EmptyTypes);
                gen.Emit(OpCodes.Newobj, u.Import(typeof(System.Resources.ResourceManager)).GetConstructor(new IKVM.Reflection.Type[] { u.Import(typeof(string)), u.Import(typeof(System.Reflection.Assembly)) }));
                gen.Emit(OpCodes.Ldstr, "IPDll." + Path.GetFileNameWithoutExtension(config.Output) + ".dll");
                gen.EmitCall(OpCodes.Call, u.Import(typeof(System.Resources.ResourceManager)).GetMethod("GetObject", new IKVM.Reflection.Type[] { u.Import(typeof(string)) }), Type.EmptyTypes);
                gen.EmitCall(OpCodes.Call, u.Import(typeof(System.Reflection.Assembly)).GetMethod("Load", new IKVM.Reflection.Type[] { u.Import(typeof(byte[])) }), Type.EmptyTypes);
            } else {
                // save current working directory
                gen.EmitCall(OpCodes.Call, u.Import(typeof(System.Environment)).GetMethod("get_CurrentDirectory"), Type.EmptyTypes);
                gen.Emit(OpCodes.Stloc, strVar);
                gen.EmitCall(OpCodes.Call, u.Import(typeof(System.Reflection.Assembly)).GetMethod("GetEntryAssembly"), Type.EmptyTypes);
                gen.EmitCall(OpCodes.Callvirt, u.Import(typeof(System.Reflection.Assembly)).GetMethod("get_Location"), Type.EmptyTypes);
                gen.Emit(OpCodes.Newobj, u.Import(typeof(System.IO.FileInfo)).GetConstructor(new IKVM.Reflection.Type[] { u.Import(typeof(string)) }));
                gen.EmitCall(OpCodes.Call, u.Import(typeof(System.IO.FileInfo)).GetMethod("get_Directory"), Type.EmptyTypes);
                gen.EmitCall(OpCodes.Call, u.Import(typeof(System.IO.DirectoryInfo)).GetMethod("get_FullName"), Type.EmptyTypes);
                gen.EmitCall(OpCodes.Call, u.Import(typeof(System.Environment)).GetMethod("set_CurrentDirectory"), Type.EmptyTypes);
                gen.Emit(OpCodes.Ldstr, config.Output + ".dll");
                gen.EmitCall(OpCodes.Call, u.Import(typeof(System.IO.Path)).GetMethod("GetFullPath", new IKVM.Reflection.Type[] { u.Import(typeof(string)) }), Type.EmptyTypes);
                // result of GetFullPath stays on the stack during the restore of the
                // original working directory

                // restore original working directory
                gen.Emit(OpCodes.Ldloc, strVar);
                gen.EmitCall(OpCodes.Call, u.Import(typeof(System.Environment)).GetMethod("set_CurrentDirectory"), Type.EmptyTypes);

                // for the LoadFile() call, the full path of the assembly is still is on the stack
                // as the result from the call to GetFullPath()
                gen.EmitCall(OpCodes.Call, u.Import(typeof(System.Reflection.Assembly)).GetMethod("LoadFile", new IKVM.Reflection.Type[] { u.Import(typeof(string)) }), Type.EmptyTypes);
            }

            // emit module name
            gen.Emit(OpCodes.Ldstr, "__main__");  // main module name
            gen.Emit(OpCodes.Ldnull);             // no references
            gen.Emit(OpCodes.Ldc_I4_0);           // don't ignore environment variables for engine startup
            if(config.PythonOptions.Count > 0) {
                gen.Emit(OpCodes.Ldloc, dictVar);
            } else {
                gen.Emit(OpCodes.Ldnull);
            }

            // call InitializeModuleEx
            // (this will also run the script)
            // and put the return code on the stack
            gen.EmitCall(OpCodes.Call, u.Import(typeof(PythonOps)).GetMethod("InitializeModuleEx", 
                new IKVM.Reflection.Type[] { u.Import(typeof(System.Reflection.Assembly)), u.Import(typeof(string)), u.Import(typeof(string[])), u.Import(typeof(bool)), u.Import(typeof(Dictionary<string, object>)) }), 
                Type.EmptyTypes);
            gen.Emit(OpCodes.Stloc, intVar);

            gen.BeginCatchBlock(u.Import(typeof(Exception)));

            if (config.Target == PEFileKinds.ConsoleApplication) {
                gen.EmitCall(OpCodes.Callvirt, u.Import(typeof(System.Exception)).GetMethod("get_Message", Type.EmptyTypes), Type.EmptyTypes);
                gen.Emit(OpCodes.Stloc, strVar);
                gen.Emit(OpCodes.Ldstr, config.ErrorMessageFormat);
                gen.Emit(OpCodes.Ldloc, strVar);
                gen.EmitCall(OpCodes.Call, u.Import(typeof(System.Console)).GetMethod("WriteLine", new IKVM.Reflection.Type[] { u.Import(typeof(string)), u.Import(typeof(string)) }), Type.EmptyTypes);                
            } else {
                gen.EmitCall(OpCodes.Callvirt, u.Import(typeof(System.Exception)).GetMethod("get_Message", Type.EmptyTypes), Type.EmptyTypes);
                gen.Emit(OpCodes.Stloc, strVar);
                gen.Emit(OpCodes.Ldstr, config.ErrorMessageFormat);
                gen.Emit(OpCodes.Ldloc, strVar);
                gen.EmitCall(OpCodes.Call, u.Import(typeof(string)).GetMethod("Format", new IKVM.Reflection.Type[] { u.Import(typeof(string)), u.Import(typeof(string)) }), Type.EmptyTypes);
                gen.Emit(OpCodes.Ldstr, "Error");
                gen.Emit(OpCodes.Ldc_I4, (int)System.Windows.Forms.MessageBoxButtons.OK);
                gen.Emit(OpCodes.Ldc_I4, (int)System.Windows.Forms.MessageBoxIcon.Error);
                gen.EmitCall(OpCodes.Call, u.Import(typeof(System.Windows.Forms.MessageBox)).GetMethod("Show", new IKVM.Reflection.Type[] { u.Import(typeof(string)), u.Import(typeof(string)), u.Import(typeof(System.Windows.Forms.MessageBoxButtons)), u.Import(typeof(System.Windows.Forms.MessageBoxIcon)) }), Type.EmptyTypes);
                gen.Emit(OpCodes.Pop);
            }

            gen.Emit(OpCodes.Ldc_I4, -1); // return code is -1 to show failure
            gen.Emit(OpCodes.Stloc, intVar);
            
            gen.EndExceptionBlock();

            gen.Emit(OpCodes.Ldloc, intVar);
            gen.Emit(OpCodes.Ret);

            tb.CreateType();
            ab.SetEntryPoint(mainMethod, config.Target);
            string fileName = aName.Name.EndsWith(".exe") ? aName.Name : aName.Name + ".exe";
            ab.Save(fileName, config.Platform, config.Machine);
        }

        static int Main(string[] args) {
            var files = new List<string>();
            var config = new Config();
            config.ParseArgs(args);
            if (!config.Validate()) {
                ConsoleOps.Usage(true);
            }

            // we don't use the engine, but we create it so we can have a default context.
            ScriptEngine engine = Python.CreateEngine(config.PythonOptions);

            ConsoleOps.Info("IronPython Compiler for {0} ({1})", engine.Setup.DisplayName, engine.LanguageVersion);
            ConsoleOps.Info("{0}", config);
            ConsoleOps.Info("compiling...");

            var compileOptions = new Dictionary<string, object>() {
                { "mainModule", config.MainName }
            };
            
            try
            {
                ClrModule.CompileModules(DefaultContext.DefaultCLS, 
                    Path.ChangeExtension(config.Output, ".dll"), 
                    compileOptions, 
                    config.Files.ToArray());
                
                var outputfilename = Path.ChangeExtension(config.Output, ".dll");
                if (config.Target != PEFileKinds.Dll) {
                    outputfilename = Path.ChangeExtension(config.Output, ".exe");                    
                    GenerateExe(config);
                }
                ConsoleOps.Info("Saved to {0}", outputfilename);
            }
            catch (Exception e)
            {
                Console.WriteLine();
                ConsoleOps.Error(true, e.Message);
            }

            ConsoleOps.Info("Saved to {0}", config.Output);
            return 0;
        }
    }
}
