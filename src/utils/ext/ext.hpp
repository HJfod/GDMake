#pragma once

#include <string>

namespace ext {
    class string;
    template<typename T, typename Alloc_T = std::allocator<T>>
    class vector;
    template<class DataT = ext::string>
    class file;
    class dir;
    template<typename T1, typename T2>
    class stack;
}

#include "result.hpp"
#include "vector.hpp"
#include "string.hpp"
#include "file.hpp"
#include "stack.hpp"
#include "table.hpp"
