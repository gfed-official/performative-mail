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

usage() {
  cat <<'EOF'
Usage: tools/godot/ci.sh [all|verify|import|boot|hud|overlay|lobby|overlays|join]

  verify   Godot 4.7.2 .NET on PATH, --headless --quit, dotnet 8.x
  import   godot --import + dotnet build of game/
  boot     headless main-scene smoke (C# _Ready marker)
  hud      bind HudFrame and read Control text (match then mismatch)
  overlay  open InventoryOverlay from a U2 replica and read cell text
  lobby    bind LobbyFrame and read Control text (seed and ready)
  overlays bind payday, draft, and results frames and read Control text
  join     two-process LAN host/join on 127.0.0.1:7777
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
  join)
    host_join_smoke
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
    host_join_smoke
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
