#pragma once

#include "../utils.hpp"

namespace cli {
    ext::string exec(ext::string const& cmd);
}

#include "git.hpp"
#include "cmake.hpp"
#include "compiler.hpp"
