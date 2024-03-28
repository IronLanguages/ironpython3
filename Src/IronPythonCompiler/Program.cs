// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;

using IronPython.Hosting;
using IronPython.Runtime;
using IronPython.Runtime.Operations;

using Microsoft.Scripting.Generation;
using Microsoft.Scripting.Hosting;
using Microsoft.Scripting.Runtime;

namespace IronPythonCompiler {

    public class Program {

        /// <summary>
        /// Generates the stub .exe file for starting the app
        /// </summary>
        /// <param name="config"></param>
        private static void GenerateExe(AssemblyGen assemblyGen, Config config) {
            var ab = assemblyGen.AssemblyBuilder;
            var mb = assemblyGen.ModuleBuilder;
            var tb = mb.DefineType("PythonMain", TypeAttributes.Public);

            if (!string.IsNullOrEmpty(config.Win32Icon)) {
                //ab.DefineIconResource(File.ReadAllBytes(config.Win32Icon));
            }

            MethodBuilder assemblyResolveMethod = null;
            ILGenerator gen = null;

            if (config.Standalone) {
                ConsoleOps.Info("Generating stand alone executable");
                config.Embed = true;

                var embedAssemblies = new HashSet<string> {
                    // DLR
                    "Microsoft.Dynamic",
                    "Microsoft.Scripting",
                    // System.Memory
                    "System.Buffers",
                    "System.Memory",
                    "System.Numerics.Vectors",
                    "System.Runtime.CompilerServices.Unsafe",
                };

                foreach (var a in AppDomain.CurrentDomain.GetAssemblies()) {
                    var n = new AssemblyName(a.FullName);
                    if (!a.IsDynamic && a.EntryPoint == null && (n.Name.StartsWith("IronPython", StringComparison.Ordinal) || embedAssemblies.Contains(n.Name))) {
                        ConsoleOps.Info($"\tEmbedded {n.Name} {n.Version}");
                        var f = new FileStream(a.Location, FileMode.Open, FileAccess.Read);
                        mb.DefineManifestResource("Dll." + n.Name, f, ResourceAttributes.Public);
                    }
                }

                foreach (var dll in config.DLLs) {
                    var name = Path.GetFileNameWithoutExtension(dll);
                    ConsoleOps.Info($"\tEmbedded {name}");
                    var f = new FileStream(dll, FileMode.Open, FileAccess.Read);
                    mb.DefineManifestResource("Dll." + name, f, ResourceAttributes.Public);
                }

                // we currently do no error checking on what is passed in to the assemblyresolve event handler
                assemblyResolveMethod = tb.DefineMethod("AssemblyResolve", MethodAttributes.Public | MethodAttributes.Static, typeof(System.Reflection.Assembly), new Type[] { typeof(System.Object), typeof(System.ResolveEventArgs) });
                gen = assemblyResolveMethod.GetILGenerator();
                var s = gen.DeclareLocal(typeof(Stream)); // resource stream

                gen.Emit(OpCodes.Ldnull);
                gen.Emit(OpCodes.Stloc, s);
                var d = gen.DeclareLocal(typeof(byte[])); // data buffer;
                gen.EmitCall(OpCodes.Call, typeof(Assembly).GetMethod("GetEntryAssembly"), Type.EmptyTypes);
                gen.Emit(OpCodes.Ldstr, "Dll.");
                gen.Emit(OpCodes.Ldarg_1);    // The event args
                gen.EmitCall(OpCodes.Callvirt, typeof(ResolveEventArgs).GetMethod("get_Name"), Type.EmptyTypes);
                gen.Emit(OpCodes.Newobj, typeof(AssemblyName).GetConstructor(new Type[] { typeof(string) }));
                gen.EmitCall(OpCodes.Call, typeof(AssemblyName).GetMethod("get_Name"), Type.EmptyTypes);
                gen.EmitCall(OpCodes.Call, typeof(string).GetMethod("Concat", new Type[] { typeof(string), typeof(string) }), Type.EmptyTypes);
                gen.EmitCall(OpCodes.Callvirt, typeof(Assembly).GetMethod("GetManifestResourceStream", new Type[] { typeof(string) }), Type.EmptyTypes);
                gen.Emit(OpCodes.Stloc, s);
                gen.Emit(OpCodes.Ldloc, s);
                gen.EmitCall(OpCodes.Callvirt, typeof(Stream).GetMethod("get_Length"), Type.EmptyTypes);
                gen.Emit(OpCodes.Newarr, typeof(byte));
                gen.Emit(OpCodes.Stloc, d);
                gen.Emit(OpCodes.Ldloc, s);
                gen.Emit(OpCodes.Ldloc, d);
                gen.Emit(OpCodes.Ldc_I4_0);
                gen.Emit(OpCodes.Ldloc, s);
                gen.EmitCall(OpCodes.Callvirt, typeof(Stream).GetMethod("get_Length"), Type.EmptyTypes);
                gen.Emit(OpCodes.Conv_I4);
                gen.EmitCall(OpCodes.Callvirt, typeof(Stream).GetMethod("Read", new Type[] { typeof(byte[]), typeof(int), typeof(int) }), Type.EmptyTypes);
                gen.Emit(OpCodes.Pop);
                gen.Emit(OpCodes.Ldloc, d);
                gen.EmitCall(OpCodes.Call, typeof(Assembly).GetMethod("Load", new Type[] { typeof(byte[]) }), Type.EmptyTypes);
                gen.Emit(OpCodes.Ret);

                // generate a static constructor to assign the AssemblyResolve handler (otherwise it tries to use IronPython before it adds the handler)
                // the other way of handling this would be to move the call to InitializeModule into a separate method.
                var staticConstructor = tb.DefineConstructor(MethodAttributes.Public | MethodAttributes.Static, CallingConventions.Standard, Type.EmptyTypes);
                gen = staticConstructor.GetILGenerator();
                gen.EmitCall(OpCodes.Call, typeof(AppDomain).GetMethod("get_CurrentDomain"), Type.EmptyTypes);
                gen.Emit(OpCodes.Ldnull);
                gen.Emit(OpCodes.Ldftn, assemblyResolveMethod);
                gen.Emit(OpCodes.Newobj, typeof(ResolveEventHandler).GetConstructor(new Type[] { typeof(object), typeof(IntPtr) }));
                gen.EmitCall(OpCodes.Callvirt, typeof(AppDomain).GetMethod("add_AssemblyResolve"), Type.EmptyTypes);
                gen.Emit(OpCodes.Ret);
            }

            var mainMethod = tb.DefineMethod("Main", MethodAttributes.Public | MethodAttributes.Static, typeof(int), Type.EmptyTypes);
            if (config.Target == PEFileKinds.WindowApplication && config.UseMta) {
                mainMethod.SetCustomAttribute(typeof(MTAThreadAttribute).GetConstructor(Type.EmptyTypes), Array.Empty<byte>());
            } else if (config.Target == PEFileKinds.WindowApplication || config.Target == PEFileKinds.ConsoleApplication && config.UseSta) {
                mainMethod.SetCustomAttribute(typeof(STAThreadAttribute).GetConstructor(Type.EmptyTypes), Array.Empty<byte>());
            }

            gen = mainMethod.GetILGenerator();

            // variables for saving original working directory and return code of script
            var strVar = gen.DeclareLocal(typeof(string));
            var intVar = gen.DeclareLocal(typeof(int));
            LocalBuilder dictVar = null;

            if (config.PythonOptions.Count > 0) {
                var True = typeof(ScriptingRuntimeHelpers).GetField("True");
                var False = typeof(ScriptingRuntimeHelpers).GetField("False");

                dictVar = gen.DeclareLocal(typeof(Dictionary<string, object>));
                gen.Emit(OpCodes.Newobj, typeof(Dictionary<string, object>).GetConstructor(Type.EmptyTypes));
                gen.Emit(OpCodes.Stloc, dictVar);

                foreach (var option in config.PythonOptions) {
                    gen.Emit(OpCodes.Ldloc, dictVar);
                    gen.Emit(OpCodes.Ldstr, option.Key);
                    if (option.Value is int val) {
                        if (val >= -128 && val <= 127)
                            gen.Emit(OpCodes.Ldc_I4_S, val); // this is more optimized
                        else
                            gen.Emit(OpCodes.Ldc_I4, val);
                        gen.Emit(OpCodes.Box, typeof(int));
                    } else if (option.Value.Equals(ScriptingRuntimeHelpers.True)) {
                        gen.Emit(OpCodes.Ldsfld, True);
                    } else if (option.Value.Equals(ScriptingRuntimeHelpers.False)) {
                        gen.Emit(OpCodes.Ldsfld, False);
                    }
                    gen.EmitCall(OpCodes.Callvirt, typeof(Dictionary<string, object>).GetMethod("Add", new Type[] { typeof(string), typeof(object) }), Type.EmptyTypes);
                }
            }

            Label tryStart = gen.BeginExceptionBlock();

            // get the ScriptCode assembly...
            gen.EmitCall(OpCodes.Call, typeof(Assembly).GetMethod(nameof(Assembly.GetExecutingAssembly), Type.EmptyTypes), Type.EmptyTypes);

            // emit module name
            gen.Emit(OpCodes.Ldstr, "__main__");  // main module name
            gen.Emit(OpCodes.Ldnull);             // no references
            gen.Emit(OpCodes.Ldc_I4_0);           // don't ignore environment variables for engine startup
            if (config.PythonOptions.Count > 0) {
                gen.Emit(OpCodes.Ldloc, dictVar);
            } else {
                gen.Emit(OpCodes.Ldnull);
            }

            // call InitializeModuleEx
            // (this will also run the script)
            // and put the return code on the stack
            gen.EmitCall(OpCodes.Call, typeof(PythonOps).GetMethod(nameof(PythonOps.InitializeModuleEx),
                new Type[] { typeof(Assembly), typeof(string), typeof(string[]), typeof(bool), typeof(Dictionary<string, object>) }),
                Type.EmptyTypes);
            gen.Emit(OpCodes.Stloc, intVar);
            gen.BeginCatchBlock(typeof(Exception));

            if (config.Target == PEFileKinds.ConsoleApplication) {
                gen.EmitCall(OpCodes.Callvirt, typeof(Exception).GetMethod("get_Message", Type.EmptyTypes), Type.EmptyTypes);
                gen.Emit(OpCodes.Stloc, strVar);
                gen.Emit(OpCodes.Ldstr, config.ErrorMessageFormat);
                gen.Emit(OpCodes.Ldloc, strVar);
                gen.EmitCall(OpCodes.Call, typeof(Console).GetMethod("WriteLine", new Type[] { typeof(string), typeof(string) }), Type.EmptyTypes);
            } else {
                gen.EmitCall(OpCodes.Callvirt, typeof(Exception).GetMethod("get_Message", Type.EmptyTypes), Type.EmptyTypes);
                gen.Emit(OpCodes.Stloc, strVar);
                gen.Emit(OpCodes.Ldstr, config.ErrorMessageFormat);
                gen.Emit(OpCodes.Ldloc, strVar);
                gen.EmitCall(OpCodes.Call, typeof(string).GetMethod("Format", new Type[] { typeof(string), typeof(string) }), Type.EmptyTypes);
                gen.Emit(OpCodes.Ldstr, "Error");
                gen.Emit(OpCodes.Ldc_I4, (int)System.Windows.Forms.MessageBoxButtons.OK);
                gen.Emit(OpCodes.Ldc_I4, (int)System.Windows.Forms.MessageBoxIcon.Error);
                gen.EmitCall(OpCodes.Call, typeof(System.Windows.Forms.MessageBox).GetMethod("Show", new Type[] { typeof(string), typeof(string), typeof(System.Windows.Forms.MessageBoxButtons), typeof(System.Windows.Forms.MessageBoxIcon) }), Type.EmptyTypes);
                gen.Emit(OpCodes.Pop);
            }

            gen.Emit(OpCodes.Ldc_I4, -1); // return code is -1 to show failure
            gen.Emit(OpCodes.Stloc, intVar);

            gen.EndExceptionBlock();

            gen.Emit(OpCodes.Ldloc, intVar);
            gen.Emit(OpCodes.Ret);

            tb.CreateType();
            ab.SetEntryPoint(mainMethod, config.Target);
        }

        public static int Main(string[] args) {
            var config = new Config();
            config.ParseArgs(args);
            if (!config.Validate()) {
                ConsoleOps.Usage(true);
            }
            ConsoleOps.NoLogo = config.NoLogo;

            // we don't use the engine, but we create it so we can have a default context.
            ScriptEngine engine = Python.CreateEngine(config.PythonOptions);

            ConsoleOps.Info($"IronPython Compiler for {engine.Setup.DisplayName} ({engine.LanguageVersion})");
            ConsoleOps.Info($"{config}");
            ConsoleOps.Info("compiling...");

            var compileOptions = new Dictionary<string, object>() {
                { "mainModule", config.MainName },
                { "assemblyFileVersion", config.FileVersion },
                { "copyright", config.Copyright },
                { "productName", config.ProductName },
                { "productVersion", config.ProductVersion },
            };

            try {
                string outputfilename = Path.Combine(config.OutputPath, config.Output);

                if (!Path.HasExtension(outputfilename)) {
                    outputfilename = config.Target == PEFileKinds.Dll
                        ? Path.ChangeExtension(outputfilename, ".dll")
                        : Path.ChangeExtension(outputfilename, ".exe");
                }

                var ag = ClrModule.CreateAssemblyGen(DefaultContext.DefaultCLS,
                    outputfilename,
                    compileOptions,
                    config.Files.ToArray());

                if (config.Target != PEFileKinds.Dll) {
                    GenerateExe(ag, config);
                }

                ag.AssemblyBuilder.Save(Path.GetFileName(outputfilename), config.Platform, config.Machine);

                ConsoleOps.Info($"Saved to {outputfilename}");
            } catch (Exception e) {
                Console.WriteLine();
                ConsoleOps.Error(true, e.Message);
            }
            return 0;
        }
    }
}
