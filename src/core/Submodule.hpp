#pragma once

#include "GDMake.hpp"

enum SubmoduleType {
    kSubType_IncludeSource,
    kSubType_HeaderOnly,
    kSubType_CompiledLib,
};

class Submodule {
    protected:
        GDMAKE_PROP_CHAIN(ext::string, url, URL, Submodule)
        GDMAKE_PROP_CHAIN(ext::string, name, Name, Submodule)
        GDMAKE_PROP_CHAIN(ext::string, header, Header, Submodule)
        GDMAKE_PROP_CHAIN(ext::string, cmakeDefs, CMakeDefs, Submodule)
        GDMAKE_PROP_CHAIN(ext::string, compilerOptions, CompilerOpts, Submodule)
        GDMAKE_PROP_CHAIN(ext::vector<ext::string>, libs, Libs, Submodule)
        GDMAKE_PROP_CHAIN(ext::vector<ext::string>, sources, Sources, Submodule)
        GDMAKE_PROP_CHAIN(ext::vector<ext::string>, includePaths, IncludePaths, Submodule)
        GDMAKE_PROP_CHAIN(bool, isDefault, Default, Submodule)
        GDMAKE_PROP_CHAIN(SubmoduleType, type, Type, Submodule)
    
    public:
        Submodule() = default;
        ~Submodule() = default;

        Submodule & setRepo(ext::string const& repo) {
            return this->setURL("https://github.com/" + repo);
        }
        ext::string getSubmodulePath() const {
            return gdmake_submodulePath + "/" + this->m_name;
        }

        ext::result<> install(OutputLevel = kOutput_Default) const;
        ext::result<> compile(OutputLevel = kOutput_Default) const;

        bool isInstalled() const;
        bool isCompiled() const;
};
