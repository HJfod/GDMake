using System;
using System.Linq;
using System.Collections.Generic;
using utils;

namespace gdmake {
    using AHDict = Dictionary<string, ArgHandler>;

    public static class Program {
        public enum TType {
            tParam, tFlag, tFlagWValue
        }

        public class Doc {
            public class Param {

                public string Name;
                public bool Optional;
                public TType Type = TType.tParam;
                public string Description;

                public Param(string name, bool opt, string desc) {
                    this.Name = name;
                    this.Optional = opt;
                    this.Description = desc;
                }

                public Param(string name, bool opt, TType type, string desc) {
                    this.Name = name;
                    this.Optional = opt;
                    this.Description = desc;
                    this.Type = type;
                }
            }

            public Param[] Params;
            public string Description;

            public string GenerateCommandParamText() {
                var str = "";

                foreach (var param in Params) {
                    str += param.Optional ? '[' : '<';

                    if (param.Type != TType.tParam)
                        str += '-';
                    
                    str += param.Name;

                    if (param.Type == TType.tFlagWValue)
                        str += $" <value>";

                    str += param.Optional ? ']' : '>';
                    str += ' ';
                }

                str = str.Substring(0, str.Length - 1);

                return str;
            }

            public Doc(string desc, Param[] pms) {
                this.Description = desc;
                this.Params = pms;
            }
        }

        public static readonly Dictionary<string, Doc> Docs = new Dictionary<string, Doc> {
            { "setup", new Doc(
                "Setup GDMake",
                new Doc.Param[] { new Doc.Param( "re", true, TType.tFlag, "Reinitialize from scratch" ) }
            ) },
            { "init", new Doc(
                "Initialize a new GDMake project",
                new Doc.Param[] {
                    new Doc.Param( "name", true, "Name of the project" ),
                    new Doc.Param( "directory", true, "Directory to initialize in" ),
                    new Doc.Param( "name", true, TType.tFlagWValue, "Name of the project" ),
                    new Doc.Param( "dir", true, TType.tFlagWValue, "Directory to initialize in" ),
                    new Doc.Param( "mkdir", true, TType.tFlag, "Create directory if it does not exist" ),
                    new Doc.Param( "no-example", true, TType.tFlag, "Don't create entry .cpp file" ),
                }
            ) },
            { "default", new Doc(
                "Build & run a GDMake project",
                new Doc.Param[] {
                    new Doc.Param( "path", true, "Path to the project directory" ),
                    new Doc.Param( "path", true, TType.tFlagWValue, "Path to the project directory" ),
                    new Doc.Param( "outlvl", true, TType.tFlagWValue,
                        "Compiler output level; One of [silent|quiet|minimal|normal|detailed|dianostic]" ),
                    new Doc.Param( "config", true, TType.tFlagWValue,
                        "Output configuration; One of [Release|RelWithDebInfo]" ),
                }
            ) },
            { "generate", new Doc(
                "Generate the files for a project",
                new Doc.Param[] {
                    new Doc.Param( "path", true, "Path to the project directory" ),
                    new Doc.Param( "path", true, TType.tFlagWValue, "Path to the project directory" ),
                    new Doc.Param( "re", true, TType.tFlag, "Regenerate from scratch" ),
                }
            ) },
            { "build", new Doc(
                "Generate & build project",
                new Doc.Param[] {
                    new Doc.Param( "path", true, "Path to the project directory" ),
                    new Doc.Param( "path", true, TType.tFlagWValue, "Path to the project directory" ),
                    new Doc.Param( "re", true, TType.tFlag, "Regenerate from scratch" ),
                }
            ) },
            { "run", new Doc(
                "Run project",
                new Doc.Param[] {
                    new Doc.Param( "path", true, "Path to the project directory" ),
                    new Doc.Param( "path", true, TType.tFlagWValue, "Path to the project directory" ),
                }
            ) },
            { "help", new Doc(
                "More information about a command",
                new Doc.Param[] {
                    new Doc.Param( "command", false, "Command name" ),
                }
            ) },
        };

        public static void ShowHelpMessage() {
            Console.WriteLine(
@"GDMake Help

For further help, contact HJfod#1795 on Discord or check https://github.com/HJfod/GDMake

Commands (Use help <command> for extra information):"
            );

            foreach (var doc in Docs)
                Console.WriteLine(
                    $"{doc.Value.Description}{new String(' ', 40 - doc.Value.Description.Length)}" +
                    $"{(doc.Key == "default" ? "" : doc.Key + " ")}{doc.Value.GenerateCommandParamText()}"
                );
        }

        public static void ShowHelpForCommand(string cmd) {
            if (Docs.ContainsKey(cmd)) {
                var doc = Docs.GetValueOrDefault(cmd, null);

                if (doc != null) {
                    Console.WriteLine($"{cmd} {doc.GenerateCommandParamText()}\n\n{doc.Description}\n");

                    foreach (var d in doc.Params) {
                        var name = d.Name;

                        if (d.Type != TType.tParam) name = '-' + name;
                        if (d.Type == TType.tFlagWValue) name += " <value>";

                        Console.WriteLine(
                            $"{name}{new String(' ', 25 - name.Length)}" +
                            d.Description
                        );
                    }
                } else
                    Console.WriteLine($"Unable to get information for {cmd} :(");
            } else
                Console.WriteLine($"No information for {cmd} found :( (Use \"default\" for build & run)");
        }

        static void DefaultHandlerProjectRun(ArgParser ap, List<string> args) {
            var project = new Project();

            project.Dir = ap.GetFlagOrArg("dir", args, 0);

            var res = project.Load();

            if (res.Failure)
                Console.WriteLine($"Error: {(res as ErrorResult).Message}");
            else {
                res = project.Generate();

                if (res.Failure)
                    Console.WriteLine($"Error: {(res as ErrorResult).Message}");
                else {
                    res = project.Build(
                        ap.GetFlagValue("outlvl") ?? "silent",
                        ap.GetFlagValue("config") ?? "RelWithDebInfo"
                    );

                    if (res.Failure)
                        Console.WriteLine($"Error: {(res as ErrorResult).Message}");
                    else {
                        res = project.Run();

                        if (res.Failure)
                            Console.WriteLine($"Error: {(res as ErrorResult).Message}");
                    }
                }
            }
        }

        [STAThread]
        static void Main(string[] allArgs) {
            var ap = new ArgParser();
            var res = ap.Parse(allArgs);

            if (res.Success) {
                ap.RunDict(new ArgHandler (
                    new AHDict {
                        { "init", new ArgHandler (null, args => {
                            if (!GDMake.IsGlobalInitialized()) {
                                GDMake.ShowGlobalNotInitializedError();
                                return;
                            }

                            var project = new Project();

                            project.Name = ap.GetFlagOrArg("name", args, 0);
                            project.Dir = ap.GetFlagOrArg("dir", args, 1);

                            var res = project.Save(ap.HasFlag("mkdir"), !ap.HasFlag("no-example"));

                            if (res.Failure)
                                Console.WriteLine($"Error initializing: {(res as ErrorResult<string>).Message}");
                            else
                                Console.WriteLine(res.Data);
                        })},
                        
                        { "run", new ArgHandler (null, args => {
                            if (!GDMake.IsGlobalInitialized()) {
                                GDMake.ShowGlobalNotInitializedError();
                                return;
                            }

                            var project = new Project();

                            project.Dir = ap.GetFlagOrArg("dir", args, 0);

                            var res = project.Load();

                            if (res.Failure)
                                Console.WriteLine($"Error: {(res as ErrorResult).Message}");
                            else
                                project.Run();
                        })},
                 
                        { "generate", new ArgHandler (null, args => {
                            if (!GDMake.IsGlobalInitialized()) {
                                GDMake.ShowGlobalNotInitializedError();
                                return;
                            }

                            var project = new Project();

                            project.Dir = ap.GetFlagOrArg("dir", args, 0);

                            var res = project.Load();

                            if (res.Failure)
                                Console.WriteLine($"Error: {(res as ErrorResult).Message}");
                            else {
                                res = project.Generate(ap.HasFlag("re"));
                                
                                if (res.Failure)
                                    Console.WriteLine($"Error: {(res as ErrorResult).Message}");
                                else
                                    Console.WriteLine("Generated files!");
                            }
                        })},

                        { "build", new ArgHandler (null, args => {
                            if (!GDMake.IsGlobalInitialized()) {
                                GDMake.ShowGlobalNotInitializedError();
                                return;
                            }

                            var project = new Project();

                            project.Dir = ap.GetFlagOrArg("dir", args, 0);

                            var res = project.Load();

                            if (res.Failure)
                                Console.WriteLine($"Error: {(res as ErrorResult).Message}");
                            else {
                                res = project.Generate(ap.HasFlag("re"));

                                if (res.Failure)
                                    Console.WriteLine($"Error: {(res as ErrorResult).Message}");
                                
                                res = project.Build();

                                if (res.Failure)
                                    Console.WriteLine($"Error: {(res as ErrorResult).Message}");
                                else
                                    Console.WriteLine("Build finished!");
                            }
                        })},
                    
                        { "setup", new ArgHandler (null, args => {
                            if (args.Count > 0)
                                Console.WriteLine("Setup does not take any arguments!");
                            else {
                                if (ap.HasFlag("gdpath")) {
                                    if (!GDMake.IsGlobalInitialized()) {
                                        GDMake.ShowGlobalNotInitializedError();
                                        return;
                                    }

                                    GDMake.LoadSettings();
                                    
                                    if (ap.GetFlagValue("gdpath") != null) {
                                        GDMake.SettingsFile.GDPath = ap.GetFlagValue("gdpath");

                                        Console.WriteLine("Updated GD Path!");

                                        return;
                                    }

                                    Console.WriteLine(GDMake.SettingsFile.GDPath);

                                    return;
                                }

                                var res = GDMake.InitializeGlobal(ap.HasFlag("re"));

                                if (res.Failure)
                                    Console.WriteLine($"Error: {(res as ErrorResult).Message}");
                                else
                                    Console.WriteLine("Succesfully initialized GDMake!");
                            }
                        })},
                    
                        { "submodules", new ArgHandler (new AHDict {
                            { "list", new ArgHandler(null, args => {
                                if (!GDMake.IsGlobalInitialized()) {
                                    GDMake.ShowGlobalNotInitializedError();
                                    return;
                                }

                                foreach (var sub in GDMake.Submodules) {
                                    var name = sub.Name;

                                    if (GDMake.DefaultSubmodules.Any(s => s.Name == sub.Name))
                                        name += " (default)";

                                    Console.WriteLine($"{name}{new String(' ', 25 - name.Length)}{sub.URL}");
                                }
                            })},
                            
                            { "add", new ArgHandler(null, args => {
                                if (!GDMake.IsGlobalInitialized()) {
                                    GDMake.ShowGlobalNotInitializedError();
                                    return;
                                }

                                if (args.Count < 2)
                                    Console.WriteLine("Usage: submodules add <name> <url>");
                                else {
                                    var sub = new GDMake.Submodule(
                                        args[0],
                                        args[1],
                                        !ap.HasFlag("header-only"),
                                        ap.GetFlagValue("cmake")
                                    );

                                    GDMake.AddSubmodule(sub);

                                    if (!ap.HasFlag("header-only"))
                                        GDMake.CompileLibs();
                                    
                                    Console.WriteLine("Added submodule!");
                                }
                            })},

                            { "rm", new ArgHandler(null, args => {
                                if (!GDMake.IsGlobalInitialized()) {
                                    GDMake.ShowGlobalNotInitializedError();
                                    return;
                                }

                                if (args.Count < 1)
                                    Console.WriteLine("Usage: submodules rm <name>");
                                else {
                                    var res = GDMake.RemoveSubmodule(args[0]);

                                    if (res.Failure)
                                        Console.WriteLine($"Error: {(res as ErrorResult).Message}");
                                    else
                                        Console.WriteLine("Removed submodule!");
                                }
                            })},
                        }, args => {
                            ShowHelpForCommand("submodules");
                        })},

                        { "help", new ArgHandler (null, args => {
                            if (args.Count == 0)
                                ShowHelpMessage();
                            else
                                ShowHelpForCommand(args[0]);
                        })},
                    },
                    args => {
                        if (args.Count == 0)
                            ShowHelpMessage();
                        else
                            DefaultHandlerProjectRun(ap, args);
                    }
                ));

            } else
                Console.WriteLine("Error: " + (res as ErrorResult).Message);
        }
    }
}
