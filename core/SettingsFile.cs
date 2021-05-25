using System;
using System.Collections.Generic;

namespace gdmake {
    public class SettingsFile {
        public string GDPath { get; set; } = null;
        public List<GDMake.Submodule> Submodules { get; set; } = new List<GDMake.Submodule>();
        public List<Tuple<string, int>> Addresses { get; set; } = new List<Tuple<string, int>>();
    }
}