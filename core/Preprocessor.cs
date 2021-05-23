using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace gdmake {
    public class Preprocessor {
        public class Replacement {
            public string MacroName { get; set; }
            public string Value { get; set; }
        }

        public abstract class MacroFuncRes {}

        public class Macro {
            public string Text { get; internal set; }
            public string[] Parameters { get; internal set; }
            public string CppReplace { get; internal set; }
            public bool IsReplace { get; internal set; }
            public string Description { get; internal set; }
            public Func<string, MacroFuncRes> Replace { get; internal set; }
            public enum EReplaceType { Inside, NextFunction }
            public EReplaceType ReplaceType;

            public string GetFormattedDesc() {
                string desc = "";
                string collect = " * ";
                int collected = 0;

                foreach (var word in this.Description.Split(" ")) {
                    collect += word + " ";
                    collected += word.Length + 1;
                    if (collected > 30) {
                        desc += collect + "\n";
                        collected = 0;
                        collect = " * ";
                    }
                }

                desc += collect;

                return desc;
            }

            public Macro(
                string txt,
                string[] pms,
                string crepl,
                string desc,
                Func<string, MacroFuncRes> repl,
                EReplaceType type = EReplaceType.Inside
            ) {
                this.Text = txt;
                this.Parameters = pms;
                this.CppReplace = crepl;
                this.IsReplace = repl != null;
                this.Description = desc;
                this.Replace = repl;
                this.ReplaceType = type;
            }
        }

        public class Hook : MacroFuncRes {
            public string HookData { get; internal set; }
            public int Address { get; internal set; }
            public string HookName { get; internal set; }
            public string ReturnType { get; internal set; }
            public string CallingConvention { get; internal set; }
            public List<string> ArgTypes { get; internal set; } = new List<string>();

            public Hook(string rawData) {
                var data = rawData.Substring(1);

                var addr = data.Substring(0, data.IndexOf(')'));
                int addri;

                if (!Int32.TryParse(addr, out addri))
                    Console.WriteLine($"Unable to create hook: {addr} is not a valid address");
                else {
                    this.Address = addri;

                    data = data.Substring(data.IndexOf(')') + 1);

                    var funcDef = data.Substring(0, data.IndexOf('{'));

                    funcDef = funcDef.Trim();

                    // todo: figure out cconv, rtype, args from funcdef
                }
            }
        }

        public static readonly Macro[] Macros = new Macro[] {
            new Macro(
                "GDMAKE_MAIN", null, "void mod::loadMod(HMODULE)",
                "Main entry point for the mod. All default variables should be initialized at this point.",
                null
            ),
            new Macro(
                "GDMAKE_MAIN_HM", new string[] { "hModule" }, "void mod::loadMod(HMODULE hModule)",
                "Main entry point for the mod. All default variables should be initialized at this point.",
                null
            ),
            new Macro(
                "GDMAKE_UNLOAD", null, "void mod::unloadMod()",
                "Called when the mod is unloaded. Default modules are automatically unloaded after this function.",
                null
            ),
            new Macro(
                "GDMAKE_CREATE_HOOK", new string[] { "addr", "detour", "orig" },
                "MH_CreateHook((PVOID)(gd::base + addr), reinterpret_cast<LPVOID>(detour), reinterpret_cast<LPVOID*>(&orig))",
                "Alias macro for creating a hook at an address.",
                null
            ),
            new Macro(
                "GDMAKE_HOOK", new string[] { "addr" }, "",
                "Turns the function following this macro into a hook in the address. Use GDMAKE_ORIG to call the original function.",
                s => new Hook(s), Macro.EReplaceType.NextFunction
            ),
            new Macro(
                "GDMAKE_ORIG", new string[] { "..." }, "1",
                "Call the original function from a hook created with GDMAKE_HOOK.",
                null
            ),
            new Macro(
                "GDMAKE_ORIG_S", new string[] { "..." }, "\"\"",
                "Call the original function from a hook created with GDMAKE_HOOK.",
                null
            ),
        };

        public List<Hook> Hooks = new List<Hook>();

        public void GetMacrosAndReplace(string file) {
            var oText = File.ReadAllText(file);

            foreach (var macro in Macros)
                if (macro.Replace != null) {
                    var text = oText;

                    void ReplaceForSubstring(int sx, int ex) {
                        var res = macro.Replace(text.Substring(sx, ex - sx));

                        if (res is Hook)
                            this.Hooks.Add(res as Hook);
                    };
                    
                    while (text.Contains(macro.Text)) {
                        switch (macro.ReplaceType) {
                            case Macro.EReplaceType.Inside: {
                                var startIndex = text.IndexOf(macro.Text) + macro.Text.Length;
                                startIndex = text.IndexOf('(', startIndex) + 1;

                                int endIndex = startIndex;
                                int paren = 1;
                                while (paren > 0)
                                    if (text.Length < ++endIndex)
                                        switch (text[endIndex]) {
                                            case '(': paren++; break;
                                            case ')': paren--; break;
                                        }
                                    else break;
                                
                                ReplaceForSubstring(startIndex, endIndex + 1);

                                text = text.Substring(startIndex);
                            } break;
                            
                            case Macro.EReplaceType.NextFunction: {
                                var startIndex = text.IndexOf(macro.Text) + macro.Text.Length;
                                startIndex = text.IndexOf('(', startIndex);

                                int endIndex = text.IndexOf('{', startIndex) + 1;
                                int paren = 1;
                                while (paren > 0)
                                    if (text.Length > ++endIndex)
                                        switch (text[endIndex]) {
                                            case '{': paren++; break;
                                            case '}': paren--; break;
                                        }
                                    else break;
                                
                                ReplaceForSubstring(startIndex, endIndex + 1);

                                text = text.Substring(startIndex);
                            } break;
                        }
                    }
                }
        }

        public static Preprocessor PreprocessAllFilesInFolder(string path) {
            var pre = new Preprocessor();

            foreach (var file in Directory
                .EnumerateFiles(path, "*.*", SearchOption.AllDirectories)
                .Where(f => (new string[] { ".cpp", ".c", ".h", ".hpp" }).Any(s => f.EndsWith(s))))
                    pre.GetMacrosAndReplace(file);

            return pre;
        }
    }
}