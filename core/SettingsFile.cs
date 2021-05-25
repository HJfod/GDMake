using System;
using System.Collections.Generic;

namespace gdmake {
    public class SettingsFile {
        public string GDPath { get; set; } = null;
        public HashSet<GDMake.Submodule> Submodules { get; set; } = new HashSet<GDMake.Submodule>();
        public List<Tuple<string, int>> Addresses { get; set; } = new List<Tuple<string, int>>();
    }
}