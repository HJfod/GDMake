#include "cli.hpp"
#include <array>

#define popen _popen
#define pclose _pclose

ext::string cli::exec(ext::string const& rcmd) {
    auto cmd = rcmd + " 2>&1";
    std::array<char, 128> buffer;
    std::string result;
    std::unique_ptr<FILE, decltype(&pclose)> pipe(popen(cmd.c_str(), "r"), pclose);
    if (!pipe)
        throw std::runtime_error("popen() failed!");

    while (fgets(buffer.data(), static_cast<int>(buffer.size()), pipe.get()) != nullptr)
        result += buffer.data();
        
    return result;
}
