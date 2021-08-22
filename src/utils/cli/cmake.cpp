#include "cmake.hpp"

bool cmake::isInstalled() {
    return cli::exec("cmake").startsWith("Usage");
}
