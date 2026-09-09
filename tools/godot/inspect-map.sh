#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"

export PATH="${HOME}/.dotnet:${HOME}/.local/bin:${PATH}"

PROJECT_PATH="${PROJECT_PATH:-game}"
DUMP="${1:-${ROOT}/tools/godot/map-node-dump.txt}"
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
  --inspect-map --map-dump="$DUMP" >"$log" 2>&1; then
  cat "$log"
  fail "map inspect exited non-zero"
fi
cat "$log"
echo "---- $DUMP ----"
cat "$DUMP"

expect() {
  grep -Fqx "$1" "$DUMP" || fail "dump missing line: $1"
}

grep -q 'MAP_DUMP case=open' "$DUMP" || fail "missing open dump"
expect "visible=true"
expect "width=24"
expect "height=16"
expect "layers=districts,streets"
expect "chip.districts=on"
expect "chip.streets=on"
expect "street.1=Oak Street d=1 hex=#3D7EFF"
expect "street.2=Larch Lane d=2 hex=#E85D3A"
expect "house.1/1/1 mail=1"
expect "house.2/2/2 mail=0"
expect "resource.wood=4,3"
expect "route.1-2"
grep -q 'MAP_DUMP case=mail' "$DUMP" || fail "missing mail dump"
expect "chip.mail=on"
grep -q 'MAP_DUMP case=filters' "$DUMP" || fail "missing filters dump"
expect "filters=mail,routes,resources"
expect "chip.routes=on"
expect "chip.resources=on"
grep -q 'MAP_DUMP case=ping' "$DUMP" || fail "missing ping dump"
expect "pings=1"
expect "ping.1 tile=5,6 kind=default"
grep -q 'MAP_DUMP case=closed' "$DUMP" || fail "missing closed dump"
expect "visible=false"
grep -q 'MAP_DUMP_END' "$DUMP" || fail "missing MAP_DUMP_END"

open_visible="$(awk '/MAP_DUMP case=open/,/MAP_DUMP case=mail/' "$DUMP" | grep -m1 '^visible=')"
closed_visible="$(awk '/MAP_DUMP case=closed/,/MAP_DUMP_END/' "$DUMP" | grep -m1 '^visible=')"
test "$open_visible" = "visible=true" || fail "open case is not visible"
test "$closed_visible" = "visible=false" || fail "closed case is still visible"
echo "map open/close split ok"

echo "==> live-ui-verified"
