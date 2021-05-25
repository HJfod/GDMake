using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using utils;

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
            public const string TrampolineExt = "_o";
            public string HookData { get; internal set; }
            public int Address { get; internal set; }
            public string FuncName { get; internal set; }
            public string ReturnType { get; internal set; }
            public string CallingConvention { get; internal set; }
            public string Args { get; internal set; }
            public string RawSignature { get; internal set; }
            public List<string> Includes { get; internal set; } = new List<string>();

            public string GetTrampolineName() {
                return $"{this.ReturnType} ({this.CallingConvention}* {this.FuncName}{TrampolineExt})({this.Args})";
            }

            public string GetFunctionSignature() {
                return this.RawSignature;
            }

            public Hook(string rawData) {
                var data = rawData.Substring("GDMAKE_HOOK".Length);
                data = data.Substring(data.IndexOf('(') + 1);

                var addr = data.Substring(0, data.IndexOf(')'));
                int addri = 0;

                if (!Int32.TryParse(addr.Substring(2), NumberStyles.HexNumber, null, out addri))
                    addri = Addresses.Names.GetValueOrDefault(addr.Replace(" ", "").Replace("::", "."), 0);
                
                this.Address = addri;

                if (addri == 0) {
                    Console.WriteLine($"Unable to create hook: {addr} is not a valid address");
                    this.HookData = rawData;
                } else {
                    try {
                        data = data.Substring(data.IndexOf(')') + 1);

                        this.HookData = data;

                        var funcDef = data.Substring(0, data.IndexOf('{'));

                        funcDef = funcDef.Replace("\r\n", "").Trim();
                        funcDef = Utils.NormalizeWhiteSpaceForLoop(funcDef);

                        this.RawSignature = funcDef;

                        var funcType = funcDef.Substring(0, funcDef.IndexOf("(")).Trim();
                        var funcParams = funcDef.Substring(
                            funcDef.IndexOf('(') + 1,
                            funcDef.IndexOf(')') - funcDef.IndexOf('(') - 1
                        ).Trim();

                        var types = funcType.Split(' ');
                        var cconvs = new string[] {
                            "fastcall",
                            "thiscall",
                            "stdcall",
                            "cdecl",
                            "vectorcall",
                            "clrcall"
                        };

                        for (var i = 0; i < types.Length - 1; i++)
                            if (cconvs.Any(s => types[i].Contains(s)))
                                this.CallingConvention = types[i];
                            else
                                this.ReturnType += types[i] + ' ';
                        
                        this.FuncName = types[types.Length - 1];
                        this.Args = funcParams;

                        this.HookData = this.HookData.Replace("GDMAKE_ORIG_S", $"{this.FuncName}{TrampolineExt}");
                        this.HookData = this.HookData.Replace("GDMAKE_ORIG", $"{this.FuncName}{TrampolineExt}");
                    } catch (Exception e) {
                        Console.WriteLine($"Unable to create hook: {e}");
                    }
                }
            }
        }

        public static readonly Macro[] Macros = new Macro[] {
            new Macro(
                "GDMAKE_MAIN", null, "void mod::loadMod(HMODULE)",
                "Main entry point for the mod. All default variables should be initialized at this point. Only called if EntryPoint is null.",
                null
            ),
            new Macro(
                "GDMAKE_MAIN_HM", new string[] { "hModule" }, "void mod::loadMod(HMODULE hModule)",
                "Main entry point for the mod. All default variables should be initialized at this point. Only called if EntryPoint is null.",
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
            // var includes = new List<string>();
            var extraIncludes = "";

            try {
            var iText = oText;

            // while (iText.Contains("#include")) {
            //     var ix = iText.IndexOf("#include");

            //     iText = iText.Substring(ix + "#include".Length).TrimStart();

            //     var next = iText.IndexOf('>') + 1;
            //     if (iText[0] == '"')
            //         next = iText.IndexOf('"', 1) + 1;
                
            //     includes.Add(iText.Substring(0, next));
            // }

            foreach (var macro in Macros)
                if (macro.Replace != null) {
                    var index = 0;

                    void ReplaceForSubstring(int sx, int ex) {
                        var res = macro.Replace(oText.Substring(sx, ex));

                        if (res is Hook) {
                            if ((res as Hook).Address == 0) {
                                Console.WriteLine($"^^ Note: in {Path.GetFileName(file)}");
                                return;
                            }

                            this.Hooks.Add(res as Hook);

                            if (!extraIncludes.Contains("#include <hooks.h>"))
                                extraIncludes += "#include <hooks.h>\n";

                            var aStringBuilder = new StringBuilder(oText);
                            aStringBuilder.Remove(sx, ex);
                            aStringBuilder.Insert(sx, (res as Hook).HookData);
                            oText = aStringBuilder.ToString();
                        }
                    };
                    
                    while (index < oText.Length && oText.IndexOf(macro.Text, index) != -1) {
                        switch (macro.ReplaceType) {
                            case Macro.EReplaceType.Inside: {
                                var startIndex = oText.IndexOf(macro.Text) + macro.Text.Length;
                                startIndex = oText.IndexOf('(', startIndex) + 1;

                                int endIndex = startIndex;
                                int paren = 1;
                                while (paren > 0)
                                    if (oText.Length < ++endIndex)
                                        switch (oText[endIndex]) {
                                            case '(': paren++; break;
                                            case ')': paren--; break;
                                        }
                                    else break;
                                
                                ReplaceForSubstring(startIndex, endIndex + 1);

                                index += startIndex;
                            } break;
                            
                            case Macro.EReplaceType.NextFunction: {
                                var startIndex = oText.IndexOf(macro.Text, index);
                                var six = startIndex + macro.Text.Length;
                                six = oText.IndexOf('(', six);

                                int endIndex = oText.IndexOf('{', six) + 1;
                                int paren = 1;
                                while (paren > 0)
                                    if (oText.Length > ++endIndex)
                                        switch (oText[endIndex]) {
                                            case '{': paren++; break;
                                            case '}': paren--; break;
                                        }
                                    else break;

                                index += six;

                                ReplaceForSubstring(startIndex, endIndex - startIndex + 1);
                            } break;
                        }
                    }
                }
            } catch (Exception e) {
                Console.WriteLine($"Unable to process GDMake macros in {file}: {e}");
            }

            oText = extraIncludes + oText;

            File.WriteAllText(file, oText);
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
