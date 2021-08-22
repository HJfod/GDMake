#include "Submodule.hpp"

bool Submodule::isInstalled() const {
    return std::filesystem::exists(this->getSubmodulePath() + "/.git");
}

bool Submodule::isCompiled() const {
    for (auto const& lib : m_libs)
        if (!std::filesystem::exists(lib.asStd()))
            return false;
    
    return true;
}

ext::result<> Submodule::compile(OutputLevel lvl) const {
    if (lvl == kOutput_Default)
        lvl = GDMake::get().getOutputLevel();
    
    if (!Compiler::get().isInstalled())
        return ext::result<>::err("A supported C++ compiler is not installed!");
    
    if (!this->isInstalled())
        return ext::result<>::err("Submodule has not been downloaded!");
    
    Compiler::CompileTarget t;
    t.m_sources = this->m_sources;

    auto res = t.compile();
    if (res)
        return ext::result<>::err("Error compiling: " + res);
    
    return ext::result<>::res();
}

ext::result<> Submodule::install(OutputLevel lvl) const {
    if (lvl == kOutput_Default)
        lvl = GDMake::get().getOutputLevel();
    
    if (!git::isInstalled())
        return ext::result<>::err("Git is not installed!");
    
    if (!git::repoExists(this->m_url))
        return ext::result<>::err("Repository \"" + m_url + "\" does not exist!");
    
    if (!git::cloneRepo(this->m_url, this->getSubmodulePath(), git::fClone_Recursive))
        return ext::result<>::err("Error cloning repository!");
    
    return ext::result<>::res();
}
