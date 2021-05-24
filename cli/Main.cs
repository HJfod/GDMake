using System;
using System.Collections.Generic;
using utils;

namespace gdmake {
    using AHDict = Dictionary<string, ArgHandler>;

    public static class Program {
        static void ShowHelpMessage() {
            Console.WriteLine("Help message or smth");
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
                        ap.GetFlagValue("release") ?? "RelWithDebInfo"
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
                                var res = GDMake.InitializeGlobal();

                                if (res.Failure)
                                    Console.WriteLine($"Error: {(res as ErrorResult).Message}");
                                else
                                    Console.WriteLine("Succesfully initialized GDMake!");
                            }
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
