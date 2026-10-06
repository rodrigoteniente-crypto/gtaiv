#!/usr/bin/env bash
set -euo pipefail
repo_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
hook_file="${1:-/workspace/.onboarding/ScriptHookDotNet.dll}"
if [[ ! -f "$hook_file" ]]; then
  echo 'Uso: tools/build.sh /ruta/ScriptHookDotNet.asi' >&2
  exit 1
fi
if [[ -x /workspace/.onboarding/dotnet/dotnet ]]; then
  export DOTNET_ROOT=/workspace/.onboarding/dotnet
  export PATH="$DOTNET_ROOT:$PATH"
  export DOTNET_CLI_HOME=/workspace/.onboarding/cli
  export NUGET_PACKAGES=/workspace/.onboarding/nuget
fi
export DOTNET_CLI_TELEMETRY_OPTOUT=1
dotnet build "$repo_dir/src/KickChaos/KickChaos.net.dll.csproj" -c Release "-p:ScriptHookPath=$hook_file"
