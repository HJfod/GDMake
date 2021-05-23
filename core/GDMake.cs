using System;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using Microsoft.Win32;
using utils;

namespace gdmake {
    public static class GDMake {
        public class Submodule {
            public string Name;
            public string URL;
            public string IncludeHeader;
            public bool CompileLib;
            public string CMakeDefs;

            public Submodule(string name, string url, bool lib = true, string defs = "") {
                this.Name = name;
                this.URL = url;
                this.IncludeHeader = Name;
                this.CMakeDefs = defs;
                this.CompileLib = lib;
            }
        };

        public const string DotfileName = ".gdmake";
        public static string ExePath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
        public static Submodule[] DefaultSubmodules = new Submodule[] {
            new Submodule ( "gd.h", "https://github.com/HJfod/gd.h", false ),
            new Submodule ( "MinHook", "https://github.com/TsudaKageyu/MinHook", true, "-DBUILD_SHARED_LIBS=ON" ),
            new Submodule ( "Cocos2d", "https://github.com/HJfod/cocos-headers", false ),
        };
        public static List<Submodule> Submodules = new List<Submodule>(DefaultSubmodules);

        public static bool IsGlobalInitialized() {
            if (!Directory.Exists(Path.Join(ExePath, "submodules")))
                return false;

            if (!Directory.Exists(Path.Join(ExePath, "include")))
                return false;

            if (!Directory.Exists(Path.Join(ExePath, "src")))
                return false;

            if (!Directory.Exists(Path.Join(ExePath, "tools")))
                return false;

            return true;
        }

        public static void ShowGlobalNotInitializedError() {
            Console.WriteLine("Error: GDMake has not been initialized! Use \"gdmake setup\" to initialize.");
        }
    
        private static void GenerateIncludeFiles() {
            var includeText =
                DefaultStrings.HeaderCredit + "\n" +
                "#ifndef __INCLUDE_GDMAKE_H__\n" +
                "#define __INCLUDE_GDMAKE_H__\n\n" +
                "#pragma warning(push, 0)\n";

            foreach (var sub in Submodules)
                includeText += $"#include <{(sub.IncludeHeader.EndsWith(".h") ? sub.IncludeHeader : sub.IncludeHeader + ".h")}>\n";

            includeText += "#pragma warning(pop)\n\n";
            includeText += "#include \"GDMakeMacros.h\"\n\n";
            includeText += DefaultStrings.GDMakeModNS + "\n";
            includeText += "#endif";

            var includeMacros =
                DefaultStrings.HeaderCredit + "\n" +
                "#ifndef __GDMAKE_MACROS_H__\n" +
                "#define __GDMAKE_MACROS_H__\n\n";
            
            foreach (var macro in Preprocessor.Macros)
                includeMacros += $"/**\n * Semantic information for GDMake.\n{macro.GetFormattedDesc()}\n */\n" + 
                    $"#define {macro.Text}" +
                    $"{(macro.Parameters != null ? "(" + String.Join(",", macro.Parameters) + ")" : "")}" + 
                    $" {macro.CppReplace}\n\n";
            
            includeMacros += "#endif";

            File.WriteAllText(Path.Join(ExePath, "include/GDMakeMacros.h"), includeMacros);
            File.WriteAllText(Path.Join(ExePath, "include/GDMake.h"), includeText);
        }

        private static void GenerateSourceFiles() {
            File.WriteAllText(Path.Join(ExePath, "src/console.h"), DefaultStrings.ConsoleHeader);
            File.WriteAllText(Path.Join(ExePath, "src/console.cpp"), DefaultStrings.ConsoleSource);
        }

        private static void GenerateTools() {
            try { Directory.CreateDirectory(Path.Join(ExePath, "tools", "Inject32")); }
            catch (Exception) { return; }

            File.WriteAllText(Path.Join(ExePath, "tools", "Inject32", "CMakeLists.txt"), DefaultStrings.InjectorCmake);
            File.WriteAllText(Path.Join(ExePath, "tools", "Inject32", "injector.cpp"), DefaultStrings.InjectorCpp);
        }

        public static void RunBuildBat(
            string cd,
            string pName,
            string config,
            string cMakeOpts = null,
            bool silent = false,
            string verb = "quiet"
        ) {
            var process = new Process();

            process.StartInfo.Arguments = $"\"{cd}\" {pName} {config} {verb} {(cMakeOpts != null ? $"\"{cMakeOpts}\"" : "")}";
            process.StartInfo.FileName = Path.Join(ExePath, "build.bat");
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.OutputDataReceived += (sender, args) => { if (!silent) Console.WriteLine(args.Data); };

            process.Start();
            process.BeginOutputReadLine();
            process.WaitForExit();
        }

        public static Result<string> GetGDPath() {
            var regv = Registry.LocalMachine.OpenSubKey(@"Software\WOW6432Node\Valve\Steam", false)?.GetValue("InstallPath");
        
            if (regv == null)
                return new ErrorResult<string>("Unable to find GeometryDash.exe! (No RegKey)");
            else {
                var test = regv + "\\steamapps\\common\\Geometry Dash\\GeometryDash.exe";

                if (File.Exists(test))
                    return new SuccessResult<string>(test);
                else {
                    foreach (var line in File.ReadAllLines(regv + "\\config\\config.vdf"))
                        if (line.Contains("BaseInstallFolder_")) {
                            var val = line.Substring(0, line.LastIndexOf('\"'));
                            val = val.Substring(val.LastIndexOf('\"') + 1);

                            var path = val + "\\steamapps\\common\\Geometry Dash\\GeometryDash.exe";

                            if (File.Exists(path))
                                return new SuccessResult<string>(path);
                        }

                    return new ErrorResult<string>("Unable to find GeometryDash.exe! (No path found)");
                }
            }
        }

        public static bool GDIsRunning() {
            return Process.GetProcessesByName("GeometryDash").Length > 0;
        }

        public static Result MoveSharedDLLs() {
            var res = GetGDPath();

            if (res.Failure)
                return new ErrorResult((res as ErrorResult<string>).Message);
            else {
                var path = Path.GetDirectoryName(res.Data);

                foreach (var dll in Directory.GetFiles(
                    Path.Join(ExePath, "dlls")
                ))
                    try { File.Copy(dll, Path.Join(path, Path.GetFileName(dll)), true); }
                    catch (Exception) {}
                
                return new SuccessResult();
            }
        }

        public static Result InjectDLL(string name) {
            var process = new Process();

            string error = null;

            process.StartInfo.Arguments = $"GeometryDash.exe {name}";
            process.StartInfo.FileName = Path.Join(ExePath, "tools", "bin", "Inject32.exe");
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.OutputDataReceived += (sender, args) => error = args.Data;

            process.Start();
            process.BeginOutputReadLine();
            process.WaitForExit();

            if (error != null)
                return new ErrorResult(error);
            
            return new SuccessResult();
        }

        public static bool IsDirectoryEmpty(string path) {
            return !Directory.EnumerateFileSystemEntries(path).Any();
        }

        public static void CompileLibs() {
            if (!Directory.Exists(Path.Join(ExePath, "libs")) || IsDirectoryEmpty(Path.Join(ExePath, "libs"))) {
                Console.WriteLine("Building libraries...");

                Directory.CreateDirectory(Path.Join(ExePath, "libs"));
                Directory.CreateDirectory(Path.Join(ExePath, "dlls"));

                foreach (var sub in Submodules)
                    if (sub.CompileLib) {
                        RunBuildBat(Path.Join(ExePath, "submodules", sub.Name), sub.Name, "Release", sub.CMakeDefs, true);

                        foreach (var file in Directory.GetFiles(
                            Path.Join(ExePath, "submodules", sub.Name, "build", "Release")
                        ))
                            if (file.EndsWith(".dll"))
                                File.Copy(file, Path.Join(ExePath, "dlls", Path.GetFileName(file)), true);
                            else if (file.EndsWith(".lib"))
                                File.Copy(file, Path.Join(ExePath, "libs", Path.GetFileName(file)), true);
                    }
            }

            if (!Directory.Exists(Path.Join(ExePath, "tools", "bin")) || IsDirectoryEmpty(Path.Join(ExePath, "tools", "bin"))) {
                Console.WriteLine("Building GDMake tools...");

                Directory.CreateDirectory(Path.Join(ExePath, "tools", "bin"));

                foreach (var dir in Directory.GetDirectories(Path.Join(ExePath, "tools")))
                    if (Path.GetFileName(dir) != "bin") {
                        var path = Path.Join(ExePath, "tools", Path.GetFileName(dir));

                        RunBuildBat(path, Path.GetFileName(dir), "Release", null, true);

                        var exe = $"{Path.GetFileName(dir)}.exe";

                        File.Copy(
                            Path.Join(path, "build", "Release", exe),
                            Path.Join(ExePath, "tools", "bin", exe),
                            true
                        );
                    }
            }
        }

        public static Result InitializeGlobal() {
            foreach (var dir in new string[] {
                "submodules",
                "include",
                "src",
                "tools",
            })
                try { Directory.CreateDirectory(Path.Join(ExePath, dir)); }
                catch (Exception e) {
                    return new ErrorResult($"Unable to create directory: {e.Message}");
                }

            foreach (var sub in DefaultSubmodules)
                try {
                    Console.WriteLine($"Installing {sub.Name}...");

                    var path = Path.Join(ExePath, "submodules", sub.Name);

                    if (Directory.Exists(path))
                        continue;

                    LibGit2Sharp.Repository.Clone(sub.URL, path);
                } catch (Exception e) {
                    return new ErrorResult($"Unable to install submodule {sub.Name}: {e.Message}");
                }

            Console.WriteLine("Generating files...");

            File.WriteAllText(Path.Join(ExePath, "build.bat"), DefaultStrings.BuildBat);

            GenerateIncludeFiles();
            GenerateSourceFiles();
            GenerateTools();

            CompileLibs();

            return new SuccessResult();
        }
    
        public static string FilterDefaultString(string str, string sub, bool val) {
            var res = "";

            foreach (var line in str.Split('\n')) {
                if (line.Contains(sub)) {
                    var lline = line.Replace(sub, "");

                    if (val)
                        res += lline + "\n";
                } else
                    res += line + '\n';
            }

            return res;
        }

        public static string MakeBuildDirectory(string projectName, bool empty = false) {
            string path = Path.Join(ExePath, "builds", projectName);

            if (empty && Directory.Exists(path))
                Directory.Delete(path, true);

            try {
                Directory.CreateDirectory(path);
            }
            catch (Exception) {
                return null;
            }

            return path;
        }

        public static Result<string> GetBuildDirectory(string projectName) {
            string path = Path.Join(ExePath, "builds", projectName);

            if (!Directory.Exists(path))
                return new ErrorResult<string>("Build directory does not exist!");

            return new SuccessResult<string>(path);
        }
    }
}
