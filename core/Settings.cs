using System.Collections.Generic;

namespace gdmake {
    public class SettingsFile {
        public string GDPath { get; set; } = null;
        public List<GDMake.Submodule> Submodules { get; set; } = new List<GDMake.Submodule>();
    }
}