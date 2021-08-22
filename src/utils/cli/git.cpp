#include "git.hpp"

bool git::isInstalled() {
    return cli::exec("git").startsWith("usage: ");
}

bool git::repoExists(ext::string const& url) {
    return !cli::exec("git ls-remote " + url).contains("Repository not found.");
}

bool git::cloneRepo(ext::string const& url, ext::string const& dest, CloneFlags flags, ext::string * output) {
    ext::string cmd = "git clone ";

    cmd += url.trim() + ' ';
    cmd += dest.trim();

    if (flags & fClone_Recursive)
        cmd += " --recursive";
    
    auto out = cli::exec(cmd);
    if (output) *output = out;
    
    return std::filesystem::exists(dest.asStd());
}
