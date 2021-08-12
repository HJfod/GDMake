using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using utils;

namespace gdmake {
    using FindItem = Tuple<string, bool, Func<string, string>>;

    public class Preprocessor {
        private string BasePath { get; set; }
        private string ResultPath { get; set; }
        private bool Verbose { get; set; }
        private bool ReplaceAllFiles { get; set; }
        public bool AddLogToHook { get; internal set; }

        public class Replacement {
            public string MacroName { get; set; }
            public string Value { get; set; }
        }

        public abstract class MacroFuncRes {
            public int StringOffset = 0;
        }

        public class Macro {
            public string Text { get; internal set; }
            public string[] Parameters { get; internal set; }
            public string CppReplace { get; internal set; }
            public bool IsReplace { get; internal set; }
            public string Description { get; internal set; }
            public Func<string, Preprocessor, MacroFuncRes> Replace { get; internal set; }
            public enum EReplaceType { Inside, NextFunction, NoReplace }
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
                Func<string, Preprocessor, MacroFuncRes> repl,
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
            public string Symbol { get; internal set; }
            public string Module { get; internal set; }
            public string FuncName { get; internal set; }
            public string ReturnType { get; internal set; }
            public string CallingConvention { get; internal set; } = null;
            public string Args { get; internal set; }
            public string RawSignature { get; internal set; }
            public HashSet<string> IncludesAndUsings { get; internal set; } = new HashSet<string>();

            public string GetTrampolineName() {
                return $"{this.ReturnType} ({this.CallingConvention}* {this.FuncName}{TrampolineExt})({this.Args})";
            }

            public string GetFunctionSignature() {
                if (this.RawSignature.Contains("edx_t,")) {
                    var sig = this.RawSignature;
                    sig.Replace("__fastcall", "__thiscall");
                    sig.Replace("edx_t,", "");
                    return sig;
                }

                return this.RawSignature;
            }

            public Hook(string rawData, Preprocessor pre) {
                var data = rawData.Substring("GDMAKE_HOOK".Length);
                data = data.Substring(data.IndexOf('(') + 1);

                var addr = data.Substring(0, data.IndexOf(')'));
                int addri = 0;

                if (addr.Contains('"')) {
                    addr = addr.Substring(1, addr.Length - 2);

                    if (!addr.Contains("::")) {
                        Console.WriteLine($"Unable to create hook: {addr} - missing module name");
                        this.HookData = rawData;
                    } else {
                        this.Module = addr.Substring(0, addr.IndexOf("::"));
                        this.Symbol = addr.Substring(addr.IndexOf("::") + 2);

                        addri = -1;
                        this.Address = -1;
                    }
                } else {
                    if (!Int32.TryParse(addr.Substring(2), NumberStyles.HexNumber, null, out addri))
                        addri = Addresses.Names.GetValueOrDefault(addr.Replace(" ", "").Replace("::", "."), 0);

                    this.Address = addri;
                }

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

                        var funcType = funcDef.Substring(0, funcDef.LastIndexOf("(")).Trim();
                        var funcParams = funcDef.Substring(
                            funcDef.LastIndexOf('(') + 1,
                            funcDef.LastIndexOf(')') - funcDef.LastIndexOf('(') - 1
                        ).Trim();

                        var types = funcType.Split(' ');
                        var cconvs = new string[] {
                            "fastcall",
                            "thiscall",
                            "stdcall",
                            "cdecl",
                            "vectorcall",
                            "clrcall",
                            "gdmakecall"
                        };

                        for (var i = 0; i < types.Length - 1; i++)
                            if (cconvs.Any(s => types[i].Contains(s)))
                                this.CallingConvention = types[i];
                            else
                                this.ReturnType += types[i] + ' ';

                        var attrs = new List<string>();  
                        if (funcDef.Contains("GDMAKE_ATTR")) {
                            var attrStr = funcDef.Substring(
                                funcDef.IndexOf("GDMAKE_ATTR")
                            );

                            funcDef = funcDef.Substring(0, funcDef.IndexOf("GDMAKE_ATTR"));
                            attrStr = attrStr.Substring(attrStr.IndexOf("(") + 1);
                            funcDef += attrStr.Substring(attrStr.IndexOf(")") + 1);
                            attrStr = attrStr.Substring(0, attrStr.IndexOf(")"));

                            attrs = attrStr.Split(",").ToList();
                            
                            attrs.ForEach(s => s.Trim());
                        }
                        
                        this.FuncName = types[types.Length - 1];
                        this.Args = funcParams;

                        if (pre.AddLogToHook && !attrs.Contains("NoLog")) {
                            var start = this.HookData.Substring(0, this.HookData.IndexOf('{') + 1);
                            var end = this.HookData.Substring(this.HookData.IndexOf('{') + 1);

                            this.HookData =
                                start + $"std::cout << \"hook -> {this.FuncName}\\n\";" + end;
                        }

                        this.HookData = this.HookData.Replace("GDMAKE_ORIG_S", $"{this.FuncName}{TrampolineExt}");
                        this.HookData = this.HookData.Replace("GDMAKE_ORIG_P", $"{this.FuncName}{TrampolineExt}");
                        this.HookData = this.HookData.Replace("GDMAKE_ORIG_V", $"{this.FuncName}{TrampolineExt}");
                        this.HookData = this.HookData.Replace("GDMAKE_ORIG", $"{this.FuncName}{TrampolineExt}");

                        this.StringOffset = this.HookData.Length - rawData.Length;
                    } catch (Exception e) {
                        Console.WriteLine($"Unable to create hook: {e}");
                    }
                }
            }
        }

        public class DebugMsg : MacroFuncRes {
            public string Command { get; internal set; }
            public string DbgData { get; internal set; }

            public DebugMsg(string rawData) {
                var data = rawData.Substring("GDMAKE_DEBUG".Length);
                data = data.Substring(data.IndexOf('(') + 1);

                this.Command = data.Substring(0, data.IndexOf(')'));
                data = data.Substring(data.IndexOf(')') + 1);

                string argsName = "";
                if (this.Command.Contains(',')) {
                    argsName = this.Command.Substring(this.Command.IndexOf(',') + 1);

                    this.Command = this.Command.Substring(0, this.Command.IndexOf(','));
                }

                this.DbgData = $"void dbg_{this.Command}(std::vector<std::string> {argsName}){data}";

                this.StringOffset = rawData.Length;
            }
        }

        public static readonly Macro[] Macros = new Macro[] {
            new Macro(
                "GDMAKE_MAIN", null, "bool mod::loadMod(HMODULE)",
                "Main entry point for the mod. All default variables should be initialized at this point. Only called if EntryPoint is null.",
                null
            ),
            new Macro(
                "GDMAKE_MAIN_HM", new string[] { "hModule" }, "bool mod::loadMod(HMODULE hModule)",
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
                "if (MH_CreateHook((PVOID)(gd::base + addr), reinterpret_cast<LPVOID>(detour), reinterpret_cast<LPVOID*>(&orig)) != MH_OK) return \"Unable to hook \"#detour\"!\";",
                "Alias macro for creating a hook at an address.",
                null
            ),
            new Macro(
                "GDMAKE_CREATE_HOOK_A", new string[] { "addr", "detour", "orig" },
                "if (MH_CreateHook((PVOID)(addr), reinterpret_cast<LPVOID>(detour), reinterpret_cast<LPVOID*>(&orig)) != MH_OK) return \"Unable to hook \"#detour\"!\";",
                "Alias macro for creating a hook at an address.",
                null
            ),
            new Macro(
                "GDMAKE_HOOK", new string[] { "addr" }, "",
                "Turns the function following this macro into a hook in the address. Use GDMAKE_ORIG to call the original function.",
                (s, pre) => new Hook(s, pre), Macro.EReplaceType.NextFunction
            ),
            new Macro(
                "GDMAKE_ORIG", new string[] { "..." }, "1",
                "Call the original function from a hook created with GDMAKE_HOOK.",
                null
            ),
            new Macro(
                "GDMAKE_ORIG_V", new string[] { "..." }, "",
                "Call the original function from a hook created with GDMAKE_HOOK.",
                null
            ),
            new Macro(
                "GDMAKE_ORIG_S", new string[] { "..." }, "\"\"",
                "Call the original function from a hook created with GDMAKE_HOOK.",
                null
            ),
            new Macro(
                "GDMAKE_ORIG_P", new string[] { "..." }, "nullptr",
                "Call the original function from a hook created with GDMAKE_HOOK.",
                null
            ),
            new Macro(
                "GDMAKE_INT_CONCAT10", new string[] { "str0", "str1", "str2", "str3", "str4", "str5", "str6", "str7", "str8", "str9" },
                "str0##str1##str2##str3##str4##str5##str6##str7##str8##str9",
                "Internal GDMake macro for concatenating 10 strings (note: __VA_ARGS__ just doesn't work)",
                null
            ),
            new Macro(
                "GDMAKE_INT_CONCAT2", new string[] { "str0", "str1" }, "str0##str1",
                "Internal GDMake macro for concatenating two strings",
                null
            ),
            new Macro(
                "GDMAKE_DEBUG", new string[] { "command", "argvar" }, "void GDMAKE_INT_CONCAT2(dbg_, command)(std::vector<std::string> argvar)",
                "Add a custom debug input to the console",
                (s, pre) => new DebugMsg(s), Macro.EReplaceType.NoReplace
            ),
            new Macro(
                "GDMAKE_ATTR", new string[] { "..." }, "",
                "Add custom GDMake attributes",
                null
            ),
        };

        private static int HasSubstringAndItsNotCommentedOut(string str, string sub) {
            int inBlockComment = -1;
            int foffset = 0;
            foreach (var line in str.Split('\n')) {
                int offset = line.Length;
                int s_offset = 0;

                if (inBlockComment != -1) {
                    s_offset = line.IndexOf("*/") + 1;

                    if (s_offset > 0)
                        inBlockComment = -1;
                } else
                    inBlockComment = line.IndexOf("/*");

                if (inBlockComment != -1)
                    offset = inBlockComment;
                else
                    if (line.Contains("//"))
                        offset = line.IndexOf("//");

                foffset += line.Length + 1;

                if (offset - s_offset <= 0)
                    continue;

                if (line.Substring(s_offset, offset - s_offset).Contains(sub))
                    return foffset - line.Length - 1 + line.Substring(s_offset, offset - s_offset).IndexOf(sub);
            }

            return -1;
        }

        private static string GetIncludeGuard(string file) {
            var fname = file
                .ToUpper()
                .Replace("\\", "__")
                .Replace("/", "__")
                .Replace(":", "_")
                .Replace(' ', '_')
                .Replace('.', '_');

            var ig = $"__GDMAKE_IG_{fname}__";

            return $"#pragma once\n#ifndef {ig}\n#define {ig}\n";
        }

        public List<Hook> Hooks = new List<Hook>();
        public List<DebugMsg> DebugMsgs = new List<DebugMsg>();

        public void GetMacrosAndReplace(string file, string destFile) {
            var oText = File.ReadAllText(file);
            var includesAndUsings = new HashSet<string>();
            var extraIncludes = "";
            var macroCount = 0;

            bool addIncludeGuard = oText.Contains("#pragma once");
            oText = oText.Replace("#pragma once", GetIncludeGuard(file));

            try {
                foreach (var find in new FindItem[] {
                    new FindItem ( @"using.*?;", true, s => s.Contains('"') ? "" : s ),
                    new FindItem ( @"#include [""<].*[>""]", false, s => {
                        if (s.Contains('"')) {
                            s = s.Substring(s.IndexOf('"') + 1);

                            return $"#include \"{Path.Join(Path.GetDirectoryName(destFile), s).Replace("\\", "/")}";
                        } else
                            return s;
                    } ),
                    new FindItem ( @"class \w*", true, s => s + ";" ),
                    new FindItem ( @"struct \w*", true, s => s + ";" ),
                }) {
                    var options = RegexOptions.Compiled;

                    if (find.Item2)
                        options |= RegexOptions.Singleline;
                    
                    var rgx = new Regex (find.Item1, options);

                    foreach (var match in rgx.Matches(oText))
                        includesAndUsings.Add(find.Item3((match as Match).Value));
                }

                foreach (var macro in Macros)
                    if (macro.Replace != null) {
                        int ReplaceForSubstring(int sx, int ex) {
                            macroCount++;
                            var res = macro.Replace(oText.Substring(sx, ex), this);

                            if (res is Hook) {
                                if ((res as Hook).Address == 0) {
                                    Console.WriteLine($"^^ Note: in {Path.GetFileName(file)}");
                                    return res.StringOffset;
                                }

                                this.Hooks.Add(res as Hook);

                                (res as Hook).IncludesAndUsings = includesAndUsings;

                                if (!extraIncludes.Contains("#include <hooks.h>"))
                                    extraIncludes += "#include <hooks.h>\n";

                                var aStringBuilder = new StringBuilder(oText);
                                aStringBuilder.Remove(sx, ex);
                                aStringBuilder.Insert(sx, (res as Hook).HookData);
                                oText = aStringBuilder.ToString();
                            }

                            if (res is DebugMsg) {
                                this.DebugMsgs.Add(res as DebugMsg);

                                if (!extraIncludes.Contains("#include <debug.h>"))
                                    extraIncludes += "#include <debug.h>\n";

                                var aStringBuilder = new StringBuilder(oText);
                                aStringBuilder.Remove(sx, ex);
                                aStringBuilder.Insert(sx, (res as DebugMsg).DbgData);
                                oText = aStringBuilder.ToString();
                            }

                            return res.StringOffset;
                        };
                        
                        while (HasSubstringAndItsNotCommentedOut(oText, macro.Text) != -1) {
                            switch (macro.ReplaceType) {
                                case Macro.EReplaceType.Inside: {
                                    var startIndex = HasSubstringAndItsNotCommentedOut(oText, macro.Text) + macro.Text.Length;
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
                                } break;
                                
                                case Macro.EReplaceType.NextFunction: {
                                    var startIndex = HasSubstringAndItsNotCommentedOut(oText, macro.Text);

                                    int endIndex = oText.IndexOf('{', startIndex) + 1;
                                    int paren = 1;
                                    while (paren > 0)
                                        if (oText.Length > ++endIndex)
                                            switch (oText[endIndex]) {
                                                case '{': paren++; break;
                                                case '}': paren--; break;
                                            }
                                        else break;

                                    ReplaceForSubstring(startIndex, endIndex - startIndex + 1);
                                } break;

                                case Macro.EReplaceType.NoReplace: {
                                    var startIndex = HasSubstringAndItsNotCommentedOut(oText, macro.Text);

                                    int endIndex = oText.IndexOf('{', startIndex) + 1;
                                    int paren = 1;
                                    while (paren > 0)
                                        if (oText.Length > ++endIndex)
                                            switch (oText[endIndex]) {
                                                case '{': paren++; break;
                                                case '}': paren--; break;
                                            }
                                        else break;

                                    ReplaceForSubstring(startIndex, endIndex - startIndex + 1);
                                } break;
                            }
                        }
                    }
            } catch (Exception e) {
                Console.WriteLine($"Unable to process GDMake macros in {file}: {e}");
            }

            oText = extraIncludes + oText;
            
            if (addIncludeGuard)
                oText += "\n#endif\n";

            if (File.Exists(destFile) && !this.ReplaceAllFiles) {
                FileInfo sourceInfo = new FileInfo(file);
                FileInfo targetInfo = new FileInfo(destFile);

                if (sourceInfo.LastWriteTime > targetInfo.LastWriteTime)
                    File.WriteAllText(destFile, oText);
            } else {
                (new FileInfo(destFile)).Directory.Create();
                File.WriteAllText(destFile, oText);
            }

            if (this.Verbose)
                Console.WriteLine($"Processed {macroCount} macros in {Path.GetFileName(file)}");
        }

        public void PreprocessAllFilesInFolder(string path, string resPath, bool replaceAlways = false, bool verbose = false) {
            this.BasePath = path;
            this.ResultPath = resPath;
            this.Verbose = verbose;
            this.ReplaceAllFiles = replaceAlways;

            foreach (var file in Directory
                .EnumerateFiles(path, "*.*", SearchOption.AllDirectories)
                .Where(f => (new string[] { ".cpp", ".c", ".h", ".hpp" }).Any(s => f.EndsWith(s))))
                    this.GetMacrosAndReplace(file, file.Replace(path, resPath));
        }
    }
}
