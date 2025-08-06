$RootPath = "Z:/home/johanw/Dev/workspaces/MonogameBase/HelloMonoGame/"

Set-Location -Path $RootPath

$ContentPath = "Content"
$ContentFullPath = "$($RootPath)$ContentPath"

$Files = Get-ChildItem -Path "$($ContentPath)/*.fx" -File -Recurse
$OutputPath = "Z:/home/johanw/.local/share/HelloMonoGame/CompiledShaders/"
 
ForEach ($File in $Files) 
{
  $Input = $File
  $RelativePath = $File.FullName.Substring($ContentFullPath.Length + 1)
  $Output = "$($OutputPath)$($RelativePath.Replace('.fx', '.mgfx'))"

  $OutputPathWithoutFileName = Split-Path $Output -Parent

  # Create folder structure and remove existing shader
  New-Item -ItemType Directory $OutputPathWithoutFileName -Force

  if (Test-Path $Output) {
      Remove-Item $Output -Force
  }

  Write-Host "Compiling Shader File: $($File.Name)..."
  Write-Host $Input
  Write-Host $Output
  Write-Host $RelativePath
  Z:/home/johanw/Dev/MonoGame/Artifacts/MonoGame.Effect.Compiler/Release/win-x64/publish/mgfxc.exe $Input $Output
}
