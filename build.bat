@echo off

if not exist build\ (
    mkdir build
)

cd build

cmake ..

msbuild GDMake.sln /verbosity:minimal /property:configuration=Debug

cd Debug

GDMake.exe %*

