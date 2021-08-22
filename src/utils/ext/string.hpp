#pragma once

#include <string>
#include <algorithm>
#include "../utils.hpp"
#include <iostream>
#include <functional>
#include "vector.hpp"

// this header is so cursed

namespace ext {
    class string : public std::string {
        public:
            inline string() : std::string() {}
            inline string(std::string const& str) : std::string(str) {}
            inline string(const char* str) : std::string(str) {}
            inline string(char c) : std::string(1, c) {}

            using split_list = std::vector<std::tuple<ext::string, char>>;

            operator int() const {
                if (this->length())
                    return h$(this->c_str());
                
                return 0;
            }

            operator std::string() const {
                return this->asStd();
            }
            
            inline std::string asStd() const { return *this; }

            inline ext::string * toLower_self() {
                std::transform(this->begin(), this->end(), this->begin(), [](unsigned char c){ return std::tolower(c); });

                return this;
            }
            inline ext::string toLower() const {
                auto res = *this;
                return *res.toLower_self();
            }

            inline ext::string * replace_self(ext::string const& orig, ext::string const& repl) {
                std::string::size_type n = 0;

                while ( ( n = this->find( orig, n ) ) != std::string::npos ) {
                    this->std::string::replace( n, orig.size(), repl );
                    n += repl.size();
                }

                return this;
            }
            inline ext::string replace(ext::string const& orig, ext::string const& repl) const {
                auto res = *this;
                return *res.replace_self(orig, repl);
            }

            inline ext::string * remove_line(size_t ix) {
                if (this->count('\n') < ix)
                    return this;
                
                if (ix) {
                    auto st = this->nthIndexOf('\n', ix - 1);
                    auto ed = this->nthIndexOf('\n', ix);

                    *this = this->erase(st + 1, ed - st);
                } else
                    *this = this->erase(0, this->indexOf('\n') + 1);
                
                return this;
            }

            ext::vector<ext::string> split(ext::string const& split) const;
            inline std::vector<char> split() {
                std::vector<char> res;

                for (auto c : *this)
                    res.push_back(c);

                return res;
            }
            split_list split_a(ext::vector<char> const& splitcs) const;
    
            inline bool contains(std::string const& subs) const {
                return this->find(subs) != std::string::npos;
            }
            inline bool contains(char c) const {
                return this->find(c) != std::string::npos;
            }
            inline bool contains(std::vector<ext::string> const& subs) const {
                for (auto const& sub : subs)
                    if (this->contains(sub))
                        return true;

                return false;
            }

            inline size_t count(char _c) {
                size_t res = 0;
                for (auto c : *this)
                    if (c == _c) res++;
                return res;
            }
    
            inline ext::string substr(size_t startIx, size_t count) const {
                return this->std::string::substr(startIx, count);
            }
            inline ext::string substr(size_t startIx) const {
                return this->std::string::substr(startIx);
            }
            inline ext::string substr_s(size_t startIx, ext::string const& c) const {
                return this->substr(startIx, this->indexOf(c) - startIx);
            }
            inline ext::string substr_s(ext::string const& c) const {
                return this->substr(this->indexOf(c));
            }

            inline bool startsWith(ext::string const& str) const {
                return this->_Starts_with(str);
            }
            inline bool endsWith(ext::string const& str) const {
                return this->ends_with(str.asStd());
            }
            inline long long indexOf(ext::string const& str, size_t off = 0u) const {
                auto f = this->find(str, off);
                if (f == std::string::npos) return -1;
                return static_cast<long long>(f);
            }
            inline const size_t nthIndexOf(char c, size_t ix) const {
                size_t f = 0u;
                size_t six = 0u;
                for (auto const& cc : *this) {
                    if (cc == c) {
                        if (f == ix)
                            return six;
                        f++;
                    }
                    six++;
                }
                return std::string::npos;
            }
            inline long long lastIndexOf(ext::string const& str, size_t off = std::string::npos) {
                auto f = this->rfind(str, off);
                if (f == std::string::npos) return -1;
                return static_cast<long long>(f);
            }

            inline ext::string * trim_left_self() {
                this->erase(this->begin(), std::find_if(this->begin(), this->end(), [](unsigned char ch) {
                    return !std::isspace(ch);
                }));
                return this;
            }
            inline ext::string * trim_right_self() {
                this->erase(std::find_if(this->rbegin(), this->rend(), [](unsigned char ch) {
                    return !std::isspace(ch);
                }).base(), this->end());
                return this;
            }
            inline ext::string * trim_self() {
                return this
                    ->trim_left_self()
                    ->trim_right_self();
            }

            inline ext::string trim_left() const {
                auto res = *this;
                return *res.trim_left_self();
            }
            inline ext::string trim_right() const {
                auto res = *this;
                return *res.trim_right_self();
            }
            inline ext::string trim() const {
                auto res = *this;
                return *res.trim_self();
            }
    
            inline ext::string * normalize_self() {
                while (this->contains("  "))
                    this->replace_self("  ", " ");
                return this;
            }
            inline ext::string normalize() {
                auto res = *this;
                return *res.normalize_self();
            }
    
            inline ext::string * remove_range_self(
                ext::string const& start,
                ext::string const& end,
                bool remove_trail = true
            ) {
                while (
                    this->contains(start) &&
                    this->contains(end) &&
                    this->indexOf(start) < this->indexOf(end, this->indexOf(start))
                ) {
                    this->erase(
                        this->indexOf(start),
                        this->indexOf(end, this->indexOf(start)) +
                            (remove_trail ? end.length() : 0) - this->indexOf(start)
                    );
                }

                return this;
            }
    };
}

inline ext::string operator"" _exs(const char* txt, size_t) {
    return txt;
}
