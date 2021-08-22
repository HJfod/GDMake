#include "Project.hpp"

Project::Project() {
    if (!GDMake::get().isSetup())
        throw GDMakeError(gdmake_not_init_error_msg);
}

Project & Project::setDir(ext::string const& dir) {
    this->m_path = dir;

    return *this;
}

Project & Project::setName(ext::string const& name) {
    this->m_name = name;

    return *this;
}

Project & Project::save() {
    return *this;
}

Project & Project::load() {
    return *this;
}

