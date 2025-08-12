
#! /bin/bash

cd FrogFightServer
dotnet build
cd ..
cd FrogFight
dotnet build --runtime win-x64
cd ..

yes | cp -R FrogFight/bin/Debug/net8.0/win-x64/ ~/Applications/Kegworks/FrogFightTest.app/Contents/drive_c/FrogFightDebug

# Run server and 2 clients
wezterm cli spawn --cwd "$PWD/FrogFightServer/" -- bash -c "dotnet run --no-build"
wezterm cli spawn --cwd "$PWD/FrogFight/" -- bash -c "open -n ~/Applications/Kegworks/FrogFightTest.app"
wezterm cli spawn --cwd "$PWD/FrogFight/" -- bash -c "open -n ~/Applications/Kegworks/FrogFightTest.app"
