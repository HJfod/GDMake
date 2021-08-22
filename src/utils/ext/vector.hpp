#pragma once

#include <string>
#include <algorithm>
#include <vector>
#include "../utils.hpp"
#include <iostream>
#include "string.hpp"

namespace ext {
    class string;

    template<class T, class Alloc_T>
    class vector : public std::vector<T, Alloc_T> {
        public:
            inline vector<T, Alloc_T>() : std::vector<T, Alloc_T>() {}
            inline vector<T, Alloc_T>(std::initializer_list<T> list) : std::vector<T, Alloc_T>(list) {}
            inline vector<T, Alloc_T>(std::vector<T, Alloc_T> const& sub) : std::vector<T, Alloc_T>(sub) {}

            inline bool contains(T elem) const {
                return std::find(this->begin(), this->end(), elem) != this->end();
            }
    
            inline void push_sub(ext::vector<T, Alloc_T> const& sub) {
                this->insert(this->end(), sub.begin(), sub.end());
            }

            inline ext::string join(ext::string const& sep) const {
                ext::string res = "";

                if (!std::is_same<T, ext::string>::value)
                    return res;

                for (auto p : *this)
                    res += p + sep;
                
                res = res.substr(0, res.length() - sep.length());

                return res;
            }
            
            template<class T2, class Alloc_T2 = std::allocator<T2>>
            inline ext::vector<T2, Alloc_T2> map(std::function<T2(T t)> func) const {
                ext::vector<T2, Alloc_T2> res;

                for (auto m : *this)
                    res.push_back(func(m));
                
                return res;
            }
            inline ext::vector<T, Alloc_T> filter(std::function<bool(T t)> func) const {
                ext::vector<T, Alloc_T> res;

                for (auto m : *this)
                    if (func(m)) res.push_back(m);
                
                return res;
            }
    };
}
