#!/bin/bash
 # Environment.SetEnvironmentVariable("WINEARCH", "win64");
 #            Environment.SetEnvironmentVariable("WINEDLLOVERRIDES", "d3dcompiler_47=n");
 #            Environment.SetEnvironmentVariable("WINEPREFIX", mgfxcwine);
 #            Environment.SetEnvironmentVariable("WINEDEBUG", "-all");

export WINEARCH=win64
export WINEDLLOVERRIDES="d3dcompiler_47=n"
export WINEPREFIX=$HOME/.winemonogame
export WINEDEBUG="-all"
# set WINEDLLOVERRIDES win64
# set WINEARCH "d3dcompiler_47=n"
# set WINEPREFIX mgfxcwine
# set WINEDEBUG "-all"
wine64 $HOME/Dev/MonoGame/Artifacts/MonoGame.Effect.Compiler/Release/win-x64/publish/mgfxc.exe effect.fx test.out
