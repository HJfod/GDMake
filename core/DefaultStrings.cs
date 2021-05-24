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
    void loadMod(HMODULE);
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

    loadMod(hModule);

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
#include ""console.h""
#include <Windows.h>
#include <stdio.h>
#include <iostream>
#include <string>

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

set(PROJECT_NAME <<MOD_NAME>>)

project(${PROJECT_NAME} VERSION 1.0.0)

file(GLOB_RECURSE SOURCES 
    dllmain.cpp
    mod.cpp
    <<GDMAKE_DIR>>/src/console.cpp
    src/*.cpp
)

set(WIN32 ON)
add_library(${PROJECT_NAME} SHARED ${SOURCES})

set_target_properties(${PROJECT_NAME} PROPERTIES PREFIX """")

target_include_directories(
    ${PROJECT_NAME} PRIVATE
    <<GDMAKE_DIR>>/submodules/Cocos2d/cocos2dx
    <<GDMAKE_DIR>>/submodules/Cocos2d/cocos2dx/include
    <<GDMAKE_DIR>>/submodules/Cocos2d/cocos2dx/kazmath/include
    <<GDMAKE_DIR>>/submodules/Cocos2d/cocos2dx/platform/third_party/win32/OGLES
    <<GDMAKE_DIR>>/submodules/Cocos2d/cocos2dx/platform/win32
    <<GDMAKE_DIR>>/submodules/Cocos2d/extensions
    <<GDMAKE_DIR>>/submodules/gd.h/include
    <<GDMAKE_DIR>>/submodules/gd.h
    <<GDMAKE_DIR>>/submodules/MinHook/include
    <<GDMAKE_DIR>>/include
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
rem GDMake build.bat

@echo off

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
}

GDMAKE_UNLOAD {
    // if you need to do some extra cleanup
    // for your mod, write it here.
    
    // all default submodules are automatically
    // dealt with afterwards.
}
";
    }
}
