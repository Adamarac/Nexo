#!/usr/bin/env bash
# Roda um comando do dotnet num container Linux, numa cópia do código.
# Necessário nesta máquina: o Smart App Control do Windows bloqueia as DLLs geradas no build.
set -euo pipefail
raiz="$(cd "$(dirname "$0")/.." && pwd -W 2>/dev/null || pwd)"
MSYS_NO_PATHCONV=1 docker run --rm \
  -v "$raiz:/src" \
  -v nexo-nuget:/root/.nuget/packages \
  -v //var/run/docker.sock:/var/run/docker.sock \
  -e DOTNET_NOLOGO=1 -e DOTNET_CLI_TELEMETRY_OPTOUT=1 \
  -e TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal \
  mcr.microsoft.com/dotnet/sdk:10.0 bash -c "
    mkdir -p /work && cp -r /src/. /work && cd /work
    find . \( -name bin -o -name obj -o -name node_modules \) -type d -prune -exec rm -rf {} +
    $*
  "
