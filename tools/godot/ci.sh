#!/usr/bin/env bash
# Godot 4.7.2 .NET integration checks. Run inside barichello/godot-ci:mono-4.7.2
# (GitHub Actions `container:`) or any image with that editor + a .NET 8 SDK.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"

GODOT_PIN="${GODOT_PIN:-4.7.2}"
PROJECT_PATH="${PROJECT_PATH:-game}"
REQUIRE_DOTNET_8="${REQUIRE_DOTNET_8:-1}"

fail() {
  echo "ERROR: $*" >&2
  exit 1
}

need_cmd() {
  command -v "$1" >/dev/null 2>&1 || fail "$1 is not on PATH"
}

need_jq() {
  if ! command -v jq >/dev/null 2>&1; then
    if command -v apt-get >/dev/null 2>&1; then
      DEBIAN_FRONTEND=noninteractive apt-get update -qq
      DEBIAN_FRONTEND=noninteractive apt-get install -y -qq jq
    fi
  fi
  need_cmd jq
}

verify_godot() {
  echo "==> command -v godot"
  need_cmd godot
  local godot_bin
  godot_bin="$(command -v godot)"
  echo "$godot_bin"
  test -x "$godot_bin" || fail "godot is not executable: $godot_bin"

  echo "==> godot --version"
  local version
  version="$(godot --version)"
  echo "$version"
  echo "$version" | grep -E "$GODOT_PIN" >/dev/null \
    || fail "expected Godot $GODOT_PIN, got: $version"
  echo "$version" | grep -iE 'mono|\.net|dotnet' >/dev/null \
    || fail "expected a .NET/mono Godot build, got: $version"

  echo "==> godot --headless --quit"
  godot --headless --display-driver headless --quit
}

verify_dotnet() {
  echo "==> dotnet --version"
  need_cmd dotnet
  local version
  if version="$(dotnet --version 2>/dev/null)"; then
    echo "$version"
    if [[ "$REQUIRE_DOTNET_8" == "1" ]]; then
      echo "$version" | grep -E '^8\.' >/dev/null \
        || fail "expected dotnet 8.x so the Sim toolchain stays intact, got: $version"
    fi
    return
  fi

  if [[ "$REQUIRE_DOTNET_8" == "1" ]]; then
    fail "dotnet --version failed (repo global.json pins SDK 8.0.100 / latestFeature)"
  fi

  echo "Repo global.json wants 8.x; container SDK follows:"
  (cd /tmp && dotnet --version)
  cat > "$PROJECT_PATH/global.json" <<'EOF'
{
  "sdk": {
    "version": "8.0.100",
    "rollForward": "latestMajor"
  }
}
EOF
  echo "Wrote $PROJECT_PATH/global.json with rollForward latestMajor for this run"
}

import_and_build() {
  echo "==> import $PROJECT_PATH"
  test -f "$PROJECT_PATH/project.godot" || fail "missing $PROJECT_PATH/project.godot"
  # First import generates .godot even if C# has not been compiled yet.
  godot --headless --display-driver headless --path "$PROJECT_PATH" --import --quit \
    || true
  test -d "$PROJECT_PATH/.godot" || fail "godot --import did not create $PROJECT_PATH/.godot"

  echo "==> dotnet build $PROJECT_PATH"
  dotnet restore "$PROJECT_PATH/PerformativeMail.csproj"
  dotnet build "$PROJECT_PATH/PerformativeMail.csproj" --no-restore --configuration Debug
}

boot_smoke() {
  echo "==> headless boot smoke"
  local log
  log="$(mktemp)"
  if ! godot --headless --display-driver headless --path "$PROJECT_PATH" --quit-after 60 \
    >"$log" 2>&1; then
    cat "$log"
    fail "boot smoke exited non-zero"
  fi
  cat "$log"
  grep -q "performative-mail boot ok" "$log" \
    || fail "C# Main._Ready did not print the boot marker (script failed to load?)"
  if grep -E 'SCRIPT ERROR|Parse Error|Failed to load script' "$log" >/dev/null; then
    fail "boot smoke log contains a script error"
  fi
}

hud_inspect() {
  echo "==> HUD Control text inspect"
  SKIP_BUILD=1 bash "$ROOT/tools/godot/inspect-hud.sh" "$(mktemp)"
}

overlay_inspect() {
  echo "==> inventory overlay Control text inspect"
  SKIP_BUILD=1 bash "$ROOT/tools/godot/inspect-overlay.sh" "$(mktemp)"
}

lobby_inspect() {
  echo "==> lobby Control text inspect"
  SKIP_BUILD=1 bash "$ROOT/tools/godot/inspect-lobby.sh" "$(mktemp)"
}

debug_inspect() {
  echo "==> debug menu Control text inspect"
  SKIP_BUILD=1 bash "$ROOT/tools/godot/inspect-debug.sh" "$(mktemp)"
}

overlays_inspect() {
  echo "==> payday draft results Control text inspect"
  SKIP_BUILD=1 bash "$ROOT/tools/godot/inspect-overlays.sh" "$(mktemp)"
}

host_join_smoke() {
  echo "==> headless host/join smoke"
  local reports host_log guest_log host_pid
  reports="$(mktemp -d)"
  host_log="$(mktemp)"
  guest_log="$(mktemp)"

  godot --headless --display-driver headless --path "$PROJECT_PATH" -- \
    --host --walk --report="$reports/host.json" --quit-after-ms=12000 \
    >"$host_log" 2>&1 &
  host_pid=$!

  # Bind + hello before the guest dials 127.0.0.1:7777.
  sleep 4

  if ! godot --headless --display-driver headless --path "$PROJECT_PATH" -- \
    --join=127.0.0.1 --walk --report="$reports/guest.json" --quit-after-ms=8000 \
    >"$guest_log" 2>&1; then
    echo "---- host log ----"
    cat "$host_log"
    echo "---- guest log ----"
    cat "$guest_log"
    wait "$host_pid" || true
    fail "guest process exited non-zero"
  fi

  wait "$host_pid" || true

  echo "---- host report ----"
  if [[ ! -f "$reports/host.json" ]]; then
    echo "---- host log ----"
    cat "$host_log"
    fail "host did not write a report"
  fi
  cat "$reports/host.json"
  echo
  echo "---- guest report ----"
  if [[ ! -f "$reports/guest.json" ]]; then
    echo "---- guest log ----"
    cat "$guest_log"
    fail "guest did not write a report"
  fi
  cat "$reports/guest.json"
  echo

  assert_playing_with_two_pawns "$reports/host.json" host
  assert_playing_with_two_pawns "$reports/guest.json" guest
}

assert_playing_with_two_pawns() {
  local file="$1"
  local who="$2"
  grep -q '"state":"Playing"' "$file" || fail "$who report is not Playing: $(cat "$file")"
  local ids
  ids="$(grep -o '"id":[0-9][0-9]*' "$file" | wc -l | tr -d ' ')"
  test "$ids" -ge 2 || fail "$who report expected at least 2 pawns, got $ids: $(cat "$file")"
}

host_play_smoke() {
  echo "==> headless host play report"
  local report log
  report="$(mktemp)"
  log="$(mktemp)"
  if ! godot --headless --display-driver headless --path "$PROJECT_PATH" -- \
    --host --quit-after-ms=8000 --report="$report" \
    >"$log" 2>&1; then
    cat "$log"
    fail "host play process exited non-zero"
  fi
  if [[ ! -f "$report" ]]; then
    cat "$log"
    fail "host play did not write a report"
  fi
  cat "$report"
  echo
  grep -q '"state":"Playing"' "$report" || fail "host play report is not Playing: $(cat "$report")"
  grep -q '"worldHash":"0x821670054873680E"' "$report" \
    || fail "host play report missing golden worldHash: $(cat "$report")"
  grep -q '"hudPhase":"PREP"' "$report" \
    || fail "host play report missing PREP HUD: $(cat "$report")"
  grep -q '"hudShift":"Shift 1 / 5"' "$report" \
    || fail "host play report missing shift HUD: $(cat "$report")"
  need_jq
  jq -e '
    .state == "Playing"
    and .phase == "Prep"
    and .shift == 1
    and .worldHash == "0x821670054873680E"
    and (.pawns | length) >= 1
    and .worldEntityCounts.postOffices == 1
    and .worldEntityCounts.intakes == 1
    and .worldEntityCounts.houses == 50
    and .worldEntityCounts.mailboxes == 50
    and .overlayOpen == false
    and .debugOpen == false
  ' "$report" >/dev/null \
    || fail "host play report failed jq schema: $(cat "$report")"
}

host_debug_world_smoke() {
  echo "==> headless host debug-world report"
  local report log
  report="$(mktemp)"
  log="$(mktemp)"
  if ! godot --headless --display-driver headless --path "$PROJECT_PATH" -- \
    --host --debug-world --quit-after-ms=8000 --report="$report" \
    >"$log" 2>&1; then
    cat "$log"
    fail "host debug-world process exited non-zero"
  fi
  if [[ ! -f "$report" ]]; then
    cat "$log"
    fail "host debug-world did not write a report"
  fi
  cat "$report"
  echo
  grep -q '"state":"Playing"' "$report" || fail "host debug-world report is not Playing: $(cat "$report")"
  grep -q '"hudPhase":"PREP"' "$report" \
    || fail "host debug-world report missing PREP HUD: $(cat "$report")"
  grep -q '"hudShift":"Shift 1 / 5"' "$report" \
    || fail "host debug-world report missing shift HUD: $(cat "$report")"
  grep -q '"worldHash":"0x4CF184F2FA4D4EEE"' "$report" \
    || fail "host debug-world report missing debug worldHash: $(cat "$report")"
  need_jq
  jq -e '
    .state == "Playing"
    and .phase == "Prep"
    and .shift == 1
    and .worldHash == "0x4CF184F2FA4D4EEE"
    and (.pawns | length) >= 1
    and .worldEntityCounts.postOffices == 1
    and .worldEntityCounts.intakes == 1
    and .worldEntityCounts.houses == 2
    and .worldEntityCounts.mailboxes == 2
    and .overlayOpen == false
    and .debugOpen == false
  ' "$report" >/dev/null \
    || fail "host debug-world report failed jq schema: $(cat "$report")"
}

host_debug_helpers_smoke() {
  echo "==> headless host debug-helper=intake report"
  local report log
  report="$(mktemp)"
  log="$(mktemp)"
  if ! godot --headless --display-driver headless --path "$PROJECT_PATH" -- \
    --host --debug-world --debug-helper=intake --quit-after-ms=8000 --report="$report" \
    >"$log" 2>&1; then
    cat "$log"
    fail "host debug-helpers process exited non-zero"
  fi
  if [[ ! -f "$report" ]]; then
    cat "$log"
    fail "host debug-helpers did not write a report"
  fi
  cat "$report"
  echo
  grep -q '"state":"Playing"' "$report" || fail "host debug-helpers report is not Playing: $(cat "$report")"
  grep -q '"worldHash":"0x4CF184F2FA4D4EEE"' "$report" \
    || fail "host debug-helpers report missing debug worldHash: $(cat "$report")"
  need_jq
  jq -e '
    .state == "Playing"
    and .phase == "Prep"
    and .shift == 1
    and .worldHash == "0x4CF184F2FA4D4EEE"
    and (.pawns | length) >= 1
    and (.pawns[] | select(.role == "Local") | .x == 1100 and .y == 500)
    and .worldEntityCounts.postOffices == 1
    and .worldEntityCounts.intakes == 1
    and .worldEntityCounts.houses == 2
    and .worldEntityCounts.mailboxes == 2
    and .overlayOpen == false
    and .debugOpen == false
  ' "$report" >/dev/null \
    || fail "host debug-helpers report failed intake teleport: $(cat "$report")"
}

host_worldstage_smoke() {
  echo "==> headless host WorldStage live dump"
  local reports report dump log
  reports="$(mktemp -d)"
  report="$reports/report.json"
  dump="$reports/world-dump.txt"
  log="$(mktemp)"
  if ! godot --headless --display-driver headless --path "$PROJECT_PATH" -- \
    --host --debug-world --quit-after-ms=8000 --report="$report" --world-dump="$dump" \
    >"$log" 2>&1; then
    cat "$log"
    fail "host worldstage process exited non-zero"
  fi
  if [[ ! -f "$report" ]]; then
    cat "$log"
    fail "host worldstage did not write a report"
  fi
  if [[ ! -f "$dump" ]]; then
    cat "$log"
    fail "host worldstage did not write a WorldStage dump"
  fi
  cat "$report"
  echo
  echo "---- $dump ----"
  cat "$dump"
  echo
  grep -q '"state":"Playing"' "$report" || fail "host worldstage report is not Playing: $(cat "$report")"
  grep -q '"worldHash":"0x4CF184F2FA4D4EEE"' "$report" \
    || fail "host worldstage report missing debug worldHash: $(cat "$report")"
  need_jq
  jq -e '
    .state == "Playing"
    and .phase == "Prep"
    and .shift == 1
    and .worldHash == "0x4CF184F2FA4D4EEE"
    and (.pawns | length) >= 1
    and .worldEntityCounts.postOffices == 1
    and .worldEntityCounts.intakes == 1
    and .worldEntityCounts.mailboxes >= 2
  ' "$report" >/dev/null \
    || fail "host worldstage report failed jq schema: $(cat "$report")"
  grep -q 'WORLD_DUMP' "$dump" || fail "missing WORLD_DUMP: $(cat "$dump")"
  grep -q 'WORLD_DUMP_END' "$dump" || fail "missing WORLD_DUMP_END: $(cat "$dump")"
  grep -Fqx "PostOffice Label=Post Office" "$dump" \
    || fail "WorldStage dump missing Post Office: $(cat "$dump")"
  grep -Fqx "MailIntake Label=Mail" "$dump" \
    || fail "WorldStage dump missing Mail intake: $(cat "$dump")"
  grep -Fqx "House_1 Label=1 Debug Lane" "$dump" \
    || fail "WorldStage dump missing house address 1 Debug Lane: $(cat "$dump")"
  grep -Fqx "House_2 Label=2 Debug Lane" "$dump" \
    || fail "WorldStage dump missing house address 2 Debug Lane: $(cat "$dump")"
  grep -Fqx "Mailbox_1 Label=1 Debug Lane" "$dump" \
    || fail "WorldStage dump missing mailbox 1 Debug Lane: $(cat "$dump")"
  grep -Fqx "Mailbox_2 Label=2 Debug Lane" "$dump" \
    || fail "WorldStage dump missing mailbox 2 Debug Lane: $(cat "$dump")"
  local boxes
  boxes="$(grep -c '^Mailbox_' "$dump" || true)"
  boxes="${boxes// /}"
  test "$boxes" -ge 2 || fail "WorldStage dump expected at least 2 mailboxes, got $boxes: $(cat "$dump")"
}

usage() {
  cat <<'EOF'
Usage: tools/godot/ci.sh [all|verify|import|boot|hud|overlay|lobby|overlays|debug|join|play|debug-world|debug-helpers|worldstage]

  verify   Godot 4.7.2 .NET on PATH, --headless --quit, dotnet 8.x
  import   godot --import + dotnet build of game/
  boot     headless main-scene smoke (C# _Ready marker)
  hud      bind HudFrame and read Control text (match then mismatch)
  overlay  open InventoryOverlay from a U2 replica and read cell text
  lobby    bind LobbyFrame and read Control text (seed and ready)
  overlays bind payday, draft, and results frames and read Control text
  debug    open DebugMenu from DebugBoot and read inspect/cheat labels
  join     two-process LAN host/join on 127.0.0.1:7777
  play     solo Host play report with golden worldHash and HUD
  debug-world solo Host --debug-world report (2 houses, hash 0x4CF184F2FA4D4EEE)
  debug-helpers solo Host --debug-world --debug-helper=intake; local pawn at Intake (1100, 500)
  worldstage solo Host --debug-world report plus WorldStage Label3D dump (PO, Mail, addresses)
  all      all of the above (default)
EOF
}

cmd="${1:-all}"
case "$cmd" in
  verify)
    verify_godot
    verify_dotnet
    ;;
  import)
    import_and_build
    ;;
  boot)
    boot_smoke
    ;;
  hud)
    hud_inspect
    ;;
  overlay)
    overlay_inspect
    ;;
  lobby)
    lobby_inspect
    ;;
  overlays)
    overlays_inspect
    ;;
  debug)
    debug_inspect
    ;;
  join)
    host_join_smoke
    ;;
  play)
    host_play_smoke
    ;;
  debug-world)
    host_debug_world_smoke
    ;;
  debug-helpers)
    host_debug_helpers_smoke
    ;;
  worldstage)
    host_worldstage_smoke
    ;;
  all)
    verify_godot
    verify_dotnet
    import_and_build
    boot_smoke
    hud_inspect
    overlay_inspect
    lobby_inspect
    overlays_inspect
    debug_inspect
    host_join_smoke
    host_play_smoke
    host_debug_world_smoke
    host_debug_helpers_smoke
    host_worldstage_smoke
    echo "==> Godot 4.7.2 .NET integration checks passed"
    ;;
  -h|--help)
    usage
    ;;
  *)
    usage >&2
    fail "unknown command: $cmd"
    ;;
esac
