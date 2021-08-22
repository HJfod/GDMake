#include "utils/ArgParser.hpp"
#include "utils/CppParser.hpp"
#include "utils/Console.hpp"
#include "core/GDMake.hpp"

#define CATCH_GDMAKE_ERRORS catch (GDMakeError & e) {                       \
    io << io::Red << "GDMake error: ";                                      \
    bool ocol = false;                                                      \
    for (auto c : std::string(e.what())) {                                  \
        if (c == '`') {                                                     \
            ocol = !ocol;                                                   \
            if (ocol) io << io::Yellow << "`";                              \
            else io << "`" << io::Red;                                      \
        }                                                                   \
        else io << c;                                                       \
    }                                                                       \
    io << io::White << "\n";                                                \
} catch (std::runtime_error & e) {                                          \
    io << io::Red << "Runtime error: " << e.what() << io::White << "\n";    \
} catch (...) {                                                             \
    io << io::Red << "Unknown error\n" << io::White;                        \
}

using namespace args;

int main(int ac, char* av[]) {
    auto io = io::Console::initShared(std::cin, std::cout);

    auto ap = ArgParser::init();

    ap.parse(ac, av);

    #define ARG_PARSER ap

    ARG_SWITCH {
        ARG("setup") {
            try {
                GDMake::get()
                    .setup();
            } CATCH_GDMAKE_ERRORS;
        } break;

        ARG("test_file") {
            if (ap.hasArg(1))
                CppParser().parse(ap.argAt(1));
        } break;

        ARG("test") ARG("test_dir") {
            if (ap.hasArg(1)) {
                auto res = ext::dir::iterate_dir(ap.argAt(1), { ".cpp", ".hpp", ".c", ".h" });

                if (res.success)
                    for (auto const& file : res.data)
                        CppParser().parse(file);
                else
                    io << io::Red << res.error << io::White << "\n";
            }
        } break;

        DEFAULT
            if (ap.hasArg(0)) {
                try {
                    Project()
                        .setDir(ap.argAt(0))
                        .setName(ap.argAt(1))
                        .save();
                } CATCH_GDMAKE_ERRORS;

                break;
            } // fallthrough to help

        ARG("help") {
            io << "Help for GDMake\n";
        } break;
    }

    return 0;
}
