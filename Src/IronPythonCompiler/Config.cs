using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IKVM.Reflection;
using IKVM.Reflection.Emit;
using System.Resources;
using System.Reflection;


namespace IronPythonCompiler {
    public class Config {

        public Config() {
            Embed = false;
            Files = new List<string>();
            Platform = IKVM.Reflection.PortableExecutableKinds.ILOnly;
            Machine = IKVM.Reflection.ImageFileMachine.I386;
            Standalone = false;
            Target = PEFileKinds.Dll;
            UseMta = false;
            MainName = Main = string.Empty;
            Output = string.Empty;
            Win32Icon = string.Empty;
            Version = string.Empty;
            ErrorMessageFormat = "Error occured: {0}";
        }

        public string ErrorMessageFormat {
            get;
            private set;
        }

        public string Version {
            get;
            private set;
        }

        public string Win32Icon {
            get;
            private set;
        }

        public string Output {
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
            foreach (var a in args) {
                var arg = a.Trim();
                if (arg.StartsWith("#")) {
                    continue;
                }

                if (arg.StartsWith("/main:")) {
                    MainName = Main = arg.Substring(6).Trim('"');
                    // only override the target kind if its currently a DLL
                    if (Target == PEFileKinds.Dll) {
                        Target = PEFileKinds.ConsoleApplication;
                    }
                } else if (arg.StartsWith("/out:")) {
                    Output = arg.Substring(5).Trim('"');
                } else if (arg.StartsWith("/target:")) {
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
                } else if (arg.StartsWith("/platform:")) {
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
                            Machine = IKVM.Reflection.ImageFileMachine.I386;
                            break;
                    }
                } else if (arg.StartsWith("/win32icon:")) {
                    Win32Icon = arg.Substring(11).Trim('"'); 
                } else if (arg.StartsWith("/version:")) {
                    Version = arg.Substring(9).Trim('"');
                } else if(arg.StartsWith("/errfmt:")) {
                    ErrorMessageFormat = arg.Substring(8);
                } else if (arg.StartsWith("/embed")) {
                    Embed = true;
                } else if (arg.StartsWith("/standalone")) {
                    Standalone = true;
                } else if (arg.StartsWith("/mta")) {
                    UseMta = true;
                } else if (Array.IndexOf(new string[] { "/?", "-?", "/h", "-h" }, args) >= 0) {
                    ConsoleOps.Usage(true);
                } else {
                    if (arg.StartsWith("@")) {
                        var respFile = Path.GetFullPath(arg.Substring(1));
                        if (respFiles == null) {
                            respFiles = new List<string>();
                        }

                        if (!respFiles.Contains(respFile)) {
                            respFiles.Add(respFile);
                            ParseArgs(File.ReadAllLines(respFile), respFiles);
                        } else {
                            ConsoleOps.Warning("Already parsed response file '{0}'", arg.Substring(1));
                        }
                    } else {
                        Files.Add(arg);
                    }
                }
            }
        }

        public bool Validate() {
            if (Files.Count == 1 && string.IsNullOrEmpty(MainName)) {
                MainName = Files[0];
            }

            if (Files == null || Files.Count == 0 || string.IsNullOrEmpty(MainName)) {
                ConsoleOps.Error("No files or main defined");
                return false;
            }

            if (Target != PEFileKinds.Dll && string.IsNullOrEmpty(MainName)) {
                ConsoleOps.Error("EXEs require /main:<filename> to be specified");
                return false;
            }

            if (string.IsNullOrEmpty(Output) && !string.IsNullOrEmpty(MainName)) {
                Output = Path.GetFileNameWithoutExtension(MainName);
            } else if (string.IsNullOrEmpty(Output) && Files != null && Files.Count > 0) {
                Output = Path.GetFileNameWithoutExtension(Files[0]);
            }

            if (!string.IsNullOrEmpty(Win32Icon) && Target == PEFileKinds.Dll) {
                ConsoleOps.Error("DLLs may not have a win32icon");
                return false;
            } else if (!string.IsNullOrEmpty(Win32Icon) && !File.Exists(Win32Icon)) {
                ConsoleOps.Error("win32icon '{0}' does not exist", Win32Icon);
                return false;
            }

            return true;
        }

        public override string ToString() {
            StringBuilder res = new StringBuilder("Input Files:\n");
            foreach (var file in Files) {
                res.AppendFormat("\t{0}\n", file);
            }

            res.AppendFormat("Output:\n\t{0}\n", Output);
            res.AppendFormat("Target:\n\t{0}\n", Target);
            res.AppendFormat("Platform:\n\t{0}\n", Machine);
            if (Target == PEFileKinds.WindowApplication) {
                res.AppendLine("Threading:");
                if (UseMta) {
                    res.AppendLine("\tMTA");
                } else {
                    res.AppendLine("\tSTA");
                }
            }

            return res.ToString();
        }
    }

}
