#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"

export PATH="${HOME}/.dotnet:${HOME}/.local/bin:${PATH}"

PROJECT_PATH="${PROJECT_PATH:-game}"
DUMP="${1:-${ROOT}/tools/godot/build-node-dump.txt}"
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
  --inspect-build --build-dump="$DUMP" >"$log" 2>&1; then
  cat "$log"
  fail "build inspect exited non-zero"
fi
cat "$log"
echo "---- $DUMP ----"
cat "$DUMP"

expect() {
  grep -Fqx "$1" "$DUMP" || fail "dump missing line: $1"
}

grep -q 'BUILD_DUMP case=open' "$DUMP" || fail "missing open dump"
expect "open=true"
expect "category=Transport"
expect "selected=belt_mk1"
expect "selectedName=Conveyor Belt"
expect "valid=true"
expect "reason="
expect "category_0=Transport"
expect "category_1=Sorting"
expect "choice_0=belt_mk1"
grep -q 'BUILD_DUMP case=street' "$DUMP" || fail "missing street dump"
expect "valid=false"
expect "reason=On street"
expect "ReasonLabel=On street"
grep -q 'BUILD_DUMP case=closed' "$DUMP" || fail "missing closed dump"
closed_open="$(awk '/BUILD_DUMP case=closed/,/BUILD_DUMP_END/' "$DUMP" | grep -m1 '^open=')"
test "$closed_open" = "open=false" || fail "closed case is still open"
grep -q 'BUILD_DUMP_END' "$DUMP" || fail "missing BUILD_DUMP_END"
echo "==> live-ui-verified"
