#pragma once

#define GDMAKE_PROP(__type__, __name__, __setter__)     \
    protected: __type__ m_##__name__;                   \
    public: void set##__setter__(__type__ const& val) { m_##__name__ = val; } \
            __type__ get##__setter__() const { return m_##__name__; }

#define GDMAKE_PROP_CHAIN(__type__, __name__, __setter__, __chain__)     \
    protected: __type__ m_##__name__;                   \
    public: __chain__ & set##__setter__(__type__ const& val) { m_##__name__ = val; return *this; } \
            __type__ get##__setter__() const { return m_##__name__; }

#define GDMAKE_PROP_GET(__type__, __name__, __setter__)     \
    protected: __type__ m_##__name__;                   \
    public: __type__ const& get##__setter__() const { return m_##__name__; }

enum OutputLevel {
    kOutput_Default,
    kOutput_Silent,
    kOutput_Low,
    kOutput_Normal,
    kOutput_Verbose,
};

#include "../utils/utils.hpp"
#include "../utils/cli/cli.hpp"
#include "Project.hpp"
#include "Submodule.hpp"

struct GDMakeError : public std::exception {
    std::string m_err;

    GDMakeError(std::string const& _err) : m_err(_err) {};

	const char * what () const throw () {
    	return m_err.c_str();
    }
};

static constexpr const char* gdmake_not_init_error_msg =
    "GDMake has not been setup! Use `gdmake setup` to setup GDMake";

static const std::string gdmake_submodulePath = "submodules";

class Submodule;

class GDMake {
    protected:
        ext::vector<Submodule> m_submodules;
        OutputLevel m_outputLevel = kOutput_Low;

        GDMake();
        ~GDMake() = default;

    public:
        static GDMake const& get();

        void setup() const;
        bool isSetup() const;

        OutputLevel getOutputLevel() const { return m_outputLevel; };

        ext::vector<Submodule> const& getSubmodules() { return m_submodules; }
        void addSubmodule(Submodule const& sub);
        void removeSubmodule(ext::string const& name);
};
