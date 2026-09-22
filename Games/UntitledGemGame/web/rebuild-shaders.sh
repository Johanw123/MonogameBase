#!/usr/bin/env bash
set -euo pipefail

web_dir=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
game_dir=$(cd -- "$web_dir/.." && pwd)
nuget_packages=${NUGET_PACKAGES:-"$HOME/.nuget/packages"}
kni_tools="$nuget_packages/nkast.xna.framework.content.pipeline.builder/4.2.9001/tools"
wine_prefix=${WINEPREFIX:-"$HOME/.wine"}
windows_dotnet="$wine_prefix/drive_c/Program Files/dotnet/dotnet.exe"
shader_output="$web_dir/obj/browser-shaders"
shader_intermediate="$web_dir/obj/browser-shaders-intermediate"

if [[ ! -f "$kni_tools/MGCB.dll" || ! -f "$windows_dotnet" ]]; then
  echo 'Requires restored KNI 4.2.9001 packages and Windows .NET 8 in the Wine prefix.' >&2
  exit 1
fi
mkdir -p "$shader_output" "$shader_intermediate"
shaders=(GemShader LineSDF JuicySDFRect HarvesterShader BackgroundShader)
args=(
  /platform:BlazorGL /profile:HiDef
  "/outputDir:$(winepath -w "$shader_output")"
  "/intermediateDir:$(winepath -w "$shader_intermediate")"
  "/workingDir:$(winepath -w "$game_dir/Content")"
  /rebuild
)
for shader in "${shaders[@]}"; do
  args+=("/build:Shaders/$shader.fx")
done
wine "$windows_dotnet" "$(winepath -w "$kni_tools/MGCB.dll")" "${args[@]}"
# Publish only after every shader compiled successfully.
for shader in "${shaders[@]}"; do
  test -s "$shader_output/Shaders/$shader.xnb"
done
for shader in "${shaders[@]}"; do
  cp -- "$shader_output/Shaders/$shader.xnb" "$game_dir/wwwroot/Content/Shaders/$shader.xnb"
done
printf '%s\n' 'Browser shaders rebuilt from current game sources. Restart the web build.'
