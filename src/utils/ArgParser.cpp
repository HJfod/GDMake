#include "ArgParser.hpp"

using namespace args;

ArgParser* g_parser;

ArgParseError ArgParser::parse(int ac, char* av[]) {
    ext::string lastFlag = "";
    for (auto i = 1; i < ac; i++) {
        ext::string s = av[i];

        if (s.contains('-'))
            if (s.count('-') > 1)
                if (lastFlag) {
                    this->m_flags.push_back({ lastFlag });
                    lastFlag = "";
                } else
                    lastFlag = s;
            else
                for (auto c : s.substr(1).split())
                    this->m_flags.push_back({ ext::string(c) });
        else
            if (lastFlag) {
                this->m_flags.push_back({ lastFlag, s });
                lastFlag = "";
            } else
                this->m_args.push_back({ s });
    }
    if (lastFlag)
        this->m_flags.push_back({ lastFlag });
    
    return ArgParseError::OK;
}

bool ArgParser::hasFlag(ext::string const& fname) {
    for (auto flag : this->m_flags)
        if (flag.name == fname)
            return true;
    
    return false;
}

ArgParser::Flag * ArgParser::getFlag(ext::string const& fname) {
    for (auto & flag : this->m_flags)
        if (flag.name == fname)
            return &flag;
    
    return nullptr;
}

ext::string ArgParser::getFlagValue(ext::string const& fname) {
    auto flag = getFlag(fname);
    if (flag) return flag->value;
    return "";
}

std::vector<ArgParser::Flag> & ArgParser::getFlags() {
    return this->m_flags;
}

std::vector<ext::string> & ArgParser::getArgs() {
    return this->m_args;
}

ext::string ArgParser::argAt(size_t index) {
    if (this->m_args.size() <= index)
        return "";

    return this->m_args.at(index);
}

bool ArgParser::hasArg(size_t index) {
    return this->m_args.size() > index;
}

ArgParser & ArgParser::init() {
    g_parser = new ArgParser;
    return *g_parser;
}

ArgParser & ArgParser::get() {
    return *g_parser;
}
