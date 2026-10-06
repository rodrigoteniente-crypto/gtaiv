#!/usr/bin/env bash
set -euo pipefail
repo_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
if [[ -x /workspace/.onboarding/dotnet/dotnet ]]; then
  export DOTNET_ROOT=/workspace/.onboarding/dotnet
  export PATH="$DOTNET_ROOT:$PATH"
  export DOTNET_CLI_HOME=/workspace/.onboarding/cli
  export NUGET_PACKAGES=/workspace/.onboarding/nuget
fi
export DOTNET_CLI_TELEMETRY_OPTOUT=1
for suite in StreamStability StreamCamera StreamEvents StreamHud StreamAmbient NpcPolicy StreamAdmin StreamPersistence; do
  dotnet run --project "$repo_dir/stream/tests/$suite" -c Release
done
