#!/usr/bin/env bash
# Idempotent Cloud Agent bootstrap for performative-mail.
# Installs the .NET 8 SDK (global.json pins the 8.0.1xx band) and primes the
# solution. The Godot 4.7.2 game/ shell is intentionally excluded here; it is
# not part of PerformativeMail.sln and CI exercises it in a separate container.
set -euo pipefail

cd "$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

export DOTNET_NOLOGO=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1

if ! command -v dotnet >/dev/null 2>&1 || ! dotnet --list-sdks | grep -q '^8\.'; then
  sudo apt-get update -y
  sudo DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends dotnet-sdk-8.0
fi

dotnet --version

dotnet restore PerformativeMail.sln
dotnet build PerformativeMail.sln --no-restore --configuration Release
