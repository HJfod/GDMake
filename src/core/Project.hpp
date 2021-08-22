#pragma once

#include "GDMake.hpp"

class Project {
    protected:
        ext::string m_path;
        ext::string m_name;

    public:
        Project();
        ~Project() = default;

        Project & setDir(ext::string const& path);
        Project & setName(ext::string const& name);

        Project & save();
        Project & load();
};
