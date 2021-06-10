using System.IO;
using System.Reflection;

namespace gdmake {
    static class DefaultStrings {
        public const string HeaderCredit = "// generated through GDMake https://github.com/HJfod/GDMake\n";
        public const string HeaderCreditH = "# generated through GDMake https://github.com/HJfod/GDMake\n";

        public const string DllMain =
DefaultStrings.HeaderCredit +
@"
<<?CONSOLE>>#include ""<<GDMAKE_DIR>>/src/console.h""
#include ""mod.h""

DWORD WINAPI load_thread(LPVOID hModule) {
    if (gd::init()) {
        
        <<?CONSOLE>>if (!gdmake::console::load()) {
        <<?CONSOLE>>    MessageBoxA(nullptr, ""Unable to hook up debugging console!"", ""<<MOD_NAME>>"", MB_ICONERROR);
        <<?CONSOLE>>    FreeLibraryAndExitThread((HMODULE)hModule, 0);
        <<?CONSOLE>>    return 1;
        <<?CONSOLE>>}

        if (mod::load((HMODULE)hModule)) {
            <<?CONSOLE>>gdmake::console::awaitUnload();
        } else {
            MessageBoxA(nullptr, ""Unable to set up hooks!"", ""<<MOD_NAME>>"", MB_ICONERROR);
            <<?CONSOLE>>gdmake::console::unload();
            FreeLibraryAndExitThread((HMODULE)hModule, 0);
        }
    } else {
        MessageBoxA(nullptr, ""Unable to load!"", ""<<MOD_NAME>>"", MB_ICONERROR);
        FreeLibraryAndExitThread((HMODULE)hModule, 0);
    }

    <<?CONSOLE>>mod::unload();
    <<?CONSOLE>>gdmake::console::unload();
    <<?CONSOLE>>FreeLibraryAndExitThread((HMODULE)hModule, 0);

    return 0;
}

BOOL APIENTRY DllMain(
    HMODULE hModule,
    DWORD  ul_reason_for_call,
    LPVOID lpReserved
) {
    if (ul_reason_for_call == DLL_PROCESS_ATTACH) {
        HANDLE _ = CreateThread(0, 0, load_thread, hModule, 0, nullptr);
        if (_) CloseHandle(_);
    }
    return true;
}
";

        public const string GDMakeModNS =
@"
namespace mod {
    bool loadMod(HMODULE);
    void unloadMod();
}
";
    
        public const string ModLoadHeader =
DefaultStrings.HeaderCredit +
@"
#pragma once

#include <GDMake.h>

namespace mod {
    bool load(HMODULE);
    void unload();
}
";

        public const string ModLoadSource =
DefaultStrings.HeaderCredit +
@"
#ifndef __GDMAKE_MOD_H__
#define __GDMAKE_MOD_H__

#include ""mod.h""
#include ""hooks.h""

bool mod::load(HMODULE hModule) {
    auto init = MH_Initialize();
    if (init != MH_OK && init != MH_ERROR_ALREADY_INITIALIZED) [[unlikely]]
        return false;

    if (!loadMod(hModule))
        return false;

<<GDMAKE_HOOKS>>

    if (MH_EnableHook(MH_ALL_HOOKS) != MH_OK) [[unlikely]] {
        MH_Uninitialize();
        return false;
    }
    
    return true;
}

void mod::unload() {
    unloadMod();

    MH_DisableHook(MH_ALL_HOOKS);

    MH_Uninitialize();
}

#endif
";

        public const string ConsoleHeader =
DefaultStrings.HeaderCredit +
@"
#pragma once

namespace gdmake {
    namespace console {
        bool load();
        void unload();
        void awaitUnload();
    }
}
";

        public const string ConsoleSource =
DefaultStrings.HeaderCredit +
@"
#include ""<<GDMAKE_DIR>>/src/console.h""
#include <Windows.h>
#include <stdio.h>
#include <iostream>
#include <string>
#include <sstream>
#include ""debug.h""

bool gdmake::console::load() {
    if (AllocConsole() == 0)
        return false;

    // redirect console output
    freopen_s(reinterpret_cast<FILE**>(stdout), ""CONOUT$"", ""w"", stdout);
    freopen_s(reinterpret_cast<FILE**>(stdin), ""CONIN$"", ""r"", stdin);
    freopen_s(reinterpret_cast<FILE**>(stdout), ""CONERR$"", ""w"", stderr);

    return true;
}

void gdmake::console::unload() {
    fclose(stdin);
    fclose(stdout);
    fclose(stderr);
    FreeConsole();
}

void gdmake::console::awaitUnload() {
    std::string inp;
    getline(std::cin, inp);

    std::string inpa;
    std::stringstream ss(inp);
    std::vector<std::string> args;
    while (ss >> inpa)
        args.push_back(inpa);
    ss.clear();

    <<GDMAKE_DEBUGS>>

    if (inp != ""e"")
        awaitUnload();
}
";
        
        public const string CMakeLists =
HeaderCreditH +
@"
cmake_minimum_required(VERSION 3.10)
set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# shut the fuck up cmake
function(message)
  list(GET ARGV 0 MessageType)
  if(MessageType STREQUAL FATAL_ERROR OR
     MessageType STREQUAL SEND_ERROR OR
     MessageType STREQUAL WARNING OR
     MessageType STREQUAL AUTHOR_WARNING)
    list(REMOVE_AT ARGV 0)
    _message(${MessageType} ""${ARGV}"")
  endif()
endfunction()

set(PROJECT_NAME <<MOD_NAME>>)

project(${PROJECT_NAME} VERSION 1.0.0)

file(GLOB_RECURSE SOURCES 
    <<?GDMAKE_DLLMAIN>>dllmain.cpp
    <<?GDMAKE_DLLMAIN>>mod.cpp
    <<?GDMAKE_CONSOLE>>console.cpp
    src/*.cpp
    <<GDMAKE_SOURCES>>
)

set(WIN32 ON)
add_library(${PROJECT_NAME} SHARED ${SOURCES})

set_target_properties(${PROJECT_NAME} PROPERTIES PREFIX """")

target_include_directories(
    ${PROJECT_NAME} PRIVATE
    ${CMAKE_SOURCE_DIR}
    <<GDMAKE_HEADERS>>
)

target_link_libraries(
    ${PROJECT_NAME}
    <<GDMAKE_LIBS>>
)
";

        public const string BuildBat =
@"
@echo off

rem GDMake build.bat

cd ""%1""

if not exist build\ (
    mkdir build
)

cd build

cmake .. -A Win32 -Thost=x86 %5

msbuild %2.sln /p:Configuration=%3 /verbosity:%4 /p:PlatformTarget=x86 /m
";

        public const string InjectorCmake =
HeaderCreditH +
@"
cmake_minimum_required(VERSION 3.10)
set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

set(WIN32 ON)
set(PROJECT_NAME Inject32)

project(${PROJECT_NAME} VERSION 1.0.0)

add_executable(${PROJECT_NAME} injector.cpp)
target_link_libraries(${PROJECT_NAME} WtsApi32)
";

        public const string InjectorCpp =
HeaderCredit +
@"
#include <Windows.h>
#include <string>
#include <WtsApi32.h>
#include <iostream>
#include <filesystem>

#define INJECT_SUCCESS 0x3F
#define INJECT_TARGET_OPEN_FAIL 0x30
#define INJECT_TARGET_MALLOC_FAIL 0x31
#define INJECT_TARGET_CANT_WRITE 0x32
#define INJECT_TARGET_CANT_CREATE_THREAD 0x33
#define INJECT_ERROR_UNKNOWN 0x34

bool proc_running(const char* _proc, DWORD* _pid = NULL) {
    WTS_PROCESS_INFO* pWPIs = NULL;
    DWORD dwProcCount = 0;
    bool found = false;
    if (WTSEnumerateProcesses(WTS_CURRENT_SERVER_HANDLE, NULL, 1, &pWPIs, &dwProcCount))
        for (DWORD i = 0; i < dwProcCount; i++)
            if (strcmp((LPSTR)pWPIs[i].pProcessName, _proc) == 0) {
                found = true;
                if (_pid != NULL)
                    *_pid = pWPIs[i].ProcessId;
            }

    if (pWPIs) {
        WTSFreeMemory(pWPIs);
        pWPIs = NULL;
    }

    return found;
}

int InjectDLL(const int &pid, const std::string &DLL_Path) {
    // adapted from https://github.com/saeedirha/DLL-Injector
    
    long dll_size = DLL_Path.length() + 1;
    HANDLE hProc = OpenProcess(PROCESS_ALL_ACCESS, FALSE, pid);

    if (hProc == NULL)
        return INJECT_TARGET_OPEN_FAIL;

    LPVOID MyAlloc = VirtualAllocEx(hProc, NULL, dll_size, MEM_COMMIT, PAGE_EXECUTE_READWRITE);
    if (MyAlloc == NULL)
        return INJECT_TARGET_MALLOC_FAIL;

    int IsWriteOK = WriteProcessMemory(hProc , MyAlloc, DLL_Path.c_str() , dll_size, 0);
    if (IsWriteOK == 0)
        return INJECT_TARGET_CANT_WRITE;

    DWORD dWord;
    LPTHREAD_START_ROUTINE addrLoadLibrary = (LPTHREAD_START_ROUTINE)GetProcAddress(LoadLibraryA(""kernel32""), ""LoadLibraryA"");
    HANDLE ThreadReturn = CreateRemoteThread(hProc, NULL, 0, addrLoadLibrary, MyAlloc, 0, &dWord);
    if (ThreadReturn == NULL)
        return INJECT_TARGET_CANT_CREATE_THREAD;

    if ((hProc != NULL) && (MyAlloc != NULL) && (IsWriteOK != ERROR_INVALID_HANDLE) && (ThreadReturn != NULL))
        return INJECT_SUCCESS;

    return INJECT_ERROR_UNKNOWN;
}

int throwErr(std::string str) {
    std::cout << str << ""\n"";

    return 1;
}

int main(int argc, char* argv[]) {
    if (argc < 3)
        return throwErr(""Invalid arguments: <Process name> <Dll path>"");

    ////////////////////////////

    const char* exeName = argv[1];
    const char* dllPath = argv[2];

    if (!std::filesystem::exists(dllPath))
        return throwErr(""Dll does not exist!"");

    ////////////////////////////

    DWORD GD_PID = 0;
    if (!proc_running(exeName, &GD_PID))
        return throwErr(""Exe isn't running!"");

    ////////////////////////////

    int dll_suc = InjectDLL(GD_PID, dllPath);
    if (dll_suc != INJECT_SUCCESS)
        return throwErr(""Unable to inject DLL! (Error code: "" + std::to_string(dll_suc) + "")"");
    
    ////////////////////////////

    return 0;
}
";

        public const string ExampleProjectCpp =
HeaderCredit +
@"
// include GDMake & submodules
#include <GDMake.h>

GDMAKE_MAIN {
    // main entrypoint for your mod.
    // this is where you do things like
    // create hooks, load settings, etc.

    // you don't have to enable hooks, as
    // they are automatically enabled after
    // this method.

    // return true if load was succesful,
    // false if an error occurred.

    return true;
}

GDMAKE_UNLOAD {
    // if you need to do some extra cleanup
    // for your mod, write it here.
    
    // all default submodules are automatically
    // dealt with afterwards.
}
";
        public const string HelpersCpp =
HeaderCredit +
@"
#ifndef __GDMAKE_HELPERS_HPP__
#define __GDMAKE_HELPERS_HPP__

#include <string>
#include <filesystem>
#include <fstream>
#include <Windows.h>

namespace gdmake {
    namespace extra {
        template<typename T>
        static T getChild(cocos2d::CCNode* x, int i) {
            return static_cast<T>(x->getChildren()->objectAtIndex(i));
        }

        template <typename T, typename R>
        T as(R const v) { return reinterpret_cast<T>(v); }

        inline std::string operator"""" _s (const char* _txt, size_t) {
            return std::string(_txt);
        }

        template<typename T, typename U> constexpr size_t offsetOf(U T::*member) {
            return (char*)&((T*)nullptr->*member) - (char*)nullptr;
        }

        template<typename T>
        constexpr const T& clamp( const T& v, const T& lo, const T& hi) {
            return v < lo ? lo : hi < v ? hi : v;
        }
    }

    [[nodiscard]]
    static std::string readFileString(const std::string & _path) {
        std::ifstream in(_path, std::ios::in | std::ios::binary);
        if (in) {
            std::string contents;
            in.seekg(0, std::ios::end);
            contents.resize((const size_t)in.tellg());
            in.seekg(0, std::ios::beg);
            in.read(&contents[0], contents.size());
            in.close();
            return(contents);
        }
        return """";
    }

    [[nodiscard]]
    static std::vector<uint8_t> readFileBinary(const std::string & _path) {
        std::ifstream in(_path, std::ios::in | std::ios::binary);
        if (in)
            return std::vector<uint8_t> ( std::istreambuf_iterator<char>(in), {});
        return {};
    }

    static bool writeFileString(const std::string & _path, const std::string & _cont) {
        std::ofstream file;
        file.open(_path);
        if (file.is_open()) {
            file << _cont;
            file.close();

            return true;
        }
        file.close();
        return false;
    }

    static bool writeFileBinary(const std::string & _path, std::vector<uint8_t> const& _bytes) {
        std::ofstream file;
        file.open(_path, std::ios::out | std::ios::binary);
        if (file.is_open()) {
            file.write(reinterpret_cast<const char*>(_bytes.data()), _bytes.size());
            file.close();

            return true;
        }
        file.close();
        return false;
    }

    static constexpr unsigned int hash(const char* str, int h = 0) {
        return !str[h] ? 5381 : (hash(str, h+1) * 33) ^ str[h];
    }

    inline bool patchBytes(
        uintptr_t const address,
        std::vector<uint8_t> const& bytes
    ) {
        return WriteProcessMemory(
            GetCurrentProcess(),
            reinterpret_cast<LPVOID>(gd::base + address),
            bytes.data(),
            bytes.size(),
            nullptr
        );
    }

    using unknown_t = uintptr_t;
    using edx_t = uintptr_t;
}

#endif
";

        public const string ExtraCCMacros =
@"
#define CCARRAY_FOREACH_B_BASE(__array__, __obj__, __type__, __index__) \
    if (__array__ && __array__->count()) \
    for (auto [__index__, __obj__] = std::tuple<unsigned int, __type__> { 0u, nullptr }; \
        (__index__ < __array__->count() && (__obj__ = reinterpret_cast<__type__>(__array__->objectAtIndex(__index__)))); \
        __index__++)

#define CCARRAY_FOREACH_B_TYPE(__array__, __obj__, __type__) \
    CCARRAY_FOREACH_B_BASE(__array__, __obj__, __type__*, ix)

#define CCARRAY_FOREACH_B(__array__, __obj__) \
    CCARRAY_FOREACH_B_BASE(__array__, __obj__, cocos2d::CCObject*, ix)
";
    }
}
