using System;
using System.Linq;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using utils;

namespace gdmake {
    public class Project {
        private string name;
        public string Name {
            get { return name; }
            set {
                name = value ?? Path.GetFileName(Directory.GetCurrentDirectory());
            }
        }

        private string dir;
        public string Dir {
            get { return dir; }
            set {
                dir = Path.GetFullPath(value ?? Directory.GetCurrentDirectory());
            }
        }

        private string builddir = null;
        private List<string> builddlls = null;
        private bool FullRegen = false;

        public GDMakeFile Dotfile = null;

        public Project() {
            this.Name = null;
            this.Dir = null;
        }

        public Result<string> Save(bool mkdir = false, bool mkexample = true) {
            if (!Directory.Exists(this.Dir))
                if (mkdir)
                    try { Directory.CreateDirectory(this.Dir); } 
                    catch (Exception e) {
                        return new ErrorResult<string>($"Unable to create directory {this.Dir}: {e.Message}");
                    }
                else
                    return new ErrorResult<string>($"Directory {this.Dir} does not exist! (Use --mkdir to create it)");

            this.Dotfile = new GDMakeFile(this.Name);
            var dotfile_str = JsonSerializer.Serialize(
                this.Dotfile, new JsonSerializerOptions { WriteIndented = true }
            );

            bool alreadyExists = File.Exists(Path.Join(this.Dir, GDMake.DotfileName));

            try { File.WriteAllText(Path.Join(this.Dir, GDMake.DotfileName), dotfile_str); }
            catch (Exception e) {
                return new ErrorResult<string>($"Unable to create {GDMake.DotfileName} file: {e.Message}");
            }

            if (mkexample && !File.Exists(Path.Join(this.Dir, "main.cpp")))
                File.WriteAllText(Path.Join(this.Dir, "main.cpp"), DefaultStrings.ExampleProjectCpp);

            if (alreadyExists)
                return new SuccessResult<string>($"Reinitialized as {this.Name} in {this.Dir}!");
            
            return new SuccessResult<string>($"Initialized as {this.Name} in {this.Dir}!");
        }
    
        public Result Load() {
            if (!Directory.Exists(this.Dir))
                return new ErrorResult($"Directory {this.Dir} does not exist!");
            
            if (!File.Exists(Path.Join(this.Dir, GDMake.DotfileName)))
                return new ErrorResult($"Directory {this.Dir} does not contain a {GDMake.DotfileName} file! " + 
                    "Use \"gdmake init\" to initialize a project.");

            this.Dotfile = JsonSerializer.Deserialize<GDMakeFile>(
                File.ReadAllText(Path.Join(this.Dir, GDMake.DotfileName))
            );

            this.Name = this.Dotfile.ProjectName;

            return new SuccessResult();
        }
    
        private string GenerateDLLMain() {
            var str = DefaultStrings.DllMain;
       
            str = GDMake.FilterDefaultString(str, "<<?CONSOLE>>", this.Dotfile.ConsoleEnabled);
            str = str.Replace("<<MOD_NAME>>", this.Name);
            str = str.Replace("<<GDMAKE_DIR>>", GDMake.ExePath);

            return str;
        }

        private string GenerateHookHeader(Preprocessor pre) {
            var str = DefaultStrings.HeaderCredit + 
            "\n#ifndef __GDMAKE_HOOKS_H__\n#define __GDMAKE_HOOKS_H__\n\n#include <GDMake.h>\n\n";

            var includes = "";
            var hooks = "";

            foreach (var hook in pre.Hooks) {
                foreach (var inc in hook.IncludesAndUsings)
                    if (!includes.Contains(inc))
                        includes += inc + "\n";

                hooks += $"inline {hook.GetTrampolineName()};\n{hook.GetFunctionSignature()};\n\n";
            }

            str += includes + "\n\n";
            str += hooks + "\n";

            str += "\n#endif\n";

            return str;
        }

        private string GenerateDebugHeader(Preprocessor pre) {
            var str = DefaultStrings.HeaderCredit +
            "\n#pragma once\n\n#include <string>\n#include <vector>\n\n";

            var dbgs = "";

            foreach (var dbg in pre.DebugMsgs)
                dbgs += $"void dbg_{dbg.Command.Replace(" ", "")}(std::vector<std::string>);\n\n";

            str += dbgs + "\n";

            return str;
        }

        private string GenerateConsoleSource(Preprocessor pre) {
            var str = DefaultStrings.ConsoleSource;

            var dbgs = "";

            foreach (var dbg in pre.DebugMsgs)
                dbgs += $"if (inp._Starts_with(\"{dbg.Command}\")) dbg_{dbg.Command.Replace(" ", "")}(args);\n";

            str = str.Replace("<<GDMAKE_DEBUGS>>", dbgs);
            str = str.Replace("<<GDMAKE_DIR>>", GDMake.ExePath);

            return str;
        }

        private string GenerateModLoad(Preprocessor pre) {
            var str = DefaultStrings.ModLoadSource;

            str = str.Replace("<<LOAD_CODE>>", "");
            str = str.Replace("<<UNLOAD_CODE>>", "");

            var hookCode = "";

            foreach (var hook in pre.Hooks)
                hookCode += $"    GDMAKE_CREATE_HOOK({hook.Address}, {hook.FuncName}, {hook.FuncName}{Preprocessor.Hook.TrampolineExt});\n";
            
            str = str.Replace("<<GDMAKE_HOOKS>>", hookCode);

            return str;
        }

        private string GenerateCMakeLists(List<string> libs) {
            var str = DefaultStrings.CMakeLists;
       
            str = str.Replace("<<GDMAKE_DIR>>", GDMake.ExePath.Replace("\\", "/"));
            str = str.Replace("<<MOD_NAME>>", this.Name);
            
            var libstr = "";
            foreach (var lib in libs)
                if (Path.IsPathRooted(lib))
                    libstr += lib + "\n";
                else
                    libstr += $"{GDMake.ExePath.Replace("\\", "/")}/{lib}\n";

            str = str.Replace("<<GDMAKE_LIBS>>", libstr);

            var incpath = "";
            foreach (var inc in GDMake.GetIncludePath())
                incpath += inc + "\n";
                
            str = str.Replace("<<GDMAKE_HEADERS>>", incpath);

            var srcpath = "";
            foreach (var sub in GDMake.Submodules)
                if (this.Dotfile.Submodules.Contains(sub.Name) && sub.Type == GDMake.Submodule.TSubmoduleType.stIncludeSource)
                    foreach (var src in sub.SourcePaths)
                        srcpath += src + "\n";
            
            str = str.Replace("<<GDMAKE_SOURCES>>", srcpath);

            str = GDMake.FilterDefaultString(str, "<<?GDMAKE_DLLMAIN>>", this.Dotfile.EntryPoint == null);
            str = GDMake.FilterDefaultString(str, "<<?GDMAKE_CONSOLE>>", this.Dotfile.ConsoleEnabled);

            return str;
        }

        private List<string> FindFiles(string dir_name, string patterns, bool search_subdirectories) {
            // from http://csharphelper.com/blog/2015/06/find-files-that-match-multiple-patterns-in-c/

            // Make the result list.
            List<string> files = new List<string>();

            // Get the patterns.
            string[] pattern_array = patterns.Split(';');

            // Search.
            SearchOption search_option = SearchOption.TopDirectoryOnly;
            if (search_subdirectories)
                search_option = SearchOption.AllDirectories;
            foreach (string pattern in pattern_array)
            {
                foreach (string filename in Directory.GetFiles(
                    dir_name, pattern, search_option))
                {
                    if (!files.Contains(filename)) files.Add(filename);
                }
            }

            // Sort.
            files.Sort();

            // Return the result.
            return files;
        }

        private bool IsNotIgnoredFilePath(string ignore, string path) {
            path = path.Replace("\\", "/");
            var dirs = path.Split("/");

            foreach (var dir in dirs)
                if (Regex.IsMatch(dir, ignore))
                    return false;
            
            return true;
        }

        private void CopyFolderRecurse(string sourcePath, string targetPath, List<string> ignores) {
            foreach (string dirPath in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
                if (!ignores.Any(s => IsNotIgnoredFilePath(s, dirPath)))
                    Directory.CreateDirectory(dirPath.Replace(sourcePath, targetPath));

            foreach (string newPath in FindFiles(
                sourcePath,
                "*.cpp;*.cc;*.cp;*.c;*.cxx;*.hpp;*.hh;*.hp;*.h;*.hxx",
                true
            ))
                if (!ignores.Any(s => IsNotIgnoredFilePath(s, newPath))) {
                    var aTargetPath = newPath.Replace(sourcePath, targetPath);

                    if (File.Exists(aTargetPath) && !this.FullRegen) {
                        FileInfo sourceInfo = new FileInfo(sourcePath);
                        FileInfo targetInfo = new FileInfo(aTargetPath);

                        if (sourceInfo.LastWriteTime < targetInfo.LastWriteTime)
                            continue;
                    }

                    File.Copy(newPath, aTargetPath, true);
                }
        }

        private void CopyAllSourceFiles(string dest) {
            var ignores = this.Dotfile.IgnoredFiles;
            ignores.Add(".gdmake");
            ignores.Add(".vscode");
            ignores.Add(".gitignore");
            ignores.Add(".git");

            CopyFolderRecurse(this.Dir, dest, ignores);
        }

        private void DeleteSourceFilesNotInDestination(string dest) {
            foreach (string sourcePath in FindFiles(
                dest,
                "*.cpp;*.cc;*.cp;*.c;*.cxx;*.hpp;*.hh;*.hp;*.h;*.hxx",
                true
            )) {
                var targetPath = sourcePath.Replace(dest, this.Dir);

                if (!File.Exists(targetPath))
                    File.Delete(sourcePath);
            }
        }

        private void GenerateAndSaveFile(string dir, string filename, string data) {
            string file = Path.Join(dir, filename);
            string oldFile = "";
            if (File.Exists(file))
                oldFile = File.ReadAllText(file);

            if (Dotfile.EntryPoint == null && oldFile != data)
                File.WriteAllText(file, data);
        }

        public Result Generate(bool empty = false, bool fullRegen = false, bool verbose = false) {
            Console.WriteLine("Generating...");

            var dir = GDMake.MakeBuildDirectory(this.Name, empty);

            this.FullRegen = fullRegen;

            if (dir == null)
                return new ErrorResult("Unable to create build directory!");

            if (FullRegen) {
                if (Directory.Exists(Path.Join(dir, "src")))
                    GDMake.ForceDeleteDirectory(Path.Join(dir, "src"));

                try { Directory.CreateDirectory(Path.Join(dir, "src")); }
                catch (Exception e) {
                    return new ErrorResult($"Error: {e.Message}");
                }
            }

            CopyAllSourceFiles(Path.Join(dir, "src"));
            DeleteSourceFilesNotInDestination(Path.Join(dir, "src"));

            var pre = Preprocessor.PreprocessAllFilesInFolder(this.Dir, Path.Join(dir, "src"), fullRegen, verbose);
            
            GenerateAndSaveFile(dir, "dllmain.cpp", GenerateDLLMain());
            GenerateAndSaveFile(dir, "debug.h", GenerateDebugHeader(pre));
            GenerateAndSaveFile(dir, "console.cpp", GenerateConsoleSource(pre));
            GenerateAndSaveFile(dir, "hooks.h", GenerateHookHeader(pre));
            GenerateAndSaveFile(dir, "mod.cpp", GenerateModLoad(pre));
            GenerateAndSaveFile(dir, "mod.h", DefaultStrings.ModLoadHeader);
            GenerateAndSaveFile(dir, "CMakeLists.txt", GenerateCMakeLists(this.Dotfile.Libs));

            this.builddir = dir;
            this.builddlls = this.Dotfile.Dlls;

            return new SuccessResult();
        }

        public Result Build(string verbosity = "silent", string config = "RelWithDebInfo") {
            if (builddir == null)
                return new ErrorResult("Build directory not set (Make sure to generate first!)");

            // GDMake.CompileLibs();

            Console.WriteLine("Building DLL...");

            if (Directory.Exists(Path.Join(builddir, "build", config)))
                foreach (var file in Directory.GetFiles(Path.Join(builddir, "build", config), "*.dll"))
                    try { File.Delete(file); } catch (Exception) {}

            var verb = verbosity;
            if (verb == "silent") verb = "quiet";
            GDMake.RunBuildBat(Path.Join(builddir).Replace("\\", "/"), this.Name, config, null, verbosity == "silent", verb);

            var resDir = Path.Join(builddir, "res");

            if (Directory.Exists(resDir))
                foreach (var file in Directory.GetFiles(resDir, "*.dll")) {
                    try { File.Delete(file); } catch (Exception) {}
                }

            Directory.CreateDirectory(resDir);

            string resPath = null;
            foreach (var file in Directory.GetFiles(
                Path.Join(builddir, "build", config)
            )) 
                if (file.EndsWith(".dll")){
                    resPath = Path.Join(resDir, Path.GetFileName(file));

                    File.Copy(file, resPath, true);
                }
            
            if (this.builddlls != null)
                foreach (var dll in this.builddlls)
                    File.Copy(dll, Path.Join(resDir));

            if (resPath == null)
                return new ErrorResult("Compile error, see message above");
            
            Console.WriteLine($"Succesfully built in {resPath}");

            return new SuccessResult();
        }

        public Result Run() {
            Console.WriteLine("Running...");

            var res = GDMake.MoveSharedDLLs();

            if (res.Failure)
                return res;

            if (!GDMake.GDIsRunning())
                return new ErrorResult("GD is not running!");
            
            res = GDMake.GetBuildDirectory(this.Name);

            if (res.Failure)
                return res;
            
            foreach (var resc in Dotfile.Resources)
                try {
                    var target = Path.Join(
                        Path.GetDirectoryName(GDMake.GetGDPath().Data),
                        "Resources", 
                        Path.GetFileName(resc)
                    );
                    if (!File.Exists(target))
                        File.Copy(resc, target);
                } catch (Exception) { Console.WriteLine($"Error copying {resc}"); }

            res = GDMake.InjectDLL(Path.Join((res as SuccessResult<string>).Data, "res", $"{this.Name}.dll"));

            if (res.Failure)
                return res;

            Console.WriteLine("Injected DLL!");

            return new SuccessResult();
        }
    }
}
