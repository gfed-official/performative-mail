#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"

export PATH="${HOME}/.dotnet:${HOME}/.local/bin:${PATH}"

PROJECT_PATH="${PROJECT_PATH:-game}"
DUMP="${1:-${ROOT}/tools/godot/shop-node-dump.txt}"
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
  --inspect-shop --shop-dump="$DUMP" >"$log" 2>&1; then
  cat "$log"
  fail "shop inspect exited non-zero"
fi
cat "$log"
echo "---- $DUMP ----"
cat "$DUMP"

expect() {
  grep -Fqx "$1" "$DUMP" || fail "dump missing line: $1"
}

grep -q 'SHOP_DUMP case=open' "$DUMP" || fail "missing open dump"
expect "visible=true"
expect "WalletLabel=\$10.00"
expect "PhaseLabel=PREP"
expect "OfferCount=4"
expect "Offer.axe=Axe|\$0.80||can"
expect "Offer.bandage_x3=Bandages ×3|\$0.80||can"
expect "Offer.bike=Bike|\$1.20||can"
expect "Offer.bp_pipes=Blueprint: Pneumatics|\$7.00|Unlocks shift 3|locked"
grep -q 'SHOP_DUMP case=closed' "$DUMP" || fail "missing closed dump"
expect "visible=false"
grep -q 'SHOP_DUMP_END' "$DUMP" || fail "missing SHOP_DUMP_END"

open_visible="$(awk '/SHOP_DUMP case=open/,/SHOP_DUMP case=closed/' "$DUMP" | grep -m1 '^visible=')"
closed_visible="$(awk '/SHOP_DUMP case=closed/,/SHOP_DUMP_END/' "$DUMP" | grep -m1 '^visible=')"
test "$open_visible" = "visible=true" || fail "open case is not visible"
test "$closed_visible" = "visible=false" || fail "closed case is still visible"
echo "shop open/close split ok"

echo "==> live-ui-verified"
