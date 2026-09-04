#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"

export PATH="${HOME}/.dotnet:${HOME}/.local/bin:${PATH}"

PROJECT_PATH="${PROJECT_PATH:-game}"
DUMP="${1:-${ROOT}/tools/godot/lobby-node-dump.txt}"
mkdir -p "$(dirname "$DUMP")"

fail() {
  echo "ERROR: $*" >&2
  exit 1
}

need() {
  command -v "$1" >/dev/null 2>&1 || fail "$1 is not on PATH"
}

need godot
need dotnet

if [[ "${SKIP_BUILD:-0}" != "1" ]]; then
  godot --headless --display-driver headless --path "$PROJECT_PATH" --import --quit || true
  dotnet restore "$PROJECT_PATH/PerformativeMail.csproj"
  dotnet build "$PROJECT_PATH/PerformativeMail.csproj" --no-restore --configuration Debug
fi

log="$(mktemp)"
if ! godot --headless --display-driver headless --path "$PROJECT_PATH" -- \
  --inspect-lobby --lobby-dump="$DUMP" >"$log" 2>&1; then
  cat "$log"
  fail "lobby inspect exited non-zero"
fi
cat "$log"
echo "---- $DUMP ----"
cat "$DUMP"

expect() {
  grep -Fqx "$1" "$DUMP" || fail "dump missing line: $1"
}

grep -q 'LOBBY_DUMP case=arcade' "$DUMP" || fail "missing arcade dump"
expect "visible=true"
expect "SeedLabel=0x7F3A9C21"
expect "ArchetypeLabel=small_island"
expect "KitLabel=land"
expect "ReadyLabel=not ready"
expect "StartLabel=Start"
expect "StartEnabled=false"
expect "PlayerList=Jules host land not ready"
grep -q 'LOBBY_DUMP case=ready' "$DUMP" || fail "missing ready dump"
expect "ReadyLabel=ready"
expect "StartEnabled=true"
expect "PlayerList=Jules host land ready"
grep -q 'LOBBY_DUMP_END' "$DUMP" || fail "missing LOBBY_DUMP_END"
echo "==> live-ui-verified"
