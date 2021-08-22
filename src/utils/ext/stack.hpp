#pragma once

#include "string.hpp"
#include "vector.hpp"

namespace ext {
    template<typename T1, typename T2>
    class stack {
        protected:
            ext::vector<T1> column1;
            ext::vector<T2> column2;

        public:
            inline void push(T1 val1, T2 val2) {
                column1.push_back(val1);
                column2.push_back(val2);
            }

            inline void pop() {
                if (column1.size()) {
                    column1.pop_back();
                    column2.pop_back();
                }
            }

            inline size_t size() {
                return column1.size();
            }

            inline bool is_empty() { return !column1.size(); }

            inline T1 value1() { return column1.at(column1.size() - 1); }
            inline T2 value2() { return column2.at(column2.size() - 1); }

            inline ext::vector<T1> get_column1() { return column1; }
            inline ext::vector<T2> get_column2() { return column2; }
    };
}
