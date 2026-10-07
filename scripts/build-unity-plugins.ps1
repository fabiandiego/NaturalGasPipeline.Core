# Builds the engine for Unity (netstandard2.1) and collects every DLL Unity needs
# (engine assemblies + UnitsNet) in artifacts\UnityPlugins.
# Copy that folder's content to <UnityProject>\Assets\_Project\Plugins\Engine.
$ErrorActionPreference = "Stop"
Set-Location (Join-Path $PSScriptRoot "..")
if (Test-Path artifacts\UnityPlugins) { Remove-Item -Recurse -Force artifacts\UnityPlugins }
dotnet publish src\NaturalGasPipeline.Network\NaturalGasPipeline.Network.csproj `
  -c Release -f netstandard2.1 -o artifacts\UnityPlugins
Get-ChildItem artifacts\UnityPlugins -Name
