#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"

export PATH="${HOME}/.dotnet:${HOME}/.local/bin:${PATH}"

PROJECT_PATH="${PROJECT_PATH:-game}"
DUMP="${1:-${ROOT}/tools/godot/debug-node-dump.txt}"
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
  --inspect-debug --debug-dump="$DUMP" >"$log" 2>&1; then
  cat "$log"
  fail "debug inspect exited non-zero"
fi
cat "$log"
echo "---- $DUMP ----"
cat "$DUMP"

expect() {
  grep -Fqx "$1" "$DUMP" || fail "dump missing line: $1"
}

grep -q 'DEBUG_DUMP case=open' "$DUMP" || fail "missing open dump"
expect "visible=true"
expect "ConnectionLabel=PLAYING"
expect "RoleLabel=host"
expect "TickLabel=42"
expect "PhaseLabel=DELIVERY"
expect "ShiftLabel=1"
expect "SeedLabel=0x7F3A9C21"
expect "WorldHashLabel=0x821670054873680E"
expect "PlayerLabel=1"
expect "WalletLabel=\$18.20"
expect "AuthorityLabel=host-only"
expect "GiveWallet=enabled"
expect "AdvancePhase=enabled"
expect "ResetPawn=enabled"
expect "TeleportIntake=enabled"
expect "TeleportMailbox=enabled"
expect "GiveMail=enabled"
expect "OpenInventory=enabled"
grep -q '^SpawnCount=' "$DUMP" || fail "missing SpawnCount"
expect "Spawn.axe=enabled"
expect "Spawn.letter=enabled"
expect "Spawn.bike=enabled"
expect "ToggleKey=F3"
grep -q 'DEBUG_DUMP case=closed' "$DUMP" || fail "missing closed dump"
expect "visible=false"
grep -q 'DEBUG_DUMP_END' "$DUMP" || fail "missing DEBUG_DUMP_END"

open_visible="$(awk '/DEBUG_DUMP case=open/,/DEBUG_DUMP case=closed/' "$DUMP" | grep -m1 '^visible=')"
closed_visible="$(awk '/DEBUG_DUMP case=closed/,/DEBUG_DUMP_END/' "$DUMP" | grep -m1 '^visible=')"
test "$open_visible" = "visible=true" || fail "open case is not visible"
test "$closed_visible" = "visible=false" || fail "closed case is still visible"
echo "debug open/close split ok"

echo "==> live-ui-verified"
