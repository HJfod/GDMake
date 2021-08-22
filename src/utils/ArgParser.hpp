#pragma once

#include <string>
#include <vector>
#include <functional>
#include "utils.hpp"

#define HSH(ix) h$(ARG_PARSER.argAt(ix).toLower().c_str())
#define ARG(text) case h$(text):
#define DEFAULT default:
#define SUB_SWITCH(ix, arg) ARG(arg) switch (HSH(ix))
#define ARG_SWITCH switch (HSH(0))

namespace args {
    enum ArgParseError {
        OK = 0,
    };

    class ArgParser {
        public:
            struct Flag {
                ext::string name;
                ext::string value = "";

                Flag(ext::string const& _name) {
                    this->name = _name;
                    while (this->name.startsWith('-'))
                        this->name = this->name.substr(1);
                }

                Flag(ext::string const& _name, ext::string const& _val) {
                    this->name = _name;
                    while (this->name.startsWith('-'))
                        this->name = this->name.substr(1);
                    
                    this->value = _val;
                }
            };

        protected:
            std::vector<ext::string> m_args;
            std::vector<Flag> m_flags;

        public:
            ArgParser() = default;
            ~ArgParser() = default;

            static ArgParser & get();
            static ArgParser & init();

            ArgParseError parse(int ac, char* av[]);

            bool hasFlag(ext::string const& name);
            Flag * getFlag(ext::string const& name);
            ext::string getFlagValue(ext::string const& name);
            std::vector<Flag> & getFlags();

            std::vector<ext::string> & getArgs();
            ext::string argAt(size_t index);
            bool hasArg(size_t index);
    };
}
