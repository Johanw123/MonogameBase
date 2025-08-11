
#! /bin/bash

cd FrogFightServer
dotnet build --no-restore
cd ..
cd FrogFight
dotnet build --no-restore
cd ..

# Run server and 2 clients
wezterm cli spawn --cwd "$PWD/FrogFightServer/" -- bash -c "dotnet run --no-build"
wezterm cli spawn --cwd "$PWD/FrogFight/" -- bash -c "dotnet run --no-build"
wezterm cli spawn --cwd "$PWD/FrogFight/" -- bash -c "dotnet run --no-build"
