#!/usr/bin/env bash
set -euo pipefail
cd -- "$(dirname -- "${BASH_SOURCE[0]}")"
# KNI's shader/audio compiler needs Windows libraries. Linux builds can use
# the Wine/.NET prefix installed for MonoGame shader compilation.
if [[ ${OSTYPE:-} != msys* && ${OSTYPE:-} != cygwin* ]]; then
  if [[ -z ${MGFXC_WINE_PATH:-} && -d ${HOME}/.winemonogame ]]; then
    export MGFXC_WINE_PATH="${HOME}/.winemonogame"
  fi
fi
dotnet publish web/UntitledGemGame.web.csproj -c Release -m:1 -o web/export "$@"
dotnet web/ContentBuilder/bin/Release/net10.0/ContentBuilder.dll --package-itch web/export/wwwroot web/beyond-the-belt-itch.zip
