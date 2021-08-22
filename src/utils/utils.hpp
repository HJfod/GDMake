#pragma once

// #define DEBUG

static constexpr unsigned int h$(const char* str, int h = 0) {
    return !str[h] ? 5381 : (h$(str, h+1) * 33) ^ str[h];
}

#include "ext/ext.hpp"
