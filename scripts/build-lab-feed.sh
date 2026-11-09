#!/usr/bin/env bash
# Packs the lab-only PoC package into local-lab-feed/. Required once before
# building VictimApp or DefensiveApp directly; `dotnet test` does this for
# you automatically in an isolated temp copy and never touches this folder.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
dotnet pack src/EvilPkg.Benign/EvilPkg.Benign.csproj -c Release -o local-lab-feed
