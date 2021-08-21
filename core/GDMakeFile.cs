using System.Linq;
using System.Collections.Generic;

namespace gdmake {
    public class GDMakeFile {
        public enum TargetCompiler {
            MSVC,
            Clang
        }

        public enum TargetPlatform {
            Win32
        }

        public string ProjectName { get; set; }
        public string EntryPoint { get; set; } = null;
        public string Version { get; set; } = "v1.0";
        public bool ConsoleEnabled { get; set; } = false;
        public TargetCompiler Compiler { get; set; } = TargetCompiler.MSVC;
        public TargetPlatform Platform { get; set; } = TargetPlatform.Win32;
        public List<string> Submodules { get; set; } = GDMake.DefaultSubmodules.Select(x => x.Name).ToList<string>();
        public List<string> IgnoredFiles { get; set; } = new List<string>();
        public List<string> Libs { get; set; } = GDMake.DefaultSubmodules
            .Where(x => x.LibPaths != null)
            .SelectMany(x => x.LibPaths)
            .ToList();
        public List<string> Dlls { get; set; } = null;
        public List<string> Resources { get; set; } = new List<string>();
        public bool DebugLogHookCalls { get; set; } = false;
        public bool SeparateHookFiles { get; set; } = false;
        public bool ReplaceIncludeGuards { get; set; } = false;
        public bool IncludeVersionInName { get; set; } = true;

        public GDMakeFile() {
            this.ProjectName = null;
        }
        public GDMakeFile(string name) {
            this.ProjectName = name;
        }
    }
}