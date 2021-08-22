using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using IKVM.Reflection;
using IKVM.Reflection.Emit;
using System.Resources;
using System.Reflection;
using Microsoft.Scripting.Runtime;

namespace IronPythonCompiler {
    public class Config {

        public Config() {
            Embed = false;
            Files = new List<string>();
            Platform = IKVM.Reflection.PortableExecutableKinds.ILOnly;
            Machine = IKVM.Reflection.ImageFileMachine.AMD64;
            Standalone = false;
            Target = PEFileKinds.Dll;
            UseMta = false;
            MainName = Main = string.Empty;
            Output = string.Empty;
            OutputPath = string.Empty;
            Win32Icon = string.Empty;
            FileVersion = string.Empty;
            ProductName = string.Empty;
            Copyright = string.Empty;
            ProductVersion = string.Empty;
            ErrorMessageFormat = "Error occurred: {0}";
            PythonOptions = new Dictionary<string, object>();
            DLLs = new List<string>();
        }

        public string ErrorMessageFormat {
            get;
            private set;
        }

        public string FileVersion {
            get;
            private set;
        }

        public string Win32Icon {
            get;
            private set;
        }

        public string ProductName {
            get;
            private set;
        }

        public string Copyright {
            get;
            private set;
        }

        public string ProductVersion {
            get;
            private set;
        }

        public string Output {
            get;
            private set;
        }

        public string OutputPath {
            get;
            private set;
        }

        public string Main {
            get;
            private set;
        }

        public string MainName {
            get;
            private set;
        }

        public PEFileKinds Target {
            get;
            private set;
        }

        public bool UseMta {
            get;
            private set;
        }

        public bool Standalone {
            get;
            private set;
        }

        public List<string> Files {
            get;
            private set;
        }

        public List<string> DLLs {
            get;
            private set;
        }

        public IDictionary<string, object> PythonOptions {
            get;
            private set;
        }

        public bool Embed {
            get;
            internal set;
        }

        public IKVM.Reflection.ImageFileMachine Machine {
            get;
            private set;
        }

        public IKVM.Reflection.PortableExecutableKinds Platform {
            get;
            private set;
        }

        public void ParseArgs(IEnumerable<string> args, List<string> respFiles = null) {
            var helpStrings = new string[] { "/?", "-?", "/h", "-h" };

            foreach (var a in args) {
                var arg = a.Trim();
                if (arg.StartsWith("#", StringComparison.Ordinal)) {
                    continue;
                }

                if (arg.StartsWith("/main:", StringComparison.Ordinal)) {
                    MainName = Main = arg.Substring(6).Trim('"');
                    // only override the target kind if its currently a DLL
                    if (Target == PEFileKinds.Dll) {
                        Target = PEFileKinds.ConsoleApplication;
                    }
                } else if (arg.StartsWith("/out:", StringComparison.Ordinal)) {
                    Output = arg.Substring(5).Trim('"');
                } else if (arg.StartsWith("/target:", StringComparison.Ordinal)) {
                    string tgt = arg.Substring(8).Trim('"');
                    switch (tgt) {
                        case "exe":
                            Target = PEFileKinds.ConsoleApplication;
                            break;
                        case "winexe":
                            Target = PEFileKinds.WindowApplication;
                            break;
                        default:
                            Target = PEFileKinds.Dll;
                            break;
                    }
                } else if (arg.StartsWith("/platform:", StringComparison.Ordinal)) {
                    string plat = arg.Substring(10).Trim('"');
                    switch (plat) {
                        case "x86":
                            Platform = IKVM.Reflection.PortableExecutableKinds.ILOnly | IKVM.Reflection.PortableExecutableKinds.Required32Bit;
                            Machine = IKVM.Reflection.ImageFileMachine.I386;
                            break;
                        case "x64":
                            Platform = IKVM.Reflection.PortableExecutableKinds.ILOnly | IKVM.Reflection.PortableExecutableKinds.PE32Plus;
                            Machine = IKVM.Reflection.ImageFileMachine.AMD64;
                            break;
                        default:
                            Platform = IKVM.Reflection.PortableExecutableKinds.ILOnly;
                            Machine = IKVM.Reflection.ImageFileMachine.AMD64;
                            break;
                    }
                } else if (arg.StartsWith("/win32icon:", StringComparison.Ordinal)) {
                    Win32Icon = arg.Substring(11).Trim('"');
                } else if (arg.StartsWith("/fileversion:", StringComparison.Ordinal)) {
                    FileVersion = arg.Substring(13).Trim('"');
                } else if (arg.StartsWith("/productversion:", StringComparison.Ordinal)) {
                    ProductVersion = arg.Substring(16).Trim('"');
                } else if (arg.StartsWith("/productname:", StringComparison.Ordinal)) {
                    ProductName = arg.Substring(13).Trim('"');
                } else if (arg.StartsWith("/copyright:", StringComparison.Ordinal)) {
                    Copyright = arg.Substring(11).Trim('"');
                } else if (arg.StartsWith("/errfmt:", StringComparison.Ordinal)) {
                    ErrorMessageFormat = arg.Substring(8);
                } else if (arg.StartsWith("/embed", StringComparison.Ordinal)) {
                    Embed = true;
                } else if (arg.StartsWith("/standalone", StringComparison.Ordinal)) {
                    Standalone = true;
                } else if (arg.StartsWith("/mta", StringComparison.Ordinal)) {
                    UseMta = true;
                } else if (arg.StartsWith("/recurse:", StringComparison.Ordinal)) {
                    string pattern = arg.Substring(9);
                    if (string.IsNullOrWhiteSpace(pattern)) {
                        ConsoleOps.Error(true, "Missing pattern for /recurse option");
                    }
                    foreach (var f in Directory.EnumerateFiles(Environment.CurrentDirectory, pattern)) {
                        Files.Add(Path.GetFullPath(f));
                    }
                } else if (Array.IndexOf(helpStrings, arg) >= 0) {
                    ConsoleOps.Usage(true);
                } else if (arg.StartsWith("/py:", StringComparison.Ordinal)) {
                    // if you add a parameter that takes a different type then 
                    // ScriptingRuntimeHelpers.True/False or int
                    // you need ot also modify Program.cs for standalone generation.
                    string[] pyargs = arg.Substring(4).Trim('"').Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    switch (pyargs[0]) {
                        case "-X:Frames":
                            PythonOptions["Frames"] = ScriptingRuntimeHelpers.True;
                            break;
                        case "-X:FullFrames":
                            PythonOptions["Frames"] = PythonOptions["FullFrames"] = ScriptingRuntimeHelpers.True;
                            break;
                        case "-X:Tracing":
                            PythonOptions["Tracing"] = ScriptingRuntimeHelpers.True;
                            break;
                        case "-X:GCStress":
                            int gcStress;
                            if (!int.TryParse(pyargs[1], out gcStress) || (gcStress < 0 || gcStress > GC.MaxGeneration)) {
                                ConsoleOps.Error(true, $"The argument for the {pyargs[1]} option must be between 0 and {GC.MaxGeneration}.");
                            }

                            PythonOptions["GCStress"] = gcStress;
                            break;

                        case "-X:MaxRecursion":
                            // we need about 6 frames for starting up, so 10 is a nice round number.
                            int limit;
                            if (!int.TryParse(pyargs[1], out limit) || limit < 10) {
                                ConsoleOps.Error(true, $"The argument for the {pyargs[1]} option must be an integer >= 10.");
                            }

                            PythonOptions["RecursionLimit"] = limit;
                            break;

                        case "-X:EnableProfiler":
                            PythonOptions["EnableProfiler"] = ScriptingRuntimeHelpers.True;
                            break;

                        case "-X:LightweightScopes":
                            PythonOptions["LightweightScopes"] = ScriptingRuntimeHelpers.True;
                            break;

                        case "-X:Debug":
                            PythonOptions["Debug"] = ScriptingRuntimeHelpers.True;
                            break;
                    }
                } else {
                    if (arg.StartsWith("@", StringComparison.Ordinal)) {
                        var respFile = Path.GetFullPath(arg.Substring(1));
                        if (respFiles == null) {
                            respFiles = new List<string>();
                        }

                        if (!respFiles.Contains(respFile)) {
                            respFiles.Add(respFile);
                            ParseArgs(File.ReadAllLines(respFile), respFiles);
                        } else {
                            ConsoleOps.Warning($"Already parsed response file '{arg.Substring(1)}'");
                        }
                    } else {
                        if (arg.ToLower().EndsWith(".dll", StringComparison.Ordinal)) {
                            DLLs.Add(arg);
                        } else {
                            Files.Add(arg);
                        }
                    }
                }
            }
        }

        public bool Validate() {
            if (Files.Count == 1 && string.IsNullOrWhiteSpace(MainName)) {
                MainName = Files[0];
            }

            if (Files.Count == 0 && !string.IsNullOrWhiteSpace(MainName)) {
                Files.Add(MainName);
            }

            if (Files == null || Files.Count == 0 || string.IsNullOrEmpty(MainName)) {
                ConsoleOps.Error("No files or main defined");
                return false;
            }

            if (Target != PEFileKinds.Dll && string.IsNullOrEmpty(MainName)) {
                ConsoleOps.Error("EXEs require /main:<filename> to be specified");
                return false;
            }

            if (DLLs.Count > 0 && !Standalone) {
                ConsoleOps.Error("DLLs can only be used in standalone mode");
                return false;
            }

            if (string.IsNullOrWhiteSpace(Output) && !string.IsNullOrWhiteSpace(MainName)) {
                Output = Path.GetFileNameWithoutExtension(MainName);
                OutputPath = Path.GetDirectoryName(MainName);
            } else if (string.IsNullOrWhiteSpace(Output) && Files != null && Files.Count > 0) {
                Output = Path.GetFileNameWithoutExtension(Files[0]);
                OutputPath = Path.GetDirectoryName(Files[0]);
            }

            if (!string.IsNullOrWhiteSpace(Win32Icon) && Target == PEFileKinds.Dll) {
                ConsoleOps.Error("DLLs may not have a win32icon");
                return false;
            } else if (!string.IsNullOrWhiteSpace(Win32Icon) && !File.Exists(Win32Icon)) {
                ConsoleOps.Error($"win32icon '{Win32Icon}' does not exist");
                return false;
            }

            return true;
        }

        public override string ToString() {
            StringBuilder res = new StringBuilder("Input Files:\n");
            foreach (var file in Files) {
                res.AppendLine($"\t{file}");
            }

            res.AppendLine($"Output:\n\t{Output}");
            res.AppendLine($"OutputPath:\n\t{OutputPath}");
            res.AppendLine($"Target:\n\t{Target}");
            res.AppendLine($"Platform:\n\t{Machine}");
            if (Target == PEFileKinds.WindowApplication) {
                res.AppendLine("Threading:");
                if (UseMta) {
                    res.AppendLine("\tMTA");
                } else {
                    res.AppendLine("\tSTA");
                }
            }

            if (PythonOptions.Count > 0) {
                res.AppendLine("\nIronPython Context Options:");
                foreach (var option in PythonOptions) {
                    res.AppendLine($"\t{option.Key} = {option.Value}");
                }
            }

            return res.ToString();
        }
    }

}
