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
        public bool ConsoleEnabled { get; set; } = false;
        public TargetCompiler Compiler { get; set; } = TargetCompiler.MSVC;
        public TargetPlatform Platform { get; set; } = TargetPlatform.Win32;
        public List<string> IgnoredFiles { get; set; } = new List<string>();
        public List<string> Libs { get; set; } = GDMake.DefaultSubmodules
            .Where(x => x.LibPaths != null)
            .SelectMany(x => x.LibPaths)
            .ToList();
        public List<string> Dlls { get; set; } = null;
        public List<string> Resources { get; set; } = new List<string>();

        public GDMakeFile() {
            this.ProjectName = null;
        }
        public GDMakeFile(string name) {
            this.ProjectName = name;
        }
    }
}