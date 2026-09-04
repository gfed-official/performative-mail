#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"

export PATH="${HOME}/.dotnet:${HOME}/.local/bin:${PATH}"

PROJECT_PATH="${PROJECT_PATH:-game}"
DUMP="${1:-${ROOT}/tools/godot/overlays-node-dump.txt}"
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
  --inspect-overlays --overlays-dump="$DUMP" >"$log" 2>&1; then
  cat "$log"
  fail "overlays inspect exited non-zero"
fi
cat "$log"
echo "---- $DUMP ----"
cat "$DUMP"

expect() {
  grep -Fqx "$1" "$DUMP" || fail "dump missing line: $1"
}

grep -q 'PAYDAY_DUMP case=payday' "$DUMP" || fail "missing payday dump"
expect "EarnedLabel=640"
expect "QuotaLabel=2214"
grep -q 'DRAFT_DUMP case=draft' "$DUMP" || fail "missing draft dump"
expect "Card1Label=insured"
expect "Card2Label=quick_hands"
expect "Card3Label=union_rep"
grep -q 'RESULTS_DUMP case=results' "$DUMP" || fail "missing results dump"
expect "ScoreLabel=14375"
expect "SeedLabel=PM1-SMALL-7F3A9C21-CM.DR"
grep -q 'PHASE_DUMP_END' "$DUMP" || fail "missing PHASE_DUMP_END"
echo "==> live-ui-verified"
