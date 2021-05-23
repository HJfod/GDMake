using System;
using System.Linq;
using System.IO;
using System.Text.Json;
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

            if (mkexample)
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

        private string GenerateModLoad() {
            var str = DefaultStrings.ModLoadSource;

            str = str.Replace("<<LOAD_CODE>>", "");
            str = str.Replace("<<UNLOAD_CODE>>", "");

            return str;
        }

        private string GenerateCMakeLists() {
            var str = DefaultStrings.CMakeLists;
       
            str = str.Replace("<<GDMAKE_DIR>>", GDMake.ExePath.Replace("\\", "/"));
            str = str.Replace("<<MOD_NAME>>", this.Name);

            return str;
        }

        private void CopyFolderRecurse(string sourcePath, string targetPath, List<string> ignores) {
            // Now Create all of the directories
            foreach (string dirPath in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
                if (!ignores.Any(s => s == dirPath))
                    Directory.CreateDirectory(dirPath.Replace(sourcePath, targetPath));

            // Copy all the files & Replaces any files with the same name
            foreach (string newPath in Directory.EnumerateFiles(sourcePath, "*.*", SearchOption.AllDirectories))
                if (!ignores.Any(s => s == Path.GetFileName(newPath)))
                    File.Copy(newPath, newPath.Replace(sourcePath, targetPath), true);
        }

        private void CopyAllSourceFiles(string dest) {
            var ignores = this.Dotfile.IgnoredFiles;
            ignores.Add(".gdmake");
            ignores.Add(".vscode");
            ignores.Add(".gitignore");
            ignores.Add(".git");

            CopyFolderRecurse(this.Dir, dest, ignores);
        }

        public Result Generate(bool empty = false) {
            Console.WriteLine("Generating...");

            var dir = GDMake.MakeBuildDirectory(this.Name, empty);

            if (dir == null)
                return new ErrorResult("Unable to create build directory!");

            try { File.WriteAllText(Path.Join(dir, "dllmain.cpp"), GenerateDLLMain()); }
            catch (Exception e) {
                return new ErrorResult($"Error: {e.Message}");
            }

            try { Directory.CreateDirectory(Path.Join(dir, "src")); }
            catch (Exception e) {
                return new ErrorResult($"Error: {e.Message}");
            }

            CopyAllSourceFiles(Path.Join(dir, "src"));

            Preprocessor.PreprocessAllFilesInFolder(Path.Join(dir, "src"));
            
            try { File.WriteAllText(Path.Join(dir, "mod.cpp"), GenerateModLoad()); }
            catch (Exception e) {
                return new ErrorResult($"Error: {e.Message}");
            }

            try { File.WriteAllText(Path.Join(dir, "mod.h"), DefaultStrings.ModLoadHeader); }
            catch (Exception e) {
                return new ErrorResult($"Error: {e.Message}");
            }

            try { File.WriteAllText(Path.Join(dir, "CMakeLists.txt"), GenerateCMakeLists()); }
            catch (Exception e) {
                return new ErrorResult($"Error: {e.Message}");
            }

            this.builddir = dir;

            return new SuccessResult();
        }

        public Result Build(string verbosity = "quiet", string config = "RelWithDebInfo") {
            if (builddir == null)
                return new ErrorResult("Build directory not set (Make sure to generate first!)");

            GDMake.CompileLibs();

            Console.WriteLine("Building DLL...");

            GDMake.RunBuildBat(Path.Join(builddir), this.Name, config, null, false, verbosity);

            var resDir = Path.Join(builddir, "res");

            if (Directory.Exists(resDir))
                foreach (var file in Directory.GetFiles(resDir, "*.dll"))
                    File.Delete(file);

            Directory.CreateDirectory(resDir);

            string resPath = null;
            foreach (var file in Directory.GetFiles(
                Path.Join(builddir, "build", config)
            )) 
                if (file.EndsWith(".dll")){
                    resPath = Path.Join(resDir, Path.GetFileName(file));

                    File.Copy(file, resPath, true);
                }

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

            res = GDMake.InjectDLL(Path.Join((res as SuccessResult<string>).Data, "res", $"{this.Name}.dll"));

            if (res.Failure)
                return res;

            Console.WriteLine("Injected DLL!");

            return new SuccessResult();
        }
    }
}
