#pragma once

#include "cli.hpp"

enum TargetCompiler {
    kCompiler_Unknown = -1,
    kCompiler_MSVC = 0,
    kCompiler_Clang
};

ext::string TargetCompiler_toString(TargetCompiler com);

TargetCompiler TargetCompiler_fromString(ext::string const& str);

class Compiler {
    protected:
        ext::vector<TargetCompiler> m_available;
        TargetCompiler m_selected;

    public:
        static Compiler & get();

        bool isInstalled();
        ext::vector<ext::string> listAvailable();

        struct CompileTarget {
            ext::vector<ext::string> m_sources;
            ext::string m_compiler;

            ext::string compile();
        };
};
