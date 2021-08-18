using System;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Text.Json;
using System.Reflection;
using System.Collections.Generic;
using Microsoft.Win32;
using Microsoft.Build.Execution;
using utils;

namespace gdmake {
    public static class GDMake {
        public class Submodule {
            public enum TSubmoduleType {
                stCompiledLib,
                stHeaderOnly,
                stIncludeSource
            }

            public string Name { get; set; }
            public string URL { get; set; }
            public string IncludeHeader { get; set; }
            public TSubmoduleType Type { get; set; }
            public string CMakeDefs { get; set; }
            public string[] LibPaths { get; set; } = null; 
            public HashSet<string> IncludePaths { get; set; } = new HashSet<string>();
            public HashSet<string> SourcePaths { get; set; } = new HashSet<string>();

            public Submodule() {
                this.Name = "none";
                this.URL = "";
                this.IncludeHeader = "none";
                this.Type = TSubmoduleType.stIncludeSource;
                this.CMakeDefs = null;
            }

            public Submodule(
                string name,
                string url,
                TSubmoduleType type = TSubmoduleType.stIncludeSource,
                string defs = "",
                string[] lpath = null,
                HashSet<string> ipath = null
            ) {
                this.Name = name;
                this.URL = url;
                this.IncludeHeader = Name;
                this.CMakeDefs = defs;
                this.Type = type;
                this.LibPaths = lpath;
                if (ipath != null)
                    this.IncludePaths = ipath;
            }
        };

        public enum OutputLevel {
            Silent, Quiet, Minimal, Normal, Loud
        }

        public static string OutputToString(OutputLevel level) {
            switch (level) {
                case OutputLevel.Silent: return "Silent";
                case OutputLevel.Quiet: return "Quiet";
                case OutputLevel.Minimal: return "Minimal";
                case OutputLevel.Normal: return "Normal";
                case OutputLevel.Loud: return "Diagnostic";
            }

            return "Unknown";
        }

        public static OutputLevel OutputFromString(string level) {
            switch (level.ToLower()) {
                case "silent": return OutputLevel.Silent;
                case "quiet": return OutputLevel.Quiet;
                case "minimal": return OutputLevel.Minimal;
                case "normal": return OutputLevel.Normal;
                case "diagnostic": return OutputLevel.Loud;
                case "details": return OutputLevel.Loud;
                case "loud": return OutputLevel.Loud;
            }

            return OutputLevel.Quiet;
        }

        public const string DotfileName = ".gdmake";
        public static string ExePath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location).Replace("\\", "/");
        public static Submodule[] DefaultSubmodules = new Submodule[] {
            new Submodule (
                "gd.h",
                "https://github.com/HJfod/gd.h",
                Submodule.TSubmoduleType.stHeaderOnly
            ),

            new Submodule (
                "MinHook",
                "https://github.com/HJfod/MinHook",
                Submodule.TSubmoduleType.stCompiledLib,
                null, // "-DBUILD_SHARED_LIBS=ON",
                new string[] { "libs/minhook.x32.lib" }
            ),

            new Submodule (
                "Cocos2d",
                "https://github.com/HJfod/cocos-headers",
                Submodule.TSubmoduleType.stHeaderOnly,
                null,
                new string[] {
                    "submodules/Cocos2d/cocos2dx/libcocos2d.lib",
                    "submodules/Cocos2d/extensions/libExtensions.lib",
                },
                new HashSet<string> {
                    $"{ExePath}/submodules/Cocos2d/cocos2dx",
                    $"{ExePath}/submodules/Cocos2d/cocos2dx/include",
                    $"{ExePath}/submodules/Cocos2d/cocos2dx/kazmath/include",
                    $"{ExePath}/submodules/Cocos2d/cocos2dx/platform/third_party/win32/OGLES",
                    $"{ExePath}/submodules/Cocos2d/cocos2dx/platform/win32",
                    $"{ExePath}/submodules/Cocos2d/extensions",
                }
            ),
        };
        public static HashSet<Submodule> Submodules = new HashSet<Submodule>();
        public static SettingsFile SettingsFile = null;

        public static bool IsGlobalInitialized(bool dontLoadSettings = false) {
            if (!Directory.Exists(Path.Join(ExePath, "submodules")))
                return false;

            if (!Directory.Exists(Path.Join(ExePath, "include")))
                return false;

            if (!Directory.Exists(Path.Join(ExePath, "src")))
                return false;

            if (!Directory.Exists(Path.Join(ExePath, "tools")))
                return false;

            if (!File.Exists(Path.Join(ExePath, "GDMakeSettings.json")))
                return false;
            
            if (!dontLoadSettings)
                GDMake.LoadSettings();

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
                if (sub.IncludeHeader != null)
                    includeText += $"#include <{(sub.IncludeHeader.EndsWith(".h") ? sub.IncludeHeader : sub.IncludeHeader + ".h")}>\n";

            includeText += "#pragma warning(pop)\n\n";
            includeText += "#include \"GDMakeMacros.h\"\n";
            includeText += "#include \"GDMakeHelpers.h\"\n\n";
            includeText += DefaultStrings.GDMakeModNS + "\n";
            includeText += "#endif";

            var includeMacros =
                DefaultStrings.HeaderCredit + "\n" +
                "#ifndef __GDMAKE_MACROS_H__\n" +
                "#define __GDMAKE_MACROS_H__\n\n";
            
            foreach (var macro in Preprocessor.Macros)
                includeMacros += $"/**\n * Semantic information for GDMake.\n{macro.GetFormattedDesc()}\n */\n" + 
                    $"#define {macro.Text}" +
                    $"{(macro.Parameters != null ? ("(" + String.Join(",", macro.Parameters) + ")") : "")}" + 
                    $" {macro.CppReplace}\n\n";
            
            includeMacros += DefaultStrings.ExtraCCMacros;
            
            includeMacros += "#endif";

            File.WriteAllText(Path.Join(ExePath, "include/GDMakeMacros.h"), includeMacros);
            File.WriteAllText(Path.Join(ExePath, "include/GDMake.h"), includeText);
            File.WriteAllText(Path.Join(ExePath, "include/GDMakeHelpers.h"), DefaultStrings.HelpersCpp);
        }

        private static void GenerateSourceFiles() {
            File.WriteAllText(Path.Join(ExePath, "src/console.h"), DefaultStrings.ConsoleHeader);
            // File.WriteAllText(Path.Join(ExePath, "src/console.cpp"), DefaultStrings.ConsoleSource);
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
            OutputLevel level = OutputLevel.Quiet
        ) {
            var process = new Process();

            if (String.IsNullOrWhiteSpace(cMakeOpts)) cMakeOpts = null;

            process.StartInfo.Arguments = $"\"{cd}\" {pName} {config} {OutputToString(level)} {(cMakeOpts != null ? $"\"{cMakeOpts}\"" : "")}";
            process.StartInfo.FileName = Path.Join(ExePath, "build.bat");
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.OutputDataReceived += (sender, args) => {
                if (level != OutputLevel.Silent)
                    Console.WriteLine(args.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.WaitForExit();

            // var project = new Microsoft.Build.Evaluation.Project(Path.Join(cd, "build", pName + ".sln"));

            // project.SetProperty("Configuration", config);
            // project.SetProperty("PlatformTarget", "x86");

            // var res = BuildManager.DefaultBuildManager.Build(
            //     new BuildParameters {
            //         DetailedSummary = false,
            //         EnableNodeReuse = false,
            //         MaxNodeCount = System.Environment.ProcessorCount,
            //         OnlyLogCriticalEvents = false,
            //         ShutdownInProcNodeOnBuildFinish = true,
            //     },
            //     new BuildRequestData (
            //         project.CreateProjectInstance(),
            //         project.Targets.Select(t => t.Key).ToArray()
            //     )
            // );

            // if (res.OverallResult == BuildResultCode.Success) {
            //     Console.WriteLine("Build succesful!");
                
            //     return true;
            // }

            // Console.WriteLine(res.Exception.ToString());

            // return false;
        }

        public static Result<string> GetGDPath(bool figureOut = false) {
            if (!figureOut) {
                LoadSettings();

                if (SettingsFile.GDPath != null)
                    return new SuccessResult<string>(SettingsFile.GDPath);
            }

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

        public static void CompileLibs(OutputLevel outlvl = OutputLevel.Silent) {
            LoadSettings();

            Directory.CreateDirectory(Path.Join(ExePath, "libs"));
            Directory.CreateDirectory(Path.Join(ExePath, "dlls"));

            foreach (var sub in Submodules)
                if (sub.Type == Submodule.TSubmoduleType.stCompiledLib) {
                    Console.WriteLine($"Building {sub.Name}...");

                    RunBuildBat(Path.Join(ExePath, "submodules", sub.Name), sub.Name, "Release", sub.CMakeDefs, outlvl);

                    foreach (var file in Directory.GetFiles(
                        Path.Join(ExePath, "submodules", sub.Name, "build", "Release")
                    ))
                        if (file.EndsWith(".dll"))
                            File.Copy(file, Path.Join(ExePath, "dlls", Path.GetFileName(file)), true);
                        else if (file.EndsWith(".lib"))
                            File.Copy(file, Path.Join(ExePath, "libs", Path.GetFileName(file)), true);
                }

            if (!Directory.Exists(Path.Join(ExePath, "tools", "bin")) || IsDirectoryEmpty(Path.Join(ExePath, "tools", "bin"))) {
                Console.WriteLine("Building GDMake tools...");

                Directory.CreateDirectory(Path.Join(ExePath, "tools", "bin"));

                foreach (var dir in Directory.GetDirectories(Path.Join(ExePath, "tools")))
                    if (Path.GetFileName(dir) != "bin") {
                        var path = Path.Join(ExePath, "tools", Path.GetFileName(dir));

                        RunBuildBat(path, Path.GetFileName(dir), "Release", null, outlvl);

                        var exe = $"{Path.GetFileName(dir)}.exe";

                        File.Copy(
                            Path.Join(path, "build", "Release", exe),
                            Path.Join(ExePath, "tools", "bin", exe),
                            true
                        );
                    }
            }
        }

        public static void LoadSettings() {
            if (SettingsFile == null) {
                SettingsFile = JsonSerializer.Deserialize<SettingsFile>(
                    File.ReadAllText(Path.Join(ExePath, "GDMakeSettings.json"))
                );

                if (SettingsFile != null)
                    Submodules = SettingsFile.Submodules;
            }

            CheckSubmodules();
        }

        public static List<string> GetIncludePath() {
            var res = new List<string>();

            foreach (var sub in GDMake.Submodules)
                if (sub.IncludePaths != null)
                    foreach (var inc in sub.IncludePaths)
                        res.Add(inc);

            res.Add(Path.Join(GDMake.ExePath, "include").Replace("\\", "/"));
            
            return res;
        }

        public static Result SaveSettings() {
            CheckSubmodules();

            try {
                var sett_str = JsonSerializer.Serialize<SettingsFile>(
                    SettingsFile, new JsonSerializerOptions { WriteIndented = true }
                );

                File.WriteAllText(Path.Join(ExePath, "GDMakeSettings.json"), sett_str);
            } catch (Exception) {
                return new ErrorResult("Unable to save settings!");
            }

            return new SuccessResult();
        }

        public static void ForceDeleteDirectory(string target_dir) {
            string[] files = Directory.GetFiles(target_dir);
            string[] dirs = Directory.GetDirectories(target_dir);

            foreach (string file in files) {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }

            foreach (string dir in dirs)
                ForceDeleteDirectory(dir);

            Directory.Delete(target_dir, false);
        }

        public static void CheckSubmodules() {
            Submodules = Submodules.GroupBy(x => x.Name).Select(x => x.First()).ToHashSet();
            if (SettingsFile != null)
                SettingsFile.Submodules = Submodules;
        }

        public static Submodule GetSubmoduleByName(string name) {
            foreach (var sub in Submodules)
                if (sub.Name == name)
                    return sub;
                
            return null;
        }

        public static void AddSubmodule(Submodule sub) {
            CheckSubmodules();

            Console.WriteLine($"Installing {sub.Name}...");

            var path = Path.Join(ExePath, "submodules", sub.Name);

            if (Directory.Exists(path)) {
                Console.WriteLine($"{sub.Name} has already been installed!");

                Submodules.Add(sub);
                return;
            }

            LibGit2Sharp.Repository.Clone(sub.URL, path, new LibGit2Sharp.CloneOptions { RecurseSubmodules = true });

            Submodules.Add(sub);

            sub.IncludePaths.Add($"{GDMake.ExePath.Replace("\\", "/")}/submodules/{sub.Name}");

            foreach (var subi in Directory.GetDirectories(
                Path.Join(GDMake.ExePath, "submodules", sub.Name), "include", SearchOption.TopDirectoryOnly)
            )
                sub.IncludePaths.Add(Path.GetFullPath(subi).Replace("\\", "/"));

            foreach (var subi in Directory.GetDirectories(
                Path.Join(GDMake.ExePath, "submodules", sub.Name), "src", SearchOption.TopDirectoryOnly)
            ) {
                sub.SourcePaths.Add(Path.Join(Path.GetFullPath(subi), "*.cpp").Replace("\\", "/"));
                sub.SourcePaths.Add(Path.Join(Path.GetFullPath(subi), "*.c").Replace("\\", "/"));
            }

            GenerateIncludeFiles();

            var res = SaveSettings();
            if (res.Failure)
                Console.WriteLine((res as ErrorResult).Message);

            CheckSubmodules();
        }

        public static void UpdateSubmodule(Submodule sub) {
            Console.WriteLine($"Updating {sub.Name}...");

            string logMessage = "";
            using (var repo = new LibGit2Sharp.Repository(Path.Join(ExePath, "submodules", sub.Name)))
            {
                LibGit2Sharp.Commands.Pull(repo, new LibGit2Sharp.Signature(
                    new LibGit2Sharp.Identity(
                        "hi", "hello@yo"
                    ), DateTimeOffset.Now
                ), new LibGit2Sharp.PullOptions());
            }
            Console.WriteLine(logMessage);
        }

        public static Result RemoveSubmodule(string name) {
            CheckSubmodules();

            var dirPath = Path.Join(ExePath, "submodules", name);

            if (!Directory.Exists(dirPath))
                return new ErrorResult($"Submodule {name} does not exist!");
        
            try {
                ForceDeleteDirectory(dirPath);
            } catch (Exception) {
                return new ErrorResult($"Unable to delete directory!");
            }

            foreach (var sub in Submodules)
                if (sub.Name == name) {
                    Submodules.Remove(sub);
                    break;
                }

            CheckSubmodules();

            SaveSettings();

            return new SuccessResult();
        }

        public static Result InitializeGlobal(bool re = false, OutputLevel outlvl = OutputLevel.Silent) {
            if (!IsGlobalInitialized(true))
                re = true;

            foreach (var dir in new string[] {
                "builds",
                "submodules",
                "include",
                "src",
                "tools",
                "libs",
                "dlls",
            }) {
                if (re) try { ForceDeleteDirectory(Path.Join(ExePath, dir)); }
                catch (Exception) {}
                
                try { Directory.CreateDirectory(Path.Join(ExePath, dir)); }
                catch (Exception e) {
                    return new ErrorResult($"Unable to create directory: {e.Message}");
                }
            }

            if (re) {
                Submodules = new HashSet<Submodule>();

                File.Delete(Path.Join(ExePath, "GDMakeSettings.json"));

                SettingsFile = new SettingsFile();
                var res = GetGDPath(true);

                if (res.Failure)
                    return res;

                SettingsFile.GDPath = res.Data.Replace("\\\\", "\\").Replace("\\", "/");

                SaveSettings();
            } else
                try {
                    LoadSettings();
                } catch (Exception) {}

            foreach (var sub in DefaultSubmodules)
                try {
                    AddSubmodule(sub);
                } catch (Exception e) {
                    return new ErrorResult($"Unable to install submodule {sub.Name}: {e.Message}");
                }

            Console.WriteLine("Generating files...");

            File.WriteAllText(Path.Join(ExePath, "build.bat"), DefaultStrings.BuildBat);

            GenerateIncludeFiles();
            GenerateSourceFiles();
            GenerateTools();

            CompileLibs(outlvl);

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
