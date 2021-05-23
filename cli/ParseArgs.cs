using System;
using System.Linq;
using System.Collections.Generic;
using utils;

namespace gdmake {
    using AHDict = Dictionary<string, ArgHandler>;
    using AHFunc = Action<List<string>>;

    public class Flag {
        public string Name { get; internal set; }
        public string Value { get; internal set; } = null;

        public Flag(string name, string val) {
            this.Name = name;
            this.Value = val;
        }
    }

    public class ArgHandler {
        public AHDict SubHandlers { get; internal set; }
        public AHFunc DefaultHandler { get; internal set; }

        public ArgHandler(AHDict dict = null, AHFunc handler = null) {
            this.SubHandlers = dict;
            this.DefaultHandler = handler;
        }
    }

    public class ArgParser {
        public List<string> Args { get; internal set; } = new List<string>();
        public List<Flag> Flags { get; internal set; } = new List<Flag>();

        public void RunDict(ArgHandler handlers, int arg = 0) {
            if (this.Args.Count > arg)
                if (handlers.SubHandlers != null)
                    foreach (var h in handlers.SubHandlers)
                        if (h.Key == this.Args[arg]) {
                            RunDict(h.Value, arg + 1);
                            return;
                        }

            handlers.DefaultHandler?.Invoke(this.Args.Skip(arg).ToList());
        }

        public string GetFlagOrArg(string fname, List<string> args = null, int arg = -1) {
            return GetFlagValue(fname) ??
                ((arg != -1 && args.Count > arg) ? args[arg] : null);
        }

        public bool HasFlag(string name) {
            foreach (var flag in this.Flags)
                if (flag.Name == name)
                    return true;
            
            return false;
        }

        public Flag GetFlag(string name) {
            foreach (var flag in this.Flags)
                if (flag.Name == name)
                    return flag;
            
            return null;
        }

        public string GetFlagValue(string name) {
            return GetFlag(name)?.Value;
        }

        public Result Parse(string[] args) {
            Flag last = null;

            foreach (var arg in args)
                if (arg.StartsWith('-')) {
                    var name = arg;

                    while (name.StartsWith('-'))
                        name = name.Substring(1);

                    if (name.Length == 0)
                        return new ErrorResult("Flag without a name provided!");

                    if (last != null)
                        this.Flags.Add(last);

                    last = new Flag(name, null);
                } else {
                    if (last != null) {
                        last.Value = arg;

                        this.Flags.Add(last);

                        last = null;
                    } else
                        this.Args.Add(arg);
                }

            if (last != null)
                this.Flags.Add(last);

            return new SuccessResult();
        }
    }
}
