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

python3 - <<PY
from pathlib import Path
text = Path("$DUMP").read_text()
open_part, closed_part = text.split("OVERLAY_DUMP case=closed", 1)
if "visible=true" not in open_part.split("OVERLAY_DUMP case=open", 1)[1]:
    raise SystemExit("open case is not visible")
if "visible=false" not in closed_part:
    raise SystemExit("closed case is still visible")
if "opacity=1.0" in [line for line in open_part.splitlines() if "hotbar_1_0" in line]:
    raise SystemExit("pending hotbar cell is fully opaque")
print("overlay open/close split ok")
PY

echo "==> live-ui-verified"
