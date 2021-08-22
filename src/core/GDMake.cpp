#include "GDMake.hpp"
#include "../utils/Console.hpp"
#include "../include/json.hpp"
#include <thread>

GDMake* g_gdmake;

const ext::vector<Submodule> g_defaultSubmodules {
    Submodule()
        .setName("gd.h")
        .setHeader("gd.h")
        .setRepo("HJfod/gd.h")
        .setType(kSubType_HeaderOnly)
        .setDefault(true),

    Submodule()
        .setName("Cocos2d")
        .setHeader("cocos2d.h")
        .setRepo("HJfod/cocos-headers")
        .setType(kSubType_HeaderOnly)
        .setLibs({
            "submodules/Cocos2d/cocos2dx/libcocos2d.lib",
            "submodules/Cocos2d/extensions/libExtensions.lib"
        })
        .setIncludePaths({
            "submodules/Cocos2d/cocos2dx",
            "submodules/Cocos2d/cocos2dx/include",
            "submodules/Cocos2d/cocos2dx/kazmath/include",
            "submodules/Cocos2d/cocos2dx/platform/third_party/win32/OGLES",
            "submodules/Cocos2d/cocos2dx/platform/win32",
            "submodules/Cocos2d/extensions"
        })
        .setDefault(true),
    
    Submodule()
        .setName("MinHook")
        .setHeader("MinHook.h")
        .setRepo("HJfod/MinHook")
        .setType(kSubType_CompiledLib)
        .setLibs({ "libs/minhook.x32.lib" })
        .setDefault(true),
};


GDMake::GDMake() {
    this->m_submodules.push_sub(g_defaultSubmodules);
}

void GDMake::addSubmodule(Submodule const& sub) {
    for (auto mod : m_submodules)
        if (mod.getName() == sub.getName())
            return;
    
    m_submodules.push_back(sub);
}

void GDMake::removeSubmodule(ext::string const& name) {
    auto ix = 0u;
    for (auto sub : m_submodules)
        if (sub.getName() == name)
            m_submodules.erase(m_submodules.begin() + ix);
        else
            ix++;
}

GDMake const& GDMake::get() {
    if (!g_gdmake)
        g_gdmake = new GDMake();
    
    return *g_gdmake;
}

void GDMake::setup() const {
    auto io = io::Console::get();
    
    io << io::Lime << " == Setting up GDMake == \n" << io::White;

    io << "Directory: " << std::filesystem::current_path().string() << "\n";

    ////////////////////////////////////

    io << io::Cyan << ">> Checking tools...\n";
    
    io << io::White << "git      .. ";
    if (!git::isInstalled())
        throw GDMakeError("Git has not been installed!");
    io << io::Lime << "✓\n";

    io << io::White << "cmake    .. ";
    if (!cmake::isInstalled())
        throw GDMakeError("CMake has not been installed!");
    io << io::Lime << "✓\n";

    io << io::White << "compiler .. ";
    if (!Compiler::get().isInstalled())
        throw GDMakeError("You don't have a compiler installed!");
    else
        io << io::Lime << "✓ " << io::Yellow << "(" << Compiler::get().listAvailable().join(", ") << ")\n" << io::White;

    ////////////////////////////////////

    io << io::Cyan << ">> Creating folders...\n" << io::White;

    for (auto const& dir : std::initializer_list<const char*> {
        "submodules",
        "include",
        "libs",
        "dlls",
    })
        if (!std::filesystem::exists(dir))
            std::filesystem::create_directory(dir);
    
    io << "Created\n";

    ////////////////////////////////////

    io << io::Cyan << ">> Downloading submodules\n" << io::White;

    for (auto const& sub : this->m_submodules) {
        io << io::Yellow << sub.getName() << io::White << " .. ";

        if (sub.isInstalled())
            io << io::Lime << "already installed ✓\n" << io::White;
        else {
            auto stop = new bool(false);
            io.showLoad(stop);

            auto res = sub.install();

            *stop = true;
            if (res.success)
                io << io::Lime << "\b✓\n" << io::White;
            else
                io << io::Red << "\b✗ " << res.error << io::White << "\n";
        }
    }

    ////////////////////////////////////

    io << io::Cyan << ">> Compiling Submodules\n" << io::White;

    for (auto const& sub : this->m_submodules)
        if (sub.getType() == kSubType_CompiledLib) {
            io << io::Yellow << sub.getName() << io::White << " .. ";

            if (sub.isCompiled())
                io << io::Lime << "already compiled ✓\n";
            else {
                auto stop = new bool(false);
                io.showLoad(stop);

                auto res = sub.compile();

                if (res.success)
                    io << io::Lime << "\b✓\n" << io::White;
                else
                    io << io::Red << "\b✗ " << res.error << io::White << "\n";

                *stop = true;
            }
        }

    ////////////////////////////////////
    
    io << io::Lime << " == Succesfully setup ✓ == \n";
}

bool GDMake::isSetup() const {
    return false;
}
