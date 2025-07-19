#!/bin/bash

muvm -ti -- box64 ~/Downloads/wine-10.11-amd64/bin/wine  ~/Dev/MonoGame/Artifacts/MonoGame.Effect.Compiler/Release/win-x64/publish/mgfxc.exe ./effect.fx ./effect.out
