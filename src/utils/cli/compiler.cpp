#include "compiler.hpp"

Compiler* g_compiler;

ext::string TargetCompiler_toString(TargetCompiler com) {
    switch (com) {
        case kCompiler_MSVC: return "MSBuild";
        case kCompiler_Clang: return "Clang";
        default: return "Unknown";
    }
}

TargetCompiler TargetCompiler_fromString(ext::string const& str) {
    switch (str.toLower().trim()) {
        case h$("msvc"): return kCompiler_MSVC;
        case h$("clang"): return kCompiler_Clang;
        default: return kCompiler_Unknown;
    }
}

Compiler & Compiler::get() {
    if (!g_compiler)
        g_compiler = new Compiler();
    
    return *g_compiler;
}

ext::vector<ext::string> Compiler::listAvailable() {
    return m_available.map<ext::string>([](auto t) -> ext::string { return TargetCompiler_toString(t); });
}

bool Compiler::isInstalled() {
    if (cli::exec("msbuild").startsWith("Microsoft (R) Build Engine"))
        m_available.push_back(kCompiler_MSVC);
        
    if (cli::exec("clang").startsWith("clang: error: no input files"))
        m_available.push_back(kCompiler_Clang);

    return m_available.size();
}

ext::string Compiler::CompileTarget::compile() {
    return "";
}
