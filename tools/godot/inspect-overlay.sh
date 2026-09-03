#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"

export PATH="${HOME}/.dotnet:${HOME}/.local/bin:${PATH}"

PROJECT_PATH="${PROJECT_PATH:-game}"
DUMP="${1:-${ROOT}/tools/godot/overlay-node-dump.txt}"
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
  --inspect-overlay --overlay-dump="$DUMP" >"$log" 2>&1; then
  cat "$log"
  fail "overlay inspect exited non-zero"
fi
cat "$log"
echo "---- $DUMP ----"
cat "$DUMP"

expect() {
  grep -Fqx "$1" "$DUMP" || fail "dump missing line: $1"
}

grep -q 'OVERLAY_DUMP case=open' "$DUMP" || fail "missing open dump"
expect "visible=true"
expect "hotbar cols=8 rows=1"
expect "inventory cols=8 rows=2"
expect "backpack cols=8 rows=2"
expect "external cols=8 rows=4"
expect "hotbar[1,0] count=1 address=13 pending=1 opacity=0.6"
expect "hotbar_1_0 text=1 13 opacity=0.6"
expect "ShiftLabel=Shift 1 / 5"
expect "PhaseLabel=DELIVERY"
expect "WalletLabel=\$18.20"
grep -q 'OVERLAY_DUMP case=closed' "$DUMP" || fail "missing closed dump"
expect "visible=false"
expect "MatchMark=tick"
grep -q 'OVERLAY_DUMP_END' "$DUMP" || fail "missing OVERLAY_DUMP_END"

open_visible="$(awk '/OVERLAY_DUMP case=open/,/OVERLAY_DUMP case=closed/' "$DUMP" | grep -m1 '^visible=')"
closed_visible="$(awk '/OVERLAY_DUMP case=closed/,/OVERLAY_DUMP_END/' "$DUMP" | grep -m1 '^visible=')"
test "$open_visible" = "visible=true" || fail "open case is not visible"
test "$closed_visible" = "visible=false" || fail "closed case is still visible"
if awk '/OVERLAY_DUMP case=open/,/OVERLAY_DUMP case=closed/' "$DUMP" | grep -Fqx 'hotbar_1_0 text=1 13 opacity=1.0'; then
  fail "pending hotbar cell is fully opaque"
fi
echo "overlay open/close split ok"

echo "==> live-ui-verified"
