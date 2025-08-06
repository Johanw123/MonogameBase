$RootPath = "C:\Users\Johan\source\repos\HelloMonoGame\HelloMonoGame\"

Set-Location -Path $RootPath

$ContentPath = "Content"
$ContentFullPath = "$($RootPath)$ContentPath"

$Files = Get-ChildItem -Path "$($ContentPath)/*.fx" -File -Recurse
$OutputPath = "C:\Users\Johan\AppData\Local\HelloMonoGame\CompiledShaders\"
 
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
  mgfxc $Input $Output
}
