#!/usr/bin/env bash
# Thin playtest harness. Headless work is tools/godot/ci.sh + dotnet test.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"
# shellcheck source=lib/playtest.sh
source "$ROOT/tools/playtest/lib/playtest.sh"

export PATH="${HOME}/.dotnet:${HOME}/.local/bin:${PATH}"
export DOTNET_NOLOGO="${DOTNET_NOLOGO:-1}"
export DOTNET_CLI_TELEMETRY_OPTOUT="${DOTNET_CLI_TELEMETRY_OPTOUT:-1}"

CI="$ROOT/tools/godot/ci.sh"
PROJECT_PATH="${PROJECT_PATH:-game}"
DISPLAY="${DISPLAY:-:19}"
export DISPLAY

MODE="headless"
DEEP_GUI=0
GUI_MS="${PLAYTEST_GUI_MS:-8000}"
DEEP_GUI_MS="${PLAYTEST_DEEP_GUI_MS:-20000}"

usage() {
  cat <<'EOF'
Usage: bash tools/playtest/run.sh [--gui|--gui-only|--deep-gui|--help]

  (default)     headless H1–H9 plus cheap extras (play, debug-world, worldstage, leave)
  --gui         headless suite, then a short DISPLAY GUI pass (G-A…G-G)
  --gui-only    GUI pass only
  --deep-gui    same as --gui with longer waits / extra shots (alias for visual/fail follow-up)
  --help        this message

DISPLAY defaults to :19. Artifacts: artifacts/playtest/<run-id>/report.json
See tools/playtest/README.md for check IDs.
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --gui) MODE="gui" ;;
    --gui-only) MODE="gui-only" ;;
    --deep-gui)
      DEEP_GUI=1
      if [[ "$MODE" == "headless" ]]; then
        MODE="gui"
      fi
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      usage >&2
      echo "ERROR: unknown argument: $1" >&2
      exit 2
      ;;
  esac
  shift
done

if [[ "$DEEP_GUI" -eq 1 ]]; then
  GUI_MS="$DEEP_GUI_MS"
fi

SHA="$(git_sha)"
STARTED_AT="$(iso_now)"
SHORT_SHA="$(git -C "$ROOT" rev-parse --short HEAD 2>/dev/null || echo nogit)"
RUN_ID="$(date -u +%Y%m%dT%H%M%SZ)-${SHORT_SHA}"
OUT="$ROOT/artifacts/playtest/$RUN_ID"
mkdir -p "$OUT/logs" "$OUT/shots"
CHECKS_FILE="$OUT/checks.jsonl"
SHOTS_FILE="$OUT/shots.txt"
REPORT_FILE="$OUT/report.json"
: >"$CHECKS_FILE"
: >"$SHOTS_FILE"

echo "==> playtest $RUN_ID sha=${SHA:0:12} mode=$MODE display=$DISPLAY"
if git_dirty; then
  echo "==> git dirty; leaving tree alone"
else
  echo "==> git clean"
fi

run_ci() {
  bash "$CI" "$@"
}

need_jq() {
  have jq || {
    echo "ERROR: jq is required to write report.json" >&2
    exit 2
  }
}

need_jq

skip_check() {
  append_check "$1" "$2" skip "$3"
}

fail_remaining_gui() {
  local reason="$1"
  local id name
  for id in G-B G-C G-D G-E G-F G-G; do
    case "$id" in
      G-B) name="HUD / playing window" ;;
      G-C) name="Debug menu (F3 path)" ;;
      G-D) name="Teleport helper (intake)" ;;
      G-E) name="Overlay helper" ;;
      G-F) name="Leave / Esc" ;;
      G-G) name="Screenshot set" ;;
      *) name="$id" ;;
    esac
    skip_check "$id" "$name" "$reason"
  done
}

run_ci_check() {
  local id="$1" name="$2"
  shift 2
  if ! have_godot; then
    skip_check "$id" "$name" "godot not on PATH"
    return 0
  fi
  local log="$OUT/logs/${id}.log" cmd
  mkdir -p "$(dirname "$log")"
  : >"$log"
  for cmd in "$@"; do
    echo "==> tools/godot/ci.sh $cmd" >>"$log"
    if ! bash "$CI" "$cmd" >>"$log" 2>&1; then
      append_check "$id" "$name" fail "$(tail_detail "$log")"
      return 0
    fi
  done
  append_check "$id" "$name" pass "$(tail_detail "$log")"
}

ensure_game_build() {
  if have_godot; then
    run_ci import
    return
  fi
  if have_dotnet; then
    # game/ needs Godot.NET.Sdk; sln build still covers Sim/Net for H7/H9.
    dotnet build "$ROOT/PerformativeMail.sln" --configuration Debug
    return
  fi
  return 1
}

run_h7() {
  local id="H7" name="LanAddress / Host advertise == default-route NIC"
  local log="$OUT/logs/${id}.log"
  mkdir -p "$(dirname "$log")"
  : >"$log"
  local src="" src_note=""
  if src="$(default_route_src)"; then
    if advertisable_ipv4 "$src"; then
      src_note="default-route src=$src (advertisable)"
      echo "$src_note" >>"$log"
    else
      echo "default-route src=$src is not advertisable (loopback/APIPA/any)" >>"$log"
      append_check "$id" "$name" fail "$(tail_detail "$log")"
      return 0
    fi
  else
    src_note="no default-route IPv4; LanAddress falls back to first advertisable NIC or 127.0.0.1"
    echo "$src_note" >>"$log"
  fi

  if ! have_dotnet; then
    skip_check "$id" "$name" "dotnet not on PATH; $src_note. Surface tests/Net.Tests/LanAddressTests.cs when SDK is present."
    return 0
  fi

  if dotnet test "$ROOT/tests/Net.Tests/PerformativeMail.Net.Tests.csproj" \
    --configuration Debug --verbosity minimal \
    --filter FullyQualifiedName~LanAddress \
    >>"$log" 2>&1; then
    echo "LanAddressTests passed (HostAdvertisement uses FirstNonLoopbackIPv4 / default-route probe)" >>"$log"
    append_check "$id" "$name" pass "$(tail_detail "$log")"
  else
    append_check "$id" "$name" fail "$(tail_detail "$log")"
  fi
}

run_h9() {
  local id="H9" name="dotnet test solution (Sim + Net + Bot)"
  if ! have_dotnet; then
    skip_check "$id" "$name" "dotnet not on PATH"
    return 0
  fi
  local log="$OUT/logs/${id}.log"
  if run_logged "$log" dotnet test "$ROOT/PerformativeMail.sln" --configuration Debug --verbosity minimal; then
    append_check "$id" "$name" pass "$(tail_detail "$log")"
  else
    append_check "$id" "$name" fail "$(tail_detail "$log")"
  fi
}

run_headless() {
  echo "==> headless suite"
  run_ci_check H1 "verify + import + boot" verify import boot
  run_ci_check H2 "lobby" lobby
  run_ci_check H3 "hud + live-hud" hud live-hud
  run_ci_check H4 "overlays + overlay + live-overlay" overlays overlay live-overlay
  run_ci_check H5 "debug (panelRect > 0, panelGlobal.x >= 0)" debug
  run_ci_check H6 "join" join
  run_h7
  run_ci_check H8 "interact" interact
  run_ci_check H-play "play" play
  run_ci_check H-debug-world "debug-world" debug-world
  run_ci_check H-worldstage "worldstage" worldstage
  run_ci_check H-leave "leave" leave
  run_h9

  local ran=0
  if [[ -s "$CHECKS_FILE" ]]; then
    ran="$(jq -s '[.[] | select(.id | startswith("H")) | select(.status == "pass" or .status == "fail")] | length' "$CHECKS_FILE")"
  fi
  if [[ "$ran" -eq 0 ]]; then
    append_check H-env "toolchain" fail "godot and dotnet missing; headless suite skipped"
  fi
}

run_gui_host() {
  local tag="$1"
  shift
  local log="$OUT/logs/gui-${tag}.log"
  local report="$OUT/gui-${tag}.json"
  local shot="$OUT/shots/${tag}.png"
  local ms="$GUI_MS"
  mkdir -p "$OUT/shots"

  # Existing CLI: Host + debug helpers / teleports. No new Sim.
  if ! run_logged "$log" godot --path "$PROJECT_PATH" -- \
    --host --debug-world --quit-after-ms="$ms" --report="$report" "$@"; then
    echo "godot $tag exited non-zero" >>"$log"
  fi

  if capture_display "$shot"; then
    record_shot "$shot" || true
  else
    echo "screenshot failed for $tag" >>"$log"
    shot=""
  fi

  printf '%s\t%s\t%s\n' "$log" "$report" "$shot"
}

gui_shots_arg() {
  local shot="$1"
  if [[ -n "$shot" && -f "$shot" ]]; then
    shots_json_from_files "$shot"
  else
    echo '[]'
  fi
}

run_gui() {
  echo "==> short GUI pass DISPLAY=$DISPLAY ms=$GUI_MS"
  if ! have_godot; then
    append_check G-A "Host play on DISPLAY" fail "godot not on PATH"
    fail_remaining_gui "godot not on PATH"
    return 0
  fi
  if ! display_up "$DISPLAY"; then
    append_check G-A "Host play on DISPLAY" fail "DISPLAY $DISPLAY is not reachable"
    fail_remaining_gui "DISPLAY $DISPLAY is not reachable"
    return 0
  fi

  local host_meta host_log host_report host_shot
  host_meta="$(run_gui_host host)"
  IFS=$'\t' read -r host_log host_report host_shot <<<"$host_meta"

  if [[ -f "$host_report" ]] && grep -q '"state":"Playing"' "$host_report"; then
    append_check G-A "Host play on DISPLAY" pass "$(tail_detail "$host_log")" "$(gui_shots_arg "$host_shot")"
  else
    append_check G-A "Host play on DISPLAY" fail "$(tail_detail "$host_log")" "$(gui_shots_arg "$host_shot")"
  fi

  if [[ -f "$host_report" ]] && grep -q '"hudPhase":"PREP"' "$host_report"; then
    append_check G-B "HUD / playing window" pass "hudPhase PREP in $host_report" "$(gui_shots_arg "$host_shot")"
  else
    append_check G-B "HUD / playing window" fail "no PREP hudPhase in GUI host report" "$(gui_shots_arg "$host_shot")"
  fi

  # F3 key inject is not automated. Reuse inspect-debug (same panelRect asserts as H5).
  local dlog="$OUT/logs/G-C.log"
  if run_logged "$dlog" bash "$CI" debug; then
    append_check G-C "Debug menu (F3 path)" pass \
      "ci.sh debug (panelRect>0, panelGlobal.x>=0). F3/backtick click not injected; see README gaps."
  else
    append_check G-C "Debug menu (F3 path)" fail "$(tail_detail "$dlog")"
  fi

  local intake_meta intake_log intake_report intake_shot
  intake_meta="$(run_gui_host intake --debug-helper=intake)"
  IFS=$'\t' read -r intake_log intake_report intake_shot <<<"$intake_meta"
  if [[ -f "$intake_report" ]] && grep -q '"state":"Playing"' "$intake_report"; then
    append_check G-D "Teleport helper (intake)" pass "$(tail_detail "$intake_log")" "$(gui_shots_arg "$intake_shot")"
  else
    append_check G-D "Teleport helper (intake)" fail "$(tail_detail "$intake_log")" "$(gui_shots_arg "$intake_shot")"
  fi

  local overlay_meta overlay_log overlay_report overlay_shot
  overlay_meta="$(run_gui_host overlay --debug-helper=overlay)"
  IFS=$'\t' read -r overlay_log overlay_report overlay_shot <<<"$overlay_meta"
  if [[ -f "$overlay_report" ]] && grep -q '"overlayOpen":true' "$overlay_report"; then
    append_check G-E "Overlay helper" pass "$(tail_detail "$overlay_log")" "$(gui_shots_arg "$overlay_shot")"
  else
    append_check G-E "Overlay helper" fail "$(tail_detail "$overlay_log")" "$(gui_shots_arg "$overlay_shot")"
  fi

  local leave_meta leave_log leave_report leave_shot
  leave_meta="$(run_gui_host leave --debug-helper=leave)"
  IFS=$'\t' read -r leave_log leave_report leave_shot <<<"$leave_meta"
  if [[ -f "$leave_report" ]] && grep -q '"state":"Menu"' "$leave_report"; then
    append_check G-F "Leave / Esc" pass "$(tail_detail "$leave_log")" "$(gui_shots_arg "$leave_shot")"
  else
    append_check G-F "Leave / Esc" fail "$(tail_detail "$leave_log")" "$(gui_shots_arg "$leave_shot")"
  fi

  local shot_count=0
  if [[ -s "$SHOTS_FILE" ]]; then
    shot_count="$(grep -c . "$SHOTS_FILE" || true)"
  fi
  if [[ "$shot_count" -gt 0 ]]; then
    append_check G-G "Screenshot set" pass "$shot_count shot(s) under $OUT/shots"
  else
    append_check G-G "Screenshot set" fail \
      "no shots (need scrot, ImageMagick import, or ffmpeg x11grab on DISPLAY=$DISPLAY)"
  fi
}

case "$MODE" in
  headless) run_headless ;;
  gui)
    run_headless
    run_gui
    ;;
  gui-only)
    if have_godot; then
      run_logged "$OUT/logs/H-build.log" ensure_game_build || true
    fi
    run_gui
    ;;
  *)
    echo "ERROR: unknown mode: $MODE" >&2
    exit 2
    ;;
esac

FINISHED_AT="$(iso_now)"
FAILED="$(jq -s '[.[] | select(.status=="fail")] | length' "$CHECKS_FILE")"
OK=false
if [[ "$FAILED" -eq 0 ]]; then
  OK=true
fi
write_report "$OK"

echo "==> report $REPORT_FILE"
jq '{ok,sha,startedAt,finishedAt,checks:[.checks[]|{id,name,status}],shots}' "$REPORT_FILE"

if [[ "$OK" == "true" ]]; then
  exit 0
fi
exit 1
