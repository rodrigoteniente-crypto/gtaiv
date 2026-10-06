#!/usr/bin/env bash
set -euo pipefail
repo_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
hook_file="${1:-/workspace/.onboarding/ScriptHookDotNet.dll}"
if [[ ! -f "$hook_file" ]]; then
  echo 'Uso: bash tools/build-stream.sh /ruta/ScriptHookDotNet.asi' >&2
  exit 1
fi
hook_file="$(realpath -- "$hook_file")"
if [[ -x /workspace/.onboarding/dotnet/dotnet ]]; then
  export DOTNET_ROOT=/workspace/.onboarding/dotnet
  export PATH="$DOTNET_ROOT:$PATH"
  export DOTNET_CLI_HOME=/workspace/.onboarding/cli
  export NUGET_PACKAGES=/workspace/.onboarding/nuget
fi
export DOTNET_CLI_TELEMETRY_OPTOUT=1
dotnet restore "$repo_dir/stream/KickChaos/KickChaos.net.dll.csproj" --locked-mode
dotnet build "$repo_dir/stream/KickChaos/KickChaos.net.dll.csproj" -c Release --no-restore "-p:ScriptHookPath=$hook_file"
dotnet restore "$repo_dir/panel/KickChaos.Panel.csproj" --locked-mode
dotnet build "$repo_dir/panel/KickChaos.Panel.csproj" -c Release --no-restore
