#!/usr/bin/env bash
# Builds the engine for Unity (netstandard2.1) and collects every DLL Unity needs
# (engine assemblies + UnitsNet) in artifacts/UnityPlugins.
# Copy that folder's content to <UnityProject>/Assets/_Project/Plugins/Engine.
set -euo pipefail
cd "$(dirname "$0")/.."
rm -rf artifacts/UnityPlugins
dotnet publish src/NaturalGasPipeline.Network/NaturalGasPipeline.Network.csproj \
  -c Release -f netstandard2.1 -o artifacts/UnityPlugins
ls -1 artifacts/UnityPlugins
