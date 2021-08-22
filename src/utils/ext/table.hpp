#pragma once

#include "string.hpp"
#include "vector.hpp"

namespace ext {
    template<typename T, size_t C>
    class static_width_table {
        protected:
            size_t rowCount = 0u;

            ext::vector<std::array<T, C>> data;
        public:
            void addRow(std::array<T, C> const& row) {
                data.push_back(row);
                rowCount++;
            }

            void removeRow(size_t index) {
                if (index < rowCount) {
                    data.erase(data.begin() + index);
                    rowCount--;
                }
            }
            
            void insert(size_t column, size_t row, T _data) {
                if (column < C && row < rowCount)
                    data.at(column).at(row) = _data;
            }
    };
}
