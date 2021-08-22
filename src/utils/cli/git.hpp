#pragma once

#include "cli.hpp"

namespace git {
    enum CloneFlags {
        fClone_None          = 0x0,
        fClone_Recursive     = 0x1,
    };

    bool isInstalled();
    bool repoExists(ext::string const& url);
    bool cloneRepo(
        ext::string const& url,
        ext::string const& dest,
        CloneFlags = fClone_None,
        ext::string * output = nullptr
    );
}
