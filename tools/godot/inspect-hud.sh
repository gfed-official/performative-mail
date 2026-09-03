#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"

export PATH="${HOME}/.dotnet:${HOME}/.local/bin:${PATH}"

PROJECT_PATH="${PROJECT_PATH:-game}"
DUMP="${1:-${ROOT}/tools/godot/hud-node-dump.txt}"
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
  --inspect-hud --hud-dump="$DUMP" >"$log" 2>&1; then
  cat "$log"
  fail "HUD inspect exited non-zero"
fi
cat "$log"
echo "---- $DUMP ----"
cat "$DUMP"

expect() {
  grep -Fqx "$1" "$DUMP" || fail "dump missing line: $1"
}

grep -q 'HUD_DUMP case=match' "$DUMP" || fail "missing match dump"
expect "ShiftLabel=Shift 1 / 5"
expect "PhaseLabel=DELIVERY"
expect "TimerLabel=01:30"
expect "WalletLabel=\$18.20"
expect "HeldAddress=13 Larch Lane"
expect "TargetAddress=13 Larch Lane"
expect "MatchMark=tick"
grep -q 'HUD_DUMP case=mismatch' "$DUMP" || fail "missing mismatch dump"
expect "TargetAddress=8 Oak Street"
expect "MatchMark=cross"
grep -q 'HUD_DUMP_END' "$DUMP" || fail "missing HUD_DUMP_END"
echo "==> live-ui-verified"
