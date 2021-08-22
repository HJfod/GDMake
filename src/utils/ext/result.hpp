#pragma once

namespace ext {
    template<class Res_T = bool>
    struct result {
        bool success;
        Res_T data;
        const char* error;

        static result res() {
            return { true, Res_T(), "" };
        }
        static result res(Res_T _data) {
            return { true, _data, "" };
        }
        static result err(const char* text) {
            return { false, Res_T(), text };
        }
        static result err(std::string const& text) {
            return { false, Res_T(), text.c_str() };
        }
    };
}
